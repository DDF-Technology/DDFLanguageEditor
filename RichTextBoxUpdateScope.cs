using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    internal sealed class RichTextBoxUpdateScope : IDisposable
    {
        private const int WmSetRedraw = 0x000B;
        private const int EmGetEventMask = 0x0400 + 59;
        private const int EmGetOleInterface = 0x0400 + 60;
        private const int EmSetEventMask = 0x0400 + 69;
        private const int TomSuspend = -9999995;
        private const int TomResume = -9999994;

        private readonly RichTextBox editor;
        private readonly IntPtr eventMask;
        private readonly ITextDocument textDocument;
        private readonly bool isActive;
        private bool isDisposed;

        private RichTextBoxUpdateScope(RichTextBox editor)
        {
            this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
            if (!editor.IsHandleCreated || editor.IsDisposed)
            {
                return;
            }

            eventMask = SendMessage(editor.Handle, EmGetEventMask, IntPtr.Zero, IntPtr.Zero);
            SendMessage(editor.Handle, EmSetEventMask, IntPtr.Zero, IntPtr.Zero);
            SendMessage(editor.Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
            textDocument = TryGetTextDocument(editor.Handle);
            textDocument?.Undo(TomSuspend, IntPtr.Zero);
            isActive = true;
        }

        public static RichTextBoxUpdateScope Begin(RichTextBox editor)
        {
            return new RichTextBoxUpdateScope(editor);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            if (!isActive || editor.IsDisposed || !editor.IsHandleCreated)
            {
                return;
            }

            try
            {
                textDocument?.Undo(TomResume, IntPtr.Zero);
            }
            finally
            {
                SendMessage(editor.Handle, EmSetEventMask, IntPtr.Zero, eventMask);
                SendMessage(editor.Handle, WmSetRedraw, new IntPtr(1), IntPtr.Zero);
                editor.Invalidate();
            }
        }

        private static ITextDocument TryGetTextDocument(IntPtr handle)
        {
            IntPtr oleInterface = IntPtr.Zero;
            IntPtr documentInterface = IntPtr.Zero;
            try
            {
                if (SendMessage(handle, EmGetOleInterface, IntPtr.Zero, out oleInterface) == IntPtr.Zero ||
                    oleInterface == IntPtr.Zero)
                {
                    return null;
                }

                Guid interfaceId = typeof(ITextDocument).GUID;
                if (Marshal.QueryInterface(oleInterface, in interfaceId, out documentInterface) < 0 ||
                    documentInterface == IntPtr.Zero)
                {
                    return null;
                }

                return (ITextDocument)Marshal.GetTypedObjectForIUnknown(documentInterface, typeof(ITextDocument));
            }
            finally
            {
                if (documentInterface != IntPtr.Zero) Marshal.Release(documentInterface);
                if (oleInterface != IntPtr.Zero) Marshal.Release(oleInterface);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, out IntPtr lParam);

        [ComImport]
        [Guid("8CC497C0-A1DF-11CE-8098-00AA0047BE5D")]
        [InterfaceType(ComInterfaceType.InterfaceIsDual)]
        private interface ITextDocument
        {
            [PreserveSig] int GetName(IntPtr name);
            [PreserveSig] int GetSelection(IntPtr selection);
            [PreserveSig] int GetStoryCount(IntPtr count);
            [PreserveSig] int GetStoryRanges(IntPtr stories);
            [PreserveSig] int GetSaved(IntPtr value);
            [PreserveSig] int SetSaved(int value);
            [PreserveSig] int GetDefaultTabStop(IntPtr value);
            [PreserveSig] int SetDefaultTabStop(float value);
            [PreserveSig] int New();
            [PreserveSig] int Open(IntPtr value, int flags, int codePage);
            [PreserveSig] int Save(IntPtr value, int flags, int codePage);
            [PreserveSig] int Freeze(IntPtr count);
            [PreserveSig] int Unfreeze(IntPtr count);
            [PreserveSig] int BeginEditCollection();
            [PreserveSig] int EndEditCollection();
            [PreserveSig] int Undo(int count, IntPtr actualCount);
            [PreserveSig] int Redo(int count, IntPtr actualCount);
        }
    }
}
