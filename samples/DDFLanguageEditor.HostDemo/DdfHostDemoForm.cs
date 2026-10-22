using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using DDF___Program_Language_Editor;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.HostDemo
{
    public sealed class DdfHostDemoForm : Form
    {
        private readonly Label leftStatus;
        private readonly Label rightStatus;
        private readonly ToolTip hostToolTip;
        private int leftHostCounter = 10;
        private int rightHostCounter = 100;

        public DdfHostDemoForm()
        {
            Name = "ddfHostDemoForm";
            Text = "DDF Editor SDK — Host Demo";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 620);
            Size = new Size(1260, 760);
            BackColor = Color.FromArgb(245, 246, 248);
            Font = new Font("Segoe UI", 9F);

            LeftEditor = new DdfEditorView { Name = "leftEmbeddedEditor" };
            RightEditor = new DdfEditorView { Name = "rightEmbeddedEditor" };
            LeftPersistence = new DdfInMemoryEditorPersistenceProvider();
            RightPersistence = new DdfInMemoryEditorPersistenceProvider();
            LeftProjectContainer = new DdfInMemoryEditorProjectContainer();
            RightProjectContainer = new DdfInMemoryEditorProjectContainer();
            leftStatus = new Label { Name = "leftHostStatus", AutoEllipsis = true };
            rightStatus = new Label { Name = "rightHostStatus", AutoEllipsis = true };
            LeftOutput = CreateOutput("leftHostOutput");
            RightOutput = CreateOutput("rightHostOutput");
            hostToolTip = new ToolTip();
            ConfigureLeftProfile();
            ConfigureRightProfile();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = BackColor,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(root);

            var title = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 32, 32),
                Margin = new Padding(4, 4, 4, 12),
                Text = "Demo preliminare SDK · due editor isolati · Run e Debug usano esclusivamente i buffer virtuali dell'host"
            };
            root.Controls.Add(title, 0, 0);
            root.SetColumnSpan(title, 2);

            root.Controls.Add(CreateProfileCard("Profilo tecnico", LeftEditor, LeftOutput, leftStatus,
                SaveLeftProfile, RestoreLeftProfile, SaveLeftProject, OpenLeftProject), 0, 1);
            root.Controls.Add(CreateProfileCard("Profilo didattico", RightEditor, RightOutput, rightStatus,
                SaveRightProfile, RestoreRightProfile, SaveRightProject, OpenRightProject), 1, 1);
            UpdateStatus(LeftEditor, leftStatus, "Pronto");
            UpdateStatus(RightEditor, rightStatus, "Pronto");
        }

        public DdfEditorView LeftEditor { get; }
        public DdfEditorView RightEditor { get; }
        public OpenDocumentCollection LeftDocuments { get; private set; }
        public OpenDocumentCollection RightDocuments { get; private set; }
        public DdfInMemoryEditorPersistenceProvider LeftPersistence { get; }
        public DdfInMemoryEditorPersistenceProvider RightPersistence { get; }
        public DdfInMemoryEditorProjectContainer LeftProjectContainer { get; }
        public DdfInMemoryEditorProjectContainer RightProjectContainer { get; }
        public RichTextBox LeftOutput { get; }
        public RichTextBox RightOutput { get; }

        public void SaveLeftProfile() => SaveProfile(LeftEditor, leftStatus);
        public void SaveRightProfile() => SaveProfile(RightEditor, rightStatus);
        public void SaveLeftProject() => SaveProject(LeftEditor, "technical-demo",
            "Progetto tecnico", leftStatus);
        public void SaveRightProject() => SaveProject(RightEditor, "educational-demo",
            "Progetto didattico", rightStatus);

        public void OpenLeftProject()
        {
            LeftDocuments = OpenProject(LeftEditor, LeftProjectContainer, leftStatus);
        }

        public void OpenRightProject()
        {
            RightDocuments = OpenProject(RightEditor, RightProjectContainer, rightStatus);
        }

        public void RestoreLeftProfile()
        {
            LeftDocuments = RestoreProfile(LeftEditor, LeftPersistence, leftStatus);
        }

        public void RestoreRightProfile()
        {
            RightDocuments = RestoreProfile(RightEditor, RightPersistence, rightStatus);
        }

        private void ConfigureLeftProfile()
        {
            ConfigureHostValues(LeftEditor, () => leftHostCounter,
                value => leftHostCounter = Convert.ToInt32(value),
                "Profilo tecnico", 22);
            LeftDocuments = new OpenDocumentCollection();
            LeftEditor.AttachDocumentService(LeftDocuments);
            LeftEditor.AttachPersistenceProvider(LeftPersistence);
            LeftEditor.AttachProjectContainer(LeftProjectContainer);
            LeftEditor.HostConfiguration.ApplyOptions(new DdfEditorHostOptions(
                "Consolas", 10F, 100, EditorTheme.Dark, false, false, 4));
            LeftEditor.HostConfiguration.SetShortcut(
                DdfEditorCommandIds.SaveHostState, Keys.Control | Keys.Shift | Keys.S);
            LeftDocuments.CreateVirtual("main.ddf",
                "main() out int\n{\n    int result << weightedScore(helper(20), 0, 1);\n    bool accepted << isWithinRange(result, 0, 200);\n\n    if(accepted)\n    {\n        hostCounter << applyHostScore(hostCounter + result);\n    }\n\n    hostContext.label >> Console;\n    result >> Console;\n    ret result;\n}");
            LeftDocuments.CreateVirtual("helpers.ddf",
                "helper(int value) out int\n{\n    ret value + 22;\n}\n\n" +
                "calculateHost(int value, int factor) out int\n{\n    ret value * factor;\n}");
            LeftDocuments.ActivateDocument(LeftDocuments.OpenDocuments[0].Id);
            WireEvents(LeftEditor, true);
        }

        private void ConfigureRightProfile()
        {
            ConfigureHostValues(RightEditor, () => rightHostCounter,
                value => rightHostCounter = Convert.ToInt32(value),
                "Profilo didattico", 5);
            RightDocuments = new OpenDocumentCollection();
            RightEditor.AttachDocumentService(RightDocuments);
            RightEditor.AttachPersistenceProvider(RightPersistence);
            RightEditor.AttachProjectContainer(RightProjectContainer);
            RightEditor.HostConfiguration.ApplyOptions(new DdfEditorHostOptions(
                "Consolas", 12F, 110, EditorTheme.Light, true, false, 2));
            RightEditor.ExperienceState.SetEducationalMode(true);
            RightDocuments.CreateVirtual("esercizio.ddf",
                "main() out int\n{\n  int numero << weightedScore(hostContext.baseValue, 0, 1);\n  bool valido << isWithinRange(numero, 0, 200);\n\n  if(valido)\n  {\n    hostCounter << applyHostScore(hostCounter + numero);\n    numero >> Console;\n    ret numero;\n  }\n\n  ret 0;\n}\n\n" +
                "calculateHost(int value, int factor) out int\n{\n  ret value * factor;\n}");
            WireEvents(RightEditor, false);
        }

        private static void ConfigureHostValues(DdfEditorView editor,
            Func<object> readCounter, Action<object> writeCounter,
            string label, int baseValue)
        {
            var catalog = (DdfEditorHostCatalog)editor.HostCatalog;
            catalog.RegisterPermission(new DdfHostPermissionDefinition(
                "algorithms.use", "Uso algoritmi applicativi",
                "Abilita le funzioni matematiche, logiche e mutanti della demo."));
            catalog.RegisterType(new DdfHostTypeDefinition("HostContext", new[]
            {
                new DdfHostFieldDefinition("label", "string",
                    "Etichetta fornita dall'applicazione host."),
                new DdfHostFieldDefinition("baseValue", "int",
                    "Valore iniziale specifico dell'istanza incorporata.")
            }, "Oggetto personalizzato e tipizzato dell'applicazione host."));
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "hostCounter", "int", readCounter,
                "Variabile host modificabile, isolata per istanza.",
                writeValue: writeCounter));
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "hostContext", "HostContext", () =>
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["label"] = label,
                        ["baseValue"] = baseValue
                    }, "Oggetto personalizzato in sola lettura."));
            catalog.RegisterFunctionLibrary(new DdfHostFunctionLibraryDefinition(
                "demo.algorithms", "Algoritmi host", new[]
                {
                    new DdfHostFunctionDefinition("weightedScore", "int", new[]
                    {
                        new DdfHostParameterDefinition("current", "int"),
                        new DdfHostParameterDefinition("sample", "int"),
                        new DdfHostParameterDefinition("weight", "int")
                    }, arguments => Convert.ToInt32(arguments[0]) +
                        Convert.ToInt32(arguments[1]) * Convert.ToInt32(arguments[2]),
                        "Combina un valore corrente con un campione pesato."),
                    new DdfHostFunctionDefinition("isWithinRange", "bool", new[]
                    {
                        new DdfHostParameterDefinition("value", "int"),
                        new DdfHostParameterDefinition("minimum", "int"),
                        new DdfHostParameterDefinition("maximum", "int")
                    }, arguments => Convert.ToInt32(arguments[0]) >=
                            Convert.ToInt32(arguments[1]) &&
                        Convert.ToInt32(arguments[0]) <= Convert.ToInt32(arguments[2]),
                        "Verifica una condizione logica di intervallo inclusivo."),
                    new DdfHostFunctionDefinition("applyHostScore", "int", new[]
                    {
                        new DdfHostParameterDefinition("value", "int")
                    }, arguments =>
                    {
                        int result = Math.Max(0, Math.Min(1000,
                            Convert.ToInt32(arguments[0])));
                        writeCounter(result);
                        return result;
                    }, "Normalizza e aggiorna il contatore dell'applicazione host.",
                    effect: DdfHostFunctionEffect.HostMutation,
                    simulate: arguments => Math.Max(0, Math.Min(1000,
                        Convert.ToInt32(arguments[0]))))
                }, "Funzioni tipizzate condivise da IntelliSense, Build, Run e Debug.",
                new[] { "algorithms.use" }));
            catalog.SetPermission("algorithms.use", true);
        }

        private Control CreateProfileCard(string title, DdfEditorView editor,
            RichTextBox output, Label status,
            Action save, Action restore, Action saveProject, Action openProject)
        {
            var card = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(4),
                Padding = new Padding(8)
            };
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var toolbar = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            toolbar.Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(3, 8, 12, 0),
                Text = title
            });
            toolbar.Controls.Add(CreateButton("Analizza", () =>
            {
                DdfEditorAnalysisSnapshot snapshot = editor.AnalyzeDocument();
                UpdateStatus(editor, status, "Analisi: " + snapshot.Diagnostics.Count + " diagnostiche");
            }, "Analizza il documento attivo senza eseguirlo."));
            toolbar.Controls.Add(CreateButton("Convalida", () =>
                BuildProfile(editor, output, status, false),
                "Convalida tutti i buffer del progetto host senza eseguirli né scrivere file."));
            toolbar.Controls.Add(CreateButton("Compila", () =>
                BuildProfile(editor, output, status, true),
                "Compila in memoria tutti i buffer e produce uno snapshot dell'artefatto."));
            toolbar.Controls.Add(CreateButton("Vai alla definizione", () =>
            {
                UpdateStatus(editor, status,
                    editor.TryGoToDefinition() ? "Definizione raggiunta" : "Definizione non trovata");
            }, "Raggiunge la dichiarazione del simbolo sotto il cursore, anche in un altro buffer."));
            toolbar.Controls.Add(CreateButton("▶ Esegui", () => RunProfile(editor, output, status),
                "Compila ed esegue una fotografia dei documenti virtuali di questa istanza."));
            toolbar.Controls.Add(CreateButton("ƒ Procedura", () =>
                InvokeProcedure(editor, output, status, false),
                "Richiama manualmente una procedura DDF con argomenti nominati e risultato tipizzato."));
            toolbar.Controls.Add(CreateButton("⚡ Evento", () =>
                InvokeProcedure(editor, output, status, true),
                "Simula un evento esplicito dell'host che richiama la stessa procedura DDF."));
            toolbar.Controls.Add(CreateButton("■ Arresta", () =>
                StopProfile(editor.ExecutionService, editor, status),
                "Richiede l'arresto della sola sessione appartenente a questa istanza."));
            toolbar.Controls.Add(CreateButton("◆ Debug qui", () => DebugProfile(editor, output, status),
                "Avvia il debug impostando un breakpoint sulla riga corrente del documento attivo."));
            toolbar.Controls.Add(CreateButton("▶ Continua", () => editor.DebugService.Continue(),
                "Riprende la sessione quando il debugger è in pausa."));
            toolbar.Controls.Add(CreateButton("↓ Dentro", () => editor.DebugService.StepInto(),
                "Esegue la prossima istruzione entrando nelle funzioni chiamate (Step Into)."));
            toolbar.Controls.Add(CreateButton("→ Oltre", () => editor.DebugService.StepOver(),
                "Esegue la prossima istruzione senza entrare nelle funzioni chiamate (Step Over)."));
            toolbar.Controls.Add(CreateButton("↑ Fuori", () => editor.DebugService.StepOut(),
                "Continua fino al ritorno nella funzione chiamante (Step Out)."));
            toolbar.Controls.Add(CreateButton("■ Stop debug", () =>
                StopProfile(editor.DebugService, editor, status),
                "Arresta la sessione di debug appartenente a questa istanza."));
            var watchExpression = new TextBox
            {
                Name = editor.Name + "WatchExpression",
                AccessibleName = "Espressione Watch",
                Margin = new Padding(10, 5, 3, 3),
                Text = "result",
                Width = 110
            };
            toolbar.Controls.Add(watchExpression);
            toolbar.Controls.Add(CreateButton("Watch", () => ShowWatchResult(output,
                    editor.DebugService.EvaluateWatch(watchExpression.Text)),
                "Valuta un'espressione pura sullo snapshot immutabile della pausa corrente."));
            toolbar.Controls.Add(CreateButton("Salva profilo", save,
                "Salva buffer e preferenze nel provider in memoria dell'host."));
            toolbar.Controls.Add(CreateButton("Ripristina profilo", restore,
                "Ricrea documenti e preferenze dall'ultimo snapshot salvato."));
            toolbar.Controls.Add(CreateButton("Salva progetto", saveProject,
                "Consegna il progetto completo al contenitore scelto dall'host."));
            toolbar.Controls.Add(CreateButton("Apri progetto", openProject,
                "Ricrea il progetto dal contenitore dell'host senza accedere direttamente al file system."));
            var educational = new CheckBox
            {
                AutoSize = true,
                Checked = editor.ExperienceState.EducationalModeEnabled,
                Margin = new Padding(10, 7, 3, 0),
                Text = "Modalità didattica"
            };
            educational.CheckedChanged += (sender, args) =>
                editor.ExperienceState.SetEducationalMode(educational.Checked);
            editor.ExperienceState.EducationalModeChanged += (sender, args) =>
                educational.Checked = args.Enabled;
            toolbar.Controls.Add(educational);

            status.AutoSize = false;
            status.Dock = DockStyle.Fill;
            status.Height = 28;
            status.Padding = new Padding(4, 6, 4, 2);
            status.ForeColor = Color.FromArgb(75, 75, 75);
            card.Controls.Add(toolbar, 0, 0);
            card.Controls.Add(editor, 0, 1);
            card.Controls.Add(output, 0, 2);
            card.Controls.Add(status, 0, 3);
            return card;
        }

        private Button CreateButton(string text, Action action, string description)
        {
            var button = new Button
            {
                AutoSize = true,
                BackColor = Color.FromArgb(248, 248, 248),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(3),
                Text = text,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(190, 190, 190);
            button.Click += (sender, args) => action();
            button.AccessibleDescription = description;
            hostToolTip.SetToolTip(button, description);
            return button;
        }

        private static RichTextBox CreateOutput(string name)
        {
            return new RichTextBox
            {
                Name = name,
                AccessibleName = "Output esecuzione host",
                BackColor = Color.FromArgb(250, 250, 250),
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                WordWrap = false
            };
        }

        private void WireEvents(DdfEditorView editor, bool left)
        {
            Label status = left ? leftStatus : rightStatus;
            editor.SourceTextChanged += (sender, args) => UpdateStatus(editor, status, "Buffer modificato");
            editor.AnalysisCompleted += (sender, args) => UpdateStatus(editor, status,
                "Analisi completata: " + args.Snapshot.Diagnostics.Count + " diagnostiche");
            editor.DefinitionNavigated += (sender, args) => UpdateStatus(editor, status,
                "Definizione: " + args.Target.Name + " · " +
                (args.Target.Document?.DisplayName ?? "buffer locale"));
            editor.ExperienceState.EducationalModeChanged += (sender, args) => UpdateStatus(editor, status,
                args.Enabled ? "Modalità didattica attiva" : "Modalità didattica disattivata");
            RichTextBox output = left ? LeftOutput : RightOutput;
            editor.ExecutionOutputReceived += (sender, args) => PostToUi(() =>
                AppendOutput(output, args.Text));
            editor.ExecutionStateChanged += (sender, args) => PostToUi(() =>
                UpdateStatus(editor, status, args.State == DdfEditorExecutionState.Running
                    ? "Esecuzione in corso" : args.State == DdfEditorExecutionState.Stopping
                        ? "Arresto richiesto" : "Esecuzione inattiva"));
            editor.ExecutionCompleted += (sender, args) => PostToUi(() =>
                ShowCompletion(output, status, editor, args.Completion));
            editor.DebugOutputReceived += (sender, args) => PostToUi(() =>
                AppendOutput(output, args.Text));
            editor.DebugPaused += (sender, args) => PostToUi(() =>
                ShowDebugSnapshot(editor, output, status, args.Snapshot));
            editor.DebugStateChanged += (sender, args) => PostToUi(() =>
            {
                if (args.State == DdfEditorDebugState.Running)
                    UpdateStatus(editor, status, "Debug in esecuzione");
                else if (args.State == DdfEditorDebugState.Stopping)
                    UpdateStatus(editor, status, "Arresto debug richiesto");
            });
            editor.DebugCompleted += (sender, args) => PostToUi(() =>
                ShowCompletion(output, status, editor, args.Completion));
        }

        private async void RunProfile(DdfEditorView editor, RichTextBox output,
            Label status)
        {
            if (editor.ExecutionService.IsRunning) return;
            output.Clear();
            UpdateStatus(editor, status, "Avvio esecuzione...");
            try
            {
                await editor.ExecutionService.RunAsync();
            }
            catch (Exception exception)
            {
                AppendOutput(output, "Errore host: " + exception.Message);
            }
        }

        private async void DebugProfile(DdfEditorView editor, RichTextBox output,
            Label status)
        {
            if (editor.DebugService.IsActive) return;
            IDdfEditorDocument document = editor.DocumentService?.ActiveDocumentInfo;
            if (document == null) return;
            int line = 1;
            int limit = Math.Min(editor.SelectionStart, editor.SourceText.Length);
            for (int index = 0; index < limit; index++)
                if (editor.SourceText[index] == '\n') line++;
            editor.DebugService.SetBreakpoints(new[]
                { new DdfEditorBreakpoint(document.Id, line) });
            output.Clear();
            UpdateStatus(editor, status, "Avvio debug · breakpoint riga " + line);
            try
            {
                await editor.DebugService.RunAsync();
            }
            catch (Exception exception)
            {
                AppendOutput(output, "Errore host: " + exception.Message);
            }
        }

        private async void InvokeProcedure(DdfEditorView editor, RichTextBox output,
            Label status, bool fromEvent)
        {
            if (editor.ExecutionService.IsRunning || editor.DebugService.IsActive) return;
            output.Clear();
            UpdateStatus(editor, status, fromEvent
                ? "Evento host demo.rule.requested..."
                : "Invocazione manuale calculateHost...");
            try
            {
                await editor.ExecutionService.InvokeAsync(
                    new DdfEditorProcedureRequest("calculateHost",
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["value"] = 6,
                            ["factor"] = 7
                        }, fromEvent ? DdfEditorProcedureTrigger.HostEvent :
                            DdfEditorProcedureTrigger.Manual,
                        fromEvent ? "demo.rule.requested" : null));
            }
            catch (Exception exception)
            {
                AppendOutput(output, "Errore host: " + exception.Message);
            }
        }

        private static void ShowDebugSnapshot(DdfEditorView editor,
            RichTextBox output, Label status, DdfEditorDebugSnapshot snapshot)
        {
            DdfEditorDebugLocation location = snapshot.Location;
            if (!string.IsNullOrEmpty(location.DocumentId))
                editor.DocumentService.ActivateDocument(location.DocumentId);
            editor.SetSelection(location.Start, location.Length);
            editor.FocusEditor();
            AppendOutput(output, "Pausa: " + location.DisplayName + " · riga " +
                location.Line + ", colonna " + location.Column);
            AppendOutput(output, "Call Stack:");
            foreach (DdfEditorDebugStackFrameSnapshot frame in snapshot.CallStack)
                AppendOutput(output, "  #" + frame.Index + " " + frame.FunctionName +
                    "() · " + frame.Location.DisplayName + ":" + frame.Location.Line);
            AppendOutput(output, "Variabili visibili:");
            if (snapshot.Variables.Count == 0) AppendOutput(output, "  (nessuna)");
            foreach (DdfEditorDebugVariableSnapshot variable in snapshot.Variables)
                AppendOutput(output, "  " + variable.Name + " : " + variable.TypeName +
                    " = " + variable.DisplayValue);
            UpdateStatus(editor, status, "Debug in pausa · riga " + location.Line +
                ", colonna " + location.Column);
        }

        private static void ShowWatchResult(RichTextBox output,
            DdfEditorWatchResult result)
        {
            AppendOutput(output, result.Available
                ? "Watch " + result.Expression + " = " + result.Value.DisplayValue +
                  " : " + result.Value.TypeName + " (frame #" + result.FrameIndex + ")"
                : "Watch " + result.Expression + " — " + result.Message);
        }

        private static void ShowBuildSnapshot(DdfEditorPreparationSnapshot snapshot,
            RichTextBox output, DdfEditorView editor, Label status)
        {
            output.Clear();
            foreach (DdfBuildIssue issue in snapshot.Issues)
                AppendOutput(output, issue.ToString());
            if (snapshot.Succeeded)
            {
                AppendOutput(output, snapshot.Mode == DdfEditorPreparationMode.Validate
                    ? "Convalida completata." : "Preparazione in memoria completata.");
                if (snapshot.PreparedArtifact != null)
                    AppendOutput(output, "Fingerprint: " + snapshot.PreparedArtifact.Fingerprint);
            }
            UpdateStatus(editor, status, snapshot.Succeeded
                ? (snapshot.Mode == DdfEditorPreparationMode.Validate
                    ? "Script valido" : "Preparazione completata")
                : "Preparazione terminata con errori");
        }

        private static void BuildProfile(DdfEditorView editor,
            RichTextBox output, Label status, bool compile)
        {
            try
            {
                ShowBuildSnapshot(compile ? editor.PrepareScript() :
                    editor.ValidateScript(), output, editor, status);
            }
            catch (InvalidOperationException exception)
            {
                UpdateStatus(editor, status, exception.Message);
            }
        }

        private void PostToUi(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(action); }
                catch (InvalidOperationException) { }
                return;
            }
            action();
        }

        private static void ShowCompletion(RichTextBox output, Label status,
            DdfEditorView editor, DdfEditorExecutionCompletion completion)
        {
            foreach (DdfBuildIssue issue in completion.BuildIssues)
                AppendOutput(output, issue.ToString());
            if (!string.IsNullOrEmpty(completion.InternalError))
                AppendOutput(output, "Errore interno: " + completion.InternalError);
            DdfExecutionResult result = completion.ExecutionResult;
            if (completion.HostEffects.Disposition != DdfHostEffectDisposition.None)
            {
                AppendOutput(output, "Effetti host: " +
                    completion.HostEffects.Disposition + " · " +
                    completion.HostEffects.Operations.Count + " operazioni");
                foreach (DdfHostEffectRecord effect in completion.HostEffects.Operations)
                    AppendOutput(output, "  #" + effect.Sequence + " " +
                        effect.OperationId + " · " + effect.Status);
            }
            if (result != null)
            {
                if (completion.ProcedureInvocation != null)
                    AppendOutput(output, "Procedura: " +
                        completion.ProcedureInvocation.ProcedureName + " · " +
                        (completion.ProcedureInvocation.Trigger ==
                            DdfEditorProcedureTrigger.HostEvent
                            ? "evento " + completion.ProcedureInvocation.TriggerId
                            : "invocazione manuale"));
                foreach (DdfDiagnostic diagnostic in result.Diagnostics)
                    AppendOutput(output, diagnostic.ToString());
                if (completion.ProcedureInvocation != null &&
                    !string.IsNullOrEmpty(result.ReturnTypeName))
                    AppendOutput(output, "Tipo risultato: " + result.ReturnTypeName);
                if (result.ReturnValue != null)
                    AppendOutput(output, "Valore restituito: " +
                        Convert.ToString(result.ReturnValue, CultureInfo.InvariantCulture));
                if (result.HostValues.Count > 0)
                {
                    AppendOutput(output, "Risultati host:");
                    foreach (DdfHostValueResult value in result.HostValues)
                        AppendOutput(output, "  " + value.Name + " : " + value.TypeName +
                            " = " + FormatHostValue(value.Value) +
                            " (iniziale " + FormatHostValue(value.InitialValue) + ", " +
                            (value.Access == DdfHostValueAccess.ReadWrite
                                ? "lettura/scrittura" : "sola lettura") +
                            (value.WasWritten ? ", modificato" : ", invariato") + ")");
                }
                AppendOutput(output, result.WasCancelled
                    ? "Esecuzione arrestata."
                    : "Esecuzione completata: " + result.Instructions + " passi runtime.");
            }
            UpdateStatus(editor, status, completion.Succeeded
                ? "Esecuzione completata" : completion.WasCancelled
                    ? "Esecuzione arrestata" : "Esecuzione terminata con errori");
        }

        private static void AppendOutput(RichTextBox output, string line)
        {
            if (output.TextLength > 0) output.AppendText(Environment.NewLine);
            output.AppendText(line ?? string.Empty);
            output.SelectionStart = output.TextLength;
            output.ScrollToCaret();
        }

        private static string FormatHostValue(object value)
        {
            if (value == null) return "null";
            if (value is IReadOnlyDictionary<string, object> fields)
                return "{" + string.Join(", ", fields.Select(item =>
                    item.Key + ": " + FormatHostValue(item.Value))) + "}";
            if (value is System.Collections.IEnumerable sequence && value is not string)
                return "[" + string.Join(", ", sequence.Cast<object>()
                    .Select(FormatHostValue)) + "]";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void SaveProfile(DdfEditorView editor, Label status)
        {
            editor.SaveState();
            UpdateStatus(editor, status, "Profilo salvato dall'host in memoria");
        }

        private static OpenDocumentCollection RestoreProfile(DdfEditorView editor,
            DdfInMemoryEditorPersistenceProvider persistence, Label status)
        {
            if (persistence.State == null)
            {
                UpdateStatus(editor, status, "Nessun profilo salvato");
                return editor.DocumentService as OpenDocumentCollection;
            }
            var documents = new OpenDocumentCollection();
            editor.AttachDocumentService(documents);
            editor.RestoreState();
            UpdateStatus(editor, status, "Profilo ripristinato in un nuovo servizio documentale");
            return documents;
        }

        private static void SaveProject(DdfEditorView editor, string projectId,
            string displayName, Label status)
        {
            editor.SaveProject(projectId, displayName);
            UpdateStatus(editor, status, "Progetto consegnato al contenitore host");
        }

        private static OpenDocumentCollection OpenProject(DdfEditorView editor,
            DdfInMemoryEditorProjectContainer container, Label status)
        {
            if (container.Project == null)
            {
                UpdateStatus(editor, status, "Nessun progetto salvato");
                return editor.DocumentService as OpenDocumentCollection;
            }
            if (editor.ExecutionService.IsRunning || editor.DebugService.IsActive)
            {
                UpdateStatus(editor, status,
                    "Termina Run o Debug prima di aprire un progetto");
                return editor.DocumentService as OpenDocumentCollection;
            }
            DdfLanguagePackage activePackage = editor.LanguageSession.ActivePackage;
            if (!string.Equals(container.Project.LanguagePackageId, activePackage.Id,
                    StringComparison.Ordinal) ||
                !string.Equals(container.Project.LanguagePackageVersion,
                    activePackage.PackageVersion, StringComparison.Ordinal))
            {
                UpdateStatus(editor, status, "Pacchetto linguaggio richiesto: " +
                    container.Project.LanguagePackageId + " " +
                    container.Project.LanguagePackageVersion);
                return editor.DocumentService as OpenDocumentCollection;
            }
            var previousDocuments = editor.DocumentService as OpenDocumentCollection;
            var documents = new OpenDocumentCollection();
            try
            {
                editor.AttachDocumentService(documents);
                editor.OpenProject();
                UpdateStatus(editor, status,
                    "Progetto aperto da un nuovo servizio documentale");
                return documents;
            }
            catch (Exception exception)
            {
                editor.AttachDocumentService(previousDocuments);
                UpdateStatus(editor, status,
                    "Apertura progetto non riuscita: " + exception.Message);
                return previousDocuments;
            }
        }

        private static void UpdateStatus(DdfEditorView editor, Label status, string message)
        {
            if (status == null) return;
            string mode = editor.ExperienceState.EducationalModeEnabled ? "didattica ON" : "didattica OFF";
            string document = editor.DocumentService?.ActiveDocumentInfo?.DisplayName ?? "buffer locale";
            status.Text = document + " · " + mode + " · " + message;
        }

        private static async void StopProfile(object service,
            DdfEditorView editor, Label status)
        {
            try
            {
                if (service is IDdfEditorAsyncLifecycleService lifecycle)
                {
                    DdfEditorLifecycleResult result = await lifecycle.StopAsync(
                        TimeSpan.FromSeconds(5));
                    UpdateStatus(editor, status, result.Succeeded
                        ? "Sessione arrestata"
                        : result.ErrorCode + ": " + result.Message);
                    return;
                }

                bool requested = service switch
                {
                    IDdfEditorExecutionService execution => execution.Stop(),
                    IDdfEditorDebugService debugging => debugging.Stop(),
                    _ => false
                };
                UpdateStatus(editor, status, requested
                    ? "Arresto legacy richiesto"
                    : "Nessuna sessione attiva");
            }
            catch (Exception exception)
            {
                UpdateStatus(editor, status,
                    "Arresto non riuscito: " + exception.Message);
            }
        }
    }
}
