using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using DDF___Program_Language_Editor;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.DatabaseDemo
{
    public sealed class DatabaseDemoForm : Form
    {
        private const string HostApiDocumentation = @"// API HOST — DATABASE DEMO
//
// Questo documento descrive i simboli messi a disposizione dall'applicazione.
// Non e' un secondo modulo DDF: l'implementazione risiede nel codice C# host,
// in DemoDatabase.cs, ed e' registrata in ConfigureHostCatalog().
//
// OGGETTI DELLA FORM
// formData.firstName     string   Valore della casella Nome
// formData.lastName      string   Valore della casella Cognome
// formData.isCustomer    bool     Stato della casella Cliente
// selection.customerId  int      ID della riga selezionata
//
// CONTROLLI UI ESPOSTI
// firstNameTextBox, lastNameTextBox, customerCheckBox
// insertButton, removeVowelsButton, countLettersButton, extractRowButton
// Le proprietà disponibili sono consultabili nella palette API host.
// Esempio: firstNameTextBox.backColor << ""LightGreen"";
//
// FUNZIONI DATABASE ESPOSTE ALLO SCRIPT
// insertCustomer(string firstName, string lastName, bool isCustomer) out int
// removeCustomerVowels(int customerId) out int
// countCustomerLetters(int customerId) out int
// loadCustomer(int customerId) out CustomerRow
//
// CustomerRow espone: id, firstName, lastName, isCustomer.
// Le procedure DDF richiamate dai pulsanti si trovano in customer-actions.ddf.";

        private const string DemoSource = @"main() out int
{
    string firstName << formData.firstName;
    string lastName << formData.lastName;
    bool isCustomer << formData.isCustomer;

    int insertedId << insertCustomer(firstName, lastName, isCustomer);
    insertedId >> Console;
    ret insertedId;
}

removeSelectedVowels() out int
{
    ret removeCustomerVowels(selection.customerId);
}

countSelectedLetters() out int
{
    ret countCustomerLetters(selection.customerId);
}

extractSelectedCustomer() out CustomerRow
{
    ret loadCustomer(selection.customerId);
}";

        private readonly TextBox firstNameTextBox;
        private readonly TextBox lastNameTextBox;
        private readonly CheckBox customerCheckBox;
        private readonly Button executeButton;
        private readonly Button removeVowelsButton;
        private readonly Button countLettersButton;
        private readonly Button extractRowButton;
        private readonly Label statusLabel;
        private readonly Label resultLabel;
        private SplitContainer applicationEditorSplit;
        private string currentFirstName = string.Empty;
        private string currentLastName = string.Empty;
        private bool currentIsCustomer;
        private int selectedCustomerId;
        private DemoAction pendingAction;
        private readonly string scriptDocumentId;

        public DatabaseDemoForm()
        {
            Name = "databaseDemoForm";
            Text = "DDF Editor SDK — Database Application Demo";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1180, 760);
            BackColor = Color.FromArgb(245, 246, 248);
            Font = new Font("Segoe UI", 9F);

            Database = new DemoDatabaseService();
            firstNameTextBox = CreateTextBox("firstNameTextBox", "Ada");
            lastNameTextBox = CreateTextBox("lastNameTextBox", "Lovelace");
            customerCheckBox = new CheckBox
            {
                Name = "customerCheckBox",
                AutoSize = true,
                Checked = true,
                Text = "Cliente",
                Margin = new Padding(12, 9, 12, 0)
            };
            executeButton = new Button
            {
                Name = "executeScriptButton",
                AutoSize = true,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font, FontStyle.Bold),
                Padding = new Padding(12, 4, 12, 4),
                Text = "Inserisci tramite script"
            };
            executeButton.FlatAppearance.BorderSize = 0;
            executeButton.Click += ExecuteButton_Click;
            removeVowelsButton = CreateSecondaryButton(
                "removeVowelsButton", "Elimina vocali dal cognome");
            removeVowelsButton.Click += (sender, args) =>
                ExecuteSelectedProcedure(DemoAction.RemoveVowels,
                    "removeSelectedVowels");
            countLettersButton = CreateSecondaryButton(
                "countLettersButton", "Conta lettere cognome");
            countLettersButton.Click += (sender, args) =>
                ExecuteSelectedProcedure(DemoAction.CountLetters,
                    "countSelectedLetters");
            extractRowButton = CreateSecondaryButton(
                "extractRowButton", "Estrai riga selezionata");
            extractRowButton.Click += (sender, args) =>
                ExecuteSelectedProcedure(DemoAction.ExtractRow,
                    "extractSelectedCustomer");

            statusLabel = new Label
            {
                Name = "databaseDemoStatus",
                AutoSize = true,
                ForeColor = Color.FromArgb(80, 80, 80),
                Margin = new Padding(16, 11, 3, 0),
                Text = "Compila i campi e avvia lo script DDF collegato."
            };
            resultLabel = new Label
            {
                Name = "databaseDemoResult",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 100, 175),
                Margin = new Padding(16, 11, 3, 0),
                Text = "Risultato: —"
            };
            CustomerGrid = CreateCustomerGrid();
            CustomerGrid.SelectionChanged += (sender, args) => UpdateSelectedCustomer();

            EditorHost = new DdfEmbeddedEditorControl
            {
                Name = "embeddedDdfEditor",
                HostDisplayName = "Database Application Demo",
                Dock = DockStyle.Fill
            };
            ConfigureHostCatalog();
            EditorHost.ExecutionPreparing += (sender, args) => CaptureFormValues();
            EditorHost.DocumentSaveRequested += EditorShell_EmbeddedDocumentSaveRequested;
            EditorHost.ExecutionCompleted += (sender, args) =>
            {
                RefreshCustomers();
                SetActionButtonsEnabled(true);
                DdfExecutionResult result = EditorHost.LastExecutionResult;
                CompletePendingAction(result);
            };

            Controls.Add(CreateLayout());
            scriptDocumentId = EditorHost.SetPrimaryDocument(
                "customer-actions.ddf", DemoSource);
            EditorHost.AddDocument("API host - documentazione.ddf",
                HostApiDocumentation);
            RefreshCustomers();
            Shown += (sender, args) => RestoreLayout();
        }

        public DdfEmbeddedEditorControl EditorHost { get; }
        public DdfEditorView Editor => EditorHost.Editor;
        public DemoDatabaseService Database { get; }
        public DataGridView CustomerGrid { get; }
        public TextBox FirstNameTextBox => firstNameTextBox;
        public TextBox LastNameTextBox => lastNameTextBox;
        public CheckBox CustomerCheckBox => customerCheckBox;
        public Button ExecuteButton => executeButton;
        public Button RemoveVowelsButton => removeVowelsButton;
        public Button CountLettersButton => countLettersButton;
        public Button ExtractRowButton => extractRowButton;
        public Label ResultLabel => resultLabel;
        public Label StatusLabel => statusLabel;
        public SplitContainer ApplicationEditorSplit => applicationEditorSplit;
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string LayoutPersistencePath { get; set; } = GetDefaultLayoutPath();

        public bool ExecuteLinkedScript(bool? debug = null)
        {
            EditorHost.ActivateDocument(scriptDocumentId);
            bool startDebug = debug ?? EditorHost.HasBreakpoints;
            CaptureFormValues();
            pendingAction = DemoAction.Insert;
            SetActionButtonsEnabled(false);
            statusLabel.Text = startDebug
                ? "Script avviato con supporto breakpoint."
                : "Script DDF in esecuzione...";
            bool started = EditorHost.ExecuteScript(startDebug);
            if (!started)
            {
                pendingAction = DemoAction.None;
                SetActionButtonsEnabled(true);
                statusLabel.Text = "Avvio non riuscito: controllare Output e Diagnostica.";
            }
            return started;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveLayout();
            base.OnFormClosing(e);
        }

        private void EditorShell_EmbeddedDocumentSaveRequested(object sender,
            DdfEmbeddedDocumentSaveEventArgs e)
        {
            if (!string.Equals(e.DocumentId, scriptDocumentId, StringComparison.Ordinal))
            {
                e.ErrorMessage = "La documentazione API è fornita dall'host e non è salvabile.";
                return;
            }
            e.Succeeded = SaveLayout(e.SourceText);
            if (e.Succeeded)
                statusLabel.Text = "Script associato ai pulsanti salvato nell'applicazione host.";
            else
                e.ErrorMessage = "Impossibile salvare lo script nello stato della demo.";
        }

        private Control CreateLayout()
        {
            applicationEditorSplit = new SplitContainer
            {
                Name = "applicationEditorSplit",
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 225,
                SplitterWidth = 7,
                BackColor = Color.FromArgb(205, 208, 212)
            };
            applicationEditorSplit.Panel1.BackColor = BackColor;
            applicationEditorSplit.Panel1.Padding = new Padding(16, 12, 16, 10);
            applicationEditorSplit.Panel1.Controls.Add(CreateApplicationArea());
            applicationEditorSplit.Panel2.BackColor = Color.White;
            applicationEditorSplit.Panel2.Controls.Add(EditorHost);
            return applicationEditorSplit;
        }

        private Control CreateApplicationArea()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 35, 35),
                Margin = new Padding(0, 0, 0, 8),
                Text = "Applicazione clienti"
            }, 0, 0);

            var inputs = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            inputs.Controls.Add(CreateField("Nome", firstNameTextBox));
            inputs.Controls.Add(CreateField("Cognome", lastNameTextBox));
            inputs.Controls.Add(customerCheckBox);
            inputs.Controls.Add(executeButton);
            root.Controls.Add(inputs, 0, 1);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            actions.Controls.Add(removeVowelsButton);
            actions.Controls.Add(countLettersButton);
            actions.Controls.Add(extractRowButton);
            actions.Controls.Add(resultLabel);
            actions.Controls.Add(statusLabel);
            root.Controls.Add(actions, 0, 2);
            root.Controls.Add(CustomerGrid, 0, 3);
            return root;
        }

        private static Control CreateField(string caption, TextBox textBox)
        {
            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 12, 0)
            };
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = caption,
                Margin = new Padding(0, 0, 0, 3)
            });
            panel.Controls.Add(textBox);
            return panel;
        }

        private static TextBox CreateTextBox(string name, string text) => new TextBox
        {
            Name = name,
            Text = text,
            Width = 180,
            Margin = Padding.Empty
        };

        private static Button CreateSecondaryButton(string name, string text)
        {
            var button = new Button
            {
                Name = name,
                AutoSize = true,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(35, 35, 35),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 2, 8, 2),
                Padding = new Padding(8, 2, 8, 2),
                Text = text
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(175, 180, 185);
            return button;
        }

        private static DataGridView CreateCustomerGrid() => new DataGridView
        {
            Name = "databaseDemoCustomerGrid",
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RowHeadersVisible = false
        };

        private void ConfigureHostCatalog()
        {
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterPermission(new DdfHostPermissionDefinition(
                "db.access", "Accesso al database virtuale"));
            catalog.RegisterPermission(new DdfHostPermissionDefinition(
                "db.write", "Inserimento clienti"));
            catalog.RegisterPermission(new DdfHostPermissionDefinition(
                "ui.controls", "Controlli applicazione",
                "Lettura e modifica delle proprietà UI esplicitamente esposte."));
            catalog.RegisterType(new DdfHostTypeDefinition("HostTextBox", new[]
            {
                HostProperty("text", "string", true, "Testo visualizzato dal controllo."),
                HostProperty("backColor", "string", true, "Colore di sfondo: nome o valore HTML."),
                HostProperty("foreColor", "string", true, "Colore del testo: nome o valore HTML."),
                HostProperty("enabled", "bool", true, "Abilita o disabilita il controllo."),
                HostProperty("visible", "bool", true, "Visibilità del controllo.")
            }, "Casella di testo esposta dall'applicazione host.", new[]
            {
                new DdfHostEventDefinition("textChanged", "evento host",
                    "L'host può associare questo evento a una procedura DDF.")
            }));
            catalog.RegisterType(new DdfHostTypeDefinition("HostButton", new[]
            {
                HostProperty("text", "string", true, "Testo visualizzato sul pulsante."),
                HostProperty("backColor", "string", true, "Colore di sfondo: nome o valore HTML."),
                HostProperty("foreColor", "string", true, "Colore del testo: nome o valore HTML."),
                HostProperty("enabled", "bool", true, "Abilita o disabilita il pulsante."),
                HostProperty("visible", "bool", true, "Visibilità del pulsante.")
            }, "Pulsante esposto dall'applicazione host.", new[]
            {
                new DdfHostEventDefinition("click", "evento host",
                    "Avvia la procedura DDF associata dall'applicazione host.")
            }));
            catalog.RegisterType(new DdfHostTypeDefinition("HostCheckBox", new[]
            {
                HostProperty("text", "string", true, "Etichetta della casella."),
                HostProperty("checked", "bool", true, "Stato selezionato."),
                HostProperty("backColor", "string", true, "Colore di sfondo: nome o valore HTML."),
                HostProperty("enabled", "bool", true, "Abilita o disabilita il controllo."),
                HostProperty("visible", "bool", true, "Visibilità del controllo.")
            }, "Casella di selezione esposta dall'applicazione host.", new[]
            {
                new DdfHostEventDefinition("checkedChanged", "evento host",
                    "L'host può associare questo evento a una procedura DDF.")
            }));
            catalog.RegisterType(new DdfHostTypeDefinition("DemoFormData", new[]
            {
                new DdfHostFieldDefinition("firstName", "string"),
                new DdfHostFieldDefinition("lastName", "string"),
                new DdfHostFieldDefinition("isCustomer", "bool")
            }));
            catalog.RegisterType(new DdfHostTypeDefinition("CustomerSelection", new[]
            {
                new DdfHostFieldDefinition("customerId", "int")
            }));
            catalog.RegisterType(new DdfHostTypeDefinition("CustomerRow", new[]
            {
                new DdfHostFieldDefinition("id", "int"),
                new DdfHostFieldDefinition("firstName", "string"),
                new DdfHostFieldDefinition("lastName", "string"),
                new DdfHostFieldDefinition("isCustomer", "bool")
            }));
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "formData", "DemoFormData", () =>
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["firstName"] = currentFirstName,
                        ["lastName"] = currentLastName,
                        ["isCustomer"] = currentIsCustomer
                    }));
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "selection", "CustomerSelection", () =>
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["customerId"] = selectedCustomerId
                    }));
            RegisterControl(catalog, "firstNameTextBox", "HostTextBox", firstNameTextBox,
                "Campo Nome della form.");
            RegisterControl(catalog, "lastNameTextBox", "HostTextBox", lastNameTextBox,
                "Campo Cognome della form.");
            RegisterControl(catalog, "customerCheckBox", "HostCheckBox", customerCheckBox,
                "Selezione Cliente sì/no.");
            RegisterControl(catalog, "insertButton", "HostButton", executeButton,
                "Il click avvia main().");
            RegisterControl(catalog, "removeVowelsButton", "HostButton", removeVowelsButton,
                "Il click avvia removeSelectedVowels().");
            RegisterControl(catalog, "countLettersButton", "HostButton", countLettersButton,
                "Il click avvia countSelectedLetters().");
            RegisterControl(catalog, "extractRowButton", "HostButton", extractRowButton,
                "Il click avvia extractSelectedCustomer().");
            catalog.RegisterDatabase(new DdfHostDatabaseDefinition(
                "customers", Database, new[]
                {
                    new DdfHostDatabaseOperationDefinition(
                        "insertCustomer", "customer.insert",
                        DdfHostDatabaseOperationKind.Command, "int", new[]
                        {
                            new DdfHostParameterDefinition("firstName", "string"),
                            new DdfHostParameterDefinition("lastName", "string"),
                            new DdfHostParameterDefinition("isCustomer", "bool")
                        }, requiredPermissions: new[] { "db.write" })
                    ,
                    new DdfHostDatabaseOperationDefinition(
                        "removeCustomerVowels", "customer.removeLastNameVowels",
                        DdfHostDatabaseOperationKind.Command, "int", new[]
                        {
                            new DdfHostParameterDefinition("customerId", "int")
                        }, requiredPermissions: new[] { "db.write" }),
                    new DdfHostDatabaseOperationDefinition(
                        "countCustomerLetters", "customer.countLastNameLetters",
                        DdfHostDatabaseOperationKind.Query, "int", new[]
                        {
                            new DdfHostParameterDefinition("customerId", "int")
                        }, requiredPermissions: new[] { "db.access" }),
                    new DdfHostDatabaseOperationDefinition(
                        "loadCustomer", "customer.byId",
                        DdfHostDatabaseOperationKind.Query, "CustomerRow", new[]
                        {
                            new DdfHostParameterDefinition("customerId", "int")
                        }, requiredPermissions: new[] { "db.access" })
                }, requiredPermissions: new[] { "db.access" }));
            catalog.SetPermission("db.access", true);
            catalog.SetPermission("db.write", true);
            catalog.SetPermission("ui.controls", true);
            EditorHost.Editor.AttachHostCatalog(catalog);
        }

        private static DdfHostFieldDefinition HostProperty(string name, string type,
            bool writable, string documentation) => new DdfHostFieldDefinition(
                name, type, documentation,
                writable ? DdfHostValueAccess.ReadWrite : DdfHostValueAccess.ReadOnly,
                DdfHostMemberKind.Property);

        private static void RegisterControl(DdfEditorHostCatalog catalog, string name,
            string typeName, Control control, string documentation)
        {
            catalog.RegisterObject(new DdfHostObjectDefinition(name, typeName,
                () => ReadControlState(control), documentation,
                new[] { "ui.controls" },
                value => ApplyControlState(control, value),
                DdfHostObjectLifetime.Live));
        }

        private static object ReadControlState(Control control)
        {
            if (control.InvokeRequired)
                return control.Invoke(new Func<object>(() => ReadControlState(control)));
            var values = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["text"] = control.Text,
                ["backColor"] = ColorTranslator.ToHtml(control.BackColor),
                ["enabled"] = control.Enabled,
                ["visible"] = control.Visible
            };
            if (control is TextBoxBase || control is ButtonBase)
                values["foreColor"] = ColorTranslator.ToHtml(control.ForeColor);
            if (control is CheckBox checkBox) values["checked"] = checkBox.Checked;
            return values;
        }

        private static void ApplyControlState(Control control, object value)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new Action(() => ApplyControlState(control, value)));
                return;
            }
            if (!(value is IReadOnlyDictionary<string, object> values) &&
                !(value is IDictionary<string, object>))
                throw new InvalidOperationException("Lo stato del controllo host non è valido.");
            object Read(string key)
            {
                if (value is IReadOnlyDictionary<string, object> readOnly &&
                    readOnly.TryGetValue(key, out object readOnlyValue)) return readOnlyValue;
                if (value is IDictionary<string, object> dictionary &&
                    dictionary.TryGetValue(key, out object dictionaryValue)) return dictionaryValue;
                return null;
            }
            control.Text = Convert.ToString(Read("text"));
            control.BackColor = ParseHostColor(Convert.ToString(Read("backColor")));
            control.Enabled = Convert.ToBoolean(Read("enabled"));
            control.Visible = Convert.ToBoolean(Read("visible"));
            object foreColor = Read("foreColor");
            if (foreColor != null && (control is TextBoxBase || control is ButtonBase))
                control.ForeColor = ParseHostColor(Convert.ToString(foreColor));
            if (control is CheckBox checkBox)
                checkBox.Checked = Convert.ToBoolean(Read("checked"));
        }

        private static Color ParseHostColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return SystemColors.Window;
            try { return ColorTranslator.FromHtml(value.Trim()); }
            catch (Exception exception) when (exception is ArgumentException ||
                                               exception is FormatException)
            {
                throw new InvalidOperationException("Colore host non valido: " + value + ".");
            }
        }

        private void ExecuteButton_Click(object sender, EventArgs e) => ExecuteLinkedScript();

        private void CaptureFormValues()
        {
            currentFirstName = firstNameTextBox.Text.Trim();
            currentLastName = lastNameTextBox.Text.Trim();
            currentIsCustomer = customerCheckBox.Checked;
        }

        private void ExecuteSelectedProcedure(DemoAction action, string procedureName)
        {
            EditorHost.ActivateDocument(scriptDocumentId);
            UpdateSelectedCustomer();
            if (selectedCustomerId <= 0)
            {
                statusLabel.Text = "Selezionare prima una riga della griglia.";
                return;
            }
            pendingAction = action;
            SetActionButtonsEnabled(false);
            statusLabel.Text = "Funzione " + procedureName +
                " avviata con supporto breakpoint.";
            bool started = EditorHost.ExecuteProcedure(
                procedureName, debug: EditorHost.HasBreakpoints);
            if (!started)
            {
                pendingAction = DemoAction.None;
                SetActionButtonsEnabled(true);
                statusLabel.Text = "Avvio non riuscito: controllare Output e Diagnostica.";
            }
        }

        private void SetActionButtonsEnabled(bool enabled)
        {
            executeButton.Enabled = enabled;
            removeVowelsButton.Enabled = enabled;
            countLettersButton.Enabled = enabled;
            extractRowButton.Enabled = enabled;
        }

        private void CompletePendingAction(DdfExecutionResult result)
        {
            DemoAction completedAction = pendingAction;
            pendingAction = DemoAction.None;
            if (result?.Succeeded != true)
            {
                statusLabel.Text = "Script terminato: controllare Output e Diagnostica nell'editor.";
                return;
            }

            switch (completedAction)
            {
                case DemoAction.Insert:
                    statusLabel.Text = "Record inserito nel database virtuale tramite DDF.";
                    resultLabel.Text = "Risultato: ID " + Convert.ToString(result.ReturnValue);
                    break;
                case DemoAction.RemoveVowels:
                    statusLabel.Text = "Vocali eliminate dal cognome tramite DDF.";
                    resultLabel.Text = "Risultato: riga " + Convert.ToString(result.ReturnValue);
                    break;
                case DemoAction.CountLetters:
                    statusLabel.Text = "Conteggio completato tramite DDF.";
                    resultLabel.Text = "Risultato: " + Convert.ToString(result.ReturnValue) +
                        " lettere nel cognome";
                    break;
                case DemoAction.ExtractRow:
                    if (TryReadCustomer(result.ReturnValue, out DemoCustomer customer))
                    {
                        firstNameTextBox.Text = customer.FirstName;
                        lastNameTextBox.Text = customer.LastName;
                        customerCheckBox.Checked = customer.IsCustomer;
                        resultLabel.Text = "Risultato: riga " + customer.Id + " caricata";
                        statusLabel.Text = "Dati estratti dallo script nei controlli della form.";
                    }
                    else statusLabel.Text = "Risultato estrazione non riconosciuto.";
                    break;
                default:
                    statusLabel.Text = "Script DDF completato.";
                    break;
            }
        }

        private static bool TryReadCustomer(object value, out DemoCustomer customer)
        {
            customer = null;
            if (!(value is IReadOnlyDictionary<string, object> row)) return false;
            customer = new DemoCustomer
            {
                Id = Convert.ToInt32(row["id"]),
                FirstName = Convert.ToString(row["firstName"]),
                LastName = Convert.ToString(row["lastName"]),
                IsCustomer = Convert.ToBoolean(row["isCustomer"])
            };
            return true;
        }

        private void UpdateSelectedCustomer()
        {
            if (CustomerGrid.CurrentRow == null || CustomerGrid.CurrentRow.IsNewRow ||
                !CustomerGrid.Columns.Contains("Id"))
            {
                if (Database.Customers.Count == 0) selectedCustomerId = 0;
                return;
            }
            object value = CustomerGrid.CurrentRow.Cells["Id"]?.Value;
            selectedCustomerId = value == null ? 0 : Convert.ToInt32(value);
        }

        private void RefreshCustomers()
        {
            int previousSelection = selectedCustomerId;
            CustomerGrid.DataSource = Database.Customers.Select(item => new
            {
                item.Id,
                Nome = item.FirstName,
                Cognome = item.LastName,
                Cliente = item.IsCustomer
            }).ToList();
            if (CustomerGrid.Rows.Count > 0)
            {
                DataGridViewRow selected = CustomerGrid.Rows.Cast<DataGridViewRow>()
                    .FirstOrDefault(row => Convert.ToInt32(row.Cells["Id"].Value) ==
                        previousSelection) ?? CustomerGrid.Rows[0];
                selected.Selected = true;
                CustomerGrid.CurrentCell = selected.Cells[0];
            }
            UpdateSelectedCustomer();
        }

        private void RestoreLayout()
        {
            try
            {
                string path = LayoutPersistencePath;
                if (!File.Exists(path)) return;
                DemoLayoutState state = JsonSerializer.Deserialize<DemoLayoutState>(
                    File.ReadAllText(path));
                if (state == null) return;
                int maximum = Math.Max(applicationEditorSplit.Panel1MinSize,
                    applicationEditorSplit.Height - applicationEditorSplit.Panel2MinSize -
                    applicationEditorSplit.SplitterWidth);
                applicationEditorSplit.SplitterDistance = Math.Max(
                    applicationEditorSplit.Panel1MinSize,
                    Math.Min(state.ApplicationAreaHeight, maximum));
                if (!string.IsNullOrWhiteSpace(state.ScriptSource))
                {
                    EditorHost.ActivateDocument(scriptDocumentId);
                    EditorHost.SetPrimaryDocument(
                        "customer-actions.ddf", state.ScriptSource);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (JsonException) { }
            catch (InvalidOperationException) { }
        }

        private bool SaveLayout(string scriptSource = null)
        {
            if (applicationEditorSplit == null || applicationEditorSplit.IsDisposed) return false;
            try
            {
                string path = LayoutPersistencePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string persistedSource = scriptSource ?? EditorHost.Editor.DocumentService
                    .OpenDocuments.FirstOrDefault(document =>
                        string.Equals(document.Id, scriptDocumentId, StringComparison.Ordinal))
                    ?.SourceText ?? DemoSource;
                File.WriteAllText(path, JsonSerializer.Serialize(new DemoLayoutState
                {
                    ApplicationAreaHeight = applicationEditorSplit.SplitterDistance,
                    ScriptSource = persistedSource
                }, new JsonSerializerOptions { WriteIndented = true }));
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static string GetDefaultLayoutPath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DDFTechnology", "DDFLanguageEditor", "DatabaseDemo.layout.json");

        private enum DemoAction
        {
            None,
            Insert,
            RemoveVowels,
            CountLetters,
            ExtractRow
        }

        private sealed class DemoLayoutState
        {
            public int ApplicationAreaHeight { get; set; } = 225;
            public string ScriptSource { get; set; }
        }
    }
}
