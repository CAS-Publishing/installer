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
        // Watchdog: a step that hasn't resolved this long after its install was issued is treated as
        // stalled (failed git clone / registry download, or a PackageManager lock) and surfaced to the
        // user instead of spinning forever. Generous so a slow-but-working clone resolves first — the
        // watchdog only fires while the step is STILL unresolved.
        private const double StepTimeoutSeconds = 150;
        // A Client.List request that never completes (PM busy / lock contention) would freeze the whole
        // driver, since Drive only runs once a list completes. Abandon and retry a request older than this.
        private const double ListRequestTimeoutSeconds = 25;
        private const string LogPrefix = "[PSV Installer]";
        private ListRequest _listReq;
        private double _listReqStartedAt; // when the in-flight _listReq was issued (stuck-request guard)
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
                _listReqStartedAt = EditorApplication.timeSinceStartup;
                return;
            }
            if (!_listReq.IsCompleted)
            {
                // Don't let a single stuck list request freeze the driver forever — abandon it and the
                // next tick re-issues a fresh one.
                if (EditorApplication.timeSinceStartup - _listReqStartedAt > ListRequestTimeoutSeconds)
                {
                    Debug.LogWarning($"{LogPrefix} Package list request stalled (>{ListRequestTimeoutSeconds}s); retrying.");
                    _listReq = null;
                }
                return;
            }

            var resolved = new HashSet<string>();
            if (_listReq.Status == StatusCode.Success && _listReq.Result != null)
                foreach (var p in _listReq.Result)
                    resolved.Add(p.name);
            else if (_listReq.Status == StatusCode.Failure)
                Debug.LogWarning($"{LogPrefix} Package list failed: {_listReq.Error?.message}");
            _listReq = null;

            Drive(resolved);
        }

        /// <summary>Renders the current state and advances the install sequence.</summary>
        private void Drive(HashSet<string> resolved)
        {
            // First component not yet resolved is the "current" step.
            var next = AutoInstallProgress.FirstUnresolved(_targetIds, resolved);

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
                var id = _targetIds[next];
                var result = AutoInstaller.InstallOne(id);
                AutoInstaller.IssuedIndex = next;

                if (!result.Success)
                {
                    FailStep(id,
                        "Install failed:\n• " + string.Join("\n• ", result.Failures) +
                        "\n\nNo backup is kept — use 'git restore .' to inspect or revert.");
                    return;
                }

                // The manifest write is synchronous, so the dependency must be present NOW. If it
                // isn't, the plan added nothing for this component (e.g. it's detected as already
                // present, or the catalog has no source for the chosen install method) — the poll
                // would otherwise wait forever for a package that can never resolve. Surface it.
                if (!AutoInstaller.ManifestHasDependency(id))
                {
                    FailStep(id,
                        "The installer didn't add a manifest entry for this component, so it can't " +
                        "resolve.\n\nLikely causes:\n" +
                        "• it's detected as already present (leftover assets or types in the project), or\n" +
                        "• its catalog source for the selected install method (UPM / Git) is missing.\n\n" +
                        "See the Console for the planned actions, then install it from the Components tab.");
                    return;
                }

                // Arm the per-step watchdog: from here we're waiting on UPM to resolve the dependency.
                AutoInstaller.StepDeadline = EditorApplication.timeSinceStartup + StepTimeoutSeconds;
                Debug.Log($"{LogPrefix} Issued install for '{id}' — waiting up to {StepTimeoutSeconds}s to resolve.");
                // On success: manifest written + resolve queued. The reload re-enters this screen,
                // and the next poll sees this component resolved and moves on.
            }
            else
            {
                // Current step already issued — waiting for UPM to resolve it. Enforce the watchdog so
                // a failed/stuck resolve becomes an actionable prompt instead of an endless spinner.
                if (AutoInstallProgress.IsStepOverdue(EditorApplication.timeSinceStartup, AutoInstaller.StepDeadline))
                    WatchdogTimeout(_targetIds[next]);
            }
        }

        // A hard failure (apply failed, or nothing landed in the manifest): stop, surface, and drop
        // back to the manual Components tab so the user can retry per component.
        private void FailStep(string id, string detail)
        {
            StopPoll();
            AutoInstaller.Clear();
            EditorUtility.DisplayDialog("PSV Installer",
                $"Couldn't install {NameOf(id)}.\n\n{detail}", "OK");
            _router.GoTo("components");
        }

        // The step was issued and written to the manifest but hasn't resolved within the deadline.
        // It may still be downloading/cloning (slow network, Git LFS) or it may have failed — let the
        // user keep waiting or stop, rather than guessing and aborting a working-but-slow install.
        private void WatchdogTimeout(string id)
        {
            StopPoll();
            var keepWaiting = EditorUtility.DisplayDialog("PSV Installer",
                $"{NameOf(id)} is taking longer than expected to install " +
                $"({Mathf.RoundToInt((float)StepTimeoutSeconds)}s).\n\n" +
                "It may still be downloading or cloning, or it may have failed. Check the Console for " +
                "Package Manager errors (git URL, version, network, or Git LFS).\n\n" +
                "Keep waiting, or stop and finish on the Components tab?",
                "Keep waiting", "Stop");

            if (keepWaiting)
            {
                AutoInstaller.StepDeadline = EditorApplication.timeSinceStartup + StepTimeoutSeconds;
                StartPoll();
            }
            else
            {
                AutoInstaller.Clear();
                _router.GoTo("components");
            }
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
