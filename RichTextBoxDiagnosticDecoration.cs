using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal static class RichTextBoxDiagnosticDecoration
    {
        private const int EmGetCharFormat = 0x043A;
        private const int EmSetCharFormat = 0x0444;
        private const int ScfSelection = 0x0001;
        private const uint CfmUnderline = 0x00000004;
        private const uint CfmUnderlineType = 0x00800000;
        private const uint CfeUnderline = 0x00000004;
        private const byte CfuUnderlineNone = 0;
        private const byte CfuUnderlineWave = 8;
        private const byte RedColorIndex = 5;
        private const byte YellowColorIndex = 6;

        public static void ClearSelection(RichTextBox editor)
        {
            SetSelection(editor, false, DdfDiagnosticSeverity.Error);
        }

        public static void ApplySelection(RichTextBox editor, DdfDiagnosticSeverity severity)
        {
            SetSelection(editor, true, severity);
        }

        public static byte GetSelectionUnderlineType(RichTextBox editor)
        {
            CharFormat2 format = CreateFormat();
            SendMessage(editor.Handle, EmGetCharFormat, new IntPtr(ScfSelection), ref format);
            return format.UnderlineType;
        }

        public static byte GetSelectionUnderlineColor(RichTextBox editor)
        {
            CharFormat2 format = CreateFormat();
            SendMessage(editor.Handle, EmGetCharFormat, new IntPtr(ScfSelection), ref format);
            return format.UnderlineColor;
        }

        private static void SetSelection(RichTextBox editor, bool enabled, DdfDiagnosticSeverity severity)
        {
            CharFormat2 format = CreateFormat();
            format.Mask = CfmUnderline | CfmUnderlineType;
            format.Effects = enabled ? CfeUnderline : 0;
            format.UnderlineType = enabled ? CfuUnderlineWave : CfuUnderlineNone;
            format.UnderlineColor = severity == DdfDiagnosticSeverity.Warning
                ? YellowColorIndex
                : RedColorIndex;
            SendMessage(editor.Handle, EmSetCharFormat, new IntPtr(ScfSelection), ref format);
        }

        private static CharFormat2 CreateFormat()
        {
            return new CharFormat2
            {
                Size = (uint)Marshal.SizeOf<CharFormat2>(),
                FaceName = string.Empty
            };
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(
            IntPtr windowHandle,
            int message,
            IntPtr parameter,
            ref CharFormat2 format);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct CharFormat2
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
