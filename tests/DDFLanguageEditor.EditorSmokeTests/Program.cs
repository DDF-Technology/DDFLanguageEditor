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

                Console.WriteLine("PASS smoke dinamico WinForms: 12 scenari editor e tutti i 19 comandi di menu completati senza eccezioni.");
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

                AssertGutterAndAutoHidePalettes(form, editor, lineNumbers, diagnosticsPanel);
                AssertEditorPalette(editor);
                SetSource(editor);
                AssertSelectionStableDuringHighlight(editor);
                AssertMouseGesturePreservesNativeSelection(form, editor);
                AssertColoredFoldFromFunctionHeader(form, editor, foldedView, lineNumbers);
                AssertMultipleIndependentFolds(form, editor, foldedView);
                AssertWholeLibraryCutIsClean(editor, diagnostics);
                AssertUndoRestoresLibrary(editor);
                AssertPartialLibraryCutIsRecoverable(editor, diagnostics);
                AssertRapidTransientEditsAreRecoverable(editor);
                AssertContextualCompletion(form, editor);
                AssertDocumentFormatting(form, editor);
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

            using (var form = ShowSmokeForm(visible))
            {
                var shortcuts = new Dictionary<string, Keys>
                {
                    { "newMenuItem", Keys.Control | Keys.N },
                    { "openMenuItem", Keys.Control | Keys.O },
                    { "saveMenuItem", Keys.Control | Keys.S },
                    { "saveAsMenuItem", Keys.Control | Keys.Shift | Keys.S },
                    { "undoMenuItem", Keys.Control | Keys.Z },
                    { "redoMenuItem", Keys.Control | Keys.Y },
                    { "cutMenuItem", Keys.Control | Keys.X },
                    { "copyMenuItem", Keys.Control | Keys.C },
                    { "pasteMenuItem", Keys.Control | Keys.V },
                    { "selectAllMenuItem", Keys.Control | Keys.A },
                    { "findMenuItem", Keys.Control | Keys.F },
                    { "replaceMenuItem", Keys.Control | Keys.H },
                    { "completionMenuItem", Keys.Control | Keys.Space },
                    { "formatDocumentMenuItem", Keys.Control | Keys.Shift | Keys.F },
                    { "toggleFoldMenuItem", Keys.Control | Keys.M },
                    { "expandAllFoldsMenuItem", Keys.Control | Keys.Shift | Keys.M }
                };

                string[] commands =
                {
                    "newMenuItem", "openMenuItem", "saveMenuItem", "saveAsMenuItem",
                    "recentMenuItem", "exitMenuItem", "undoMenuItem", "redoMenuItem",
                    "cutMenuItem", "copyMenuItem", "pasteMenuItem", "selectAllMenuItem",
                    "findMenuItem", "replaceMenuItem", "completionMenuItem", "formatDocumentMenuItem",
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

            Console.WriteLine("PASS struttura menu e 16 scorciatoie");
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
                Require(FindControl<Label>(about, "aboutVersionLabel").Text.Contains("0.5.4") &&
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
                TextBox findText = FindControl<TextBox>(findForm, "findTextBox");
                Require(findText.Text == "alpha", "Trova non ha precompilato il testo selezionato.");
                FindControl<Button>(findForm, "findNextButton").PerformClick();
                Require(editor.SelectionStart == 11 && editor.SelectedText == "alpha",
                    "Trova dopo non ha selezionato la corrispondenza successiva.");

                FindMenuItem(form, "replaceMenuItem").PerformClick();
                PumpMessages(60);
                Require(findForm.Text == "Trova e sostituisci" && FindControl<TextBox>(findForm, "replaceTextBox").Visible,
                    "Sostituisci non ha attivato i controlli di sostituzione.");
                findText.Text = "beta";
                FindControl<TextBox>(findForm, "replaceTextBox").Text = "delta";
                editor.Select(6, 4);
                FindControl<Button>(findForm, "replaceButton").PerformClick();
                Require(editor.Text == "alpha delta alpha", "Sostituisci non ha modificato la corrispondenza corrente.");

                findText.Text = "alpha";
                FindControl<TextBox>(findForm, "replaceTextBox").Text = "omega";
                FindControl<Button>(findForm, "replaceAllButton").PerformClick();
                Require(editor.Text == "omega delta omega", "Sostituisci tutto non ha modificato tutte le corrispondenze.");
                FindControl<Button>(findForm, "closeButton").PerformClick();
                PumpMessages(40);
                Require(findForm.IsDisposed, "Chiudi non ha terminato la finestra Trova/Sostituisci.");

                editor.Text = string.Empty;
                editor.Select(0, 0);
                FindMenuItem(form, "completionMenuItem").PerformClick();
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
                    Require(editor.Text == Source && form.Text.Contains("aperto.ddf"), "Apri non ha caricato il file temporaneo.");
                    editor.AppendText("\n// saved");
                    FindMenuItem(form, "saveMenuItem").PerformClick();
                    Require(File.ReadAllText(openPath).EndsWith("// saved", StringComparison.Ordinal),
                        "Salva non ha scritto il documento corrente.");

                    editor.AppendText("\n// save as");
                    FindMenuItem(form, "saveAsMenuItem").PerformClick();
                    Require(File.Exists(saveAsPath) && File.ReadAllText(saveAsPath).EndsWith("// save as", StringComparison.Ordinal),
                        "Salva con nome non ha scritto il percorso scelto.");

                    FindMenuItem(form, "newMenuItem").PerformClick();
                    Require(editor.TextLength == 0 && form.Text.Contains("Senza titolo.ddf"), "Nuovo non ha creato un documento vuoto.");
                    ToolStripMenuItem recent = FindMenuItem(form, "recentMenuItem").DropDownItems
                        .OfType<ToolStripMenuItem>()
                        .FirstOrDefault(item => string.Equals(item.Tag as string, saveAsPath, StringComparison.OrdinalIgnoreCase));
                    Require(recent != null, "File recenti non contiene il documento appena salvato.");
                    recent.PerformClick();
                    Require(editor.Text.EndsWith("// save as", StringComparison.Ordinal) && form.Text.Contains("salvato-con-nome.ddf"),
                        "File recenti non ha riaperto il documento selezionato.");
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

            Console.WriteLine("PASS menu File: Nuovo, Apri, Salva, Salva con nome, File recenti ed Esci");
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
            Button outlinePin = FindControl<Button>(form, "buttonOutlinePin");
            Button diagnosticsPin = FindControl<Button>(form, "buttonDiagnosticsPin");
            Require(outlinePanel.Width <= 190,
                "L'Outline occupa ancora troppo spazio all'avvio: " + outlinePanel.Width + " px.");

            outlinePin.PerformClick();
            PumpMessages(20);
            Require(outlinePanel.Width == 28 && outlinePin.Text == "\uE76C",
                "L'Outline non entra nello stato auto-hide compatto.");
            InvokeHandler(form, "panelOutline_MouseEnter", outlinePanel, EventArgs.Empty);
            Require(outlinePanel.Width >= 180,
                "L'Outline non si riapre durante l'auto-hide.");
            outlinePin.PerformClick();
            Require(outlinePin.Text == "\uE718" && outlinePanel.Width >= 180,
                "L'Outline non torna nello stato pinnato.");

            diagnosticsPin.PerformClick();
            PumpMessages(20);
            Require(diagnosticsPanel.Height == 26 && diagnosticsPin.Text == "\uE70E",
                "La Diagnostica non entra nello stato auto-hide compatto.");
            InvokeHandler(form, "panelDiagnostics_MouseEnter", diagnosticsPanel, EventArgs.Empty);
            Require(diagnosticsPanel.Height >= 116,
                "La Diagnostica non si riapre durante l'auto-hide.");
            diagnosticsPin.PerformClick();
            Require(diagnosticsPin.Text == "\uE718" && diagnosticsPanel.Height >= 116,
                "La Diagnostica non torna nello stato pinnato.");
            Console.WriteLine("PASS gutter non selezionabile e palette pinnabili con auto-hide");
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

        private static void AssertRapidTransientEditsAreRecoverable(RichTextBox editor)
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
            Console.WriteLine("PASS 80 modifiche transitorie rapide");
        }

        private static void AssertContextualCompletion(MainForm form, RichTextBox editor)
        {
            editor.Text = "wh";
            editor.Select(2, 0);
            editor.Focus();
            PumpMessages(220);
            ListBox completion = FindControl<ListBox>(form, "completionListBox");
            Require(completion.Visible, "Il completamento automatico non è apparso dopo il prefisso.");
            Require(completion.Items.Cast<DdfCompletionItem>().Any(item => item.DisplayText == "while"),
                "Il completamento non propone la parola chiave while.");

            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Tab));
            PumpMessages(120);
            Require(editor.Text == "while" && editor.SelectionStart == "while".Length,
                "Tab non ha applicato il completamento selezionato.");
            Require(editor.CanUndo, "Il completamento non ha creato un'operazione Undo.");
            editor.Undo();
            PumpMessages(100);
            Require(editor.Text == "wh", "Undo non ha ripristinato il prefisso precedente al completamento.");

            editor.Text = string.Empty;
            editor.Select(0, 0);
            InvokeHandler(
                form,
                "richTextBoxMainEditor_KeyDown",
                editor,
                new KeyEventArgs(Keys.Control | Keys.Space));
            Require(completion.Visible && completion.Items.Count > 0,
                "Ctrl+Spazio non ha mostrato l'elenco completo.");
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Escape));
            Require(!completion.Visible, "Esc non ha chiuso il completamento.");
            Console.WriteLine("PASS completamento contestuale con Tab, Undo, Ctrl+Spazio ed Esc");
        }

        private static void AssertDocumentFormatting(MainForm form, RichTextBox editor)
        {
            const string source = "main()out int{int value<<1+2;ret value;}";
            const string expected =
                "main() out int\n" +
                "{\n" +
                "    int value << 1 + 2;\n" +
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

            format.PerformClick();
            PumpMessages(80);
            Require(editor.Text == expected, "Una seconda formattazione ha modificato il risultato.");
            Require(editor.CanUndo, "La formattazione non è annullabile.");
            editor.Undo();
            PumpMessages(160);
            Require(editor.Text == source, "Undo non ha ripristinato il documento prima della formattazione.");
            Console.WriteLine("PASS formattazione idempotente con cursore preservato e singolo Undo");
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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException("Campo privato non trovato: " + fieldName);
            }

            field.SetValue(target, value);
        }

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
