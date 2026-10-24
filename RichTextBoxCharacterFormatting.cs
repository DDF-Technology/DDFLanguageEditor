using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    /// <summary>
    /// Applies character formatting without routing every span through
    /// RichTextBox.Select/SelectionColor. The managed properties raise a large
    /// amount of RichEdit bookkeeping even while redraw is suspended; direct
    /// range messages keep full-document highlighting responsive.
    /// </summary>
    internal static class RichTextBoxCharacterFormatting
    {
        private const int EmExSetSel = 0x0400 + 55;
        private const int EmSetCharFormat = 0x0400 + 68;
        private const int ScfSelection = 0x0001;
        private const uint CfmColor = 0x40000000;
        private const uint CfmBackColor = 0x04000000;
        private const uint CfmUnderline = 0x00000004;
        private const uint CfmUnderlineType = 0x00800000;

        public static void ApplyTextColor(RichTextBox editor, int start,
            int length, Color color)
        {
            ApplyColors(editor, start, length, color, null);
        }

        public static void ApplyColors(RichTextBox editor, int start,
            int length, Color textColor, Color? backColor)
        {
            if (editor == null || editor.IsDisposed || !editor.IsHandleCreated ||
                length <= 0) return;
            int safeStart = Math.Max(0, Math.Min(start, editor.TextLength));
            int safeEnd = Math.Max(safeStart,
                Math.Min(safeStart + length, editor.TextLength));
            if (safeEnd == safeStart) return;

            var range = new CharacterRange
            {
                Minimum = safeStart,
                Maximum = safeEnd
            };
            SendMessage(editor.Handle, EmExSetSel, IntPtr.Zero, ref range);

            var format = new CharacterFormat
            {
                Size = (uint)Marshal.SizeOf<CharacterFormat>(),
                Mask = CfmColor | CfmUnderline | CfmUnderlineType |
                    (backColor.HasValue ? CfmBackColor : 0),
                Effects = 0,
                TextColor = ColorTranslator.ToWin32(textColor),
                BackColor = backColor.HasValue
                    ? ColorTranslator.ToWin32(backColor.Value)
                    : 0,
                UnderlineType = 0,
                FaceName = string.Empty
            };
            SendMessage(editor.Handle, EmSetCharFormat,
                new IntPtr(ScfSelection), ref format);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr windowHandle,
            int message, IntPtr parameter, ref CharacterRange range);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr windowHandle,
            int message, IntPtr parameter, ref CharacterFormat format);

        [StructLayout(LayoutKind.Sequential)]
        private struct CharacterRange
        {
            public int Minimum;
            public int Maximum;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct CharacterFormat
        {
            public uint Size;
            public uint Mask;
            public uint Effects;
            public int Height;
            public int Offset;
            public int TextColor;
            public byte CharacterSet;
            public byte PitchAndFamily;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FaceName;

            public ushort Weight;
            public short Spacing;
            public int BackColor;
            public uint LocaleId;
            public uint Reserved;
            public short Style;
            public ushort Kerning;
            public byte UnderlineType;
            public byte Animation;
            public byte RevisionAuthor;
            public byte UnderlineColor;
        }
    }
}
