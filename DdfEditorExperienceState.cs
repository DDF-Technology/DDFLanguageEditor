using System;

namespace DDF___Program_Language_Editor
{
    public interface IDdfEditorExperienceState
    {
        bool EducationalModeEnabled { get; }
        event EventHandler<DdfEducationalModeChangedEventArgs> EducationalModeChanged;
        void SetEducationalMode(bool enabled);
    }

    public sealed class DdfEducationalModeChangedEventArgs : EventArgs
    {
        public DdfEducationalModeChangedEventArgs(bool enabled)
        {
            Enabled = enabled;
        }

        public bool Enabled { get; }
    }

    /// <summary>
    /// Presentation state owned by one editor instance. It has no persistence or
    /// execution side effects and can be shared with an embedded FlowchartView.
    /// </summary>
    public sealed class DdfEditorExperienceState : IDdfEditorExperienceState
    {
        public bool EducationalModeEnabled { get; private set; }

        public event EventHandler<DdfEducationalModeChangedEventArgs> EducationalModeChanged;

        public void SetEducationalMode(bool enabled)
        {
            if (EducationalModeEnabled == enabled) return;
            EducationalModeEnabled = enabled;
            EducationalModeChanged?.Invoke(this, new DdfEducationalModeChangedEventArgs(enabled));
        }
    }
}
