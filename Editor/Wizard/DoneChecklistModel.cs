namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Pure copy/tone rules for the Done screen's 3-row checklist (CAS SDK / Tenjin / Firebase).
    /// Kept separate from <see cref="Screens.DoneScreen"/> so the string logic is unit-testable
    /// without touching Unity APIs — the screen itself only does live detection (CasSettingsReader,
    /// TenjinKeyDetect, SetupModel) and hands the results in here.
    /// </summary>
    internal static class DoneChecklistModel
    {
        /// <summary>One checklist row: display text (already includes the ✓/⚠ glyph) and whether it
        /// should render in the warning (yellow) tone instead of the normal (green) one.</summary>
        internal readonly struct Line
        {
            public readonly string Text;
            public readonly bool Warn;

            public Line(string text, bool warn)
            {
                Text = text;
                Warn = warn;
            }
        }

        /// <summary>
        /// CAS SDK row. <paramref name="managerId"/> is the value read for the active platform via
        /// <see cref="CasSettingsReader.ReadExisting"/> — null/empty (not yet set, or CAS not installed)
        /// drops the parenthetical id from the line. Always green: Done is only reached after the
        /// intro install flow, so CAS SDK itself is present.
        /// </summary>
        internal static Line CasLine(string managerId) =>
            string.IsNullOrEmpty(managerId)
                ? new Line("✓ CAS SDK — mediation ready", false)
                : new Line($"✓ CAS SDK — mediation ready ({managerId})", false);

        /// <summary>
        /// Tenjin row from a <see cref="TenjinKeyProbe"/>. Older CAS versions without the key field
        /// are informational only ("handled on our end"); newer versions need a non-empty key or the
        /// row warns (the developer can still fix it later via Components/CAS Settings).
        /// </summary>
        internal static Line TenjinLine(bool fieldSupported, string key)
        {
            if (!fieldSupported) return new Line("✓ Tenjin — handled on our end", false);
            return string.IsNullOrEmpty(key)
                ? new Line("⚠ Tenjin — attribution key missing", true)
                : new Line("✓ Tenjin — attribution key configured", false);
        }

        /// <summary>Firebase row: green when the active platform's config file/field checks all
        /// come back Configured (or NotApplicable), yellow otherwise.</summary>
        internal static Line FirebaseLine(bool configured) =>
            configured
                ? new Line("✓ Firebase — analytics connected", false)
                : new Line("⚠ Firebase — configuration file missing", true);
    }
}
