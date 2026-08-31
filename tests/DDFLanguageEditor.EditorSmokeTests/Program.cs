using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DDF___Program_Language_Editor;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.EditorSmokeTests
{
    internal static class Program
    {
        private const int WmLeftButtonDown = 0x0201;
        private const string Source =
            "@@'Console'\n" +
            "main() out int {\n" +
            "    int value << 1;\n" +
            "    ret value;\n" +
            "}";

        private static readonly List<Exception> UiExceptions = new List<Exception>();

        [STAThread]
        private static int Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, eventArgs) => UiExceptions.Add(eventArgs.Exception);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool visible = args.Any(argument => string.Equals(argument, "--visible", StringComparison.OrdinalIgnoreCase));
            try
            {
                RunEditorScenarios(visible);
                RunMenuScenarios(visible);
                if (UiExceptions.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Eccezioni UI intercettate:\n" + string.Join("\n---\n", UiExceptions.Select(exception => exception.ToString())));
                }

                Console.WriteLine("PASS smoke dinamico WinForms: scenari editor e tutti i 46 comandi di menu completati senza eccezioni.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL smoke dinamico WinForms: " + exception);
                return 1;
            }
        }

        private static void RunEditorScenarios(bool visible)
        {
            using (var form = new MainForm())
            {
                form.ShowInTaskbar = visible;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = visible ? new Point(80, 80) : new Point(-32000, -32000);
                if (!visible) form.WindowState = FormWindowState.Normal;
                form.Show();
                PumpMessages(160);

                RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                RichTextBox foldedView = FindControl<RichTextBox>(form, "richTextBoxFoldedView");
                RichTextBox lineNumbers = FindControl<RichTextBox>(form, "richTextBoxLineNumbers");
                ListBox diagnostics = FindControl<ListBox>(form, "listBoxDiagnostics");
                Panel diagnosticsPanel = FindControl<Panel>(form, "panelDiagnostics");

                Require(diagnosticsPanel.Visible, "La finestra diagnostica non è visibile all'avvio.");
                Require(form.Controls.Find("panelBeta", true).Length == 0,
                    "La barra beta è ancora presente nella finestra principale.");

                AssertDocumentTabsDoNotCoverSource(form, editor);
                AssertMainToolbar(form);
                AssertUnifiedLightTheme(form, editor, foldedView, lineNumbers);
                AssertGutterAndAutoHidePalettes(form, editor, lineNumbers, diagnosticsPanel);
                AssertEditorPalette(editor);
                SetSource(editor);
                AssertSelectionStableDuringHighlight(editor);
                AssertMouseGesturePreservesNativeSelection(form, editor);
                AssertClosingBraceAlignment(form, editor);
                AssertCodingGesturesAndContextMenu(form, editor);
                AssertSyntacticSelectionAndDelimiterNavigation(form, editor);
                AssertMultipleSelections(form, editor);
                AssertColoredFoldFromFunctionHeader(form, editor, foldedView, lineNumbers);
                AssertMultipleIndependentFolds(form, editor, foldedView);
                AssertWholeLibraryCutIsClean(editor, diagnostics);
                AssertUndoRestoresLibrary(editor);
                AssertPartialLibraryCutIsRecoverable(editor, diagnostics);
                AssertRapidTransientEditsAreRecoverable(form, editor);
                AssertContextualCompletion(form, editor);
                AssertNavigableSnippets(form, editor);
                AssertSignatureHelp(form, editor);
                AssertDocumentFormatting(form, editor);
                AssertSemanticNavigationAndRename(form, editor);
                AssertInlineDiagnostics(form, editor, diagnostics);
                AssertQuickFixes(form, editor, diagnostics);
                AssertTypeChecking(form, editor, diagnostics);
                AssertWorkspaceNavigation(form, editor, diagnostics);
                AssertProgramExecution(form, editor);
            }
        }

        private static void RunMenuScenarios(bool visible)
        {
            IDataObject originalClipboard = null;
            try
            {
                originalClipboard = Clipboard.GetDataObject();
                AssertMenuStructureAndShortcuts(visible);
                AssertEditAndFindReplaceMenus(visible);
                AssertViewMenu(visible);
                AssertHelpMenu(visible);
                AssertFileMenu(visible);
            }
            finally
            {
                RestoreClipboard(originalClipboard);
            }
        }

        private static void AssertMenuStructureAndShortcuts(bool visible)
        {
            using (var defaultForm = new MainForm())
            {
                Require(defaultForm.WindowState == FormWindowState.Maximized,
                    "La finestra principale non è configurata per l'avvio massimizzato.");
                Require(defaultForm.Controls.Find("panelBeta", true).Length == 0,
                    "La barra beta è ancora presente nel layout principale.");
            }

            Type inputType = typeof(MainForm).Assembly.GetType("DDF___Program_Language_Editor.RuntimeInputForm", true);
            using (var inputForm = (Form)Activator.CreateInstance(inputType))
            {
                Require(inputForm.StartPosition == FormStartPosition.CenterScreen &&
                        IsLight(inputForm.BackColor) && IsLight(FindControl<TextBox>(inputForm, "runtimeInputTextBox").BackColor),
                    "La finestra di input runtime conserva colori scuri.");
            }
            Type renameType = typeof(MainForm).Assembly.GetType("DDF___Program_Language_Editor.RenameSymbolForm", true);
            using (var renameForm = (Form)Activator.CreateInstance(renameType, "value"))
            {
                Require(renameForm.StartPosition == FormStartPosition.CenterScreen && IsLight(renameForm.BackColor),
                    "La finestra Rinomina simbolo non è chiara e centrata sullo schermo.");
            }

            using (var form = ShowSmokeForm(visible))
            {
                var shortcuts = new Dictionary<string, Keys>
                {
                    { "newMenuItem", Keys.Control | Keys.N },
                    { "openMenuItem", Keys.Control | Keys.O },
                    { "openWorkspaceMenuItem", Keys.Control | Keys.Alt | Keys.O },
                    { "saveMenuItem", Keys.Control | Keys.S },
                    { "saveAsMenuItem", Keys.Control | Keys.Shift | Keys.S },
                    { "saveAllMenuItem", Keys.Control | Keys.Alt | Keys.S },
                    { "closeDocumentMenuItem", Keys.Control | Keys.W },
                    { "undoMenuItem", Keys.Control | Keys.Z },
                    { "redoMenuItem", Keys.Control | Keys.Y },
                    { "cutMenuItem", Keys.Control | Keys.X },
                    { "copyMenuItem", Keys.Control | Keys.C },
                    { "pasteMenuItem", Keys.Control | Keys.V },
                    { "toggleLineCommentMenuItem", Keys.Control | Keys.OemQuestion },
                    { "duplicateLinesMenuItem", Keys.Control | Keys.Shift | Keys.D },
                    { "selectNextOccurrenceMenuItem", Keys.Control | Keys.D },
                    { "selectAllOccurrencesMenuItem", Keys.Control | Keys.Shift | Keys.L },
                    { "moveLinesUpMenuItem", Keys.Alt | Keys.Up },
                    { "moveLinesDownMenuItem", Keys.Alt | Keys.Down },
                    { "deleteLinesMenuItem", Keys.Control | Keys.Shift | Keys.K },
                    { "expandSelectionMenuItem", Keys.Shift | Keys.Alt | Keys.Right },
                    { "shrinkSelectionMenuItem", Keys.Shift | Keys.Alt | Keys.Left },
                    { "matchingDelimiterMenuItem", Keys.Control | Keys.Shift | Keys.OemPipe },
                    { "selectAllMenuItem", Keys.Control | Keys.A },
                    { "findMenuItem", Keys.Control | Keys.F },
                    { "replaceMenuItem", Keys.Control | Keys.H },
                    { "workspaceSearchMenuItem", Keys.Control | Keys.Alt | Keys.F },
                    { "workspaceReplaceMenuItem", Keys.Control | Keys.Alt | Keys.H },
                    { "goToFileMenuItem", Keys.Control | Keys.P },
                    { "goToSymbolMenuItem", Keys.Control | Keys.Shift | Keys.O },
                    { "findReferencesMenuItem", Keys.Shift | Keys.F12 },
                    { "goToLineMenuItem", Keys.Control | Keys.G },
                    { "goToLastEditMenuItem", Keys.Control | Keys.Shift | Keys.Back },
                    { "completionMenuItem", Keys.Control | Keys.Space },
                    { "formatDocumentMenuItem", Keys.Control | Keys.Shift | Keys.F },
                    { "quickFixMenuItem", Keys.Control | Keys.OemPeriod },
                    { "goToDefinitionMenuItem", Keys.F12 },
                    { "renameSymbolMenuItem", Keys.F2 },
                    { "runProgramMenuItem", Keys.F5 },
                    { "toggleBreakpointMenuItem", Keys.F9 },
                    { "stopProgramMenuItem", Keys.Shift | Keys.F5 },
                    { "toggleFoldMenuItem", Keys.Control | Keys.M },
                    { "expandAllFoldsMenuItem", Keys.Control | Keys.Shift | Keys.M }
                };

                string[] commands =
                {
                    "newMenuItem", "openMenuItem", "saveMenuItem", "saveAsMenuItem", "saveAllMenuItem", "closeDocumentMenuItem",
                    "openWorkspaceMenuItem", "closeWorkspaceMenuItem",
                    "recentMenuItem", "exitMenuItem", "undoMenuItem", "redoMenuItem",
                    "cutMenuItem", "copyMenuItem", "pasteMenuItem", "selectAllMenuItem", "toggleLineCommentMenuItem",
                    "duplicateLinesMenuItem", "moveLinesUpMenuItem", "moveLinesDownMenuItem", "deleteLinesMenuItem",
                    "expandSelectionMenuItem", "shrinkSelectionMenuItem", "matchingDelimiterMenuItem",
                    "selectNextOccurrenceMenuItem", "selectAllOccurrencesMenuItem",
                    "findMenuItem", "replaceMenuItem", "workspaceSearchMenuItem", "workspaceReplaceMenuItem",
                    "goToFileMenuItem", "goToSymbolMenuItem", "findReferencesMenuItem", "goToLineMenuItem", "goToLastEditMenuItem",
                    "completionMenuItem", "formatDocumentMenuItem", "quickFixMenuItem",
                    "goToDefinitionMenuItem", "renameSymbolMenuItem", "runProgramMenuItem", "toggleBreakpointMenuItem", "stopProgramMenuItem",
                    "toggleFoldMenuItem", "expandAllFoldsMenuItem", "aboutMenuItem"
                };
                foreach (string command in commands)
                {
                    ToolStripMenuItem item = FindMenuItem(form, command);
                    Require(item != null, "Comando di menu non trovato: " + command);
                    Require(command == "recentMenuItem" || item.GetType().GetEvent("Click") != null,
                        "Il comando non espone l'evento Click: " + command);
                }

                foreach (KeyValuePair<string, Keys> shortcut in shortcuts)
                {
                    Require(FindMenuItem(form, shortcut.Key).ShortcutKeys == shortcut.Value,
                        "Scorciatoia errata per " + shortcut.Key + ".");
                }
            }

            Console.WriteLine("PASS struttura menu e 42 scorciatoie");
        }

        private static void AssertHelpMenu(bool visible)
        {
            using (var form = ShowSmokeForm(visible))
            {
                FindMenuItem(form, "aboutMenuItem").PerformClick();
                PumpMessages(80);

                Form about = Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(openForm => openForm.Name == "AboutForm" && openForm.Owner == form);
                Require(about != null && about.Visible, "About non ha aperto il popup informativo.");
                Require(about.StartPosition == FormStartPosition.CenterScreen,
                    "About non è configurato per apparire al centro dello schermo.");
                Require(FindControl<Label>(about, "aboutProductLabel").Text == "DDFLanguageEditor",
                    "About non riporta il nome dell'applicazione.");
                Require(FindControl<Label>(about, "aboutVersionLabel").Text.Contains("0.9.3.3") &&
                        FindControl<Label>(about, "aboutVersionLabel").Text.Contains("Beta"),
                    "About non riporta versione e stato beta.");
                Require(FindControl<Label>(about, "aboutAuthorLabel").Text.Contains("Fabio De Deo"),
                    "About non riporta l'autore.");
                Require(FindControl<LinkLabel>(about, "aboutWebsiteLink").Text == "www.ddf.technology",
                    "About non riporta il sito web.");
                Require(FindControl<TextBox>(about, "aboutLicenseTextBox").Text.Contains("MIT License"),
                    "About non riporta la licenza MIT.");
                Require(about.Icon != null, "About non mostra l'icona dell'applicazione.");
                PictureBox aboutIcon = FindControl<PictureBox>(about, "aboutIconPictureBox");
                Require(aboutIcon.Image != null && aboutIcon.Image.Width >= 512 && aboutIcon.Image.Height >= 512,
                    "About non usa l'asset dell'icona ad alta risoluzione.");
                Require(IsLight(about.BackColor) && IsLight(FindControl<TextBox>(about, "aboutLicenseTextBox").BackColor) &&
                        IsLight(FindControl<Button>(about, "aboutCloseButton").BackColor),
                    "About contiene ancora superfici del precedente tema scuro.");

                FindControl<Button>(about, "aboutCloseButton").PerformClick();
                PumpMessages(40);
                Require(about.IsDisposed, "Il pulsante Chiudi non ha terminato About.");
            }

            Console.WriteLine("PASS menu Help: About, autore, sito, licenza, versione beta e icona");
        }

        private static void AssertEditAndFindReplaceMenus(bool visible)
        {
            using (var form = ShowSmokeForm(visible))
            {
                RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                editor.Text = "alpha beta alpha";
                PumpMessages(180);

                editor.Select(0, 5);
                FindMenuItem(form, "copyMenuItem").Enabled = false;
                InvokeProcessCmdKey(form, Keys.Control | Keys.C);
                Require(GetClipboardTextWithRetry() == "alpha",
                    "Ctrl+C dipende ancora dallo stato obsoleto della voce di menu.");
                FindMenuItem(form, "cutMenuItem").Enabled = false;
                InvokeProcessCmdKey(form, Keys.Control | Keys.X);
                Require(editor.Text == " beta alpha",
                    "Ctrl+X dipende ancora dallo stato obsoleto della voce di menu.");
                editor.Undo();
                Clipboard.SetText("gamma");
                editor.Select(6, 4);
                FindMenuItem(form, "pasteMenuItem").Enabled = false;
                InvokeProcessCmdKey(form, Keys.Control | Keys.V);
                Require(editor.Text == "alpha gamma alpha",
                    "Ctrl+V dipende ancora dallo stato obsoleto della voce di menu.");
                editor.Undo();

                editor.Select(0, 5);
                editor.Focus();
                Clipboard.SetText("omega");
                InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);

                Require(FindMenuItem(form, "cutMenuItem").Enabled, "Taglia non è abilitato con una selezione.");
                Require(FindMenuItem(form, "copyMenuItem").Enabled, "Copia non è abilitato con una selezione.");
                Require(FindMenuItem(form, "pasteMenuItem").Enabled, "Incolla non è abilitato con testo negli appunti.");

                FindMenuItem(form, "copyMenuItem").PerformClick();
                Require(GetClipboardTextWithRetry() == "alpha", "Copia non ha trasferito la selezione negli appunti.");
                FindMenuItem(form, "cutMenuItem").PerformClick();
                Require(editor.Text == " beta alpha", "Taglia non ha rimosso la selezione.");
                InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
                FindMenuItem(form, "undoMenuItem").PerformClick();
                Require(editor.Text == "alpha beta alpha", "Annulla non ha ripristinato il testo.");
                InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
                FindMenuItem(form, "redoMenuItem").PerformClick();
                Require(editor.Text == " beta alpha", "Ripristina non ha riapplicato il taglio.");
                InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
                FindMenuItem(form, "undoMenuItem").PerformClick();

                Clipboard.SetText("gamma");
                editor.Select(6, 4);
                FindMenuItem(form, "pasteMenuItem").PerformClick();
                Require(editor.Text == "alpha gamma alpha", "Incolla non ha sostituito la selezione.");
                FindMenuItem(form, "undoMenuItem").PerformClick();
                FindMenuItem(form, "selectAllMenuItem").PerformClick();
                Require(editor.SelectionLength == editor.TextLength, "Seleziona tutto non ha selezionato il documento.");

                editor.Select(0, 5);
                FindMenuItem(form, "findMenuItem").PerformClick();
                PumpMessages(60);
                Form findForm = GetPrivateField<Form>(form, "findReplaceForm");
                Require(findForm != null && findForm.Visible && findForm.Text == "Trova",
                    "Trova non ha aperto la finestra prevista.");
                Require(findForm.StartPosition == FormStartPosition.CenterScreen,
                    "Trova/Sostituisci non è configurata al centro dello schermo.");
                Require(IsLight(findForm.BackColor) && IsLight(FindControl<TextBox>(findForm, "findTextBox").BackColor) &&
                        IsLight(FindControl<Button>(findForm, "findNextButton").BackColor),
                    "Trova/Sostituisci non usa interamente il tema chiaro.");
                TextBox findText = FindControl<TextBox>(findForm, "findTextBox");
                CheckBox matchCase = FindControl<CheckBox>(findForm, "matchCaseCheckBox");
                Button findNext = FindControl<Button>(findForm, "findNextButton");
                Button replaceCurrent = FindControl<Button>(findForm, "replaceButton");
                Button replaceAll = FindControl<Button>(findForm, "replaceAllButton");
                Button closeFind = FindControl<Button>(findForm, "closeButton");
                Require(!FindControl<TextBox>(findForm, "replaceTextBox").Visible &&
                        !replaceCurrent.Visible && !replaceAll.Visible,
                    "La modalità Trova lascia visibili i comandi di sostituzione.");
                Require(Math.Abs(matchCase.Left - findText.Left) <= 4,
                    "La checkbox non è allineata con i campi di testo.");
                Require(findText.Text == "alpha", "Trova non ha precompilato il testo selezionato.");
                findNext.PerformClick();
                Require(editor.SelectionStart == 11 && editor.SelectedText == "alpha",
                    "Trova dopo non ha selezionato la corrispondenza successiva.");

                FindMenuItem(form, "replaceMenuItem").PerformClick();
                PumpMessages(60);
                Require(findForm.Text == "Trova e sostituisci" && FindControl<TextBox>(findForm, "replaceTextBox").Visible,
                    "Sostituisci non ha attivato i controlli di sostituzione.");
                Require(replaceCurrent.Visible && replaceAll.Visible &&
                        findNext.Top == replaceCurrent.Top && replaceCurrent.Top == replaceAll.Top && replaceAll.Top == closeFind.Top,
                    "I pulsanti Trova/Sostituisci non sono allineati sulla stessa barra.");
                Require(findNext.Left < replaceCurrent.Left && replaceCurrent.Left < replaceAll.Left && replaceAll.Left < closeFind.Left,
                    "L'ordine dei pulsanti Trova/Sostituisci non è coerente: " +
                    findNext.Left + ", " + replaceCurrent.Left + ", " + replaceAll.Left + ", " + closeFind.Left + ".");
                Require(new[] { findNext, replaceCurrent, replaceAll, closeFind }.All(button => button.Height == 30),
                    "I pulsanti Trova/Sostituisci non hanno dimensioni uniformi.");
                findText.Text = "beta";
                FindControl<TextBox>(findForm, "replaceTextBox").Text = "delta";
                editor.Select(6, 4);
                replaceCurrent.PerformClick();
                Require(editor.Text == "alpha delta alpha", "Sostituisci non ha modificato la corrispondenza corrente.");

                findText.Text = "alpha";
                FindControl<TextBox>(findForm, "replaceTextBox").Text = "omega";
                replaceAll.PerformClick();
                Require(editor.Text == "omega delta omega", "Sostituisci tutto non ha modificato tutte le corrispondenze.");
                closeFind.PerformClick();
                PumpMessages(40);
                Require(findForm.IsDisposed, "Chiudi non ha terminato la finestra Trova/Sostituisci.");

                editor.Text = string.Empty;
                editor.Select(0, 0);
                FindMenuItem(form, "completionMenuItem").PerformClick();
                PumpMessages(180);
                ListBox completion = FindControl<ListBox>(form, "completionListBox");
                Require(completion.Visible && completion.Items.Count > 0,
                    "Il comando Completamento non ha mostrato i suggerimenti.");
                InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Escape));
            }

            Console.WriteLine("PASS menu Modifica: Annulla, Ripristina, Taglia, Copia, Incolla, Seleziona tutto, Trova, Sostituisci, Completamento e Formatta documento");
        }

        private static void AssertViewMenu(bool visible)
        {
            using (var form = ShowSmokeForm(visible))
            {
                RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                RichTextBox foldedView = FindControl<RichTextBox>(form, "richTextBoxFoldedView");
                editor.Text = Source;
                editor.Select(Source.IndexOf("main", StringComparison.Ordinal), 0);
                PumpMessages(180);
                InvokeHandler(form, "viewMenuItem_DropDownOpening", FindMenuItem(form, "viewMenuItem"), EventArgs.Empty);
                ToolStripMenuItem toggle = FindMenuItem(form, "toggleFoldMenuItem");
                ToolStripMenuItem expandAll = FindMenuItem(form, "expandAllFoldsMenuItem");
                Require(toggle.Enabled && toggle.Text == "Comprimi blocco", "Visualizza non propone la compressione sul blocco.");
                toggle.PerformClick();
                PumpMessages(80);
                Require(foldedView.Visible && !editor.Visible, "Comprimi blocco non ha attivato la proiezione.");

                InvokeHandler(form, "viewMenuItem_DropDownOpening", FindMenuItem(form, "viewMenuItem"), EventArgs.Empty);
                Require(toggle.Enabled && toggle.Text == "Espandi blocco" && expandAll.Enabled,
                    "Lo stato dei comandi Visualizza non riflette la vista compressa.");
                InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
                Require(!FindMenuItem(form, "cutMenuItem").Enabled && !FindMenuItem(form, "pasteMenuItem").Enabled,
                    "La vista compressa consente modifiche che dovrebbero essere bloccate.");
                foldedView.Select(0, Math.Min(5, foldedView.TextLength));
                InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
                Require(FindMenuItem(form, "copyMenuItem").Enabled,
                    "La vista compressa non consente una selezione copiabile.");
                FindMenuItem(form, "copyMenuItem").PerformClick();
                Require(Clipboard.ContainsText(), "Copia non opera sulla vista compressa.");
                FindMenuItem(form, "selectAllMenuItem").PerformClick();
                Require(foldedView.SelectionLength == foldedView.TextLength,
                    "Seleziona tutto non opera sulla vista compressa.");
                expandAll.PerformClick();
                PumpMessages(80);
                Require(editor.Visible && !foldedView.Visible && editor.Text == Source,
                    "Espandi tutto non ha ripristinato il sorgente originale.");
            }

            Console.WriteLine("PASS menu Visualizza: Comprimi blocco ed Espandi tutto");
        }

        private static void AssertFileMenu(bool visible)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "DDFLanguageEditor-menu-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            string openPath = Path.Combine(tempDirectory, "aperto.ddf");
            string saveAsPath = Path.Combine(tempDirectory, "salvato-con-nome.ddf");
            File.WriteAllText(openPath, Source);
            try
            {
                using (var form = ShowSmokeForm(visible))
                {
                    DisableRecentFilePersistence(form);
                    SetPrivateField(form, "showOpenFileDialog", new Func<OpenFileDialog, DialogResult>(dialog =>
                    {
                        dialog.FileName = openPath;
                        return DialogResult.OK;
                    }));
                    SetPrivateField(form, "showSaveFileDialog", new Func<SaveFileDialog, DialogResult>(dialog =>
                    {
                        dialog.FileName = saveAsPath;
                        return DialogResult.OK;
                    }));

                    RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                    FindMenuItem(form, "openMenuItem").PerformClick();
                    editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                    Require(editor.Text == Source && form.Text.Contains("aperto.ddf"), "Apri non ha caricato il file temporaneo.");
                    editor.AppendText("\n// saved");
                    FindMenuItem(form, "saveMenuItem").PerformClick();
                    Require(File.ReadAllText(openPath).EndsWith("// saved", StringComparison.Ordinal),
                        "Salva non ha scritto il documento corrente.");

                    editor.AppendText("\n// save as");
                    FindMenuItem(form, "saveAsMenuItem").PerformClick();
                    Require(File.Exists(saveAsPath) && File.ReadAllText(saveAsPath).EndsWith("// save as", StringComparison.Ordinal),
                        "Salva con nome non ha scritto il percorso scelto.");

                    editor.Select(editor.GetFirstCharIndexFromLine(2), 0);
                    FindMenuItem(form, "toggleBreakpointMenuItem").PerformClick();
                    RichTextBox gutter = FindControl<RichTextBox>(form, "richTextBoxLineNumbers");
                    Require(gutter.Text.Contains("● 3"), "Il breakpoint non appare nel documento corrente.");
                    ListBox breakpointPalette = FindControl<ListBox>(form, "listBoxBreakpoints");
                    Require(breakpointPalette.Items.Count == 1 && breakpointPalette.Items[0].ToString().Contains("riga 3"),
                        "La palette Breakpoint non elenca il punto di arresto.");

                    FindMenuItem(form, "newMenuItem").PerformClick();
                    editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                    Require(editor.TextLength == 0 && form.Text.Contains("Senza titolo.ddf"), "Nuovo non ha creato un documento vuoto.");
                    TabControl documentTabs = FindControl<TabControl>(form, "documentTabs");
                    Require(documentTabs.TabCount == 3, "Nuovo ha sostituito il buffer invece di aggiungere una scheda.");
                    Require(!gutter.Text.Contains("● 3"), "Il breakpoint di un altro file è visibile nella nuova scheda.");

                    TabPage savedTab = documentTabs.TabPages.Cast<TabPage>()
                        .Single(page => string.Equals(page.ToolTipText, saveAsPath, StringComparison.OrdinalIgnoreCase));
                    documentTabs.SelectedTab = savedTab;
                    PumpMessages(180);
                    editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                    Require(editor.Text.EndsWith("// save as", StringComparison.Ordinal) && gutter.Text.Contains("● 3"),
                        "Testo o breakpoint non sono stati ripristinati tornando alla scheda.");
                    editor.AppendText("\n// modifica locale");
                    editor.Select(4, 0);
                    documentTabs.SelectedIndex = documentTabs.TabCount - 1;
                    PumpMessages(100);
                    documentTabs.SelectedTab = savedTab;
                    PumpMessages(140);
                    editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                    Require(editor.Text.EndsWith("// modifica locale", StringComparison.Ordinal) && editor.CanUndo && editor.SelectionStart == 4,
                        "Cambio scheda ha perso modifiche, cursore o cronologia Undo.");
                    editor.Undo();
                    PumpMessages(120);
                    Require(editor.Text.EndsWith("// save as", StringComparison.Ordinal),
                        "Undo non è rimasto indipendente nella scheda riattivata.");

                    editor.AppendText("\n// save all");
                    FindMenuItem(form, "saveAllMenuItem").PerformClick();
                    Require(File.ReadAllText(saveAsPath).EndsWith("// save all", StringComparison.Ordinal),
                        "Salva tutto non ha scritto il buffer modificato.");
                    ToolStripMenuItem recent = FindMenuItem(form, "recentMenuItem").DropDownItems
                        .OfType<ToolStripMenuItem>()
                        .FirstOrDefault(item => string.Equals(item.Tag as string, saveAsPath, StringComparison.OrdinalIgnoreCase));
                    Require(recent != null, "File recenti non contiene il documento appena salvato.");
                    recent.PerformClick();
                    editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                    Require(editor.Text.EndsWith("// save all", StringComparison.Ordinal) && form.Text.Contains("salvato-con-nome.ddf"),
                        "File recenti non ha riaperto il documento selezionato.");
                    FindMenuItem(form, "closeDocumentMenuItem").PerformClick();
                    Require(documentTabs.TabCount == 2, "Chiudi documento non ha rimosso la scheda attiva.");
                }

                using (var exitForm = ShowSmokeForm(visible))
                {
                    DisableRecentFilePersistence(exitForm);
                    FindMenuItem(exitForm, "exitMenuItem").PerformClick();
                    PumpMessages(40);
                    Require(exitForm.IsDisposed, "Esci non ha chiuso la finestra principale.");
                }
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }

            Console.WriteLine("PASS menu File: documenti, cartella workspace, file recenti ed Esci");
        }

        private static MainForm ShowSmokeForm(bool visible)
        {
            var form = new MainForm
            {
                ShowInTaskbar = visible,
                StartPosition = FormStartPosition.Manual,
                Location = visible ? new Point(80, 80) : new Point(-32000, -32000)
            };
            if (!visible) form.WindowState = FormWindowState.Normal;
            form.Show();
            PumpMessages(100);
            return form;
        }

        private static void DisableRecentFilePersistence(MainForm form)
        {
            SetPrivateField(form, "saveRecentFilesSetting", new Action<string>(value => { }));
        }

        private static void RestoreClipboard(IDataObject originalClipboard)
        {
            try
            {
                if (originalClipboard == null) Clipboard.Clear();
                else Clipboard.SetDataObject(originalClipboard, true);
            }
            catch (ExternalException)
            {
                Console.Error.WriteLine("WARN impossibile ripristinare gli appunti dopo lo smoke menu.");
            }
        }

        private static string GetClipboardTextWithRetry()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text)) return text;
                }
                catch (ExternalException)
                {
                }

                PumpMessages(20);
            }

            return string.Empty;
        }

        private static void SetSource(RichTextBox editor)
        {
            editor.Text = Source;
            editor.Select(0, 0);
            PumpMessages(180);
            Require(editor.Text == Source, "Il buffer iniziale non coincide con il sorgente smoke.");
            Console.WriteLine("PASS caricamento sorgente smoke");
        }

        private static void AssertEditorPalette(RichTextBox editor)
        {
            const string paletteSource =
                "main() out int { string message << \"testo leggibile\"; // commento\n ret 1; }";
            editor.Text = paletteSource;
            PumpMessages(180);
            Require(editor.BackColor == Color.FromArgb(30, 30, 30),
                "Lo sfondo dell'editor non usa il colore IDE previsto.");

            int stringStart = paletteSource.IndexOf("\"testo leggibile\"", StringComparison.Ordinal);
            editor.Select(stringStart, 1);
            Require(editor.SelectionColor == Color.FromArgb(206, 145, 120),
                "Le stringhe non usano il colore ad alto contrasto previsto.");
            int commentStart = paletteSource.IndexOf("// commento", StringComparison.Ordinal);
            editor.Select(commentStart, 1);
            Require(editor.SelectionColor == Color.FromArgb(106, 153, 85),
                "I commenti non usano il verde attenuato previsto.");
            Console.WriteLine("PASS palette IDE ad alto contrasto per sfondo, stringhe e commenti");
        }

        private static void AssertGutterAndAutoHidePalettes(
            MainForm form,
            RichTextBox editor,
            RichTextBox lineNumbers,
            Panel diagnosticsPanel)
        {
            lineNumbers.SelectAll();
            Require(lineNumbers.SelectionAlignment == HorizontalAlignment.Left,
                "I numeri di riga non sono allineati a sinistra.");
            Require(lineNumbers.Cursor == Cursors.Arrow && !lineNumbers.TabStop,
                "Il gutter usa ancora il cursore testuale o partecipa alla navigazione Tab.");
            bool gutterAcceptedFocus = lineNumbers.Focus();
            PumpMessages(20);
            Require(!gutterAcceptedFocus && !lineNumbers.Focused,
                "È ancora possibile posizionare il cursore nel gutter dei numeri di riga.");
            editor.Focus();

            Panel outlinePanel = FindControl<Panel>(form, "panelOutline");
            Panel diagnosticsPanelFromForm = FindControl<Panel>(form, "panelDiagnostics");
            Splitter outlineSplitter = FindControl<Splitter>(form, "splitterOutline");
            Splitter diagnosticsSplitter = FindControl<Splitter>(form, "splitterDiagnostics");
            StatusStrip status = FindControl<StatusStrip>(form, "statusStripMain");
            ToolStrip toolbar = FindControl<ToolStrip>(form, "toolStripMain");
            TabControl navigation = FindControl<TabControl>(form, "navigationTabs");
            Button outlinePin = FindControl<Button>(form, "buttonOutlinePin");
            Button diagnosticsPin = FindControl<Button>(form, "buttonDiagnosticsPin");
            Require(outlinePanel.Width >= 300 && outlinePanel.Width <= 310,
                "La palette destra non misura i 300 px previsti all'avvio: " + outlinePanel.Width + " px.");
            Require(navigation.Appearance == TabAppearance.Normal,
                "Workspace e Outline non usano schede standard come Diagnostica e Output.");
            Require(outlinePanel.Top == toolbar.Bottom && outlinePanel.Bottom == status.Top,
                "La palette destra non occupa tutta l'altezza utile della form.");
            Require(diagnosticsPanelFromForm.Right == outlineSplitter.Left && diagnosticsPanelFromForm.Width < form.ClientSize.Width,
                "Diagnostica e Output non si adattano allo spazio lasciato dalla palette destra.");
            Require(diagnosticsSplitter.Dock == DockStyle.Bottom && diagnosticsSplitter.Cursor == Cursors.HSplit &&
                    diagnosticsSplitter.Bottom == diagnosticsPanelFromForm.Top,
                "La palette inferiore non ha uno splitter orizzontale correttamente posizionato.");

            diagnosticsPanelFromForm.Height = 210;
            form.PerformLayout();
            InvokeHandler(form, "splitterDiagnostics_SplitterMoved", diagnosticsSplitter,
                new SplitterEventArgs(0, 0, 0, 0));
            Require(Convert.ToInt32(GetPrivateField<object>(form, "diagnosticsExpandedHeight")) == 210,
                "L'altezza impostata tramite splitter non viene memorizzata.");

            outlinePin.PerformClick();
            PumpMessages(20);
            Require(outlinePanel.Width == 28 && outlinePin.Text == "\uE76C",
                "L'Outline non entra nello stato auto-hide compatto.");
            InvokeHandler(form, "panelOutline_MouseEnter", outlinePanel, EventArgs.Empty);
            Require(outlinePanel.Width >= 300,
                "L'Outline non si riapre durante l'auto-hide.");
            outlinePin.PerformClick();
            Require(outlinePin.Text == "\uE718" && outlinePanel.Width >= 300,
                "L'Outline non torna nello stato pinnato.");

            diagnosticsPin.PerformClick();
            PumpMessages(20);
            Require(diagnosticsPanel.Height == 26 && diagnosticsPin.Text == "\uE70E" && !diagnosticsSplitter.Visible,
                "La Diagnostica non entra nello stato auto-hide compatto.");
            InvokeHandler(form, "panelDiagnostics_MouseEnter", diagnosticsPanel, EventArgs.Empty);
            Require(diagnosticsPanel.Height == 210 && diagnosticsSplitter.Visible && !diagnosticsSplitter.Enabled,
                "La Diagnostica non ripristina l'altezza ridimensionata durante l'auto-hide.");
            diagnosticsPin.PerformClick();
            Require(diagnosticsPin.Text == "\uE718" && diagnosticsPanel.Height == 210 && diagnosticsSplitter.Enabled,
                "La Diagnostica non torna nello stato pinnato.");
            Console.WriteLine("PASS gutter non selezionabile e palette ridimensionabili, pinnabili con auto-hide");
        }

        private static void AssertSelectionStableDuringHighlight(RichTextBox editor)
        {
            int insertion = editor.Text.IndexOf("ret value", StringComparison.Ordinal);
            editor.Select(insertion, 0);
            editor.SelectedText = " ";
            int expectedStart = insertion + 1;
            int selectionNotifications = 0;
            EventHandler handler = (sender, eventArgs) => selectionNotifications++;
            editor.SelectionChanged += handler;
            try
            {
                PumpMessages(180);
            }
            finally
            {
                editor.SelectionChanged -= handler;
            }

            Require(editor.SelectionStart == expectedStart && editor.SelectionLength == 0,
                "La ricolorazione ha modificato la selezione dell'utente.");
            Require(selectionNotifications <= 1,
                "La ricolorazione ha generato " + selectionNotifications + " notifiche di selezione interne.");
            Console.WriteLine("PASS selezione stabile; notifiche interne=" + selectionNotifications);
        }

        private static void AssertWholeLibraryCutIsClean(RichTextBox editor, ListBox diagnostics)
        {
            SetSource(editor);
            int directiveLength = "@@'Console'\n".Length;
            editor.Select(0, directiveLength);
            editor.SelectedText = string.Empty;
            PumpMessages(180);

            Require(editor.Text.StartsWith("main()", StringComparison.Ordinal),
                "Il taglio completo della direttiva non ha lasciato il buffer atteso.");
            Require(diagnostics.Items.Count == 0,
                "Il taglio completo di una direttiva valida ha prodotto diagnostiche residue.");
            Require(editor.SelectionStart == 0 && editor.SelectionLength == 0,
                "Il taglio completo non ha conservato il caret nella posizione prevista.");
            Console.WriteLine("PASS taglio completo della libreria senza diagnostiche");
        }

        private static void AssertColoredFoldFromFunctionHeader(
            MainForm form,
            RichTextBox editor,
            RichTextBox foldedView,
            RichTextBox lineNumbers)
        {
            const string source =
                "@@'Console'\n" +
                "main() out int\n" +
                "{\n" +
                "    int value << 1;\n" +
                "    ret value;\n" +
                "}";
            editor.Text = source;
            editor.Select(source.IndexOf("main", StringComparison.Ordinal) + 1, 0);
            InvokeHandler(
                form,
                "richTextBoxMainEditor_KeyDown",
                editor,
                new KeyEventArgs(Keys.Control | Keys.M));
            PumpMessages(120);

            Require(foldedView.Visible && !editor.Visible,
                "Il blocco non è stato compresso partendo dalla firma della funzione.");
            Require(foldedView.Text.Contains("⋯ blocco compresso"),
                "La proiezione compressa non contiene il marcatore previsto.");
            Require(lineNumbers.Visible,
                "La colonna dei numeri di riga è scomparsa nella vista compressa.");
            string[] visibleLineNumbers = lineNumbers.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Require(visibleLineNumbers.Contains("⋯"),
                "La colonna non segnala le righe compresse.");
            Require(visibleLineNumbers.Contains("6"),
                "La parentesi finale non conserva il numero di riga sorgente.");

            int returnType = foldedView.Text.IndexOf("int", StringComparison.Ordinal);
            foldedView.Select(returnType, 3);
            Require(foldedView.SelectionColor == Color.FromArgb(78, 201, 176),
                "Il tipo visibile nella proiezione ha perso il colore sintattico.");

            int marker = foldedView.Text.IndexOf("⋯ blocco compresso", StringComparison.Ordinal);
            foldedView.Select(marker, 1);
            Require(foldedView.SelectionColor == Color.FromArgb(220, 220, 170),
                "Il marcatore della proiezione non ha la formattazione dedicata.");

            InvokeHandler(
                form,
                "richTextBoxFoldedView_KeyDown",
                foldedView,
                new KeyEventArgs(Keys.Control | Keys.M));
            PumpMessages(120);
            Require(editor.Visible && !foldedView.Visible,
                "Il comando non ha ripristinato la vista sorgente.");
            Require(editor.Text == source,
                "Compressione ed espansione hanno modificato il sorgente.");
            Console.WriteLine("PASS folding con colori, numeri originali e sorgente invariato");
        }

        private static void AssertMultipleIndependentFolds(
            MainForm form,
            RichTextBox editor,
            RichTextBox foldedView)
        {
            const string source =
                "first() out int\n{\n    int firstValue << 1;\n    ret firstValue;\n}\n" +
                "second() out int\n{\n    int secondValue << 2;\n    ret secondValue;\n}";
            editor.Text = source;
            editor.Select(source.IndexOf("first()", StringComparison.Ordinal), 0);
            PumpMessages(180);
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor,
                new KeyEventArgs(Keys.Control | Keys.M));
            PumpMessages(80);

            int secondHeader = foldedView.Text.IndexOf("second()", StringComparison.Ordinal);
            Require(secondHeader >= 0, "La prima compressione ha nascosto il secondo blocco indipendente.");
            foldedView.Select(secondHeader, 0);
            InvokeHandler(form, "richTextBoxFoldedView_KeyDown", foldedView,
                new KeyEventArgs(Keys.Control | Keys.M));
            PumpMessages(80);

            Require(CountOccurrences(foldedView.Text, "⋯ blocco compresso") == 2,
                "Non è stato possibile mantenere compressi due blocchi indipendenti.");
            Require(editor.Text == source, "La compressione multipla ha modificato il sorgente.");

            int firstMarker = foldedView.Text.IndexOf("⋯ blocco compresso", StringComparison.Ordinal);
            foldedView.Select(firstMarker, 0);
            InvokeHandler(form, "richTextBoxFoldedView_KeyDown", foldedView,
                new KeyEventArgs(Keys.Control | Keys.M));
            PumpMessages(80);
            Require(CountOccurrences(foldedView.Text, "⋯ blocco compresso") == 1,
                "L'espansione di un blocco ha alterato anche gli altri blocchi compressi.");
            Require(foldedView.Text.Contains("firstValue") && !foldedView.Text.Contains("secondValue"),
                "L'espansione selettiva non ha ripristinato soltanto il blocco scelto.");

            InvokeHandler(form, "richTextBoxFoldedView_KeyDown", foldedView,
                new KeyEventArgs(Keys.Control | Keys.Shift | Keys.M));
            PumpMessages(80);
            Require(editor.Visible && editor.Text == source,
                "Espandi tutto non ha ripristinato il sorgente dopo il folding multiplo.");
            Console.WriteLine("PASS folding multiplo con espansione selettiva");
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int position = 0;
            while ((position = text.IndexOf(value, position, StringComparison.Ordinal)) >= 0)
            {
                count++;
                position += value.Length;
            }

            return count;
        }

        private static void AssertMouseGesturePreservesNativeSelection(MainForm form, RichTextBox editor)
        {
            SetSource(editor);
            int wordStart = editor.Text.IndexOf("value", StringComparison.Ordinal);
            InvokeMouseHandler(form, "richTextBoxMainEditor_MouseDown", editor, MouseButtons.Left);
            editor.Select(wordStart, "value".Length);
            PumpMessages(180);
            Require(string.Equals(editor.SelectedText, "value", StringComparison.Ordinal),
                "Il matching dei delimitatori ha sostituito la selezione durante il gesto mouse.");

            InvokeMouseHandler(form, "richTextBoxMainEditor_MouseUp", editor, MouseButtons.Left);
            PumpMessages(180);
            Require(string.Equals(editor.SelectedText, "value", StringComparison.Ordinal),
                "Il matching dei delimitatori ha sostituito la selezione dopo il rilascio mouse.");
            Console.WriteLine("PASS selezione nativa preservata durante e dopo il gesto mouse");
        }

        private static void AssertUndoRestoresLibrary(RichTextBox editor)
        {
            Require(editor.CanUndo, "Undo non disponibile dopo il taglio della direttiva.");
            editor.Undo();
            PumpMessages(180);
            Require(editor.Text == Source, "Undo non ha ripristinato la direttiva di libreria.");
            Console.WriteLine("PASS Undo del taglio libreria");
        }

        private static void AssertPartialLibraryCutIsRecoverable(RichTextBox editor, ListBox diagnostics)
        {
            editor.Select(0, 2);
            editor.SelectedText = string.Empty;
            PumpMessages(180);
            Require(editor.Text.StartsWith("'Console'", StringComparison.Ordinal),
                "Il taglio parziale non ha prodotto lo stato transitorio previsto.");
            Require(diagnostics.Items.Count > 0,
                "Lo stato transitorio privo di @@ dovrebbe essere diagnosticato.");
            Require(editor.SelectionStart >= 0 && editor.SelectionStart <= editor.TextLength,
                "Il caret è uscito dai limiti dopo la diagnostica transitoria.");
            Console.WriteLine("PASS taglio parziale recuperabile; diagnostiche attese=" + diagnostics.Items.Count);
        }

        private static void AssertRapidTransientEditsAreRecoverable(MainForm form, RichTextBox editor)
        {
            for (int index = 0; index < 80; index++)
            {
                int position = Math.Min(editor.TextLength, index % 7);
                editor.Select(position, 0);
                editor.SelectedText = index % 2 == 0 ? "@" : "'";
                if (editor.TextLength > 120)
                {
                    editor.Select(0, 3);
                    editor.SelectedText = string.Empty;
                }

                Application.DoEvents();
            }

            PumpMessages(220);
            Require(editor.SelectionStart >= 0 && editor.SelectionStart <= editor.TextLength,
                "Il caret è uscito dai limiti durante le modifiche rapide.");
            Require(GetPrivateField<string>(form, "lastAnalyzedText") == editor.Text,
                "L'analisi in background ha applicato uno snapshot precedente al testo corrente.");
            Require(Convert.ToInt64(GetPrivateField<object>(form, "analysisAppliedVersion")) ==
                    Convert.ToInt64(GetPrivateField<object>(form, "analysisRequestVersion")),
                "L'ultima analisi in background non ha sostituito le richieste obsolete.");
            Console.WriteLine("PASS 80 modifiche transitorie rapide con snapshot obsoleti scartati");
        }

        private static void AssertContextualCompletion(MainForm form, RichTextBox editor)
        {
            editor.Text = "wh";
            editor.Select(2, 0);
            editor.Focus();
            PumpMessages(220);
            ListBox completion = FindControl<ListBox>(form, "completionListBox");
            Require(completion.Visible, "Il completamento automatico non è apparso dopo il prefisso.");
            DdfCompletionItem whileItem = completion.Items.Cast<DdfCompletionItem>()
                .SingleOrDefault(item => item.DisplayText == "while");
            Require(whileItem != null,
                "Il completamento non propone la parola chiave while.");
            Require(completion.DrawMode == DrawMode.OwnerDrawFixed && completion.ItemHeight >= 24 && completion.Width >= 500,
                "Il popup non usa righe strutturate sufficienti per categoria, tipo e origine.");
            Require(whileItem.CategoryLabel == "parola chiave" && whileItem.Origin == "linguaggio DDF" &&
                    !string.IsNullOrEmpty(whileItem.Glyph),
                "La voce di completamento non espone icona, categoria e origine.");

            completion.SelectedItem = whileItem;
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Tab));
            PumpMessages(120);
            Require(editor.Text == "while" && editor.SelectionStart == "while".Length,
                "Tab non ha applicato il completamento selezionato.");
            Require(editor.CanUndo, "Il completamento non ha creato un'operazione Undo.");
            editor.Undo();
            PumpMessages(100);
            Require(editor.Text == "wh", "Undo non ha ripristinato il prefisso precedente al completamento.");

            const string typedSource = "main() out string { int count; string text; ret  }";
            editor.Text = typedSource;
            editor.Select(typedSource.IndexOf("ret  ", StringComparison.Ordinal) + 5, 0);
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor,
                new KeyEventArgs(Keys.Control | Keys.Space));
            PumpMessages(180);
            DdfCompletionResult typedResult = GetPrivateField<DdfCompletionResult>(form, "activeCompletionResult");
            Require(typedResult != null && typedResult.ExpectedType == "string" &&
                    typedResult.Context == DdfCompletionContextKind.Expression,
                "Il popup non riceve il tipo atteso dal contesto di return.");
            Require(((DdfCompletionItem)completion.Items[0]).DisplayText == "text" &&
                    ((DdfCompletionItem)completion.Items[0]).TypeName == "string" &&
                    ((DdfCompletionItem)completion.Items[0]).Origin == "documento corrente",
                "Il ranking UI non privilegia il simbolo locale compatibile con il tipo atteso.");
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Escape));

            editor.Text = string.Empty;
            editor.Select(0, 0);
            FindToolbarButton(form, "toolbarCompletionButton").PerformClick();
            PumpMessages(180);
            Require(completion.Visible && completion.Items.Count > 0,
                "La icon bar non ha mostrato l'elenco completo.");
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Escape));
            Require(!completion.Visible, "Esc non ha chiuso il completamento.");

            editor.Text = "wh";
            editor.Select(editor.TextLength, 0);
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor,
                new KeyEventArgs(Keys.Control | Keys.Space));
            editor.Text = "\"inside\"";
            editor.Select(4, 0);
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor,
                new KeyEventArgs(Keys.Control | Keys.Space));
            PumpMessages(220);
            Require(!completion.Visible && GetPrivateField<DdfCompletionResult>(form, "activeCompletionResult") == null,
                "Un completamento calcolato su uno snapshot obsoleto è rimasto visibile.");
            Console.WriteLine("PASS completamento contestuale con Tab, Undo, Ctrl+Spazio ed Esc");
        }

        private static void AssertNavigableSnippets(MainForm form, RichTextBox editor)
        {
            const string source = "main() out int\n{\n    i\n}";
            editor.Text = source;
            editor.Select(source.IndexOf("i\n", StringComparison.Ordinal) + 1, 0);
            editor.Focus();
            PumpMessages(220);

            ListBox completion = FindControl<ListBox>(form, "completionListBox");
            DdfCompletionItem snippet = completion.Items.Cast<DdfCompletionItem>().Single(item =>
                item.Kind == DdfCompletionKind.Snippet && item.Snippet.Prefix == "if");
            completion.SelectedItem = snippet;
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Tab));
            Require(editor.SelectedText == "condition", "Lo snippet non ha selezionato il primo campo.");
            Require(editor.Text.Contains("    if(condition)\n    {\n        statement;\n    }"),
                "Lo snippet non ha rispettato l'indentazione del contesto.");

            editor.SelectedText = "value > 0";
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Tab));
            Require(editor.SelectedText == "statement", "Tab non ha raggiunto il campo successivo dello snippet.");
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Shift | Keys.Tab));
            Require(editor.SelectedText == "value > 0", "Shift+Tab non è tornato al campo modificato.");
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Tab));
            editor.SelectedText = "value++";
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Tab));
            Require(editor.SelectionLength == 0 && editor.Text.Contains("value++;"),
                "L'ultimo Tab non ha concluso correttamente la sessione snippet.");

            editor.Text = source;
            editor.Select(source.IndexOf("i\n", StringComparison.Ordinal) + 1, 0);
            PumpMessages(180);
            completion.SelectedItem = completion.Items.Cast<DdfCompletionItem>().Single(item =>
                item.Kind == DdfCompletionKind.Snippet && item.Snippet.Prefix == "if");
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Tab));
            editor.Undo();
            PumpMessages(120);
            Require(editor.Text == source, "Undo non ha rimosso l'inserimento dello snippet in un solo passo.");
            Console.WriteLine("PASS snippet indentati con campi Tab/Shift+Tab e singolo Undo");
        }

        private static void AssertSignatureHelp(MainForm form, RichTextBox editor)
        {
            const string source =
                "combine(int count, string text) out int { ret count; }\n" +
                "main() out int { ret combine(1, ); }";
            editor.Text = source;
            int secondParameter = source.IndexOf(", )", StringComparison.Ordinal) + 2;
            editor.Select(secondParameter, 0);
            editor.Focus();
            PumpMessages(220);

            Panel popup = FindControl<Panel>(form, "signatureHelpPanel");
            Label signature = FindControl<Label>(form, "signatureHelpLabel");
            Label parameter = FindControl<Label>(form, "signatureParameterLabel");
            Require(popup.Visible, "La Signature Help non è apparsa dentro la chiamata.");
            Require(signature.Text.Contains("combine(int count, string text) out int") &&
                    signature.Text.Contains("documento corrente"),
                "La Signature Help non mostra firma e origine della funzione.");
            Require(parameter.Text.Contains("Parametro 2/2") && parameter.Text.Contains("string text") &&
                    parameter.Font.Bold,
                "La Signature Help non evidenzia il parametro corrente.");

            editor.Select(source.LastIndexOf("}", StringComparison.Ordinal) + 1, 0);
            PumpMessages(140);
            Require(!popup.Visible, "La Signature Help è rimasta visibile fuori dalla chiamata.");

            editor.Select(secondParameter, 0);
            PumpMessages(140);
            Require(popup.Visible, "La Signature Help non è riapparsa tornando nella chiamata.");
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Escape));
            Require(!popup.Visible, "Esc non ha chiuso la Signature Help.");
            Console.WriteLine("PASS Signature Help locale con parametro corrente, riposizionamento ed Esc");
        }

        private static void AssertDocumentFormatting(MainForm form, RichTextBox editor)
        {
            const string source = "// formatter smoke\nmain()out int{int value<<1+2;ret value;}";
            const string expected =
                "// formatter smoke\n" +
                "main() out int\n" +
                "{\n" +
                "    int value << 1 + 2;\n" +
                "\n" +
                "    ret value;\n" +
                "}";
            editor.Text = source;
            int caret = source.LastIndexOf("value", StringComparison.Ordinal) + "value".Length;
            editor.Select(caret, 0);
            editor.Focus();
            PumpMessages(180);
            InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
            ToolStripMenuItem format = FindMenuItem(form, "formatDocumentMenuItem");
            Require(format.Enabled, "Formatta documento non è abilitato con un sorgente presente.");
            format.PerformClick();
            PumpMessages(180);
            Require(editor.Text == expected, "Formatta documento non ha prodotto il testo atteso.");
            Require(editor.SelectionStart >= "value".Length &&
                    editor.Text.Substring(editor.SelectionStart - "value".Length, "value".Length) == "value",
                "La formattazione non ha mantenuto il cursore sul simbolo corrente.");
            editor.Select(0, 2);
            Require(editor.SelectionColor == Color.FromArgb(106, 153, 85),
                "Formatta documento non ha mantenuto verde il commento iniziale.");
            int firstKeyword = expected.IndexOf("int", StringComparison.Ordinal);
            editor.Select(firstKeyword, 3);
            Require(editor.SelectionColor == Color.FromArgb(78, 201, 176),
                "Formatta documento ha esteso il verde del commento al codice successivo.");

            format.PerformClick();
            PumpMessages(80);
            Require(editor.Text == expected, "Una seconda formattazione ha modificato il risultato.");
            Require(editor.CanUndo, "La formattazione non è annullabile.");
            editor.Undo();
            PumpMessages(160);
            Require(editor.Text == source, "Undo non ha ripristinato il documento prima della formattazione.");
            Console.WriteLine("PASS formattazione idempotente con cursore preservato e singolo Undo");
        }

        private static void AssertSemanticNavigationAndRename(MainForm form, RichTextBox editor)
        {
            const string source =
                "/// semantic smoke\nfirst() out int { int value; ret value; } second() out int { int value; ret value; }";
            const string renamed =
                "/// semantic smoke\nfirst() out int { int result; ret result; } second() out int { int value; ret value; }";
            editor.Text = source;
            PumpMessages(220);

            int reference = source.IndexOf("ret value", StringComparison.Ordinal) + 4;
            int declaration = source.IndexOf("value", StringComparison.Ordinal);
            editor.Select(reference, 0);
            InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
            ToolStripMenuItem goToDefinition = FindMenuItem(form, "goToDefinitionMenuItem");
            ToolStripMenuItem rename = FindMenuItem(form, "renameSymbolMenuItem");
            Require(goToDefinition.Enabled && rename.Enabled,
                "I comandi semantici non sono abilitati su un riferimento risolto.");

            goToDefinition.PerformClick();
            Require(editor.SelectionStart == declaration && editor.SelectedText == "value",
                "Vai alla definizione non ha selezionato la dichiarazione corretta.");

            Point referencePoint = editor.GetPositionFromCharIndex(reference);
            InvokeMouseMoveHandler(form, "richTextBoxMainEditor_MouseMove", editor, referencePoint);
            DdfDocumentSymbol hovered = GetPrivateField<DdfDocumentSymbol>(form, "hoveredSymbol");
            Require(hovered != null && hovered.Name == "value" && hovered.SelectionStart == declaration,
                "L'hover non espone le informazioni del simbolo risolto.");
            DdfHoverInfo variableHover = GetPrivateField<DdfHoverInfo>(form, "activeHoverInfo");
            Require(variableHover != null && variableHover.TypeName == "int" &&
                    variableHover.ReferenceCount == 1 && !string.IsNullOrEmpty(variableHover.Origin),
                "L'hover strutturato non espone tipo, origine e riferimenti della variabile.");

            int functionStart = source.IndexOf("first", StringComparison.Ordinal);
            InvokeMouseMoveHandler(form, "richTextBoxMainEditor_MouseMove", editor,
                editor.GetPositionFromCharIndex(functionStart));
            DdfHoverInfo functionHover = GetPrivateField<DdfHoverInfo>(form, "activeHoverInfo");
            Require(functionHover != null && functionHover.Signature == "first() out int" &&
                    functionHover.Documentation == "semantic smoke" && functionHover.DeclarationLine == 2,
                "L'hover della funzione non mostra firma, documentazione e dichiarazione.");

            editor.Select(reference, 0);
            SetPrivateField(form, "requestSymbolRename", new Func<string, string>(name => "result"));
            rename.PerformClick();
            PumpMessages(220);
            Require(editor.Text == renamed,
                "Rinomina ha modificato simboli omonimi fuori dall'ambito selezionato o ha perso riferimenti.");
            Require(editor.SelectedText == "result", "Rinomina non ha mantenuto selezionato il simbolo corrente.");
            int firstKeyword = renamed.IndexOf("int", StringComparison.Ordinal);
            editor.Select(firstKeyword, 3);
            Require(editor.SelectionColor == Color.FromArgb(78, 201, 176),
                "Rinomina ha lasciato il codice precedente con il colore verde del commento iniziale.");
            Require(editor.CanUndo, "Rinomina non è annullabile.");
            editor.Undo();
            PumpMessages(180);
            Require(editor.Text == source, "Undo non ha ripristinato il documento precedente alla rinomina.");

            const string standardSource = "main() out int { print(\"ok\"); ret 0; }";
            editor.Text = standardSource;
            PumpMessages(180);
            int printStart = standardSource.IndexOf("print", StringComparison.Ordinal);
            InvokeMouseMoveHandler(form, "richTextBoxMainEditor_MouseMove", editor,
                editor.GetPositionFromCharIndex(printStart));
            DdfHoverInfo standardHover = GetPrivateField<DdfHoverInfo>(form, "activeHoverInfo");
            Require(standardHover != null && standardHover.Origin == "libreria standard" &&
                    standardHover.Signature == "print(string) out void" &&
                    standardHover.Documentation.Contains("Output"),
                "L'hover non documenta le funzioni della libreria standard.");
            Console.WriteLine("PASS hover strutturato, Vai alla definizione e rinomina scoped con Undo");
        }

        private static void AssertClosingBraceAlignment(MainForm form, RichTextBox editor)
        {
            const string source =
                "main() out int\n{\n    if(true)\n    {\n        int value;\n            ";
            const string expected =
                "main() out int\n{\n    if(true)\n    {\n        int value;\n    }";
            editor.Text = source;
            editor.Select(editor.TextLength, 0);
            PumpMessages(180);
            var keyPress = new KeyPressEventArgs('}');
            InvokeHandler(form, "richTextBoxMainEditor_KeyPress", editor, keyPress);
            PumpMessages(180);
            Require(keyPress.Handled, "La graffa chiusa non è stata gestita dall'editor.");
            Require(editor.Text == expected,
                "La graffa chiusa non è allineata alla graffa aperta corrispondente.");
            Require(editor.SelectionStart == expected.Length, "Il cursore non segue la graffa riallineata.");
            Require(editor.CanUndo, "Il riallineamento della graffa non è annullabile.");
            editor.Undo();
            PumpMessages(140);
            Require(editor.Text == source, "Undo non ripristina l'indentazione precedente alla graffa.");
            Console.WriteLine("PASS graffa chiusa allineata al blocco con Undo");
        }

        private static void AssertCodingGesturesAndContextMenu(MainForm form, RichTextBox editor)
        {
            editor.Text = string.Empty;
            var openingParenthesis = new KeyPressEventArgs('(');
            InvokeHandler(form, "richTextBoxMainEditor_KeyPress", editor, openingParenthesis);
            Require(openingParenthesis.Handled && editor.Text == "()" && editor.SelectionStart == 1,
                "La parentesi aperta non crea la coppia con il cursore al centro.");
            var closingParenthesis = new KeyPressEventArgs(')');
            InvokeHandler(form, "richTextBoxMainEditor_KeyPress", editor, closingParenthesis);
            Require(closingParenthesis.Handled && editor.Text == "()" && editor.SelectionStart == 2,
                "La parentesi chiusa già presente viene duplicata.");

            editor.Text = "{}";
            editor.Select(1, 0);
            var enter = new KeyEventArgs(Keys.Enter);
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, enter);
            Require(enter.Handled && editor.Text == "{\n    \n}" && editor.SelectionStart == 6,
                "Invio tra graffe non apre un blocco indentato.");
            editor.Undo();
            Require(editor.Text == "{}", "Il blocco automatico non è una singola operazione Undo.");

            editor.Text = "[]";
            editor.Select(1, 0);
            var backspace = new KeyEventArgs(Keys.Back);
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, backspace);
            Require(backspace.Handled && editor.TextLength == 0,
                "Backspace tra una coppia vuota non elimina entrambi i caratteri.");

            editor.Text = "value";
            editor.Select(0, editor.TextLength);
            var quote = new KeyPressEventArgs('"');
            InvokeHandler(form, "richTextBoxMainEditor_KeyPress", editor, quote);
            Require(quote.Handled && editor.Text == "\"value\"" && editor.SelectedText == "value",
                "Le virgolette non racchiudono la selezione conservandola.");

            editor.Text = "    int value;\n    ret value;";
            editor.Select(0, editor.TextLength);
            FindMenuItem(form, "toggleLineCommentMenuItem").PerformClick();
            Require(editor.Text == "    // int value;\n    // ret value;",
                "Commenta selezione non conserva il rientro delle righe.");
            FindToolbarButton(form, "toolbarCommentButton").PerformClick();
            Require(editor.Text == "    int value;\n    ret value;",
                "Decommenta dalla toolbar non ripristina il testo originale.");
            editor.Select(0, 0);
            InvokeProcessCmdKey(form, Keys.Control | Keys.Shift | Keys.D7);
            Require(editor.Text.StartsWith("    // int value;", StringComparison.Ordinal),
                "La variante italiana Ctrl+/ non commenta la riga corrente.");
            InvokeProcessCmdKey(form, Keys.Control | Keys.Shift | Keys.D7);

            ContextMenuStrip contextMenu = editor.ContextMenuStrip;
            Require(contextMenu != null && contextMenu.Name == "editorContextMenu",
                "Il menu contestuale dell'editor non è installato.");
            InvokeHandler(form, "editorContextMenu_Opening", contextMenu,
                new System.ComponentModel.CancelEventArgs());
            string[] expectedItems =
            {
                "contextUndoItem", "contextRedoItem", "contextCutItem", "contextCopyItem",
                "contextPasteItem", "contextSelectAllItem", "contextFindItem", "contextRenameItem", "contextFindReferencesItem", "contextCommentItem",
                "contextDuplicateLinesItem", "contextMoveLinesUpItem", "contextMoveLinesDownItem", "contextDeleteLinesItem",
                "contextExpandSelectionItem", "contextShrinkSelectionItem", "contextMatchingDelimiterItem"
                , "contextSelectNextOccurrenceItem", "contextSelectAllOccurrencesItem", "contextQuickFixItem"
            };
            foreach (string name in expectedItems)
                Require(contextMenu.Items.OfType<ToolStripMenuItem>().Any(item => item.Name == name),
                    "Comando mancante nel menu contestuale: " + name);
            Require(IsLight(contextMenu.BackColor), "Il menu contestuale non usa il tema chiaro.");

            const string lines = "one\ntwo\nthree";
            editor.Text = lines;
            editor.Select(4, 0);
            InvokeProcessCmdKey(form, Keys.Control | Keys.Shift | Keys.D);
            Require(editor.Text == "one\ntwo\ntwo\nthree", "Ctrl+Shift+D non duplica la riga corrente.");
            editor.Undo();
            editor.Select(4, 3);
            InvokeProcessCmdKey(form, Keys.Alt | Keys.Up);
            Require(editor.Text == "two\none\nthree" && editor.SelectedText == "two",
                "Alt+Su non sposta la riga conservando la selezione.");
            editor.Undo();
            editor.Select(4, 3);
            FindToolbarButton(form, "toolbarMoveLinesDownButton").PerformClick();
            Require(editor.Text == "one\nthree\ntwo", "La toolbar non sposta la riga verso il basso.");
            editor.Undo();
            editor.Select(4, 0);
            InvokeProcessCmdKey(form, Keys.Control | Keys.Shift | Keys.K);
            Require(editor.Text == "one\nthree", "Ctrl+Shift+K non elimina la riga completa.");
            editor.Undo();

            editor.Text = "main()\n{\n    \n}";
            editor.Select(13, 0);
            Clipboard.SetText("    if(true)\r\n    {\r\n        ret 1;\r\n    }");
            InvokeProcessCmdKey(form, Keys.Control | Keys.V);
            Require(editor.Text == "main()\n{\n    if(true)\n    {\n        ret 1;\n    }\n}",
                "L'incolla multilinea non adatta il rientro al blocco corrente.");
            Console.WriteLine("PASS coppie automatiche, Invio/Backspace, commenti e menu contestuale");
        }

        private static void AssertSyntacticSelectionAndDelimiterNavigation(MainForm form, RichTextBox editor)
        {
            const string source = "@@'Console'\nmain() out int { ret value + 1; }";
            editor.Text = source;
            int caret = source.IndexOf("value", StringComparison.Ordinal) + 2;
            editor.Select(caret, 0);
            InvokeProcessCmdKey(form, Keys.Shift | Keys.Alt | Keys.Right);
            Require(editor.SelectedText == "value", "L'espansione non parte dal token sotto il cursore.");
            InvokeProcessCmdKey(form, Keys.Shift | Keys.Alt | Keys.Right);
            Require(editor.SelectedText == "value + 1", "L'espansione non raggiunge l'espressione.");

            FindMenuItem(form, "newMenuItem").PerformClick();
            PumpMessages(80);
            RichTextBox secondEditor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
            InvokeProcessCmdKey(form, Keys.Shift | Keys.Alt | Keys.Left);
            Require(secondEditor.SelectionLength == 0,
                "Una nuova scheda ha ereditato la cronologia di selezione di un altro documento.");
            TabControl documentTabs = FindControl<TabControl>(form, "documentTabs");
            documentTabs.SelectedIndex = 0;
            PumpMessages(100);
            InvokeProcessCmdKey(form, Keys.Shift | Keys.Alt | Keys.Left);
            Require(editor.SelectedText == "value",
                "Il cambio scheda non conserva la cronologia sintattica del documento originale.");
            InvokeProcessCmdKey(form, Keys.Shift | Keys.Alt | Keys.Right);
            FindToolbarButton(form, "toolbarExpandSelectionButton").PerformClick();
            Require(editor.SelectedText == "ret value + 1;", "La toolbar non espande fino all'istruzione.");
            InvokeProcessCmdKey(form, Keys.Shift | Keys.Alt | Keys.Left);
            Require(editor.SelectedText == "value + 1", "La riduzione non ripristina il livello precedente.");
            FindToolbarButton(form, "toolbarShrinkSelectionButton").PerformClick();
            Require(editor.SelectedText == "value", "La cronologia di riduzione non conserva i livelli selezionati.");

            int open = source.IndexOf('(', source.IndexOf("main", StringComparison.Ordinal));
            int close = source.IndexOf(')', open);
            editor.Select(open, 0);
            FindToolbarButton(form, "toolbarMatchingDelimiterButton").PerformClick();
            Require(editor.SelectionStart == close && editor.SelectionLength == 0,
                "La navigazione non raggiunge il delimitatore di chiusura.");
            InvokeProcessCmdKey(form, Keys.Control | Keys.Shift | Keys.OemPipe);
            Require(editor.SelectionStart == open, "La scorciatoia non ritorna al delimitatore di apertura.");
            documentTabs.SelectedIndex = 1;
            PumpMessages(60);
            FindMenuItem(form, "closeDocumentMenuItem").PerformClick();
            PumpMessages(80);
            Console.WriteLine("PASS selezione sintattica progressiva, riduzione e navigazione delimitatori");
        }

        private static void AssertMultipleSelections(MainForm form, RichTextBox editor)
        {
            const string source = "int value;\nvalue << value + 1; // value";
            editor.Text = source;
            int first = source.IndexOf("value", StringComparison.Ordinal);
            editor.Select(first + 2, 0);
            InvokeProcessCmdKey(form, Keys.Control | Keys.D);
            Require(editor.SelectedText == "value", "Il primo Ctrl+D non seleziona il simbolo corrente.");
            InvokeProcessCmdKey(form, Keys.Control | Keys.D);
            ToolStripStatusLabel positionLabel = (ToolStripStatusLabel)FindControl<StatusStrip>(form, "statusStripMain")
                .Items["statusPositionLabel"];
            Require(positionLabel.Text.Contains("2 cursori"),
                "Il secondo Ctrl+D non aggiunge un cursore sulla prossima occorrenza.");
            var typed = new KeyPressEventArgs('x');
            InvokeHandler(form, "richTextBoxMainEditor_KeyPress", editor, typed);
            Require(typed.Handled && editor.Text == "int x;\nx << value + 1; // value",
                "La digitazione non sostituisce simultaneamente le selezioni attive.");
            Require(editor.CanUndo, "La modifica multi-cursore non è annullabile.");
            editor.Undo();
            PumpMessages(100);
            Require(editor.Text == source, "Undo non ripristina in un passo la modifica multi-cursore.");

            editor.Select(first, "value".Length);
            InvokeProcessCmdKey(form, Keys.Control | Keys.Shift | Keys.L);
            Clipboard.SetText("item");
            InvokeProcessCmdKey(form, Keys.Control | Keys.V);
            Require(editor.Text == "int item;\nitem << item + 1; // value",
                "Seleziona tutte e Incolla hanno modificato commenti o perso un'occorrenza del codice.");
            editor.Undo();
            PumpMessages(80);

            editor.Select(0, 0);
            InvokePrivate(form, "addCursorAtPosition", editor.Text.IndexOf('\n') + 1);
            var tab = new KeyEventArgs(Keys.Tab);
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, tab);
            Require(tab.Handled && editor.Text.StartsWith("    int value;\n    value", StringComparison.Ordinal),
                "Tab non indenta simultaneamente tutti i cursori.");
            var insert = new KeyPressEventArgs('>');
            InvokeHandler(form, "richTextBoxMainEditor_KeyPress", editor, insert);
            Require(editor.Text.StartsWith("    >int value;\n    >value", StringComparison.Ordinal),
                "Il cursore aggiunto tramite il flusso Alt+Click non partecipa alla scrittura.");
            InvokeProcessCmdKey(form, Keys.Escape);
            Console.WriteLine("PASS cursori multipli, occorrenze, scrittura/incolla simultanei e Undo unico");
        }

        private static void AssertWorkspaceNavigation(MainForm form, RichTextBox editor, ListBox diagnostics)
        {
            string directory = Path.Combine(Path.GetTempPath(), "ddf-editor-workspace-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(directory, "lib"));
                string mainPath = Path.Combine(directory, "main.ddf");
                string helperPath = Path.Combine(directory, "lib", "helpers.ddf");
                DdfDocumentFile.Save(mainPath, "main() out int { ret helper(); }");
                DdfDocumentFile.Save(helperPath, "// library declaration\nhelper() out int { ret 1; }");

                SetPrivateField(form, "showWorkspaceDialog", new Func<FolderBrowserDialog, DialogResult>(dialog =>
                {
                    dialog.SelectedPath = directory;
                    return DialogResult.OK;
                }));
                FindMenuItem(form, "openWorkspaceMenuItem").PerformClick();
                PumpMessages(160);
                TreeView workspaceTree = FindControl<TreeView>(form, "treeViewWorkspace");
                Require(workspaceTree.Nodes.Count == 1 && FindControl<Label>(form, "labelWorkspace").Text.Contains("2 file"),
                    "Apri cartella non ha popolato l'explorer con i sorgenti DDF.");

                TreeNode mainNode = FindTreeNodeByTag(workspaceTree.Nodes, mainPath);
                Require(mainNode != null, "Il file principale non è presente nell'explorer workspace.");
                GetPrivateField<DocumentSession>(form, "documentSession").SetUntitled();
                InvokeHandler(form, "treeViewWorkspace_NodeMouseDoubleClick", workspaceTree,
                    new TreeNodeMouseClickEventArgs(mainNode, MouseButtons.Left, 2, 0, 0));
                PumpMessages(220);
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(editor.Text.Contains("helper()"), "Il doppio clic sul file workspace non ha aperto il documento.");
                Require(!diagnostics.Items.Cast<object>().Any(item => item.ToString().Contains("DDF201")),
                    "Un simbolo definito in un altro file è ancora segnalato come non risolto.");

                int helperReference = editor.Text.IndexOf("helper", StringComparison.Ordinal);
                editor.Select(helperReference + 3, 0);
                InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor,
                    new KeyEventArgs(Keys.Control | Keys.Space));
                PumpMessages(180);
                ListBox completion = FindControl<ListBox>(form, "completionListBox");
                DdfCompletionItem workspaceHelper = completion.Items.Cast<DdfCompletionItem>()
                    .SingleOrDefault(item => item.DisplayText == "helper");
                Require(workspaceHelper != null && workspaceHelper.TypeName == "int" &&
                        workspaceHelper.Origin.EndsWith("helpers.ddf", StringComparison.OrdinalIgnoreCase),
                    "Il completamento workspace non mostra tipo e file di origine del simbolo esterno.");
                InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Escape));

                InvokeMouseMoveHandler(form, "richTextBoxMainEditor_MouseMove", editor,
                    editor.GetPositionFromCharIndex(helperReference));
                DdfHoverInfo workspaceHover = GetPrivateField<DdfHoverInfo>(form, "activeHoverInfo");
                Require(workspaceHover != null && workspaceHover.Signature == "helper() out int" &&
                        workspaceHover.Origin.EndsWith("helpers.ddf", StringComparison.OrdinalIgnoreCase) &&
                        workspaceHover.DeclarationLine == 2,
                    "L'hover workspace non mostra firma, origine e dichiarazione esterne.");

                editor.Select(helperReference, 0);
                InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
                FindMenuItem(form, "goToDefinitionMenuItem").PerformClick();
                PumpMessages(220);
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(editor.SelectedText == "helper" && editor.Text.Contains("helper()"),
                    "F12 non ha aperto la definizione presente nell'altro documento.");

                FindMenuItem(form, "workspaceSearchMenuItem").PerformClick();
                TextBox searchText = FindControl<TextBox>(form, "workspaceSearchTextBox");
                ComboBox searchKind = FindControl<ComboBox>(form, "workspaceSearchKindComboBox");
                ListView searchResults = FindControl<ListView>(form, "workspaceSearchResultsListView");
                searchKind.SelectedIndex = 0;
                searchText.Text = "helper";
                FindControl<Button>(form, "workspaceSearchButton").PerformClick();
                PumpMessages(260);
                Require(searchResults.Items.Count == 2 &&
                        FindControl<TabControl>(form, "tabControlBottom").SelectedTab.Name == "tabPageWorkspaceSearch",
                    "La ricerca testuale workspace non include riferimento e dichiarazione nei due file.");
                searchResults.Items[0].Selected = true;
                InvokeHandler(form, "workspaceSearchResultsListView_DoubleClick", searchResults, EventArgs.Empty);
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(editor.SelectedText == "helper", "Il risultato di ricerca non è navigabile.");

                editor.AppendText("\n// unsaved-workspace-marker");
                searchText.Text = "unsaved-workspace-marker";
                FindControl<Button>(form, "workspaceSearchButton").PerformClick();
                PumpMessages(260);
                Require(searchResults.Items.Count == 1,
                    "La ricerca workspace non usa il contenuto non salvato del buffer aperto.");

                searchKind.SelectedIndex = 1;
                searchText.Text = "helper";
                FindControl<Button>(form, "workspaceSearchButton").PerformClick();
                PumpMessages(280);
                Require(searchResults.Items.Count == 1 && searchResults.Items[0].SubItems[2].Text == "funzione",
                    "La ricerca simboli workspace non restituisce la dichiarazione tipizzata.");

                string sourceBeforeReplacement = editor.Text;
                FindMenuItem(form, "workspaceReplaceMenuItem").PerformClick();
                TextBox replacementText = FindControl<TextBox>(form, "workspaceReplacementTextBox");
                Button previewButton = FindControl<Button>(form, "workspaceReplacementPreviewButton");
                Button applyButton = FindControl<Button>(form, "workspaceReplacementApplyButton");
                searchText.Text = "helper";
                replacementText.Text = "renamed";
                previewButton.PerformClick();
                PumpMessages(280);
                Require(searchResults.CheckBoxes && searchResults.Items.Count == 2 &&
                        searchResults.Items.Cast<ListViewItem>().All(item => item.Checked) &&
                        searchResults.Items.Cast<ListViewItem>().All(item => item.SubItems[3].Text.Contains("→")),
                    "L'anteprima di sostituzione non mostra le occorrenze selezionabili e il testo aggiornato.");
                foreach (ListViewItem item in searchResults.Items)
                    item.Checked = item.Text.IndexOf("helpers.ddf", StringComparison.OrdinalIgnoreCase) >= 0;
                PumpMessages(80);
                Require(applyButton.Enabled && searchResults.CheckedItems.Count == 1,
                    "La selezione delle singole sostituzioni non aggiorna il comando Applica.");
                applyButton.PerformClick();
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                PumpMessages(280);
                OpenDocumentCollection replacementDocuments = GetPrivateField<OpenDocumentCollection>(form, "openDocuments");
                Require(editor.Text.Contains("renamed()") && editor.Text.Contains("unsaved-workspace-marker"),
                    "La sostituzione selettiva non ha modificato soltanto il buffer scelto.");
                Require(replacementDocuments.FindByPath(helperPath).Session.IsDirty &&
                        replacementDocuments.FindByPath(mainPath).Source.Contains("helper()") &&
                        !replacementDocuments.FindByPath(mainPath).Source.Contains("renamed()"),
                    "La sostituzione ha salvato automaticamente o modificato un'occorrenza esclusa.");
                editor.Select(0, 2);
                Require(editor.SelectionColor == Color.FromArgb(106, 153, 85),
                    "La sostituzione workspace non ha mantenuto verde il commento iniziale.");
                int replacementKeyword = editor.Text.IndexOf("int", StringComparison.Ordinal);
                editor.Select(replacementKeyword, 3);
                Require(editor.SelectionColor == Color.FromArgb(78, 201, 176),
                    "La sostituzione workspace ha esteso il verde del commento al codice successivo.");
                Require(editor.CanUndo, "La sostituzione workspace non è annullabile nel documento modificato.");
                editor.Undo();
                Require(editor.Text == sourceBeforeReplacement,
                    "Un singolo Undo non ha ripristinato lo snapshot precedente alla sostituzione workspace.");

                SetPrivateField(form, "showNavigationDialog", new Func<NavigationForm, DialogResult>(dialog =>
                {
                    Require(dialog.StartPosition == FormStartPosition.CenterScreen && IsLight(dialog.BackColor),
                        "La palette di navigazione non è chiara e centrata sullo schermo.");
                    ListView results = FindControl<ListView>(dialog, "navigationResultsListView");
                    FindControl<TextBox>(dialog, "navigationQueryTextBox").Text = "main.ddf";
                    Require(results.Items.Count == 1, "Vai a file non filtra il workspace.");
                    results.Items[0].Selected = true;
                    return DialogResult.OK;
                }));
                FindMenuItem(form, "goToFileMenuItem").PerformClick();
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(editor.Text.Contains("ret helper()"), "Vai a file non ha aperto il documento scelto.");

                SetPrivateField(form, "showNavigationDialog", new Func<NavigationForm, DialogResult>(dialog =>
                {
                    ListView results = FindControl<ListView>(dialog, "navigationResultsListView");
                    FindControl<TextBox>(dialog, "navigationQueryTextBox").Text = "helper";
                    Require(results.Items.Count >= 1, "Vai a simbolo non trova la dichiarazione workspace.");
                    results.Items.Cast<ListViewItem>().First(item => item.Text == "helper").Selected = true;
                    return DialogResult.OK;
                }));
                FindMenuItem(form, "goToSymbolMenuItem").PerformClick();
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(editor.SelectedText == "helper" && editor.Text.Contains("library declaration"),
                    "Vai a simbolo non raggiunge la dichiarazione in un altro file.");

                PumpMessages(180);
                InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
                SetPrivateField(form, "showNavigationDialog", new Func<NavigationForm, DialogResult>(dialog =>
                {
                    ListView results = FindControl<ListView>(dialog, "navigationResultsListView");
                    Require(results.Items.Count == 2,
                        "Trova riferimenti non distingue dichiarazione e utilizzo tra i file.");
                    FindControl<TextBox>(dialog, "navigationQueryTextBox").Text = "main.ddf";
                    Require(results.Items.Count == 1,
                        "Trova riferimenti non filtra per documento.");
                    return DialogResult.OK;
                }));
                FindMenuItem(form, "findReferencesMenuItem").PerformClick();
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(editor.SelectedText == "helper" && editor.Text.Contains("ret helper()"),
                    "Trova riferimenti non naviga all'utilizzo selezionato.");

                SetPrivateField(form, "showNavigationDialog", new Func<NavigationForm, DialogResult>(dialog =>
                {
                    FindControl<TextBox>(dialog, "navigationQueryTextBox").Text = "1:5";
                    return DialogResult.OK;
                }));
                FindMenuItem(form, "goToLineMenuItem").PerformClick();
                Require(editor.SelectionStart == 4 && editor.SelectionLength == 0,
                    "Vai a riga/colonna non raggiunge la posizione richiesta.");

                editor.AppendText("\n// navigation-last-edit");
                int lastEditPosition = editor.TextLength;
                editor.Select(0, 0);
                InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
                FindMenuItem(form, "goToLastEditMenuItem").PerformClick();
                Require(editor.SelectionStart == lastEditPosition,
                    "Vai all'ultima modifica non ripristina la posizione dell'ultimo edit.");

                FindMenuItem(form, "closeWorkspaceMenuItem").PerformClick();
                Require(workspaceTree.Nodes.Count == 0 && !FindMenuItem(form, "closeWorkspaceMenuItem").Enabled,
                    "Chiudi cartella non ha svuotato lo stato workspace.");
                Console.WriteLine("PASS workspace multi-file, ricerca/sostituzione e navigazione rapida completa");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void AssertTypeChecking(MainForm form, RichTextBox editor, ListBox diagnostics)
        {
            const string invalid =
                "sum(int value) out int { ret value; } " +
                "main() out bool { string text; int number << \"wrong\"; text + number; sum(); " +
                "if(number) { int local; } Missing item; ret number; }";
            editor.Text = invalid;
            PumpMessages(240);
            string[] expectedCodes = { "DDF301", "DDF302", "DDF303", "DDF304", "DDF305", "DDF306" };
            foreach (string code in expectedCodes)
            {
                Require(diagnostics.Items.Cast<object>().Any(item => item.ToString().Contains(code)),
                    "Il pannello non mostra la diagnostica semantica " + code + ".");
            }

            const string valid = "main() out int { int value << 1; ret value; }";
            editor.Text = valid;
            PumpMessages(220);
            int reference = valid.LastIndexOf("value", StringComparison.Ordinal);
            Point point = editor.GetPositionFromCharIndex(reference);
            InvokeMouseMoveHandler(form, "richTextBoxMainEditor_MouseMove", editor, point);
            DdfTypedSpan typed = GetPrivateField<DdfTypedSpan>(form, "hoveredTypedSpan");
            Require(typed != null && typed.Type.Name == "int",
                "L'hover non espone il tipo calcolato del riferimento.");
            Console.WriteLine("PASS diagnostiche DDF3xx e hover con tipo calcolato");
        }

        private static void AssertQuickFixes(MainForm form, RichTextBox editor, ListBox diagnostics)
        {
            const string unterminated = "main() out string { ret \"testo";
            editor.Text = unterminated;
            editor.Select(editor.TextLength - 1, 0);
            PumpMessages(220);
            Require(diagnostics.Items.Cast<DdfDiagnostic>().Any(item => item.Code == "DDF001"),
                "La stringa non terminata non produce DDF001.");

            ContextMenuStrip contextMenu = editor.ContextMenuStrip;
            InvokeHandler(form, "editorContextMenu_Opening", contextMenu,
                new System.ComponentModel.CancelEventArgs());
            ToolStripMenuItem quickFixMenu = contextMenu.Items.OfType<ToolStripMenuItem>()
                .Single(item => item.Name == "contextQuickFixItem");
            ToolStripItem closeStringFix = quickFixMenu.DropDownItems.Cast<ToolStripItem>()
                .FirstOrDefault(item => item.Text.Contains("Chiudi la stringa"));
            Require(quickFixMenu.Enabled && closeStringFix != null,
                "Il menu contestuale non propone la correzione DDF001.");
            ((ToolStripMenuItem)closeStringFix).PerformClick();
            PumpMessages(220);
            Require(editor.Text == unterminated + "\"" &&
                    !diagnostics.Items.Cast<DdfDiagnostic>().Any(item => item.Code == "DDF001"),
                "La correzione contestuale non chiude la stringa.");
            Require(editor.CanUndo, "La correzione rapida non è annullabile.");
            editor.Undo();
            PumpMessages(180);
            Require(editor.Text == unterminated, "Undo non ripristina il sorgente prima della correzione.");

            editor.Select(editor.TextLength - 1, 0);
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.OemPeriod), "Ctrl+. non viene gestito.");
            PumpMessages(220);
            Require(editor.Text == unterminated + "\"", "Ctrl+. non applica la prima correzione disponibile.");

            const string badToken = "main() out int { ret 1; }§";
            editor.Text = badToken;
            editor.Select(editor.TextLength - 1, 0);
            PumpMessages(220);
            FindToolbarButton(form, "toolbarQuickFixButton").PerformClick();
            PumpMessages(220);
            Require(editor.Text == badToken.Substring(0, badToken.Length - 1) && diagnostics.Items.Count == 0,
                "La toolbar non rimuove il carattere non riconosciuto.");

            const string missingSemicolon =
                "main() out int\n{\n    int value << 1\n\n    ret value;\n}";
            editor.Text = missingSemicolon;
            int returnStart = missingSemicolon.IndexOf("ret", StringComparison.Ordinal);
            editor.Select(returnStart, 0);
            PumpMessages(220);
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.OemPeriod),
                "Ctrl+. non gestisce il punto e virgola mancante.");
            PumpMessages(220);
            string expectedSemicolon = missingSemicolon.Insert(missingSemicolon.IndexOf('1') + 1, ";");
            Require(editor.Text == expectedSemicolon,
                "Il punto e virgola è stato inserito sul token sottolineato invece che alla fine dell'istruzione precedente.");

            const string validNestedBlock =
                "main() out int\n{\n    if(true)\n    {\n        ret 1;\n    }\n}";
            string missingInnerBrace = validNestedBlock.Remove(
                validNestedBlock.IndexOf("    }\n", StringComparison.Ordinal),
                "    }\n".Length);
            editor.Text = missingInnerBrace;
            editor.Select(editor.Text.LastIndexOf('}'), 0);
            PumpMessages(220);
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.OemPeriod),
                "Ctrl+. non gestisce la graffa interna mancante.");
            PumpMessages(220);
            Require(editor.Text == validNestedBlock,
                "La graffa mancante non è stata reinserita nel blocco e con l'indentazione corretti.");
            Console.WriteLine("PASS correzioni rapide estensibili da menu, Ctrl+. e toolbar con Undo");
        }

        private static void AssertInlineDiagnostics(MainForm form, RichTextBox editor, ListBox diagnostics)
        {
            const string valid = "main() out int { ret 1; }";
            editor.Text = valid;
            PumpMessages(220);
            int literal = valid.IndexOf('1');
            editor.Select(literal, 1);
            editor.SelectedText = "missing";
            editor.Select(4, 3);
            int selectionStart = editor.SelectionStart;
            int selectionLength = editor.SelectionLength;
            PumpMessages(240);

            DdfDiagnostic diagnostic = diagnostics.Items.Cast<DdfDiagnostic>()
                .FirstOrDefault(item => item.Code == "DDF201");
            Require(diagnostic != null, "La diagnostica inline DDF201 non è stata prodotta.");
            Require(editor.SelectionStart == selectionStart && editor.SelectionLength == selectionLength,
                "La decorazione diagnostica ha modificato la selezione.");
            Require(editor.CanUndo, "La decorazione diagnostica ha cancellato la cronologia Undo.");
            Require((byte)InvokePrivate(form, "getDiagnosticUnderlineTypeAt", diagnostic.Start) == 8,
                "La diagnostica non usa la sottolineatura ondulata nativa.");
            Require((byte)InvokePrivate(form, "getDiagnosticUnderlineColorAt", diagnostic.Start) == 5,
                "La diagnostica di errore non usa il colore rosso nativo.");
            editor.Select(diagnostic.Start, 1);
            Require(editor.SelectionBackColor == editor.BackColor,
                "La diagnostica altera ancora lo sfondo del sorgente.");

            Point point = editor.GetPositionFromCharIndex(diagnostic.Start);
            InvokeMouseMoveHandler(form, "richTextBoxMainEditor_MouseMove", editor, point);
            DdfDiagnostic hovered = GetPrivateField<DdfDiagnostic>(form, "hoveredDiagnostic");
            Require(ReferenceEquals(diagnostic, hovered) && hovered.ToHoverText().Contains(diagnostic.Message),
                "L'hover diagnostico non espone il messaggio completo della palette.");

            editor.Undo();
            PumpMessages(240);
            Require(editor.Text == valid, "Undo non ha ripristinato il testo dopo la diagnostica inline.");
            Require(diagnostics.Items.Count == 0, "La decorazione rimossa ha lasciato diagnostiche nella palette.");
            Require((byte)InvokePrivate(form, "getDiagnosticUnderlineTypeAt", literal) == 0,
                "La sottolineatura diagnostica non è stata rimossa incrementalmente.");
            Console.WriteLine("PASS diagnostiche inline ondulate, hover, selezione e Undo invariati");
        }

        private static TreeNode FindTreeNodeByTag(TreeNodeCollection nodes, string path)
        {
            foreach (TreeNode node in nodes)
            {
                if (string.Equals(node.Tag as string, path, StringComparison.OrdinalIgnoreCase)) return node;
                TreeNode nested = FindTreeNodeByTag(node.Nodes, path);
                if (nested != null) return nested;
            }
            return null;
        }

        private static T FindControl<T>(Control root, string name) where T : Control
        {
            T control = root.Controls.Find(name, true).OfType<T>().SingleOrDefault();
            if (control == null)
            {
                throw new InvalidOperationException("Controllo non trovato: " + name);
            }

            return control;
        }

        private static void AssertProgramExecution(MainForm form, RichTextBox editor)
        {
            editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
            editor.Text = "main() out int\n{\n    int value << 2;\n    value + 3 >> Console;\n    ret value;\n}";
            PumpMessages(240);
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpMessages(500);
            RichTextBox output = FindControl<RichTextBox>(form, "richTextBoxOutput");
            TabControl tabs = FindControl<TabControl>(form, "tabControlBottom");
            Require(output.Text.Contains("5"), "L'output runtime non contiene il valore atteso.");
            Require(output.Text.Contains("Valore restituito: 2"), "La palette non mostra separatamente il valore restituito da main.");
            Require(output.Text.Contains("passi runtime") && !output.Text.Contains(" istruzioni)"),
                "Il contatore tecnico è ancora presentato come valore restituito o istruzioni DDF.");
            Require(output.Text.Contains("Esecuzione completata"), "Il runtime non segnala il completamento.");
            Require(tabs.SelectedTab.Name == "tabPageOutput", "La palette Output non viene selezionata durante Run.");
            Require(FindMenuItem(form, "runProgramMenuItem").Enabled, "Run non viene riabilitato al termine.");

            editor.Text = "main() out int\n{\n    int value << 2;\n    value << value + 1;\n    ret value;\n}";
            PumpMessages(180);
            RichTextBox gutter = FindControl<RichTextBox>(form, "richTextBoxLineNumbers");
            editor.Select(editor.GetFirstCharIndexFromLine(3), 0);
            FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
            Require(gutter.Text.Contains("● 4"), "Il gutter non mostra il breakpoint attivo sulla riga 4.");
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpMessages(400);
            Require(output.Text.Contains("Breakpoint raggiunto: riga 4"), "Il runtime non si sospende sul breakpoint.");
            Require(FindMenuItem(form, "runProgramMenuItem").Enabled &&
                    FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua"),
                "F5 non passa allo stato Continua durante la pausa.");
            Require(editor.SelectedText.Contains("value << value + 1"),
                "La pausa non evidenzia lo statement associato al breakpoint.");
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpMessages(400);
            Require(output.Text.Contains("Valore restituito: 3") && output.Text.Contains("Esecuzione completata"),
                "Continua non completa l'esecuzione sospesa.");
            ClickGutterLine(gutter, 4);
            Require(!gutter.Text.Contains("● 4"), "Il breakpoint non viene rimosso dal gutter.");

            editor.Text = "main() out void { while(true) { } }";
            PumpMessages(180);
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            Require(FindToolbarButton(form, "toolbarStopButton").Enabled, "Stop non viene abilitato durante l'esecuzione.");
            FindToolbarButton(form, "toolbarStopButton").PerformClick();
            PumpMessages(300);
            Require(output.Text.Contains("Esecuzione arrestata"), "Stop non arresta cooperativamente il runtime.");
            Require(FindMenuItem(form, "runProgramMenuItem").Enabled, "Run non viene riabilitato dopo Stop.");

            SetPrivateField(form, "requestRuntimeInput", new Func<string>(() => "41"));
            editor.Text = "main() out int { string text << readLine(); print(text); ret toInt(text) + 1; }";
            PumpMessages(180);
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpMessages(400);
            Require(output.Text.Contains("41") && output.Text.Contains("Valore restituito: 42"),
                "Input e funzioni standard non attraversano correttamente la UI.");
            PumpMessages(300);
            Require(FindMenuItem(form, "runProgramMenuItem").Enabled,
                "Run non viene riabilitato dopo l'uso dell'input standard.");

            editor.Text = "fail() out int\n{\n    int zero << 0;\n    ret 10 / zero;\n}\nmiddle() out int\n{\n    ret fail();\n}\nmain() out int\n{\n    ret middle();\n}";
            PumpMessages(180);
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpMessages(800);
            Require(output.Text.Contains("DDF404") && output.Text.Contains("Stack chiamate DDF:"),
                "L'errore runtime non mostra diagnostica e intestazione dello stack DDF. Output: " + output.Text);
            Require(output.Text.Contains("in fail()") && output.Text.Contains("in middle()") && output.Text.Contains("in main()"),
                "Lo stack runtime non contiene tutte le chiamate DDF. Output: " + output.Text);

            output.SelectionStart = output.Text.IndexOf("DDF404", StringComparison.Ordinal);
            InvokeHandler(form, "richTextBoxOutput_DoubleClick", output, EventArgs.Empty);
            Require(editor.SelectedText.Contains("10 / zero"),
                "Il doppio clic sulla diagnostica runtime non seleziona l'istruzione che ha fallito.");

            output.SelectionStart = output.Text.IndexOf("in fail()", StringComparison.Ordinal);
            InvokeHandler(form, "richTextBoxOutput_DoubleClick", output, EventArgs.Empty);
            Require(editor.SelectedText == "fail",
                "Il doppio clic su un frame runtime non raggiunge il relativo punto di chiamata. Selezione: '" + editor.SelectedText + "'.");
            Console.WriteLine("PASS interprete Run/Stop, breakpoint, pausa/continua, stack navigabile, input standard e palette Output");
        }

        private static void AssertMainToolbar(MainForm form)
        {
            ToolStrip toolbar = FindControl<ToolStrip>(form, "toolStripMain");
            string[] expected =
            {
                "toolbarNewButton", "toolbarOpenButton", "toolbarSaveButton", "toolbarSaveAllButton", "toolbarCloseDocumentButton",
                "toolbarUndoButton", "toolbarRedoButton", "toolbarCutButton",
                "toolbarCopyButton", "toolbarPasteButton", "toolbarCommentButton",
                "toolbarDuplicateLinesButton", "toolbarMoveLinesUpButton", "toolbarMoveLinesDownButton", "toolbarDeleteLinesButton",
                "toolbarExpandSelectionButton", "toolbarShrinkSelectionButton", "toolbarMatchingDelimiterButton",
                "toolbarSelectNextOccurrenceButton", "toolbarSelectAllOccurrencesButton",
                "toolbarFindButton", "toolbarWorkspaceSearchButton", "toolbarWorkspaceReplaceButton",
                "toolbarGoToFileButton", "toolbarGoToSymbolButton", "toolbarFindReferencesButton",
                "toolbarGoToLineButton", "toolbarGoToLastEditButton",
                "toolbarCompletionButton",
                "toolbarQuickFixButton",
                "toolbarFormatButton", "toolbarFoldButton", "toolbarBreakpointButton", "toolbarRunButton", "toolbarStopButton"
            };
            foreach (string name in expected)
            {
                ToolStripButton button = FindToolbarButton(form, name);
                Require(button.DisplayStyle == ToolStripItemDisplayStyle.Text && !string.IsNullOrWhiteSpace(button.Text),
                    "Il comando toolbar non presenta un'icona: " + name);
                Require(!string.IsNullOrWhiteSpace(button.ToolTipText), "Tooltip toolbar mancante: " + name);
                if (button.Enabled)
                    Require(ColorDistance(button.ForeColor, button.BackColor) >= 80,
                        "L'icona toolbar non ha contrasto sufficiente: " + name);
            }
            Require(toolbar.Items.OfType<ToolStripButton>().Count() == expected.Length,
                "Numero inatteso di pulsanti nella toolbar principale.");
            Require(toolbar.Top >= form.MainMenuStrip.Bottom, "La toolbar non è posizionata sotto il menu principale.");
            using (Bitmap icon = form.Icon.ToBitmap())
            {
                bool hasNavy = false, hasOrange = false;
                for (int y = 0; y < icon.Height; y++)
                for (int x = 0; x < icon.Width; x++)
                {
                    Color pixel = icon.GetPixel(x, y);
                    hasNavy |= pixel.A > 0 && pixel.R < 60 && pixel.G < 80 && pixel.B < 110;
                    hasOrange |= pixel.A > 0 && pixel.R > 200 && pixel.G > 90 && pixel.G < 210 && pixel.B < 60;
                }
                Require(hasNavy && hasOrange, "La form non usa la nuova icona incorporata navy/arancio.");
            }
            Console.WriteLine("PASS toolbar a icone con 35 comandi principali");
        }

        private static void AssertDocumentTabsDoNotCoverSource(MainForm form, RichTextBox editor)
        {
            TabControl tabs = FindControl<TabControl>(form, "documentTabs");
            Point tabsBottom = tabs.PointToScreen(new Point(0, tabs.ClientSize.Height));
            Point editorTop = editor.PointToScreen(Point.Empty);
            Require(editorTop.Y >= tabsBottom.Y,
                "La barra delle schede copre la prima parte visibile del documento.");

            const string multiline = "prima riga\nseconda riga\nterza riga";
            editor.Text = string.Empty;
            editor.Select(0, 0);
            Clipboard.SetText(multiline);
            InvokeProcessCmdKey(form, Keys.Control | Keys.V);
            PumpMessages(120);
            Require(editor.Text == multiline && editor.GetPositionFromCharIndex(0).Y >= 0,
                "L'incolla multilinea perde o nasconde le prime righe nella scheda.");
            editor.Clear();
            editor.ClearUndo();
            Console.WriteLine("PASS schede documento senza sovrapposizione e incolla multilinea integrale");
        }

        private static void AssertUnifiedLightTheme(MainForm form, RichTextBox editor, RichTextBox foldedView, RichTextBox gutter)
        {
            string[] lightControls =
            {
                "toolStripMain", "panelOutline", "navigationTabs", "workspaceTabPage", "outlineTabPage",
                "treeViewWorkspace", "treeViewOutline", "panelDiagnostics", "tabPageDiagnostics",
                "tabPageOutput", "tabPageWorkspaceSearch", "workspaceSearchResultsListView",
                "richTextBoxOutput", "listBoxDiagnostics", "buttonOutlinePin",
                "buttonDiagnosticsPin", "splitterDiagnostics", "statusStripMain", "completionListBox"
            };
            foreach (string name in lightControls)
            {
                Control control = form.Controls.Find(name, true).SingleOrDefault();
                Require(control != null && IsLight(control.BackColor),
                    "Il controllo conserva uno sfondo scuro: " + name + ".");
            }
            Require(IsLight(form.BackColor) && IsLight(form.MainMenuStrip.BackColor),
                "La cornice principale o il menu non usa il tema chiaro.");
            Require(!IsLight(editor.BackColor) && !IsLight(foldedView.BackColor) && !IsLight(gutter.BackColor),
                "Editor, folding e gutter non hanno mantenuto il tema scuro dedicato al codice.");
            Require(ColorDistance(FindControl<Button>(form, "buttonOutlinePin").ForeColor,
                                  FindControl<Button>(form, "buttonOutlinePin").BackColor) >= 80,
                "L'icona pin non è visibile sul tema chiaro.");
            Console.WriteLine("PASS tema chiaro uniforme con sola superficie editor scura");
        }

        private static bool IsLight(Color color)
        {
            return color.GetBrightness() >= 0.65F;
        }

        private static int ColorDistance(Color left, Color right)
        {
            return Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B);
        }

        private static ToolStripButton FindToolbarButton(Form form, string name)
        {
            ToolStrip toolbar = FindControl<ToolStrip>(form, "toolStripMain");
            ToolStripButton button = toolbar.Items.OfType<ToolStripButton>().FirstOrDefault(item => item.Name == name);
            if (button == null) throw new InvalidOperationException("Pulsante toolbar non trovato: " + name);
            return button;
        }

        private static ToolStripMenuItem FindMenuItem(Form form, string name)
        {
            ToolStripMenuItem result = FindMenuItem(form.MainMenuStrip.Items, name);
            if (result == null)
            {
                throw new InvalidOperationException("Voce di menu non trovata: " + name);
            }

            return result;
        }

        private static ToolStripMenuItem FindMenuItem(ToolStripItemCollection items, string name)
        {
            foreach (ToolStripItem item in items)
            {
                var menuItem = item as ToolStripMenuItem;
                if (menuItem == null) continue;
                if (string.Equals(menuItem.Name, name, StringComparison.Ordinal)) return menuItem;
                ToolStripMenuItem nested = FindMenuItem(menuItem.DropDownItems, name);
                if (nested != null) return nested;
            }

            return null;
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException("Campo privato non trovato: " + fieldName);
            }

            return field.GetValue(target) as T;
        }

        private static void InvokeMouseMoveHandler(Form form, string methodName, Control sender, Point location)
        {
            MethodInfo method = form.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Handler non trovato: " + methodName);
            method.Invoke(form, new object[]
            {
                sender,
                new MouseEventArgs(MouseButtons.None, 0, location.X, location.Y, 0)
            });
        }

        private static bool InvokeProcessCmdKey(MainForm form, Keys keys)
        {
            MethodInfo method = typeof(MainForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("ProcessCmdKey non trovato.");
            var message = Message.Create(form.Handle, 0, IntPtr.Zero, IntPtr.Zero);
            object[] arguments = { message, keys };
            return (bool)method.Invoke(form, arguments);
        }

        private static object InvokePrivate(MainForm form, string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(MainForm).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Metodo non trovato: " + methodName);
            return method.Invoke(form, arguments);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException("Campo privato non trovato: " + fieldName);
            }

            field.SetValue(target, value);
        }

        private static void ClickGutterLine(RichTextBox gutter, int oneBasedLine)
        {
            int character = gutter.GetFirstCharIndexFromLine(oneBasedLine - 1);
            if (character < 0) throw new InvalidOperationException("Riga gutter non visibile: " + oneBasedLine);
            Point point = gutter.GetPositionFromCharIndex(character);
            int packedPoint = ((point.Y + 2) << 16) | Math.Max(1, point.X + 2);
            SendMessage(gutter.Handle, WmLeftButtonDown, new IntPtr(1), new IntPtr(packedPoint));
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        private static void InvokeMouseHandler(MainForm form, string methodName, RichTextBox editor, MouseButtons button)
        {
            InvokeHandler(
                form,
                methodName,
                editor,
                new MouseEventArgs(button, 1, 0, 0, 0));
        }

        private static void InvokeHandler(MainForm form, string methodName, object sender, EventArgs eventArgs)
        {
            MethodInfo method = typeof(MainForm).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("Handler non trovato: " + methodName);
            }

            method.Invoke(form, new[] { sender, eventArgs });
        }

        private static void PumpMessages(int milliseconds)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < milliseconds)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
