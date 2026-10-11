using System;
using System.Collections.Generic;
using System.Linq;

namespace DDF___Program_Language_Editor
{
    public static class DdfEditorCommandIds
    {
        public const string NewDocument = "file.new";
        public const string OpenDocument = "file.open";
        public const string OpenWorkspace = "file.openWorkspace";
        public const string SaveDocument = "file.save";
        public const string SaveDocumentAs = "file.saveAs";
        public const string Undo = "edit.undo";
        public const string Redo = "edit.redo";
        public const string Cut = "edit.cut";
        public const string Copy = "edit.copy";
        public const string Paste = "edit.paste";
        public const string SelectAll = "edit.selectAll";
        public const string DeleteSelection = "edit.deleteSelection";
        public const string Find = "edit.find";
        public const string Replace = "edit.replace";
        public const string Completion = "edit.completion";
        public const string FormatDocument = "edit.formatDocument";
        public const string AnalyzeDocument = "analysis.refresh";
        public const string GoToDefinition = "navigate.goToDefinition";
        public const string ValidateScript = "script.validate";
        public const string PrepareScript = "script.prepare";
        public const string RunWithoutDebug = "run.startWithoutDebug";
        public const string StopExecution = "run.stop";
        public const string StartDebugging = "debug.start";
        public const string ContinueDebugging = "debug.continue";
        public const string StepInto = "debug.stepInto";
        public const string StepOver = "debug.stepOver";
        public const string StepOut = "debug.stepOut";
        public const string StopDebugging = "debug.stop";
        public const string ToggleBreakpoint = "debug.toggleBreakpoint";
        public const string ToggleFold = "view.toggleFold";
        public const string ExpandAllFolds = "view.expandAllFolds";
        public const string ShowCommandPalette = "view.commandPalette";
        public const string ToggleWordWrap = "view.toggleWordWrap";
        public const string FocusEditor = "editor.focus";
        public const string SaveHostState = "persistence.saveState";
        public const string RestoreHostState = "persistence.restoreState";
        public const string SaveHostProject = "project.save";
        public const string OpenHostProject = "project.open";
    }

    public interface IDdfEditorCommand
    {
        string Id { get; }
        string Title { get; }
        string Category { get; }
        string Description { get; }
        string ShortcutText { get; }
        bool CanExecute { get; }
        bool IsVisible { get; }
        bool IsChecked { get; }
    }

    public enum DdfEditorCommandChangeKind
    {
        Registered,
        StateChanged,
        Removed
    }

    public sealed class DdfEditorCommandChangedEventArgs : EventArgs
    {
        public DdfEditorCommandChangedEventArgs(IDdfEditorCommand command,
            DdfEditorCommandChangeKind changeKind)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            ChangeKind = changeKind;
        }

        public IDdfEditorCommand Command { get; }
        public DdfEditorCommandChangeKind ChangeKind { get; }
    }

    public interface IDdfEditorCommandService
    {
        IReadOnlyList<IDdfEditorCommand> Commands { get; }
        event EventHandler<DdfEditorCommandChangedEventArgs> CommandChanged;

        IDdfEditorCommand Register(string id, string title, string category,
            Action execute, Func<bool> canExecute = null, Func<bool> isVisible = null,
            Func<bool> isChecked = null, string description = null,
            string shortcutText = null, Func<string> shortcutTextProvider = null);
        IDdfEditorCommand Find(string id);
        bool TryExecute(string id);
        bool Remove(string id);
        void Refresh();
    }

    /// <summary>
    /// Per-instance command registry. Delegates are evaluated only when state is
    /// queried or refreshed; the catalog does not depend on menus or toolbars.
    /// </summary>
    public sealed class DdfEditorCommandCatalog : IDdfEditorCommandService
    {
        private sealed class Command : IDdfEditorCommand
        {
            private readonly Action execute;
            private readonly Func<bool> canExecute;
            private readonly Func<bool> isVisible;
            private readonly Func<bool> isChecked;
            private readonly string shortcutText;
            private readonly Func<string> shortcutTextProvider;

            public Command(string id, string title, string category, Action execute,
                Func<bool> canExecute, Func<bool> isVisible, Func<bool> isChecked,
                string description, string shortcutText, Func<string> shortcutTextProvider)
            {
                Id = id;
                Title = title;
                Category = category;
                Description = description;
                this.shortcutText = shortcutText;
                this.shortcutTextProvider = shortcutTextProvider;
                this.execute = execute;
                this.canExecute = canExecute;
                this.isVisible = isVisible;
                this.isChecked = isChecked;
                CaptureState();
            }

            public string Id { get; }
            public string Title { get; }
            public string Category { get; }
            public string Description { get; }
            public string ShortcutText => shortcutTextProvider?.Invoke() ?? shortcutText;
            public bool CanExecute => canExecute?.Invoke() ?? true;
            public bool IsVisible => isVisible?.Invoke() ?? true;
            public bool IsChecked => isChecked?.Invoke() ?? false;
            public bool LastCanExecute { get; private set; }
            public bool LastIsVisible { get; private set; }
            public bool LastIsChecked { get; private set; }
            public string LastShortcutText { get; private set; }

            public bool TryExecute()
            {
                if (!IsVisible || !CanExecute) return false;
                execute();
                return true;
            }

            public bool CaptureState()
            {
                bool nextCanExecute = CanExecute;
                bool nextIsVisible = IsVisible;
                bool nextIsChecked = IsChecked;
                string nextShortcutText = ShortcutText ?? string.Empty;
                bool changed = nextCanExecute != LastCanExecute ||
                               nextIsVisible != LastIsVisible ||
                               nextIsChecked != LastIsChecked ||
                               !string.Equals(nextShortcutText, LastShortcutText, StringComparison.Ordinal);
                LastCanExecute = nextCanExecute;
                LastIsVisible = nextIsVisible;
                LastIsChecked = nextIsChecked;
                LastShortcutText = nextShortcutText;
                return changed;
            }
        }

        private readonly List<Command> commands = new List<Command>();
        private readonly Dictionary<string, Command> commandsById =
            new Dictionary<string, Command>(StringComparer.Ordinal);

        public IReadOnlyList<IDdfEditorCommand> Commands =>
            commands.Cast<IDdfEditorCommand>().ToList().AsReadOnly();

        public event EventHandler<DdfEditorCommandChangedEventArgs> CommandChanged;

        public IDdfEditorCommand Register(string id, string title, string category,
            Action execute, Func<bool> canExecute = null, Func<bool> isVisible = null,
            Func<bool> isChecked = null, string description = null,
            string shortcutText = null, Func<string> shortcutTextProvider = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Command id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Command title is required.", nameof(title));
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Command category is required.", nameof(category));
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            if (commandsById.ContainsKey(id))
                throw new InvalidOperationException("A command with id '" + id + "' is already registered.");

            var command = new Command(id, title.Trim(), category.Trim(), execute,
                canExecute, isVisible, isChecked, description ?? string.Empty,
                shortcutText ?? string.Empty, shortcutTextProvider);
            commands.Add(command);
            commandsById.Add(command.Id, command);
            CommandChanged?.Invoke(this, new DdfEditorCommandChangedEventArgs(
                command, DdfEditorCommandChangeKind.Registered));
            return command;
        }

        public IDdfEditorCommand Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return commandsById.TryGetValue(id, out Command command) ? command : null;
        }

        public bool TryExecute(string id)
        {
            if (!commandsById.TryGetValue(id ?? string.Empty, out Command command)) return false;
            bool executed = command.TryExecute();
            if (command.CaptureState())
                CommandChanged?.Invoke(this, new DdfEditorCommandChangedEventArgs(
                    command, DdfEditorCommandChangeKind.StateChanged));
            return executed;
        }

        public bool Remove(string id)
        {
            if (!commandsById.TryGetValue(id ?? string.Empty, out Command command)) return false;
            commandsById.Remove(id);
            commands.Remove(command);
            CommandChanged?.Invoke(this, new DdfEditorCommandChangedEventArgs(
                command, DdfEditorCommandChangeKind.Removed));
            return true;
        }

        public void Refresh()
        {
            foreach (Command command in commands)
            {
                if (command.CaptureState())
                    CommandChanged?.Invoke(this, new DdfEditorCommandChangedEventArgs(
                        command, DdfEditorCommandChangeKind.StateChanged));
            }
        }
    }
}
