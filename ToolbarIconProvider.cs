using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    // Geometry is rendered at twice the displayed size; no private-use font glyphs are required.
    internal static class ToolbarIconProvider
    {
        public static Image Create(string name)
        {
            var image = new Bitmap(40, 40);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                graphics.ScaleTransform(2f, 2f);
                Color ink = SystemInformation.HighContrast ? SystemColors.MenuText : AppTheme.Text;
                Color accent = SystemInformation.HighContrast ? SystemColors.Highlight : AppTheme.Accent;
                Color run = SystemInformation.HighContrast ? SystemColors.MenuText : AppTheme.Run;
                Color stop = SystemInformation.HighContrast ? SystemColors.MenuText : AppTheme.Stop;
                using (var p = new Pen(ink, 1.55f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                using (var a = new Pen(accent, 1.65f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    Draw(graphics, name, p, a, ink, accent, run, stop);
                }
            }
            return image;
        }

        private static void Draw(Graphics g, string name, Pen p, Pen a, Color ink, Color accent, Color run, Color stop)
        {
            switch (name)
            {
                case "toolbarNewButton": Document(g, p); Plus(g, a, 14, 14); break;
                case "toolbarOpenButton": Folder(g, p); Arrow(g, a, 10, 5, 10, 11); break;
                case "toolbarSaveButton": Disk(g, p); break;
                case "toolbarSaveAllButton": Disk(g, p); Line(g, a, 3, 15, 3, 18); Line(g, a, 3, 18, 16, 18); break;
                case "toolbarCloseDocumentButton": Document(g, p); Cross(g, a, 11, 12, 3); break;
                case "toolbarUndoButton": CurvedArrow(g, p, false); break;
                case "toolbarRedoButton": CurvedArrow(g, p, true); break;
                case "toolbarCutButton": Scissors(g, p); break;
                case "toolbarCopyButton": Document(g, p); g.DrawRectangle(a, 6, 6, 10, 11); break;
                case "toolbarPasteButton": g.DrawRectangle(p, 4, 4, 12, 14); g.DrawRectangle(a, 7, 2, 6, 4); Line(g, p, 7, 10, 13, 10); Line(g, p, 7, 13, 12, 13); break;
                case "toolbarCommentButton": Text(g, "//", ink, 12, 3, 2); break;
                case "toolbarDuplicateLinesButton": Lines(g, p, 4, 6, 10); Lines(g, a, 7, 11, 10); break;
                case "toolbarMoveLinesUpButton": Lines(g, p, 5, 10, 11); Arrow(g, a, 10, 8, 10, 2); break;
                case "toolbarMoveLinesDownButton": Lines(g, p, 5, 4, 11); Arrow(g, a, 10, 11, 10, 18); break;
                case "toolbarDeleteLinesButton": Lines(g, p, 4, 6, 12); Cross(g, a, 13, 13, 3); break;
                case "toolbarExpandSelectionButton": Brackets(g, p); Arrow(g, a, 10, 10, 16, 10); break;
                case "toolbarShrinkSelectionButton": Brackets(g, p); Arrow(g, a, 15, 10, 8, 10); break;
                case "toolbarSelectNextOccurrenceButton": g.DrawRectangle(p, 2, 5, 7, 9); g.DrawRectangle(a, 11, 5, 7, 9); Arrow(g, a, 8, 17, 15, 17); break;
                case "toolbarSelectAllOccurrencesButton": g.DrawRectangle(p, 2, 4, 6, 6); g.DrawRectangle(a, 12, 4, 6, 6); g.DrawRectangle(a, 7, 12, 6, 6); break;
                case "toolbarMatchingDelimiterButton": Text(g, "{}", ink, 14, 2, 1); break;
                case "toolbarCompletionButton": Text(g, "</>", ink, 10, 1, 3); Plus(g, a, 16, 4); break;
                case "toolbarQuickFixButton": Bolt(g, a); break;
                case "toolbarFormatButton": Lines(g, p, 3, 4, 13); Line(g, a, 6, 16, 15, 16); Arrow(g, a, 6, 12, 6, 16); break;
                case "toolbarFoldButton": Brackets(g, p); Line(g, a, 8, 10, 13, 10); break;
                case "toolbarFindButton": Magnifier(g, p); break;
                case "toolbarWorkspaceSearchButton": Folder(g, p); Magnifier(g, a, 11, 12, 4); break;
                case "toolbarWorkspaceReplaceButton": Folder(g, p); Text(g, "R", accent, 10, 10, 6); break;
                case "toolbarNavigateBackButton": Arrow(g, p, 17, 10, 3, 10); break;
                case "toolbarNavigateForwardButton": Arrow(g, p, 3, 10, 17, 10); break;
                case "toolbarGoToFileButton": Document(g, p); Arrow(g, a, 7, 13, 13, 13); break;
                case "toolbarGoToSymbolButton": Text(g, "@", ink, 16, 1, 0); break;
                case "toolbarFindReferencesButton": Magnifier(g, p); Text(g, "#", accent, 8, 8, 10); break;
                case "toolbarGoToLineButton": Lines(g, p, 5, 5, 10); Text(g, "#", accent, 8, 8, 10); break;
                case "toolbarGoToLastEditButton": Document(g, p); Pencil(g, a); break;
                case "toolbarCommandPaletteButton": g.DrawRectangle(p, 2, 4, 16, 12); Text(g, ">", accent, 12, 4, 4); break;
                case "toolbarHighlightCurrentLineButton": Lines(g, p, 3, 5, 14); Line(g, a, 3, 10, 17, 10); break;
                case "toolbarShowWhitespaceButton": g.DrawEllipse(a, 3, 7, 2, 2); g.DrawEllipse(a, 8, 7, 2, 2); Arrow(g, p, 16, 5, 16, 13); break;
                case "toolbarShowIndentGuidesButton": Line(g, a, 5, 2, 5, 18); Lines(g, p, 9, 5, 8); break;
                case "toolbarWordWrapButton": Line(g, p, 2, 5, 17, 5); Line(g, p, 2, 9, 17, 9); Arrow(g, a, 17, 13, 8, 13); break;
                case "toolbarOverviewButton": g.DrawRectangle(p, 4, 2, 12, 16); Line(g, a, 12, 4, 12, 16); Line(g, a, 5, 8, 11, 8); break;
                case "toolbarSettingsButton": Gear(g, p); break;
                case "toolbarValidateButton": Document(g, p); Check(g, a, 8, 13); break;
                case "toolbarBuildButton": Hammer(g, p); break;
                case "toolbarCompileAndRunButton": Hammer(g, p); Play(g, run, 13, 11, 5); break;
                case "toolbarRunLastBuildButton": Disk(g, p); Play(g, run, 12, 11, 5); break;
                case "toolbarPublishExecutableButton": g.DrawRectangle(p, 3, 6, 14, 11); Arrow(g, a, 10, 2, 10, 12); break;
                case "toolbarOpenCompiledOutputButton": Folder(g, p); Text(g, ">", accent, 8, 7, 10); break;
                case "toolbarLanguageManagerButton": Globe(g, p); Plus(g, a, 16, 15); break;
                case "toolbarLanguageStudioButton": Globe(g, p); Pencil(g, a); break;
                case "toolbarLanguagePlaygroundButton": Globe(g, p); Play(g, run, 12, 12, 5); break;
                case "toolbarLanguageButton": Globe(g, p); break;
                case "toolbarModuleManagerButton": g.DrawRectangle(p, 3, 4, 10, 10); g.DrawRectangle(a, 8, 9, 10, 9); break;
                case "toolbarRunWithoutDebugButton": Play(g, run, 7, 10, 12); break;
                case "toolbarBreakpointButton": using (var b = new SolidBrush(stop)) g.FillEllipse(b, 4, 4, 12, 12); break;
                case "toolbarRunButton": Play(g, run, 5, 10, 10); g.DrawEllipse(a, 12, 10, 6, 6); break;
                case "toolbarStepIntoButton": Arrow(g, a, 10, 3, 10, 13); Line(g, p, 3, 17, 17, 17); break;
                case "toolbarStepOverButton": Line(g, p, 3, 16, 17, 16); Arrow(g, a, 4, 10, 16, 10); break;
                case "toolbarStepOutButton": Line(g, p, 3, 15, 17, 15); Arrow(g, a, 10, 13, 10, 3); break;
                case "toolbarStopButton": using (var b = new SolidBrush(stop)) g.FillRectangle(b, 5, 5, 10, 10); break;
                default: throw new ArgumentOutOfRangeException(nameof(name), name, "Icona toolbar non definita.");
            }
        }

        private static void Line(Graphics g, Pen p, float x1, float y1, float x2, float y2) => g.DrawLine(p, x1, y1, x2, y2);
        private static void Document(Graphics g, Pen p) { g.DrawRectangle(p, 4, 2, 11, 16); Line(g, p, 7, 7, 12, 7); Line(g, p, 7, 10, 12, 10); }
        private static void Folder(Graphics g, Pen p) { g.DrawLines(p, new[] { new Point(2, 6), new Point(7, 6), new Point(9, 8), new Point(18, 8), new Point(17, 17), new Point(3, 17), new Point(2, 6) }); }
        private static void Disk(Graphics g, Pen p) { g.DrawRectangle(p, 3, 3, 14, 14); g.DrawRectangle(p, 6, 3, 8, 5); g.DrawRectangle(p, 6, 12, 8, 5); }
        private static void Lines(Graphics g, Pen p, int x, int y, int width) { Line(g, p, x, y, x + width, y); Line(g, p, x, y + 4, x + width, y + 4); Line(g, p, x, y + 8, x + width, y + 8); }
        private static void Brackets(Graphics g, Pen p) { Line(g, p, 5, 4, 3, 4); Line(g, p, 3, 4, 3, 16); Line(g, p, 3, 16, 5, 16); Line(g, p, 15, 4, 17, 4); Line(g, p, 17, 4, 17, 16); Line(g, p, 17, 16, 15, 16); }
        private static void Plus(Graphics g, Pen p, int x, int y) { Line(g, p, x - 3, y, x + 3, y); Line(g, p, x, y - 3, x, y + 3); }
        private static void Cross(Graphics g, Pen p, int x, int y, int r) { Line(g, p, x - r, y - r, x + r, y + r); Line(g, p, x + r, y - r, x - r, y + r); }
        private static void Arrow(Graphics g, Pen p, int x1, int y1, int x2, int y2)
        {
            Line(g, p, x1, y1, x2, y2);
            if (x2 > x1) { Line(g, p, x2, y2, x2 - 4, y2 - 3); Line(g, p, x2, y2, x2 - 4, y2 + 3); }
            else if (x2 < x1) { Line(g, p, x2, y2, x2 + 4, y2 - 3); Line(g, p, x2, y2, x2 + 4, y2 + 3); }
            else if (y2 > y1) { Line(g, p, x2, y2, x2 - 3, y2 - 4); Line(g, p, x2, y2, x2 + 3, y2 - 4); }
            else { Line(g, p, x2, y2, x2 - 3, y2 + 4); Line(g, p, x2, y2, x2 + 3, y2 + 4); }
        }
        private static void Magnifier(Graphics g, Pen p, int x = 8, int y = 8, int r = 5) { g.DrawEllipse(p, x - r, y - r, r * 2, r * 2); Line(g, p, x + r - 1, y + r - 1, 18, 18); }
        private static void Play(Graphics g, Color color, int x, int y, int size) { using (var b = new SolidBrush(color)) g.FillPolygon(b, new[] { new Point(x, y - size / 2), new Point(x, y + size / 2), new Point(x + size, y) }); }
        private static void Check(Graphics g, Pen p, int x, int y) { Line(g, p, x - 3, y, x, y + 3); Line(g, p, x, y + 3, x + 6, y - 4); }
        private static void Text(Graphics g, string text, Color color, float size, int x, int y)
        {
            using (var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color)) g.DrawString(text, font, brush, x, y, StringFormat.GenericTypographic);
        }
        private static void Globe(Graphics g, Pen p) { g.DrawEllipse(p, 3, 3, 14, 14); g.DrawEllipse(p, 7, 3, 6, 14); Line(g, p, 3, 10, 17, 10); }
        private static void Gear(Graphics g, Pen p) { g.DrawEllipse(p, 5, 5, 10, 10); g.DrawEllipse(p, 8, 8, 4, 4); for (int n = 0; n < 8; n++) { double t = n * Math.PI / 4; Line(g, p, 10 + (float)Math.Cos(t) * 6, 10 + (float)Math.Sin(t) * 6, 10 + (float)Math.Cos(t) * 8, 10 + (float)Math.Sin(t) * 8); } }
        private static void Hammer(Graphics g, Pen p) { Line(g, p, 6, 17, 13, 8); Line(g, p, 10, 5, 16, 10); Line(g, p, 8, 7, 11, 3); Line(g, p, 11, 3, 18, 8); }
        private static void Bolt(Graphics g, Pen p) { g.DrawLines(p, new[] { new Point(11, 2), new Point(5, 11), new Point(10, 11), new Point(8, 18), new Point(16, 8), new Point(11, 8), new Point(11, 2) }); }
        private static void CurvedArrow(Graphics g, Pen p, bool right) { int d = right ? 1 : -1; using (var path = new GraphicsPath()) { path.AddArc(right ? 4 : 5, 5, 11, 10, right ? 180 : 0, 180); g.DrawPath(p, path); } Arrow(g, p, 10, 14, 10 + d * 7, 14); }
        private static void Scissors(Graphics g, Pen p) { g.DrawEllipse(p, 2, 2, 5, 5); g.DrawEllipse(p, 2, 12, 5, 5); Line(g, p, 6, 6, 17, 16); Line(g, p, 6, 14, 17, 4); }
        private static void Pencil(Graphics g, Pen p) { Line(g, p, 9, 16, 17, 8); Line(g, p, 11, 18, 18, 11); Line(g, p, 9, 16, 11, 18); }
    }
}
