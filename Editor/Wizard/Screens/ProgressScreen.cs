using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Installation Progress. In auto-install mode (set by <see cref="AutoInstaller"/>) it drives
    /// a step-by-step install: poll the real UPM package list, and as each default component
    /// resolves, kick off the next one — surviving the per-step domain reloads — then route to
    /// Done. Outside auto mode it shows a static stub (dev/manual preview).
    /// </summary>
    internal sealed class ProgressScreen : IWizardScreen
    {
        public string Id => "progress";
        public VisualElement Root { get; }

        private WizardRouter _router;
        private bool _bound;

        private readonly VisualElement _stepsHost;
        private readonly VisualElement _fill;
        private readonly Label _pct;

        // Auto-install tracking.
        private const double StepPauseSeconds = 0.7; // breathing room so the UI paints between steps
        private ListRequest _listReq;
        private IVisualElementScheduledItem _poll;
        private List<string> _targetIds;
        private Dictionary<string, string> _targetNames;
        private double _issueAtTime; // when the next install may be issued (0 = not scheduled)

        public ProgressScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Progress", Root);
            _stepsHost = Root.Q<VisualElement>("progress-steps");
            _fill      = Root.Q<VisualElement>("progress-fill");
            _pct       = Root.Q<Label>("progress-pct");
        }

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (!_bound)
            {
                _bound = true;
                var cancel = Root.Q<Button>("progress-cancel");
                if (cancel != null) cancel.clicked += OnCancel;
            }

            if (AutoInstaller.IsActive)
                EnterAutoMode();
            else
                EnterStubMode();
        }

        private void OnCancel()
        {
            StopPoll();
            if (AutoInstaller.IsActive) AutoInstaller.Clear();
            _router.GoTo("components");
        }

        // ── Auto-install mode (real, step by step) ───────────────────────────

        private void EnterAutoMode()
        {
            if (_targetIds == null)
            {
                _targetIds = new List<string>();
                _targetNames = new Dictionary<string, string>();
                if (ComponentStatusProvider.TryGetStatuses(out var statuses, out _))
                {
                    foreach (var s in statuses)
                    {
                        // A non-UPM copy is already on disk — it will never appear in the UPM
                        // package list, so polling for it would stall the sequence forever.
                        // Skip it (it's also skipped by the planner, so it's never installed here).
                        if (s.OutsideUpm) continue;
                        _targetIds.Add(s.Id);
                        _targetNames[s.Id] = s.DisplayName;
                    }
                }
            }

            if (_targetIds.Count == 0)
            {
                _stepsHost?.Clear();
                var note = new Label("Couldn't read components — is the catalog installed?");
                note.AddToClassList("cas-empty-note");
                _stepsHost?.Add(note);
                return;
            }

            Render(new HashSet<string>(), currentIndex: 0);
            StartPoll();
        }

        private void StartPoll()
        {
            if (_poll == null)
                _poll = Root.schedule.Execute(PollOnce).Every(400);
            else
                _poll.Resume();
        }

        private void StopPoll()
        {
            _poll?.Pause();
            _listReq = null;
        }

        private void PollOnce()
        {
            if (!AutoInstaller.IsActive) { StopPoll(); return; }
            if (Root.resolvedStyle.display == DisplayStyle.None) return;

            if (_listReq == null)
            {
                _listReq = Client.List(true, false); // offlineMode, includeIndirectDependencies
                return;
            }
            if (!_listReq.IsCompleted) return;

            var resolved = new HashSet<string>();
            if (_listReq.Status == StatusCode.Success && _listReq.Result != null)
                foreach (var p in _listReq.Result)
                    resolved.Add(p.name);
            _listReq = null;

            Drive(resolved);
        }

        /// <summary>Renders the current state and advances the install sequence.</summary>
        private void Drive(HashSet<string> resolved)
        {
            var total = _targetIds.Count;

            // First component not yet resolved is the "current" step.
            var next = -1;
            for (var i = 0; i < total; i++)
                if (!resolved.Contains(_targetIds[i])) { next = i; break; }

            Render(resolved, next);

            if (next < 0)
            {
                // Everything resolved → write any captured CAS IDs into the settings asset, then done.
                StopPoll();
                AutoInstaller.Clear();
                CasIdApplier.ApplyPending();
                _router.GoTo("done");
                return;
            }

            // Kick off the current step's install if we haven't already issued it — but pause
            // first so the UI paints the current state (e.g. "X installed, Y installing…")
            // before InstallOne freezes the editor for its resolve/recompile.
            if (AutoInstaller.IssuedIndex < next)
            {
                if (_issueAtTime <= 0)
                {
                    _issueAtTime = EditorApplication.timeSinceStartup + StepPauseSeconds;
                    return; // wait — the next poll ticks will keep the UI repainted during the pause
                }
                if (EditorApplication.timeSinceStartup < _issueAtTime)
                    return;

                _issueAtTime = 0;
                var result = AutoInstaller.InstallOne(_targetIds[next]);
                AutoInstaller.IssuedIndex = next;

                if (!result.Success)
                {
                    StopPoll();
                    AutoInstaller.Clear();
                    EditorUtility.DisplayDialog("PSV Installer",
                        $"Install failed for {NameOf(_targetIds[next])}:\n• " +
                        string.Join("\n• ", result.Failures) +
                        "\n\nNo backup is kept — use 'git restore .' to inspect or revert.", "OK");
                    _router.GoTo("components");
                }
                // On success: manifest written + resolve queued. The reload re-enters this screen,
                // and the next poll sees this component resolved and moves on.
            }
            // else: current step already issued — keep polling until it resolves.
        }

        private void Render(HashSet<string> resolved, int currentIndex)
        {
            if (_stepsHost == null) return;
            _stepsHost.Clear();

            var done = 0;
            for (var i = 0; i < _targetIds.Count; i++)
            {
                var id = _targetIds[i];
                string state;
                if (resolved.Contains(id)) { state = "done"; done++; }
                else if (i == currentIndex)  state = "active";
                else                          state = "wait";
                _stepsHost.Add(BuildStep(NameOf(id), state));
            }

            var pctVal = _targetIds.Count > 0 ? Mathf.RoundToInt(done * 100f / _targetIds.Count) : 0;
            if (_fill != null) _fill.style.width = Length.Percent(pctVal);
            if (_pct != null) _pct.text = pctVal + "%";
        }

        private string NameOf(string id) => _targetNames.TryGetValue(id, out var n) ? n : id;

        // ── Stub mode (dev / manual preview) ─────────────────────────────────

        private void EnterStubMode()
        {
            StopPoll();
            if (_stepsHost == null) return;

            _stepsHost.Clear();
            foreach (var step in StubData.ProgressSteps)
                _stepsHost.Add(BuildStep(step.Label, step.State, step.Right));

            if (_fill != null) _fill.style.width = Length.Percent(StubData.ProgressPercent);
            if (_pct != null) _pct.text = StubData.ProgressPercent + "%";
        }

        // ── Shared step row ──────────────────────────────────────────────────

        private static VisualElement BuildStep(string label, string state, string activeRight = null)
        {
            var row = new VisualElement();
            row.AddToClassList("cas-step");

            var icons = new VisualElement();
            icons.AddToClassList("cas-step__icongroup");
            if (state == "done")
            {
                var c = new Label("✓");
                c.AddToClassList("cas-checkmark");
                icons.Add(c);
            }
            else if (state == "active")
            {
                var sp = new VisualElement();
                sp.AddToClassList("cas-spinner");
                icons.Add(sp);
            }
            else
            {
                var d = new VisualElement();
                d.AddToClassList("cas-wait-dot");
                icons.Add(d);
            }

            var nameLabel = new Label(label);
            nameLabel.AddToClassList("cas-step__label");
            if (state == "wait") nameLabel.AddToClassList("cas-step__label--wait");
            row.Add(icons);
            row.Add(nameLabel);

            if (state == "active")
            {
                var r = new Label(string.IsNullOrEmpty(activeRight) ? "Installing…" : activeRight);
                r.AddToClassList("cas-step__right");
                row.Add(r);
            }
            else if (state == "wait")
            {
                var r = new Label("Waiting");
                r.AddToClassList("cas-step__right");
                r.AddToClassList("cas-step__right--wait");
                row.Add(r);
            }
            else if (state == "done")
            {
                var r = new Label("Installed");
                r.AddToClassList("cas-step__right");
                row.Add(r);
            }

            return row;
        }
    }
}
