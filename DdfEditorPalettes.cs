using System;
using System.Collections.Generic;
using System.Linq;

namespace DDF___Program_Language_Editor
{
    public enum DdfEditorPalettePlacement
    {
        Sidebar,
        Bottom,
        Floating
    }

    public enum DdfEditorPaletteChangeKind
    {
        Registered,
        Visibility,
        Activation,
        Unregistered
    }

    public interface IDdfEditorPalette
    {
        string Id { get; }
        string DisplayName { get; }
        DdfEditorPalettePlacement Placement { get; }
        bool CanHide { get; }
        bool IsVisible { get; }
        bool IsActive { get; }
    }

    public interface IDdfEditorPaletteService
    {
        IReadOnlyList<IDdfEditorPalette> Palettes { get; }
        event EventHandler<DdfEditorPaletteChangedEventArgs> PaletteChanged;

        IDdfEditorPalette Register(string id, string displayName,
            DdfEditorPalettePlacement placement, bool visible = true, bool canHide = true);
        bool SetVisible(string id, bool visible);
        bool Activate(string id);
        bool Unregister(string id);
        IDdfEditorPalette Find(string id);
    }

    public sealed class DdfEditorPaletteChangedEventArgs : EventArgs
    {
        public DdfEditorPaletteChangedEventArgs(IDdfEditorPalette palette,
            DdfEditorPaletteChangeKind changeKind)
        {
            Palette = palette;
            ChangeKind = changeKind;
        }

        public IDdfEditorPalette Palette { get; }
        public DdfEditorPaletteChangeKind ChangeKind { get; }
    }

    public static class DdfEditorPaletteIds
    {
        public const string Workspace = "workspace";
        public const string Flowchart = "flowchart";
        public const string Outline = "outline";
        public const string HostApi = "host-api";
        public const string Diagnostics = "diagnostics";
        public const string Output = "output";
        public const string Breakpoints = "breakpoints";
        public const string Variables = "variables";
        public const string CallStack = "call-stack";
        public const string Watch = "watch";
        public const string WorkspaceSearch = "workspace-search";
    }

    /// <summary>
    /// UI-independent state catalog used by both embedded hosts and the standalone
    /// shell. It describes availability and intent; concrete controls remain private
    /// to the composing application.
    /// </summary>
    public sealed class DdfEditorPaletteCatalog : IDdfEditorPaletteService
    {
        private readonly List<PaletteState> palettes = new List<PaletteState>();

        public IReadOnlyList<IDdfEditorPalette> Palettes => palettes;

        public event EventHandler<DdfEditorPaletteChangedEventArgs> PaletteChanged;

        public IDdfEditorPalette Register(string id, string displayName,
            DdfEditorPalettePlacement placement, bool visible = true, bool canHide = true)
        {
            string normalizedId = NormalizeRequired(id, nameof(id));
            if (palettes.Any(item => string.Equals(item.Id, normalizedId, StringComparison.Ordinal)))
                throw new ArgumentException("Esiste già una palette con identificativo '" + normalizedId + "'.", nameof(id));
            var palette = new PaletteState(normalizedId,
                NormalizeRequired(displayName, nameof(displayName)), placement, visible, canHide);
            palettes.Add(palette);
            PaletteChanged?.Invoke(this, new DdfEditorPaletteChangedEventArgs(
                palette, DdfEditorPaletteChangeKind.Registered));
            return palette;
        }

        public bool SetVisible(string id, bool visible)
        {
            PaletteState palette = FindState(id);
            if (palette == null || (!visible && !palette.CanHide)) return false;
            if (palette.IsVisible == visible) return true;
            palette.IsVisible = visible;
            if (!visible) palette.IsActive = false;
            PaletteChanged?.Invoke(this, new DdfEditorPaletteChangedEventArgs(
                palette, DdfEditorPaletteChangeKind.Visibility));
            return true;
        }

        public bool Activate(string id)
        {
            PaletteState palette = FindState(id);
            if (palette == null) return false;
            if (!palette.IsVisible) SetVisible(id, true);
            foreach (PaletteState peer in palettes.Where(item => item.Placement == palette.Placement))
                peer.IsActive = ReferenceEquals(peer, palette);
            PaletteChanged?.Invoke(this, new DdfEditorPaletteChangedEventArgs(
                palette, DdfEditorPaletteChangeKind.Activation));
            return true;
        }

        public bool Unregister(string id)
        {
            PaletteState palette = FindState(id);
            if (palette == null) return false;
            palettes.Remove(palette);
            PaletteChanged?.Invoke(this, new DdfEditorPaletteChangedEventArgs(
                palette, DdfEditorPaletteChangeKind.Unregistered));
            return true;
        }

        public IDdfEditorPalette Find(string id) => FindState(id);

        private PaletteState FindState(string id) => palettes.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.Ordinal));

        private static string NormalizeRequired(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Il valore non può essere vuoto.", parameterName);
            return value.Trim();
        }

        private sealed class PaletteState : IDdfEditorPalette
        {
            public PaletteState(string id, string displayName,
                DdfEditorPalettePlacement placement, bool visible, bool canHide)
            {
                Id = id;
                DisplayName = displayName;
                Placement = placement;
                CanHide = canHide;
                IsVisible = visible || !canHide;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public DdfEditorPalettePlacement Placement { get; }
            public bool CanHide { get; }
            public bool IsVisible { get; internal set; }
            public bool IsActive { get; internal set; }
        }
    }
}
