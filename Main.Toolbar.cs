using System;
using System.Drawing;
using System.Windows.Forms;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ToolStripButton toolbarRunButton;
        private ToolStripButton toolbarStopButton;
        private ToolStripButton toolbarBreakpointButton;
        private ToolStripButton toolbarSaveAllButton;
        private ToolStripButton toolbarCloseDocumentButton;

        private void initializeMainToolbar()
        {
            toolStripMain.Items.Add(createToolbarButton("toolbarNewButton", "\uE710", "Nuovo (Ctrl+N)", newMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarOpenButton", "\uE8E5", "Apri (Ctrl+O)", openMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarSaveButton", "\uE74E", "Salva (Ctrl+S)", saveMenuItem_Click));
            toolbarSaveAllButton = createToolbarButton("toolbarSaveAllButton", "\uE8F4", "Salva tutto (Ctrl+Alt+S)", saveAllMenuItem_Click);
            toolStripMain.Items.Add(toolbarSaveAllButton);
            toolbarCloseDocumentButton = createToolbarButton("toolbarCloseDocumentButton", "\uE8BB", "Chiudi documento (Ctrl+W)", closeDocumentMenuItem_Click);
            toolStripMain.Items.Add(toolbarCloseDocumentButton);
            toolStripMain.Items.Add(new ToolStripSeparator());
            toolStripMain.Items.Add(createToolbarButton("toolbarUndoButton", "\uE7A7", "Annulla (Ctrl+Z)", undoMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarRedoButton", "\uE7A6", "Ripristina (Ctrl+Y)", redoMenuItem_Click));
            toolStripMain.Items.Add(new ToolStripSeparator());
            toolStripMain.Items.Add(createToolbarButton("toolbarCutButton", "\uE8C6", "Taglia (Ctrl+X)", cutMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarCopyButton", "\uE8C8", "Copia (Ctrl+C)", copyMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarPasteButton", "\uE77F", "Incolla (Ctrl+V)", pasteMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarCommentButton", "\uE8C1", "Commenta/decommenta (Ctrl+/)", toggleLineCommentMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarDuplicateLinesButton", "\uE8C8", "Duplica righe (Ctrl+Shift+D)", duplicateLinesMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarMoveLinesUpButton", "\uE74A", "Sposta righe su (Alt+Su)", moveLinesUpMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarMoveLinesDownButton", "\uE74B", "Sposta righe giù (Alt+Giù)", moveLinesDownMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarDeleteLinesButton", "\uE74D", "Elimina righe (Ctrl+Shift+K)", deleteLinesMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarExpandSelectionButton", "\uE8B7", "Espandi selezione (Shift+Alt+Destra)", expandSelectionMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarShrinkSelectionButton", "\uE8B6", "Riduci selezione (Shift+Alt+Sinistra)", shrinkSelectionMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarMatchingDelimiterButton", "\uE8A7", "Vai al delimitatore corrispondente (Ctrl+Shift+\\)", matchingDelimiterMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarSelectNextOccurrenceButton", "\uE8EE", "Seleziona occorrenza successiva (Ctrl+D)", selectNextOccurrenceMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarSelectAllOccurrencesButton", "\uE8B3", "Seleziona tutte le occorrenze (Ctrl+Shift+L)", selectAllOccurrencesMenuItem_Click));
            toolStripMain.Items.Add(new ToolStripSeparator());
            toolStripMain.Items.Add(createToolbarButton("toolbarFindButton", "\uE721", "Trova (Ctrl+F)", findMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarCompletionButton", "\uE943", "Completamento contestuale (Ctrl+Spazio)", completionMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarFormatButton", "\uE8D2", "Formatta documento (Ctrl+Shift+F)", formatDocumentMenuItem_Click));
            toolStripMain.Items.Add(createToolbarButton("toolbarFoldButton", "\uE8B0", "Comprimi o espandi blocco (Ctrl+M)", toggleFoldMenuItem_Click));
            toolStripMain.Items.Add(new ToolStripSeparator());
            toolbarBreakpointButton = createToolbarButton("toolbarBreakpointButton", "●", "Attiva/disattiva breakpoint (F9)", toggleBreakpointMenuItem_Click);
            toolbarBreakpointButton.Font = new Font("Segoe UI Symbol", 12F, FontStyle.Bold);
            toolbarBreakpointButton.ForeColor = AppTheme.Stop;
            toolStripMain.Items.Add(toolbarBreakpointButton);
            toolbarRunButton = createToolbarButton("toolbarRunButton", "\uE768", "Avvia (F5)", runProgramMenuItem_Click);
            toolbarRunButton.ForeColor = AppTheme.Run;
            toolStripMain.Items.Add(toolbarRunButton);
            toolbarStopButton = createToolbarButton("toolbarStopButton", "\uE71A", "Arresta (Shift+F5)", stopProgramMenuItem_Click);
            toolbarStopButton.ForeColor = AppTheme.Stop;
            toolbarStopButton.Enabled = false;
            toolStripMain.Items.Add(toolbarStopButton);
        }

        private static ToolStripButton createToolbarButton(string name, string glyph, string toolTip, EventHandler handler)
        {
            var button = new ToolStripButton
            {
                AutoSize = false,
                AccessibleName = toolTip,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new Font("Segoe MDL2 Assets", 11F),
                Margin = new Padding(1, 1, 1, 2),
                Name = name,
                Size = new Size(30, 28),
                Text = glyph,
                ToolTipText = toolTip
            };
            button.Click += handler;
            return button;
        }
    }
}
