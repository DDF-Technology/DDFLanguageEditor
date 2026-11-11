using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDF___Program_Language_Editor;
using DDFLanguageEditor.Compiler;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.DatabaseDemo;
using DDFLanguageEditor.HostDemo;
using DDFLanguageEditor.MinimalHost;

namespace DDFLanguageEditor.EditorSmokeTests
{
    internal static class Program
    {
        private const int WmLeftButtonDown = 0x0201;
        private const int EmGetScrollPos = 0x0400 + 221;
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
                if (args.Any(argument => string.Equals(argument, "--database-demo-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertDatabaseIntegrationDemo(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--host-contracts-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertMinimalHostDemo(visible);
                    AssertEmbeddableHostDemo(visible);
                    AssertDatabaseIntegrationDemo(visible);
                    AssertSdkInstanceIsolation();
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument, "--execution-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    using (var executionForm = ShowSmokeForm(visible))
                        AssertProgramExecution(executionForm,
                            FindControl<RichTextBox>(executionForm, "richTextBoxMainEditor"));
                    Console.WriteLine("PASS smoke mirato esecuzione/debug");
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--product-updates-only", StringComparison.OrdinalIgnoreCase)))
                {
                    using (var updateForm = ShowSmokeForm(visible))
                        AssertProductUpdates(updateForm);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--session-restore-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertRestoredMultiFileGutter(visible);
                    Console.WriteLine("PASS ripristino della prima scheda multifile");
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--compiled-workspace-debug-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertCompiledLanguageFeaturesWorkspaceDebug(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--workspace-rename-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertWorkspaceRenameFromExternalReference(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--workspace-flowchart-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    using (var workspaceForm = ShowSmokeForm(visible))
                    {
                        AssertWorkspaceNavigation(workspaceForm,
                            FindControl<RichTextBox>(workspaceForm, "richTextBoxMainEditor"),
                            FindControl<ListBox>(workspaceForm, "listBoxDiagnostics"));
                    }
                    Console.WriteLine("PASS smoke mirato Flowchart workspace multifile");
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument, "--language-profile-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertInstalledProfilesAndCloseAllWorkspaceDocuments(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument, "--language-studio-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertHelpMenu(visible);
                    AssertStandalonePackageStudioFactory();
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument, "--module-manager-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertModuleMenu(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument, "--embedded-module-capability-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertEmbeddedModuleStoreCapability(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument, "--large-workspace-performance-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertLargeDocumentAndWorkspaceResponsiveness(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--realistic-performance-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertPerformanceBudgets(visible);
                    AssertLargeDocumentAndWorkspaceResponsiveness(visible);
                    AssertDebuggerPerformance(visible);
                    AssertRealisticLanguagePackageSwitching(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--embedded-reactivity-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertExtendedEmbeddedReactivity(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--lifecycle-endurance-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertExecutionLifecycleEndurance(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--persistence-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertVersionedPersistenceContracts();
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--capability-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertOptionalCapabilityNegotiation();
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--sdk-contract-matrix-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertVersionedPersistenceContracts();
                    AssertOptionalCapabilityNegotiation();
                    AssertSdkInstanceIsolation();
                    AssertSdkFailureRecovery();
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--flowchart-geometry-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertFlowchartGeometryOnly(visible);
                    return 0;
                }
                if (args.Any(argument => string.Equals(argument,
                        "--flowchart-runtime-sync-only",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    using (var executionForm = ShowSmokeForm(visible))
                        AssertProgramExecution(executionForm,
                            FindControl<RichTextBox>(executionForm, "richTextBoxMainEditor"));
                    Console.WriteLine("PASS matrice sincronizzazione runtime editor/Flowchart");
                    return 0;
                }
                if (!args.Any(argument => string.Equals(argument, "--skip-host-demo",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    AssertEmbeddableHostDemo(visible);
                    AssertDatabaseIntegrationDemo(visible);
                    AssertMinimalHostDemo(visible);
                    AssertEmbeddedModuleStoreCapability(visible);
                    AssertSdkInstanceIsolation();
                }
                AssertExtendedEmbeddedReactivity(visible);
                AssertVersionedPersistenceContracts();
                AssertOptionalCapabilityNegotiation();
                AssertSdkFailureRecovery();
                RunEditorScenarios(visible);
                AssertStandalonePackageStudioFactory();
                AssertCompiledLanguageFeaturesWorkspaceDebug(visible);
                AssertDocumentViewportPersistence(visible);
                AssertRestoredMultiFileGutter(visible);
                AssertPerformanceBudgets(visible);
                AssertLargeDocumentAndWorkspaceResponsiveness(visible);
                AssertProlongedEditingSession(visible);
                AssertExecutionLifecycleEndurance(visible);
                AssertDebuggerPerformance(visible);
                RunMenuScenarios(visible);
                AssertRunLastBuildCommand(visible);
                AssertInstalledProfilesAndCloseAllWorkspaceDocuments(visible);
                AssertRealisticLanguagePackageSwitching(visible);
                if (UiExceptions.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Eccezioni UI intercettate:\n" + string.Join("\n---\n", UiExceptions.Select(exception => exception.ToString())));
                }

                Console.WriteLine("PASS smoke dinamico WinForms: scenari editor e tutti gli 80 comandi di menu completati senza eccezioni.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL smoke dinamico WinForms: " + exception);
                return 1;
            }
        }

        private static void AssertExtendedEmbeddedReactivity(bool visible)
        {
            using var host = new Form
            {
                ShowInTaskbar = visible,
                StartPosition = FormStartPosition.Manual,
                Location = visible ? new Point(80, 80) : new Point(-32000, -32000),
                Size = new Size(900, 650)
            };
            using var first = new DdfEditorView { Dock = DockStyle.Left, Width = 440 };
            using var second = new DdfEditorView { Dock = DockStyle.Fill };
            host.Controls.Add(second);
            host.Controls.Add(first);
            host.Show();
            PumpMessages(100);
            int uiThread = Environment.CurrentManagedThreadId;

            var firstDocuments = new OpenDocumentCollection();
            IDdfEditorDocument firstDocument = firstDocuments.CreateVirtual(
                "reactivity.ddf", "main() out int { int value << 0; while(true) { value++; } ret value; }");
            first.AttachDocumentService(firstDocuments);
            var secondDocuments = new OpenDocumentCollection();
            secondDocuments.CreateVirtual("parallel.ddf",
                "main() out int { int value << 0; while(true) { value++; } ret value; }");
            second.AttachDocumentService(secondDocuments);

            Task<DdfEditorExecutionCompletion> cpuRun = first.ExecutionService.RunAsync(
                new DdfEditorExecutionRequest(maxInstructions: int.MaxValue));
            PumpUntil(() => first.ExecutionService.IsRunning, 2000,
                "Lo script CPU-bound embedded non entra in esecuzione.");
            AssertUiHeartbeat(first,
                "Il thread UI si blocca durante uno script CPU-bound.");
            Task<DdfEditorLifecycleResult> cpuStop =
                ((IDdfEditorAsyncLifecycleService)first.ExecutionService)
                .StopAsync(TimeSpan.FromSeconds(5));
            PumpUntil(() => cpuStop.IsCompleted && cpuRun.IsCompleted, 6000,
                "StopAsync non termina lo script CPU-bound.");
            Require(cpuStop.Result.Succeeded && cpuRun.Result.WasCancelled,
                "StopAsync non osserva la cancellazione dello script CPU-bound.");

            var popupEntered = new ManualResetEventSlim();
            var popupRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int popupThread = 0;
            var popupCatalog = new DdfEditorHostCatalog();
            popupCatalog.RegisterPermission(new DdfHostPermissionDefinition(
                "ui.smoke", "Smoke UI"));
            popupCatalog.SetPermission("ui.smoke", true);
            popupCatalog.RegisterFunction(new DdfHostFunctionDefinition(
                "popupProbe", "int", Array.Empty<DdfHostParameterDefinition>(),
                invoke: null, requiredPermissions: new[] { "ui.smoke" },
                invokeAsync: (context, arguments) =>
                {
                    popupThread = Environment.CurrentManagedThreadId;
                    popupEntered.Set();
                    return new ValueTask<object>(popupRelease.Task);
                }, dispatchTarget: DdfHostFunctionDispatchTarget.Ui));
            first.AttachHostCatalog(popupCatalog);
            firstDocuments.UpdateDocument(firstDocument.Id,
                "main() out int { ret popupProbe() + 1; }");
            Task<DdfEditorExecutionCompletion> popupRun =
                first.ExecutionService.RunAsync();
            PumpUntil(() => popupEntered.IsSet &&
                    ((IDdfEditorCooperativeExecutionService)first.ExecutionService)
                    .CooperativeSnapshot.IsSuspended,
                3000, "La capability popup non sospende il runtime embedded.");
            AssertUiHeartbeat(first,
                "La capability popup sospesa blocca l'heartbeat UI.");
            Require(popupThread == uiThread,
                "La presentazione popup non viene eseguita sul dispatcher UI.");
            popupRelease.SetResult(41);
            PumpUntil(() => popupRun.IsCompleted, 5000,
                "La capability popup non riprende il runtime.");
            Require(popupRun.Result.Succeeded && Convert.ToInt64(
                    popupRun.Result.ExecutionResult.ReturnValue) == 42L,
                "La ripresa dalla capability popup produce un risultato errato.");

            var slowEntered = new ManualResetEventSlim();
            popupCatalog.RegisterFunction(new DdfHostFunctionDefinition(
                "slowProbe", "int", Array.Empty<DdfHostParameterDefinition>(),
                arguments =>
                {
                    slowEntered.Set();
                    Thread.Sleep(250);
                    return 7;
                }));
            firstDocuments.UpdateDocument(firstDocument.Id,
                "main() out int { ret slowProbe(); }");
            Task<DdfEditorExecutionCompletion> slowRun =
                first.ExecutionService.RunAsync();
            PumpUntil(() => slowEntered.IsSet, 2000,
                "Il provider host lento non viene invocato.");
            AssertUiHeartbeat(first,
                "Un provider host lento blocca il thread UI.");
            PumpUntil(() => slowRun.IsCompleted, 3000,
                "Il provider host lento non completa.");
            Require(slowRun.Result.Succeeded,
                "Il provider host lento fallisce nello smoke di reattività.");

            firstDocuments.UpdateDocument(firstDocument.Id, string.Join("\n", new[]
            {
                "main() out int",
                "{",
                "    int value << 0;",
                "    while(value < 10)",
                "    {",
                "        value++;",
                "    }",
                "    ret value;",
                "}"
            }));
            first.DebugService.SetBreakpoints(new[]
                { new DdfEditorBreakpoint(firstDocument.Id, 6) });
            Task<DdfEditorExecutionCompletion> debugRun =
                first.DebugService.RunAsync();
            PumpUntil(() => first.DebugService.IsPaused, 5000,
                "Il debugger embedded non raggiunge il breakpoint nello smoke di reattività.");
            AssertUiHeartbeat(first,
                "Un breakpoint embedded blocca l'heartbeat UI.");
            Task<DdfEditorLifecycleResult> debugStop =
                ((IDdfEditorAsyncLifecycleService)first.DebugService)
                .StopAsync(TimeSpan.FromSeconds(5));
            PumpUntil(() => debugStop.IsCompleted && debugRun.IsCompleted, 6000,
                "StopAsync non chiude il debugger fermo a un breakpoint.");
            Require(debugStop.Result.Succeeded && debugRun.Result.WasCancelled,
                "Il lifecycle asincrono del debugger non restituisce la cancellazione.");

            firstDocuments.UpdateDocument(firstDocument.Id,
                "main() out int { int value << 0; while(true) { value++; } ret value; }");
            Task<DdfEditorExecutionCompletion> firstParallel =
                first.ExecutionService.RunAsync(new DdfEditorExecutionRequest(
                    maxInstructions: int.MaxValue));
            Task<DdfEditorExecutionCompletion> secondParallel =
                second.ExecutionService.RunAsync(new DdfEditorExecutionRequest(
                    maxInstructions: int.MaxValue));
            PumpUntil(() => first.ExecutionService.IsRunning &&
                    second.ExecutionService.IsRunning, 3000,
                "Le due istanze embedded non avanzano in parallelo.");
            AssertUiHeartbeat(second,
                "Due istanze embedded simultanee bloccano il thread UI.");
            Task<DdfEditorLifecycleResult> firstStop =
                ((IDdfEditorAsyncLifecycleService)first.ExecutionService)
                .StopAsync(TimeSpan.FromSeconds(5));
            Task<DdfEditorLifecycleResult> secondStop =
                ((IDdfEditorAsyncLifecycleService)second.ExecutionService)
                .StopAsync(TimeSpan.FromSeconds(5));
            PumpUntil(() => firstStop.IsCompleted && secondStop.IsCompleted &&
                    firstParallel.IsCompleted && secondParallel.IsCompleted,
                7000, "Le istanze embedded simultanee entrano in deadlock durante Stop.");
            Require(firstStop.Result.Succeeded && secondStop.Result.Succeeded &&
                    firstParallel.Result.WasCancelled &&
                    secondParallel.Result.WasCancelled,
                "Le istanze embedded simultanee non restano isolate durante Stop.");

            using (var disposable = new DdfEditorView())
            using (var disposeEntered = new ManualResetEventSlim())
            {
                disposable.CreateControl();
                var disposeDocuments = new OpenDocumentCollection();
                disposeDocuments.CreateVirtual("dispose.ddf",
                    "main() out int { ret cancellableProbe(); }");
                disposable.AttachDocumentService(disposeDocuments);
                var disposeCatalog = new DdfEditorHostCatalog();
                disposeCatalog.RegisterFunction(new DdfHostFunctionDefinition(
                    "cancellableProbe", "int",
                    Array.Empty<DdfHostParameterDefinition>(), invoke: null,
                    invokeAsync: async (context, arguments) =>
                    {
                        disposeEntered.Set();
                        await Task.Delay(TimeSpan.FromMinutes(1),
                            context.CancellationToken);
                        return 1;
                    }));
                disposable.AttachHostCatalog(disposeCatalog);
                int callbacks = 0;
                disposable.ExecutionStateChanged += (sender, args) => callbacks++;
                disposable.ExecutionCompleted += (sender, args) => callbacks++;
                _ = disposable.ExecutionService.RunAsync();
                PumpUntil(() => disposeEntered.IsSet, 3000,
                    "Il provider da cancellare durante dispose non viene invocato.");
                Task<DdfEditorLifecycleResult> disposing =
                    ((IDdfEditorAsyncLifecycleService)disposable.ExecutionService)
                    .DisposeAsync(TimeSpan.FromSeconds(5));
                PumpUntil(() => disposing.IsCompleted, 6000,
                    "DisposeAsync embedded non osserva il cleanup.");
                int callbacksAfterDispose = callbacks;
                PumpMessages(150);
                Require(disposing.Result.Succeeded &&
                        callbacks == callbacksAfterDispose,
                    "Sono stati consegnati callback embedded dopo DisposeAsync.");
            }

            Console.WriteLine(
                "PASS matrice reattività embedded: CPU, capability UI, provider lento, breakpoint, dispose e multi-istanza");
        }

        private static void AssertVersionedPersistenceContracts()
        {
            var workspace = new WorkspaceSessionSnapshot
            {
                WorkspacePath = "C:\\workspace",
                Documents = new List<WorkspaceSessionDocumentSnapshot>
                {
                    new WorkspaceSessionDocumentSnapshot { Path = "main.ddf" }
                }
            };
            DdfPersistenceReadResult<WorkspaceSessionSnapshot> currentWorkspace =
                WorkspaceSessionSnapshot.ParseVersioned(workspace.Serialize());
            Require(currentWorkspace.Succeeded && !currentWorkspace.WasMigrated &&
                    currentWorkspace.Inspection.Status ==
                    DdfPersistenceInspectionStatus.Current,
                "La sessione workspace corrente non viene riconosciuta.");

            const string legacyWorkspace =
                "{\"WorkspacePath\":\"C:\\\\workspace\",\"ActiveDocumentIndex\":0," +
                "\"Documents\":[{\"Path\":\"main.ddf\"}]}";
            DdfPersistenceReadResult<WorkspaceSessionSnapshot> migratedWorkspace =
                WorkspaceSessionSnapshot.ParseVersioned(legacyWorkspace);
            Require(migratedWorkspace.Succeeded && migratedWorkspace.WasMigrated &&
                    migratedWorkspace.OriginalPayload == legacyWorkspace,
                "La sessione workspace legacy non viene migrata preservando l'originale.");

            const string futureWorkspace =
                "{\"FormatVersion\":9,\"Documents\":[{\"Path\":\"main.ddf\"}]}";
            DdfPersistenceReadResult<WorkspaceSessionSnapshot> rejectedWorkspace =
                WorkspaceSessionSnapshot.ParseVersioned(futureWorkspace);
            Require(!rejectedWorkspace.Succeeded &&
                    rejectedWorkspace.OriginalPayload == futureWorkspace &&
                    rejectedWorkspace.Inspection.Status ==
                    DdfPersistenceInspectionStatus.UnsupportedVersion,
                "Una sessione futura non viene rifiutata conservando il payload.");

            var hotExit = new HotExitSnapshot
            {
                Documents = new List<HotExitDocumentSnapshot>
                {
                    new HotExitDocumentSnapshot { Source = "main() out int { ret 1; }" }
                }
            };
            Require(HotExitSnapshot.ParseVersioned(hotExit.Serialize()).Succeeded,
                "Hot Exit corrente non supera il contratto versionato.");
            var recovery = new CrashRecoverySnapshot
            {
                Documents = hotExit.Documents
            };
            Require(CrashRecoverySnapshot.ParseVersioned(recovery.Serialize()).Succeeded,
                "Crash Recovery corrente non supera il contratto versionato.");

            var watch = new WatchExpressionSettings();
            Require(WatchExpressionSettings.ParseVersioned(watch.Serialize()).Succeeded,
                "Watch corrente non supera il contratto versionato.");
            const string futureWatch = "{\"FormatVersion\":8,\"Projects\":[]}";
            DdfPersistenceReadResult<WatchExpressionSettings> rejectedWatch =
                WatchExpressionSettings.ParseVersioned(futureWatch);
            Require(!rejectedWatch.Succeeded &&
                    rejectedWatch.OriginalPayload == futureWatch,
                "Watch futura non viene rifiutata preservando il payload.");

            const string legacyPalette =
                "outlineWidth=340\ndiagnosticsHeight=210\noutlinePinned=True";
            DdfPersistenceReadResult<PaletteLayoutSettings> migratedPalette =
                PaletteLayoutSettings.ParseVersioned(legacyPalette);
            Require(migratedPalette.Succeeded && migratedPalette.WasMigrated &&
                    migratedPalette.Value.Serialize().StartsWith(
                        "schemaVersion=1.0", StringComparison.Ordinal),
                "Il layout palette legacy non viene migrato allo schema 1.0.");
            const string futurePalette =
                "schemaVersion=9.0\noutlineWidth=999";
            Require(!PaletteLayoutSettings.ParseVersioned(futurePalette).Succeeded,
                "Un layout palette futuro viene applicato silenziosamente.");

            Type toolbarType = typeof(MainForm).Assembly.GetType(
                "DDF___Program_Language_Editor.ToolbarLayoutSettings", true);
            MethodInfo parseToolbar = toolbarType.GetMethod("ParseVersioned",
                BindingFlags.Static | BindingFlags.NonPublic);
            object toolbarResult = parseToolbar.Invoke(null,
                new object[] { "file.visible=True", new[] { "file" } });
            Require(GetPropertyValue<bool>(toolbarResult, "Succeeded") &&
                    GetPropertyValue<bool>(toolbarResult, "WasMigrated"),
                "Il layout toolbar legacy non viene migrato esplicitamente.");
            object toolbar = GetPropertyValue<object>(toolbarResult, "Value");
            string toolbarPayload = (string)toolbarType.GetMethod("Serialize")
                .Invoke(toolbar, null);
            Require(toolbarPayload.StartsWith("schemaVersion=1.0",
                    StringComparison.Ordinal),
                "Il layout toolbar non salva la versione dello schema.");

            var preferences = new DdfEditorPersistedPreferences(
                new DdfEditorHostOptions(),
                Array.Empty<KeyValuePair<string, Keys>>(), false);
            var legacyEditorState = new DdfEditorPersistedState("legacy",
                new[]
                {
                    new DdfEditorPersistedDocument("legacy.ddf", string.Empty,
                        "main() out int { ret 7; }", true)
                }, 0, preferences);
            using (var rejectingView = new DdfEditorView())
            {
                var rejectingDocuments = new OpenDocumentCollection();
                rejectingView.AttachDocumentService(rejectingDocuments);
                var rejectingProvider = new DdfInMemoryEditorPersistenceProvider();
                rejectingProvider.Save(legacyEditorState);
                rejectingView.AttachPersistenceProvider(rejectingProvider);
                bool rejected = false;
                try { rejectingView.RestoreState(); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && rejectingDocuments.OpenDocuments.Count == 0 &&
                        ReferenceEquals(rejectingProvider.State, legacyEditorState),
                    "Uno snapshot SDK legacy senza migratore viene applicato o modificato.");
            }

            using (var migratingView = new DdfEditorView())
            {
                var migratingDocuments = new OpenDocumentCollection();
                migratingView.AttachDocumentService(migratingDocuments);
                var migratingProvider = new MigratingEditorPersistenceProvider(
                    legacyEditorState);
                migratingView.AttachPersistenceProvider(migratingProvider);
                Require(migratingView.RestoreState() &&
                        migratingProvider.MigrationCount == 1 &&
                        migratingDocuments.OpenDocuments.Single().SourceText
                            .Contains("ret 7", StringComparison.Ordinal) &&
                        ReferenceEquals(migratingProvider.State,
                            legacyEditorState),
                    "Il provider SDK non migra atomicamente lo snapshot legacy.");
            }

            Console.WriteLine("PASS persistenza versionata, migrazione legacy e rifiuto atomico degli snapshot futuri");
        }

        private static void AssertOptionalCapabilityNegotiation()
        {
            Require(typeof(IDdfEditorSurface).GetProperty("KnownCapabilities") == null &&
                    !typeof(IDdfEditorSurface).GetInterfaces().Contains(
                        typeof(IDdfEditorCapabilityProvider)),
                "La capability discovery è stata aggiunta al contratto obbligatorio legacy.");

            using (var view = new DdfEditorView())
            {
                var provider = (IDdfEditorCapabilityProvider)view;
                Require(provider.KnownCapabilities.Count == 5 &&
                        provider.KnownCapabilities.Select(item => item.Id)
                            .Distinct(StringComparer.Ordinal).Count() == 5,
                    "Il catalogo capability SDK non è stabile o contiene duplicati.");
                foreach (DdfEditorCapabilityDescriptor descriptor in
                    provider.KnownCapabilities)
                {
                    DdfEditorCapabilityResult inspected =
                        provider.InspectCapability(descriptor.Id);
                    if (descriptor.Id == DdfEditorCapabilityIds.ProductUpdates)
                    {
                        Require(!inspected.IsAvailable && inspected.Issue?.Code ==
                                DdfEditorCapabilityCatalog.UnavailableDiagnosticCode,
                            "L'embedded non configurato abilita implicitamente gli aggiornamenti.");
                    }
                    else
                    {
                        Require(inspected.IsAvailable && inspected.Issue == null,
                            "La superficie predefinita non espone " + descriptor.Id + ".");
                    }
                }

                DdfProductComponentVersion sdkComponent =
                    DdfProductVersionCatalog.Current.EmbeddedComponents.Single(
                        item => item.Id == "winforms-sdk");
                DdfProductUpdateResult sdkCurrent =
                    DdfProductUpdateResult.UpToDate(
                        DdfProductUpdateComponentKind.EmbeddedSdk,
                        sdkComponent.Id, sdkComponent.Version,
                        sdkComponent.Version, DdfProductUpdateChannel.Beta,
                        DateTimeOffset.UtcNow);
                var updateProvider = new FixedProductUpdateProvider(sdkCurrent);
                view.AttachProductUpdateService(
                    DdfEditorProductUpdateService.ForEmbeddedComponent(
                        updateProvider, sdkComponent.Id));
                DdfEditorCapabilityResult updateCapability =
                    provider.InspectCapability(DdfEditorCapabilityIds.ProductUpdates);
                Require(updateCapability.TryGetService(
                            out IDdfEditorProductUpdateService updateService) &&
                        updateProvider.CheckCount == 0,
                    "Il servizio aggiornamenti embedded non è opt-in o esegue rete al collegamento.");
                Require(updateService.CheckAsync(DdfProductUpdateChannel.Beta)
                            .GetAwaiter().GetResult().State ==
                            DdfProductUpdateState.UpToDate &&
                        updateProvider.CheckCount == 1 &&
                        view.DetachProductUpdateService() &&
                        !provider.InspectCapability(
                            DdfEditorCapabilityIds.ProductUpdates).IsAvailable,
                    "Il servizio aggiornamenti embedded non rispetta invocazione esplicita o revoca.");

                DdfEditorCapabilityResult unknown = provider.InspectCapability(
                    "editor.capability.unknown");
                Require(!unknown.IsAvailable && unknown.Descriptor == null &&
                        unknown.Issue?.Code ==
                        DdfEditorCapabilityCatalog.UnknownDiagnosticCode,
                    "Una capability sconosciuta non produce DDFH007.");

                var legacyService = new LegacyExecutionService();
                view.AttachExecutionService(legacyService);
                DdfEditorCapabilityResult unavailable = provider.InspectCapability(
                    DdfEditorCapabilityIds.ScriptStatePersistence);
                Require(!unavailable.IsAvailable && unavailable.Descriptor != null &&
                        unavailable.Issue?.Code ==
                        DdfEditorCapabilityCatalog.UnavailableDiagnosticCode &&
                        unavailable.Issue.Domain ==
                        DdfDiagnosticDomain.EngineCapability,
                    "Un servizio legacy senza capability non produce DDFH006.");

                bool typedFailure = false;
                try { view.SaveScriptState(); }
                catch (DdfEditorCapabilityUnavailableException exception)
                {
                    typedFailure = exception.Code ==
                        DdfEditorCapabilityCatalog.UnavailableDiagnosticCode &&
                        exception.CapabilityId ==
                        DdfEditorCapabilityIds.ScriptStatePersistence;
                }
                Require(typedFailure,
                    "La chiamata imperativa legacy non espone l'errore capability tipizzato.");
            }

            MemberInfo[] compatibilityMembers =
            {
                typeof(IDdfEditorExecutionService).GetMethod("Stop"),
                typeof(IDdfEditorDebugService).GetMethod("Stop"),
                typeof(DdfHostValueAdapter).GetConstructor(new[]
                {
                    typeof(Func<object, object>), typeof(Func<object, object>)
                }),
                typeof(DdfEditorPersistedState).GetConstructor(new[]
                {
                    typeof(IEnumerable<DdfEditorPersistedDocument>), typeof(int),
                    typeof(DdfEditorPersistedPreferences),
                    typeof(DdfEditorPersistedDebugState)
                })
            };
            var lifecycleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MemberInfo member in compatibilityMembers)
            {
                Require(member != null,
                    "Un membro SDK mantenuto per compatibilità è stato rimosso.");
                DdfEditorDeprecatedApiAttribute lifecycle =
                    member.GetCustomAttribute<DdfEditorDeprecatedApiAttribute>();
                Require(lifecycle != null && lifecycleIds.Add(lifecycle.ApiId),
                    "Un membro sostituito non dichiara lifecycle univoco.");
                DdfEditorApiLifecycleEntry entry =
                    DdfEditorApiLifecycleCatalog.Find(lifecycle.ApiId);
                Require(entry != null &&
                        entry.DeprecatedInContractVersion ==
                        lifecycle.DeprecatedInContractVersion &&
                        entry.EarliestRemovalContractVersion ==
                        lifecycle.EarliestRemovalContractVersion &&
                        entry.ReplacementApiId == lifecycle.ReplacementApiId &&
                        !string.IsNullOrWhiteSpace(entry.MigrationGuide),
                    "Attributo e catalogo lifecycle SDK non coincidono.");
            }
            Require(lifecycleIds.SetEquals(
                    DdfEditorApiLifecycleCatalog.Entries.Select(item => item.ApiId)) &&
                    DdfEditorApiLifecycleCatalog.PolicyVersion == "1.0" &&
                    DdfEditorApiLifecycleCatalog.EarliestBreakingContractVersion ==
                    "2.0" &&
                    DdfEditorApiLifecycleCatalog.MinimumDocumentedSuccessorContracts >= 2,
                "Il catalogo lifecycle contiene API non verificate o una finestra insufficiente.");

            Console.WriteLine(
                "PASS capability SDK additive e lifecycle 1.0: discovery, DDFH006/DDFH007, host legacy e migrazioni fino a 2.0");
        }

        private static void AssertSdkFailureRecovery()
        {
            var preferences = new DdfEditorPersistedPreferences(
                new DdfEditorHostOptions(),
                Array.Empty<KeyValuePair<string, Keys>>(), false);
            var rejectedState = new DdfEditorPersistedState("99.0", new[]
            {
                new DdfEditorPersistedDocument("future.ddf", string.Empty,
                    "main() out int { ret 99; }", true)
            }, 0, preferences);
            var validState = new DdfEditorPersistedState(new[]
            {
                new DdfEditorPersistedDocument("recovered.ddf", string.Empty,
                    "main() out int { ret 42; }", true)
            }, 0, preferences);

            using (var view = new DdfEditorView())
            {
                var documents = new OpenDocumentCollection();
                var provider = new DdfInMemoryEditorPersistenceProvider();
                view.AttachDocumentService(documents);
                view.AttachPersistenceProvider(provider);
                provider.Save(rejectedState);

                bool rejected = false;
                try { view.RestoreState(); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && documents.OpenDocuments.Count == 0 &&
                        ReferenceEquals(provider.State, rejectedState),
                    "Uno snapshot non migrabile ha lasciato stato parziale nell'host SDK.");

                provider.Save(validState);
                Require(view.RestoreState() &&
                        documents.OpenDocuments.Count == 1 &&
                        documents.ActiveDocumentInfo.DisplayName == "recovered.ddf" &&
                        view.AnalyzeDocument().Diagnostics.Count == 0,
                    "L'host SDK non è riutilizzabile dopo il rifiuto di uno snapshot.");
            }

            var executionDocuments = new OpenDocumentCollection();
            executionDocuments.CreateVirtual("main.ddf",
                "main() out int { ret 42; }");
            var language = new DdfLanguagePackageSession();
            var recoveredExecution = new DdfEditorExecutionService(
                executionDocuments, language);
            try
            {
                using (var view = new DdfEditorView())
                {
                    view.AttachDocumentService(executionDocuments);
                    view.AttachLanguageSession(language);
                    view.AttachExecutionService(new LegacyExecutionService());
                    var capabilities = (IDdfEditorCapabilityProvider)view;
                    Require(!capabilities.InspectCapability(
                            DdfEditorCapabilityIds.ScriptStatePersistence).IsAvailable,
                        "La revoca della capability state persistence non è osservabile.");

                    bool revoked = false;
                    try { view.SaveScriptState(); }
                    catch (DdfEditorCapabilityUnavailableException exception)
                    {
                        revoked = exception.Code ==
                            DdfEditorCapabilityCatalog.UnavailableDiagnosticCode;
                    }
                    Require(revoked,
                        "La capability revocata non produce il fallimento tipizzato atteso.");

                    view.AttachExecutionService(recoveredExecution);
                    var stateProvider =
                        new DdfInMemoryEditorScriptStatePersistenceProvider();
                    view.AttachScriptStatePersistenceProvider(stateProvider);
                    view.SaveScriptState();
                    Task<DdfEditorExecutionCompletion> run =
                        view.ExecutionService.RunAsync();
                    PumpUntil(() => run.IsCompleted, 5000,
                        "L'esecuzione non termina dopo il ripristino della capability.");
                    Require(capabilities.InspectCapability(
                                DdfEditorCapabilityIds.ScriptStatePersistence)
                            .IsAvailable &&
                            stateProvider.Load() != null &&
                            run.Result.Succeeded &&
                            Convert.ToInt64(run.Result.ExecutionResult.ReturnValue) == 42L,
                        "L'host SDK non è riutilizzabile dopo la revoca di una capability.");
                }
            }
            finally
            {
                recoveredExecution.Dispose();
            }

            Console.WriteLine(
                "PASS recupero SDK atomico dopo snapshot non migrabile e capability revocata");
        }

        private static void AssertUiHeartbeat(Control owner, string message)
        {
            bool heartbeat = false;
            owner.BeginInvoke(new Action(() => heartbeat = true));
            PumpUntil(() => heartbeat, 1000, message);
        }

        private static void AssertEmbeddableHostDemo(bool visible)
        {
            using (var form = new DdfHostDemoForm())
            {
                form.ShowInTaskbar = visible;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = visible ? new Point(60, 60) : new Point(-32000, -32000);
                form.Show();
                PumpMessages(120);

                Require(!ReferenceEquals(form.LeftEditor, form.RightEditor) &&
                        !ReferenceEquals(form.LeftDocuments, form.RightDocuments) &&
                        !ReferenceEquals(form.LeftEditor.HostConfiguration,
                            form.RightEditor.HostConfiguration) &&
                        !ReferenceEquals(form.LeftEditor.LanguageSession,
                            form.RightEditor.LanguageSession) &&
                        !ReferenceEquals(form.LeftEditor.DebugService,
                            form.RightEditor.DebugService),
                    "La demo host condivide servizi o stato tra le due istanze incorporate.");
                Require(form.LeftDocuments.OpenDocuments.Count == 2 &&
                        form.RightDocuments.OpenDocuments.Count == 1 &&
                        form.LeftEditor.HostConfiguration.Options.Theme == EditorTheme.Dark &&
                        form.RightEditor.HostConfiguration.Options.Theme == EditorTheme.Light &&
                        form.LeftEditor.HostCatalog.Snapshot.FunctionLibraries.Any(item =>
                            item.Id == "demo.algorithms" && item.Functions.Count == 3) &&
                        form.RightEditor.HostCatalog.Snapshot.FunctionLibraries.Any(item =>
                            item.Id == "demo.algorithms" && item.Functions.Count == 3) &&
                        !form.LeftEditor.ExperienceState.EducationalModeEnabled &&
                        form.RightEditor.ExperienceState.EducationalModeEnabled,
                    "La demo host non applica documenti e profili distinti alle due istanze.");

                Task<DdfEditorExecutionCompletion> leftExecution =
                    form.LeftEditor.ExecutionService.RunAsync();
                Task<DdfEditorExecutionCompletion> rightExecution =
                    form.RightEditor.ExecutionService.RunAsync();
                PumpUntil(() => leftExecution.IsCompleted && rightExecution.IsCompleted,
                    5000, "Le sessioni di esecuzione della demo non terminano.");
                PumpMessages(80);
                Require(leftExecution.Result.Succeeded && rightExecution.Result.Succeeded &&
                        Convert.ToInt64(leftExecution.Result.ExecutionResult.ReturnValue) == 42L &&
                        Convert.ToInt64(rightExecution.Result.ExecutionResult.ReturnValue) == 5L &&
                        leftExecution.Result.HostValues.Single(item =>
                            item.Name == "hostCounter").WasWritten &&
                        Convert.ToInt32(leftExecution.Result.HostValues.Single(item =>
                            item.Name == "hostCounter").Value) == 52 &&
                        rightExecution.Result.HostValues.Single(item =>
                            item.Name == "hostCounter").WasWritten &&
                        Convert.ToInt32(rightExecution.Result.HostValues.Single(item =>
                            item.Name == "hostCounter").Value) == 105 &&
                        leftExecution.Result.HostValues.Single(item =>
                            item.Name == "hostContext").Access == DdfHostValueAccess.ReadOnly &&
                        form.LeftOutput.Text.Contains("42", StringComparison.Ordinal) &&
                        form.LeftOutput.Text.Contains("Risultati host:", StringComparison.Ordinal) &&
                        form.RightOutput.Text.Contains("5", StringComparison.Ordinal) &&
                        !ReferenceEquals(form.LeftEditor.ExecutionService,
                            form.RightEditor.ExecutionService),
                    "La demo host non esegue e visualizza in modo isolato i due programmi virtuali.");

                Task<DdfEditorExecutionCompletion> manualProcedure =
                    form.LeftEditor.ExecutionService.InvokeAsync(
                        new DdfEditorProcedureRequest("calculateHost",
                            new Dictionary<string, object>
                            {
                                ["value"] = 6,
                                ["factor"] = 7
                            }));
                PumpUntil(() => manualProcedure.IsCompleted, 5000,
                    "La procedura manuale della demo non termina.");
                Task<DdfEditorExecutionCompletion> eventProcedure =
                    form.RightEditor.ExecutionService.InvokeAsync(
                        new DdfEditorProcedureRequest("calculateHost",
                            new Dictionary<string, object>
                            {
                                ["value"] = 8,
                                ["factor"] = 5
                            }, DdfEditorProcedureTrigger.HostEvent,
                            "smoke.rule.requested"));
                PumpUntil(() => eventProcedure.IsCompleted, 5000,
                    "La procedura attivata da evento della demo non termina.");
                PumpMessages(80);
                Require(manualProcedure.Result.Succeeded &&
                        Convert.ToInt64(manualProcedure.Result.ExecutionResult.ReturnValue) == 42L &&
                        manualProcedure.Result.ExecutionResult.ReturnTypeName == "int" &&
                        manualProcedure.Result.ProcedureInvocation.Trigger ==
                            DdfEditorProcedureTrigger.Manual &&
                        eventProcedure.Result.Succeeded &&
                        Convert.ToInt64(eventProcedure.Result.ExecutionResult.ReturnValue) == 40L &&
                        eventProcedure.Result.ProcedureInvocation.TriggerId ==
                            "smoke.rule.requested" &&
                        form.LeftOutput.Text.Contains("Procedura: calculateHost", StringComparison.Ordinal) &&
                        form.RightOutput.Text.Contains("smoke.rule.requested", StringComparison.Ordinal),
                    "La demo host non espone procedure manuali ed eventi tipizzati isolati.");

                string leftMain = form.LeftEditor.SourceText;
                form.LeftEditor.SetSelection(leftMain.IndexOf("helper", StringComparison.Ordinal), 0);
                Require(form.LeftEditor.TryGoToDefinition() &&
                        form.LeftDocuments.ActiveDocumentInfo.DisplayName == "helpers.ddf",
                    "La demo host non naviga tra i propri buffer virtuali.");
                form.SaveLeftProfile();
                OpenDocumentCollection originalLeftDocuments = form.LeftDocuments;
                form.LeftEditor.SourceText = "helper() out int { ret 99; }";
                form.RestoreLeftProfile();
                Require(!ReferenceEquals(originalLeftDocuments, form.LeftDocuments) &&
                        form.LeftDocuments.OpenDocuments.Count == 2 &&
                        form.LeftDocuments.ActiveDocumentInfo.DisplayName == "helpers.ddf" &&
                        form.LeftEditor.SourceText.Contains("value + 22", StringComparison.Ordinal),
                    "La demo host non ripristina il profilo tecnico in un nuovo servizio documentale.");

                form.SaveRightProfile();
                form.RightEditor.ExperienceState.SetEducationalMode(false);
                form.RestoreRightProfile();
                Require(form.RightEditor.ExperienceState.EducationalModeEnabled &&
                        form.RightDocuments.OpenDocuments.Count == 1 &&
                        form.RightEditor.SourceText.Contains("numero", StringComparison.Ordinal) &&
                        form.LeftDocuments.OpenDocuments.Count == 2,
                    "Il ripristino del profilo didattico modifica l'altra istanza o perde il proprio stato.");
                Require(form.LeftEditor.AnalyzeDocument().Symbols.Any(item => item.Name == "helper") &&
                        form.RightEditor.AnalyzeDocument().Symbols.Any(item => item.Name == "main"),
                    "Le due istanze della demo non analizzano indipendentemente i propri buffer.");
                form.SaveLeftProject();
                OpenDocumentCollection leftBeforeProjectOpen = form.LeftDocuments;
                form.LeftEditor.SourceText = "helper(int value) out int { ret 100; }";
                form.OpenLeftProject();
                Require(!ReferenceEquals(leftBeforeProjectOpen, form.LeftDocuments) &&
                        form.LeftDocuments.OpenDocuments.Count == 2 &&
                        form.LeftEditor.SourceText.Contains("value + 22", StringComparison.Ordinal) &&
                        form.LeftEditor.CurrentProject.ProjectId == "technical-demo",
                    "I pulsanti Salva/Apri progetto della demo non ricostruiscono il progetto tecnico.");
                form.SaveRightProject();
                OpenDocumentCollection rightBeforeProjectOpen = form.RightDocuments;
                form.RightEditor.SourceText = "main() out int { ret 99; }";
                form.OpenRightProject();
                Require(!ReferenceEquals(rightBeforeProjectOpen, form.RightDocuments) &&
                        form.RightDocuments.OpenDocuments.Count == 1 &&
                        form.RightEditor.SourceText.Contains("numero", StringComparison.Ordinal) &&
                        form.RightEditor.CurrentProject.ProjectId == "educational-demo",
                    "I pulsanti Salva/Apri progetto della demo non ricostruiscono il progetto didattico.");
                Console.WriteLine("PASS applicazione host dimostrativa con due istanze, profili, progetti e persistenza isolati");
            }
            PumpMessages(80);
        }

        private static void AssertMinimalHostDemo(bool visible)
        {
            using (var form = new MinimalHostForm())
            {
                form.ShowInTaskbar = visible;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = visible ? new Point(120, 120) :
                    new Point(-32000, -32000);
                form.Show();
                PumpMessages(160);

                DdfEditorAnalysisSnapshot analysis = form.Editor.AnalyzeDocument();
                Require(form.EditorHost.ContractVersion ==
                            DdfEditorSdkContract.CurrentVersion &&
                        form.Editor.DocumentService.OpenDocuments.Count == 1 &&
                        form.Editor.DocumentService.ActiveDocumentInfo.Id ==
                            form.ScriptDocumentId &&
                        analysis.Diagnostics.Count == 0 &&
                        form.Editor is IDdfEditorCapabilityProvider &&
                        form.Editor.CommandService.Find(
                            DdfEditorCommandIds.PrepareScript)?.IsVisible == false &&
                        form.Editor.CommandService.Find(
                            DdfEditorCommandIds.RunWithoutDebug)?.IsVisible == false,
                    "MinimalHost non rappresenta più l'integrazione embedded minima compatibile.");

                const string savedMarker = "// salvato dal minimal host";
                form.Editor.SourceText += Environment.NewLine + savedMarker;
                Require(form.EditorHost.SaveActiveDocument() &&
                        form.SavedScript.Contains(savedMarker,
                            StringComparison.Ordinal),
                    "MinimalHost non conserva lo script tramite il callback dell'host.");

                form.Editor.SourceText = form.Editor.SourceText.Replace(
                    "ret value;", "ret value + 1;");
                Require(form.ExecuteLinkedScript(),
                    "MinimalHost non avvia lo script dal controllo applicativo.");
                PumpUntil(() => !form.EditorHost.IsExecutionRunning &&
                        form.EditorHost.LastExecutionResult != null, 5000,
                    "L'esecuzione di MinimalHost non termina.");
                Require(form.EditorHost.LastExecutionResult.Succeeded &&
                        Convert.ToInt64(
                            form.EditorHost.LastExecutionResult.ReturnValue) == 43L &&
                        form.StatusLabel.Text.Contains("43",
                            StringComparison.Ordinal),
                    "MinimalHost non consegna risultato e stato all'applicazione host.");
            }
            PumpMessages(80);
            Console.WriteLine(
                "PASS MinimalHost: un documento, persistenza delegata, esecuzione da evento e capability additive");
        }

        private static void AssertDatabaseIntegrationDemo(bool visible)
        {
            string layoutDirectory = Path.Combine(Path.GetTempPath(),
                "ddf-database-demo-" + Guid.NewGuid().ToString("N"));
            string layoutPath = Path.Combine(layoutDirectory, "layout.json");
            DdfLanguagePackageLoadResult alternateGlobalLanguage =
                DdfLanguageCatalog.TryActivate(Path.Combine(
                    AppContext.BaseDirectory, "language-package-seeds",
                    "ddf-it-didactic"));
            Require(alternateGlobalLanguage.Success,
                "Lo smoke non riesce a predisporre il profilo globale alternativo.");
            try
            {
                var form = new DatabaseDemoForm();
                form.LayoutPersistencePath = layoutPath;
                form.ShowInTaskbar = visible;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = visible ? new Point(90, 90) :
                    new Point(-32000, -32000);
                form.Show();
                PumpMessages(250);
                DdfEditorAnalysisSnapshot analysis = form.Editor.AnalyzeDocument();
                RichTextBox embeddedEditor = FindControl<RichTextBox>(form,
                    "richTextBoxMainEditor");
                int returnKeyword = embeddedEditor.Text.IndexOf("ret",
                    StringComparison.Ordinal);
                int stringKeyword = embeddedEditor.Text.IndexOf("string",
                    StringComparison.Ordinal);
                int mainFunction = embeddedEditor.Text.IndexOf("main(",
                    StringComparison.Ordinal);
                embeddedEditor.Select(returnKeyword, "ret".Length);
                Color returnColor = embeddedEditor.SelectionColor;
                embeddedEditor.Select(stringKeyword, "string".Length);
                Color stringColor = embeddedEditor.SelectionColor;
                embeddedEditor.Select(mainFunction, "main".Length);
                Color functionColor = embeddedEditor.SelectionColor;
                Require(returnKeyword >= 0 && stringKeyword >= 0 &&
                        mainFunction >= 0 &&
                        returnColor != embeddedEditor.ForeColor &&
                        stringColor != embeddedEditor.ForeColor &&
                        returnColor != stringColor &&
                        functionColor == Color.FromArgb(220, 220, 170),
                    "La DatabaseDemo ha perso la colorazione sintattica embedded: " +
                    "ret=" + returnColor + ", string=" + stringColor +
                    ", function=" + functionColor +
                    ", default=" + embeddedEditor.ForeColor + ".");
                Require(analysis.Diagnostics.Count == 0 &&
                        form.Editor.LanguageSession.ActivePackage.Id == "ddf" &&
                        form.Editor.HostCatalog.Snapshot.Databases.Count == 1 &&
                        form.Editor.HostCatalog.Snapshot.Objects.Any(item =>
                            item.Name == "firstNameTextBox" && item.TypeName == "HostTextBox") &&
                        form.EditorHost.ContractVersion == DdfEditorSdkContract.CurrentVersion &&
                        form.Editor.DocumentService.OpenDocuments.Any(document =>
                            document.DisplayName == "customer-actions.ddf") &&
                        form.Editor.DocumentService.OpenDocuments.Any(document =>
                            document.DisplayName == "API host - documentazione.ddf") &&
                        !FindControl<ListBox>(form, "listBoxDiagnostics").Items
                            .Cast<object>().Any(item => item.ToString().Contains("DDF201")),
                    "La demo database non incorpora la shell completa con il catalogo host. Diagnostiche: " +
                    string.Join(" | ", analysis.Diagnostics.Select(item => item.Code + ": " + item.Message)));
                Require(form.Editor.CommandService.Find(DdfEditorCommandIds.PrepareScript)?.IsVisible == false &&
                        form.Editor.CommandService.Find(DdfEditorCommandIds.StartDebugging)?.IsVisible == false &&
                        form.Editor.CommandService.Find(DdfEditorCommandIds.RunWithoutDebug)?.IsVisible == false,
                    "La modalità embedded espone ancora compilazione o avvio manuale.");
                TreeView hostApiTree = FindControl<TreeView>(form, "hostApiTreeView");
                Require(hostApiTree.Nodes.Count == 1 &&
                        hostApiTree.Nodes[0].Text.Contains("Database Application Demo") &&
                        FlattenTree(hostApiTree.Nodes).Any(node =>
                            node.Text.StartsWith("firstNameTextBox : HostTextBox")) &&
                        FlattenTree(hostApiTree.Nodes).Any(node =>
                            node.Text.StartsWith("backColor : string")) &&
                        FlattenTree(hostApiTree.Nodes).Any(node => node.Text.StartsWith("click")),
                    "La palette API host non descrive controlli, proprietà ed eventi esposti.");
                const string memberSource =
                    "main() out int { firstNameTextBox.ba ret 0; }";
                int memberPosition = memberSource.IndexOf("ba", StringComparison.Ordinal) + 2;
                DdfCompletionResult memberCompletion = DdfCompletionService.GetCompletions(
                    memberSource, memberPosition, language: DdfLanguageCatalog.Default,
                    hostCatalog: form.Editor.HostCatalog.Snapshot);
                Require(memberCompletion.Items.Any(item => item.DisplayText == "backColor" &&
                            item.Kind == DdfCompletionKind.Field),
                    "IntelliSense non propone le proprietà dell'oggetto host dopo il punto.");

                form.Editor.SourceText = form.Editor.SourceText.Replace(
                    "    ret insertedId;",
                    "    firstNameTextBox.backColor << \"LightGreen\";\n    ret insertedId;");

                form.FirstNameTextBox.Text = "Ada";
                form.LastNameTextBox.Text = "Lovelace";
                form.CustomerCheckBox.Checked = true;
                form.ExecuteButton.PerformClick();
                PumpUntil(() => form.Database.Customers.Count == 1 &&
                    !form.EditorHost.IsExecutionRunning &&
                    form.ExecuteButton.Enabled, 5000,
                    "Lo script collegato al pulsante non termina.");
                PumpMessages(100);
                DemoCustomer inserted = form.Database.Customers.Single();
                Require(form.EditorHost.LastExecutionResult?.Succeeded == true &&
                        inserted.FirstName == "Ada" && inserted.LastName == "Lovelace" &&
                        inserted.IsCustomer && form.FirstNameTextBox.BackColor == Color.LightGreen,
                    "Lo script non trasferisce i controlli della form nel database virtuale.");

                form.FirstNameTextBox.Text = "Grace";
                form.LastNameTextBox.Text = "Hopper";
                form.CustomerCheckBox.Checked = false;
                form.EditorHost.ToggleBreakpoint(7);
                Require(form.ExecuteLinkedScript(true),
                    "Il debug della shell incorporata non parte.");
                PumpUntil(() => form.EditorHost.IsDebugPaused, 5000,
                    "Il debugger incorporato non raggiunge il breakpoint nel gutter. Stato=" +
                    form.Editor.DebugService.State + ", breakpoint=" +
                    string.Join(",", form.Editor.DebugService.Breakpoints.Select(item =>
                        item.DocumentId + ":" + item.Line + ":" + item.Enabled)) +
                    ", esito=" + (form.Editor.DebugService.LastCompletion?.InternalError ?? "—") +
                    ", build=" + string.Join(" | ", form.Editor.DebugService.LastCompletion?.BuildIssues
                        .Select(item => item.ToString()) ?? Array.Empty<string>()));
                Require(form.EditorHost.ContinueDebug(),
                    "Il debugger incorporato non riprende dal breakpoint.");
                PumpUntil(() => !form.EditorHost.IsExecutionRunning &&
                    form.ExecuteButton.Enabled, 5000,
                    "Il debug della shell incorporata non termina.");
                PumpMessages(100);
                Require(UiExceptions.Count == 0,
                    "Eccezione UI dopo il debug incorporato: " +
                    string.Join(" | ", UiExceptions.Select(item => item.Message)));
                Require(form.Database.Customers.Count == 2 &&
                        form.Database.Customers.Last().FirstName == "Grace" &&
                        !form.Database.Customers.Last().IsCustomer,
                    "Il debug non conserva il collegamento tra form, script e database.");

                form.EditorHost.ToggleBreakpoint(7);
                DataGridViewRow graceRow = form.CustomerGrid.Rows.Cast<DataGridViewRow>()
                    .FirstOrDefault(row => Convert.ToInt32(row.Cells["Id"].Value) == 2);
                Require(graceRow != null,
                    "La griglia non si aggiorna dopo il debug. Righe=" +
                    form.CustomerGrid.Rows.Count + ", pulsante=" + form.ExecuteButton.Enabled +
                    ", stato='" + form.StatusLabel.Text + "', risultato='" +
                    form.ResultLabel.Text + "'.");
                form.CustomerGrid.CurrentCell = graceRow.Cells[0];
                graceRow.Selected = true;
                form.CountLettersButton.PerformClick();
                Require(form.EditorHost.IsExecutionRunning,
                    "Il pulsante Conta lettere non avvia la procedura: " +
                    form.StatusLabel.Text);
                PumpUntil(() => !form.EditorHost.IsExecutionRunning &&
                    form.ResultLabel.Text.Contains("6 lettere"), 5000,
                    "La funzione DDF di conteggio lettere non restituisce il risultato alla form. " +
                    "Risultato='" + form.ResultLabel.Text + "', esito=" +
                    form.EditorHost.LastExecutionResult?.Succeeded +
                    ", valore=" + form.EditorHost.LastExecutionResult?.ReturnValue +
                    ", output='" + FindControl<RichTextBox>(form,
                        "richTextBoxOutput").Text.Replace(Environment.NewLine, " | ") + "'.");
                form.RemoveVowelsButton.PerformClick();
                PumpUntil(() => !form.EditorHost.IsExecutionRunning &&
                    form.RemoveVowelsButton.Enabled &&
                    form.Database.Customers.Last().LastName == "Hppr", 5000,
                    "La funzione DDF di modifica non aggiorna la riga selezionata.");
                form.FirstNameTextBox.Clear();
                form.LastNameTextBox.Clear();
                form.CustomerCheckBox.Checked = true;
                form.ExtractRowButton.PerformClick();
                PumpUntil(() => !form.EditorHost.IsExecutionRunning &&
                    form.FirstNameTextBox.Text == "Grace" &&
                    form.LastNameTextBox.Text == "Hppr" &&
                    !form.CustomerCheckBox.Checked, 5000,
                    "La funzione DDF di estrazione non riporta la riga nei controlli host. " +
                    "Stato='" + form.StatusLabel.Text + "', tipo=" +
                    form.EditorHost.LastExecutionResult?.ReturnValue?.GetType().FullName +
                    ", valore='" + form.EditorHost.LastExecutionResult?.ReturnValue + "'.");
                form.ApplicationEditorSplit.SplitterDistance = 280;
                ToolStripPanel embeddedToolbarPanel = FindControl<ToolStripPanel>(
                    form, "toolStripPanelMain");
                ToolStrip navigationToolbar = FindControl<ToolStrip>(form,
                    "toolStripNavigation");
                ToolStrip viewToolbar = FindControl<ToolStrip>(form,
                    "toolStripView");
                ToolStrip fileToolbar = FindControl<ToolStrip>(form,
                    "toolStripMain");
                embeddedToolbarPanel.Join(navigationToolbar, new Point(0,
                    fileToolbar.Top + Math.Max(1, fileToolbar.Height / 2)));
                viewToolbar.Visible = false;
                embeddedToolbarPanel.PerformLayout();
                PumpMessages(80);
                string expectedToolbarLayout =
                    form.EditorHost.CaptureToolbarLayout();
                const string persistedMarker = "// script personalizzato persistente";
                form.Editor.SourceText += Environment.NewLine + persistedMarker;
                Require(form.EditorHost.SaveActiveDocument(),
                    "Il controllo embedded non inoltra il salvataggio all'host.");
                Require(File.Exists(layoutPath) &&
                        File.ReadAllText(layoutPath).Contains(persistedMarker),
                    "Salva non delega lo script all'archivio dell'applicazione host.");
                var restored = new DatabaseDemoForm();
                restored.LayoutPersistencePath = layoutPath;
                restored.ShowInTaskbar = false;
                restored.StartPosition = FormStartPosition.Manual;
                restored.Location = new Point(-32000, -32000);
                restored.Show();
                PumpMessages(160);
                Require(restored.ApplicationEditorSplit.SplitterDistance == 280 &&
                        restored.Editor.SourceText.Contains("// script personalizzato persistente") &&
                        restored.EditorHost.CaptureToolbarLayout() ==
                            expectedToolbarLayout &&
                        !FindControl<ToolStrip>(restored,
                            "toolStripView").Visible,
                    "Dimensione della palette, script o disposizione delle toolbar " +
                    "non vengono ripristinati tra gli avvii.");
                restored.Close();
                PumpMessages(80);
                form.Close();
                PumpMessages(80);
            }
            finally
            {
                DdfLanguageCatalog.ActivateEmbedded();
                if (Directory.Exists(layoutDirectory))
                    Directory.Delete(layoutDirectory, true);
            }
            Console.WriteLine("PASS demo applicativa con form, shell IDE completa, database virtuale e debug");
        }

        private static void AssertSdkInstanceIsolation()
        {
            var leftDocuments = new OpenDocumentCollection();
            var rightDocuments = new OpenDocumentCollection();
            IDdfEditorDocument leftDocument = leftDocuments.CreateVirtual(
                "left.ddf", "main() out int { ret 11; }");
            rightDocuments.CreateVirtual(
                "right.ddf", "main() out int { ret 29; }");
            var leftPalettes = new DdfEditorPaletteCatalog();
            var rightPalettes = new DdfEditorPaletteCatalog();
            var leftPersistence = new CountingPersistenceProvider();
            var rightPersistence = new CountingPersistenceProvider();
            var leftProjects = new CountingProjectContainer();
            var rightProjects = new CountingProjectContainer();

            using (var left = new DdfEditorView())
            using (var right = new DdfEditorView())
            {
                left.AttachDocumentService(leftDocuments);
                right.AttachDocumentService(rightDocuments);
                left.AttachPaletteService(leftPalettes);
                right.AttachPaletteService(rightPalettes);
                left.AttachPersistenceProvider(leftPersistence);
                right.AttachPersistenceProvider(rightPersistence);
                left.AttachProjectContainer(leftProjects);
                right.AttachProjectContainer(rightProjects);

                Require(!ReferenceEquals(left.DocumentService, right.DocumentService) &&
                        !ReferenceEquals(left.PaletteService, right.PaletteService) &&
                        !ReferenceEquals(left.HostConfiguration, right.HostConfiguration) &&
                        !ReferenceEquals(left.LanguageSession, right.LanguageSession) &&
                        !ReferenceEquals(left.HostCatalog, right.HostCatalog) &&
                        !ReferenceEquals(left.ExperienceState, right.ExperienceState) &&
                        !ReferenceEquals(left.AnalysisService, right.AnalysisService) &&
                        !ReferenceEquals(left.PreparationService, right.PreparationService) &&
                        !ReferenceEquals(left.CommandService, right.CommandService) &&
                        !ReferenceEquals(left.ExecutionService, right.ExecutionService) &&
                        !ReferenceEquals(left.DebugService, right.DebugService) &&
                        !ReferenceEquals(left.PersistenceProvider, right.PersistenceProvider) &&
                        !ReferenceEquals(left.ProjectContainer, right.ProjectContainer),
                    "Due superfici SDK condividono almeno un servizio mutabile per istanza.");

                int leftAnalysisEvents = 0;
                int rightAnalysisEvents = 0;
                int leftBuildEvents = 0;
                int rightBuildEvents = 0;
                int leftSelectionEvents = 0;
                int rightSelectionEvents = 0;
                int leftCommandExecutions = 0;
                left.AnalysisCompleted += (sender, args) => leftAnalysisEvents++;
                right.AnalysisCompleted += (sender, args) => rightAnalysisEvents++;
                left.PreparationCompleted += (sender, args) => leftBuildEvents++;
                right.PreparationCompleted += (sender, args) => rightBuildEvents++;
                left.SelectionSnapshotChanged += (sender, args) => leftSelectionEvents++;
                right.SelectionSnapshotChanged += (sender, args) => rightSelectionEvents++;

                left.HostConfiguration.ApplyOptions(new DdfEditorHostOptions(
                    "Consolas", 11F, 110, EditorTheme.Light, true, false, 2));
                left.ExperienceState.SetEducationalMode(true);
                left.SetSelection(0, 4);
                leftPalettes.Register("left-only", "Solo sinistra",
                    DdfEditorPalettePlacement.Sidebar);
                left.CommandService.Register("host.left-only", "Solo sinistra", "Host",
                    () => leftCommandExecutions++);
                left.DebugService.SetBreakpoints(new[]
                {
                    new DdfEditorBreakpoint(leftDocument.Id, 1)
                });
                left.DebugService.SetWatchExpressions(new[] { "value" });
                var leftHostCatalog = (DdfEditorHostCatalog)left.HostCatalog;
                leftHostCatalog.RegisterPermission(new DdfHostPermissionDefinition(
                    "demo.read", "Lettura demo"));
                leftHostCatalog.RegisterObject(new DdfHostObjectDefinition(
                    "hostValue", "int", () => 5, "Valore della sola istanza sinistra."));

                Require(left.HostConfiguration.SetShortcut(
                            DdfEditorCommandIds.SaveHostState, Keys.Control | Keys.Shift | Keys.S) &&
                        right.HostConfiguration.SetShortcut(
                            DdfEditorCommandIds.SaveHostState, Keys.Control | Keys.Alt | Keys.S) &&
                        !left.HostConfiguration.SetShortcut(
                            DdfEditorCommandIds.RestoreHostState, Keys.Control | Keys.Shift | Keys.S) &&
                        left.CommandService.Find(DdfEditorCommandIds.SaveHostState).ShortcutText ==
                            ShortcutSettings.Format(Keys.Control | Keys.Shift | Keys.S) &&
                        right.CommandService.Find(DdfEditorCommandIds.SaveHostState).ShortcutText ==
                            ShortcutSettings.Format(Keys.Control | Keys.Alt | Keys.S),
                    "Le scorciatoie host non restano isolate o non aggiornano i metadati dei comandi.");
                Require(left.WordWrap && !right.WordWrap &&
                        left.ExperienceState.EducationalModeEnabled &&
                        !right.ExperienceState.EducationalModeEnabled &&
                        leftPalettes.Find("left-only") != null &&
                        rightPalettes.Find("left-only") == null &&
                        left.CommandService.Find("host.left-only") != null &&
                        right.CommandService.Find("host.left-only") == null &&
                        left.CommandService.TryExecute("host.left-only") &&
                        leftCommandExecutions == 1 &&
                        left.DebugService.Breakpoints.Count == 1 &&
                        right.DebugService.Breakpoints.Count == 0 &&
                        left.DebugService.WatchExpressions.Count == 1 &&
                        right.DebugService.WatchExpressions.Count == 0 &&
                        left.HostCatalog.Snapshot.Objects.Any(item =>
                            item.Name == "hostValue") &&
                        right.HostCatalog.Snapshot.Objects.Count == 0,
                    "Preferenze, palette, comandi o stato debug contaminano un'altra istanza.");

                left.AnalyzeDocument();
                DdfEditorPreparationSnapshot leftBuild = left.PrepareScript();
                Require(leftAnalysisEvents == 1 && rightAnalysisEvents == 0 &&
                        leftBuildEvents == 1 && rightBuildEvents == 0 &&
                        leftSelectionEvents > 0 && rightSelectionEvents == 0 &&
                        leftBuild.Succeeded && right.PreparationService.LastSnapshot == null,
                    "Eventi o snapshot di analisi/build attraversano il confine di istanza.");

                right.AnalyzeDocument();
                DdfEditorPreparationSnapshot rightBuild = right.PrepareScript();
                Require(rightAnalysisEvents == 1 && rightBuildEvents == 1 &&
                        rightBuild.Succeeded &&
                        !string.Equals(leftBuild.PreparedArtifact.Fingerprint,
                            rightBuild.PreparedArtifact.Fingerprint, StringComparison.Ordinal) &&
                        ReferenceEquals(left.PreparationService.LastSnapshot, leftBuild) &&
                        ReferenceEquals(right.PreparationService.LastSnapshot, rightBuild),
                    "Le pipeline di build non mantengono snapshot indipendenti per superficie.");

                Task<DdfEditorExecutionCompletion> leftRun = left.ExecutionService.RunAsync();
                Task<DdfEditorExecutionCompletion> rightRun = right.ExecutionService.RunAsync();
                PumpUntil(() => leftRun.IsCompleted && rightRun.IsCompleted, 5000,
                    "Le esecuzioni parallele delle due istanze non terminano.");
                Require(leftRun.Result.Succeeded && rightRun.Result.Succeeded &&
                        Convert.ToInt64(leftRun.Result.ExecutionResult.ReturnValue) == 11L &&
                        Convert.ToInt64(rightRun.Result.ExecutionResult.ReturnValue) == 29L,
                    "Le esecuzioni di istanze diverse contaminano documenti o risultati.");
                Require(leftPersistence.SaveCount == 0 && leftPersistence.LoadCount == 0 &&
                        rightPersistence.SaveCount == 0 && rightPersistence.LoadCount == 0 &&
                        leftProjects.SaveCount == 0 && leftProjects.LoadCount == 0 &&
                        rightProjects.SaveCount == 0 && rightProjects.LoadCount == 0,
                    "Un'operazione editor/analisi/build/run ha attivato persistenza implicita.");

                left.SaveState();
                left.SaveProject("left-project", "Progetto sinistro");
                Require(leftPersistence.SaveCount == 1 && leftProjects.SaveCount == 1 &&
                        rightPersistence.SaveCount == 0 && rightProjects.SaveCount == 0,
                    "La persistenza esplicita di una superficie ha coinvolto un altro host.");
            }

            var ownedDocuments = new OpenDocumentCollection();
            ownedDocuments.CreateVirtual("external.ddf", "main() out int { ret 7; }");
            var ownedLanguage = new DdfLanguagePackageSession();
            var externalExecution = new DdfEditorExecutionService(ownedDocuments, ownedLanguage);
            var externalDebug = new DdfEditorDebugService(ownedDocuments, ownedLanguage);
            try
            {
                using (var surface = new DdfEditorView())
                {
                    surface.AttachDocumentService(ownedDocuments);
                    surface.AttachExecutionService(externalExecution);
                    surface.AttachDebugService(externalDebug);
                }
                DdfEditorExecutionCompletion execution = externalExecution.RunAsync()
                    .GetAwaiter().GetResult();
                DdfEditorExecutionCompletion debugging = externalDebug.RunAsync()
                    .GetAwaiter().GetResult();
                Require(execution.Succeeded && debugging.Succeeded,
                    "La superficie dispone servizi iniettati che devono restare di proprietà dell'host.");
            }
            finally
            {
                externalDebug.Dispose();
                externalExecution.Dispose();
            }

            Console.WriteLine("PASS isolamento multi-istanza SDK, scorciatoie locali, eventi e persistenza esplicita");
        }

        private static void AssertStandalonePackageStudioFactory()
        {
            DdfLanguagePackage embedded = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;
            using (Form studio = DdfLanguagePackageStudio.CreateForm(embedded))
            {
                Require(FindControl<TabControl>(studio,
                            "languagePackageStudioTabs").TabPages.Count == 4 &&
                    FindControl<TabControl>(studio,
                            "languageStudioAdvancedTabs").TabPages.Count == 10,
                    "La shell autonoma non riusa le schede del Package Studio integrato.");
            }

            var identity = new DdfLanguageAuthoringIdentity("studio-smoke",
                "Studio smoke", "0.1.0", "0.13", new[] { ".ddf" });
            DdfLanguageAuthoringTemplateResult starter =
                DdfLanguageAuthoringProjectFactory.CreateBlank(identity);
            Require(starter.Success, "Lo starter di authoring non viene creato.");
            DdfLanguageAuthoringResult incomplete = DdfLanguagePackageAuthoring.Apply(
                starter.Project, new DdfLanguageAuthoringChangeSet(
                    starter.Project.Revision, new[]
                    {
                        DdfLanguageAuthoringChange.Upsert("manifest.json",
                            "{ \"schemaVersion\":")
                    }));
            Require(incomplete.Success, "Il draft incompleto non viene preparato.");
            using (Form studio = DdfLanguagePackageStudio.CreateForm(
                incomplete.Project))
            {
                Require(FindControl<TabControl>(studio,
                            "languagePackageStudioTabs").SelectedTab.Name ==
                        "languageStudioAdvancedPage" &&
                    FindControl<TabControl>(studio,
                            "languageStudioAdvancedTabs").SelectedTab.Name ==
                        "languageStudioSourcesPage" &&
                    FindControl<ListBox>(studio,
                        "languageStudioSourceFilesListBox").Items.Count > 0 &&
                    FindControl<TextBox>(studio,
                        "languageStudioDiagnosticsTextBox").Text.Contains(
                            "Correggere", StringComparison.Ordinal),
                    "La shell autonoma non apre un JSON incompleto nei sorgenti riparabili.");
            }
            Console.WriteLine("PASS Package Studio autonomo: shell condivisa e draft incompleto riparabile");
        }

        private static void RunEditorScenarios(bool visible)
        {
            using (var form = new MainForm())
            {
                DisableRecentFilePersistence(form);
                ResetEditorSettingsForSmoke(form);
                ResetShortcutSettingsForSmoke(form);
                ResetPaletteLayoutForSmoke(form);
                ResetToolbarLayoutForSmoke(form);
                form.ShowInTaskbar = visible;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = visible ? new Point(80, 80) : new Point(-32000, -32000);
                if (!visible) form.WindowState = FormWindowState.Normal;
                form.Show();
                PumpMessages(160);
                DdfLanguageCatalog.ActivateEmbedded();
                DdfLanguagePackageLoadResult baselineLanguage =
                    form.EmbeddedEditor.LanguageSession.UseEmbedded();
                Require(baselineLanguage.Success,
                    "Lo smoke non riesce a ripristinare il linguaggio DDF incorporato.");
                PumpMessages(80);

                RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                RichTextBox foldedView = FindControl<RichTextBox>(form, "richTextBoxFoldedView");
                RichTextBox lineNumbers = FindControl<RichTextBox>(form, "richTextBoxLineNumbers");
                ListBox diagnostics = FindControl<ListBox>(form, "listBoxDiagnostics");
                Panel diagnosticsPanel = FindControl<Panel>(form, "panelDiagnostics");

                Require(diagnosticsPanel.Visible, "La finestra diagnostica non è visibile all'avvio.");
                Require(form.Controls.Find("panelBeta", true).Length == 0,
                    "La barra beta è ancora presente nella finestra principale.");
                ToolStripStatusLabel languageStatus = (ToolStripStatusLabel)FindControl<StatusStrip>(form, "statusStripMain")
                    .Items["statusEncodingLabel"];
                DdfLanguagePackage activeLanguage = form.EmbeddedEditor.LanguageSession.ActivePackage;
                Require(languageStatus.Text.Contains(activeLanguage.Name + " " + activeLanguage.LanguageVersion,
                            StringComparison.Ordinal) &&
                        languageStatus.ToolTipText.Contains("Pacchetto linguaggio: " + activeLanguage.Id + " " +
                            activeLanguage.PackageVersion, StringComparison.Ordinal) &&
                        languageStatus.ToolTipText.Contains("Origine:", StringComparison.Ordinal),
                    "Versione e origine del pacchetto linguaggio attivo non sono esplicite nello stato.");

                AssertEmbeddableEditorSurface(form, editor);
                AssertWrappedGutterHasNoMissingLogicalLines(form, editor, lineNumbers);
                AssertDocumentTabsDoNotCoverSource(form, editor);
                AssertMainToolbar(form);
                AssertToolbarDpiGeometry();
                AssertUnifiedLightTheme(form, editor, foldedView, lineNumbers);
                AssertAccessibilityAndDpi(form, editor);
                AssertEditorSettings(form, editor);
                AssertProductUpdates(form);
                AssertTextBoxEditingSupport(form, editor);
                AssertGutterAndAutoHidePalettes(form, editor, lineNumbers, diagnosticsPanel);
                AssertEditorPalette(editor);
                AssertFunctionNameHighlight(form, editor);
                SetSource(editor);
                AssertFlowchartPalette(form, editor);
                SetSource(editor);
                AssertContextualBreadcrumb(form, editor);
                SetSource(editor);
                AssertCommandPalette(form, editor);
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
                try
                {
                    AssertWorkspaceNavigation(form, editor, diagnostics);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("DETTAGLIO scenario workspace/build: " +
                        exception);
                    throw;
                }
                try
                {
                    AssertProgramExecution(form, editor);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("DETTAGLIO scenario interprete/debug: " + exception);
                    throw;
                }
                // The final debugger scenario can queue a cross-document palette
                // activation. Let WinForms finish any pending handle creation
                // before the using scope disposes the entire form tree.
                PumpMessages(250);
            }
        }

        private static void AssertFlowchartGeometryOnly(bool visible)
        {
            using (var form = ShowSmokeForm(visible))
            {
                DdfLanguageCatalog.ActivateEmbedded();
                Require(form.EmbeddedEditor.LanguageSession.UseEmbedded().Success,
                    "Il gate geometrico non riesce ad attivare il linguaggio DDF incorporato.");
                RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                SetSource(editor);
                AssertFlowchartPalette(form, editor);
                PumpMessages(100);
            }
            Require(UiExceptions.Count == 0,
                "Il gate geometrico Flowchart ha intercettato eccezioni UI: " +
                string.Join(" | ", UiExceptions.Select(exception => exception.Message)));
            Console.WriteLine("PASS matrice geometrica Flowchart semplice/complessa e deterministica");
        }

        private static void AssertFlowchartPalette(MainForm form, RichTextBox editor)
        {
            PumpMessages(450);
            TabControl navigation = FindControl<TabControl>(form, "navigationTabs");
            TabPage page = FindControl<TabPage>(form, "flowchartTabPage");
            Control view = FindControl<Control>(form, "flowchartView");
            Control canvas = FindControl<Control>(form, "flowchartCanvas");
            Label status = FindControl<Label>(form, "flowchartStatusLabel");
            Require(navigation.TabPages.Contains(page), "La palette Flowchart non è disponibile nella navigazione laterale.");
            Require(ReferenceEquals(view.Parent, page) &&
                    ReferenceEquals(GetPropertyValue<DdfFlowchartLayout>(view, "Diagram"),
                        GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram")),
                "La palette non ospita il controllo Flowchart riutilizzabile o la sua API non espone il diagramma.");
            using (var isolatedView = new FlowchartView(FindControl<MenuStrip>(form, "menuStripMain").Font))
            {
                isolatedView.Size = new Size(420, 320);
                isolatedView.CreateControl();
                isolatedView.SetDiagramPreservingViewport(
                    GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram"), 0);
                isolatedView.SetStatus("FLOWCHART — controllo isolato");
                Require(isolatedView.Diagram != null && isolatedView.Controls.Find(
                        "flowchartCanvas", true).Length == 1 && isolatedView.Controls.Find(
                        "flowchartToolStrip", true).Length == 1,
                    "FlowchartView non funziona indipendentemente dalla MainForm.");
            }
            var diagram = GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram");
            Require(diagram != null && diagram.Width > 0 && diagram.Height > 0,
                "La palette Flowchart non ha applicato il layout del documento corrente.");
            Require(status.Text.Contains("main()", StringComparison.Ordinal) && status.Text.Contains("nodi", StringComparison.Ordinal),
                "La palette Flowchart non identifica funzione e stato del diagramma.");
            ToolStrip toolbar = FindControl<ToolStrip>(form, "flowchartToolStrip");
            var functions = (ToolStripDropDownButton)toolbar.Items["flowchartFunctionsButton"];
            var pointer = (ToolStripButton)toolbar.Items["flowchartPointerButton"];
            var pan = (ToolStripButton)toolbar.Items["flowchartPanButton"];
            var groups = (ToolStripButton)toolbar.Items["flowchartGroupsButton"];
            var educational = (ToolStripButton)toolbar.Items["flowchartEducationalModeButton"];
            Panel educationalPanel = FindControl<Panel>(form, "flowchartEducationalPanel");
            RichTextBox educationalExplanation = FindControl<RichTextBox>(form,
                "flowchartEducationalExplanationBox");
            var zoomOut = (ToolStripButton)toolbar.Items["flowchartZoomOutButton"];
            var zoomIn = (ToolStripButton)toolbar.Items["flowchartZoomInButton"];
            var save = (ToolStripButton)toolbar.Items["flowchartSaveButton"];
            Require(functions != null && pointer.Checked && pan != null && groups != null && educational != null &&
                    zoomOut != null && zoomIn != null && save != null,
                "La barra Flowchart non espone funzioni, puntatore, pan, gruppi, modalità didattica, zoom e salvataggio.");
            ToolStripMenuItem educationalMenu = FindMenuItem(form, "educationalModeMenuItem");
            string persistedEducationalMode = null;
            SetPrivateField(form, "saveEducationalModeSetting",
                new Action<string>(value => persistedEducationalMode = value));
            string sourceBeforeEducationalToggle = editor.Text;
            int selectionBeforeEducationalToggle = editor.SelectionStart;
            int selectionLengthBeforeEducationalToggle = editor.SelectionLength;
            bool modifiedBeforeEducationalToggle = editor.Modified;
            bool undoBeforeEducationalToggle = editor.CanUndo;
            bool debugBeforeEducationalToggle = Convert.ToBoolean(GetPrivateField<object>(form, "executionRunning"));
            navigation.SelectedTab = page;
            educationalMenu.PerformClick();
            PumpMessages(20);
            Require(educational.Checked == educationalMenu.Checked &&
                    Convert.ToBoolean(GetPrivateField<object>(form, "educationalModeEnabled")) == educationalMenu.Checked &&
                    persistedEducationalMode == educationalMenu.Checked.ToString() &&
                    status.Text.Contains("DIDATTICA", StringComparison.Ordinal) == educationalMenu.Checked,
                "Menu, controllo riutilizzabile e preferenza della modalità didattica non sono sincronizzati.");
            Require(educationalPanel.Visible == educationalMenu.Checked,
                "Il pannello di spiegazione non segue lo stato della modalità didattica.");
            Require(editor.Text == sourceBeforeEducationalToggle &&
                    editor.SelectionStart == selectionBeforeEducationalToggle &&
                    editor.SelectionLength == selectionLengthBeforeEducationalToggle &&
                    editor.Modified == modifiedBeforeEducationalToggle && editor.CanUndo == undoBeforeEducationalToggle &&
                    Convert.ToBoolean(GetPrivateField<object>(form, "executionRunning")) == debugBeforeEducationalToggle &&
                    GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram") != null,
                "Il cambio di modalità didattica altera documento, selezione, Undo, debug o diagramma ordinario.");
            educational.PerformClick();
            PumpMessages(20);
            Require(educational.Checked == educationalMenu.Checked &&
                    persistedEducationalMode == educational.Checked.ToString() &&
                    status.Text.Contains("DIDATTICA", StringComparison.Ordinal) == educational.Checked,
                "Il comando incorporabile della modalità didattica non sincronizza menu e persistenza.");
            bool originalEducationalMode = educational.Checked;
            if (!educational.Checked) educational.PerformClick();
            navigation.SelectedTab = page;
            view.GetType().GetMethod("SetSourcePosition", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(view, new object[] { diagram.Nodes.First(item =>
                    item.Node.Kind == DdfFlowchartNodeKind.Return).Node.Start });
            PumpMessages(20);
            Require(educationalPanel.Visible &&
                    educationalExplanation.Text.Contains("Ritorno", StringComparison.Ordinal) &&
                    GetControlProperty<bool>(canvas, "EducationalModeEnabled"),
                "La modalità didattica non spiega il nodo selezionato. Visibile=" +
                educationalPanel.Visible + "; testo='" + educationalExplanation.Text +
                "'; nodo=" + GetPropertyValue<DdfFlowchartNode>(canvas, "SelectedNode")?.Kind + ".");
            if (!originalEducationalMode) educational.PerformClick();
            Require(pan.Image != null && pan.Image.Width >= 20 && pan.Image.Height >= 20,
                "La modalità Pan non usa un'icona grafica a forma di mano.");
            Require(save.Image != null && save.Image.Width >= 20 && save.Image.Height >= 20,
                "L'esportazione della Flowchart non usa un'icona grafica esplicativa.");
            DdfFlowchartLayoutNode caretTarget = diagram.Nodes.First(item =>
                item.Node.Kind == DdfFlowchartNodeKind.Process);
            view.GetType().GetMethod("SetSourcePosition", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(view, new object[] { caretTarget.Node.Start });
            PumpMessages(30);
            Require(GetPropertyValue<DdfFlowchartNode>(view, "SelectedNode")?.Id == caretTarget.Node.Id,
                "L'API del controllo Flowchart non sincronizza la posizione sorgente con il nodo.");
            DdfFlowchartLayoutNode secondCaretTarget = diagram.Nodes.First(item =>
                item.Node.Id != caretTarget.Node.Id && item.Node.Kind == DdfFlowchartNodeKind.Return);
            editor.Select(secondCaretTarget.Node.Start, 0);
            PumpMessages(30);
            Require(GetPropertyValue<DdfFlowchartNode>(canvas, "SelectedNode")?.Id == secondCaretTarget.Node.Id,
                "Lo spostamento del cursore non evidenzia immediatamente il nodo Flowchart corrispondente.");
            const string routingSource =
                "main() out int { int value << 0; do { value++; if(value == 2) { value << value + 3; } " +
                "else { value << value + 1; } } while(value < 8); while(value < 20) { value++; " +
                "if(value > 15) { brk; } } for(int index << 0; index < 3; index++) { " +
                "if(index == 1) { value << value + index; } else { value++; } } ret value; }";
            DdfFlowchartLayout routingDiagram = DdfFlowchartLayouter.Layout(
                DdfFlowchartBuilder.Build(routingSource, "main",
                    form.EmbeddedEditor.LanguageSession.ActivePackage.Language).Flowchart);
            canvas.GetType().GetMethod("SetDiagram", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(canvas, new object[] { routingDiagram });
            object routeMap = routingDiagram.Nodes.ToDictionary(item => item.Node.Id,
                item => item, StringComparer.Ordinal);
            object routedResult = canvas.GetType().GetMethod("BuildRoutes",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new[] { routeMap });
            IEnumerable routed = (IEnumerable)routedResult;
            List<FlowchartRouteSnapshot> routes = ReadFlowchartRoutes(routed);
            AssertFlowchartRouteGeometry(routingDiagram, routes, "diagramma combinato");
            IEnumerable mergeArrows = (IEnumerable)canvas.GetType().GetMethod("BuildMergeArrows",
                    BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new[] { routedResult });
            var mergeArrowKinds = mergeArrows.Cast<object>().Select(item =>
                (bool)item.GetType().GetProperty("BackEdge").GetValue(item)).ToList();
            Require(mergeArrowKinds.Any(kind => !kind) && mergeArrowKinds.Any(kind => kind),
                "Le frecce di direzione prima dei ricongiungimenti non coprono linee continue e tratteggiate.");

            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            string complexSource = File.ReadAllText(Path.Combine(projectRoot, "samples",
                "flowchart-complex-smoke-test.ddf"));
            DdfFlowchartLayout complexDiagram = DdfFlowchartLayouter.Layout(
                DdfFlowchartBuilder.Build(complexSource, "main",
                    form.EmbeddedEditor.LanguageSession.ActivePackage.Language).Flowchart);
            List<FlowchartRouteSnapshot> firstComplexRoutes = BuildFlowchartRoutes(canvas, complexDiagram);
            List<FlowchartRouteSnapshot> secondComplexRoutes = BuildFlowchartRoutes(canvas, complexDiagram);
            AssertFlowchartRouteGeometry(complexDiagram, firstComplexRoutes, "diagramma complesso");
            Require(SerializeFlowchartRoutes(firstComplexRoutes) == SerializeFlowchartRoutes(secondComplexRoutes),
                "Il routing del diagramma complesso non e' deterministico a parita' di input.");
            canvas.GetType().GetMethod("SetDiagram", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(canvas, new object[] { routingDiagram });
            string svgPath = Path.Combine(Path.GetTempPath(), "ddf-flowchart-" +
                Guid.NewGuid().ToString("N") + ".svg");
            try
            {
                int decisionPosition = routingSource.IndexOf("value == 2", StringComparison.Ordinal);
                ((FlowchartView)view).SetSourcePosition(decisionPosition);
                ((FlowchartView)view).EducationalModeEnabled = true;
                ((FlowchartView)view).SaveSvg(svgPath);
                var svgDocument = new System.Xml.XmlDocument();
                svgDocument.Load(svgPath);
                string svg = File.ReadAllText(svgPath);
                Require(svgDocument.DocumentElement?.LocalName == "svg" &&
                        svgDocument.DocumentElement.NamespaceURI == "http://www.w3.org/2000/svg" &&
                        svg.Contains("<polyline", StringComparison.Ordinal) &&
                        svg.Contains("marker-end=", StringComparison.Ordinal) &&
                        svg.Contains("stroke-dasharray=", StringComparison.Ordinal) &&
                        svg.Contains("#168242", StringComparison.Ordinal) &&
                        svg.Contains("#be3737", StringComparison.Ordinal) &&
                        !svg.Contains("<image", StringComparison.OrdinalIgnoreCase),
                    "L'export SVG non conserva geometrie, frecce, ritorni tratteggiati e rami didattici vettoriali.");
            }
            finally
            {
                ((FlowchartView)view).EducationalModeEnabled = originalEducationalMode;
                if (File.Exists(svgPath)) File.Delete(svgPath);
            }
            navigation.SelectedTab = page;
            PumpMessages(30);
            int anchorPosition = routingSource.IndexOf("value > 15", StringComparison.Ordinal);
            DdfFlowchartLayoutNode viewportAnchor = routingDiagram.Nodes.Single(item =>
                anchorPosition >= item.Node.Start && anchorPosition <= item.Node.Start + item.Node.Length);
            canvas.GetType().GetMethod("SetSourcePosition", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(canvas, new object[] { anchorPosition });
            zoomIn.PerformClick();
            float preservedZoom = GetControlProperty<float>(canvas, "ZoomFactor");
            var scrollableCanvas = (ScrollableControl)canvas;
            scrollableCanvas.AutoScrollPosition = new Point(
                Math.Max(0, (int)((viewportAnchor.X + viewportAnchor.Width / 2) * preservedZoom -
                    canvas.ClientSize.Width / 2F)),
                Math.Max(0, (int)((viewportAnchor.Y + viewportAnchor.Height / 2) * preservedZoom -
                    canvas.ClientSize.Height / 2F)));
            PumpMessages(20);
            Point beforePan = scrollableCanvas.AutoScrollPosition;
            pan.PerformClick();
            Require(pan.Checked && !pointer.Checked &&
                    GetControlProperty<bool>(canvas, "PanToolEnabled") &&
                    !GetControlProperty<bool>(canvas, "SelectionToolEnabled"),
                "La modalità Pan non si attiva in modo esclusivo rispetto al puntatore.");
            MethodInfo mouseDown = canvas.GetType().GetMethod("OnMouseDown",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo mouseMove = canvas.GetType().GetMethod("OnMouseMove",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo mouseUp = canvas.GetType().GetMethod("OnMouseUp",
                BindingFlags.Instance | BindingFlags.NonPublic);
            mouseDown.Invoke(canvas, new object[]
            {
                new MouseEventArgs(MouseButtons.Left, 1, 120, 120, 0)
            });
            mouseMove.Invoke(canvas, new object[]
            {
                new MouseEventArgs(MouseButtons.Left, 0, 90, 80, 0)
            });
            mouseUp.Invoke(canvas, new object[]
            {
                new MouseEventArgs(MouseButtons.Left, 1, 90, 80, 0)
            });
            PumpMessages(20);
            Require(scrollableCanvas.AutoScrollPosition != beforePan,
                "Trascinare con la modalità Pan non sposta la viewport Flowchart.");
            pointer.PerformClick();
            Require(pointer.Checked && !pan.Checked &&
                    !GetControlProperty<bool>(canvas, "PanToolEnabled") &&
                    GetControlProperty<bool>(canvas, "SelectionToolEnabled"),
                "Il puntatore non viene ripristinato in modo esclusivo dopo il Pan.");
            float anchorClientY = (float)((viewportAnchor.Y + viewportAnchor.Height / 2) * preservedZoom) +
                scrollableCanvas.AutoScrollPosition.Y;
            string changedRoutingSource = routingSource.Replace("int value << 0;",
                "int prefix << 1; int value << 0;");
            int changedAnchorPosition = changedRoutingSource.IndexOf("value > 15", StringComparison.Ordinal);
            DdfFlowchartLayout changedRoutingDiagram = DdfFlowchartLayouter.Layout(
                DdfFlowchartBuilder.Build(changedRoutingSource, "main",
                    form.EmbeddedEditor.LanguageSession.ActivePackage.Language).Flowchart);
            DdfFlowchartLayoutNode changedAnchor = changedRoutingDiagram.Nodes.Single(item =>
                changedAnchorPosition >= item.Node.Start &&
                changedAnchorPosition <= item.Node.Start + item.Node.Length);
            canvas.GetType().GetMethod("SetDiagramPreservingViewport",
                    BindingFlags.Instance | BindingFlags.Public)
                .Invoke(canvas, new object[] { changedRoutingDiagram, changedAnchorPosition });
            PumpMessages(20);
            float changedAnchorClientY = (float)((changedAnchor.Y + changedAnchor.Height / 2) * preservedZoom) +
                scrollableCanvas.AutoScrollPosition.Y;
            Require(Math.Abs(GetControlProperty<float>(canvas, "ZoomFactor") - preservedZoom) < 0.01F,
                "Lo zoom Flowchart cambia durante l'applicazione di uno snapshot.");
            Require(Math.Abs(changedAnchorClientY - anchorClientY) <= 2F,
                "La viewport Flowchart perde il nodo corrente dopo una modifica strutturale.");
            Require(scrollableCanvas.AutoScrollPosition.Y < 0,
                "La viewport Flowchart torna all'origine durante un aggiornamento.");
            zoomOut.PerformClick();
            canvas.GetType().GetMethod("SetDiagram", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(canvas, new object[] { diagram });
            float initialZoom = GetControlProperty<float>(canvas, "ZoomFactor");
            zoomIn.PerformClick();
            Require(GetControlProperty<float>(canvas, "ZoomFactor") > initialZoom,
                "Zoom + non aumenta la scala della Flowchart.");
            zoomOut.PerformClick();
            Require(Math.Abs(GetControlProperty<float>(canvas, "ZoomFactor") - initialZoom) < 0.01F,
                "Zoom - non ripristina la scala della Flowchart.");

            navigation.SelectedTab = page;
            PumpMessages(30);
            DdfFlowchartLayoutNode sourceNode = diagram.Nodes.First(node =>
                node.Node.Kind == DdfFlowchartNodeKind.Process || node.Node.Kind == DdfFlowchartNodeKind.Return);
            editor.Select(0, 0);
            string textBeforeNodeNavigation = editor.Text;
            bool modifiedBeforeNodeNavigation = editor.Modified;
            bool canUndoBeforeNodeNavigation = editor.CanUndo;
            MethodInfo mouseClick = canvas.GetType().GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic);
            mouseClick.Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1,
                (int)(sourceNode.X + sourceNode.Width / 2), (int)(sourceNode.Y + sourceNode.Height / 2), 0) });
            Require(editor.SelectionStart == sourceNode.Node.Start && editor.SelectionLength == sourceNode.Node.Length,
                "Il puntatore Flowchart non seleziona l'intervallo sorgente del nodo cliccato.");
            Require(GetPropertyValue<DdfFlowchartNode>(canvas, "SelectedNode")?.Id == sourceNode.Node.Id,
                "Il nodo cliccato non viene evidenziato immediatamente nella Flowchart.");
            Require(editor.Text == textBeforeNodeNavigation && editor.Modified == modifiedBeforeNodeNavigation &&
                    editor.CanUndo == canUndoBeforeNodeNavigation,
                "La navigazione Flowchart modifica il testo o la cronologia Undo.");

            MethodInfo processKey = canvas.GetType().GetMethod("ProcessCmdKey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string selectedBeforeKey = GetPropertyValue<DdfFlowchartNode>(canvas, "SelectedNode")?.Id;
            object[] keyArguments = { Message.Create(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero), Keys.Down };
            Require((bool)processKey.Invoke(canvas, keyArguments),
                "La Flowchart non gestisce la navigazione verticale da tastiera.");
            DdfFlowchartNode keyboardNode = GetPropertyValue<DdfFlowchartNode>(canvas, "SelectedNode");
            Require(keyboardNode != null && keyboardNode.Id != selectedBeforeKey,
                "Freccia Giù non sposta la selezione a un nodo Flowchart successivo.");
            object[] enterArguments = { Message.Create(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero), Keys.Enter };
            Require((bool)processKey.Invoke(canvas, enterArguments) &&
                    editor.SelectionStart == keyboardNode.Start && editor.SelectionLength == keyboardNode.Length,
                "Invio non naviga al sorgente del nodo Flowchart selezionato da tastiera.");
            Require(editor.Text == textBeforeNodeNavigation && editor.CanUndo == canUndoBeforeNodeNavigation,
                "La navigazione Flowchart da tastiera modifica testo o Undo.");

            string jpegPath = Path.Combine(Path.GetTempPath(), "ddf-flowchart-" + Guid.NewGuid().ToString("N") + ".jpg");
            try
            {
                canvas.GetType().GetMethod("SaveJpeg", BindingFlags.Instance | BindingFlags.Public)
                    .Invoke(canvas, new object[] { jpegPath });
                using (Image image = Image.FromFile(jpegPath))
                    Require(image.Width >= diagram.Width && image.Height >= diagram.Height,
                        "Il JPEG non contiene l'intero diagramma.");
            }
            finally
            {
                if (File.Exists(jpegPath)) File.Delete(jpegPath);
            }

            const string navigationSource =
                "helper(int value) out int { ret value + 1; }\n" +
                "main() out int { int first << 1; int second << 2; int third << helper(first); " +
                "third++; ret third; }";
            editor.Text = navigationSource;
            editor.Select(navigationSource.IndexOf("main", StringComparison.Ordinal), 0);
            PumpUntil(() => GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram")?.Flowchart.FunctionName == "main" &&
                    GetPrivateField<DdfFlowchartLayout>(form, "fullFlowchartLayout")?.Flowchart.Nodes
                        .Any(node => node.CalledFunctions.Contains("helper")) == true,
                4000, "La Flowchart non ha caricato la funzione selezionata per il test di navigazione.");
            InvokePrivate(form, "refreshFlowchartFunctionsMenu");
            ToolStripItem localFunctionItem = functions.DropDownItems["flowchartFunction_helper"];
            MenuStrip mainMenu = FindControl<MenuStrip>(form, "menuStripMain");
            Require(localFunctionItem?.Enabled == true,
                "Il menu funzioni della Flowchart non espone la funzione chiamata.");
            Require(!localFunctionItem.Font.Bold &&
                    Math.Abs(localFunctionItem.Font.Size - mainMenu.Font.Size) < 0.01F &&
                    string.Equals(localFunctionItem.Font.Name, mainMenu.Font.Name, StringComparison.OrdinalIgnoreCase),
                "Il menu funzioni della Flowchart non usa lo stile tipografico dei menu principali.");
            groups.PerformClick();
            PumpUntil(() => GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram").Flowchart.Nodes
                    .Any(node => node.IsCollapsedGroup), 2000,
                "Il comando gruppi non comprime una sequenza lineare della Flowchart.");
            DdfFlowchartLayoutNode collapsedGroup = GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram").Nodes
                .Single(node => node.Node.IsCollapsedGroup);
            canvas.GetType().GetMethod("SetSourcePosition", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(canvas, new object[] { collapsedGroup.Node.Start + collapsedGroup.Node.Length / 2 });
            Require(GetPropertyValue<DdfFlowchartNode>(canvas, "SelectedNode")?.IsCollapsedGroup == true,
                "Il cursore nel sorgente non viene associato al gruppo Flowchart compresso.");
            MethodInfo mouseDoubleClick = canvas.GetType().GetMethod("OnMouseDoubleClick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Point groupPoint = new Point(
                (int)((collapsedGroup.X + collapsedGroup.Width / 2) * GetControlProperty<float>(canvas, "ZoomFactor")) +
                    scrollableCanvas.AutoScrollPosition.X,
                (int)((collapsedGroup.Y + collapsedGroup.Height / 2) * GetControlProperty<float>(canvas, "ZoomFactor")) +
                    scrollableCanvas.AutoScrollPosition.Y);
            mouseDoubleClick.Invoke(canvas, new object[]
            {
                new MouseEventArgs(MouseButtons.Left, 2, groupPoint.X, groupPoint.Y, 0)
            });
            PumpUntil(() => !GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram").Flowchart.Nodes
                    .Any(node => node.IsCollapsedGroup), 2000,
                "Il doppio clic non espande il gruppo della Flowchart.");
            DdfFlowchartLayoutNode callNode = GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram").Nodes
                .Single(node => node.Node.CalledFunctions.Contains("helper"));
            Point callPoint = new Point(
                (int)((callNode.X + callNode.Width / 2) * GetControlProperty<float>(canvas, "ZoomFactor")) +
                    scrollableCanvas.AutoScrollPosition.X,
                (int)((callNode.Y + callNode.Height / 2) * GetControlProperty<float>(canvas, "ZoomFactor")) +
                    scrollableCanvas.AutoScrollPosition.Y);
            mouseDoubleClick.Invoke(canvas, new object[]
            {
                new MouseEventArgs(MouseButtons.Left, 2, callPoint.X, callPoint.Y, 0)
            });
            PumpUntil(() => GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram")?.Flowchart.FunctionName == "helper",
                4000, "Il doppio clic sul nodo chiamata non apre la funzione locale.");
            InvokePrivate(form, "refreshFlowchartFunctionsMenu");
            ToolStripItem flowchartBack = functions.DropDownItems["flowchartNavigateBackMenuItem"];
            Require(flowchartBack != null && flowchartBack.Enabled,
                "Il menu funzioni non abilita il ritorno alla Flowchart chiamante.");
            flowchartBack.PerformClick();
            PumpUntil(() => GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram")?.Flowchart.FunctionName == "main",
                4000, "Torna indietro non ripristina la Flowchart chiamante.");

            string incompleteSource = navigationSource.Replace("int first << 1;", "int first << ;");
            editor.Text = incompleteSource;
            editor.Select(incompleteSource.IndexOf("main", StringComparison.Ordinal), 0);
            PumpUntil(() => status.Text.Contains("PARZIALE", StringComparison.Ordinal) &&
                    GetControlProperty<string>(canvas, "ViewNotice").Contains("Vista parziale", StringComparison.Ordinal),
                4000, "Il Flowchart non segnala chiaramente lo snapshot recuperato da codice incompleto.");
            Require(GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram") != null,
                "Il Flowchart scompare durante un errore sintattico transitorio recuperabile.");
            DdfFlowchartLayout partialDiagram = GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram");
            DdfFlowchartLayout fullPartialDiagram = GetPrivateField<DdfFlowchartLayout>(form, "fullFlowchartLayout");
            Require(fullPartialDiagram.Nodes.Any(item => item.Node.Kind == DdfFlowchartNodeKind.Partial),
                "Il codice incompleto rappresentabile non espone un nodo parziale nel modello completo.");
            int partialPosition = incompleteSource.IndexOf("<< ;", StringComparison.Ordinal) + 3;
            editor.Select(partialPosition, 0);
            PumpMessages(30);
            DdfFlowchartNode selectedPartial = GetPropertyValue<DdfFlowchartNode>(canvas, "SelectedNode");
            Require(selectedPartial != null && (selectedPartial.Kind == DdfFlowchartNodeKind.Partial ||
                    selectedPartial.IsCollapsedGroup) && partialPosition >= selectedPartial.Start &&
                    partialPosition <= selectedPartial.Start + selectedPartial.Length,
                "La sincronizzazione sorgente/Flowchart non riconosce un nodo parziale o il relativo gruppo.");
            editor.Text = "int value << 0;";
            editor.Select(0, 0);
            PumpUntil(() => status.Text.Contains("vista precedente", StringComparison.OrdinalIgnoreCase) &&
                    GetControlProperty<string>(canvas, "ViewNotice").Contains("Vista precedente", StringComparison.Ordinal),
                4000, "Il Flowchart non identifica come non aggiornata l'ultima vista quando la funzione scompare.");
            Require(GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram")?.Flowchart.FunctionName == "main",
                "Il Flowchart elimina l'ultima vista valida quando il sorgente non è rappresentabile.");
            editor.Text = navigationSource;
            editor.Select(navigationSource.IndexOf("main", StringComparison.Ordinal), 0);
            PumpUntil(() => status.Text.Contains("main()", StringComparison.Ordinal) &&
                    !status.Text.Contains("PARZIALE", StringComparison.Ordinal) &&
                    string.IsNullOrEmpty(GetControlProperty<string>(canvas, "ViewNotice")),
                4000, "Il Flowchart non torna automaticamente allo stato aggiornato dopo la correzione.");

            ToolStripMenuItem toggle = FindMenuItem(form, "showFlowchartMenuItem");
            toggle.PerformClick();
            Require(!navigation.TabPages.Contains(page), "Visualizza > Flowchart non nasconde la palette.");
            toggle.PerformClick();
            Require(navigation.TabPages.Contains(page) && ReferenceEquals(navigation.SelectedTab, page),
                "Visualizza > Flowchart non ripristina e seleziona la palette.");
            editor.Focus();
            Console.WriteLine("PASS flowchart asincrona con layout stabile e visibilità opzionale");
        }

        private static void AssertEmbeddableEditorSurface(MainForm form, RichTextBox editor)
        {
            DdfEditorView shellSurface = FindControl<DdfEditorView>(form, "panelEditor");
            Require(shellSurface.ContractVersion == DdfEditorSdkContract.CurrentVersion &&
                    shellSurface.ContractVersion == "0.42" &&
                    ReferenceEquals(shellSurface.DocumentService,
                        GetPrivateField<OpenDocumentCollection>(form, "openDocuments")) &&
                    shellSurface.SourceText == editor.Text &&
                    shellSurface.SelectionStart == editor.SelectionStart &&
                    shellSurface.SelectionLength == editor.SelectionLength,
                "La shell standalone non usa il contratto pubblico DdfEditorView.");

            using (var isolated = new DdfEditorView())
            {
                int textChanges = 0;
                int selectionChanges = 0;
                isolated.SourceTextChanged += (sender, args) => textChanges++;
                isolated.EditorSelectionChanged += (sender, args) => selectionChanges++;
                isolated.SourceText = "main() out int { ret 7; }";
                isolated.SetSelection(17, 5);
                isolated.EditorReadOnly = true;
                isolated.WordWrap = true;
                Require(isolated.SourceText == "main() out int { ret 7; }" &&
                        isolated.SelectionStart == 17 && isolated.SelectionLength == 5 &&
                        isolated.EditorReadOnly && isolated.WordWrap &&
                        textChanges > 0 && selectionChanges > 0,
                    "Il controllo DdfEditorView isolato non espone testo, selezione, opzioni ed eventi coerenti: " +
                    "start=" + isolated.SelectionStart + ", length=" + isolated.SelectionLength +
                    ", readonly=" + isolated.EditorReadOnly + ", wrap=" + isolated.WordWrap +
                    ", textEvents=" + textChanges + ", selectionEvents=" + selectionChanges + ".");
                isolated.SetSelection(-20, int.MaxValue);
                Require(isolated.SelectionStart == 0 &&
                        isolated.SelectionLength == isolated.SourceText.Length,
                    "Il contratto DdfEditorView non normalizza una selezione richiesta dall'host.");

                int documentAddedEvents = 0;
                int activeDocumentEvents = 0;
                int documentChangedEvents = 0;
                int documentClosedEvents = 0;
                int selectionSnapshotEvents = 0;
                isolated.DocumentAdded += (sender, args) => documentAddedEvents++;
                isolated.ActiveDocumentChanged += (sender, args) => activeDocumentEvents++;
                isolated.DocumentChanged += (sender, args) => documentChangedEvents++;
                isolated.DocumentClosed += (sender, args) => documentClosedEvents++;
                isolated.SelectionSnapshotChanged += (sender, args) =>
                    selectionSnapshotEvents++;
                var documents = new OpenDocumentCollection();
                isolated.AttachDocumentService(documents);
                IDdfEditorDocument first = documents.CreateVirtual(
                    "Primo.ddf", "main() out int { ret 1; }");
                Require(ReferenceEquals(isolated.DocumentService, documents) &&
                        isolated.SourceText == first.SourceText,
                    "DdfEditorView non visualizza il primo documento virtuale attivato dall'host.");
                isolated.SourceText = "main() out int { ret 2; }";
                Require(first.SourceText == isolated.SourceText && first.IsDirty && first.Version == 2,
                    "Una modifica della superficie non viene propagata al servizio documentale.");
                IDdfEditorDocument second = documents.CreateVirtual(
                    "Secondo.ddf", "main() out int { ret 3; }");
                Require(isolated.SourceText == second.SourceText,
                    "La superficie non segue l'attivazione di un secondo documento.");
                documents.ActivateDocument(first.Id);
                documents.UpdateDocument(first.Id, "main() out int { ret 4; }");
                Require(isolated.SourceText == "main() out int { ret 4; }",
                    "La superficie non segue attivazione e aggiornamenti prodotti dal servizio.");
                IDdfEditorDocument temporary = documents.CreateVirtual(
                    "Temporaneo.ddf", "main() out int { ret 0; }");
                Require(documents.TryCloseDocument(temporary.Id, true) &&
                        documentAddedEvents == 3 && activeDocumentEvents >= 3 &&
                        documentChangedEvents >= 2 && documentClosedEvents == 1 &&
                        selectionSnapshotEvents > 0,
                    "La superficie non inoltra in modo coerente lifecycle documenti e selezione.");

                var palettes = new DdfEditorPaletteCatalog();
                int paletteEvents = 0;
                palettes.PaletteChanged += (sender, args) => paletteEvents++;
                isolated.AttachPaletteService(palettes);
                IDdfEditorPalette outline = palettes.Register(
                    "outline-test", "Outline", DdfEditorPalettePlacement.Sidebar);
                IDdfEditorPalette diagnostics = palettes.Register(
                    "diagnostics-test", "Diagnostica", DdfEditorPalettePlacement.Bottom);
                Require(palettes.Activate(outline.Id) && outline.IsActive &&
                        palettes.SetVisible(outline.Id, false) && !outline.IsVisible && !outline.IsActive &&
                        diagnostics.IsVisible && paletteEvents >= 4 &&
                        ReferenceEquals(isolated.PaletteService, palettes),
                    "Il catalogo palette isolato non espone registrazione, attivazione e visibilità coerenti.");

                IDdfEditorHostConfiguration configuration = isolated.HostConfiguration;
                configuration.ApplyOptions(new DdfEditorHostOptions(
                    "Consolas", 12F, 125, EditorTheme.Light, true, false, 2));
                RichTextBox isolatedEditor = isolated.Controls.OfType<RichTextBox>().Single();
                Require(isolated.WordWrap && Math.Abs(isolatedEditor.Font.Size - 12F) < 0.1F &&
                        Math.Abs(isolatedEditor.ZoomFactor - 1.25F) < 0.02F &&
                        isolatedEditor.BackColor == Color.White &&
                        configuration.SetShortcut("save", Keys.Control | Keys.S) &&
                        !configuration.SetShortcut("other", Keys.Control | Keys.S),
                    "La configurazione host non applica tema/preferenze o non protegge i conflitti di scorciatoia.");
                isolated.EditorReadOnly = false;
                isolatedEditor.Select(isolatedEditor.TextLength, 0);
                var tabKey = new KeyEventArgs(Keys.Tab);
                typeof(DdfEditorView).GetMethod("activeEditor_KeyDown",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(
                        isolated, new object[] { isolatedEditor, tabKey });
                Require(isolated.SourceText.EndsWith("  ", StringComparison.Ordinal) &&
                        tabKey.Handled && tabKey.SuppressKeyPress,
                    "La preferenza di indentazione per istanza non governa l'inserimento del Tab.");
                DdfEditorAnalysisSnapshot analysis = isolated.AnalysisService.Analyze(
                    new DdfEditorAnalysisRequest(
                        isolated.SourceText,
                        isolated.SourceText.Length,
                        isolated.DocumentService.ActiveDocumentInfo.Version,
                        isolated.LanguageSession.ActivePackage,
                        isolated.LanguageSession.Revision,
                        isolated.DocumentService.ActiveDocumentInfo.Id,
                        isolated.DocumentService.ActiveDocumentInfo.FilePath,
                        true));
                Require(analysis.Symbols.Any(item => item.Name == "main") &&
                        analysis.DocumentVersion == isolated.DocumentService.ActiveDocumentInfo.Version &&
                        isolated.AnalysisService.IsCurrent(analysis),
                    "Il servizio di analisi pubblico non restituisce uno snapshot coerente con il documento host.");
                IDdfEditorCommandService commands = isolated.CommandService;
                Require(DdfEditorSdkContract.CurrentVersion == "0.42" &&
                        typeof(DdfEditorView).GetProperty("PreparationService") != null &&
                        typeof(DdfEditorView).GetProperty("EventDispatcher") != null &&
                         typeof(DdfEditorView).GetProperty(
                             "ProductUpdateService") != null &&
                         typeof(DdfEditorView).GetProperty(
                             "ScriptStatePersistenceService") != null &&
                        typeof(DdfEditorView).GetMethod("ValidateScript") != null &&
                        typeof(DdfEditorView).GetMethod("PrepareScript") != null &&
                         typeof(DdfEditorView).GetMethod(
                             "AttachScriptStatePersistenceProvider") != null &&
                         typeof(DdfEditorView).GetMethod(
                             "DetachScriptStatePersistenceProvider") != null &&
                         typeof(DdfEditorView).GetMethod("SaveScriptState") != null &&
                         typeof(DdfEditorView).GetMethod("RestoreScriptState") != null &&
                         typeof(DdfEditorView).GetMethod(
                             "ResetPersistedScriptState") != null &&
                         typeof(DdfEditorView).GetMethod(
                             "AttachProductUpdateService") != null &&
                         typeof(DdfEditorView).GetMethod(
                             "DetachProductUpdateService") != null &&
                        typeof(DdfEditorView).GetProperty("BuildService") == null &&
                        typeof(DdfEditorView).GetMethod("BuildProject") == null &&
                        typeof(DdfEditorView).GetMethod("AttachBuildService") == null,
                    "Il contratto SDK espone ancora il servizio storico Build o non pubblica la preparazione script 0.37.");
                bool commandEnabled = false;
                int commandExecutions = 0;
                int commandEvents = 0;
                commands.CommandChanged += (sender, args) => commandEvents++;
                Require(commands.Find(DdfEditorCommandIds.Undo) != null &&
                        commands.Find(DdfEditorCommandIds.Redo) != null &&
                        commands.Find(DdfEditorCommandIds.SelectAll) != null &&
                        commands.Find(DdfEditorCommandIds.DeleteSelection) != null &&
                        commands.Find(DdfEditorCommandIds.ToggleWordWrap) != null &&
                        commands.Find(DdfEditorCommandIds.FocusEditor) != null &&
                        commands.Find(DdfEditorCommandIds.AnalyzeDocument) != null &&
                        commands.Find(DdfEditorCommandIds.GoToDefinition) != null &&
                        commands.Find(DdfEditorCommandIds.SaveHostState) != null &&
                        commands.Find(DdfEditorCommandIds.RestoreHostState) != null &&
                        commands.Find(DdfEditorCommandIds.SaveHostProject) != null &&
                        commands.Find(DdfEditorCommandIds.OpenHostProject) != null &&
                        commands.Find(DdfEditorCommandIds.ValidateScript) != null &&
                        commands.Find(DdfEditorCommandIds.PrepareScript) != null &&
                        commands.Find(DdfEditorCommandIds.RunWithoutDebug) != null &&
                        commands.Find(DdfEditorCommandIds.StopExecution) != null &&
                        commands.Find(DdfEditorCommandIds.StartDebugging) != null &&
                        commands.Find(DdfEditorCommandIds.ContinueDebugging) != null &&
                        commands.Find(DdfEditorCommandIds.StepInto) != null &&
                        commands.Find(DdfEditorCommandIds.StepOver) != null &&
                        commands.Find(DdfEditorCommandIds.StepOut) != null &&
                        commands.Find(DdfEditorCommandIds.StopDebugging) != null,
                    "La superficie isolata non registra tutti i comandi editor incorporabili.");
                int eventsBeforeSelection = commandEvents;
                isolated.SetSelection(0, Math.Min(4, isolated.SourceText.Length));
                IDdfEditorCommand deleteSelection = commands.Find(DdfEditorCommandIds.DeleteSelection);
                Require(deleteSelection.CanExecute && commandEvents > eventsBeforeSelection &&
                        commands.TryExecute(deleteSelection.Id) &&
                        commands.Find(DdfEditorCommandIds.Undo).CanExecute,
                    "Selezione, eliminazione e stato Undo non si aggiornano proattivamente.");
                Require(commands.TryExecute(DdfEditorCommandIds.Undo) &&
                        isolated.SourceText.StartsWith("main", StringComparison.Ordinal),
                    "Il comando Undo incorporabile non ripristina il testo eliminato.");
                Require(commands.TryExecute(DdfEditorCommandIds.SelectAll) &&
                        isolated.SelectionLength == isolated.SourceText.Length,
                    "Il comando Seleziona tutto incorporabile non controlla l'editor isolato.");
                bool wordWrapBeforeCommand = isolated.WordWrap;
                Require(commands.TryExecute(DdfEditorCommandIds.ToggleWordWrap) &&
                        isolated.WordWrap != wordWrapBeforeCommand &&
                        commands.Find(DdfEditorCommandIds.ToggleWordWrap).IsChecked == isolated.WordWrap,
                    "Il comando Accapo automatico non aggiorna azione e stato selezionato.");
                commands.TryExecute(DdfEditorCommandIds.ToggleWordWrap);
                isolated.SetSelection(isolated.SourceText.Length, 0);
                const string navigationMain =
                    "main() out int { int local << 1; ret helper(local); }";
                const string navigationHelper =
                    "helper(int value) out int { ret value + 1; }";
                documents.UpdateDocument(first.Id, navigationMain);
                documents.UpdateDocument(second.Id, navigationHelper);
                documents.ActivateDocument(first.Id);
                int analysisEvents = 0;
                int diagnosticsEvents = 0;
                int navigationEvents = 0;
                DdfEditorNavigationTarget navigatedTarget = null;
                isolated.AnalysisCompleted += (sender, args) => analysisEvents++;
                isolated.DiagnosticsUpdated += (sender, args) => diagnosticsEvents++;
                isolated.DefinitionNavigated += (sender, args) =>
                {
                    navigationEvents++;
                    navigatedTarget = args.Target;
                };
                isolated.SetSelection(navigationMain.IndexOf("helper", StringComparison.Ordinal), 0);
                Require(commands.TryExecute(DdfEditorCommandIds.AnalyzeDocument) &&
                        isolated.LastAnalysisSnapshot != null && analysisEvents == 1 &&
                        diagnosticsEvents == 1 &&
                        isolated.LastAnalysisSnapshot.Symbols.Any(item => item.Name == "main"),
                    "Il comando di analisi incorporabile non pubblica uno snapshot osservabile.");
                Require(commands.TryExecute(DdfEditorCommandIds.GoToDefinition) &&
                        ReferenceEquals(documents.ActiveDocumentInfo, second) &&
                        isolatedEditor.SelectedText == "helper" && navigationEvents == 1 &&
                        ReferenceEquals(navigatedTarget.Document, second),
                    "Vai alla definizione non attraversa i buffer virtuali forniti dall'host.");
                int valueReference = navigationHelper.LastIndexOf("value", StringComparison.Ordinal);
                isolated.SetSelection(valueReference, 0);
                Require(isolated.TryGoToDefinition() &&
                        ReferenceEquals(documents.ActiveDocumentInfo, second) &&
                        isolatedEditor.SelectedText == "value" &&
                        isolated.SelectionStart == navigationHelper.IndexOf("value", StringComparison.Ordinal),
                    "La navigazione locale non raggiunge il parametro risolto semanticamente.");
                int buildStateEvents = 0;
                int buildCompletedEvents = 0;
                isolated.PreparationStateChanged += (sender, args) => buildStateEvents++;
                isolated.PreparationCompleted += (sender, args) => buildCompletedEvents++;
                Require(commands.Find(DdfEditorCommandIds.ValidateScript).CanExecute &&
                        commands.TryExecute(DdfEditorCommandIds.ValidateScript) &&
                        isolated.PreparationService.LastSnapshot.Succeeded &&
                        isolated.PreparationService.LastSnapshot.Mode == DdfEditorPreparationMode.Validate &&
                        commands.TryExecute(DdfEditorCommandIds.PrepareScript) &&
                        isolated.PreparationService.LastSnapshot.Mode == DdfEditorPreparationMode.PrepareScript &&
                        buildCompletedEvents == 2 && buildStateEvents == 4,
                    "Convalida, build o relativi eventi del contratto host non sono coerenti.");
                int executionEvents = 0;
                int surfaceExecutionEvents = 0;
                int surfaceExecutionStateEvents = 0;
                isolated.ExecutionService.ExecutionCompleted += (sender, args) => executionEvents++;
                isolated.ExecutionCompleted += (sender, args) => surfaceExecutionEvents++;
                isolated.ExecutionStateChanged += (sender, args) => surfaceExecutionStateEvents++;
                Require(commands.Find(DdfEditorCommandIds.RunWithoutDebug).CanExecute &&
                        commands.TryExecute(DdfEditorCommandIds.RunWithoutDebug),
                    "Il comando Run incorporabile non avvia i buffer virtuali dell'host.");
                PumpUntil(() => isolated.ExecutionService.LastCompletion != null &&
                        executionEvents == 1 && surfaceExecutionEvents == 1 &&
                        surfaceExecutionStateEvents >= 2, 5000,
                    "Il servizio di esecuzione incorporabile non completa la sessione.");
                Require(isolated.ExecutionService.LastCompletion.Succeeded &&
                        Convert.ToInt64(isolated.ExecutionService.LastCompletion.ExecutionResult.ReturnValue) == 2L &&
                        executionEvents == 1 && surfaceExecutionEvents == 1 &&
                        surfaceExecutionStateEvents >= 2 &&
                        isolated.ExecutionService.State == DdfEditorExecutionState.Idle &&
                        !commands.Find(DdfEditorCommandIds.StopExecution).CanExecute,
                    "Risultato, evento o stato del servizio di esecuzione host non sono coerenti.");
                using (var responsive = new DdfEditorView())
                using (var inputStarted = new ManualResetEventSlim(false))
                {
                    responsive.CreateControl();
                    var responsiveDocuments = new OpenDocumentCollection();
                    responsive.AttachDocumentService(responsiveDocuments);
                    responsiveDocuments.CreateVirtual("Heartbeat.ddf",
                        "main() out int { string text << readLine(); ret toInt(text) + 1; }");
                    int uiThread = Environment.CurrentManagedThreadId;
                    int scriptThread = 0;
                    bool heartbeat = false;
                    var callbackThreads = new List<int>();
                    responsive.ExecutionStateChanged += (sender, args) =>
                        callbackThreads.Add(Environment.CurrentManagedThreadId);
                    responsive.ExecutionCompleted += (sender, args) =>
                        callbackThreads.Add(Environment.CurrentManagedThreadId);
                    Task<DdfEditorExecutionCompletion> responsiveRun =
                        responsive.ExecutionService.RunAsync(
                            new DdfEditorExecutionRequest(input: () =>
                            {
                                scriptThread = Environment.CurrentManagedThreadId;
                                inputStarted.Set();
                                Thread.Sleep(250);
                                return "41";
                            }));
                    PumpUntil(() => inputStarted.IsSet, 2000,
                        "Lo script embedded non raggiunge il lavoro bloccante sul worker.");
                    responsive.BeginInvoke(new Action(() => heartbeat = true));
                    PumpUntil(() => heartbeat, 1000,
                        "L'heartbeat UI si blocca durante l'esecuzione embedded.");
                    PumpUntil(() => responsiveRun.IsCompleted &&
                            callbackThreads.Count >= 3, 5000,
                        "L'esecuzione embedded non completa o perde i callback dispatchati.");
                    Require(responsiveRun.Result.Succeeded &&
                            Convert.ToInt64(responsiveRun.Result.ExecutionResult.ReturnValue) == 42L &&
                            scriptThread != 0 && scriptThread != uiThread &&
                            callbackThreads.All(thread => thread == uiThread),
                        "Script e callback embedded non rispettano worker e dispatcher UI separati.");
                }
                var persistence = new DdfInMemoryEditorPersistenceProvider();
                var projectContainer = new DdfInMemoryEditorProjectContainer();
                int projectSavedEvents = 0;
                isolated.ProjectSaved += (sender, args) => projectSavedEvents++;
                isolated.AttachPersistenceProvider(persistence);
                isolated.AttachProjectContainer(projectContainer);
                using (var debugPause = new ManualResetEventSlim(false))
                {
                    int debugPauses = 0;
                    int surfaceDebugPauses = 0;
                    int surfaceDebugCompletions = 0;
                    DdfEditorDebugLocation debugLocation = null;
                    isolated.DebugService.SetBreakpoints(new[]
                        { new DdfEditorBreakpoint(second.Id, 1) });
                    isolated.DebugService.SetWatchExpressions(new[]
                        { "value", "value + 2" });
                    isolated.DebugService.Paused += (sender, args) =>
                    {
                        debugPauses++;
                        debugLocation = args.Location;
                        debugPause.Set();
                    };
                    isolated.DebugPaused += (sender, args) => surfaceDebugPauses++;
                    isolated.DebugCompleted += (sender, args) => surfaceDebugCompletions++;
                    Require(commands.Find(DdfEditorCommandIds.StartDebugging).CanExecute &&
                            commands.TryExecute(DdfEditorCommandIds.StartDebugging),
                        "Il comando Debug incorporabile non avvia i buffer virtuali dell'host.");
                    PumpUntil(() => debugPause.IsSet, 5000,
                        "Il debugger incorporabile non raggiunge il breakpoint nel buffer virtuale.");
                    Require(isolated.DebugService.IsPaused && debugPauses == 1 &&
                            surfaceDebugPauses == 1 &&
                            debugLocation != null && debugLocation.DocumentId == second.Id &&
                            debugLocation.Line == 1 &&
                            isolated.DebugService.CurrentSnapshot != null &&
                            isolated.DebugService.CurrentSnapshot.CallStack.Count == 2 &&
                            isolated.DebugService.CurrentSnapshot.CallStack[0].FunctionName == "helper" &&
                            isolated.DebugService.CurrentSnapshot.CallStack[0].Location.DocumentId == second.Id &&
                            isolated.DebugService.CurrentSnapshot.CallStack[1].Location.DocumentId == first.Id &&
                            isolated.DebugService.CurrentSnapshot.Variables.Any(variable =>
                                variable.Name == "value" && variable.DisplayValue == "1") &&
                            isolated.DebugService.EvaluateWatch("value + 2").Available &&
                            isolated.DebugService.EvaluateWatch("value + 2").Value.DisplayValue == "3" &&
                            !isolated.DebugService.EvaluateWatch("value++").Available &&
                            commands.Find(DdfEditorCommandIds.ContinueDebugging).CanExecute,
                        "Pausa, posizione sorgente o Watch del debugger host non sono coerenti.");
                    Require(commands.TryExecute(DdfEditorCommandIds.SaveHostState) &&
                            persistence.State?.Debugging?.Breakpoints.Count == 1 &&
                            persistence.State.Debugging.Breakpoints[0].DocumentIndex == 1 &&
                            persistence.State.Debugging.Breakpoints[0].Line == 1 &&
                            persistence.State.Debugging.WatchExpressions.SequenceEqual(
                                new[] { "value", "value + 2" }) &&
                            !commands.Find(DdfEditorCommandIds.RestoreHostState).CanExecute,
                        "Il salvataggio in pausa non conserva la configurazione debug o abilita un ripristino pericoloso.");
                    DdfEditorProjectState savedProject = isolated.SaveProject(
                        "host-demo", "Progetto host dimostrativo",
                        new[] { new KeyValuePair<string, string>("tenant", "training") });
                    Require(ReferenceEquals(projectContainer.Project, savedProject) &&
                            ReferenceEquals(isolated.CurrentProject, savedProject) &&
                            savedProject.EditorState.Debugging.Breakpoints.Count == 1 &&
                            savedProject.Metadata["tenant"] == "training" &&
                            savedProject.LanguagePackageId == isolated.LanguageSession.ActivePackage.Id &&
                            projectSavedEvents == 1 &&
                            commands.Find(DdfEditorCommandIds.SaveHostProject).CanExecute &&
                            !commands.Find(DdfEditorCommandIds.OpenHostProject).CanExecute,
                        "Il contenitore host non riceve un progetto immutabile completo durante la pausa.");
                    bool rejectedDocumentSwap = false;
                    bool rejectedLanguageSwap = false;
                    bool rejectedProjectContainerSwap = false;
                    try { isolated.AttachDocumentService(new OpenDocumentCollection()); }
                    catch (InvalidOperationException) { rejectedDocumentSwap = true; }
                    try { isolated.AttachLanguageSession(new DdfLanguagePackageSession()); }
                    catch (InvalidOperationException) { rejectedLanguageSwap = true; }
                    try { isolated.AttachProjectContainer(new DdfInMemoryEditorProjectContainer()); }
                    catch (InvalidOperationException) { rejectedProjectContainerSwap = true; }
                    Require(rejectedDocumentSwap && rejectedLanguageSwap &&
                            rejectedProjectContainerSwap &&
                            commands.TryExecute(DdfEditorCommandIds.ContinueDebugging),
                        "La superficie consente di sostituire documenti, linguaggio o contenitore durante la pausa.");
                    PumpUntil(() => isolated.DebugService.LastCompletion != null &&
                            isolated.DebugService.State == DdfEditorDebugState.Idle &&
                            surfaceDebugCompletions == 1, 5000,
                        "La sessione di debug incorporabile non termina dopo Continua.");
                    Require(isolated.DebugService.LastCompletion.Succeeded &&
                            Convert.ToInt64(isolated.DebugService.LastCompletion.ExecutionResult.ReturnValue) == 2L &&
                            isolated.DebugService.State == DdfEditorDebugState.Idle &&
                            isolated.DebugService.CurrentLocation == null &&
                            !commands.Find(DdfEditorCommandIds.StopDebugging).CanExecute &&
                            surfaceDebugCompletions == 1,
                        "Completamento o lifecycle del debugger incorporabile non sono coerenti.");
                }
                IDdfEditorCommand hostCommand = commands.Register(
                    "host.calculate", "Calcola", "Host",
                    () => commandExecutions++, () => commandEnabled,
                    description: "Esegue un calcolo registrato dall'host.");
                Require(ReferenceEquals(commands.Find(hostCommand.Id), hostCommand) &&
                        !commands.TryExecute(hostCommand.Id) && commandExecutions == 0,
                    "Il catalogo comandi esegue un comando host disabilitato.");
                commandEnabled = true;
                commands.Refresh();
                Require(commands.TryExecute(hostCommand.Id) && commandExecutions == 1 &&
                        commandEvents >= 2 && commands.Remove(hostCommand.Id) &&
                        commands.Find(hostCommand.Id) == null,
                    "Registrazione, stato, esecuzione o rimozione del comando host non sono coerenti.");
                using (var independent = new DdfEditorView())
                {
                    Require(independent.HostConfiguration.Options.Theme == EditorTheme.Dark &&
                            independent.Controls.OfType<RichTextBox>().Single().BackColor != Color.White,
                        "Due istanze DdfEditorView condividono involontariamente tema o preferenze host.");
                    isolated.ExperienceState.SetEducationalMode(true);
                    Require(isolated.ExperienceState.EducationalModeEnabled &&
                            !independent.ExperienceState.EducationalModeEnabled &&
                            !ReferenceEquals(isolated.LanguageSession, independent.LanguageSession) &&
                            !ReferenceEquals(isolated.AnalysisService, independent.AnalysisService) &&
                            !ReferenceEquals(isolated.CommandService, independent.CommandService) &&
                            !ReferenceEquals(isolated.ExecutionService, independent.ExecutionService) &&
                            !ReferenceEquals(isolated.DebugService, independent.DebugService) &&
                            isolated.LanguageSession.ActivePackage != null &&
                            independent.LanguageSession.ActivePackage != null,
                        "Modalità didattica o sessione linguaggio vengono condivise tra istanze host.");
                }
                Require(commands.Find(DdfEditorCommandIds.SaveHostState).CanExecute &&
                        commands.TryExecute(DdfEditorCommandIds.SaveHostState) &&
                        persistence.State != null && persistence.State.Documents.Count == 2 &&
                        persistence.State.ActiveDocumentIndex == 1,
                    "Il provider host non riceve lo snapshot multi-documento tramite il comando pubblico.");
                Require(commands.TryExecute(DdfEditorCommandIds.SaveHostProject) &&
                        projectSavedEvents == 2 && projectContainer.Project.ProjectId == "host-demo",
                    "Il comando progetto non aggiorna il contenitore già associato.");
                using (var restored = new DdfEditorView())
                {
                    var restoredDocuments = new OpenDocumentCollection();
                    restored.AttachDocumentService(restoredDocuments);
                    restored.AttachPersistenceProvider(persistence);
                    Require(restored.CommandService.TryExecute(DdfEditorCommandIds.RestoreHostState) &&
                            restoredDocuments.OpenDocuments.Count == 2 &&
                            restoredDocuments.ActiveDocumentInfo.DisplayName == "Secondo.ddf" &&
                            restoredDocuments.ActiveDocumentInfo.SourceText == navigationHelper &&
                            restoredDocuments.OpenDocuments.All(document => document.IsDirty) &&
                            restored.HostConfiguration.Options.Theme == EditorTheme.Light &&
                            restored.HostConfiguration.Options.ZoomPercent == 125 &&
                            restored.HostConfiguration.Shortcuts["save"] ==
                                (Keys.Control | Keys.S) &&
                            restored.ExperienceState.EducationalModeEnabled,
                        "Il ripristino host non conserva buffer, documento attivo, dirty state o preferenze isolate.");
                    Require(restored.DebugService.Breakpoints.Count == 1 &&
                            restored.DebugService.Breakpoints[0].DocumentId ==
                                restoredDocuments.OpenDocuments[1].Id &&
                            restored.DebugService.Breakpoints[0].Line == 1 &&
                            restored.DebugService.WatchExpressions.SequenceEqual(
                                new[] { "value", "value + 2" }),
                        "Il ripristino host non rimappa breakpoint e Watch sui nuovi documenti.");
                    bool rejectedPopulatedRestore = false;
                    try { restored.RestoreState(); }
                    catch (InvalidOperationException) { rejectedPopulatedRestore = true; }
                    Require(rejectedPopulatedRestore && restoredDocuments.OpenDocuments.Count == 2,
                        "Il ripristino può sovrascrivere un servizio documentale già popolato.");
                }
                using (var projectRestored = new DdfEditorView())
                {
                    var projectDocuments = new OpenDocumentCollection();
                    int projectOpenedEvents = 0;
                    projectRestored.ProjectOpened += (sender, args) => projectOpenedEvents++;
                    projectRestored.AttachDocumentService(projectDocuments);
                    projectRestored.AttachProjectContainer(projectContainer);
                    Require(projectRestored.CommandService.TryExecute(
                                DdfEditorCommandIds.OpenHostProject) &&
                            projectDocuments.OpenDocuments.Count == 2 &&
                            projectDocuments.ActiveDocumentInfo.DisplayName == "Secondo.ddf" &&
                            projectRestored.CurrentProject.ProjectId == "host-demo" &&
                            projectRestored.CurrentProject.Metadata["tenant"] == "training" &&
                            projectRestored.DebugService.Breakpoints.Count == 1 &&
                            projectRestored.DebugService.Breakpoints[0].DocumentId ==
                                projectDocuments.OpenDocuments[1].Id &&
                            projectOpenedEvents == 1,
                        "L'apertura dal contenitore host non ricostruisce progetto, documenti e debug.");
                }
                using (var mismatched = new DdfEditorView())
                {
                    var mismatchedDocuments = new OpenDocumentCollection();
                    var mismatchedContainer = new DdfInMemoryEditorProjectContainer();
                    mismatchedContainer.Save(new DdfEditorProjectState(
                        "foreign", "Pacchetto incompatibile", "other.language", "9.0",
                        projectContainer.Project.EditorState));
                    mismatched.AttachDocumentService(mismatchedDocuments);
                    mismatched.AttachProjectContainer(mismatchedContainer);
                    bool rejectedPackage = false;
                    try { mismatched.OpenProject(); }
                    catch (InvalidOperationException) { rejectedPackage = true; }
                    Require(rejectedPackage && mismatchedDocuments.OpenDocuments.Count == 0 &&
                            mismatched.CurrentProject == null,
                        "Un progetto con pacchetto incompatibile modifica la superficie prima del rifiuto.");
                }
            }

            IDdfEditorPaletteService shellPalettes = shellSurface.PaletteService;
            Require(shellPalettes != null && shellPalettes.Palettes.Count == 10 &&
                    shellPalettes.Find(DdfEditorPaletteIds.Flowchart) != null &&
                    shellPalettes.Find(DdfEditorPaletteIds.Diagnostics) != null,
                "La shell non pubblica il catalogo completo delle palette tramite DdfEditorView.");
            TabControl navigationTabs = GetPrivateField<TabControl>(form, "navigationTabs");
            TabPage outlinePage = navigationTabs.TabPages.Cast<TabPage>()
                .Single(page => page.Name == "outlineTabPage");
            Require(shellPalettes.SetVisible(DdfEditorPaletteIds.Outline, false) &&
                    !navigationTabs.TabPages.Contains(outlinePage),
                "Nascondere Outline tramite il contratto host non aggiorna la shell.");
            Require(shellPalettes.Activate(DdfEditorPaletteIds.Outline) &&
                    navigationTabs.TabPages.Contains(outlinePage) &&
                    ReferenceEquals(navigationTabs.SelectedTab, outlinePage),
                "Attivare Outline tramite il contratto host non ripristina la palette nella shell.");
            IDdfEditorHostConfiguration shellConfiguration = shellSurface.HostConfiguration;
            EditorSettings shellSettings = GetPrivateField<EditorSettings>(form, "editorSettings");
            Require(shellConfiguration != null &&
                    shellConfiguration.Options.Theme == shellSettings.Theme &&
                    shellConfiguration.Options.FontFamily == shellSettings.FontFamily &&
                    shellConfiguration.Shortcuts.Count > 0,
                "La shell non converge sulla configurazione per istanza esposta dall'SDK.");
            Require(ReferenceEquals(shellSurface.LanguageSession,
                        GetPrivateField<DdfLanguagePackageSession>(form, "editorLanguageSession")) &&
                    ReferenceEquals(shellSurface.ExperienceState,
                        GetPrivateField<DdfEditorExperienceState>(form, "editorExperienceState")) &&
                    ReferenceEquals(GetPrivateField<FlowchartView>(form, "flowchartView").ExperienceState,
                        shellSurface.ExperienceState) &&
                    shellSurface.ExperienceState.EducationalModeEnabled ==
                        Convert.ToBoolean(GetPrivateField<object>(form, "educationalModeEnabled")),
                "La shell non espone sessione linguaggio e modalità didattica tramite il contratto host.");
            Require(shellSurface.AnalysisService != null && shellSurface.ExecutionService != null &&
                    shellSurface.DebugService != null,
                "La shell non espone i servizi versionati di analisi, esecuzione e debug tramite DdfEditorView.");
            IDdfEditorCommandService shellCommands = shellSurface.CommandService;
            Require(shellCommands != null && shellCommands.Commands.Count >= 35 &&
                    shellCommands.Commands.Select(command => command.Id).Distinct(StringComparer.Ordinal).Count() ==
                        shellCommands.Commands.Count &&
                    shellCommands.Find(DdfEditorCommandIds.FormatDocument) != null &&
                    shellCommands.Find(DdfEditorCommandIds.StartDebugging) != null &&
                    !shellCommands.TryExecute("missing.command"),
                "La shell non pubblica un catalogo coerente dei propri comandi tramite l'SDK.");
            int shellCommandStateEvents = 0;
            shellCommands.CommandChanged += (sender, args) =>
            {
                if (args.ChangeKind == DdfEditorCommandChangeKind.StateChanged &&
                    args.Command.Id == DdfEditorCommandIds.Copy) shellCommandStateEvents++;
            };
            shellSurface.SetSelection(0, 0);
            shellCommands.Refresh();
            int selectableLength = Math.Min(4, shellSurface.SourceText.Length);
            shellSurface.SetSelection(0, selectableLength);
            Require(selectableLength == 0 ||
                    (shellCommands.Find(DdfEditorCommandIds.Copy).CanExecute && shellCommandStateEvents > 0),
                "Il catalogo della shell non notifica proattivamente il cambio di selezione.");
            shellSurface.SetSelection(0, 0);
            Require(shellSurface.PersistenceProvider == null,
                "La shell associa implicitamente una persistenza SDK al profilo applicativo.");
            Console.WriteLine("PASS superficie DdfEditorView incorporabile con contratti pubblici 0.42, aggiornamenti embedded opt-in, snapshot cooperativi opzionali, errori recuperabili tipizzati, preparazione script separata dalla compilazione, state persistibile opt-in per istanza, shell completa embedded, transazioni degli effetti host, librerie di funzioni, servizi database parametrizzati, procedure ed eventi host tipizzati, risultati host, catalogo, progetti, persistenza, analisi, navigazione, esecuzione e debug isolati");
        }

        private static List<FlowchartRouteSnapshot> BuildFlowchartRoutes(Control canvas,
            DdfFlowchartLayout diagram)
        {
            canvas.GetType().GetMethod("SetDiagram", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(canvas, new object[] { diagram });
            object routeMap = diagram.Nodes.ToDictionary(item => item.Node.Id,
                item => item, StringComparer.Ordinal);
            object routed = canvas.GetType().GetMethod("BuildRoutes",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new[] { routeMap });
            return ReadFlowchartRoutes((IEnumerable)routed);
        }

        private static List<FlowchartRouteSnapshot> ReadFlowchartRoutes(IEnumerable routed)
        {
            var routes = new List<FlowchartRouteSnapshot>();
            foreach (object item in routed)
            {
                Type itemType = item.GetType();
                routes.Add(new FlowchartRouteSnapshot(
                    (DdfFlowchartEdge)itemType.GetProperty("Edge").GetValue(item),
                    (PointF[])itemType.GetProperty("Points").GetValue(item),
                    (PointF)itemType.GetProperty("LabelPoint").GetValue(item)));
            }
            return routes;
        }

        private static void AssertFlowchartRouteGeometry(DdfFlowchartLayout diagram,
            IReadOnlyList<FlowchartRouteSnapshot> routes, string scenario)
        {
            Require(routes.Count == diagram.Flowchart.Edges.Count,
                scenario + ": non tutti gli archi semantici sono stati instradati.");
            Require(routes.All(route => route.Points.Length >= 2),
                scenario + ": un collegamento non contiene un percorso visibile.");
            Require(routes.SelectMany(route => route.Points.Zip(route.Points.Skip(1),
                    (left, right) => Math.Abs(left.X - right.X) < 0.01F ||
                        Math.Abs(left.Y - right.Y) < 0.01F)).All(value => value),
                scenario + ": il router produce segmenti diagonali.");
            Require(routes.All(route => Math.Abs(route.Points[0].X - route.Points[1].X) < 0.01F),
                scenario + ": un collegamento non esce prima verticalmente dal proprio modulo.");
            Require(routes.All(route => !route.Points.Skip(2).Select((point, index) =>
                    FlowchartPointsAreCollinear(route.Points[index], route.Points[index + 1], point)).Any(value => value)),
                scenario + ": sono presenti due gomiti consecutivi superflui sullo stesso asse.");
            Require(routes.SelectMany(route => route.Points).All(point =>
                    point.X >= 0 && point.X <= diagram.Width &&
                    point.Y >= 0 && point.Y <= diagram.Height),
                scenario + ": un collegamento si allunga oltre l'area utile del diagramma.");
            Require(routes.All(route => route.LabelPoint.X >= 0 && route.LabelPoint.X <= diagram.Width &&
                    route.LabelPoint.Y >= 0 && route.LabelPoint.Y <= diagram.Height),
                scenario + ": un'etichetta Si/No cade fuori dall'area utile.");

            List<Tuple<PointF, PointF>> solidSegments = routes.Where(route => !route.Edge.IsBackEdge)
                .SelectMany(route => RouteSegments(route.Points)).ToList();
            List<Tuple<PointF, PointF>> dashedSegments = routes.Where(route => route.Edge.IsBackEdge)
                .SelectMany(route => RouteSegments(route.Points)).ToList();
            Require(!dashedSegments.Any(dashed => solidSegments.Any(solid =>
                    CollinearOverlap(dashed, solid) || PerpendicularCross(dashed, solid))),
                scenario + ": una linea tratteggiata si sovrappone o interseca una linea continua.");

            bool crossing = false;
            for (int left = 0; left < routes.Count && !crossing; left++)
                for (int right = left + 1; right < routes.Count && !crossing; right++)
                    crossing = RouteSegments(routes[left].Points).Any(leftSegment =>
                        RouteSegments(routes[right].Points).Any(rightSegment =>
                            PerpendicularCross(leftSegment, rightSegment)));
            Require(!crossing,
                scenario + ": due collegamenti si intersecano senza una giunzione logica.");

            List<IGrouping<string, FlowchartRouteSnapshot>> merges = routes
                .Where(route => !route.Edge.IsBackEdge).GroupBy(route => route.Edge.To)
                .Where(group => group.Count() > 1).ToList();
            if (merges.Count > 0)
                Require(merges.Any(group => group.Where(route => route.Points.Length > 2)
                        .Select(route => route.Points[route.Points.Length - 2]).Distinct().Count() == 1),
                    scenario + ": gli archi convergenti non condividono i gomiti quando possibile.");
        }

        private static bool FlowchartPointsAreCollinear(PointF first, PointF second, PointF third) =>
            (Math.Abs(first.X - second.X) < 0.01F && Math.Abs(second.X - third.X) < 0.01F) ||
            (Math.Abs(first.Y - second.Y) < 0.01F && Math.Abs(second.Y - third.Y) < 0.01F);

        private static string SerializeFlowchartRoutes(IEnumerable<FlowchartRouteSnapshot> routes) =>
            string.Join("|", routes.Select(route => route.Edge.From + ">" + route.Edge.To + ":" +
                route.Edge.Kind + ":" + route.Edge.IsBackEdge + ":" +
                string.Join(";", route.Points.Select(point =>
                    point.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                    point.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + ":" +
                route.LabelPoint.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                route.LabelPoint.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

        private static IEnumerable<Tuple<PointF, PointF>> RouteSegments(IReadOnlyList<PointF> points)
        {
            for (int index = 0; index + 1 < points.Count; index++)
                yield return Tuple.Create(points[index], points[index + 1]);
        }

        private static bool CollinearOverlap(Tuple<PointF, PointF> left,
            Tuple<PointF, PointF> right)
        {
            const float tolerance = 0.01F;
            bool leftVertical = Math.Abs(left.Item1.X - left.Item2.X) < tolerance;
            bool rightVertical = Math.Abs(right.Item1.X - right.Item2.X) < tolerance;
            if (leftVertical != rightVertical) return false;
            if (leftVertical)
            {
                if (Math.Abs(left.Item1.X - right.Item1.X) >= tolerance) return false;
                return Math.Min(Math.Max(left.Item1.Y, left.Item2.Y), Math.Max(right.Item1.Y, right.Item2.Y)) -
                    Math.Max(Math.Min(left.Item1.Y, left.Item2.Y), Math.Min(right.Item1.Y, right.Item2.Y)) > tolerance;
            }
            if (Math.Abs(left.Item1.Y - right.Item1.Y) >= tolerance) return false;
            return Math.Min(Math.Max(left.Item1.X, left.Item2.X), Math.Max(right.Item1.X, right.Item2.X)) -
                Math.Max(Math.Min(left.Item1.X, left.Item2.X), Math.Min(right.Item1.X, right.Item2.X)) > tolerance;
        }

        private static bool PerpendicularCross(Tuple<PointF, PointF> left,
            Tuple<PointF, PointF> right)
        {
            const float tolerance = 0.01F;
            bool leftVertical = Math.Abs(left.Item1.X - left.Item2.X) < tolerance;
            bool rightVertical = Math.Abs(right.Item1.X - right.Item2.X) < tolerance;
            if (leftVertical == rightVertical) return false;
            Tuple<PointF, PointF> vertical = leftVertical ? left : right;
            Tuple<PointF, PointF> horizontal = leftVertical ? right : left;
            float x = vertical.Item1.X;
            float y = horizontal.Item1.Y;
            return x > Math.Min(horizontal.Item1.X, horizontal.Item2.X) + tolerance &&
                x < Math.Max(horizontal.Item1.X, horizontal.Item2.X) - tolerance &&
                y > Math.Min(vertical.Item1.Y, vertical.Item2.Y) + tolerance &&
                y < Math.Max(vertical.Item1.Y, vertical.Item2.Y) - tolerance;
        }

        private sealed class FlowchartRouteSnapshot
        {
            public FlowchartRouteSnapshot(DdfFlowchartEdge edge, PointF[] points, PointF labelPoint)
            {
                Edge = edge;
                Points = points;
                LabelPoint = labelPoint;
            }

            public DdfFlowchartEdge Edge { get; }
            public PointF[] Points { get; }
            public PointF LabelPoint { get; }
        }

        private sealed class CallbackDisposable : IDisposable
        {
            private Action callback;

            public CallbackDisposable(Action callback)
            {
                this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref callback, null)?.Invoke();
            }
        }

        private static void AssertRestoredMultiFileGutter(bool visible)
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-gutter-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string firstPath = Path.Combine(root, "first.ddf");
                string secondPath = Path.Combine(root, "second.ddf");
                File.WriteAllText(firstPath, string.Join("\n", Enumerable.Range(1, 28)
                    .Select(line => line % 5 == 0 ? string.Empty : "int first" + line + " << " + line + ";")));
                File.WriteAllText(secondPath, string.Join("\n", Enumerable.Range(1, 32)
                    .Select(line => line % 5 == 0
                        ? string.Empty
                        : line % 4 == 0
                        ? "string second" + line + " << \"" + new string('w', 170) + "\";"
                        : "int second" + line + " << " + line + ";")));
                var snapshot = new WorkspaceSessionSnapshot
                {
                    WorkspacePath = root,
                    ActiveDocumentIndex = 0,
                    Documents = new List<WorkspaceSessionDocumentSnapshot>
                    {
                        new WorkspaceSessionDocumentSnapshot { Path = firstPath, SelectionStart = 0 },
                        new WorkspaceSessionDocumentSnapshot { Path = secondPath, SelectionStart = 0 }
                    }
                };

                using (var form = new MainForm())
                {
                    DisableRecentFilePersistence(form);
                    ResetEditorSettingsForSmoke(form);
                    EditorSettings settings = GetPrivateField<EditorSettings>(form, "editorSettings");
                    SetPrivateField(form, "editorSettings", settings.WithVisualOptions(
                        settings.HighlightCurrentLine, settings.ShowWhitespace, settings.ShowIndentGuides, true));
                    InvokePrivate(form, "applyEditorSettings");
                    SetPrivateField(form, "loadWorkspaceSessionSetting", new Func<string>(snapshot.Serialize));
                    form.ShowInTaskbar = visible;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = visible ? new Point(80, 80) : new Point(-32000, -32000);
                    if (!visible) form.WindowState = FormWindowState.Normal;
                    form.Show();
                    PumpMessages(240);

                    TabControl tabs = FindControl<TabControl>(form, "documentTabs");
                    Require(tabs.TabPages.Count == 2, "Il ripristino multifile non ha riaperto entrambe le schede.");
                    OpenDocumentCollection restoredDocuments =
                        GetPrivateField<OpenDocumentCollection>(form,
                            "openDocuments");
                    Require(string.Equals(restoredDocuments.ActiveDocument?.FilePath,
                            firstPath, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(GetPrivateField<RichTextBox>(form,
                            "richTextBoxMainEditor").Text,
                            restoredDocuments.ActiveDocument.Source,
                            StringComparison.Ordinal),
                        "Il ripristino della prima scheda non allinea il documento attivo al controllo visibile.");
                    Require(restoredDocuments.FindByPath(secondPath)?.Source
                            .StartsWith("int second1", StringComparison.Ordinal) == true,
                        "Il buffer della seconda scheda è stato sovrascritto con il sorgente della prima.");
                    for (int tab = 0; tab < tabs.TabPages.Count; tab++)
                    {
                        tabs.SelectedIndex = tab;
                        PumpMessages(100);
                        RichTextBox activeEditor = GetPrivateField<RichTextBox>(form, "richTextBoxMainEditor");
                        activeEditor.Select(activeEditor.TextLength, 0);
                        activeEditor.ScrollToCaret();
                        PumpMessages(80);
                        InvokePrivate(form, "updateLineNumbers");
                        RequireVisibleLogicalLinesInGutter(activeEditor,
                            FindControl<RichTextBox>(form, "richTextBoxLineNumbers"));
                    }
                }
                Console.WriteLine("PASS gutter continuo dopo ripristino workspace multifile");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void AssertDocumentViewportPersistence(bool visible)
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-viewport-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "viewport.ddf");
            File.WriteAllText(path, string.Join("\n", Enumerable.Range(1, 140)
                .Select(line => "int value" + line + "<<" + line + ";")));
            string sessionData = null;
            try
            {
                using (var form = ShowSmokeForm(visible))
                {
                    DisableRecentFilePersistence(form);
                    EditorSettings current = GetPrivateField<EditorSettings>(form, "editorSettings");
                    SetPrivateField(form, "editorSettings", new EditorSettings(
                        current.FontFamily, current.FontSize, current.ZoomPercent, current.Theme,
                        current.UseTabs, current.IndentSize, current.LineEnding, true,
                        current.AutoSaveEnabled, current.AutoSaveDelaySeconds, current.HotExitEnabled,
                        current.RestoreSessionEnabled, current.CrashRecoveryEnabled,
                        current.HighlightCurrentLine, current.ShowWhitespace, current.ShowIndentGuides,
                        false, current.ShowOverview));
                    InvokePrivate(form, "applyEditorSettings");

                    Require((bool)InvokePrivate(form, "openDocument", path),
                        "Lo smoke viewport non riesce ad aprire il documento lungo.");
                    PumpMessages(180);
                    RichTextBox editor = GetPrivateField<RichTextBox>(form, "richTextBoxMainEditor");
                    Require(GetEditorScrollPosition(editor).Y <= 2,
                        "Un nuovo documento non viene aperto dall'inizio della viewport.");

                    editor.Select(editor.GetFirstCharIndexFromLine(90), 0);
                    editor.ScrollToCaret();
                    PumpMessages(60);
                    Point beforeSave = GetEditorScrollPosition(editor);
                    Require(beforeSave.Y > 0, "Lo smoke viewport non ha spostato lo scroll verticale.");
                    Require((bool)InvokePrivate(form, "saveDocument", false),
                        "Il salvataggio con formattazione non è riuscito nello smoke viewport.");
                    PumpMessages(180);
                    Point afterSave = GetEditorScrollPosition(editor);
                    object savedViewState = GetPropertyValue<object>(form,
                        "activeDocumentView");
                    Point storedAfterSave = GetPropertyValue<Point>(
                        savedViewState, "ScrollPosition");
                    Require(Math.Abs(afterSave.Y - beforeSave.Y) <= 2,
                        "Salva o Formatta al salvataggio sposta la viewport verticale.");

                    TabControl tabs = FindControl<TabControl>(form, "documentTabs");
                    TabPage savedTab = tabs.SelectedTab;
                    FindMenuItem(form, "newMenuItem").PerformClick();
                    PumpMessages(50);
                    tabs.SelectedTab = savedTab;
                    PumpMessages(180);
                    editor = GetPrivateField<RichTextBox>(form, "richTextBoxMainEditor");
                    Point afterTabSwitch = GetEditorScrollPosition(editor);
                    Point storedAfterSwitch = GetPropertyValue<Point>(
                        savedViewState, "ScrollPosition");
                    Require(Math.Abs(afterTabSwitch.Y - afterSave.Y) <= 2,
                        "Il cambio scheda perde la posizione verticale del documento: " +
                        "prima=" + afterSave.Y + ", dopo=" +
                        afterTabSwitch.Y + ", statoPrima=" +
                        storedAfterSave.Y + ", statoDopo=" +
                        storedAfterSwitch.Y + ".");

                    SetPrivateField(form, "saveWorkspaceSessionSetting",
                        new Action<string>(value => sessionData = value));
                    InvokePrivate(form, "persistWorkspaceSession");
                    WorkspaceSessionSnapshot snapshot = WorkspaceSessionSnapshot.Parse(sessionData);
                    WorkspaceSessionDocumentSnapshot saved = snapshot.Documents.Single(item =>
                        string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
                    Require(saved.ScrollY.HasValue && Math.Abs(saved.ScrollY.Value - afterTabSwitch.Y) <= 2,
                        "La sessione progetto non conserva la viewport verticale.");
                }

                using (var restored = ShowSmokeForm(visible))
                {
                    DisableRecentFilePersistence(restored);
                    SetPrivateField(restored, "loadWorkspaceSessionSetting", new Func<string>(() => sessionData));
                    InvokePrivate(restored, "restoreWorkspaceSession");
                    PumpMessages(220);
                    RichTextBox editor = GetPrivateField<RichTextBox>(restored, "richTextBoxMainEditor");
                    WorkspaceSessionSnapshot snapshot = WorkspaceSessionSnapshot.Parse(sessionData);
                    int expectedY = snapshot.Documents.Single(item =>
                        string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)).ScrollY.Value;
                    Require(Math.Abs(GetEditorScrollPosition(editor).Y - expectedY) <= 2,
                        "La riapertura del progetto non ripristina la viewport verticale salvata.");
                }

                Console.WriteLine("PASS viewport verticale stabile in apertura, salvataggio, schede e sessione progetto");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RunMenuScenarios(bool visible)
        {
            IDataObject originalClipboard = null;
            try
            {
                originalClipboard = Clipboard.GetDataObject();
                AssertMenuStructureAndShortcuts(visible);
                AssertToolbarLayoutRestoration(visible);
                AssertEditAndFindReplaceMenus(visible);
                AssertViewMenu(visible);
                AssertHelpMenu(visible);
                AssertModuleMenu(visible);
                AssertFileMenu(visible);
            }
            finally
            {
                RestoreClipboard(originalClipboard);
            }
        }

        private static void AssertToolbarLayoutRestoration(bool visible)
        {
            using (var form = new MainForm
            {
                ShowInTaskbar = visible,
                StartPosition = FormStartPosition.Manual,
                Location = visible ? new Point(100, 100) : new Point(-32000, -32000),
                WindowState = FormWindowState.Normal
            })
            {
                DisableRecentFilePersistence(form);
                ResetEditorSettingsForSmoke(form);
                ResetShortcutSettingsForSmoke(form);
                ResetPaletteLayoutForSmoke(form);
                ResetToolbarLayoutForSmoke(form);
                const string saved =
                    "debug.visible=True\ndebug.row=0\ndebug.order=0\ndebug.x=10\ndebug.y=1\n" +
                    "file.visible=True\nfile.row=0\nfile.order=1\nfile.x=210\nfile.y=1\n" +
                    "code.visible=True\ncode.row=0\ncode.order=2\ncode.x=410\ncode.y=1\n" +
                    "view.visible=False\nview.row=1\nview.order=0\nview.x=10\nview.y=34\n" +
                    "project.visible=True\nproject.row=1\nproject.order=1\nproject.x=210\nproject.y=34\n" +
                    "navigation.visible=True\nnavigation.row=2\nnavigation.order=0\nnavigation.x=10\nnavigation.y=67\n" +
                    "edit.visible=True\nedit.row=3\nedit.order=0\nedit.x=10\nedit.y=100";
                Type settingsType = typeof(MainForm).Assembly.GetType(
                    "DDF___Program_Language_Editor.ToolbarLayoutSettings", true);
                object settings = settingsType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public)
                    .Invoke(null, new object[] { saved, new[] { "file", "edit", "code", "navigation", "view", "project", "debug" } });
                SetPrivateField(form, "toolbarLayoutSettings", settings);
                SetPrivateField(form, "saveToolbarLayoutSetting", new Action<string>(value => { }));
                form.Show();
                PumpMessages(180);

                ToolStrip debug = FindControl<ToolStrip>(form, "toolStripDebug");
                ToolStrip file = FindControl<ToolStrip>(form, "toolStripMain");
                ToolStrip code = FindControl<ToolStrip>(form, "toolStripCode");
                Require(debug.Top == file.Top && file.Top == code.Top && debug.Left < file.Left && file.Left < code.Left,
                    "Il ripristino non conserva riga e ordine delle barre salvate.");
                Require(!FindControl<ToolStrip>(form, "toolStripView").Visible,
                    "Il ripristino non conserva una barra nascosta.");
                string roundTrip = null;
                SetPrivateField(form, "saveToolbarLayoutSetting", new Action<string>(value => roundTrip = value));
                InvokePrivate(form, "persistToolbarLayout");
                Require(roundTrip != null && roundTrip.Contains("debug.row=0") &&
                        roundTrip.Contains("debug.order=0") && roundTrip.Contains("file.order=1") &&
                        roundTrip.Contains("code.order=2"),
                    "Il salvataggio non conserva esplicitamente riga e ordine delle barre.");
            }
            Console.WriteLine("PASS ripristino deterministico di posizione, riga, ordine e visibilità toolbar");
        }

        private static void AssertPerformanceBudgets(bool visible)
        {
            using (var form = ShowSmokeForm(visible))
            {
                RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                string source = CreatePerformanceSource();
                editor.Text = source;
                editor.Select(0, 0);
                StopInteractiveTimers(form);
                InvokePrivate(form, "resetActiveAnalysis");
                InvokePrivate(form, "applyHighlighting");
                PumpMessages(40);

                Control gutter = FindControl<RichTextBox>(form, "richTextBoxLineNumbers");
                int initialOutlineRenders = GetPrivateValue<int>(form, "outlineRenderCount");
                int initialDiagnosticsRenders = GetPrivateValue<int>(form, "diagnosticsRenderCount");
                int middlePosition = editor.GetFirstCharIndexFromLine(EditorPerformanceBudgets.ReferenceDocumentLines / 2);
                editor.Select(middlePosition, 0);
                editor.ScrollToCaret();
                PumpMessages(20);
                int initialGutterUpdates = GetPropertyValue<int>(gutter, "ContentUpdateCount");
                int firstVisibleLine = editor.GetLineFromCharIndex(
                    editor.GetCharIndexFromPosition(new Point(1, 1)));
                int invalidations = 0;
                editor.Invalidated += (sender, args) => invalidations++;
                InvokePrivate(form, "applyHighlighting");
                int firstVisibleLineAfterHighlight = editor.GetLineFromCharIndex(
                    editor.GetCharIndexFromPosition(new Point(1, 1)));
                for (int index = 0; index < 20; index++) InvokePrivate(form, "updateLineNumbers");
                Require(GetPropertyValue<int>(gutter, "ContentUpdateCount") == initialGutterUpdates &&
                        GetPrivateValue<int>(form, "outlineRenderCount") == initialOutlineRenders &&
                        GetPrivateValue<int>(form, "diagnosticsRenderCount") == initialDiagnosticsRenders,
                    "Analisi e gutter identici ricostruiscono ancora controlli UI invariati.");
                Require(invalidations <= 2 && firstVisibleLineAfterHighlight == firstVisibleLine,
                    "La colorazione identica provoca ridisegni multipli o sposta la viewport.");

                var inputSamples = new List<double>();
                editor.Select(editor.TextLength, 0);
                PumpMessages(20);
                int gutterUpdatesBeforeInput = GetPropertyValue<int>(gutter, "ContentUpdateCount");
                for (int index = 0; index < EditorPerformanceBudgets.InteractiveSampleCount; index++)
                {
                    inputSamples.Add(MeasureMilliseconds(() => editor.SelectedText = " "));
                    StopInteractiveTimers(form);
                }
                Require(GetPropertyValue<int>(gutter, "ContentUpdateCount") == gutterUpdatesBeforeInput,
                    "La digitazione senza variazioni di riga ridisegna ancora il gutter.");
                AssertPerformanceBudget(EditorPerformanceBudgets.Input, inputSamples);

                var selectionSamples = new List<double>();
                for (int index = 0; index < EditorPerformanceBudgets.InteractiveSampleCount; index++)
                {
                    int position = (int)((long)(source.Length - 1) * index /
                        Math.Max(1, EditorPerformanceBudgets.InteractiveSampleCount - 1));
                    selectionSamples.Add(MeasureMilliseconds(() => editor.Select(position, Math.Min(3, editor.TextLength - position))));
                }
                AssertPerformanceBudget(EditorPerformanceBudgets.Selection, selectionSamples);

                var scrollSamples = new List<double>();
                int gutterUpdatesBeforeScroll = GetPropertyValue<int>(gutter, "ContentUpdateCount");
                for (int index = 0; index < EditorPerformanceBudgets.InteractiveSampleCount; index++)
                {
                    int line = (EditorPerformanceBudgets.ReferenceDocumentLines - 1) * index /
                        Math.Max(1, EditorPerformanceBudgets.InteractiveSampleCount - 1);
                    int position = Math.Max(0, editor.GetFirstCharIndexFromLine(line));
                    scrollSamples.Add(MeasureMilliseconds(() =>
                    {
                        editor.Select(position, 0);
                        editor.ScrollToCaret();
                        InvokeHandler(form, "richTextBoxMainEditor_VScroll", editor, EventArgs.Empty);
                    }));
                }
                Require(GetPropertyValue<int>(gutter, "ContentUpdateCount") > gutterUpdatesBeforeScroll,
                    "Lo smoke dello scroll non ha attraversato viewport differenti.");
                AssertPerformanceBudget(EditorPerformanceBudgets.Scroll, scrollSamples);

                var highlightSamples = new List<double>();
                var highlightCoreSamples = new List<double>();
                var highlightApplySamples = new List<double>();
                var highlightRtfBuildSamples = new List<double>();
                var highlightRtfStreamSamples = new List<double>();
                for (int index = 0; index < 3; index++)
                {
                    InvokePrivate(form, "resetActiveAnalysis");
                    editor.ClearUndo();
                    highlightSamples.Add(MeasureMilliseconds(() => InvokePrivate(form, "applyHighlighting")));
                    highlightCoreSamples.Add(GetPrivateValue<double>(form,
                        "lastAnalysisCoreMilliseconds"));
                    highlightApplySamples.Add(GetPrivateValue<double>(form,
                        "lastAnalysisApplyMilliseconds"));
                    highlightRtfBuildSamples.Add(GetPrivateValue<double>(form,
                        "lastSyntaxRtfBuildMilliseconds"));
                    highlightRtfStreamSamples.Add(GetPrivateValue<double>(form,
                        "lastSyntaxRtfStreamMilliseconds"));
                }
                AssertPerformanceBudget(EditorPerformanceBudgets.Highlighting, highlightSamples);
                Console.WriteLine("Profilo colorazione: totale [" +
                    string.Join(", ", highlightSamples.Select(value => value.ToString("F1"))) +
                    "] ms; Core [" +
                    string.Join(", ", highlightCoreSamples.Select(value => value.ToString("F1"))) +
                    "] ms; UI [" +
                    string.Join(", ", highlightApplySamples.Select(value => value.ToString("F1"))) +
                    "] ms; RTF build [" +
                    string.Join(", ", highlightRtfBuildSamples.Select(value => value.ToString("F1"))) +
                    "] ms; RTF stream [" +
                    string.Join(", ", highlightRtfStreamSamples.Select(value => value.ToString("F1"))) + "] ms.");

                IReadOnlyList<CommandPaletteCommand> commands =
                    (IReadOnlyList<CommandPaletteCommand>)InvokePrivate(form, "createCommandPaletteCommands");
                var popupSamples = new List<double>();
                for (int index = 0; index < 5; index++)
                {
                    using (var dialog = new CommandPaletteForm(commands))
                    {
                        dialog.StartPosition = FormStartPosition.Manual;
                        dialog.Location = visible ? new Point(120, 120) : new Point(-32000, -32000);
                        popupSamples.Add(MeasureMilliseconds(() =>
                        {
                            dialog.Show(form);
                            Application.DoEvents();
                        }));
                        dialog.Close();
                    }
                }
                AssertPerformanceBudget(EditorPerformanceBudgets.Popup, popupSamples);

                string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", ".."));
                string flowchartSource = File.ReadAllText(Path.Combine(projectRoot, "samples",
                    "flowchart-complex-smoke-test.ddf"));
                var flowchartSamples = new List<double>();
                DdfFlowchartLayout measuredLayout = null;
                for (int index = 0; index < 20; index++)
                {
                    flowchartSamples.Add(MeasureMilliseconds(() =>
                    {
                        DdfLanguageDefinition language = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package.Language;
                        DdfFlowchart graph = DdfFlowchartBuilder.Build(flowchartSource, "main", language).Flowchart;
                        measuredLayout = DdfFlowchartLayouter.Layout(graph);
                        DdfFlowchart projection = DdfFlowchartProjection.CollapseLinearSequences(graph, 3);
                        DdfFlowchartLayouter.Layout(projection);
                    }));
                }
                Require(measuredLayout != null && measuredLayout.Nodes.Count > 20,
                    "Il carico Flowchart misurato non rappresenta un diagramma complesso.");
                AssertPerformanceBudget(EditorPerformanceBudgets.Flowchart, flowchartSamples);
            }
        }

        private static string CreatePerformanceSource()
        {
            return CreateGeneratedSource(EditorPerformanceBudgets.ReferenceDocumentLines, "main");
        }

        private static void AssertLargeDocumentAndWorkspaceResponsiveness(bool visible)
        {
            string firstDirectory = Path.Combine(Path.GetTempPath(), "ddf-workspace-obsolete-" + Guid.NewGuid().ToString("N"));
            string targetDirectory = Path.Combine(Path.GetTempPath(), "ddf-workspace-large-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(firstDirectory);
            Directory.CreateDirectory(targetDirectory);
            try
            {
                DdfDocumentFile.Save(Path.Combine(firstDirectory, "blocked.ddf"),
                    CreateGeneratedSource(EditorPerformanceBudgets.RealisticWorkspaceLinesPerDocument, "blocked"));
                for (int index = 0; index < EditorPerformanceBudgets.RealisticWorkspaceDocuments; index++)
                {
                    string path = Path.Combine(targetDirectory, "module" + index.ToString("D2") + ".ddf");
                    DdfDocumentFile.Save(path, CreateGeneratedSource(
                        EditorPerformanceBudgets.RealisticWorkspaceLinesPerDocument, "function" + index.ToString("D2")));
                }

                using (var form = ShowSmokeForm(visible))
                using (var firstStarted = new ManualResetEventSlim(false))
                {
                    int firstCancelled = 0;
                    SetPrivateField(form, "loadWorkspaceIndex",
                        new Func<string, CancellationToken, DdfWorkspaceIndex>((path, token) =>
                        {
                            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(firstDirectory),
                                StringComparison.OrdinalIgnoreCase))
                            {
                                firstStarted.Set();
                                try
                                {
                                    while (true)
                                    {
                                        token.ThrowIfCancellationRequested();
                                        Thread.Sleep(5);
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    Interlocked.Exchange(ref firstCancelled, 1);
                                    throw;
                                }
                            }
                            return DdfWorkspaceIndex.Load(path, token);
                        }));

                    bool firstAccepted = false;
                    var workspaceDispatchSamples = new List<double>
                    {
                        MeasureMilliseconds(() => firstAccepted = (bool)InvokePrivate(form, "openWorkspace", firstDirectory))
                    };
                    Require(firstAccepted, "Il primo workspace realistico non viene accettato.");
                    PumpUntil(() => firstStarted.IsSet, 3000,
                        "Il caricamento workspace in background non è stato avviato.");
                    bool heartbeat = false;
                    form.BeginInvoke(new Action(() => heartbeat = true));
                    PumpUntil(() => heartbeat, 1000,
                        "Il thread UI non resta reattivo durante l'indicizzazione workspace.");

                    bool targetAccepted = false;
                    Stopwatch workspaceCompletion = Stopwatch.StartNew();
                    workspaceDispatchSamples.Add(MeasureMilliseconds(() =>
                        targetAccepted = (bool)InvokePrivate(form, "openWorkspace", targetDirectory)));
                    Require(targetAccepted, "Il workspace sostitutivo non viene accettato.");
                    PumpUntil(() => Volatile.Read(ref firstCancelled) == 1, 3000,
                        "Una richiesta workspace obsoleta non viene cancellata.");
                    Label workspaceLabel = FindControl<Label>(form, "labelWorkspace");
                    PumpUntil(() => workspaceLabel.Text.Contains(
                            EditorPerformanceBudgets.RealisticWorkspaceDocuments + " file", StringComparison.Ordinal),
                        12000, "Il workspace realistico non completa l'indicizzazione.");
                    DdfWorkspaceIndex workspace = GetPrivateField<DdfWorkspaceIndex>(form, "workspaceIndex");
                    Require(workspace != null &&
                            string.Equals(workspace.RootPath, Path.GetFullPath(targetDirectory), StringComparison.OrdinalIgnoreCase) &&
                            workspace.Documents.Count == EditorPerformanceBudgets.RealisticWorkspaceDocuments &&
                            GetPrivateValue<long>(form, "workspaceLoadAppliedVersion") ==
                            GetPrivateValue<long>(form, "workspaceLoadRequestVersion"),
                        "Un risultato workspace obsoleto sostituisce ancora la richiesta più recente.");
                    workspaceCompletion.Stop();
                    AssertPerformanceBudget(EditorPerformanceBudgets.WorkspaceLoadDispatch, workspaceDispatchSamples);
                    AssertPerformanceBudget(EditorPerformanceBudgets.WorkspaceLoadCompletion,
                        new[] { workspaceCompletion.Elapsed.TotalMilliseconds });

                    string activePath = Path.Combine(targetDirectory, "module00.ddf");
                    Require((bool)InvokePrivate(form, "openDocument", activePath),
                        "Il documento del workspace realistico non viene aperto.");
                    RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                    string largeSource = CreateGeneratedSource(EditorPerformanceBudgets.RealisticDocumentLines, "main");
                    editor.Text = largeSource;
                    editor.Select(editor.TextLength, 0);
                    StopInteractiveTimers(form);
                    // Establish the initial syntax projection before measuring
                    // interactive edits and completion on the large document.
                    // Otherwise an already queued full-document formatting pass
                    // is charged to the unrelated completion budget.
                    InvokePrivate(form, "applyHighlighting");
                    var largeInputSamples = new List<double>();
                    for (int index = 0; index < 20; index++)
                    {
                        largeInputSamples.Add(MeasureMilliseconds(() => editor.SelectedText = " "));
                        StopInteractiveTimers(form);
                    }
                    AssertPerformanceBudget(EditorPerformanceBudgets.LargeWorkspaceInput, largeInputSamples);

                    DdfLanguageDefinition completionProfileLanguage = DdfLanguageCatalog.Default;
                    DdfLexResult completionProfileLex = null;
                    DdfParseResult completionProfileParse = null;
                    IReadOnlyList<DdfDocumentSymbol> completionProfileSymbols = null;
                    DdfSemanticModel completionProfileSemantics = null;
                    double completionLexMilliseconds = MeasureMilliseconds(() =>
                        completionProfileLex = DdfLexer.Lex(largeSource, completionProfileLanguage));
                    double completionParseMilliseconds = MeasureMilliseconds(() =>
                        completionProfileParse = DdfParser.ParseForEditorFeatures(largeSource,
                            completionProfileLex, completionProfileLanguage));
                    double completionSymbolMilliseconds = MeasureMilliseconds(() =>
                        completionProfileSymbols = DdfSymbolIndex.Create(
                            completionProfileParse, largeSource).Symbols);
                    double completionSemanticMilliseconds = MeasureMilliseconds(() =>
                        completionProfileSemantics = DdfSemanticModel.Create(
                            largeSource, completionProfileParse.Root));
                    GC.KeepAlive(completionProfileSymbols);
                    GC.KeepAlive(completionProfileSemantics);
                    Console.WriteLine("Profilo preparazione completamento: lex " +
                        completionLexMilliseconds.ToString("F1") + " ms; parse " +
                        completionParseMilliseconds.ToString("F1") + " ms; simboli " +
                        completionSymbolMilliseconds.ToString("F1") + " ms; semantica " +
                        completionSemanticMilliseconds.ToString("F1") + " ms.");

                    var completionSamples = new List<double>();
                    for (int index = 0;
                        index < EditorPerformanceBudgets.RealisticCompletionSamples;
                        index++)
                    {
                        editor.Select(editor.TextLength, 0);
                        long previousRequest = GetPrivateValue<long>(form,
                            "completionRequestVersion");
                        Stopwatch completion = Stopwatch.StartNew();
                        InvokePrivate(form, "showCompletion", true);
                        long request = GetPrivateValue<long>(form,
                            "completionRequestVersion");
                        Require(request > previousRequest,
                            "Il completamento realistico non crea una nuova richiesta.");
                        PumpUntil(() => GetPrivateValue<long>(form,
                                "completionAppliedVersion") == request,
                            5000, "Il completamento realistico non viene applicato.");
                        completion.Stop();
                        completionSamples.Add(completion.Elapsed.TotalMilliseconds);
                        DdfCompletionResult result = GetPrivateField<DdfCompletionResult>(
                            form, "activeCompletionResult");
                        Require(result != null && result.Items.Count > 0,
                            "Il carico di completamento non produce candidati.");
                        InvokePrivate(form, "hideCompletion");
                    }
                    AssertPerformanceBudget(EditorPerformanceBudgets.Completion,
                        completionSamples);

                    var analysisCompletionSamples = new List<double>();
                    var analysisCoreSamples = new List<double>();
                    var analysisApplySamples = new List<double>();
                    var analysisPreparationSamples = new List<double>();
                    var analysisFormattingSamples = new List<double>();
                    var analysisUiServicesSamples = new List<double>();
                    var analysisProfiles = new List<DdfEditorAnalysisProfile>();
                    for (int index = 0; index < 3; index++)
                    {
                        editor.Text = largeSource + new string(' ', index + 1);
                        editor.Select(editor.TextLength, 0);
                        StopInteractiveTimers(form);
                        Stopwatch completion = Stopwatch.StartNew();
                        InvokePrivate(form, "startBackgroundAnalysis");
                        long requested = GetPrivateValue<long>(form,
                            "analysisRequestVersion");
                        PumpUntil(() => GetPrivateValue<long>(form,
                                "analysisAppliedVersion") == requested &&
                                string.Equals(GetPrivateValue<string>(form,
                                    "lastAnalyzedText"), editor.Text,
                                    StringComparison.Ordinal),
                            12000, "L'analisi realistica non completa l'applicazione UI.");
                        completion.Stop();
                        analysisCompletionSamples.Add(
                            completion.Elapsed.TotalMilliseconds);
                        analysisCoreSamples.Add(GetPrivateValue<double>(form,
                            "lastAnalysisCoreMilliseconds"));
                        analysisApplySamples.Add(GetPrivateValue<double>(form,
                            "lastAnalysisApplyMilliseconds"));
                        analysisPreparationSamples.Add(GetPrivateValue<double>(form,
                            "lastAnalysisPreparationMilliseconds"));
                        analysisFormattingSamples.Add(GetPrivateValue<double>(form,
                            "lastAnalysisFormattingMilliseconds"));
                        analysisUiServicesSamples.Add(GetPrivateValue<double>(form,
                            "lastAnalysisUiServicesMilliseconds"));
                        analysisProfiles.Add(GetPrivateField<DdfEditorAnalysisProfile>(
                            form, "lastAnalysisProfile"));
                    }
                    AssertPerformanceBudget(
                        EditorPerformanceBudgets.BackgroundAnalysisCompletion,
                        analysisCompletionSamples);
                    Console.WriteLine("Profilo analisi: totale [" +
                        string.Join(", ", analysisCompletionSamples.Select(value => value.ToString("F1"))) +
                        "] ms; Core [" +
                        string.Join(", ", analysisCoreSamples.Select(value => value.ToString("F1"))) +
                        "] ms; UI [" +
                        string.Join(", ", analysisApplySamples.Select(value => value.ToString("F1"))) +
                        "] ms; prep [" +
                        string.Join(", ", analysisPreparationSamples.Select(value => value.ToString("F1"))) +
                        "] ms; format [" +
                        string.Join(", ", analysisFormattingSamples.Select(value => value.ToString("F1"))) +
                        "] ms; servizi [" +
                        string.Join(", ", analysisUiServicesSamples.Select(value => value.ToString("F1"))) + "] ms.");
                    for (int index = 0; index < analysisProfiles.Count; index++)
                    {
                        DdfEditorAnalysisProfile profile = analysisProfiles[index];
                        Console.WriteLine("Profilo Core " + (index + 1) +
                            ": lex=" + profile.LexMilliseconds.ToString("F1") +
                            ", parse=" + profile.ParseMilliseconds.ToString("F1") +
                            ", semantica=" + profile.SemanticMilliseconds.ToString("F1") +
                            ", tipi=" + profile.TypeCheckMilliseconds.ToString("F1") +
                            ", IR=" + profile.IrMilliseconds.ToString("F1") +
                            ", simboli=" + profile.SymbolMilliseconds.ToString("F1") +
                            ", folding=" + profile.FoldingMilliseconds.ToString("F1") +
                            ", completion=" + profile.CompletionMilliseconds.ToString("F1") +
                            ", diagnostiche=" + profile.DiagnosticsMilliseconds.ToString("F1") + " ms.");
                    }

                    var analysisDispatchSamples = new List<double>();
                    analysisDispatchSamples.Add(MeasureMilliseconds(() => InvokePrivate(form, "startBackgroundAnalysis")));
                    long obsoleteAnalysisVersion = GetPrivateValue<long>(form, "analysisRequestVersion");
                    editor.Text = Source;
                    StopInteractiveTimers(form);
                    analysisDispatchSamples.Add(MeasureMilliseconds(() => InvokePrivate(form, "startBackgroundAnalysis")));
                    long currentAnalysisVersion = GetPrivateValue<long>(form, "analysisRequestVersion");
                    Require(currentAnalysisVersion > obsoleteAnalysisVersion,
                        "La nuova analisi non supera la versione del documento grande obsoleto.");
                    PumpUntil(() => GetPrivateValue<long>(form, "analysisAppliedVersion") == currentAnalysisVersion &&
                                    string.Equals(GetPrivateValue<string>(form, "lastAnalyzedText"), Source,
                                        StringComparison.Ordinal),
                        12000, "L'analisi corrente non sostituisce lo snapshot grande cancellato.");
                    AssertPerformanceBudget(EditorPerformanceBudgets.BackgroundAnalysisDispatch, analysisDispatchSamples);
                }
            }
            finally
            {
                if (Directory.Exists(firstDirectory)) Directory.Delete(firstDirectory, true);
                if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, true);
            }
        }

        private static void AssertDebuggerPerformance(bool visible)
        {
            using var host = new Form
            {
                ShowInTaskbar = visible,
                StartPosition = FormStartPosition.Manual,
                Location = visible ? new Point(80, 80) :
                    new Point(-32000, -32000),
                Size = new Size(900, 650)
            };
            using var view = new DdfEditorView { Dock = DockStyle.Fill };
            var documents = new OpenDocumentCollection();
            OpenDocumentBuffer document = (OpenDocumentBuffer)documents.CreateVirtual(
                "debug-performance.ddf",
                CreateGeneratedSource(EditorPerformanceBudgets.ReferenceDocumentLines,
                    "main"));
            view.AttachDocumentService(documents);
            host.Controls.Add(view);
            host.Show();
            view.CreateControl();
            PumpMessages(80);

            const int breakpointLine = 250;
            view.DebugService.SetBreakpoints(new[]
                { new DdfEditorBreakpoint(document.Id, breakpointLine) });
            var startSamples = new List<double>();
            var stepSamples = new List<double>();
            for (int index = 0;
                index < EditorPerformanceBudgets.RealisticDebuggerSamples;
                index++)
            {
                Stopwatch start = Stopwatch.StartNew();
                Task<DdfEditorExecutionCompletion> run =
                    view.DebugService.RunAsync();
                PumpUntil(() => view.DebugService.IsPaused &&
                        view.DebugService.CurrentLocation?.Line == breakpointLine,
                    5000, "Il debugger prestazionale non raggiunge il breakpoint.");
                start.Stop();
                startSamples.Add(start.Elapsed.TotalMilliseconds);

                int lineBeforeStep = view.DebugService.CurrentLocation.Line;
                Stopwatch step = Stopwatch.StartNew();
                Require(view.DebugService.StepOver(),
                    "Step Over non viene accettato dal debugger prestazionale.");
                PumpUntil(() => view.DebugService.IsPaused &&
                        view.DebugService.CurrentLocation != null &&
                        view.DebugService.CurrentLocation.Line != lineBeforeStep,
                    3000, "Step Over non raggiunge l'istruzione successiva.");
                step.Stop();
                stepSamples.Add(step.Elapsed.TotalMilliseconds);

                Task<DdfEditorLifecycleResult> stop =
                    ((IDdfEditorAsyncLifecycleService)view.DebugService)
                    .StopAsync(TimeSpan.FromSeconds(5));
                PumpUntil(() => stop.IsCompleted && run.IsCompleted, 6000,
                    "Il debugger prestazionale non termina dopo Stop.");
                Require(stop.Result.Succeeded && run.Result.WasCancelled,
                    "Stop non chiude deterministicamente la sessione misurata.");
            }

            AssertPerformanceBudget(
                EditorPerformanceBudgets.DebugStartToBreakpoint, startSamples);
            AssertPerformanceBudget(EditorPerformanceBudgets.DebugStep,
                stepSamples);
        }

        private static void AssertRealisticLanguagePackageSwitching(bool visible)
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-package-stress-" + Guid.NewGuid().ToString("N"));
            string firstPackage = Path.Combine(root, "first");
            string secondPackage = Path.Combine(root, "second");
            string workspaceRoot = Path.Combine(root, "workspace");
            try
            {
                CreateRealisticLanguagePackage(firstPackage, "DDF Stress A", "ak");
                CreateRealisticLanguagePackage(secondPackage, "DDF Stress B", "bk");
                Directory.CreateDirectory(workspaceRoot);
                for (int index = 0; index < EditorPerformanceBudgets.RealisticWorkspaceDocuments; index++)
                {
                    DdfDocumentFile.Save(Path.Combine(workspaceRoot, "module" + index.ToString("D2") + ".ddf"),
                        CreateGeneratedSource(EditorPerformanceBudgets.RealisticWorkspaceLinesPerDocument,
                            "stress" + index.ToString("D2")));
                }

                using (var form = ShowSmokeForm(visible))
                {
                    SetPrivateField(form, "saveLanguagePackagePathSetting", new Action<string>(value => { }));
                    InvokePrivate(form, "activateLanguagePackage",
                        Path.Combine(AppContext.BaseDirectory, "language-packages", "ddf"));
                    Require(GetPrivateField<DdfLanguagePackageSession>(form, "editorLanguageSession")
                                .ActivePackage.Id == "ddf",
                        "Il test non riesce a ripristinare il profilo DDF canonico.");
                    Require((bool)InvokePrivate(form, "openWorkspace", workspaceRoot),
                        "Il workspace per il cambio pacchetto non viene accettato.");
                    PumpUntil(() => GetPrivateField<DdfWorkspaceIndex>(form, "workspaceIndex")?.Documents.Count ==
                            EditorPerformanceBudgets.RealisticWorkspaceDocuments,
                        12000, "Il workspace per il cambio pacchetto non completa il caricamento iniziale.");

                    RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                    editor.Text = CreateGeneratedSource(EditorPerformanceBudgets.RealisticDocumentLines, "main");
                    editor.Select(editor.TextLength, 0);
                    StopInteractiveTimers(form);
                    InvokePrivate(form, "startBackgroundAnalysis");

                    var samples = new List<double>();
                    for (int index = 0; index < EditorPerformanceBudgets.RealisticPackageSwitches; index++)
                    {
                        string packagePath = index % 2 == 0 ? firstPackage : secondPackage;
                        samples.Add(MeasureMilliseconds(() => InvokePrivate(form, "activateLanguagePackage", packagePath)));
                        PumpMessages(10);
                    }

                    long currentRevision = GetPrivateField<IDdfLanguagePackageSession>(form,
                        "editorLanguageSession").Revision;
                    PumpUntil(() =>
                        GetPrivateValue<long>(form, "analysisAppliedLanguageRevision") == currentRevision &&
                        GetPrivateValue<long>(form, "workspaceLoadAppliedLanguageRevision") == currentRevision &&
                        GetPrivateValue<long>(form, "workspaceLoadAppliedVersion") ==
                            GetPrivateValue<long>(form, "workspaceLoadRequestVersion"),
                        15000, "Uno snapshot del vecchio pacchetto è rimasto applicato dopo il cambio linguaggio.");

                    editor.Text = "bk";
                    editor.Select(editor.TextLength, 0);
                    editor.Focus();
                    InvokePrivate(form, "showCompletion", true);
                    PumpUntil(() => GetPrivateValue<long>(form, "completionAppliedLanguageRevision") == currentRevision,
                        5000, "Il completamento non applica lo snapshot del pacchetto corrente.");
                    DdfCompletionResult completion = GetPrivateField<DdfCompletionResult>(form, "activeCompletionResult");
                    Require(completion != null && completion.Items.Any(item => item.DisplayText == "bk000"),
                        "Il completamento non riflette il contenuto del pacchetto finale.");
                    Require(FindControl<StatusStrip>(form, "statusStripMain").Items.Cast<ToolStripItem>()
                            .Any(item => item.Name == "statusEncodingLabel" && item.Text.Contains("DDF Stress B")),
                        "La barra di stato non riflette il pacchetto finale dello stress test.");
                    AssertPerformanceBudget(EditorPerformanceBudgets.LanguagePackageSwitch, samples);
                }
            }
            finally
            {
                DdfLanguageCatalog.ActivateEmbedded();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void AssertInstalledProfilesAndCloseAllWorkspaceDocuments(bool visible)
        {
            string workspaceRoot = Path.Combine(Path.GetTempPath(),
                "ddf-profile-close-all-" + Guid.NewGuid().ToString("N"));
            string previousUserData = Environment.GetEnvironmentVariable("DDF_LANGUAGE_EDITOR_USER_DATA");
            try
            {
                Directory.CreateDirectory(workspaceRoot);
                string userDataRoot = Path.Combine(workspaceRoot, "user-data");
                Environment.SetEnvironmentVariable("DDF_LANGUAGE_EDITOR_USER_DATA", userDataRoot);
                Directory.CreateDirectory(userDataRoot);
                File.WriteAllText(Path.Combine(userDataRoot, "language-package.txt"),
                    Path.Combine(AppContext.BaseDirectory, "language-package-seeds", "ddf-c-familiar"),
                    new UTF8Encoding(false));
                string firstPath = Path.Combine(workspaceRoot, "main.ddf");
                string secondPath = Path.Combine(workspaceRoot, "helpers.ddf");
                DdfDocumentFile.Save(firstPath, "main() out int { ret helper(); }");
                DdfDocumentFile.Save(secondPath, "helper() out int { ret 1; }");

                using (var form = ShowSmokeForm(visible))
                {
                    string migratedPreference = File.ReadAllText(
                        Path.Combine(userDataRoot, "language-package.txt"), Encoding.UTF8);
                    Require(GetPrivateField<DdfLanguagePackageSession>(form, "editorLanguageSession")
                                .ActivePackage.Id == "ddf-c-familiar" &&
                            migratedPreference.StartsWith(Path.Combine(userDataRoot, "language-packages"),
                                StringComparison.OrdinalIgnoreCase),
                        "La preferenza del vecchio profilo distribuito non è stata migrata al catalogo utente.");
                    SetPrivateField(form, "saveLanguagePackagePathSetting", new Action<string>(value => { }));
                    InvokePrivate(form, "activateLanguagePackage",
                        Path.Combine(AppContext.BaseDirectory, "language-packages", "ddf"));
                    Require(GetPrivateField<DdfLanguagePackageSession>(form, "editorLanguageSession")
                                .ActivePackage.Id == "ddf",
                        "Il test non riesce a ripristinare il profilo DDF canonico.");
                    Require((bool)InvokePrivate(form, "openWorkspace", workspaceRoot),
                        "Il workspace del test profili non viene accettato.");
                    PumpUntil(() => GetPrivateField<DdfWorkspaceIndex>(form, "workspaceIndex")?.Documents.Count == 2,
                        5000, "Il workspace .ddf non completa il caricamento iniziale.");
                    Require(GetPrivateField<DdfLanguagePackageSession>(form, "editorLanguageSession")
                                .ActivePackage.Id == "ddf",
                        "Il caricamento del workspace ha sostituito il profilo DDF canonico.");
                    Require((bool)InvokePrivate(form, "openDocument", firstPath) &&
                            (bool)InvokePrivate(form, "openDocument", secondPath),
                        "I documenti workspace del test non vengono aperti.");
                    Require(GetPrivateField<DdfLanguagePackageSession>(form, "editorLanguageSession")
                                .ActivePackage.Id == "ddf",
                        "L'apertura dei documenti ha sostituito il profilo DDF canonico.");

                    OpenDocumentCollection documents = GetPrivateField<OpenDocumentCollection>(form, "openDocuments");
                    string userPackagesRoot = Path.Combine(userDataRoot, "language-packages");
                    foreach (string profileId in new[] { "ddf-it-didactic", "ddf-c-familiar" })
                    {
                        string seedPath = Path.Combine(AppContext.BaseDirectory, "language-package-seeds", profileId);
                        DdfLanguagePackageLoadResult seed = DdfLanguagePackageLoader.Load(seedPath);
                        Require(seed.Success, "Il pacchetto iniziale " + profileId + " non è valido: " +
                            string.Join(" | ", seed.Diagnostics.Select(item => item.ToString())));
                    }
                    string includedRoot = Path.Combine(AppContext.BaseDirectory, "language-packages");
                    string[] includedIds = Directory.EnumerateDirectories(includedRoot)
                        .Where(path => File.Exists(Path.Combine(path, "manifest.json")))
                        .Select(Path.GetFileName).ToArray();
                    Require(includedIds.Length == 1 && includedIds[0] == "ddf",
                        "La distribuzione deve includere soltanto il pacchetto DDF.");
                    var installedPackages = new DdfLanguagePackageStore(userPackagesRoot).List();
                    Require(installedPackages.Any(item => item.Id == "ddf-it-didactic") &&
                            installedPackages.Any(item => item.Id == "ddf-c-familiar"),
                        "I profili aggiuntivi non sono presenti nel catalogo utente. " +
                        string.Join(" | ", GetPrivateField<IReadOnlyList<DdfLanguagePackageDiagnostic>>(form,
                            "languagePackageActivationDiagnostics").Select(item => item.ToString())));
                    var profileCases = new[]
                    {
                        new
                        {
                            Id = "ddf-it-didactic",
                            Name = "DDF didattico italiano",
                            Source = "helper() restituisce int { restituisci 2; }",
                            PreviousSource = "helper() out int { ret 1; }",
                            ConvertedSource = "helper() restituisce int { restituisci 1; }",
                            ConvertedWorkspaceMain = "main() restituisce int { restituisci helper(); }",
                            TrueLabel = "Sì",
                            InvalidSource = "helper() restituisce int { restituisci missing; }",
                            DiagnosticMessage = "Questo nome non è stato dichiarato: dichiaralo oppure correggine la grafia."
                        },
                        new
                        {
                            Id = "ddf-c-familiar",
                            Name = "DDF sintassi familiare C",
                            Source = "helper() returns int { return 2; }",
                            PreviousSource = "helper() restituisce int { restituisci 2; }",
                            ConvertedSource = "helper() returns int { return 2; }",
                            ConvertedWorkspaceMain = "main() returns int { return helper(); }",
                            TrueLabel = "Yes",
                            InvalidSource = "helper() returns int { return missing; }",
                            DiagnosticMessage = "Unresolved symbol."
                        }
                    };
                    foreach (var profile in profileCases)
                    {
                        string expectedPreviousId = profile.Id == "ddf-it-didactic" ? "ddf" : "ddf-it-didactic";
                        Require(GetPrivateField<DdfLanguagePackageSession>(form, "editorLanguageSession")
                                    .ActivePackage.Id == expectedPreviousId,
                            "Il profilo precedente è cambiato in modo inatteso prima di " + profile.Id + ".");
                        DdfInstalledLanguagePackage installed = installedPackages
                            .Where(item => item.Id == profile.Id)
                            .OrderByDescending(item => Version.Parse(item.Version)).First();
                        InvokePrivate(form, "activateLanguagePackage", installed.Path);
                        PumpUntil(() => GetPrivateField<DdfLanguagePackageSession>(form, "editorLanguageSession")
                                             .ActivePackage.Id == profile.Id &&
                                         GetPrivateField<DdfWorkspaceIndex>(form, "workspaceIndex")?.Documents.Count == 2,
                            5000, "Il profilo " + profile.Id + " non conserva il workspace .ddf attivo.");
                        Require(FindControl<StatusStrip>(form, "statusStripMain").Items.Cast<ToolStripItem>()
                                .Any(status => status.Name == "statusEncodingLabel" && status.Text.Contains(profile.Name)),
                            "La barra di stato non rende visibile il profilo " + profile.Id + ".");
                        Require(FindControl<RichTextBox>(form, "richTextBoxOutput").Text.Contains(
                                "Linguaggio attivo: " + profile.Name, StringComparison.Ordinal),
                            "L'Output non conferma visibilmente il profilo installato " + profile.Id + ".");
                        RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                        Require(editor.Text == profile.ConvertedSource && editor.CanUndo,
                            "Il documento attivo non è stato convertito automaticamente in " + profile.Id +
                            ". Testo corrente: " + editor.Text.Replace("\r", "\\r").Replace("\n", "\\n") +
                            ". Output: " + FindControl<RichTextBox>(form, "richTextBoxOutput").Text);
                        Require(documents.FindByPath(firstPath).Source == profile.ConvertedWorkspaceMain,
                            "La conversione non ha aggiornato anche le altre schede aperte in " + profile.Id + ".");
                        Require(DdfDocumentFile.Load(firstPath) == "main() out int { ret helper(); }",
                            "Il cambio profilo ha salvato implicitamente il file workspace su disco.");
                        editor.Undo();
                        Require(editor.Text == profile.PreviousSource,
                            "Undo non ripristina la sintassi precedente dopo il cambio profilo.");
                        editor.Redo();
                        Require(editor.Text == profile.ConvertedSource,
                            "Redo non riapplica la conversione del profilo.");
                        editor.Text = profile.Source;
                        editor.Select(editor.TextLength, 0);
                        StopInteractiveTimers(form);
                        InvokePrivate(form, "startBackgroundAnalysis");
                        long requestedAnalysis = GetPrivateValue<long>(form, "analysisRequestVersion");
                        PumpUntil(() => GetPrivateValue<long>(form, "analysisAppliedVersion") >= requestedAnalysis &&
                                        string.Equals(GetPrivateValue<string>(form, "lastAnalyzedText"),
                                            profile.Source, StringComparison.Ordinal) &&
                                        GetPrivateValue<long>(form, "analysisAppliedLanguageRevision") ==
                                            GetPrivateField<IDdfLanguagePackageSession>(form,
                                                "editorLanguageSession").Revision,
                            5000, "L'analisi non viene applicata dopo l'attivazione di " + profile.Id + ".");
                        IReadOnlyList<DdfDiagnostic> profileDiagnostics =
                            GetPrivateField<IReadOnlyList<DdfDiagnostic>>(form, "activeDiagnostics");
                        Require(profileDiagnostics.Count == 0,
                            "La sintassi minima di " + profile.Id + " produce diagnostiche dopo l'attivazione: " +
                            string.Join(" | ", profileDiagnostics.Select(item => item.Code + ": " + item.Message)));
                        Control localizedFlowchartCanvas = FindControl<Control>(form, "flowchartCanvas");
                        Require(GetPropertyValue<string>(localizedFlowchartCanvas, "TrueLabel") == profile.TrueLabel,
                            "La Flowchart non applica le etichette del profilo " + profile.Id + ".");

                        editor.Text = profile.InvalidSource;
                        editor.Select(editor.TextLength, 0);
                        InvokePrivate(form, "startBackgroundAnalysis");
                        requestedAnalysis = GetPrivateValue<long>(form, "analysisRequestVersion");
                        PumpUntil(() => GetPrivateValue<long>(form, "analysisAppliedVersion") >= requestedAnalysis &&
                                        string.Equals(GetPrivateValue<string>(form, "lastAnalyzedText"),
                                            profile.InvalidSource, StringComparison.Ordinal),
                            5000, "L'analisi localizzata non viene applicata per " + profile.Id + ".");
                        DdfDiagnostic unresolved = GetPrivateField<IReadOnlyList<DdfDiagnostic>>(form,
                            "activeDiagnostics").FirstOrDefault(item => item.Code == "DDF201");
                        Require(unresolved != null && unresolved.DisplayMessage == profile.DiagnosticMessage,
                            "La diagnostica DDF201 non usa il testo del profilo " + profile.Id + ".");

                        editor.Text = profile.Source;
                        editor.Select(editor.TextLength, 0);
                        InvokePrivate(form, "startBackgroundAnalysis");
                        requestedAnalysis = GetPrivateValue<long>(form, "analysisRequestVersion");
                        PumpUntil(() => GetPrivateValue<long>(form, "analysisAppliedVersion") >= requestedAnalysis &&
                                        string.Equals(GetPrivateValue<string>(form, "lastAnalyzedText"),
                                            profile.Source, StringComparison.Ordinal),
                            5000, "Il sorgente valido non viene ripristinato dopo la prova diagnostica.");
                        foreach (OpenDocumentBuffer document in documents.Documents) document.MarkPersisted();
                        editor.Modified = false;
                    }

                    int exceptionsBeforeClose = UiExceptions.Count;
                    Control flowchartCanvas = FindControl<Control>(form, "flowchartCanvas");
                    PumpUntil(() => GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram") != null,
                        5000, "Il test non ha prodotto la flowchart da eliminare alla chiusura dei documenti.");
                    ToolStripMenuItem close = FindMenuItem(form, "closeDocumentMenuItem");
                    while (documents.Documents.Any(document => document.Session.HasPath) || documents.Documents.Count > 1)
                    {
                        close.PerformClick();
                        PumpMessages(60);
                    }
                    close.PerformClick();
                    PumpMessages(180);
                    TabControl tabs = FindControl<TabControl>(form, "documentTabs");
                    Require(documents.Documents.Count == 1 && documents.ActiveDocument != null &&
                            !documents.ActiveDocument.Session.HasPath && tabs.TabCount == 1,
                        "Chiudendo tutte le schede non resta un documento vuoto coerente.");
                    Require(GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram") == null &&
                            FindControl<Label>(form, "flowchartStatusLabel").Text.Contains(
                                "nessuna funzione rappresentabile", StringComparison.OrdinalIgnoreCase),
                        "La Flowchart dell'ultimo documento chiuso è rimasta visibile.");
                    Require(UiExceptions.Count == exceptionsBeforeClose,
                        "La chiusura di tutte le schede con workspace attivo ha generato un'eccezione UI.");
                }

                var embeddedPackages = new DdfLanguagePackageStore(
                    Path.Combine(userDataRoot, "language-packages")).List();
                string italianPath = embeddedPackages.Where(item => item.Id == "ddf-it-didactic")
                    .OrderByDescending(item => Version.Parse(item.Version)).First().Path;
                string familiarPath = embeddedPackages.Where(item => item.Id == "ddf-c-familiar")
                    .OrderByDescending(item => Version.Parse(item.Version)).First().Path;
                using (var embedded = new MainForm(true))
                {
                    embedded.ShowInTaskbar = visible;
                    embedded.StartPosition = FormStartPosition.Manual;
                    embedded.Location = new Point(80, 80);
                    embedded.Show();
                    PumpMessages(100);
                    Require(embedded.EmbeddedHostMode &&
                            FindMenuItem(embedded, "manageLanguagePackagesMenuItem").Available &&
                            !FindMenuItem(embedded, "compilationMenuItem").Available &&
                            !FindToolbarButton(embedded,
                                "toolbarCompileAndRunButton").Available &&
                            !FindToolbarButton(embedded,
                                "toolbarPublishExecutableButton").Available &&
                            !FindMenuItem(embedded,
                                "openCompiledOutputMenuItem").Available &&
                            !FindToolbarButton(embedded,
                                "toolbarOpenCompiledOutputButton").Available,
                        "La selezione dei pacchetti non è disponibile all'utente nella shell embedded.");
                    bool compiledBuildRejected = false;
                    try
                    {
                        embedded.AttachStandaloneCompiledBuildService(null);
                    }
                    catch (InvalidOperationException)
                    {
                        compiledBuildRejected = true;
                    }
                    Require(compiledBuildRejected,
                        "La shell embedded ha accettato un backend di compilazione standalone.");
                    embedded.SetEmbeddedPrimaryDocument("embedded.ddfc",
                        "main() out int { ret 2; }");
                    RichTextBox embeddedEditor = FindControl<RichTextBox>(embedded, "richTextBoxMainEditor");

                    DdfLanguagePackageLoadResult italianResult =
                        embedded.EmbeddedEditor.LanguageSession.TryActivate(italianPath);
                    PumpUntil(() => embeddedEditor.Text ==
                            "main() restituisce int { restituisci 2; }",
                        5000, "Il cambio linguaggio via API non converte il buffer embedded. " +
                        "successo=" + italianResult.Success + ", attivo=" +
                        embedded.EmbeddedEditor.LanguageSession.ActivePackage.Id +
                        ", testo='" + embeddedEditor.Text + "'.");
                    Require(italianResult.Success &&
                            GetPropertyValue<string>(FindControl<Control>(embedded, "flowchartCanvas"),
                                "TrueLabel") == "Sì",
                        "Il cambio linguaggio via API non aggiorna la presentazione embedded.");

                    DdfLanguagePackageLoadResult familiar = DdfLanguagePackageLoader.Load(familiarPath);
                    embedded.EmbeddedEditor.AttachLanguageSession(
                        new DdfLanguagePackageSession(familiar.Package));
                    PumpUntil(() => embeddedEditor.Text == "main() returns int { return 2; }",
                        5000, "La sostituzione della sessione linguistica non aggiorna la shell embedded.");
                    Require(familiar.Success && ReferenceEquals(
                                embedded.EmbeddedEditor.LanguageSession,
                                GetPrivateField<IDdfLanguagePackageSession>(embedded, "editorLanguageSession")) &&
                            GetPropertyValue<string>(FindControl<Control>(embedded, "flowchartCanvas"),
                                "TrueLabel") == "Yes",
                        "La shell embedded non segue la sessione linguistica fornita dall'host.");
                }
            }
            finally
            {
                DdfLanguageCatalog.ActivateEmbedded();
                Environment.SetEnvironmentVariable("DDF_LANGUAGE_EDITOR_USER_DATA", previousUserData);
                if (Directory.Exists(workspaceRoot)) Directory.Delete(workspaceRoot, true);
            }

            Console.WriteLine("PASS profili utente installati e chiusura completa schede workspace");
        }

        private static void CreateRealisticLanguagePackage(string path, string name, string keywordPrefix)
        {
            DdfLanguagePackageLoader.ExportEmbeddedDefault(path);

            string manifestPath = Path.Combine(path, "manifest.json");
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath, Encoding.UTF8)).AsObject();
            manifest["name"] = name;
            File.WriteAllText(manifestPath,
                manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

            string lexiconPath = Path.Combine(path, "lexicon.json");
            JsonObject lexicon = JsonNode.Parse(File.ReadAllText(lexiconPath, Encoding.UTF8)).AsObject();
            JsonArray keywords = lexicon["keywords"].AsArray();
            for (int index = 0; index < EditorPerformanceBudgets.RealisticPackageKeywords; index++)
            {
                keywords.Add(new JsonObject
                {
                    ["text"] = keywordPrefix + index.ToString("D3"),
                    ["tokenKind"] = "ControlFlowKeyword",
                    ["role"] = "None"
                });
            }
            File.WriteAllText(lexiconPath,
                lexicon.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

            string intellisensePath = Path.Combine(path, "intellisense.json");
            JsonObject intellisense = JsonNode.Parse(File.ReadAllText(intellisensePath, Encoding.UTF8)).AsObject();
            JsonArray snippets = intellisense["snippets"].AsArray();
            for (int index = 0; index < EditorPerformanceBudgets.RealisticPackageSnippets; index++)
            {
                string prefix = "snippet" + index.ToString("D3");
                snippets.Add(new JsonObject
                {
                    ["prefix"] = prefix,
                    ["displayText"] = prefix + " — stress",
                    ["description"] = "Snippet generato per il carico realistico",
                    ["body"] = "ret ${1:0};${0}"
                });
            }
            File.WriteAllText(intellisensePath,
                intellisense.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }

        private static string CreateGeneratedSource(int lineCount, string functionName)
        {
            if (lineCount < 5) throw new ArgumentOutOfRangeException(nameof(lineCount));
            var source = new StringBuilder();
            source.AppendLine(functionName + "() out int");
            source.AppendLine("{");
            source.AppendLine("    int value << 0;");
            for (int line = 0; line < lineCount - 5; line++)
                source.AppendLine("    value << value + 1;");
            source.AppendLine("    ret value;");
            source.Append("}");
            Require(source.ToString().Count(character => character == '\n') + 1 == lineCount,
                "Il dataset prestazionale non contiene il numero di righe dichiarato.");
            return source.ToString();
        }

        private static void AssertProlongedEditingSession(bool visible)
        {
            using (var form = ShowSmokeForm(visible))
            using (Process process = Process.GetCurrentProcess())
            {
                TabControl documentTabs = FindControl<TabControl>(form, "documentTabs");
                ToolStripMenuItem format = FindMenuItem(form, "formatDocumentMenuItem");
                ToolStripMenuItem wordWrap = FindMenuItem(form, "wordWrapMenuItem");
                ToolStripMenuItem expandAll = FindMenuItem(form, "expandAllFoldsMenuItem");
                IReadOnlyList<CommandPaletteCommand> commands =
                    (IReadOnlyList<CommandPaletteCommand>)InvokePrivate(form, "createCommandPaletteCommands");
                string stableSource = null;

                for (int index = 0; index < 4; index++)
                {
                    if (index > 0) FindMenuItem(form, "newMenuItem").PerformClick();
                    PumpMessages(30);
                    RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                    editor.Text = Source;
                    editor.Select(0, 0);
                    format.PerformClick();
                    stableSource = stableSource ?? editor.Text;
                    Require(editor.Text == stableSource, "Il formatter produce sorgenti diversi tra schede equivalenti.");
                    editor.ClearUndo();
                }

                using (var warmup = new CommandPaletteForm(commands))
                {
                    warmup.StartPosition = FormStartPosition.Manual;
                    warmup.Location = visible ? new Point(120, 120) : new Point(-32000, -32000);
                    warmup.Show(form);
                    PumpMessages(20);
                    warmup.Close();
                }
                PumpMessages(120);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long initialManaged = GC.GetTotalMemory(true);
                process.Refresh();
                int initialHandles = process.HandleCount;
                int initialGdi = GetGuiResources(process.Handle, 0);
                int initialUser = GetGuiResources(process.Handle, 1);
                int initialUiExceptions = UiExceptions.Count;
                var iterationSamples = new List<double>();

                for (int iteration = 0; iteration < EditorPerformanceBudgets.EnduranceIterations; iteration++)
                {
                    iterationSamples.Add(MeasureMilliseconds(() =>
                    {
                        documentTabs.SelectedIndex = iteration % documentTabs.TabCount;
                        Application.DoEvents();
                        RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                        int editPosition = stableSource.IndexOf("value", StringComparison.Ordinal);
                        editor.Select(editPosition, 0);

                        switch (iteration % 8)
                        {
                            case 0:
                                editor.ClearUndo();
                                editor.SelectedText = "x";
                                editor.Undo();
                                Require(editor.Text == stableSource, "Undo perde il buffer durante la sessione prolungata.");
                                editor.Redo();
                                editor.Undo();
                                break;
                            case 1:
                                editor.SelectedText = " ";
                                editor.Select(editPosition, 1);
                                editor.SelectedText = string.Empty;
                                InvokePrivate(form, "startBackgroundAnalysis");
                                break;
                            case 2:
                                editor.Select(Math.Min(editor.TextLength, editPosition + iteration % 5), 0);
                                InvokePrivate(form, "updateCaretPosition");
                                InvokePrivate(form, "updateLineNumbers");
                                InvokePrivate(form, "refreshEditorOverview");
                                break;
                            case 3:
                                editor.Focus();
                                InvokeProcessCmdKey(form, Keys.F6);
                                InvokeProcessCmdKey(form, Keys.Control | Keys.Tab);
                                InvokeProcessCmdKey(form, Keys.F6);
                                InvokeProcessCmdKey(form, Keys.Control | Keys.Tab);
                                InvokeProcessCmdKey(form, Keys.F6);
                                break;
                            case 4:
                                wordWrap.PerformClick();
                                wordWrap.PerformClick();
                                break;
                            case 5:
                                format.PerformClick();
                                break;
                            case 6:
                                InvokePrivate(form, "applyHighlighting");
                                editor.Select(Math.Max(0, stableSource.IndexOf("main", StringComparison.Ordinal)), 0);
                                FindMenuItem(form, "toggleFoldMenuItem").PerformClick();
                                if (FindControl<RichTextBox>(form, "richTextBoxFoldedView").Visible)
                                    expandAll.PerformClick();
                                break;
                            case 7:
                                using (var palette = new CommandPaletteForm(commands))
                                {
                                    palette.StartPosition = FormStartPosition.Manual;
                                    palette.Location = visible ? new Point(120, 120) : new Point(-32000, -32000);
                                    palette.Show(form);
                                    Application.DoEvents();
                                    palette.Close();
                                }
                                break;
                        }

                        Require(editor.Text == stableSource,
                            "Una sequenza di comandi ha alterato il buffer alla iterazione " + iteration + ".");
                        editor.ClearUndo();
                    }));

                    if (iteration % 8 == 0)
                    {
                        bool heartbeat = false;
                        form.BeginInvoke(new Action(() => heartbeat = true));
                        PumpUntil(() => heartbeat, 1000,
                            "Il thread UI non risponde durante la sessione prolungata.");
                    }
                    else PumpMessages(5);
                }

                PumpMessages(400);
                for (int index = 0; index < documentTabs.TabCount; index++)
                {
                    documentTabs.SelectedIndex = index;
                    PumpMessages(20);
                    Require(FindControl<RichTextBox>(form, "richTextBoxMainEditor").Text == stableSource,
                        "La sessione prolungata ha corrotto il contenuto della scheda " + index + ".");
                }
                Require(UiExceptions.Count == initialUiExceptions,
                    "La sessione prolungata ha generato eccezioni UI.");

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long managedGrowth = Math.Max(0, GC.GetTotalMemory(true) - initialManaged);
                process.Refresh();
                int handleGrowth = Math.Max(0, process.HandleCount - initialHandles);
                int gdiGrowth = Math.Max(0, GetGuiResources(process.Handle, 0) - initialGdi);
                int userGrowth = Math.Max(0, GetGuiResources(process.Handle, 1) - initialUser);
                double p95 = EditorPerformanceBudget.Percentile95(iterationSamples);

                Require(p95 <= EditorPerformanceBudgets.EnduranceP95CeilingMilliseconds,
                    "Sessione prolungata non reattiva: p95=" + p95.ToString("0.0") + " ms.");
                Require(managedGrowth <= EditorPerformanceBudgets.EnduranceManagedGrowthBytes,
                    "Crescita memoria gestita anomala: " + managedGrowth + " byte.");
                Require(handleGrowth <= EditorPerformanceBudgets.EnduranceHandleGrowth,
                    "Crescita handle anomala: " + handleGrowth + ".");
                Require(gdiGrowth <= EditorPerformanceBudgets.EnduranceGuiResourceGrowth &&
                        userGrowth <= EditorPerformanceBudgets.EnduranceGuiResourceGrowth,
                    "Crescita risorse GUI anomala: GDI=" + gdiGrowth + ", USER=" + userGrowth + ".");
                Console.WriteLine("PASS sessione prolungata: " +
                    EditorPerformanceBudgets.EnduranceIterations + " cicli, 4 schede, p95=" +
                    p95.ToString("0.0") + " ms, memoria=" + managedGrowth + " B, handle=" +
                    handleGrowth + ", GDI=" + gdiGrowth + ", USER=" + userGrowth);
            }
        }

        private static void AssertExecutionLifecycleEndurance(bool visible)
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-lifecycle-endurance-" + Guid.NewGuid().ToString("N"));
            string firstPackage = Path.Combine(root, "profile-a");
            string secondPackage = Path.Combine(root, "profile-b");
            Directory.CreateDirectory(root);
            try
            {
                CreateRealisticLanguagePackage(firstPackage,
                    "DDF Lifecycle A", "la");
                CreateRealisticLanguagePackage(secondPackage,
                    "DDF Lifecycle B", "lb");

                using var host = new Form
                {
                    ShowInTaskbar = visible,
                    StartPosition = FormStartPosition.Manual,
                    Location = visible ? new Point(80, 80) :
                        new Point(-32000, -32000),
                    Size = new Size(920, 680)
                };
                host.Show();
                PumpMessages(100);

                using Process process = Process.GetCurrentProcess();
                int initialUiExceptions = UiExceptions.Count;
                HashSet<int> initialRunnerProcesses = Process
                    .GetProcessesByName("DDFLanguageEditor.Runner")
                    .Select(item =>
                    {
                        try { return item.Id; }
                        finally { item.Dispose(); }
                    }).ToHashSet();

                // Warm up WinForms, parser, VM and embedded language loading before
                // taking the resource baseline used by the regression gate.
                using (var warmup = new DdfEditorView { Dock = DockStyle.Fill })
                {
                    host.Controls.Add(warmup);
                    warmup.CreateControl();
                    warmup.SourceText = "main() out int { ret 1; }";
                    Task<DdfEditorExecutionCompletion> run =
                        warmup.ExecutionService.RunAsync();
                    PumpUntil(() => run.IsCompleted, 5000,
                        "Il warmup lifecycle non completa l'esecuzione.");
                    host.Controls.Remove(warmup);
                }
                PumpMessages(150);
                ForceCollection();
                long initialManaged = GC.GetTotalMemory(true);
                process.Refresh();
                int initialHandles = process.HandleCount;
                int initialGdi = GetGuiResources(process.Handle, 0);
                int initialUser = GetGuiResources(process.Handle, 1);
                int cleanupCount = 0;

                for (int iteration = 0;
                    iteration < EditorPerformanceBudgets.LifecycleEnduranceIterations;
                    iteration++)
                {
                    var documents = new OpenDocumentCollection();
                    OpenDocumentBuffer main = (OpenDocumentBuffer)documents.CreateVirtual(
                        "main-" + iteration + ".ddf",
                        "main() out int { ret resourceProbe(); }");
                    OpenDocumentBuffer scratch = (OpenDocumentBuffer)documents.CreateVirtual(
                        "scratch-" + iteration + ".ddf",
                        "helper() out int { ret " + iteration + "; }");
                    var language = new DdfLanguagePackageSession();
                    var catalog = new DdfEditorHostCatalog();
                    catalog.RegisterFunction(new DdfHostFunctionDefinition(
                        "resourceProbe", "int",
                        Array.Empty<DdfHostParameterDefinition>(), invoke: null,
                        invokeAsync: (context, arguments) =>
                        {
                            context.RegisterResource(new CallbackDisposable(
                                () => Interlocked.Increment(ref cleanupCount)));
                            return ValueTask.FromResult<object>(42);
                        }));

                    var view = new DdfEditorView { Dock = DockStyle.Fill };
                    int callbacks = 0;
                    view.ExecutionStateChanged += (sender, args) =>
                        Interlocked.Increment(ref callbacks);
                    view.ExecutionCompleted += (sender, args) =>
                        Interlocked.Increment(ref callbacks);
                    view.DebugStateChanged += (sender, args) =>
                        Interlocked.Increment(ref callbacks);
                    view.DebugCompleted += (sender, args) =>
                        Interlocked.Increment(ref callbacks);
                    view.AttachDocumentService(documents);
                    view.AttachLanguageSession(language);
                    view.AttachHostCatalog(catalog);
                    host.Controls.Add(view);
                    view.CreateControl();
                    PumpMessages(15);

                    Require(documents.ActivateDocument(scratch.Id) &&
                            documents.UpdateDocument(scratch.Id,
                                scratch.SourceText + "\n// lifecycle") &&
                            documents.ActivateDocument(main.Id) &&
                            documents.TryCloseDocument(scratch.Id, true),
                        "Apertura, modifica o chiusura documenti fallita al ciclo " +
                        iteration + ".");

                    Task<DdfEditorExecutionCompletion> normalRun =
                        view.ExecutionService.RunAsync();
                    PumpUntil(() => normalRun.IsCompleted, 5000,
                        "Run non completa al ciclo lifecycle " + iteration + ".");
                    Require(normalRun.Result.Succeeded && Convert.ToInt64(
                            normalRun.Result.ExecutionResult.ReturnValue) == 42L &&
                            cleanupCount == iteration + 1,
                        "Run o cleanup host non deterministico al ciclo " + iteration + ".");

                    string packagePath = iteration % 2 == 0
                        ? firstPackage : secondPackage;
                    DdfLanguagePackageLoadResult activated =
                        language.TryActivate(packagePath);
                    Require(activated.Success && language.ActivePackage.Name ==
                            (iteration % 2 == 0
                                ? "DDF Lifecycle A" : "DDF Lifecycle B"),
                        "Il cambio profilo fallisce al ciclo " + iteration + ": " +
                        string.Join(" | ", activated.Diagnostics.Select(item =>
                            item.ToString())));

                    const string debugSource =
                        "main() out int\n{\n    int value << 0;\n" +
                        "    value++;\n    ret value;\n}";
                    Require(documents.UpdateDocument(main.Id, debugSource),
                        "Il sorgente debug non viene applicato.");
                    view.DebugService.SetBreakpoints(new[]
                        { new DdfEditorBreakpoint(main.Id, 4) });
                    Task<DdfEditorExecutionCompletion> debugRun =
                        view.DebugService.RunAsync();
                    PumpUntil(() => view.DebugService.IsPaused, 5000,
                        "Debug non raggiunge il breakpoint al ciclo " + iteration + ".");
                    long versionAtPause = main.Version;
                    Require(documents.UpdateDocument(main.Id,
                            debugSource + "\n// modificato durante la pausa") &&
                            main.Version > versionAtPause,
                        "La modifica durante pausa non aggiorna il buffer.");
                    Task<DdfEditorLifecycleResult> debugStop =
                        ((IDdfEditorAsyncLifecycleService)view.DebugService)
                        .StopAsync(TimeSpan.FromSeconds(5));
                    PumpUntil(() => debugStop.IsCompleted && debugRun.IsCompleted,
                        6000, "Stop del debug in pausa non converge al ciclo " +
                        iteration + ".");
                    Require(debugStop.Result.Succeeded &&
                            debugRun.Result.WasCancelled,
                        "Il debug modificato durante la pausa non viene cancellato.");

                    Require(documents.UpdateDocument(main.Id,
                            "main() out int { int value << 0; while(true) { value++; } ret value; }"),
                        "Il sorgente cancellabile non viene applicato.");
                    Task<DdfEditorExecutionCompletion> cancellableRun =
                        view.ExecutionService.RunAsync(new DdfEditorExecutionRequest(
                            maxInstructions: int.MaxValue));
                    PumpUntil(() => view.ExecutionService.IsRunning, 3000,
                        "Run cancellabile non parte al ciclo " + iteration + ".");
                    Task<DdfEditorLifecycleResult> executionStop =
                        ((IDdfEditorAsyncLifecycleService)view.ExecutionService)
                        .StopAsync(TimeSpan.FromSeconds(5));
                    PumpUntil(() => executionStop.IsCompleted &&
                            cancellableRun.IsCompleted, 6000,
                        "Stop del Run non converge al ciclo " + iteration + ".");
                    Require(executionStop.Result.Succeeded &&
                            cancellableRun.Result.WasCancelled,
                        "La cancellazione Run non è osservabile al ciclo " + iteration + ".");

                    Task<DdfEditorLifecycleResult> executionDispose =
                        ((IDdfEditorAsyncLifecycleService)view.ExecutionService)
                        .DisposeAsync(TimeSpan.FromSeconds(5));
                    Task<DdfEditorLifecycleResult> debugDispose =
                        ((IDdfEditorAsyncLifecycleService)view.DebugService)
                        .DisposeAsync(TimeSpan.FromSeconds(5));
                    PumpUntil(() => executionDispose.IsCompleted &&
                            debugDispose.IsCompleted, 6000,
                        "DisposeAsync non drena le code al ciclo " + iteration + ".");
                    Require(executionDispose.Result.Succeeded &&
                            debugDispose.Result.Succeeded,
                        "DisposeAsync fallisce al ciclo " + iteration + ".");

                    int callbacksAtDispose = callbacks;
                    host.Controls.Remove(view);
                    view.Dispose();
                    documents.UpdateDocument(main.Id,
                        "main() out int { ret 99; }");
                    PumpMessages(25);
                    Require(callbacks == callbacksAtDispose,
                        "Callback consegnato dopo Dispose al ciclo " + iteration + ".");

                    if (iteration % 4 == 3)
                    {
                        ForceCollection();
                        bool heartbeat = false;
                        host.BeginInvoke(new Action(() => heartbeat = true));
                        PumpUntil(() => heartbeat, 1000,
                            "Il thread UI non risponde durante il lifecycle endurance.");
                    }
                }

                PumpMessages(250);
                ForceCollection();
                process.Refresh();
                long managedGrowth = Math.Max(0,
                    GC.GetTotalMemory(true) - initialManaged);
                int handleGrowth = Math.Max(0,
                    process.HandleCount - initialHandles);
                int gdiGrowth = Math.Max(0,
                    GetGuiResources(process.Handle, 0) - initialGdi);
                int userGrowth = Math.Max(0,
                    GetGuiResources(process.Handle, 1) - initialUser);
                HashSet<int> finalRunnerProcesses = Process
                    .GetProcessesByName("DDFLanguageEditor.Runner")
                    .Select(item =>
                    {
                        try { return item.Id; }
                        finally { item.Dispose(); }
                    }).ToHashSet();

                Require(UiExceptions.Count == initialUiExceptions,
                    "Il lifecycle endurance ha generato eccezioni UI.");
                Require(managedGrowth <= EditorPerformanceBudgets
                        .LifecycleEnduranceManagedGrowthBytes,
                    "Crescita memoria lifecycle anomala: " + managedGrowth + " byte.");
                Require(handleGrowth <= EditorPerformanceBudgets
                        .LifecycleEnduranceHandleGrowth,
                    "Crescita handle lifecycle anomala: " + handleGrowth + ".");
                Require(gdiGrowth <= EditorPerformanceBudgets
                            .LifecycleEnduranceGuiResourceGrowth &&
                        userGrowth <= EditorPerformanceBudgets
                            .LifecycleEnduranceGuiResourceGrowth,
                    "Crescita risorse GUI lifecycle anomala: GDI=" + gdiGrowth +
                    ", USER=" + userGrowth + ".");
                Require(!finalRunnerProcesses.Except(initialRunnerProcesses).Any(),
                    "Il lifecycle endurance ha lasciato processi Runner residui: " +
                    string.Join(", ", finalRunnerProcesses.Except(
                        initialRunnerProcesses)) + ".");

                Console.WriteLine("PASS lifecycle endurance: " +
                    EditorPerformanceBudgets.LifecycleEnduranceIterations +
                    " cicli Run/Debug/Stop/Dispose, cleanup=" + cleanupCount +
                    ", memoria=" + managedGrowth + " B, handle=" + handleGrowth +
                    ", GDI=" + gdiGrowth + ", USER=" + userGrowth);
            }
            finally
            {
                DdfLanguageCatalog.ActivateEmbedded();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void ForceCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static void StopInteractiveTimers(MainForm form)
        {
            GetPrivateField<System.Windows.Forms.Timer>(form, "highlightTimer")?.Stop();
            GetPrivateField<System.Windows.Forms.Timer>(form, "delimiterHighlightTimer")?.Stop();
            GetPrivateField<System.Windows.Forms.Timer>(form, "completionTimer")?.Stop();
            GetPrivateField<System.Windows.Forms.Timer>(form, "signatureHelpTimer")?.Stop();
            GetPrivateField<System.Windows.Forms.Timer>(form, "workspaceSearchTimer")?.Stop();
        }

        private static double MeasureMilliseconds(Action action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static void AssertPerformanceBudget(EditorPerformanceBudget budget, IReadOnlyList<double> samples)
        {
            double p95 = EditorPerformanceBudget.Percentile95(samples);
            Require(budget.IsWithinSmokeCeiling(samples),
                budget.DisplayName + " supera il ceiling smoke: p95=" + p95.ToString("0.0") +
                " ms, limite=" + budget.SmokeCeilingP95Milliseconds.ToString("0") + " ms.");
            string targetState = p95 <= budget.TargetP95Milliseconds ? "target" : "da ottimizzare";
            Console.WriteLine("PASS budget " + budget.DisplayName + ": p95=" + p95.ToString("0.0") +
                " ms (" + targetState + ", target " + budget.TargetP95Milliseconds.ToString("0") + " ms)");
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
                    { "navigateBackMenuItem", Keys.Alt | Keys.Left },
                    { "navigateForwardMenuItem", Keys.Alt | Keys.Right },
                    { "commandPaletteMenuItem", Keys.Control | Keys.Shift | Keys.P },
                    { "settingsMenuItem", Keys.Control | Keys.Oemcomma },
                    { "completionMenuItem", Keys.Control | Keys.Space },
                    { "formatDocumentMenuItem", Keys.Control | Keys.Shift | Keys.F },
                    { "quickFixMenuItem", Keys.Control | Keys.OemPeriod },
                    { "goToDefinitionMenuItem", Keys.F12 },
                    { "renameSymbolMenuItem", Keys.F2 },
                    { "buildProjectMenuItem", Keys.Control | Keys.Shift | Keys.B },
                    { "compileAndRunMenuItem", Keys.Control | Keys.Shift | Keys.F5 },
                    { "runLastBuildMenuItem", Keys.Control | Keys.Alt | Keys.F5 },
                    { "runWithoutDebugMenuItem", Keys.Control | Keys.F5 },
                    { "runProgramMenuItem", Keys.F5 },
                    { "stepIntoMenuItem", Keys.F10 },
                    { "stepOverMenuItem", Keys.Control | Keys.F10 },
                    { "stepOutMenuItem", Keys.Shift | Keys.F11 },
                    { "toggleBreakpointMenuItem", Keys.F9 },
                    { "stopProgramMenuItem", Keys.Shift | Keys.F5 },
                    { "toggleFoldMenuItem", Keys.Control | Keys.M },
                    { "expandAllFoldsMenuItem", Keys.Control | Keys.Shift | Keys.M }
                };

                string[] commands =
                {
                    "newMenuItem", "openMenuItem", "saveMenuItem", "saveAsMenuItem", "saveAllMenuItem", "closeDocumentMenuItem",
                    "openWorkspaceMenuItem", "closeWorkspaceMenuItem",
                    "recentMenuItem", "recentWorkspacesMenuItem", "exitMenuItem", "undoMenuItem", "redoMenuItem",
                    "cutMenuItem", "copyMenuItem", "pasteMenuItem", "selectAllMenuItem", "toggleLineCommentMenuItem",
                    "duplicateLinesMenuItem", "moveLinesUpMenuItem", "moveLinesDownMenuItem", "deleteLinesMenuItem",
                    "expandSelectionMenuItem", "shrinkSelectionMenuItem", "matchingDelimiterMenuItem",
                    "selectNextOccurrenceMenuItem", "selectAllOccurrencesMenuItem",
                    "findMenuItem", "replaceMenuItem", "workspaceSearchMenuItem", "workspaceReplaceMenuItem",
                    "goToFileMenuItem", "goToSymbolMenuItem", "findReferencesMenuItem", "goToLineMenuItem", "goToLastEditMenuItem",
                    "navigateBackMenuItem", "navigateForwardMenuItem",
                    "completionMenuItem", "formatDocumentMenuItem", "quickFixMenuItem",
                    "goToDefinitionMenuItem", "renameSymbolMenuItem", "validateProjectMenuItem", "buildProjectMenuItem", "compileAndRunMenuItem", "runLastBuildMenuItem", "publishExecutableMenuItem", "openCompiledOutputMenuItem",
                    "runWithoutDebugMenuItem", "runProgramMenuItem", "stepIntoMenuItem", "stepOverMenuItem", "stepOutMenuItem", "toggleBreakpointMenuItem", "stopProgramMenuItem",
                    "toggleFoldMenuItem", "expandAllFoldsMenuItem", "highlightCurrentLineMenuItem", "showWhitespaceMenuItem",
                    "showIndentGuidesMenuItem", "wordWrapMenuItem", "showOverviewMenuItem", "educationalModeMenuItem", "languagePackageInfoMenuItem",
                    "modulesMenuItem", "manageModulePackagesMenuItem",
                    "showToolbarFileMenuItem", "showToolbarEditMenuItem", "showToolbarCodeMenuItem",
                    "showToolbarNavigationMenuItem", "showToolbarViewMenuItem", "showToolbarProjectMenuItem",
                    "showToolbarDebugMenuItem",
                    "manageLanguagePackagesMenuItem", "languagePackageStudioMenuItem", "validateLanguagePackageMenuItem", "languagePlaygroundMenuItem",
                    "aboutMenuItem", "checkProductUpdatesMenuItem",
                    "commandPaletteMenuItem", "settingsMenuItem"
                };
                foreach (string command in commands)
                {
                    ToolStripMenuItem item = FindMenuItem(form, command);
                    Require(item != null, "Comando di menu non trovato: " + command);
                    Require(command == "recentMenuItem" || command == "recentWorkspacesMenuItem" ||
                            item.GetType().GetEvent("Click") != null,
                        "Il comando non espone l'evento Click: " + command);
                }

                foreach (KeyValuePair<string, Keys> shortcut in shortcuts)
                {
                    Require(FindMenuItem(form, shortcut.Key).ShortcutKeys == shortcut.Value,
                        "Scorciatoia errata per " + shortcut.Key + ".");
                }
            }

            Console.WriteLine("PASS struttura menu e 53 scorciatoie");
        }

        private static void AssertHelpMenu(bool visible)
        {
            using (var form = ShowSmokeForm(visible))
            {
                Require(FindMenuItem(form, "manageLanguagePackagesMenuItem") != null &&
                        FindMenuItem(form, "useEmbeddedLanguageMenuItem") != null &&
                        FindControl<StatusStrip>(form, "statusStripMain").Items
                            .Cast<ToolStripItem>().Any(item => item.Name == "statusEncodingLabel" && item.Text.Contains("DDF")),
                    "Menu e stato non espongono la gestione del linguaggio attivo.");
                Require(FindControl<ToolStrip>(form, "toolStripProject").Items.Cast<ToolStripItem>().Any(item =>
                        item.Name == "toolbarLanguageButton" && item is ToolStripDropDownButton &&
                        item.ToolTipText.Contains("DDF")),
                    "La barra strumenti non espone il selettore del linguaggio attivo.");
                ToolStripMenuItem languageMenu = FindMenuItem(form, "languagePackageInfoMenuItem");
                Require(form.MainMenuStrip.Items.Contains(languageMenu) &&
                        languageMenu.DropDownItems.Contains(
                            FindMenuItem(form, "languagePlaygroundMenuItem")) &&
                        languageMenu.DropDownItems.Count > 0,
                    "Il menu principale Linguaggio non espone direttamente il Playground e gli altri comandi del pacchetto.");
                Require(!FindMenuItem(form, "helpMenuItem").DropDownItems.Contains(languageMenu),
                    "Il menu Linguaggio è ancora nascosto dentro Help.");
                IReadOnlyList<CommandPaletteCommand> languageCommands =
                    (IReadOnlyList<CommandPaletteCommand>)InvokePrivate(form,
                        "createCommandPaletteCommands");
                Require(languageCommands.Any(command =>
                        command.Id == "shell.languagePackageStudioMenuItem" &&
                        command.Title == "Package Studio..." &&
                        command.Category == "Linguaggio" && command.Enabled) &&
                        FindToolbarButton(form,
                            "toolbarLanguageStudioButton").Available,
                    "Package Studio non e coerente tra menu, Command Palette e toolbar.");
                Require(form.Text.Contains("DDFLanguageEditor " +
                    DdfProductVersionCatalog.Current.DisplayVersion, StringComparison.Ordinal),
                    "La shell standalone non espone la versione prodotto candidata.");
                languageMenu.PerformClick();
                PumpMessages(30);
                Require(!Application.OpenForms.Cast<Form>().Any(openForm =>
                        openForm.Name == "LanguagePackageInfoForm" && openForm.Owner == form),
                    "Un clic sulla voce padre Linguaggio non deve eseguire un comando.");
                SetPrivateField(form, "languagePackageActivationDiagnostics",
                    (IReadOnlyList<DdfLanguagePackageDiagnostic>)new[]
                    {
                        new DdfLanguagePackageDiagnostic("DDFL030",
                            "Diagnostica intenzionale di un'attivazione rifiutata.")
                    });
                FindMenuItem(form, "languagePackageDetailsMenuItem").PerformClick();
                PumpMessages(80);

                Form packageInfo = Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(openForm => openForm.Name == "LanguagePackageInfoForm" && openForm.Owner == form);
                Require(packageInfo != null && packageInfo.Visible,
                    "Il comando Help non ha aperto le informazioni sul pacchetto linguaggio.");
                Require(packageInfo.StartPosition == FormStartPosition.CenterScreen && IsLight(packageInfo.BackColor),
                    "Le informazioni sul pacchetto non sono chiare e centrate sullo schermo.");
                DdfLanguagePackage activePackage = form.EmbeddedEditor.LanguageSession.ActivePackage;
                string expectedOrigin = activePackage.Origin == DdfLanguagePackageOrigin.Embedded
                    ? "incorporato"
                    : activePackage.Origin == DdfLanguagePackageOrigin.Archive ? "Archivio" : "Cartella";
                Require(FindControl<Label>(packageInfo, "languagePackageVersionValueLabel").Text.Contains(
                            activePackage.Id + " " + activePackage.PackageVersion, StringComparison.Ordinal) &&
                        FindControl<Label>(packageInfo, "languagePackageOriginValueLabel").Text.Contains(
                            expectedOrigin, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(FindControl<TextBox>(packageInfo, "languagePackageSourceTextBox").Text),
                    "Versione, origine o sorgente del pacchetto attivo non sono esposte.");
                ListBox packageDiagnostics = FindControl<ListBox>(packageInfo,
                    "languagePackageDiagnosticsListBox");
                Require(packageDiagnostics.Items.Count > 0,
                    "La finestra non espone lo stato delle diagnostiche di caricamento.");
                Require(!packageDiagnostics.Items.Cast<object>().Any(item =>
                        item.ToString().Contains("DDFL030", StringComparison.Ordinal)),
                    "Le diagnostiche di un'attivazione rifiutata sono state attribuite al pacchetto attivo.");
                Require(FindControl<Label>(packageInfo, "languagePackageNativeExtensionsValueLabel").Text == "Nessuno",
                    "La finestra non espone i requisiti dei plug-in nativi del pacchetto.");
                FindControl<Button>(packageInfo, "languagePackageCloseButton").PerformClick();
                PumpMessages(40);
                Require(packageInfo.IsDisposed, "Il pulsante Chiudi non ha terminato le informazioni pacchetto.");

                string editorBeforePlayground = FindControl<RichTextBox>(form, "richTextBoxMainEditor").Text;
                FindMenuItem(form, "languagePlaygroundMenuItem").PerformClick();
                PumpMessages(80);
                Form playground = Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(openForm => openForm.Name == "LanguagePlaygroundForm" && openForm.Owner == form);
                Require(playground != null && playground.StartPosition == FormStartPosition.CenterScreen && IsLight(playground.BackColor),
                    "Il Playground non è stato aperto come finestra chiara e centrata.");
                Require(FindControl<RichTextBox>(playground, "languagePlaygroundTokensTextBox").Text.Contains("Identifier") &&
                        FindControl<RichTextBox>(playground, "languagePlaygroundTreeTextBox").Text.Contains("Compilation") &&
                        FindControl<RichTextBox>(playground, "languagePlaygroundTypesTextBox").Text.Contains("int") &&
                        FindControl<RichTextBox>(playground, "languagePlaygroundDiagnosticsTextBox").Text.Contains("Nessuna") &&
                        FindControl<RichTextBox>(playground, "languagePlaygroundErrorsTextBox").Text.Contains("DDF408") &&
                        FindControl<RichTextBox>(playground, "languagePlaygroundLoweringTextBox").Text.Contains("Function main") &&
                        FindControl<RichTextBox>(playground, "languagePlaygroundLoweringTextBox").Text.Contains("Return") &&
                        FindControl<Label>(playground, "languagePlaygroundPackageLabel").Text.Contains(activePackage.Id) &&
                        FindControl<Label>(playground, "languagePlaygroundStatusLabel").Text.Contains("IR disponibile") &&
                        FindControl<RichTextBox>(playground, "languagePlaygroundFormattedTextBox").Text.Contains("0;"),
                    "Il Playground non espone pacchetto, token, albero, tipi, errori dichiarati, diagnostiche, IR e formattazione.");
                DdfLanguagePackage canonicalPackage = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
                FindControl<RichTextBox>(playground, "languagePlaygroundSourceTextBox").Text =
                    DdfSyntaxProfileConverter.Convert("main() out int { ret ; }",
                        canonicalPackage.Language, activePackage.Language).Text;
                PumpMessages(40);
                Require(!FindControl<RichTextBox>(playground, "languagePlaygroundDiagnosticsTextBox").Text.Contains("Nessuna"),
                    "Il Playground non aggiorna le diagnostiche sul sorgente isolato.");
                Require(FindControl<RichTextBox>(form, "richTextBoxMainEditor").Text == editorBeforePlayground,
                    "Il Playground ha modificato il documento attivo.");
                FindControl<Button>(playground, "languagePlaygroundCloseButton").PerformClick();
                PumpMessages(40);
                Require(playground.IsDisposed, "Chiudi non termina il Playground modeless.");

                FindToolbarButton(form,
                    "toolbarLanguageStudioButton").PerformClick();
                PumpMessages(120);
                Form studio = Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(openForm => openForm.Name ==
                        "LanguagePackageStudioForm" && openForm.Owner == form);
                Require(studio != null && studio.Visible &&
                        studio.StartPosition == FormStartPosition.CenterScreen &&
                        IsLight(studio.BackColor),
                    "Il Package Studio non si apre come finestra chiara e centrata.");
                TabPage startPage = FindControl<TabPage>(studio,
                    "languageStudioGettingStartedPage");
                ComboBox keywordInput = FindControl<ComboBox>(studio,
                    "languageStudioGuidedKeywordComboBox");
                ComboBox operatorInput = FindControl<ComboBox>(studio,
                    "languageStudioGuidedOperatorComboBox");
                Require(keywordInput.Left == operatorInput.Left &&
                        keywordInput.Width == operatorInput.Width &&
                        !startPage.VerticalScroll.Visible,
                    "La guida Package Studio non allinea i campi o mostra uno scorrimento esterno all'apertura.");
                FindMenuItem(form, "languagePackageStudioMenuItem").PerformClick();
                PumpMessages(40);
                Require(Application.OpenForms.Cast<Form>().Count(openForm =>
                        openForm.Name == "LanguagePackageStudioForm" &&
                        openForm.Owner == form) == 1,
                    "Menu e toolbar aprono istanze Package Studio divergenti.");
                Require(FindControl<ToolStrip>(studio,
                            "languagePackageStudioToolStrip").Items[
                            "languageStudioCompareButton"] != null,
                    "Il Package Studio non espone il confronto non distruttivo tra versioni.");
                Require(FindControl<ToolStrip>(studio,
                            "languagePackageStudioToolStrip").Items[
                            "languageStudioGenerateArchiveButton"] != null,
                    "Il Package Studio non espone la generazione .ddflang.");
                Require(FindControl<TabControl>(studio,
                            "languagePackageStudioTabs").TabPages.Count == 4 &&
                        FindControl<TabControl>(studio,
                            "languageStudioAdvancedTabs").TabPages.Count == 10 &&
                        FindControl<TabControl>(studio,
                            "languagePackageStudioTabs").TabPages[0].Name ==
                            "languageStudioGettingStartedPage" &&
                        FindControl<Button>(studio,
                            "languageStudioGuideStep0Button") != null &&
                        FindControl<ListBox>(studio,
                            "languageStudioSourceFilesListBox").Items.Count >= 10 &&
                        FindControl<DataGridView>(studio,
                            "languageStudioKeywordsGrid").Rows.Count > 10 &&
                        FindControl<DataGridView>(studio,
                            "languageStudioPrimitiveTypesGrid").Rows.Count >= 6 &&
                        FindControl<DataGridView>(studio,
                            "languageStudioSemanticBindingsGrid").Rows.Count > 10 &&
                        FindControl<DataGridView>(studio,
                            "languageStudioFormattingTokensGrid").Rows.Count == 8 &&
                        FindControl<DataGridView>(studio,
                            "languageStudioSnippetsGrid").Rows.Count > 10 &&
                        FindControl<DataGridView>(studio,
                            "languageStudioDiagnosticMessagesGrid").Rows.Count > 20 &&
                        FindControl<DataGridView>(studio,
                            "languageStudioVmOperationsGrid").Rows.Count > 10 &&
                        FindControl<DataGridView>(studio,
                            "languageStudioExpressionOperationsGrid").Rows.Count > 10 &&
                        FindControl<DataGridView>(studio,
                            "languageStudioBuildBackendsGrid").Rows.Count > 0 &&
                        FindControl<DataGridView>(studio,
                            "languageStudioStandardFunctionsGrid").Rows.Count > 4 &&
                        FindControl<TextBox>(studio,
                            "languageStudioPackageIdTextBox").Text == activePackage.Id,
                    "Il Package Studio non espone manifest, lessico, tipi, semantica e sorgenti autorevoli.");
                FindControl<Button>(studio,
                    "languageStudioGuideStep0Button").PerformClick();
                Require(FindControl<TabControl>(studio,
                            "languagePackageStudioTabs").SelectedTab.Name ==
                        "languageStudioAdvancedPage" &&
                    FindControl<TabControl>(studio,
                            "languageStudioAdvancedTabs").SelectedTab.Name ==
                        "languageStudioManifestPage",
                    "La guida non apre la scheda tecnica corretta in Avanzate.");
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedIndex = 0;
                TreeView ebnfTree = FindControl<TreeView>(studio,
                    "languageStudioEbnfTreeView");
                ListView ebnfDiagnostics = FindControl<ListView>(studio,
                    "languageStudioEbnfDiagnosticsListView");
                ListView ebnfReferences = FindControl<ListView>(studio,
                    "languageStudioEbnfReferencesListView");
                TextBox ebnfSource = FindControl<TextBox>(studio,
                    "languageStudioEbnfTextBox");
                Require(ebnfTree.Nodes.Count == 1 &&
                        ebnfTree.Nodes[0].Nodes.Count > 20 &&
                        ebnfReferences.Items.Count > 20 &&
                        ebnfDiagnostics.Items.Count == 0,
                    "L'editor EBNF non espone regole, riferimenti e diagnostiche live.");
                TextBox guidedPackageName = FindControl<TextBox>(studio,
                    "languageStudioPackageNameTextBox");
                string nameBeforeDistribution = guidedPackageName.Text;
                guidedPackageName.Text += " distribuzione smoke";
                FindControl<Button>(studio,
                    "languageStudioGuideStep4Button").PerformClick();
                PumpMessages(80);
                Require(FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("manifest.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioDiagnosticsTextBox").Text.Contains(
                                "Prima di generare l'archivio") &&
                        FindControl<TabControl>(studio,
                            "languagePackageStudioTabs").SelectedTab.Name ==
                            "languageStudioPreviewPage",
                    "Distribuisci non apre l'anteprima delle modifiche prima dell'export.");
                guidedPackageName.Text = nameBeforeDistribution;
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedIndex = 0;
                ComboBox guidedKeyword = FindControl<ComboBox>(studio,
                    "languageStudioGuidedKeywordComboBox");
                TextBox guidedKeywordText = FindControl<TextBox>(studio,
                    "languageStudioGuidedKeywordTextBox");
                Require(guidedKeyword.Items.Count > 10 &&
                        guidedKeyword.SelectedItem != null,
                    "Il percorso guidato non presenta le parole chiave disponibili.");
                DataGridView keywordGrid = FindControl<DataGridView>(studio,
                    "languageStudioKeywordsGrid");
                DataGridViewRow ifKeyword = keywordGrid.Rows.Cast<DataGridViewRow>()
                    .First(row => !row.IsNewRow &&
                        Convert.ToString(row.Cells[2].Value) == "If");
                guidedKeyword.SelectedItem = Convert.ToString(ifKeyword.Cells[0].Value);
                string impact = FindControl<RichTextBox>(studio,
                    "languageStudioGuidedKeywordImpactTextBox").Text;
                Require(impact.Contains("Grammatica EBNF:") &&
                        impact.Contains("Test da controllare:") &&
                        impact.Contains("Snippet IntelliSense:") &&
                        impact.Contains("Esempio:"),
                    "La guida non mostra le dipendenze e l'esempio della parola chiave.");
                string originalKeyword = guidedKeyword.SelectedItem.ToString();
                MethodInfo replaceTokens = studio.GetType().GetMethod(
                    "ReplaceKeywordTokens", BindingFlags.Static | BindingFlags.NonPublic);
                string protectedSource = originalKeyword + "(flag) { print(\"" +
                    originalKeyword + "\"); // " + originalKeyword + "\n ret 0; }";
                string convertedSource = (string)replaceTokens.Invoke(null,
                    new object[] { protectedSource, originalKeyword,
                        "qualora_smoke", DdfTokenKind.ControlFlowKeyword,
                        activePackage.Language });
                Require(convertedSource.StartsWith("qualora_smoke(") &&
                        convertedSource.Contains("print(\"" + originalKeyword +
                            "\")") &&
                        convertedSource.Contains("// " + originalKeyword),
                    "Il cambio guidato altera stringhe o commenti nel codice di test.");
                MethodInfo replaceEbnf = studio.GetType().GetMethod(
                    "ReplaceEbnfTerminal", BindingFlags.Static | BindingFlags.NonPublic);
                string convertedEbnf = (string)replaceEbnf.Invoke(null,
                    new object[] { "rule = \"" + originalKeyword +
                        "\" ; # \"" + originalKeyword + "\"", originalKeyword,
                        "qualora_smoke" });
                Require(convertedEbnf.Contains("\"qualora_smoke\" ; # \"" +
                        originalKeyword + "\""),
                    "Il cambio guidato altera i commenti EBNF.");
                DataGridViewRow elseKeywordRow = keywordGrid.Rows
                    .Cast<DataGridViewRow>().First(row => !row.IsNewRow &&
                        Convert.ToString(row.Cells[2].Value) == "Else");
                string elseKeyword = Convert.ToString(elseKeywordRow.Cells[0].Value);
                guidedKeywordText.Text = elseKeyword;
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordStageButton").PerformClick();
                guidedKeyword.SelectedItem = elseKeyword;
                guidedKeywordText.Text = originalKeyword;
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordStageButton").PerformClick();
                ListView profileList = FindControl<ListView>(studio,
                    "languageStudioGuidedProfileListView");
                Require(profileList.Items.Count == 2,
                    "Il wizard non mette in coda più parole nello stesso profilo.");
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordPreviewButton").PerformClick();
                PumpMessages(80);
                Require(FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled &&
                        FindControl<TextBox>(studio,
                            "languageStudioDiagnosticsTextBox").Text.Contains("convalidato"),
                    "Lo scambio simultaneo di due parole chiave non resta valido: " +
                    FindControl<TextBox>(studio,
                        "languageStudioDiagnosticsTextBox").Text);
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedIndex = 0;
                guidedKeywordText.Text = "duplicato_smoke";
                Require(!FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled,
                    "La modifica del campo lascia confermabile un'anteprima obsoleta.");
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordStageButton").PerformClick();
                guidedKeyword.SelectedItem = originalKeyword;
                guidedKeywordText.Text = "duplicato_smoke";
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordPreviewButton").PerformClick();
                Require(!FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled &&
                        FindControl<TextBox>(studio,
                            "languageStudioDiagnosticsTextBox").Text.Contains(
                                "stessa scrittura finale"),
                    "Il wizard accetta due parole con la stessa scrittura finale.");
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedIndex = 0;
                while (profileList.Items.Count != 0)
                {
                    profileList.Items[0].Selected = true;
                    FindControl<Button>(studio,
                        "languageStudioGuidedProfileRemoveButton").PerformClick();
                }
                Require(!FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled,
                    "La rimozione di una parola conserva una vecchia anteprima confermabile.");
                guidedKeyword.SelectedItem = originalKeyword;
                ComboBox guidedOperator = FindControl<ComboBox>(studio,
                    "languageStudioGuidedOperatorComboBox");
                TextBox guidedOperatorText = FindControl<TextBox>(studio,
                    "languageStudioGuidedOperatorTextBox");
                guidedOperator.SelectedItem = "&&";
                Require(FindControl<RichTextBox>(studio,
                            "languageStudioGuidedKeywordImpactTextBox").Text.Contains(
                                "Grammatica operatore:") &&
                        FindControl<RichTextBox>(studio,
                            "languageStudioGuidedKeywordImpactTextBox").Text.Contains(
                                "logical.and"),
                    "La guida non mostra grammatica e binding runtime dell'operatore.");
                MethodInfo replaceSyntax = studio.GetType().GetMethod(
                    "ReplaceSyntaxTokensBatch", BindingFlags.Static |
                    BindingFlags.NonPublic);
                string operatorSource = "main() out int { bool ready << true && " +
                    "false; print(\"&&\"); // &&\n ret 0; }";
                string convertedOperatorSource = (string)replaceSyntax.Invoke(null,
                    new object[] { operatorSource,
                        new Dictionary<string, string>(),
                        new Dictionary<string, string> { ["&&"] = "&&&" },
                        activePackage.Language });
                Require(convertedOperatorSource.Contains("true &&& false") &&
                        convertedOperatorSource.Contains("print(\"&&\")") &&
                        convertedOperatorSource.Contains("// &&"),
                    "Il cambio di un operatore altera stringhe o commenti.");
                guidedOperatorText.Text = "||";
                FindControl<Button>(studio,
                    "languageStudioGuidedOperatorPreviewButton").PerformClick();
                Require(!FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled &&
                        FindControl<TextBox>(studio,
                            "languageStudioDiagnosticsTextBox").Text.Contains(
                                "esiste gia"),
                    "Il wizard accetta un operatore di destinazione gia esistente.");
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedIndex = 0;
                guidedOperatorText.Text = "&&&";
                FindControl<Button>(studio,
                    "languageStudioGuidedOperatorStageButton").PerformClick();
                Require(profileList.Items.Count == 1 &&
                        profileList.Items[0].Text == "Operatore",
                    "Il wizard non mette in coda l'operatore scelto.");
                FindControl<Button>(studio,
                    "languageStudioGuidedOperatorPreviewButton").PerformClick();
                PumpMessages(80);
                Require(FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("runtime.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("grammar.ebnf") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("tests.json"),
                    "L'operatore guidato non aggiorna grammatica, test e runtime: " +
                    FindControl<TextBox>(studio,
                        "languageStudioDiagnosticsTextBox").Text);
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedIndex = 0;
                profileList.Items[0].Selected = true;
                FindControl<Button>(studio,
                    "languageStudioGuidedProfileRemoveButton").PerformClick();
                guidedOperator.SelectedItem = "<<";
                guidedOperatorText.Text = ">>";
                FindControl<Button>(studio,
                    "languageStudioGuidedOperatorStageButton").PerformClick();
                guidedOperator.SelectedItem = ">>";
                guidedOperatorText.Text = "<<";
                FindControl<Button>(studio,
                    "languageStudioGuidedOperatorStageButton").PerformClick();
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordPreviewButton").PerformClick();
                PumpMessages(80);
                Require(FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled,
                    "Lo scambio simultaneo degli operatori non resta valido: " +
                    FindControl<TextBox>(studio,
                        "languageStudioDiagnosticsTextBox").Text);
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedIndex = 0;
                while (profileList.Items.Count != 0)
                {
                    profileList.Items[0].Selected = true;
                    FindControl<Button>(studio,
                        "languageStudioGuidedProfileRemoveButton").PerformClick();
                }
                guidedKeywordText.Text = guidedKeyword.Items[1].ToString();
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordPreviewButton").PerformClick();
                Require(!FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled &&
                        FindControl<TextBox>(studio,
                            "languageStudioDiagnosticsTextBox").Text.Contains("esiste gia"),
                    "Il cambio guidato accetta una parola chiave duplicata.");
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedIndex = 0;
                guidedKeywordText.Text = originalKeyword + "_smoke";
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordPreviewButton").PerformClick();
                PumpMessages(80);
                Require(FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("lexicon.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains(
                                originalKeyword + "_smoke") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("grammar.ebnf") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("grammar.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("tests.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("intellisense.json") &&
                        FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled &&
                        FindControl<TextBox>(studio,
                            "languageStudioDiagnosticsTextBox").Text.Contains("convalidato"),
                    "Il cambio guidato non genera un profilo coerente e convalidato.");
                foreach (DataGridViewRow row in keywordGrid.Rows)
                    if (!row.IsNewRow && Convert.ToString(row.Cells[0].Value) ==
                        originalKeyword + "_smoke")
                        row.Cells[0].Value = originalKeyword;
                string originalEbnf = ebnfSource.Text;
                ebnfSource.AppendText("$");
                PumpMessages(400);
                Require(ebnfDiagnostics.Items.Count > 0 &&
                        FindControl<Label>(studio,
                            "languageStudioEbnfStatusLabel").Text.Contains("errori"),
                    "L'editor EBNF non rileva immediatamente una grammatica non valida.");
                ebnfSource.Text = originalEbnf + Environment.NewLine +
                    "# smoke Package Studio" + Environment.NewLine;
                PumpMessages(400);
                Require(ebnfDiagnostics.Items.Count == 0,
                    "L'editor EBNF non recupera dopo la correzione del sorgente.");
                TextBox studioPlayground = FindControl<TextBox>(studio,
                    "languageStudioPlaygroundSourceTextBox");
                ListView studioPlaygroundDiagnostics = FindControl<ListView>(studio,
                    "languageStudioPlaygroundDiagnosticsListView");
                ListView studioPackageTests = FindControl<ListView>(studio,
                    "languageStudioPackageTestsListView");
                PumpMessages(700);
                Require(studioPackageTests.Items.Count > 5 &&
                        studioPackageTests.Items.Cast<ListViewItem>().All(item =>
                            item.Text == "OK") &&
                        FindControl<Label>(studio,
                            "languageStudioPlaygroundStatusLabel").Text.Contains(
                                "test superati"),
                    "Il Package Studio non esegue i test dichiarativi sullo snapshot live.");
                string originalPlaygroundSource = studioPlayground.Text;
                studioPlayground.Text = "main() out int { ret ; }";
                PumpMessages(700);
                Require(studioPlaygroundDiagnostics.Items.Cast<ListViewItem>()
                        .Any(item => item.SubItems.Count > 1 &&
                            item.SubItems[1].Text.StartsWith("DDF",
                                StringComparison.Ordinal)),
                    "Il Playground integrato non aggiorna le diagnostiche del sorgente.");
                studioPlayground.Text = originalPlaygroundSource;
                PumpMessages(700);
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedTab =
                    FindControl<TabPage>(studio, "languageStudioAdvancedPage");
                FindControl<TabControl>(studio,
                    "languageStudioAdvancedTabs").SelectedTab =
                    FindControl<TabPage>(studio, "languageStudioEbnfPage");
                PumpMessages(40);
                FindControl<Button>(studio,
                    "languageStudioPreviewEbnfButton").PerformClick();
                PumpMessages(80);
                Require(FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("grammar.ebnf"),
                    "La modifica EBNF valida non produce il diff obbligatorio. " +
                    "Conferma=" + FindControl<Button>(studio,
                        "languageStudioConfirmPreviewButton").Enabled + "; diagnostiche=" +
                    FindControl<TextBox>(studio,
                        "languageStudioDiagnosticsTextBox").Text);
                FindControl<Button>(studio,
                    "languageStudioConfirmPreviewButton").PerformClick();
                string originalPackageName = FindControl<TextBox>(studio,
                    "languageStudioPackageNameTextBox").Text;
                FindControl<TextBox>(studio, "languageStudioPackageNameTextBox").Text =
                    originalPackageName + " Studio smoke";
                FindControl<TextBox>(studio, "languageStudioTypesDescriptionTextBox").AppendText(
                    " Smoke tipi.");
                FindControl<TextBox>(studio, "languageStudioSemanticsDescriptionTextBox").AppendText(
                    " Smoke semantica.");
                ToolStrip studioTools = FindControl<ToolStrip>(studio,
                    "languagePackageStudioToolStrip");
                studioTools.Items["languageStudioPreviewGuidedButton"].PerformClick();
                PumpMessages(80);
                Require(FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("manifest.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("types.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains("semantics.json") &&
                        FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled,
                    "L'editor guidato non produce un'anteprima confermabile.");
                FindControl<Button>(studio,
                    "languageStudioConfirmPreviewButton").PerformClick();
                Require(studioTools.Items["languageStudioUndoButton"].Enabled,
                    "La modifica guidata di tipi e semantica non abilita Undo.");
                studioTools.Items["languageStudioUndoButton"].PerformClick();
                PumpMessages(80);
                Require(!FindControl<TextBox>(studio,
                            "languageStudioTypesDescriptionTextBox").Text.Contains("Smoke tipi") &&
                        studioTools.Items["languageStudioRedoButton"].Enabled,
                    "Undo non ripristina lo snapshot precedente di tipi e semantica.");
                studioTools.Items["languageStudioRedoButton"].PerformClick();
                PumpMessages(80);
                Require(FindControl<TextBox>(studio,
                            "languageStudioTypesDescriptionTextBox").Text.Contains("Smoke tipi") &&
                        FindControl<TextBox>(studio,
                            "languageStudioSemanticsDescriptionTextBox").Text.Contains("Smoke semantica"),
                    "Redo non ripristina la modifica guidata di tipi e semantica.");
                FindControl<TextBox>(studio,
                    "languageStudioFormattingDescriptionTextBox").AppendText(
                    " Smoke formatter.");
                DataGridView completionLabels = FindControl<DataGridView>(studio,
                    "languageStudioCompletionLabelsGrid");
                DataGridViewRow originRow = completionLabels.Rows
                    .Cast<DataGridViewRow>().Single(row =>
                        Convert.ToString(row.Cells[0].Value) == "keywordOrigin");
                originRow.Cells[1].Value = "origine smoke";
                DataGridView quickFixes = FindControl<DataGridView>(studio,
                    "languageStudioQuickFixesGrid");
                quickFixes.Rows[0].Cells[1].Value = "Correzione smoke";
                FindControl<TextBox>(studio,
                    "languageStudioRuntimeDescriptionTextBox").AppendText(
                    " Smoke runtime.");
                FindControl<TextBox>(studio,
                    "languageStudioBuildDescriptionTextBox").AppendText(
                    " Smoke build.");
                DataGridView standardFunctions = FindControl<DataGridView>(studio,
                    "languageStudioStandardFunctionsGrid");
                standardFunctions.Rows[0].Cells[3].Value =
                    "Documentazione smoke della funzione standard.";
                studioTools.Items["languageStudioPreviewGuidedButton"].PerformClick();
                PumpMessages(80);
                Require(FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains(
                                "formatting.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains(
                                "intellisense.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains(
                                "runtime.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains(
                                "build.json") &&
                        FindControl<TextBox>(studio,
                            "languageStudioPreviewTextBox").Text.Contains(
                                "standard-library.json") &&
                        FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled,
                    "Formatter e IntelliSense guidati non producono un diff valido: " +
                    FindControl<TextBox>(studio,
                        "languageStudioDiagnosticsTextBox").Text);
                FindControl<Button>(studio,
                    "languageStudioConfirmPreviewButton").PerformClick();
                Require(FindControl<TextBox>(studio,
                            "languageStudioFormattingDescriptionTextBox").Text.Contains(
                                "Smoke formatter") &&
                        FindControl<DataGridView>(studio,
                            "languageStudioCompletionLabelsGrid").Rows
                            .Cast<DataGridViewRow>().Any(row =>
                                Convert.ToString(row.Cells[0].Value) ==
                                    "keywordOrigin" &&
                                Convert.ToString(row.Cells[1].Value) ==
                                    "origine smoke") &&
                        FindControl<TextBox>(studio,
                            "languageStudioRuntimeDescriptionTextBox").Text.Contains(
                                "Smoke runtime") &&
                        FindControl<TextBox>(studio,
                            "languageStudioBuildDescriptionTextBox").Text.Contains(
                                "Smoke build") &&
                        FindControl<DataGridView>(studio,
                            "languageStudioStandardFunctionsGrid").Rows[0].Cells[3]
                            .Value?.ToString() ==
                            "Documentazione smoke della funzione standard.",
                    "La conferma non applica tutte le sezioni guidate allo snapshot.");
                studioTools.Items["languageStudioUndoButton"].PerformClick();
                PumpMessages(80);
                Require(!FindControl<TextBox>(studio,
                            "languageStudioFormattingDescriptionTextBox").Text.Contains(
                                "Smoke formatter") &&
                        !FindControl<TextBox>(studio,
                            "languageStudioRuntimeDescriptionTextBox").Text.Contains(
                                "Smoke runtime"),
                    "Undo non ripristina tutte le sezioni guidate.");
                studioTools.Items["languageStudioRedoButton"].PerformClick();
                PumpMessages(80);
                Require(FindControl<TextBox>(studio,
                            "languageStudioFormattingDescriptionTextBox").Text.Contains(
                                "Smoke formatter") &&
                        FindControl<TextBox>(studio,
                            "languageStudioRuntimeDescriptionTextBox").Text.Contains(
                                "Smoke runtime"),
                    "Redo non ripristina tutte le sezioni guidate.");
                keywordGrid = FindControl<DataGridView>(studio,
                    "languageStudioKeywordsGrid");
                ifKeyword = keywordGrid.Rows.Cast<DataGridViewRow>().First(row =>
                    !row.IsNewRow && Convert.ToString(row.Cells[2].Value) == "If");
                string oldIf = Convert.ToString(ifKeyword.Cells[0].Value);
                guidedKeyword = FindControl<ComboBox>(studio,
                    "languageStudioGuidedKeywordComboBox");
                guidedKeyword.SelectedItem = oldIf;
                FindControl<TextBox>(studio,
                    "languageStudioGuidedKeywordTextBox").Text = "qualora_smoke";
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedIndex = 0;
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordPreviewButton").PerformClick();
                PumpMessages(80);
                Require(FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled,
                    "Il cambio coordinato della keyword non supera la convalida: " +
                    FindControl<TextBox>(studio,
                        "languageStudioDiagnosticsTextBox").Text);
                FindControl<Button>(studio,
                    "languageStudioConfirmPreviewButton").PerformClick();
                Require(FindControl<DataGridView>(studio,
                            "languageStudioKeywordsGrid").Rows.Cast<DataGridViewRow>()
                        .Any(row => !row.IsNewRow &&
                            Convert.ToString(row.Cells[0].Value) ==
                            "qualora_smoke" &&
                            Convert.ToString(row.Cells[2].Value) == "If"),
                    "La conferma non applica il profilo coordinato allo snapshot.");
                studioTools.Items["languageStudioValidateButton"].PerformClick();
                PumpMessages(80);
                Require(FindControl<TextBox>(studio,
                            "languageStudioDiagnosticsTextBox").Text.Contains(
                                "completati correttamente", StringComparison.OrdinalIgnoreCase) &&
                        form.EmbeddedEditor.LanguageSession.ActivePackage.Name ==
                            originalPackageName,
                    "Il Package Studio non convalida lo snapshot o modifica il pacchetto attivo.");
                guidedKeyword = FindControl<ComboBox>(studio,
                    "languageStudioGuidedKeywordComboBox");
                guidedKeywordText = FindControl<TextBox>(studio,
                    "languageStudioGuidedKeywordTextBox");
                guidedKeyword.SelectedItem = "qualora_smoke";
                guidedKeywordText.Text = elseKeyword;
                FindControl<TabControl>(studio,
                    "languagePackageStudioTabs").SelectedIndex = 0;
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordStageButton").PerformClick();
                guidedKeyword.SelectedItem = elseKeyword;
                guidedKeywordText.Text = "qualora_smoke";
                guidedOperator = FindControl<ComboBox>(studio,
                    "languageStudioGuidedOperatorComboBox");
                guidedOperatorText = FindControl<TextBox>(studio,
                    "languageStudioGuidedOperatorTextBox");
                guidedOperator.SelectedItem = "&&";
                guidedOperatorText.Text = "&&&";
                FindControl<Button>(studio,
                    "languageStudioGuidedOperatorStageButton").PerformClick();
                FindControl<Button>(studio,
                    "languageStudioGuidedKeywordPreviewButton").PerformClick();
                Require(FindControl<Button>(studio,
                            "languageStudioConfirmPreviewButton").Enabled,
                    "Il profilo multiplo non produce un'anteprima confermabile: " +
                    FindControl<TextBox>(studio,
                        "languageStudioDiagnosticsTextBox").Text);
                FindControl<Button>(studio,
                    "languageStudioConfirmPreviewButton").PerformClick();
                studioTools.Items["languageStudioValidateButton"].PerformClick();
                Require(FindControl<TextBox>(studio,
                            "languageStudioDiagnosticsTextBox").Text.Contains(
                                "completati correttamente"),
                    "Il profilo multiplo confermato non supera la convalida.");
                string archivePath = Path.Combine(Path.GetTempPath(),
                    "ddf-guided-profile-smoke-" + Guid.NewGuid().ToString("N") +
                    ".ddflang");
                try
                {
                    DdfLanguageAuthoringSession studioSession =
                        GetPrivateField<DdfLanguageAuthoringSession>(studio, "session");
                    DdfLanguagePackageArchiveResult archive =
                        DdfLanguagePackageArchiveBuilder.Generate(
                            studioSession.Project, archivePath);
                    DdfLanguagePackageLoadResult reloaded =
                        DdfLanguagePackageLoader.Load(archivePath);
                    Require(archive.Success && reloaded.Success &&
                            reloaded.Package.Language.TryGetKeyword(
                                elseKeyword, out DdfKeywordDefinition exportedIf) &&
                            exportedIf.RoleId == "If" &&
                            reloaded.Package.Language.TryGetKeyword(
                                "qualora_smoke", out DdfKeywordDefinition exportedElse) &&
                            exportedElse.RoleId == "Else" &&
                            reloaded.Package.Language.TryGetOperator("&&&",
                                out DdfOperatorDefinition exportedAnd) &&
                            exportedAnd.BinaryPrecedence == 4,
                        "Il profilo guidato non produce un archivio valido e ricaricabile.");
                }
                finally
                {
                    if (File.Exists(archivePath)) File.Delete(archivePath);
                }
                FindControl<Button>(studio, "languageStudioCloseButton").PerformClick();
                PumpMessages(40);
                Require(studio.IsDisposed, "Chiudi non termina il Package Studio.");
                InvokePrivate(form, "setDebugUiLock", true);
                InvokePrivate(form, "refreshEditorCommandCatalog");
                IReadOnlyList<CommandPaletteCommand> lockedCommands =
                    (IReadOnlyList<CommandPaletteCommand>)InvokePrivate(form,
                        "createCommandPaletteCommands");
                Require(!FindMenuItem(form,
                            "languagePackageStudioMenuItem").Enabled &&
                        !FindToolbarButton(form,
                            "toolbarLanguageStudioButton").Enabled &&
                        lockedCommands.Any(command => command.Id ==
                            "shell.languagePackageStudioMenuItem" &&
                            !command.Enabled),
                    "Package Studio resta eseguibile durante il debug.");
                InvokePrivate(form, "setDebugUiLock", false);
                using (var embeddedStudioBoundary = new MainForm(true))
                {
                    Require(!FindMenuItem(embeddedStudioBoundary,
                                "languagePackageStudioMenuItem").Available &&
                            !FindToolbarButton(embeddedStudioBoundary,
                                "toolbarLanguageStudioButton").Available &&
                            !GetPrivateField<ToolStripMenuItem>(
                                embeddedStudioBoundary,
                                "toolbarLanguageStudioMenuItem").Available &&
                            !((IReadOnlyList<CommandPaletteCommand>)InvokePrivate(
                                embeddedStudioBoundary,
                                "createCommandPaletteCommands")).Any(command =>
                                    command.Id ==
                                    "shell.languagePackageStudioMenuItem"),
                        "L'embedded espone Package Studio da menu, palette o toolbar.");
                }

                FindMenuItem(form, "manageLanguagePackagesMenuItem").PerformClick();
                PumpMessages(80);
                Form manager = Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(openForm => openForm.Name == "LanguagePackageManagerForm" && openForm.Owner == form);
                Require(manager != null && manager.StartPosition == FormStartPosition.CenterScreen &&
                        FindControl<ListView>(manager, "languagePackagesListView") != null &&
                        FindControl<Button>(manager, "languagePackageInstallArchiveButton") != null &&
                        FindControl<Button>(manager, "languagePackageInstallActiveButton") != null &&
                        FindControl<Label>(manager, "languagePackageManagerActiveLabel")
                            .Text.Contains("Attivo:", StringComparison.Ordinal) &&
                        FindControl<Button>(manager, "languagePackageDisableButton") != null,
                    "La gestione pacchetti non espone installazione, selezione e disattivazione.");
                foreach (Size size in new[] { manager.Size, manager.MinimumSize })
                {
                    manager.Size = size;
                    PumpMessages(40);
                    var close = FindControl<Button>(manager, "languagePackageManagerCloseButton");
                    foreach (string name in new[] { "languagePackageValidateButton", "languagePackageActivateButton",
                        "languagePackageDisableButton", "languagePackagePlaygroundButton" })
                    {
                        var action = FindControl<Button>(manager, name);
                        Require(action.Top == close.Top && action.Height == close.Height,
                            "Chiudi non è allineato agli altri comandi del gestore pacchetti.");
                        Require(action.Bounds.Right <= close.Left, "I pulsanti del gestore pacchetti si sovrappongono.");
                    }
                    Require(close.Parent.ClientRectangle.Contains(close.Bounds), "Chiudi esce dall'area dei pulsanti.");
                }
                FindControl<Button>(manager, "languagePackageManagerCloseButton").PerformClick();
                Require(manager.IsDisposed, "Chiudi non termina il gestore pacchetti.");

                FindMenuItem(form, "aboutMenuItem").PerformClick();
                PumpMessages(80);

                Form about = Application.OpenForms.Cast<Form>()
                    .FirstOrDefault(openForm => openForm.Name == "AboutForm" && openForm.Owner == form);
                Require(about != null && about.Visible, "About non ha aperto il popup informativo.");
                Require(about.StartPosition == FormStartPosition.CenterScreen,
                    "About non è configurato per apparire al centro dello schermo.");
                Require(FindControl<Label>(about, "aboutProductLabel").Text == "DDFLanguageEditor",
                    "About non riporta il nome dell'applicazione.");
                Require(FindControl<Label>(about, "aboutVersionLabel").Text ==
                    "Versione " + DdfProductVersionCatalog.Current.DisplayVersion,
                    "About non riporta versione e stato beta.");
                Require(FindControl<Label>(about, "aboutBuildLabel").Text.Contains("Build"),
                    "About non espone l'identificativo della build corrente.");
                Require(form.Controls.Find("statusStripMain", true).OfType<StatusStrip>()
                        .Single().Items.Cast<ToolStripItem>()
                        .Any(item => item.Name == "statusBuildLabel" &&
                            item.Text.Contains("Build", StringComparison.Ordinal)),
                    "La barra di stato non espone l'identificativo della build corrente.");
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

            Console.WriteLine("PASS menu Help: pacchetto linguaggio attivo, About, autore, sito, licenza, versione beta e icona");
        }

        private static void AssertEmbeddedModuleStoreCapability(bool visible)
        {
            string projectRoot = Path.Combine(Path.GetTempPath(),
                "ddf-embedded-module-capability-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectRoot);
            try
            {
                bool rejectedRelativeRoot = false;
                try
                {
                    _ = new DdfEmbeddedModuleStoreCapability("relative-project",
                        DdfEmbeddedModuleStoreOperations.BrowseAndValidate);
                }
                catch (ArgumentException) { rejectedRelativeRoot = true; }
                Require(rejectedRelativeRoot,
                    "La capability accetta una radice progetto relativa.");

                using (var host = new Form())
                using (var embedded = new DdfEmbeddedEditorControl())
                {
                    host.ShowInTaskbar = visible;
                    host.StartPosition = FormStartPosition.Manual;
                    host.Location = visible ? new Point(100, 100) : new Point(-32000, -32000);
                    host.Controls.Add(embedded);
                    host.Show();
                    PumpMessages(120);

                    var shell = (Form)FindControl<Control>(embedded, "embeddedDdfEditorShell");
                    ToolStripMenuItem modules = FindMenuItem(shell, "modulesMenuItem");
                    ToolStripMenuItem manage = FindMenuItem(shell, "manageModulePackagesMenuItem");
                    ToolStripButton toolbar = FindToolbarButton(shell, "toolbarModuleManagerButton");
                    Require(!modules.Available && !manage.Available && !toolbar.Available &&
                            embedded.ModuleStoreCapability == null,
                        "La shell embedded espone lo store moduli senza una capability host.");

                    var readOnlyCapability = new DdfEmbeddedModuleStoreCapability(projectRoot,
                        DdfEmbeddedModuleStoreOperations.BrowseAndValidate);
                    embedded.SetModuleStoreCapability(readOnlyCapability);
                    PumpMessages(60);
                    Require(modules.Available && manage.Available && toolbar.Available &&
                            ReferenceEquals(embedded.ModuleStoreCapability, readOnlyCapability),
                        "La capability esplicita non rende disponibile il gestore moduli embedded.");

                    manage.PerformClick();
                    PumpMessages(100);
                    Form manager = Application.OpenForms.Cast<Form>().FirstOrDefault(openForm =>
                        openForm.Name == "ModulePackageManagerForm");
                    Require(manager != null &&
                            FindControl<Button>(manager, "modulePackageValidateButton").Enabled &&
                            !FindControl<Button>(manager, "modulePackageInstallArchiveButton").Enabled &&
                            !FindControl<Button>(manager, "modulePackageInstallFolderButton").Enabled &&
                            !FindControl<Button>(manager, "modulePackageCreateButton").Enabled &&
                            !FindControl<Button>(manager, "modulePackageRemoveButton").Enabled,
                        "Il gestore embedded non applica i privilegi minimi della capability.");

                    embedded.RevokeModuleStoreCapability();
                    PumpMessages(60);
                    Require(manager.IsDisposed && !modules.Available && !manage.Available &&
                            !toolbar.Available && embedded.ModuleStoreCapability == null,
                        "La revoca non chiude il gestore o non nasconde i comandi embedded.");
                    host.Close();
                }
            }
            finally
            {
                if (Directory.Exists(projectRoot)) Directory.Delete(projectRoot, true);
            }
            Console.WriteLine("PASS capability host revocabile per store moduli embedded e privilegi minimi");
        }

        private static void AssertModuleMenu(bool visible)
        {
            string projectRoot = Path.Combine(Path.GetTempPath(), "ddf-module-ui-project-" + Guid.NewGuid().ToString("N"));
            string moduleRoot = Path.Combine(Path.GetTempPath(), "ddf-module-ui-source-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectRoot);
            Directory.CreateDirectory(moduleRoot);
            try
            {
                DdfLanguagePackage language = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
                File.WriteAllText(Path.Combine(projectRoot, "main.ddf"),
                    "main() out int { ret add(1, 2); }", new UTF8Encoding(false));
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit("Module UI", language, "main.ddf");
                project.Module.Id = "example.module-ui";
                project.Module.Version = "1.0.0";
                project.Dependencies.Add(new DdfProjectDependency { Id = "example.ui-library", Version = "1.0.0" });
                File.WriteAllText(Path.Combine(projectRoot, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                File.WriteAllText(Path.Combine(moduleRoot, "library.ddf"),
                    "add(int left, int right) out int { ret left + right; }", new UTF8Encoding(false));
                DdfProjectManifest module = DdfProjectManifest.CreateImplicit("UI library", language, null);
                module.Module.Id = "example.ui-library";
                module.Module.Version = "1.0.0";
                module.Module.Exports.Add("add");
                module.EntryPoint = "unused";
                File.WriteAllText(Path.Combine(moduleRoot, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(module, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
                DdfModulePackageResult installed = new DdfModulePackageStore(projectRoot).Install(moduleRoot);
                Require(installed.Success, "Preparazione del modulo per lo smoke UI non riuscita.");

                using (var form = ShowSmokeForm(visible))
                {
                    Require((bool)InvokePrivate(form, "openWorkspace", projectRoot),
                        "Il workspace per il gestore moduli non viene aperto.");
                    PumpUntil(() => GetPrivateField<object>(form, "workspaceIndex") != null, 4000,
                        "Il workspace per il gestore moduli non completa il caricamento.");
                    PumpUntil(() => GetPrivateField<DdfModuleEditorIndex>(form, "workspaceModuleIndex")
                            .Documents.Count == 1, 4000,
                        "Gli export del modulo installato non entrano nell'indice live dell'editor.");
                    string mainPath = Path.Combine(projectRoot, "main.ddf");
                    Require((bool)InvokePrivate(form, "openDocument", mainPath),
                        "Il sorgente del workspace modulare non viene aperto.");
                    PumpUntil(() => string.Equals(GetPrivateValue<string>(form, "lastAnalyzedText"),
                            File.ReadAllText(mainPath, Encoding.UTF8), StringComparison.Ordinal), 4000,
                        "L'analisi live del workspace modulare non termina.");
                    IReadOnlyList<DdfDiagnostic> moduleDiagnostics =
                        GetPrivateField<IReadOnlyList<DdfDiagnostic>>(form, "activeDiagnostics");
                    Require(!moduleDiagnostics.Any(diagnostic =>
                            diagnostic.Code == "DDF201" || diagnostic.Code == "DDF312"),
                        "L'editor segnala come sconosciuto un export di un modulo installato.");
                    Require(!SameToolbarIcon(FindToolbarButton(form, "toolbarModuleManagerButton"),
                            FindToolbarButton(form, "toolbarLanguageManagerButton")),
                        "Gestore moduli e gestore linguaggi usano la stessa icona.");
                    ToolStripMenuItem modules = FindMenuItem(form, "modulesMenuItem");
                    Require(modules.DropDownItems.Count == 1,
                        "La voce Moduli non contiene il comando del gestore.");
                    modules.PerformClick();
                    PumpMessages(30);
                    Require(!Application.OpenForms.Cast<Form>().Any(openForm =>
                            openForm.Name == "ModulePackageManagerForm" && openForm.Owner == form),
                        "Un clic sulla voce padre Moduli esegue inaspettatamente un comando.");
                    FindMenuItem(form, "manageModulePackagesMenuItem").PerformClick();
                    PumpMessages(100);
                    Form manager = Application.OpenForms.Cast<Form>().FirstOrDefault(openForm =>
                        openForm.Name == "ModulePackageManagerForm" && openForm.Owner == form);
                    Require(manager != null && manager.StartPosition == FormStartPosition.CenterScreen &&
                            IsLight(manager.BackColor),
                        "Il gestore moduli non è stato aperto come finestra chiara e centrata.");
                    ListView list = FindControl<ListView>(manager, "modulePackagesListView");
                    Require(list.Items.Count == 1 && list.Items[0].SubItems[1].Text == "example.ui-library" &&
                            list.Items[0].SubItems[2].Text == "1.0.0" &&
                            list.Items[0].SubItems[3].Text == "1",
                        "Il gestore non mostra identità, versione ed export del modulo installato.");
                    list.Items[0].Selected = true;
                    list.Select();
                    PumpMessages(40);
                    string details = FindControl<TextBox>(manager, "modulePackageDetailsTextBox").Text;
                    Require(details.Contains("example.ui-library@1.0.0") && details.Contains("add") &&
                            details.Contains("Dipendenze:"),
                        "Il dettaglio non mostra identità, export e dipendenze del modulo.");
                    foreach (string name in new[] { "modulePackageInstallArchiveButton", "modulePackageInstallFolderButton",
                        "modulePackageCreateButton", "modulePackageRemoveButton", "modulePackageValidateButton",
                        "modulePackageRefreshButton", "modulePackageManagerCloseButton" })
                        Require(FindControl<Button>(manager, name) != null,
                            "Comando mancante nel gestore moduli: " + name + ".");
                    FindControl<Button>(manager, "modulePackageManagerCloseButton").PerformClick();
                    PumpMessages(40);
                    Require(manager.IsDisposed, "Chiudi non termina il gestore moduli.");
                }
            }
            finally
            {
                if (Directory.Exists(projectRoot)) Directory.Delete(projectRoot, true);
                if (Directory.Exists(moduleRoot)) Directory.Delete(moduleRoot, true);
            }
            Console.WriteLine("PASS menu Moduli: store progetto, metadati, toolbar e gestore standalone");
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
                Require(ClipboardTextEqualsWithRetry("alpha"),
                    "Ctrl+C dipende ancora dallo stato obsoleto della voce di menu.");
                FindMenuItem(form, "cutMenuItem").Enabled = false;
                InvokeProcessCmdKey(form, Keys.Control | Keys.X);
                Require(editor.Text == " beta alpha",
                    "Ctrl+X dipende ancora dallo stato obsoleto della voce di menu.");
                editor.Undo();
                SetClipboardTextWithRetry("gamma");
                editor.Select(6, 4);
                FindMenuItem(form, "pasteMenuItem").Enabled = false;
                InvokeProcessCmdKey(form, Keys.Control | Keys.V);
                Require(editor.Text == "alpha gamma alpha",
                    "Ctrl+V dipende ancora dallo stato obsoleto della voce di menu.");
                editor.Undo();

                editor.Select(0, 5);
                editor.Focus();
                SetClipboardTextWithRetry("omega");
                InvokeHandler(form, "editMenuItem_DropDownOpening", FindMenuItem(form, "editMenuItem"), EventArgs.Empty);

                Require(FindMenuItem(form, "cutMenuItem").Enabled, "Taglia non è abilitato con una selezione.");
                Require(FindMenuItem(form, "copyMenuItem").Enabled, "Copia non è abilitato con una selezione.");
                Require(FindMenuItem(form, "pasteMenuItem").Enabled, "Incolla non è abilitato con testo negli appunti.");

                FindMenuItem(form, "copyMenuItem").PerformClick();
                Require(ClipboardTextEqualsWithRetry("alpha"), "Copia non ha trasferito la selezione negli appunti.");
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

                SetClipboardTextWithRetry("gamma");
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

                ToolStripMenuItem currentLine = FindMenuItem(form, "highlightCurrentLineMenuItem");
                ToolStripMenuItem whitespace = FindMenuItem(form, "showWhitespaceMenuItem");
                ToolStripMenuItem indentGuides = FindMenuItem(form, "showIndentGuidesMenuItem");
                ToolStripMenuItem wordWrap = FindMenuItem(form, "wordWrapMenuItem");
                ToolStripMenuItem overview = FindMenuItem(form, "showOverviewMenuItem");
                Control overviewBar = FindControl<Control>(form, "editorOverviewBar");
                Require(currentLine.Checked && !whitespace.Checked && indentGuides.Checked && !wordWrap.Checked &&
                        overview.Checked && overviewBar.Visible && overviewBar.Width >= 20,
                    "Le opzioni visive iniziali non corrispondono al profilo predefinito.");
                editor.Text = "main()\n{\n    int value << 1;\n\tret value;\n}";
                editor.Select(editor.Text.IndexOf("int", StringComparison.Ordinal), 0);
                int selectionStart = editor.SelectionStart;
                string visualSource = editor.Text;
                whitespace.PerformClick();
                PumpMessages(80);
                Require(whitespace.Checked && FindToolbarButton(form, "toolbarShowWhitespaceButton").Checked &&
                        GetControlProperty<bool>(editor, "ShowWhitespace") &&
                        GetControlProperty<int>(editor, "LastRenderedWhitespaceCount") > 0,
                    "Spazi, tab e fine riga non vengono renderizzati o sincronizzati con menu e toolbar.");
                indentGuides.PerformClick();
                FindToolbarButton(form, "toolbarShowIndentGuidesButton").PerformClick();
                PumpMessages(80);
                Require(indentGuides.Checked && GetControlProperty<int>(editor, "LastRenderedIndentGuideCount") > 0,
                    "Le guide di indentazione non vengono ripristinate e disegnate.");
                FindToolbarButton(form, "toolbarHighlightCurrentLineButton").PerformClick();
                Require(!currentLine.Checked && !GetControlProperty<bool>(editor, "HighlightCurrentLine"),
                    "L'evidenza della riga corrente non è controllabile dalla toolbar.");
                FindToolbarButton(form, "toolbarWordWrapButton").PerformClick();
                Require(wordWrap.Checked && editor.WordWrap && foldedView.WordWrap,
                    "L'a capo automatico non viene applicato a editor e vista compressa.");
                FindToolbarButton(form, "toolbarOverviewButton").PerformClick();
                Require(!overview.Checked && !overviewBar.Visible,
                    "L'overview non è disattivabile dalla toolbar.");
                overview.PerformClick();
                PumpMessages(80);
                Require(overview.Checked && overviewBar.Visible &&
                        GetControlProperty<int>(overviewBar, "TotalLines") == editor.Lines.Length &&
                        GetControlProperty<int>(overviewBar, "CurrentLine") == 3,
                    "L'overview non è sincronizzata con menu, toolbar e righe del documento.");
                int editorLineY = editor.GetPositionFromCharIndex(editor.GetFirstCharIndexFromLine(2)).Y;
                int overviewLineY = (int)overviewBar.GetType().GetMethod("markerToY",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(overviewBar, new object[] { 3, false });
                Require(Math.Abs(editorLineY - overviewLineY) <= 1,
                    "La riga visibile dell'overview non è allineata alla posizione reale nell'editor.");
                editor.Select(editor.GetFirstCharIndexFromLine(3), 0);
                PumpMessages(20);
                Require(GetControlProperty<int>(overviewBar, "CurrentLine") == 4,
                    "Il cursore dell'overview non segue SelectionChanged in tempo reale.");
                editor.Select(selectionStart, 0);
                Require(editor.Text == visualSource && editor.SelectionStart == selectionStart,
                    "Le decorazioni visive modificano il sorgente o spostano il cursore.");
                editor.Text = "main() out int\n{\n    int value << 1\n    ret value;\n}";
                editor.Select(editor.GetFirstCharIndexFromLine(2), 0);
                PumpMessages(260);
                Require(GetControlProperty<int>(overviewBar, "ErrorMarkerCount") > 0,
                    "L'overview non espone le diagnostiche del documento.");
                InvokePrivate(form, "toggleBreakpointAtLine", 4);
                Require(GetControlProperty<int>(overviewBar, "BreakpointMarkerCount") == 1,
                    "L'overview non espone i breakpoint del documento.");
                InvokeControlMouseDown(overviewBar, overviewBar.ClientSize.Height - 1);
                Require(editor.GetLineFromCharIndex(editor.SelectionStart) == editor.Lines.Length - 1,
                    "Il clic sull'overview non naviga alla riga corrispondente.");
                InvokePrivate(form, "clearBreakpoints");

                editor.Text = string.Join("\n", Enumerable.Range(1, 50).Select(index => "int value" + index + " << ;"));
                editor.Select(editor.GetFirstCharIndexFromLine(39), 0);
                editor.ScrollToCaret();
                PumpMessages(280);
                int caretY = editor.GetPositionFromCharIndex(editor.SelectionStart).Y;
                int overviewCaretY = (int)overviewBar.GetType().GetMethod("markerToY",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(overviewBar, new object[] { 40, true });
                Require(GetControlProperty<int>(overviewBar, "CurrentLine") == 40 &&
                        Math.Abs(caretY - overviewCaretY) <= 1 &&
                        GetControlProperty<int>(overviewBar, "ErrorMarkerCount") >= 40,
                    "Cursore o diagnostiche dell'overview lunga non seguono la viewport reale.");

                var offsetDiagnostic = new DdfDiagnostic(
                    "OVERVIEW", "Posizione di prova", 0, 1, 50, 1, DdfDiagnosticSeverity.Error);
                SetPrivateField(form, "activeDiagnostics", new List<DdfDiagnostic> { offsetDiagnostic });
                SetPrivateField(form, "overviewDiagnosticsSource", editor.Text);
                InvokePrivate(form, "refreshEditorOverview");
                object markerCollection = GetPrivateField<object>(overviewBar, "markers");
                object diagnosticMarker = ((System.Collections.IEnumerable)markerCollection).Cast<object>().Single();
                Require(GetPropertyValue<int>(diagnosticMarker, "Line") == 1 &&
                        GetPropertyValue<int>(diagnosticMarker, "Position") == 0,
                    "L'overview usa il numero di riga dichiarato invece dell'offset realmente sottolineato.");
                editor.Select(0, 0);
                PumpMessages(20);
                MethodInfo markerToYMethod = overviewBar.GetType().GetMethod(
                    "markerToY", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo markerTopMethod = overviewBar.GetType().GetMethod(
                    "markerTop", BindingFlags.Instance | BindingFlags.NonPublic);
                int errorY = (int)markerToYMethod.Invoke(overviewBar, new object[] { 1, false });
                int cursorY = (int)markerToYMethod.Invoke(overviewBar, new object[] { 1, true });
                Require((int)markerTopMethod.Invoke(overviewBar, new object[] { errorY }) ==
                        (int)markerTopMethod.Invoke(overviewBar, new object[] { cursorY }),
                    "Tacca azzurra e tacca diagnostica non condividono gli stessi pixel verticali.");
                editor.AppendText("\n");
                Require(GetControlProperty<int>(overviewBar, "ErrorMarkerCount") == 0,
                    "L'overview riproietta uno snapshot diagnostico obsoleto durante la digitazione.");
            }

            Console.WriteLine("PASS menu Visualizza: folding, riga corrente, invisibili, guide, word wrap e overview");
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
                    Require(FindControl<Button>(form, "breadcrumbFileButton").Text == "salvato-con-nome.ddf",
                        "Il breadcrumb non aggiorna il nome del file dopo Salva con nome.");

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

            Console.WriteLine("PASS menu File: documenti, cartella workspace, file/cartelle recenti ed Esci");
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
            DisableRecentFilePersistence(form);
            ResetEditorSettingsForSmoke(form);
            ResetShortcutSettingsForSmoke(form);
            ResetPaletteLayoutForSmoke(form);
            ResetToolbarLayoutForSmoke(form);
            form.Show();
            PumpMessages(100);
            return form;
        }

        private static void DisableRecentFilePersistence(MainForm form)
        {
            SetPrivateField(form, "saveRecentFilesSetting", new Action<string>(value => { }));
            SetPrivateField(form, "saveRecentWorkspacesSetting", new Action<string>(value => { }));
            SetPrivateField(form, "saveEditorSettingsSetting", new Action<string>(value => { }));
            SetPrivateField(form, "saveShortcutSettingsSetting", new Action<string>(value => { }));
            SetPrivateField(form, "savePaletteLayoutSetting", new Action<string>(value => { }));
            SetPrivateField(form, "loadHotExitSnapshotSetting", new Func<string>(() => string.Empty));
            SetPrivateField(form, "saveHotExitSnapshotSetting", new Action<string>(value => { }));
            SetPrivateField(form, "clearHotExitSnapshotSetting", new Action(() => { }));
            SetPrivateField(form, "loadWorkspaceSessionSetting", new Func<string>(() => string.Empty));
            SetPrivateField(form, "saveWorkspaceSessionSetting", new Action<string>(value => { }));
            SetPrivateField(form, "clearWorkspaceSessionSetting", new Action(() => { }));
            SetPrivateField(form, "loadCrashRecoverySetting", new Func<string>(() => string.Empty));
            SetPrivateField(form, "saveCrashRecoverySetting", new Action<string>(value => { }));
            SetPrivateField(form, "clearCrashRecoverySetting", new Action(() => { }));
            SetPrivateField(form, "watchExpressionSettings", new WatchExpressionSettings());
            SetPrivateField(form, "loadWatchExpressionsSetting", new Func<string>(() => string.Empty));
            SetPrivateField(form, "saveWatchExpressionsSetting", new Action<string>(value => { }));
            SetPrivateField(form, "saveProductUpdateSettingsSetting",
                new Action<string>(value => { }));
            InvokePrivate(form, "switchWatchProject", (object)null);
            GetPrivateField<System.Windows.Forms.Timer>(form, "externalChangeTimer").Stop();
        }

        private static void ResetEditorSettingsForSmoke(MainForm form)
        {
            SetPrivateField(form, "editorSettings", EditorSettings.Default);
            InvokePrivate(form, "applyEditorSettings");
            InvokePrivate(form, "updateAutoSaveBehavior");
            InvokePrivate(form, "updateCrashRecoveryBehavior");
        }

        private static void ResetShortcutSettingsForSmoke(MainForm form)
        {
            IReadOnlyList<ShortcutCommandDefinition> definitions =
                GetPrivateField<IReadOnlyList<ShortcutCommandDefinition>>(form, "shortcutDefinitions");
            SetPrivateField(form, "shortcutSettings", ShortcutSettings.Default(definitions));
            SetPrivateField(form, "saveShortcutSettingsSetting", new Action<string>(value => { }));
            InvokePrivate(form, "applyShortcutSettings");
        }

        private static void ResetPaletteLayoutForSmoke(MainForm form)
        {
            SetPrivateField(form, "outlineExpandedWidth", 300);
            SetPrivateField(form, "diagnosticsExpandedHeight", 116);
            SetPrivateField(form, "outlinePinned", true);
            SetPrivateField(form, "diagnosticsPinned", true);
            SetPrivateField(form, "flowchartVisible", true);
            SetPrivateField(form, "savePaletteLayoutSetting", new Action<string>(value => { }));
            InvokePrivate(form, "applyFlowchartVisibility", false);
        }

        private static void ResetToolbarLayoutForSmoke(MainForm form)
        {
            Type settingsType = typeof(MainForm).Assembly.GetType(
                "DDF___Program_Language_Editor.ToolbarLayoutSettings", true);
            object settings = settingsType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[]
                {
                    string.Empty,
                    new[] { "file", "edit", "code", "navigation", "view", "project", "debug" }
                });
            SetPrivateField(form, "toolbarLayoutSettings", settings);
            SetPrivateField(form, "saveToolbarLayoutSetting", new Action<string>(value => { }));
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

        private static bool ClipboardTextEqualsWithRetry(string expected)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    string text = Clipboard.GetText();
                    if (string.Equals(text, expected, StringComparison.Ordinal)) return true;
                }
                catch (ExternalException)
                {
                }

                PumpMessages(20);
            }

            return false;
        }

        private static void SetClipboardTextWithRetry(string text)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    if (ClipboardTextEqualsWithRetry(text)) return;
                }
                catch (ExternalException)
                {
                }

                PumpMessages(20);
            }

            throw new InvalidOperationException(
                "Impossibile preparare in modo verificabile gli appunti per lo smoke.");
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

        private static void AssertFunctionNameHighlight(MainForm form,
            RichTextBox editor)
        {
            const string source = "sum(int value) out int { ret value; }\n" +
                "main() out int { int result << sum(2); print(\"sum(3)\"); " +
                "// sum(4)\n ret result; }";
            editor.Text = source;
            editor.ClearUndo();
            InvokePrivate(form, "applyHighlighting");
            Color functionColor = Color.FromArgb(220, 220, 170);
            int declaration = source.IndexOf("sum(", StringComparison.Ordinal);
            int call = source.IndexOf("sum(2)", StringComparison.Ordinal);
            int standard = source.IndexOf("print(", StringComparison.Ordinal);
            foreach (int position in new[] { declaration, call, standard })
            {
                editor.Select(position, 3);
                Require(editor.SelectionColor == functionColor,
                    "Il nome di una funzione dichiarata o richiamata non usa il colore dedicato.");
            }
            int stringPosition = source.IndexOf("sum(3)", StringComparison.Ordinal);
            editor.Select(stringPosition, 3);
            Require(editor.SelectionColor != functionColor,
                "La stringa viene confusa con un richiamo di funzione.");
            int commentPosition = source.IndexOf("sum(4)", StringComparison.Ordinal);
            editor.Select(commentPosition, 3);
            Require(editor.SelectionColor != functionColor,
                "Il commento viene confuso con un richiamo di funzione.");

            editor.Select(call, 3);
            editor.SelectedText = "sum";
            InvokePrivate(form, "applyHighlighting");
            editor.Select(call, 3);
            Require(editor.SelectionColor == functionColor,
                "La colorazione del richiamo non sopravvive all'analisi incrementale.");
            Console.WriteLine("PASS colorazione nomi funzione in dichiarazione e richiamo, RTF e incrementale");
        }

        private static void AssertAccessibilityAndDpi(MainForm form, RichTextBox editor)
        {
            Require(form.AutoScaleMode == AutoScaleMode.Dpi,
                "La finestra principale non usa lo scaling DPI per-monitor.");

            Control[] unnamed = Descendants(form)
                .Where(control => control.TabStop && control.Enabled && control.Visible &&
                    string.IsNullOrWhiteSpace(control.AccessibleName))
                .ToArray();
            Require(unnamed.Length == 0,
                "Controlli raggiungibili da tastiera senza nome accessibile: " +
                string.Join(", ", unnamed.Select(control => control.Name)));

            Require(FindToolbars(form).SelectMany(toolbar => toolbar.Items.OfType<ToolStripButton>()).All(button =>
                    !string.IsNullOrWhiteSpace(button.AccessibleName)),
                "La toolbar contiene comandi senza nome accessibile.");
            Require(contrastRatio(form.ForeColor, form.BackColor) >= 4.5D,
                "Il testo principale non raggiunge il contrasto minimo 4.5:1.");

            Type dpiLayout = typeof(MainForm).Assembly.GetType("DDF___Program_Language_Editor.DpiLayout", true);
            MethodInfo scale = dpiLayout.GetMethod("ScaleLogical", BindingFlags.Static | BindingFlags.Public);
            MethodInfo unscale = dpiLayout.GetMethod("ToLogical", BindingFlags.Static | BindingFlags.Public);
            MethodInfo clamp = dpiLayout.GetMethod("ClampToClientPercentage", BindingFlags.Static | BindingFlags.Public);
            Require((int)scale.Invoke(null, new object[] { 300, 144 }) == 450 &&
                    (int)unscale.Invoke(null, new object[] { 450, 144 }) == 300,
                "La conversione delle dimensioni persistenti tra DPI logici e fisici non è reversibile.");
            Require((int)clamp.Invoke(null, new object[] { 1072, 1916, 450, 40 }) == 766,
                "La palette non viene limitata proporzionalmente sul desktop al 150%. ");

            MethodInfo compactToolbar = typeof(MainForm).GetMethod("compactToolbarLocation",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require((int)compactToolbar.Invoke(null, new object[] { 420, 900, 260, 1024 }) == 420 &&
                    (int)compactToolbar.Invoke(null, new object[] { 420, 500, 260, 1024 }) == 500,
                "Le icon bar non eliminano gli spazi persistiti quando lo spazio DPI è insufficiente.");

            Type flowchartCanvas = typeof(MainForm).Assembly.GetType(
                "DDF___Program_Language_Editor.FlowchartCanvas", true);
            MethodInfo flowchartDpiScale = flowchartCanvas.GetMethod("DpiScaleFor",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Require(Math.Abs((float)flowchartDpiScale.Invoke(null, new object[] { 144 }) - 1.5F) < 0.001F,
                "La geometria visuale della Flowchart non segue la scala DPI al 150%.");

            TreeView outline = FindControl<TreeView>(form, "treeViewOutline");
            ListBox diagnostics = FindControl<ListBox>(form, "listBoxDiagnostics");
            Panel outlinePanel = FindControl<Panel>(form, "panelOutline");
            Panel diagnosticsPanel = FindControl<Panel>(form, "panelDiagnostics");
            Button outlinePin = FindControl<Button>(form, "buttonOutlinePin");
            Button diagnosticsPin = FindControl<Button>(form, "buttonDiagnosticsPin");
            TabControl navigationTabs = FindControl<TabControl>(form, "navigationTabs");
            TabControl bottomTabs = FindControl<TabControl>(form, "tabControlBottom");
            navigationTabs.SelectedTab = FindControl<TabPage>(form, "workspaceTabPage");
            bottomTabs.SelectedTab = FindControl<TabPage>(form, "tabPageOutput");
            outlinePin.PerformClick();
            diagnosticsPin.PerformClick();
            editor.Focus();
            Require(InvokeFilteredKey(form, editor, Keys.F6),
                "Il percorso reale dei messaggi tastiera non intercetta F6 dall'editor.");
            PumpMessages(40);
            Require(outline.ContainsFocus && outlinePanel.Width > 28 &&
                    navigationTabs.SelectedTab.Name == "outlineTabPage" &&
                    !Convert.ToBoolean(GetPrivateField<object>(form, "outlinePinned")),
                "F6 non sposta il focus dall'editor all'Outline.");
            PumpMessages(350);
            Require(outline.ContainsFocus && outlinePanel.Width > 28,
                "L'auto-hide richiude l'Outline mentre contiene il focus da tastiera.");
            TreeView workspace = FindControl<TreeView>(form, "treeViewWorkspace");
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.Tab) &&
                    navigationTabs.SelectedTab.Name == "workspaceTabPage" && workspace.ContainsFocus,
                "Ctrl+Tab non passa da Outline alla scheda File della palette laterale.");
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.Shift | Keys.Tab) &&
                    navigationTabs.SelectedTab.Name == "outlineTabPage" && outline.ContainsFocus,
                "Ctrl+Shift+Tab non torna alla scheda Outline.");
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.PageDown) &&
                    navigationTabs.SelectedTab.Name == "workspaceTabPage" &&
                    InvokeProcessCmdKey(form, Keys.Control | Keys.PageUp) &&
                    navigationTabs.SelectedTab.Name == "outlineTabPage",
                "Ctrl+PagSu/PagGiù non cambia le schede della palette laterale.");
            Require(InvokeFilteredKey(form, outline, Keys.F6),
                "Il percorso reale dei messaggi tastiera non intercetta F6 dall'Outline.");
            PumpMessages(40);
            Require(diagnostics.ContainsFocus && diagnosticsPanel.Height > 26 &&
                    bottomTabs.SelectedTab.Name == "tabPageDiagnostics" &&
                    !Convert.ToBoolean(GetPrivateField<object>(form, "diagnosticsPinned")),
                "F6 non sposta il focus dall'Outline alla Diagnostica.");
            PumpMessages(350);
            Require(diagnostics.ContainsFocus && diagnosticsPanel.Height > 26,
                "L'auto-hide richiude la Diagnostica mentre contiene il focus da tastiera.");
            RichTextBox output = FindControl<RichTextBox>(form, "richTextBoxOutput");
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.Tab) &&
                    bottomTabs.SelectedTab.Name == "tabPageOutput" && output.ContainsFocus,
                "Ctrl+Tab non passa da Diagnostica alla scheda Output.");
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.Shift | Keys.Tab) &&
                    bottomTabs.SelectedTab.Name == "tabPageDiagnostics" && diagnostics.ContainsFocus,
                "Ctrl+Shift+Tab non torna alla scheda Diagnostica.");
            Require(InvokeProcessCmdKey(form, Keys.Shift | Keys.F6) && outline.ContainsFocus,
                "Shift+F6 non percorre le palette in ordine inverso.");
            Require(InvokeProcessCmdKey(form, Keys.Shift | Keys.F6) && editor.ContainsFocus,
                "Shift+F6 non riporta il focus nell'editor.");

            RichTextBox foldedView = FindControl<RichTextBox>(form, "richTextBoxFoldedView");
            editor.Visible = false;
            foldedView.Visible = true;
            outline.Focus();
            Require(InvokeProcessCmdKey(form, Keys.Shift | Keys.F6) && foldedView.ContainsFocus,
                "Shift+F6 tenta di focalizzare l'editor sorgente nascosto durante il folding.");
            foldedView.Visible = false;
            editor.Visible = true;
            editor.Focus();
            outlinePin.PerformClick();
            diagnosticsPin.PerformClick();
            Console.WriteLine("PASS accessibilità: nomi, contrasto, F6, schede palette e layout DPI per-monitor");
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in Descendants(child)) yield return descendant;
            }
        }

        private static double contrastRatio(Color foreground, Color background)
        {
            double light = Math.Max(relativeLuminance(foreground), relativeLuminance(background));
            double dark = Math.Min(relativeLuminance(foreground), relativeLuminance(background));
            return (light + 0.05D) / (dark + 0.05D);
        }

        private static double relativeLuminance(Color color)
        {
            Func<byte, double> channel = value =>
            {
                double normalized = value / 255D;
                return normalized <= 0.03928D
                    ? normalized / 12.92D
                    : Math.Pow((normalized + 0.055D) / 1.055D, 2.4D);
            };
            return 0.2126D * channel(color.R) + 0.7152D * channel(color.G) + 0.0722D * channel(color.B);
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
            ToolStripPanel toolbar = FindControl<ToolStripPanel>(form, "toolStripPanelMain");
            TabControl navigation = FindControl<TabControl>(form, "navigationTabs");
            Button outlinePin = FindControl<Button>(form, "buttonOutlinePin");
            Button diagnosticsPin = FindControl<Button>(form, "buttonDiagnosticsPin");
            string persistedLayout = null;
            SetPrivateField(form, "savePaletteLayoutSetting", new Action<string>(value => persistedLayout = value));
            Require(outlinePanel.Width >= 300 && outlinePanel.Width <= 310,
                "La palette destra non misura i 300 px previsti all'avvio: " + outlinePanel.Width + " px.");
            Require(navigation.Appearance == TabAppearance.Normal,
                "Workspace e Outline non usano schede standard come Diagnostica e Output.");
            Require(outlinePanel.Top == 0 && outlinePanel.Bottom == status.Top && toolbar.Right == outlineSplitter.Left,
                "La palette destra non occupa tutta l'altezza utile della form: outline=" + outlinePanel.Bounds +
                ", barre=" + toolbar.Bounds + ", stato=" + status.Bounds + ".");
            Require(diagnosticsPanelFromForm.Right == outlineSplitter.Left && diagnosticsPanelFromForm.Width < form.ClientSize.Width,
                "Diagnostica e Output non si adattano allo spazio lasciato dalla palette destra.");
            Require(diagnosticsSplitter.Dock == DockStyle.Bottom && diagnosticsSplitter.Cursor == Cursors.HSplit &&
                    diagnosticsSplitter.Bottom == diagnosticsPanelFromForm.Top,
                "La palette inferiore non ha uno splitter orizzontale correttamente posizionato.");

            outlinePanel.Width = 340;
            form.PerformLayout();
            InvokeHandler(form, "splitterOutline_SplitterMoved", outlineSplitter,
                new SplitterEventArgs(0, 0, 0, 0));
            Require(Convert.ToInt32(GetPrivateField<object>(form, "outlineExpandedWidth")) == 340,
                "La larghezza impostata tramite splitter non viene memorizzata.");

            SetPrivateField(form, "outlineExpandedWidth", 2000);
            InvokePrivate(form, "expandOutlinePalette");
            Require(outlinePanel.Width <= (form.ClientSize.Width - outlineSplitter.Width) * 40 / 100 + 1,
                "L'Outline persistito può ancora sottrarre troppo spazio all'editor.");
            SetPrivateField(form, "outlineExpandedWidth", 340);
            InvokePrivate(form, "expandOutlinePalette");

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
            IDdfEditorPaletteService palettes = FindControl<DdfEditorView>(form, "panelEditor").PaletteService;
            palettes.SetVisible(DdfEditorPaletteIds.Output, false);
            PaletteLayoutSettings hiddenPersisted = PaletteLayoutSettings.Parse(persistedLayout);
            Require(hiddenPersisted.HiddenPaletteIds.Contains(DdfEditorPaletteIds.Output),
                "La visibilità delle singole palette non viene memorizzata.");
            palettes.SetVisible(DdfEditorPaletteIds.Output, true);
            PaletteLayoutSettings persisted = PaletteLayoutSettings.Parse(persistedLayout);
            Require(persisted.OutlineWidth == 340 && persisted.DiagnosticsHeight == 210 &&
                    persisted.OutlinePinned && persisted.DiagnosticsPinned && persisted.FlowchartVisible &&
                    !persisted.HiddenPaletteIds.Contains(DdfEditorPaletteIds.Output),
                "Dimensioni o stato pin delle palette non vengono serializzati correttamente.");
            SetPrivateField(form, "savePaletteLayoutSetting", new Action<string>(value => { }));
            Console.WriteLine("PASS gutter e palette con dimensioni e pin persistenti tra gli avvii");
        }

        private static void AssertWrappedGutterHasNoMissingLogicalLines(
            MainForm form, RichTextBox editor, RichTextBox lineNumbers)
        {
            editor = GetPrivateField<RichTextBox>(form, "richTextBoxMainEditor");
            form.GetType().GetMethod("leaveFoldedView", BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null).Invoke(form, null);
            editor.BringToFront();
            PumpMessages(20);
            string originalText = editor.Text;
            int originalSelection = editor.SelectionStart;
            bool originalWordWrap = editor.WordWrap;
            try
            {
                editor.WordWrap = true;
                editor.Text = string.Join("\n", Enumerable.Range(1, 36).Select(line =>
                    line % 3 == 0
                        ? "string wrapped" + line + " << \"" + new string('x', 180) + "\";"
                        : "int value" + line + " << " + line + ";"));
                editor.Select(editor.TextLength, 0);
                editor.ScrollToCaret();
                PumpMessages(120);
                InvokePrivate(form, "updateLineNumbers");

                var displayed = GetPropertyValue<IReadOnlyList<int>>(lineNumbers, "DisplayedSourceLines");
                var visibleLogicalLines = new List<int>();
                int logicalLine = 1;
                for (int start = 0; start <= editor.TextLength; logicalLine++)
                {
                    int y = editor.GetPositionFromCharIndex(start).Y;
                    if (y >= 0 && y < editor.ClientSize.Height) visibleLogicalLines.Add(logicalLine);
                    int newline = editor.Text.IndexOf('\n', start);
                    if (newline < 0) break;
                    start = newline + 1;
                }
                Require(visibleLogicalLines.Count > 2 && visibleLogicalLines.All(displayed.Contains),
                    "Il gutter con a capo automatico omette una o più righe logiche visibili. Visibili=" +
                    string.Join(",", visibleLogicalLines) + "; gutter=" + string.Join(",", displayed) +
                    "; righe=" + editor.Lines.Length + "; visibile=" + editor.Visible +
                    "; size=" + editor.ClientSize + "; fieldSame=" +
                    ReferenceEquals(editor, GetPrivateField<RichTextBox>(form, "richTextBoxMainEditor")));

                editor.SelectedText = "\nint newlyTyped << 37;";
                editor.ScrollToCaret();
                PumpMessages(80);
                InvokePrivate(form, "updateLineNumbers");
                displayed = GetPropertyValue<IReadOnlyList<int>>(lineNumbers, "DisplayedSourceLines");
                int typedLine = editor.Text.Count(character => character == '\n') + 1;
                Require(displayed.Contains(typedLine),
                    "Il gutter non aggiunge immediatamente la nuova riga durante la digitazione.");
            }
            finally
            {
                editor.WordWrap = originalWordWrap;
                editor.Text = originalText;
                editor.Select(Math.Min(originalSelection, editor.TextLength), 0);
                InvokePrivate(form, "updateLineNumbers");
            }
            Console.WriteLine("PASS gutter continuo durante digitazione e a capo automatico");
        }

        private static void RequireVisibleLogicalLinesInGutter(RichTextBox editor, RichTextBox lineNumbers)
        {
            var displayed = GetPropertyValue<IReadOnlyList<int>>(lineNumbers, "DisplayedSourceLines");
            var visible = new List<int>();
            int logicalLine = 1;
            for (int start = 0; start <= editor.TextLength; logicalLine++)
            {
                int y = editor.GetPositionFromCharIndex(start).Y;
                if (y >= 0 && y < editor.ClientSize.Height) visible.Add(logicalLine);
                int newline = editor.Text.IndexOf('\n', start);
                if (newline < 0) break;
                start = newline + 1;
            }
            Require(visible.Count > 0 && visible.All(displayed.Contains),
                "Il gutter non contiene tutte le righe sorgente visibili. Visibili=" +
                string.Join(",", visible) + "; gutter=" + string.Join(",", displayed));
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

        private static void AssertContextualBreadcrumb(MainForm form, RichTextBox editor)
        {
            const string source =
                "main() out int\n{\n" +
                "    if(true)\n    {\n" +
                "        while(false)\n        {\n" +
                "            ret 1;\n        }\n    }\n}";
            editor.Text = source;
            int returnPosition = source.IndexOf("ret 1", StringComparison.Ordinal);
            editor.Select(returnPosition, 0);
            PumpMessages(260);

            FlowLayoutPanel breadcrumb = FindControl<FlowLayoutPanel>(form, "breadcrumbPanel");
            string[] labels = breadcrumb.Controls.OfType<Button>().Select(button => button.Text).ToArray();
            Require(labels.SequenceEqual(new[] { "Senza titolo.ddf", "main()", "if", "while" }),
                "Il breadcrumb non rappresenta file, funzione e blocchi annidati: " + string.Join(" > ", labels));

            Button ifButton = FindControl<Button>(form, "breadcrumbItemButton1");
            ifButton.PerformClick();
            Require(editor.SelectedText == "if", "Il segmento if del breadcrumb non è navigabile.");
            Require(FindMenuItem(form, "navigateBackMenuItem").Enabled,
                "Il click sul breadcrumb non alimenta la cronologia di navigazione.");
            FindMenuItem(form, "navigateBackMenuItem").PerformClick();
            Require(editor.SelectionStart == returnPosition,
                "Indietro non ripristina la posizione precedente al click sul breadcrumb.");
            Console.WriteLine("PASS breadcrumb cliccabile per file, funzione e blocchi correnti");
        }

        private static void AssertCommandPalette(MainForm form, RichTextBox editor)
        {
            const string source = "main() out int{ret 1;}";
            editor.Text = source;
            editor.Select(editor.TextLength, 0);
            PumpMessages(220);
            int dialogCount = 0;
            SetPrivateField(form, "showCommandPaletteDialog", new Func<CommandPaletteForm, DialogResult>(dialog =>
            {
                dialogCount++;
                Require(dialog.StartPosition == FormStartPosition.CenterScreen,
                    "La Command Palette non si apre al centro dello schermo.");
                TextBox query = FindControl<TextBox>(dialog, "commandPaletteQueryTextBox");
                ListView results = FindControl<ListView>(dialog, "commandPaletteResultsListView");
                Button execute = FindControl<Button>(dialog, "commandPaletteExecuteButton");

                query.Text = "Arresta";
                Require(results.Items.Count == 1 &&
                        results.Items[0].SubItems[3].Text == "Non disponibile" && !execute.Enabled,
                    "La palette non mostra chiaramente un comando indisponibile.");
                query.Text = "Formatta documento";
                Require(results.Items.Count == 1 && results.Items[0].SubItems[1].Text == "Modifica" &&
                        !string.IsNullOrWhiteSpace(results.Items[0].SubItems[2].Text) && execute.Enabled,
                    "La palette non filtra comando, categoria, scorciatoia e disponibilità.");
                return DialogResult.OK;
            }));

            FindMenuItem(form, "commandPaletteMenuItem").PerformClick();
            PumpMessages(220);
            string expected = DdfFormatter.Format(source).Text;
            Require(editor.Text == expected && dialogCount == 1,
                "La Command Palette non riutilizza l'handler reale di Formatta documento.");

            SetPrivateField(form, "showCommandPaletteDialog", new Func<CommandPaletteForm, DialogResult>(dialog =>
            {
                dialogCount++;
                return DialogResult.Cancel;
            }));
            FindToolbarButton(form, "toolbarCommandPaletteButton").PerformClick();
            Require(dialogCount == 2, "Il pulsante toolbar non apre la Command Palette.");
            Console.WriteLine("PASS Command Palette filtrabile con stato, categorie, shortcut ed esecuzione reale");
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

            int functionName = foldedView.Text.IndexOf("main(", StringComparison.Ordinal);
            foldedView.Select(functionName, 4);
            Require(foldedView.SelectionColor == Color.FromArgb(220, 220, 170),
                "Il nome della funzione ha perso il colore nella vista compressa.");

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
            string typedCompletionSource =
                GetPrivateField<string>(form, "activeCompletionSource");
            Require(typedResult != null && typedResult.ExpectedType == "string" &&
                    typedResult.Context == DdfCompletionContextKind.Expression &&
                    typedCompletionSource == editor.Text,
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
            Require(!completion.Visible &&
                    GetPrivateField<DdfCompletionResult>(form, "activeCompletionResult") == null &&
                    GetPrivateField<string>(form, "activeCompletionSource") == null,
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
            SetClipboardTextWithRetry("    if(true)\r\n    {\r\n        ret 1;\r\n    }");
            InvokeProcessCmdKey(form, Keys.Control | Keys.V);
            Require(editor.Text == "main()\n{\n    if(true)\n    {\n        ret 1;\n    }\n}",
                "L'incolla multilinea non adatta il rientro al blocco corrente: " + editor.Text.Replace("\n", "\\n"));
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
            SetClipboardTextWithRetry("item");
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
                DdfDocumentFile.Save(mainPath, "main() out int\n{\n    int value << helper();\n    ret value;\n}");
                DdfDocumentFile.Save(helperPath,
                    "// library declaration\nhelper() out int\n{\n    int value << 1;\n    ret value;\n}\n" +
                    "secondary() out int\n{\n    int other << 2;\n    ret other;\n}");

                SetPrivateField(form, "showWorkspaceDialog", new Func<FolderBrowserDialog, DialogResult>(dialog =>
                {
                    dialog.SelectedPath = directory;
                    return DialogResult.OK;
                }));
                FindMenuItem(form, "openWorkspaceMenuItem").PerformClick();
                TreeView workspaceTree = FindControl<TreeView>(form, "treeViewWorkspace");
                PumpUntil(
                    () => workspaceTree.Nodes.Count == 1 && FindControl<Label>(form, "labelWorkspace").Text.Contains("2 file"),
                    5000,
                    "Apri cartella non ha popolato l'explorer con i sorgenti DDF.");
                ToolStripMenuItem recentWorkspace = FindMenuItem(form, "recentWorkspacesMenuItem").DropDownItems
                    .OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => string.Equals(item.Tag as string, directory, StringComparison.OrdinalIgnoreCase));
                Require(recentWorkspace != null, "Cartelle recenti non contiene il workspace appena aperto.");

                TreeNode mainNode = FindTreeNodeByTag(workspaceTree.Nodes, mainPath);
                Require(mainNode != null, "Il file principale non è presente nell'explorer workspace.");
                GetPrivateField<DocumentSession>(form, "documentSession").SetUntitled();
                InvokeHandler(form, "treeViewWorkspace_NodeMouseDoubleClick", workspaceTree,
                    new TreeNodeMouseClickEventArgs(mainNode, MouseButtons.Left, 2, 0, 0));
                PumpMessages(220);
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                DdfEditorView editorSurface = FindControl<DdfEditorView>(form, "panelEditor");
                Require(editor.Text.Contains("helper()"), "Il doppio clic sul file workspace non ha aperto il documento.");
                bool openedDocumentDirty = GetPrivateField<DocumentSession>(form,
                    "documentSession").IsDirty;
                Require(!openedDocumentDirty && !editor.Modified,
                    "Un file workspace appena aperto viene marcato come modificato senza interventi dell'utente: " +
                    "sessione=" + openedDocumentDirty + ", RichEdit=" + editor.Modified + ".");
                editor.Modified = true;
                InvokeHandler(form, "richTextBox_TextChanged", editor, EventArgs.Empty);
                PumpMessages(80);
                openedDocumentDirty = GetPrivateField<DocumentSession>(form,
                    "documentSession").IsDirty;
                Require(!openedDocumentDirty && !editor.Modified,
                    "Una notifica TextChanged priva di variazioni sorgente marca ancora come non salvato " +
                    "un file appena aperto: sessione=" + openedDocumentDirty +
                    ", RichEdit=" + editor.Modified + ".");
                OpenDocumentBuffer activeBuffer = GetPrivateField<OpenDocumentCollection>(form,
                    "openDocuments").ActiveDocument;
                string editorSource = editor.Text;
                activeBuffer.UpdateSource(editorSource + "\n// baseline SDK passiva", false);
                InvokePrivateInstance(editorSurface, "activeEditor_TextChanged",
                    editor, EventArgs.Empty);
                Require(!activeBuffer.Session.IsDirty &&
                        activeBuffer.Source.EndsWith("// baseline SDK passiva", StringComparison.Ordinal),
                    "La superficie SDK collegata in sola osservazione riscrive ancora il documento standalone.");
                activeBuffer.UpdateSource(editorSource, false);
                Require(editorSurface.SourceText == editor.Text,
                    "La superficie SDK non segue il documento workspace attivo.");
                Require(!diagnostics.Items.Cast<object>().Any(item => item.ToString().Contains("DDF201")),
                    "Un simbolo definito in un altro file è ancora segnalato come non risolto.");

                Control flowchartCanvas = FindControl<Control>(form, "flowchartCanvas");
                PumpUntil(() => GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram")?.Flowchart.FunctionName == "main",
                    4000, "La Flowchart non ha analizzato il file principale del workspace.");
                InvokePrivate(form, "resetActiveAnalysis");
                Require(GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram") == null,
                    "Lo smoke non ha predisposto la perdita del diagramma sulla scheda attiva.");
                Require((bool)InvokePrivate(form, "activateOpenDocument", mainPath),
                    "Il file workspace gia aperto non viene riattivato.");
                PumpUntil(() => GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram")?.Flowchart.FunctionName == "main",
                    4000, "La Flowchart non viene rigenerata riaprendo un file multifile gia attivo.");
                InvokePrivate(form, "refreshFlowchartFunctionsMenu");
                ToolStrip flowchartToolbar = FindControl<ToolStrip>(form, "flowchartToolStrip");
                var flowchartFunctions = (ToolStripDropDownButton)flowchartToolbar.Items["flowchartFunctionsButton"];
                ToolStripItem helperFunction = flowchartFunctions.DropDownItems["flowchartFunction_helper"];
                Require(helperFunction != null && helperFunction.Enabled &&
                        helperFunction.Text.Contains("helpers.ddf", StringComparison.OrdinalIgnoreCase),
                    "Il menu Flowchart non risolve la funzione chiamata nel file esterno.");
                int sdkDocumentChanges = 0;
                editorSurface.ActiveEditorChanged += (sender, args) => sdkDocumentChanges++;
                helperFunction.PerformClick();
                PumpUntil(() => string.Equals(GetPrivateField<DocumentSession>(form, "documentSession").CurrentPath,
                        helperPath, StringComparison.OrdinalIgnoreCase) &&
                    GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram")?.Flowchart.FunctionName == "helper",
                    4000, "La Flowchart non ha seguito la funzione nel documento esterno.");
                RichTextBox helperEditor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(sdkDocumentChanges > 0 && editorSurface.SourceText == helperEditor.Text,
                    "Il contratto SDK non notifica o non espone il documento attivo dopo uno switch multi-file.");
                helperEditor.Select(helperEditor.Text.IndexOf("secondary", StringComparison.Ordinal), 0);
                PumpUntil(() => GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram")?.Flowchart.FunctionName == "secondary",
                    4000, "Il test non riesce a spostarsi con il mouse simulato sulla seconda funzione esterna.");
                InvokePrivate(form, "refreshFlowchartFunctionsMenu");
                ToolStripItem flowchartBack = flowchartFunctions.DropDownItems["flowchartNavigateBackMenuItem"];
                Require(flowchartBack != null && flowchartBack.Enabled,
                    "Il menu Flowchart non abilita il ritorno dal documento esterno.");
                flowchartBack.PerformClick();
                PumpUntil(() => string.Equals(GetPrivateField<DocumentSession>(form, "documentSession").CurrentPath,
                        mainPath, StringComparison.OrdinalIgnoreCase) &&
                    GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram")?.Flowchart.FunctionName == "main",
                    4000, "Torna indietro non ha ripristinato la Flowchart principale multi-file.");
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");

                string buildDirectory = Path.Combine(directory, ".ddf-build");
                FindMenuItem(form, "validateProjectMenuItem").PerformClick();
                RichTextBox output = FindControl<RichTextBox>(form, "richTextBoxOutput");
                Require(output.Text.Contains("Convalida completata", StringComparison.Ordinal) &&
                        output.Text.Contains("Nessun file scritto", StringComparison.Ordinal) && !Directory.Exists(buildDirectory),
                    "Convalida progetto non è rimasta in memoria o non ha comunicato l'esito.");
                Require(editorSurface.PreparationService.LastSnapshot?.Mode == DdfEditorPreparationMode.Validate &&
                        editorSurface.PreparationService.LastSnapshot.Succeeded,
                    "La convalida standalone non attraversa il servizio pubblico di preparazione dell'editor.");
                string diskSource = File.ReadAllText(mainPath);
                editor.AppendText("\n// modifica non salvata");
                PumpMessages(240);
                Require(GetPrivateField<DocumentSession>(form, "documentSession").IsDirty &&
                        editor.Modified,
                    "Una modifica reale non resta marcata dopo la colorazione sintattica.");
                FindMenuItem(form, "buildProjectMenuItem").PerformClick();
                string receiptPath = Path.Combine(buildDirectory, "project.ddfbuild.json");
                Require(File.Exists(receiptPath) && File.ReadAllText(receiptPath).Contains("\"fromUnsavedBuffer\": true", StringComparison.OrdinalIgnoreCase),
                    "Compila progetto non ha prodotto una ricevuta riproducibile usando il buffer non salvato.");
                Require(File.ReadAllText(mainPath) == diskSource,
                    "Compila progetto ha scritto implicitamente il buffer non salvato sul sorgente.");
                Require(output.Text.Contains("Fingerprint:", StringComparison.Ordinal) && output.Text.Contains(receiptPath, StringComparison.OrdinalIgnoreCase),
                    "L'Output di compilazione non espone fingerprint e percorso artefatto.");
                Require(editorSurface.PreparationService.LastSnapshot?.Mode == DdfEditorPreparationMode.PrepareScript &&
                        editorSurface.PreparationService.LastSnapshot.Succeeded &&
                        string.Equals(editorSurface.PreparationService.LastSnapshot.Result.ArtifactPath,
                            receiptPath, StringComparison.OrdinalIgnoreCase),
                    "La preparazione standalone non pubblica lo snapshot tramite il contratto SDK.");

                var compiledService = new FakeCompiledBuildService();
                form.AttachStandaloneCompiledBuildService(compiledService);
                FindMenuItem(form, "debugBuildConfigurationMenuItem")
                    .PerformClick();
                ToolStripMenuItem compileAndRun = FindMenuItem(form,
                    "compileAndRunMenuItem");
                ToolStripButton compileAndRunToolbar = FindToolbarButton(form,
                    "toolbarCompileAndRunButton");
                Require(compileAndRun.Enabled && compileAndRunToolbar.Enabled,
                    "Compila ed esegui non si abilita con backend collegato.");
                compileAndRun.PerformClick();
                PumpUntil(() => compiledService.BuildCount == 1 &&
                        compiledService.RunCount == 1 && compileAndRun.Enabled,
                    5000, "Compila ed esegui non ha completato build e worker.");
                Require(compiledService.BuildConfigurations.SequenceEqual(
                            new[] { DdfEditorBuildConfiguration.Debug }) &&
                        compiledService.LastSourceCount >= 2,
                    "La build UI Debug non ha ricevuto l'intero workspace multifile.");
                Require(File.ReadAllText(mainPath).Contains(
                        "// modifica non salvata", StringComparison.Ordinal),
                    "La build persistente non ha salvato il buffer modificato prima di compilare.");
                Require(output.Text.Contains("Compila ed esegui:",
                            StringComparison.Ordinal) &&
                        output.Text.Contains("compiled-output",
                            StringComparison.Ordinal) &&
                        output.Text.Contains("Valore restituito: 42",
                            StringComparison.Ordinal),
                    "Compila ed esegui non riporta nell'Output l'esito del worker.");

                ToolStripMenuItem openOutput = FindMenuItem(form,
                    "openCompiledOutputMenuItem");
                ToolStripButton openOutputToolbar = FindToolbarButton(form,
                    "toolbarOpenCompiledOutputButton");
                string openedOutput = null;
                SetPrivateField(form, "openCompiledOutputDirectory",
                    new Action<string>(path => openedOutput = path));
                Require(openOutput.Enabled && openOutputToolbar.Enabled,
                    "Apri cartella output non si abilita dopo una build valida.");
                openOutput.PerformClick();
                Require(string.Equals(openedOutput,
                        compiledService.LastArtifactDirectory,
                        StringComparison.OrdinalIgnoreCase),
                    "Apri cartella output non usa gli artefatti dell'ultima build valida.");

                FindMenuItem(form, "releaseBuildConfigurationMenuItem")
                    .PerformClick();
                ToolStripMenuItem compiledBuild = FindMenuItem(form,
                    "buildProjectMenuItem");
                compiledBuild.PerformClick();
                PumpUntil(() => compiledService.BuildCount == 2 &&
                        compiledBuild.Enabled,
                    5000, "La build Release dalla UI non è terminata.");
                Require(compiledService.BuildConfigurations.Last() ==
                        DdfEditorBuildConfiguration.Release &&
                        compiledService.LastArtifactDirectory.Contains(
                            Path.Combine("bin", "Release", "win-x64"),
                            StringComparison.OrdinalIgnoreCase),
                    "La configurazione Release non raggiunge il backend o usa un output errato.");

                compiledService.FailNextBuild = true;
                compiledBuild.PerformClick();
                PumpUntil(() => compiledService.BuildCount == 3 &&
                        compiledBuild.Enabled && openOutputToolbar.Enabled &&
                        output.Text.Contains("DDFC999", StringComparison.Ordinal),
                    5000, "La diagnostica della build non valida non è comparsa.");
                Require(output.Text.Contains("Build non completata",
                            StringComparison.Ordinal) &&
                        GetPrivateField<System.Collections.IList>(form,
                            "outputNavigationTargets").Count > 0,
                    "L'errore compilato non è visibile o navigabile dalla palette Output.");
                openedOutput = null;
                openOutputToolbar.PerformClick();
                Require(string.Equals(openedOutput,
                        compiledService.LastSuccessfulArtifactDirectory,
                        StringComparison.OrdinalIgnoreCase),
                    "Una build fallita ha sostituito l'ultima cartella output valida.");

                string publishedExecutable = Path.Combine(directory,
                    "SmokeProgram.exe");
                SetPrivateField(form, "showPublishExecutableDialog",
                    new Func<PublishExecutableDialog, DialogResult>(dialog =>
                    {
                        dialog.ExecutablePath = publishedExecutable;
                        dialog.IconPath = string.Empty;
                        return DialogResult.OK;
                    }));
                ToolStripMenuItem publish = FindMenuItem(form,
                    "publishExecutableMenuItem");
                ToolStripButton publishToolbar = FindToolbarButton(form,
                    "toolbarPublishExecutableButton");
                Require(publish.Enabled && publishToolbar.Enabled,
                    "Pubblica EXE non si abilita con backend collegato.");
                int buildsBeforePublish = compiledService.BuildCount;
                publish.PerformClick();
                DateTime publishDeadline = DateTime.UtcNow.AddSeconds(10);
                while (DateTime.UtcNow < publishDeadline &&
                       !(compiledService.BuildCount == buildsBeforePublish + 1 &&
                         compiledService.PublishCount == 1 &&
                         publish.Enabled))
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }
                Require(compiledService.BuildCount == buildsBeforePublish + 1 &&
                        compiledService.PublishCount == 1 && publish.Enabled,
                    "Pubblica EXE non ha completato build e package. Build=" +
                    compiledService.BuildCount + ", publish=" +
                    compiledService.PublishCount + ", enabled=" +
                    publish.Enabled + ", output=" + output.Text);
                Require(output.Text.Contains("Pubblicazione EXE completata",
                            StringComparison.Ordinal) &&
                        output.Text.Contains("SmokeProgram.exe",
                            StringComparison.Ordinal),
                    "Pubblica EXE non riporta package ed eseguibile nell'Output.");
                Require(openOutput.Enabled && openOutputToolbar.Enabled,
                    "Apri cartella output non si abilita dopo la pubblicazione.");
                openedOutput = null;
                openOutputToolbar.PerformClick();
                Require(string.Equals(openedOutput,
                        compiledService.LastPublishedDirectory,
                        StringComparison.OrdinalIgnoreCase),
                    "Apri cartella output non usa l'ultimo package valido.");

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

                editor.Select(editor.GetFirstCharIndexFromLine(3), 0);
                FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") &&
                    editor.SelectedText.Contains("int value"), 4000,
                    "Il runtime workspace non raggiunge il breakpoint nella funzione dell'altro file.");
                ListView multiFileCallStack = FindControl<ListView>(form, "callStackListView");
                Require(multiFileCallStack.Items.Count == 2 &&
                        multiFileCallStack.Items[0].SubItems[1].Text == "helper()" &&
                        multiFileCallStack.Items[1].SubItems[1].Text == "main()",
                    "Il debug multi-file non mostra funzione esterna e chiamante nel Call Stack.");
                FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
                FindToolbarButton(form, "toolbarStepOutButton").PerformClick();
                PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") &&
                    FindControl<RichTextBox>(form, "richTextBoxMainEditor").SelectedText.Contains("ret value"), 4000,
                    "Step Out non torna al chiamante nel file principale del workspace.");
                Require(string.Equals(GetPrivateField<DocumentSession>(form, "documentSession").CurrentPath,
                        mainPath, StringComparison.OrdinalIgnoreCase),
                    "La pausa nel chiamante non ha attivato il documento principale.");
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(() => !FindToolbarButton(form, "toolbarStopButton").Enabled, 4000,
                    "Il runtime multi-file non termina dopo Continua.");
                Require(output.Text.Contains("Valore restituito: 1", StringComparison.Ordinal),
                    "La chiamata runtime tra file non restituisce il valore previsto. Output: " + output.Text);
                InvokePrivate(form, "clearBreakpoints");

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
                Require(editor.Text.Contains("int value << helper()"), "Vai a file non ha aperto il documento scelto.");

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
                Require(editor.SelectedText == "helper" && editor.Text.Contains("int value << helper()"),
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

                string mainSourceWithUnsavedEdit = editor.Text;
                SetPrivateField(form, "showNavigationDialog", new Func<NavigationForm, DialogResult>(dialog =>
                {
                    ListView results = FindControl<ListView>(dialog, "navigationResultsListView");
                    FindControl<TextBox>(dialog, "navigationQueryTextBox").Text = "helper";
                    Require(results.Items.Count >= 1, "La cronologia non può preparare il salto cross-file.");
                    return DialogResult.OK;
                }));
                FindMenuItem(form, "goToSymbolMenuItem").PerformClick();
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(editor.SelectedText == "helper" && editor.Text.Contains("library declaration"),
                    "Il salto usato dalla cronologia non ha raggiunto helpers.ddf.");
                Require(FindMenuItem(form, "navigateBackMenuItem").Enabled,
                    "Navigazione indietro non viene abilitata dopo un salto.");

                FindMenuItem(form, "navigateBackMenuItem").PerformClick();
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(editor.Text == mainSourceWithUnsavedEdit && editor.SelectionStart == lastEditPosition && editor.CanUndo,
                    "Indietro non ripristina file, posizione e buffer non salvato.");
                Require(FindToolbarButton(form, "toolbarNavigateForwardButton").Enabled,
                    "Navigazione avanti non viene abilitata dopo Indietro.");

                FindToolbarButton(form, "toolbarNavigateForwardButton").PerformClick();
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(editor.SelectedText == "helper" && editor.Text.Contains("library declaration"),
                    "Avanti dalla toolbar non ripristina la destinazione cross-file.");
                FindToolbarButton(form, "toolbarNavigateBackButton").PerformClick();
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                Require(editor.Text == mainSourceWithUnsavedEdit && editor.SelectionStart == lastEditPosition,
                    "Un secondo Indietro non torna al buffer sorgente.");

                SetPrivateField(form, "showNavigationDialog", new Func<NavigationForm, DialogResult>(dialog =>
                {
                    FindControl<TextBox>(dialog, "navigationQueryTextBox").Text = "1:1";
                    return DialogResult.OK;
                }));
                FindMenuItem(form, "goToLineMenuItem").PerformClick();
                Require(editor.SelectionStart == 0 && !FindMenuItem(form, "navigateForwardMenuItem").Enabled,
                    "Un nuovo salto dopo Indietro non elimina correttamente la cronologia Avanti.");

                FindMenuItem(form, "closeWorkspaceMenuItem").PerformClick();
                Require(workspaceTree.Nodes.Count == 0 && !FindMenuItem(form, "closeWorkspaceMenuItem").Enabled,
                    "Chiudi cartella non ha svuotato lo stato workspace.");
                recentWorkspace = FindMenuItem(form, "recentWorkspacesMenuItem").DropDownItems
                    .OfType<ToolStripMenuItem>()
                    .First(item => string.Equals(item.Tag as string, directory, StringComparison.OrdinalIgnoreCase));
                recentWorkspace.PerformClick();
                PumpUntil(
                    () => workspaceTree.Nodes.Count == 1 && FindMenuItem(form, "closeWorkspaceMenuItem").Enabled,
                    5000,
                    "Cartelle recenti non riapre il workspace selezionato.");
                FindMenuItem(form, "closeWorkspaceMenuItem").PerformClick();

                string ignoredPath = Path.Combine(directory, "notes.txt");
                File.WriteAllText(ignoredPath, "not a DDF source");
                var invalidDropData = new DataObject();
                invalidDropData.SetData(DataFormats.FileDrop, new[] { ignoredPath });
                var invalidDrag = new DragEventArgs(invalidDropData, 0, 0, 0, DragDropEffects.Copy, DragDropEffects.None);
                InvokeHandler(form, "workspaceDropTarget_DragEnter", form, invalidDrag);
                Require(invalidDrag.Effect == DragDropEffects.None, "Il drag-and-drop accetta file non DDF.");

                string droppedPath = Path.Combine(directory, "dropped.ddf");
                string secondDroppedPath = Path.Combine(directory, "second-dropped.ddf");
                DdfDocumentFile.Save(droppedPath, "dropped() out int { ret 7; }");
                DdfDocumentFile.Save(secondDroppedPath, "secondDropped() out int { ret 8; }");
                var fileDropData = new DataObject();
                fileDropData.SetData(DataFormats.FileDrop, new[] { droppedPath, secondDroppedPath });
                var fileDrag = new DragEventArgs(fileDropData, 0, 0, 0, DragDropEffects.Copy, DragDropEffects.None);
                InvokeHandler(form, "workspaceDropTarget_DragEnter", form, fileDrag);
                Require(fileDrag.Effect == DragDropEffects.Copy, "Il drag-and-drop non accetta un sorgente DDF.");
                InvokeHandler(form, "workspaceDropTarget_DragDrop", form, fileDrag);
                editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                OpenDocumentCollection droppedDocuments = GetPrivateField<OpenDocumentCollection>(form, "openDocuments");
                Require(editor.Text.Contains("secondDropped()") && form.Text.Contains("second-dropped.ddf") &&
                        droppedDocuments.FindByPath(droppedPath) != null && droppedDocuments.FindByPath(secondDroppedPath) != null,
                    "Il drop di più file DDF non li apre in schede indipendenti.");

                var folderDropData = new DataObject();
                folderDropData.SetData(DataFormats.FileDrop, new[] { directory });
                var folderDrag = new DragEventArgs(folderDropData, 0, 0, 0, DragDropEffects.Copy, DragDropEffects.None);
                InvokeHandler(form, "workspaceDropTarget_DragEnter", form, folderDrag);
                Require(folderDrag.Effect == DragDropEffects.Copy, "Il drag-and-drop non accetta una cartella workspace.");
                InvokeHandler(form, "workspaceDropTarget_DragDrop", form, folderDrag);
                PumpUntil(
                    () => workspaceTree.Nodes.Count == 1 && FindControl<Label>(form, "labelWorkspace").Text.Contains("4 file"),
                    5000,
                    "Il drop della cartella non apre e aggiorna il workspace.");
                FindMenuItem(form, "closeWorkspaceMenuItem").PerformClick();
                InvokePrivate(form, "createUntitledDocument");
                Console.WriteLine("PASS workspace, navigazione, recenti e drag-and-drop di file/cartelle");
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
            PumpMessages(220);
            DdfDiagnostic missingTerminator = diagnostics.Items.Cast<DdfDiagnostic>().Single(item =>
                item.Code == "DDF102" && item.Message.Contains("';'", StringComparison.Ordinal));
            Require(missingTerminator.Line == 3,
                "Il terminatore mancante non è ancorato alla riga dell'istruzione incompleta.");
            Require(!diagnostics.Items.Cast<DdfDiagnostic>().Any(item => item.Code == "DDFG101"),
                "Il terminatore mancante mostra ancora la diagnostica grammaticale generica DDFG101.");
            editor.Select(missingTerminator.Start, 0);
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
            Require(ReferenceEquals(diagnostic, hovered) && hovered.ToHoverText().Contains(diagnostic.DisplayMessage),
                "L'hover diagnostico non espone il messaggio completo della palette.");

            editor.Undo();
            PumpMessages(240);
            Require(editor.Text == valid, "Undo non ha ripristinato il testo dopo la diagnostica inline.");
            Require(diagnostics.Items.Count == 0, "La decorazione rimossa ha lasciato diagnostiche nella palette.");
            byte remainingUnderline = (byte)InvokePrivate(form,
                "getDiagnosticUnderlineTypeAt", literal);
            Require(remainingUnderline == 0,
                "La sottolineatura diagnostica non è stata rimossa incrementalmente: tipo=" +
                remainingUnderline + ", formatStart=" +
                GetPrivateValue<int>(form, "lastAnalysisFormatStart") + ", RTF=" +
                GetPrivateValue<bool>(form, "lastAnalysisUsedRtf") +
                ", CanUndo=" + editor.CanUndo + ".");

            editor.Select(literal, 1);
            editor.SelectedText = "missing";
            PumpMessages(240);
            Require((byte)InvokePrivate(form, "getDiagnosticUnderlineTypeAt",
                        literal) == 8,
                "La prova RTF non ha predisposto la sottolineatura da rimuovere.");
            editor.Text = valid;
            editor.ClearUndo();
            InvokePrivate(form, "resetActiveAnalysis");
            InvokePrivate(form, "applyHighlighting");
            Require(GetPrivateValue<bool>(form, "lastAnalysisUsedRtf") &&
                    (byte)InvokePrivate(form, "getDiagnosticUnderlineTypeAt",
                        literal) == 0,
                "La proiezione RTF completa non ha sostituito la sottolineatura diagnostica obsoleta.");
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

        private static IEnumerable<TreeNode> FlattenTreeNodes(TreeNode root)
        {
            yield return root;
            foreach (TreeNode child in root.Nodes)
                foreach (TreeNode descendant in FlattenTreeNodes(child))
                    yield return descendant;
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

        private static void AssertCompiledLanguageFeaturesWorkspaceDebug(bool visible)
        {
            string workspaceRoot = Path.GetFullPath(Path.Combine(
                Environment.CurrentDirectory, "samples",
                "language-features-debug-workspace"));
            string mainPath = Path.Combine(workspaceRoot, "main.ddf");
            string classificationPath = Path.Combine(workspaceRoot,
                "classification.ddf");
            Require(File.Exists(mainPath) && File.Exists(classificationPath),
                "Il workspace language-features-debug non è disponibile da " +
                Environment.CurrentDirectory + ".");

            IDataObject originalClipboard = null;
            try
            {
                originalClipboard = Clipboard.GetDataObject();
            }
            catch (ExternalException)
            {
            }

            using (var form = ShowSmokeForm(visible))
            {
                form.AttachStandaloneCompiledBuildService(
                    new DdfDotNetEditorBuildService());
                form.AttachStandaloneDebugService(
                    new DdfDotNetEditorDebugService(
                        () => form.EmbeddedEditor.DocumentService,
                        () => form.EmbeddedEditor.LanguageSession,
                        form.PrepareStandaloneBackendExecution,
                        eventDispatcher: form.EmbeddedEditor.EventDispatcher));
                DdfLanguagePackageLoadResult language =
                    form.EmbeddedEditor.LanguageSession.UseEmbedded();
                Require(language.Success,
                    "Il DDF incorporato non può essere attivato per lo smoke compilato.");
                Require((bool)InvokePrivate(form, "openWorkspace", workspaceRoot),
                    "Il workspace language-features-debug non viene aperto dalla UI.");
                PumpUntil(() =>
                        GetPrivateField<DdfWorkspaceIndex>(form, "workspaceIndex")
                            ?.Documents.Count == 8,
                    5000, "Il workspace completo non viene indicizzato dalla UI.");
                Require((bool)InvokePrivate(form, "openDocument", mainPath),
                    "main.ddf non viene aperto dalla UI.");
                PumpMessages(900);

                IReadOnlyList<DdfDiagnostic> diagnostics =
                    GetPrivateField<IReadOnlyList<DdfDiagnostic>>(form,
                        "activeDiagnostics");
                DdfDiagnostic falsePositive = diagnostics.FirstOrDefault(item =>
                    item.Code == "DDF201" && item.Line == 27);
                Require(falsePositive == null,
                    "DefaultPriority produce ancora DDF201 alla riga 27: " +
                    falsePositive?.Message);
                Require(!diagnostics.Any(item =>
                        item.Severity == DdfDiagnosticSeverity.Error),
                    "Il workspace di riferimento non è semanticamente valido: " +
                    string.Join(" | ", diagnostics.Select(item => item.ToString())));

                RichTextBox editor = FindControl<RichTextBox>(form,
                    "richTextBoxMainEditor");
                RichTextBox output = FindControl<RichTextBox>(form,
                    "richTextBoxOutput");
                ListBox diagnosticRows = FindControl<ListBox>(form,
                    "listBoxDiagnostics");
                TreeView variables = FindControl<TreeView>(form,
                    "treeViewVariables");
                ListView callStack = FindControl<ListView>(form,
                    "callStackListView");
                TextBox watchInput = FindControl<TextBox>(form,
                    "watchExpressionTextBox");
                ListView watchRows = FindControl<ListView>(form,
                    "watchListView");
                watchRows.Items.Clear();
                SetPrivateField(form, "watchExpressionSettings",
                    new WatchExpressionSettings());
                SetPrivateField(form, "saveWatchExpressionsSetting",
                    new Action<string>(value => { }));
                InvokePrivate(form, "switchWatchProject", workspaceRoot);
                InvokePrivate(form, "clearBreakpoints");

                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(() =>
                        form.EmbeddedEditor.DebugService.State ==
                            DdfEditorDebugState.Idle &&
                        output.Text.Contains("Valore restituito: 419",
                            StringComparison.Ordinal),
                    30000,
                    "Il debugger compilato senza breakpoint non completa il workspace. Output: " +
                    output.Text);
                Require(!output.Text.Contains("Piano debug worker non valido",
                        StringComparison.OrdinalIgnoreCase) &&
                    !output.Text.Contains("Errore interno",
                        StringComparison.OrdinalIgnoreCase),
                    "Il percorso senza breakpoint espone ancora un errore interno. Output: " +
                    output.Text);

                int resultStart = output.Text.IndexOf("Valore restituito: 419",
                    StringComparison.Ordinal);
                output.Select(resultStart, 0);
                SetClipboardTextWithRetry("sentinella-output");
                var outputCopy = new KeyEventArgs(Keys.Control | Keys.C);
                InvokeHandler(form, "richTextBoxOutput_KeyDown", output,
                    outputCopy);
                Require(outputCopy.Handled && outputCopy.SuppressKeyPress &&
                        ClipboardTextEqualsWithRetry("Valore restituito: 419"),
                    "Ctrl+C non copia la riga corrente dalla palette Output.");

                diagnosticRows.Items.Clear();
                diagnosticRows.Items.Add("Riga 27 — DDF201 di prova");
                diagnosticRows.Items.Add("Riga 28 — DDF202 di prova");
                diagnosticRows.SetSelected(0, true);
                diagnosticRows.SetSelected(1, true);
                SetClipboardTextWithRetry("sentinella-diagnostica");
                var diagnosticsCopy = new KeyEventArgs(Keys.Control | Keys.C);
                InvokeHandler(form, "listBoxDiagnostics_KeyDown",
                    diagnosticRows, diagnosticsCopy);
                Require(diagnosticsCopy.Handled &&
                        diagnosticsCopy.SuppressKeyPress &&
                        ClipboardTextEqualsWithRetry(
                            "Riga 27 — DDF201 di prova" + Environment.NewLine +
                            "Riga 28 — DDF202 di prova"),
                    "Ctrl+C non copia più righe selezionate dalla palette Diagnostica.");
                InvokePrivate(form, "updateDiagnostics", diagnostics);

                editor = FindControl<RichTextBox>(form,
                    "richTextBoxMainEditor");
                editor.Select(editor.GetFirstCharIndexFromLine(26), 0);
                FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(() =>
                        form.EmbeddedEditor.DebugService.State ==
                            DdfEditorDebugState.Paused &&
                        string.Equals(
                            GetPrivateField<OpenDocumentCollection>(form,
                                "openDocuments").ActiveDocument?.Session.CurrentPath,
                            mainPath, StringComparison.OrdinalIgnoreCase) &&
                        FindControl<RichTextBox>(form,
                            "richTextBoxMainEditor").SelectedText.Contains(
                                "priorityWeight", StringComparison.Ordinal),
                    30000,
                    "Il debugger compilato non si ferma sulla chiamata alla riga 27. Output: " +
                    output.Text);
                Require(FindControl<RichTextBox>(form,
                        "richTextBoxMainEditor").SelectedText.Contains(
                            "priorityWeight", StringComparison.Ordinal),
                    "La pausa compilata non evidenzia la chiamata priorityWeight.");
                Require(variables.Nodes.Find("completedRuns", true)
                            .FirstOrDefault()?.Text.Contains(
                                "int = 1", StringComparison.Ordinal) == true &&
                        variables.Nodes.Find("localOffset", true)
                            .FirstOrDefault()?.Text.Contains(
                                "fixed · sola lettura", StringComparison.Ordinal) == true &&
                        variables.Nodes.Find("DefaultPriority", true)
                            .FirstOrDefault()?.Text.Contains(
                                "Priority.High", StringComparison.Ordinal) == true &&
                        variables.Nodes["states"] != null,
                    "Variabili non distingue state, fixed o enum alla pausa compilata.");
                Require(callStack.Items.Count == 1 &&
                        callStack.Items[0].SubItems[1].Text == "main()" &&
                        callStack.Items[0].SubItems[3].Text == "main.ddf",
                    "Call Stack non mostra il frame DDF principale compilato.");
                FindControl<TabControl>(form, "tabControlBottom").SelectedTab =
                    FindControl<TabPage>(form, "tabPageWatch");
                foreach (string expression in new[]
                         {
                             "completedRuns", "localOffset",
                             "DefaultPriority", "priority"
                         })
                {
                    watchInput.Text = expression;
                    FindControl<Button>(form, "watchAddButton").PerformClick();
                }
                ListViewItem stateWatch = watchRows.Items.Cast<ListViewItem>()
                    .Single(item => item.Text == "completedRuns");
                ListViewItem fixedWatch = watchRows.Items.Cast<ListViewItem>()
                    .Single(item => item.Text == "localOffset");
                ListViewItem globalEnumWatch = watchRows.Items.Cast<ListViewItem>()
                    .Single(item => item.Text == "DefaultPriority");
                Require(stateWatch.SubItems[1].Text == "int · state" &&
                        stateWatch.SubItems[2].Text == "1" &&
                        fixedWatch.SubItems[1].Text == "int · fixed" &&
                        fixedWatch.SubItems[2].Text == "2" &&
                        globalEnumWatch.SubItems[1].Text == "Priority · fixed" &&
                        globalEnumWatch.SubItems[2].Text == "Priority.High",
                    "Watch non rende leggibili state, fixed ed enum globali compilati.");

                FindToolbarButton(form, "toolbarStepIntoButton").PerformClick();
                PumpUntil(() =>
                        form.EmbeddedEditor.DebugService.State ==
                            DdfEditorDebugState.Paused &&
                        string.Equals(
                            GetPrivateField<OpenDocumentCollection>(form,
                                "openDocuments").ActiveDocument?.Session.CurrentPath,
                            classificationPath,
                            StringComparison.OrdinalIgnoreCase) &&
                        FindControl<RichTextBox>(form,
                            "richTextBoxMainEditor").SelectedText.Contains(
                                "select", StringComparison.Ordinal),
                    15000,
                    "Step Into compilato non attiva classification.ddf.");
                Require(FindControl<RichTextBox>(form,
                        "richTextBoxMainEditor").SelectedText.Contains(
                            "select", StringComparison.Ordinal),
                    "Step Into compilato non evidenzia la prima istruzione della funzione esterna.");
                TreeNode priorityVariable = variables.Nodes.Find("priority", true)
                    .FirstOrDefault();
                ListViewItem priorityWatch = watchRows.Items.Cast<ListViewItem>()
                    .Single(item => item.Text == "priority");
                Require(priorityVariable != null &&
                        priorityVariable.Text.Contains(
                            "Priority = Priority.High", StringComparison.Ordinal) &&
                        priorityWatch.SubItems[1].Text == "Priority" &&
                        priorityWatch.SubItems[2].Text == "Priority.High",
                    "Watch o Variabili non mostrano l'enum nominale nel frame esterno.");
                Require(callStack.Items.Count == 2 &&
                        callStack.Items[0].SubItems[1].Text == "priorityWeight()" &&
                        callStack.Items[0].SubItems[3].Text ==
                            "classification.ddf" &&
                        callStack.Items[1].SubItems[1].Text == "main()" &&
                        callStack.Items[1].SubItems[3].Text == "main.ddf",
                    "Call Stack compilato non mostra funzione esterna e chiamante DDF.");
                TabControl bottomTabs = FindControl<TabControl>(form,
                    "tabControlBottom");
                bottomTabs.SelectedTab = FindControl<TabPage>(form,
                    "tabPageCallStack");
                callStack.Focus();
                callStack.Items[0].Selected = false;
                callStack.Items[1].Selected = true;
                callStack.Items[1].Focused = true;
                PumpMessages(100);
                Require(variables.Nodes.Find("localOffset", true)
                            .FirstOrDefault()?.Text.Contains(
                                "int = 2", StringComparison.Ordinal) == true &&
                        variables.Nodes.Find("priority", true).Length == 0 &&
                        fixedWatch.SubItems[2].Text == "2" &&
                        priorityWatch.SubItems[2].Text == "—",
                    "La selezione del frame chiamante non aggiorna insieme Variabili e Watch. " +
                    "Frame=" + string.Join(",", callStack.SelectedItems
                        .Cast<ListViewItem>().Select(item => item.Tag)) +
                    "; variabili=" + string.Join(" | ", variables.Nodes
                        .Cast<TreeNode>().SelectMany(FlattenTreeNodes)
                        .Select(node => node.Text)) +
                    "; watch=" + string.Join(" | ", watchRows.Items
                        .Cast<ListViewItem>().Select(item => item.Text + "=" +
                            item.SubItems[2].Text)));
                string projectedDebuggerText = string.Join("\n",
                    variables.Nodes.Cast<TreeNode>()
                        .SelectMany(FlattenTreeNodes)
                        .Select(node => node.Text)
                        .Concat(callStack.Items.Cast<ListViewItem>()
                            .SelectMany(item => item.SubItems.Cast<
                                ListViewItem.ListViewSubItem>())
                            .Select(item => item.Text))
                        .Concat(watchRows.Items.Cast<ListViewItem>()
                            .SelectMany(item => item.SubItems.Cast<
                                ListViewItem.ListViewSubItem>())
                            .Select(item => item.Text)));
                Require(!projectedDebuggerText.Contains("DdfGenerated_",
                            StringComparison.Ordinal) &&
                        !projectedDebuggerText.Contains("Microsoft.CodeAnalysis",
                            StringComparison.Ordinal),
                    "Le palette debugger fanno trapelare nomi CLR o dettagli Roslyn.");
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(() =>
                        form.EmbeddedEditor.DebugService.State ==
                            DdfEditorDebugState.Idle &&
                        output.Text.Contains("Valore restituito: 419",
                            StringComparison.Ordinal),
                    30000,
                    "Il debug compilato multifile non termina dopo Continua. Output: " +
                    output.Text);
                InvokePrivate(form, "clearBreakpoints");
                PumpMessages(200);
            }

            try
            {
                if (originalClipboard == null) Clipboard.Clear();
                else Clipboard.SetDataObject(originalClipboard, true);
            }
            catch (ExternalException)
            {
                Console.Error.WriteLine(
                    "WARN impossibile ripristinare gli appunti dopo lo smoke compilato.");
            }

            Console.WriteLine(
                "PASS workspace UI compilato: diagnostica, clipboard, Watch/Variabili/Call Stack DDF e Step Into multifile");
        }

        private static void AssertWorkspaceRenameFromExternalReference(bool visible)
        {
            string workspaceRoot = Path.Combine(Path.GetTempPath(),
                "ddf-workspace-rename-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspaceRoot);
            string mainPath = Path.Combine(workspaceRoot, "main.ddf");
            string statePath = Path.Combine(workspaceRoot, "state.ddf");
            File.WriteAllText(mainPath,
                "main() out int { completedRuns++; ret completedRuns; }");
            File.WriteAllText(statePath, "state int completedRuns << 0;");
            try
            {
                using (var form = ShowSmokeForm(visible))
                {
                    Require((bool)InvokePrivate(form, "openWorkspace", workspaceRoot),
                        "Il workspace per la rinomina non viene aperto.");
                    PumpUntil(() => GetPrivateField<DdfWorkspaceIndex>(form, "workspaceIndex")
                            ?.Documents.Count == 2,
                        4000, "Il workspace per la rinomina non viene indicizzato.");
                    Require((bool)InvokePrivate(form, "openDocument", mainPath),
                        "main.ddf non viene aperto per la rinomina.");
                    PumpMessages(300);

                    RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                    int reference = editor.Text.IndexOf("completedRuns", StringComparison.Ordinal);
                    editor.Select(reference, 0);
                    InvokeHandler(form, "editMenuItem_DropDownOpening",
                        FindMenuItem(form, "editMenuItem"), EventArgs.Empty);
                    ToolStripMenuItem rename = FindMenuItem(form, "renameSymbolMenuItem");
                    Require(rename.Enabled,
                        "Rinomina simbolo resta disabilitato su un riferimento globale esterno.");
                    SetPrivateField(form, "requestSymbolRename",
                        new Func<string, string>(name => "runCount"));
                    rename.PerformClick();
                    PumpMessages(500);

                    IReadOnlyList<IDdfEditorDocument> buffers =
                        form.EmbeddedEditor.DocumentService.OpenDocuments;
                    Require(buffers.Single(document => string.Equals(document.FilePath, mainPath,
                            StringComparison.OrdinalIgnoreCase)).SourceText ==
                            "main() out int { runCount++; ret runCount; }",
                        "La rinomina workspace non aggiorna i riferimenti nel file chiamante.");
                    Require(buffers.Single(document => string.Equals(document.FilePath, statePath,
                            StringComparison.OrdinalIgnoreCase)).SourceText ==
                            "state int runCount << 0;",
                        "La rinomina workspace non aggiorna la dichiarazione esterna.");
                    Require(editor.SelectedText == "runCount",
                        "La rinomina workspace non ripristina selezione e documento di origine.");
                }
                Console.WriteLine("PASS rinomina simbolo globale fra file del workspace");
            }
            finally
            {
                Directory.Delete(workspaceRoot, true);
            }
        }

        private static void RequireRuntimeEditorFlowchartSynchronization(MainForm form,
            string expectedFunction, string expectedSourceFragment, string scenario,
            string expectedPath = null)
        {
            IDdfEditorDebugService debug = form.EmbeddedEditor.DebugService;
            DdfEditorDebugSnapshot snapshot = debug?.CurrentSnapshot;
            Require(snapshot != null && (debug.State == DdfEditorDebugState.Paused ||
                    debug.State == DdfEditorDebugState.AwaitingCapability),
                scenario + ": manca uno snapshot runtime sospeso.");
            DdfEditorDebugLocation location = snapshot.Location;
            RichTextBox editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
            OpenDocumentBuffer active = GetPrivateField<OpenDocumentCollection>(form,
                "openDocuments").ActiveDocument;
            Control canvas = FindControl<Control>(form, "flowchartCanvas");
            DdfFlowchartLayout layout = GetPropertyValue<DdfFlowchartLayout>(canvas, "Diagram");
            DdfFlowchartNode executionNode = GetPropertyValue<DdfFlowchartNode>(canvas,
                "ExecutionNode");
            int debugLine = (int)editor.GetType().GetProperty("DebugExecutionLine").GetValue(editor);
            string currentPath = active?.Session.HasPath == true
                ? Path.GetFullPath(active.Session.CurrentPath)
                : string.Empty;
            string normalizedExpectedPath = string.IsNullOrWhiteSpace(expectedPath)
                ? string.Empty
                : Path.GetFullPath(expectedPath);
            bool documentMatches = string.Equals(active?.Id, location.DocumentId,
                    StringComparison.Ordinal) ||
                (normalizedExpectedPath.Length > 0 && string.Equals(currentPath,
                    normalizedExpectedPath, StringComparison.OrdinalIgnoreCase));
            bool nodeContainsLocation = executionNode != null &&
                location.Start >= executionNode.Start &&
                location.Start < executionNode.Start + Math.Max(1, executionNode.Length);
            Require(documentMatches &&
                    (normalizedExpectedPath.Length == 0 || string.Equals(currentPath,
                        normalizedExpectedPath, StringComparison.OrdinalIgnoreCase)) &&
                    layout != null && string.Equals(layout.Flowchart.FunctionName,
                        expectedFunction, StringComparison.Ordinal) &&
                    (normalizedExpectedPath.Length == 0 || string.Equals(
                        Path.GetFullPath(layout.Flowchart.DocumentPath), normalizedExpectedPath,
                        StringComparison.OrdinalIgnoreCase)) &&
                    nodeContainsLocation &&
                    editor.SelectedText.Contains(expectedSourceFragment, StringComparison.Ordinal) &&
                    debugLine == location.Line - 1,
                scenario + ": documento, selezione, riga runtime e nodo Flowchart non sono sincronizzati. " +
                "Stato=" + debug.State + "; documento='" + currentPath + "'; funzione='" +
                layout?.Flowchart.FunctionName + "'; selezione='" + editor.SelectedText +
                "'; rigaEditor=" + debugLine + "; rigaSnapshot=" + location.Line +
                "; nodo='" + executionNode?.Label + "'.");
        }

        private static void AssertProgramExecution(MainForm form, RichTextBox editor)
        {
            editor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
            Control flowchartCanvas = FindControl<Control>(form, "flowchartCanvas");
            Label flowchartStatus = FindControl<Label>(form, "flowchartStatusLabel");
            ToolStrip flowchartToolbar = FindControl<ToolStrip>(form, "flowchartToolStrip");
            var flowchartGroups = (ToolStripButton)flowchartToolbar.Items["flowchartGroupsButton"];
            ToolStripMenuItem educationalMode = FindMenuItem(form, "educationalModeMenuItem");
            if (!educationalMode.Checked) educationalMode.PerformClick();
            if (flowchartGroups.Checked) flowchartGroups.PerformClick();
            InvokePrivate(form, "clearBreakpoints");
            ToolStripStatusLabel debugStatus = (ToolStripStatusLabel)FindControl<StatusStrip>(form, "statusStripMain")
                .Items["statusDebugLabel"];
            Func<bool> debuggerIsIdle = () => debugStatus.Text == "MODIFICA" &&
                FindToolbarButton(form, "toolbarRunButton").Enabled &&
                !FindToolbarButton(form, "toolbarStopButton").Enabled;
            editor.Text = "main() out int\n{\n    int value << 2;\n    value + 3 >> Console;\n    ret value;\n}";
            PumpMessages(240);
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(debuggerIsIdle, 4000, "La prima esecuzione non torna allo stato Modifica.");
            RichTextBox output = FindControl<RichTextBox>(form, "richTextBoxOutput");
            TabControl tabs = FindControl<TabControl>(form, "tabControlBottom");
            Require(output.Text.Contains("5"), "L'output runtime non contiene il valore atteso.");
            Require(output.Text.Contains("Valore restituito: 2"), "La palette non mostra separatamente il valore restituito da main.");
            Require(output.Text.Contains("passi runtime") && !output.Text.Contains(" istruzioni)"),
                "Il contatore tecnico è ancora presentato come valore restituito o istruzioni DDF.");
            Require(output.Text.Contains("Esecuzione completata"), "Il runtime non segnala il completamento.");
            Require(tabs.SelectedTab.Name == "tabPageOutput", "La palette Output non viene selezionata durante Run.");
            Require(FindMenuItem(form, "runProgramMenuItem").Enabled, "Run non viene riabilitato al termine.");

            editor.Text = "main() out int\n{\n    int value << 2;\n    int[2] values;\n    values[0] << value;\n    value << value + 1;\n    ret value;\n}";
            PumpMessages(180);
            RichTextBox gutter = FindControl<RichTextBox>(form, "richTextBoxLineNumbers");
            editor.Select(editor.GetFirstCharIndexFromLine(5), 0);
            FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
            Require(gutter.Text.Contains("● 6"), "Il gutter non mostra il breakpoint attivo sulla riga 6.");
            Require((int)editor.GetType().GetProperty("BreakpointLineCount").GetValue(editor) == 1,
                "La riga con breakpoint non espone l'evidenziazione persistente nell'editor.");
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            Func<bool> breakpointReached = () => output.Text.Contains("Breakpoint raggiunto: riga 6") &&
                    FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") &&
                    (int)editor.GetType().GetProperty("DebugExecutionLine").GetValue(editor) == 5;
            var breakpointWait = Stopwatch.StartNew();
            while (!breakpointReached() && breakpointWait.ElapsedMilliseconds < 10000)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
            IDdfEditorDebugService breakpointSession = form.EmbeddedEditor.DebugService;
            Require(breakpointReached(),
                "Il runtime non si sospende sul breakpoint. Stato: running=" +
                GetPrivateValue<bool>(form, "executionRunning") +
                ", sessione=" + (breakpointSession == null ? "null" :
                    breakpointSession.IsPaused ? "pausa" : "attiva") +
                ", menu='" + FindMenuItem(form, "runProgramMenuItem").Text +
                "', linea=" + editor.GetType().GetProperty("DebugExecutionLine").GetValue(editor) +
                ", output='" + output.Text.Replace(Environment.NewLine, " | ") + "'.");
            Require(FindMenuItem(form, "runProgramMenuItem").Enabled &&
                    FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua"),
                "F5 non passa allo stato Continua durante la pausa.");
            Require(editor.SelectedText.Contains("value << value + 1"),
                "La pausa non evidenzia lo statement associato al breakpoint.");
            Require((int)editor.GetType().GetProperty("DebugExecutionLine").GetValue(editor) == 5,
                "La riga sospesa non espone l'evidenziazione gialla del debugger.");
            DdfFlowchartNode executionNode = GetPropertyValue<DdfFlowchartNode>(flowchartCanvas, "ExecutionNode");
            Require(executionNode != null && executionNode.Label.Contains("value << value + 1") &&
                    GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram").Flowchart.FunctionName == "main" &&
                    flowchartStatus.Text.Contains("DEBUG IN PAUSA", StringComparison.Ordinal),
                "La Flowchart non evidenzia il nodo appartenente allo snapshot realmente in pausa.");
            RequireRuntimeEditorFlowchartSynchronization(form, "main",
                "value << value + 1", "pausa sul breakpoint");
            Require(ReferenceEquals(FindControl<TabControl>(form, "navigationTabs").SelectedTab,
                    FindControl<TabPage>(form, "flowchartTabPage")),
                "La palette Flowchart non viene portata in primo piano durante la pausa.");
            Require(debugStatus.Text.Contains("DEBUG IN PAUSA") && form.Text.Contains("DEBUG IN PAUSA"),
                "Lo stato di debug in pausa non è evidente nella barra di stato e nel titolo.");
            Require(editor.ReadOnly && FindControl<TabControl>(form, "documentTabs").Enabled &&
                    !FindMenuItem(form, "saveMenuItem").Enabled && !FindToolbarButton(form, "toolbarSaveButton").Enabled,
                "Durante la pausa il sorgente deve restare protetto, ma le schede navigabili.");
            TreeView variables = FindControl<TreeView>(form, "treeViewVariables");
            tabs.SelectedTab = FindControl<TabPage>(form, "tabPageVariables");
            TreeNode localGroup = variables.Nodes["locals"];
            TreeNode valueVariable = variables.Nodes.Find("value", true).FirstOrDefault();
            TreeNode valuesVariable = variables.Nodes.Find("values", true).FirstOrDefault();
            Require(localGroup != null && valueVariable != null && valueVariable.Text.Contains("int = 2") &&
                    valuesVariable != null && valuesVariable.Text.Contains("int[] = [2]") &&
                    valuesVariable.Nodes.Count == 2 && valuesVariable.Nodes[0].Text.Contains("int = 2"),
                "La palette Variabili non mostra valori, tipi dichiarati o elementi array espandibili.");
            ListView callStack = FindControl<ListView>(form, "callStackListView");
            tabs.SelectedTab = FindControl<TabPage>(form, "tabPageCallStack");
            Require(callStack.Items.Count == 1 && callStack.Items[0].SubItems[0].Text.Contains("0") &&
                    callStack.Items[0].SubItems[1].Text == "main()" &&
                    callStack.Items[0].SubItems[2].Text.Contains("riga 6"),
                "La palette Call Stack non mostra il frame corrente con funzione e posizione.");
            callStack.Items[0].Selected = true;
            InvokePrivate(form, "navigateToSelectedCallStackFrame");
            Require(editor.SelectedText.Contains("value << value + 1"),
                "La navigazione dal Call Stack non raggiunge l'istruzione corrente del frame selezionato.");
            var watchInput = FindControl<TextBox>(form, "watchExpressionTextBox");
            var watchRows = FindControl<ListView>(form, "watchListView");
            tabs.SelectedTab = FindControl<TabPage>(form, "tabPageWatch");
            watchInput.Text = "value";
            FindControl<Button>(form, "watchAddButton").PerformClick();
            Require(watchRows.Items.Count == 1 && watchRows.Items[0].SubItems[2].Text == "2",
                "Watch non mostra il valore alla pausa.");
            watchInput.Text = "value";
            FindControl<Button>(form, "watchAddButton").PerformClick();
            Require(watchRows.Items.Count == 1, "Watch duplica una variabile già presente.");
            watchInput.Text = "values[0]";
            FindControl<Button>(form, "watchAddButton").PerformClick();
            watchInput.Text = "values[1]";
            FindControl<Button>(form, "watchAddButton").PerformClick();
            Require(watchRows.Items.Count == 3 && watchRows.Items[1].SubItems[2].Text == "2" &&
                    watchRows.Items[2].SubItems[2].Text == "0",
                "Watch non risolve gli elementi dell'array nello snapshot della pausa.");
            watchInput.Text = "value + values[0]";
            FindControl<Button>(form, "watchAddButton").PerformClick();
            watchInput.Text = "value << 99";
            FindControl<Button>(form, "watchAddButton").PerformClick();
            Require(watchRows.Items.Count == 5 && watchRows.Items[3].SubItems[1].Text == "int" &&
                    watchRows.Items[3].SubItems[2].Text == "4" &&
                    watchRows.Items[4].SubItems[3].Text.Contains("effetti collaterali"),
                "Watch non calcola le espressioni pure o non rifiuta un'assegnazione.");
            FindToolbarButton(form, "toolbarStepOverButton").PerformClick();
            PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") &&
                editor.SelectedText.Contains("ret value"), 4000,
                "F10 non avanza di una singola istruzione fino alla riga successiva.");
            Require(GetPropertyValue<DdfFlowchartNode>(flowchartCanvas, "ExecutionNode")?.Label.Contains("ret value") == true,
                "Step Over non sposta l'indicatore di esecuzione sul nodo Flowchart successivo.");
            RequireRuntimeEditorFlowchartSynchronization(form, "main", "ret value",
                "Step Over nel documento corrente");
            Require(watchRows.Items[0].SubItems[2].Text == "3" && watchRows.Items[1].SubItems[2].Text == "2",
                "Watch non aggiorna variabile ed elemento array dopo il passo successivo.");
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(debuggerIsIdle, 4000, "Continua non torna allo stato Modifica.");
            Require(output.Text.Contains("Valore restituito: 3") && output.Text.Contains("Esecuzione completata"),
                "Continua non completa l'esecuzione sospesa. Output: " + output.Text);
            Require((int)editor.GetType().GetProperty("DebugExecutionLine").GetValue(editor) == -1,
                "L'evidenziazione della riga sospesa resta visibile dopo Continua.");
            Require(GetPropertyValue<DdfFlowchartNode>(flowchartCanvas, "ExecutionNode") == null &&
                    !flowchartStatus.Text.Contains("DEBUG IN PAUSA", StringComparison.Ordinal),
                "L'indicatore Flowchart resta visibile dopo la fine del debug.");
            Require(debugStatus.Text == "MODIFICA" && !editor.ReadOnly && FindControl<TabControl>(form, "documentTabs").Enabled,
                "L'interfaccia non torna allo stato di modifica al termine del debug: stato=" + debugStatus.Text +
                ", readonly=" + editor.ReadOnly + ", schede=" + FindControl<TabControl>(form, "documentTabs").Enabled + ".");
            Require(watchRows.Items[0].SubItems[2].Text == "—", "Watch conserva valori dopo fine debug.");
            Require(variables.Nodes.Count == 0,
                "La palette Variabili conserva valori dopo la fine del debug.");
            Require(callStack.Items.Count == 0,
                "La palette Call Stack conserva frame dopo la fine del debug.");
            foreach (ListViewItem row in watchRows.Items) row.Selected = true;
            FindControl<Button>(form, "watchRemoveButton").PerformClick();
            Require(watchRows.Items.Count == 0, "Watch non rimuove la selezione.");
            ClickGutterLine(gutter, 6);
            Require(!gutter.Text.Contains("● 6"), "Il breakpoint non viene rimosso dal gutter.");

            editor.Text = "main() out int\n{\n    int value << 1;\n    value++;\n    value++;\n    ret value;\n}";
            PumpMessages(180);
            editor.Select(editor.GetFirstCharIndexFromLine(3), 0);
            FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(() => breakpointSession.IsPaused &&
                breakpointSession.CurrentLocation?.Line == 4 &&
                FindToolbarButton(form, "toolbarBreakpointButton").Enabled,
                4000,
                "Il debug dinamico non raggiunge il primo breakpoint.");
            Require(FindToolbarButton(form, "toolbarBreakpointButton").Enabled,
                "F9 non è disponibile mentre il debug è in pausa.");
            editor.Select(editor.GetFirstCharIndexFromLine(4), 0);
            FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
            editor.Select(editor.GetFirstCharIndexFromLine(3), 0);
            FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
            Require(gutter.Text.Contains("● 5") && !gutter.Text.Contains("● 4"),
                "Il gutter non riflette i breakpoint cambiati durante la pausa.");
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(() => breakpointSession.IsPaused &&
                breakpointSession.CurrentLocation?.Line == 5, 4000,
                "Il nuovo breakpoint non è diventato effettivo alla ripresa.");
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(debuggerIsIdle, 4000,
                "Il debug dinamico non termina dopo il secondo breakpoint.");
            InvokePrivate(form, "clearBreakpoints");

            editor.Text = "helper(int seed) out int\n{\nint localValue << seed;\nlocalValue++;\nret localValue;\n}\nmain() out int\n{\nint callerValue << 40;\nint result << helper(2);\nret result;\n}";
            PumpMessages(180);
            editor.Select(editor.GetFirstCharIndexFromLine(9), 0);
            FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua"), 4000,
                "Il debug non raggiunge la chiamata da usare per Step Into.");
            Require(FindMenuItem(form, "stepIntoMenuItem").Enabled &&
                    FindToolbarButton(form, "toolbarStepIntoButton").Enabled,
                "Step Into non è disponibile durante la pausa.");
            Require(InvokeProcessCmdKey(form, Keys.F10),
                "F10 non avvia il passo che entra nelle funzioni chiamate.");
            PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") &&
                editor.SelectedText.Contains("int localValue"), 4000,
                "F10 non entra nella prima istruzione della funzione chiamata.");
            Require(GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram")?.Flowchart.FunctionName == "helper" &&
                    GetPropertyValue<DdfFlowchartNode>(flowchartCanvas, "ExecutionNode")?.Label.Contains("int localValue") == true,
                "Step Into non porta la Flowchart nella funzione chiamata.");
            RequireRuntimeEditorFlowchartSynchronization(form, "helper", "int localValue",
                "Step Into nella funzione locale");
            Require(callStack.Items.Count == 2 && callStack.Items[0].SubItems[1].Text == "helper()" &&
                    callStack.Items[1].SubItems[1].Text == "main()",
                "Step Into non aggiorna il Call Stack con funzione chiamata e chiamante.");
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(debuggerIsIdle, 4000, "Il debug non termina dopo Step Into e Continua.");
            InvokePrivate(form, "clearBreakpoints");

            editor.Text = "helper(int seed) out int\n{\nint localValue << seed;\nlocalValue++;\nret localValue;\n}\nmain() out int\n{\nint callerValue << 40;\nint result << helper(2);\nret result;\n}";
            PumpMessages(180);
            editor.Select(editor.GetFirstCharIndexFromLine(3), 0);
            FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua"), 4000,
                "Il debug annidato non raggiunge il breakpoint nella funzione chiamata.");
            Require(callStack.Items.Count == 2 && callStack.Items[0].SubItems[1].Text == "helper()" &&
                    callStack.Items[1].SubItems[1].Text == "main()",
                "Il Call Stack non ordina frame corrente e chiamante. Righe=" +
                string.Join(" | ", callStack.Items.Cast<ListViewItem>().Select(item =>
                    string.Join(",", item.SubItems.Cast<ListViewItem.ListViewSubItem>()
                        .Select(cell => cell.Text)))) + ", snapshot=" +
                form.EmbeddedEditor.DebugService.CurrentSnapshot?.CallStack.Count +
                ", privato=" + (GetPrivateField<DdfEditorDebugSnapshot>(form,
                    "watchSnapshot")?.CallStack.Count.ToString() ?? "null"));
            callStack.Items[1].Selected = true;
            PumpMessages(60);
            TreeNode callerVariable = variables.Nodes.Find("callerValue", true).FirstOrDefault();
            Require(callerVariable != null && callerVariable.Text.Contains("int = 40"),
                "La selezione del frame chiamante non aggiorna la palette Variabili al relativo scope.");
            Require(variables.Nodes.Find("localValue", true).Length == 0,
                "La palette Variabili mescola lo scope corrente con quello del chiamante selezionato.");
            InvokePrivate(form, "navigateToSelectedCallStackFrame");
            Require(editor.SelectedText.Contains("helper"),
                "La navigazione al frame chiamante non raggiunge il punto di chiamata. Selezione: '" +
                editor.SelectedText + "'.");
            FindToolbarButton(form, "toolbarStepOutButton").PerformClick();
            PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") &&
                editor.SelectedText.Contains("ret result"), 4000,
                "Shift+F11 non torna alla prima istruzione del chiamante.");
            Require(callStack.Items.Count == 1 && callStack.Items[0].SubItems[1].Text == "main()",
                "Esci dalla funzione non aggiorna il Call Stack al chiamante.");
            Require(variables.Nodes.Find("result", true).FirstOrDefault()?.Text.Contains("int = 3") == true,
                "Esci dalla funzione non espone lo stato aggiornato del chiamante.");
            RequireRuntimeEditorFlowchartSynchronization(form, "main", "ret result",
                "Step Out verso il chiamante locale");
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(debuggerIsIdle, 4000, "Il debug annidato non termina dopo Esci dalla funzione e Continua.");
            InvokePrivate(form, "clearBreakpoints");

            editor.Text = "main() out void { while(true) { } }";
            PumpMessages(180);
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            Require(FindToolbarButton(form, "toolbarStopButton").Enabled, "Stop non viene abilitato durante l'esecuzione.");
            FindToolbarButton(form, "toolbarStopButton").PerformClick();
            PumpUntil(debuggerIsIdle, 4000, "Run non viene riabilitato dopo Stop.");
            Require(output.Text.Contains("Esecuzione arrestata"), "Stop non arresta cooperativamente il runtime.");

            SetPrivateField(form, "requestRuntimeInput", new Func<string>(() => "41"));
            editor.Text = "main() out int { string text << readLine(); print(text); ret toInt(text) + 1; }";
            PumpMessages(180);
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(() => debuggerIsIdle() && output.Text.Contains("41") &&
                    output.Text.Contains("Valore restituito: 42"), 4000,
                "L'esecuzione con input non completa tutti i callback UI.");
            Require(output.Text.Contains("41") && output.Text.Contains("Valore restituito: 42"),
                "Input e funzioni standard non attraversano correttamente la UI. Output: " +
                output.Text);

            using (var capabilityEntered = new ManualResetEventSlim())
            {
                var capabilityRelease = new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                int cleanupCount = 0;
                var capabilityCatalog = new DdfEditorHostCatalog();
                capabilityCatalog.RegisterFunction(new DdfHostFunctionDefinition(
                    "awaitProbe", "int", Array.Empty<DdfHostParameterDefinition>(),
                    invoke: null, invokeAsync: async (context, arguments) =>
                    {
                        context.RegisterResource(new CallbackDisposable(
                            () => Interlocked.Increment(ref cleanupCount)));
                        capabilityEntered.Set();
                        return await capabilityRelease.Task.ConfigureAwait(false);
                    }));
                form.EmbeddedEditor.AttachHostCatalog(capabilityCatalog);
                editor.Text = "main() out int\n{\n    int result << awaitProbe();\n    ret result + 1;\n}";
                PumpMessages(180);
                editor.Select(editor.GetFirstCharIndexFromLine(2), 0);
                FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(() => form.EmbeddedEditor.DebugService.State ==
                        DdfEditorDebugState.Paused &&
                        editor.SelectedText.Contains("awaitProbe", StringComparison.Ordinal),
                    5000, "Il debug non raggiunge la capability asincrona.");
                RequireRuntimeEditorFlowchartSynchronization(form, "main", "awaitProbe",
                    "pausa prima della capability asincrona");

                FindToolbarButton(form, "toolbarStepOverButton").PerformClick();
                PumpUntil(() => capabilityEntered.IsSet &&
                        form.EmbeddedEditor.DebugService.State ==
                            DdfEditorDebugState.AwaitingCapability &&
                        flowchartStatus.Text.Contains("DEBUG IN ATTESA",
                            StringComparison.Ordinal),
                    5000, "Step Over non espone l'attesa cooperativa della capability.");
                RequireRuntimeEditorFlowchartSynchronization(form, "main", "awaitProbe",
                    "sospensione cooperativa sulla capability");
                Require(cleanupCount == 0,
                    "Il cleanup della risorsa avviene prima della ripresa della capability.");

                capabilityRelease.SetResult(41);
                PumpUntil(() => form.EmbeddedEditor.DebugService.State ==
                        DdfEditorDebugState.Paused &&
                        editor.SelectedText.Contains("ret result", StringComparison.Ordinal),
                    5000, "La ripresa cooperativa non raggiunge l'istruzione successiva.");
                RequireRuntimeEditorFlowchartSynchronization(form, "main", "ret result",
                    "ripresa dopo capability asincrona");
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(debuggerIsIdle, 5000,
                    "Il debug asincrono non termina dopo Continua.");
                Require(cleanupCount == 1 &&
                        GetPropertyValue<DdfFlowchartNode>(flowchartCanvas, "ExecutionNode") == null &&
                        (int)editor.GetType().GetProperty("DebugExecutionLine").GetValue(editor) == -1,
                    "Cleanup o rimozione dei marker runtime non avvengono esattamente una volta al termine.");
                InvokePrivate(form, "clearBreakpoints");
            }

            editor.Text = "fail() out int\n{\n    int zero << 0;\n    ret 10 / zero;\n}\nmiddle() out int\n{\n    ret fail();\n}\nmain() out int\n{\n    ret middle();\n}";
            PumpMessages(180);
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(debuggerIsIdle, 4000, "L'errore runtime non chiude la sessione di debug.");
            Require(output.Text.Contains("DDF404") && output.Text.Contains("Stack chiamate DDF:"),
                "L'errore runtime non mostra diagnostica e intestazione dello stack DDF. Output: " + output.Text);
            Require(output.Text.Contains("in fail()") && output.Text.Contains("in middle()") && output.Text.Contains("in main()"),
                "Lo stack runtime non contiene tutte le chiamate DDF. Output: " + output.Text);
            Require(GetPropertyValue<DdfFlowchartNode>(flowchartCanvas, "ExecutionNode") == null &&
                    (int)editor.GetType().GetProperty("DebugExecutionLine").GetValue(editor) == -1,
                "Dopo la propagazione di un errore restano marker runtime non piu' validi.");

            output.SelectionStart = output.Text.LastIndexOf("DDF404", StringComparison.Ordinal);
            InvokeHandler(form, "richTextBoxOutput_DoubleClick", output, EventArgs.Empty);
            Require(editor.SelectedText.Contains("10 / zero"),
                "Il doppio clic sulla diagnostica runtime non seleziona l'istruzione che ha fallito.");

            output.SelectionStart = output.Text.LastIndexOf("in fail()", StringComparison.Ordinal);
            InvokeHandler(form, "richTextBoxOutput_DoubleClick", output, EventArgs.Empty);
            Require(editor.SelectedText.Contains("10 / zero"),
                "Il doppio clic sul frame runtime corrente non raggiunge l'istruzione che ha generato il fault. Selezione: '" + editor.SelectedText + "'.");
            output.SelectionStart = output.Text.LastIndexOf("in middle()", StringComparison.Ordinal);
            InvokeHandler(form, "richTextBoxOutput_DoubleClick", output, EventArgs.Empty);
            Require(editor.SelectedText == "fail",
                "Il doppio clic sul frame runtime chiamante non raggiunge la chiamata a fail(). Selezione: '" + editor.SelectedText + "'.");
            Require(FindMenuItem(form, "newMenuItem").Enabled,
                "Il debugger non riabilita i comandi documento dopo un errore runtime.");
            FindMenuItem(form, "closeWorkspaceMenuItem").PerformClick();
            FindMenuItem(form, "newMenuItem").PerformClick();
            var untitledEditor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
            untitledEditor.Text = "main() out int\n{\nint value << 1;\nvalue++;\nret value;\n}";
            PumpMessages(200);
            untitledEditor.Select(untitledEditor.GetFirstCharIndexFromLine(3), 0);
            FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
            Require(FindControl<RichTextBox>(form, "richTextBoxLineNumbers").Text.Contains("● 4"),
                "Il breakpoint della scheda non salvata non viene registrato nel gutter.");
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua"),
                10000, "Il debug della scheda non salvata ignora il breakpoint. Output: " + output.Text);
            Require(untitledEditor.SelectedText.Contains("value++"), "La pausa non torna al buffer senza percorso.");
            Require(!FindMenuItem(form, "newMenuItem").Enabled &&
                    FindControl<TabControl>(form, "documentTabs").Enabled &&
                    untitledEditor.ReadOnly,
                "Durante la pausa le schede devono restare navigabili senza poter modificare il sorgente.");
            FindToolbarButton(form, "toolbarRunButton").PerformClick();
            PumpUntil(debuggerIsIdle, 4000, "Il documento non salvato non completa il debug.");
            Require(output.Text.Contains("Valore restituito: 2"),
                "Il documento non salvato non completa correttamente. Output: " + output.Text);

            string workspaceRoot = Path.Combine(Path.GetTempPath(), "ddf-debug-workspace-" + Guid.NewGuid().ToString("N"));
            string externalRoot = Path.Combine(Path.GetTempPath(), "ddf-debug-external-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(externalRoot);
            try
            {
                File.WriteAllText(Path.Combine(workspaceRoot, "main.ddf"),
                    "main() out int\n{\nint result << helper(98);\nret result;\n}");
                string unusedPath = Path.Combine(workspaceRoot, "unused.ddf");
                File.WriteAllText(unusedPath,
                    "helper(int seed) out int\n{\nint localValue << seed;\nlocalValue++;\nret localValue;\n}\n" +
                    "unused() out int\n{\nint forgotten << 1;\nforgotten++;\nret forgotten;\n}" +
                    new string('\n', 90));
                string externalPath = Path.Combine(externalRoot, "watch-smoke-test.ddf");
                File.WriteAllText(externalPath,
                    "main() out int\n{\nint watched << 10;\nwatched++;\nret watched;\n}");
                Require((bool)InvokePrivate(form, "openWorkspace", workspaceRoot),
                    "Il workspace della regressione debug non viene aperto.");
                PumpUntil(() => GetPrivateField<DdfWorkspaceIndex>(form, "workspaceIndex")?.RootPath ==
                        Path.GetFullPath(workspaceRoot) &&
                        GetPrivateField<DdfWorkspaceIndex>(form, "workspaceIndex").Documents.Count == 2,
                    4000, "Il workspace della regressione debug non viene indicizzato.");
                string savedWatchSettings = null;
                SetPrivateField(form, "saveWatchExpressionsSetting", new Action<string>(value => savedWatchSettings = value));
                tabs.SelectedTab = FindControl<TabPage>(form, "tabPageWatch");
                watchInput.Text = "forgotten + 1";
                FindControl<Button>(form, "watchAddButton").PerformClick();
                Require(!string.IsNullOrWhiteSpace(savedWatchSettings) &&
                        WatchExpressionSettings.Parse(savedWatchSettings).GetExpressions(workspaceRoot)
                            .SequenceEqual(new[] { "forgotten + 1" }),
                    "La Watch non salva le espressioni nel profilo del progetto attivo. Progetto=" +
                    GetPrivateField<string>(form, "activeWatchProjectPath") + ", righe=" + watchRows.Items.Count +
                    ", dati=" + (savedWatchSettings ?? "<null>"));
                InvokePrivate(form, "switchWatchProject", (object)null);
                Require(watchRows.Items.Count == 0, "La Watch conserva le espressioni passando fuori dal progetto.");
                InvokePrivate(form, "switchWatchProject", workspaceRoot);
                Require(watchRows.Items.Count == 1 && watchRows.Items[0].Text == "forgotten + 1",
                    "La Watch non ripristina le espressioni tornando al progetto.");
                Require((bool)InvokePrivate(form, "openDocument", unusedPath),
                    "Il file workspace privo di main non viene aperto.");
                RichTextBox unusedEditor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                PumpMessages(200);
                unusedEditor.Select(unusedEditor.GetFirstCharIndexFromLine(9), 0);
                FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(debuggerIsIdle, 4000,
                    "Il breakpoint dimenticato nel codice non raggiungibile ha sospeso il debug.");
                Require(output.Text.Contains("Valore restituito: 99") &&
                        !output.Text.Contains("Breakpoint raggiunto: riga 4"),
                    "Il debug del workspace non parte dall'unico main o considera breakpoint in codice non eseguito. Output: " +
                    output.Text);
                InvokePrivate(form, "clearBreakpoints");
                unusedEditor.Select(unusedEditor.TextLength, 0);
                unusedEditor.ScrollToCaret();
                PumpMessages(150);
                string mainPath = Path.Combine(workspaceRoot, "main.ddf");
                Require((bool)InvokePrivate(form, "openDocument", mainPath),
                    "Il file main del workspace non viene aperto per la prova Step Into multifile.");
                RichTextBox mainEditor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                mainEditor.Select(mainEditor.GetFirstCharIndexFromLine(2), 0);
                FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") &&
                    GetPrivateField<OpenDocumentCollection>(form, "openDocuments").ActiveDocument?.Session.CurrentPath == mainPath,
                    4000, "Il debug multifile non raggiunge la chiamata nel file main.");
                Require(mainEditor.SelectedText.Contains("helper(98)"),
                    "La pausa iniziale non evidenzia la chiamata nel file main.");
                RequireRuntimeEditorFlowchartSynchronization(form, "main", "helper(98)",
                    "pausa nel chiamante multi-file", mainPath);
                Require(InvokeProcessCmdKey(form, Keys.F10),
                    "La scorciatoia F10 non viene intercettata durante la pausa multifile.");
                PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") &&
                    string.Equals(GetPrivateField<OpenDocumentCollection>(form, "openDocuments").ActiveDocument?.Session.CurrentPath,
                        unusedPath, StringComparison.OrdinalIgnoreCase),
                    4000, "Step Into non attiva automaticamente la scheda della funzione esterna.");
                RichTextBox helperEditor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                TabControl documentTabs = FindControl<TabControl>(form, "documentTabs");
                Require(string.Equals(documentTabs.SelectedTab?.Tag as string,
                        GetPrivateField<OpenDocumentCollection>(form, "openDocuments").ActiveDocument?.Id,
                        StringComparison.Ordinal),
                    "La linguetta visibile non segue il documento attivato dal debugger.");
                Require(form.Text.Contains("— " + Path.GetFileName(unusedPath),
                        StringComparison.Ordinal),
                    "Il titolo della finestra non segue la funzione esterna durante il debug: " +
                    form.Text);
                Require(helperEditor.SelectedText.Contains("int localValue"),
                    "Step Into non evidenzia la prima istruzione della funzione esterna.");
                PumpMessages(200);
                int pausedY = helperEditor.GetPositionFromCharIndex(
                    helperEditor.SelectionStart).Y;
                Require(pausedY >= 0 && pausedY < helperEditor.ClientSize.Height,
                    "La riga di pausa nel file esterno resta fuori dalla viewport: y=" +
                    pausedY + ", altezza=" + helperEditor.ClientSize.Height + ".");
                Require(string.Equals(GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram")?.Flowchart.DocumentPath,
                        unusedPath, StringComparison.OrdinalIgnoreCase) &&
                        GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram")?.Flowchart.FunctionName == "helper" &&
                        GetPropertyValue<DdfFlowchartNode>(flowchartCanvas, "ExecutionNode")?.Label.Contains("int localValue") == true,
                    "La Flowchart non segue lo snapshot di esecuzione nella funzione multi-file.");
                RequireRuntimeEditorFlowchartSynchronization(form, "helper", "int localValue",
                    "Step Into con cambio file", unusedPath);
                RichTextBox educationalExplanation = FindControl<RichTextBox>(form,
                    "flowchartEducationalExplanationBox");
                Label educationalTitle = FindControl<Label>(form, "flowchartEducationalTitleLabel");
                Require(educationalTitle.Text.Contains("PASSO CORRENTE", StringComparison.Ordinal) &&
                        educationalExplanation.Text.Contains("L'esecuzione è sospesa", StringComparison.Ordinal) &&
                        educationalExplanation.Text.Contains("seed = 98", StringComparison.Ordinal),
                    "La guida didattica non descrive il passo o i valori coinvolti nel debug multi-file. Titolo='" +
                    educationalTitle.Text + "'; testo='" + educationalExplanation.Text + "'.");
                Require(InvokeProcessCmdKey(form, Keys.F10),
                    "La scorciatoia F10 non viene intercettata nella funzione esterna.");
                PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") &&
                    helperEditor.SelectedText.Contains("localValue++"), 4000,
                    "F10 non percorre istruzione per istruzione la funzione esterna.");
                RequireRuntimeEditorFlowchartSynchronization(form, "helper", "localValue++",
                    "F10 nel file esterno", unusedPath);
                Require(InvokeProcessCmdKey(form, Keys.Shift | Keys.F11),
                    "La scorciatoia Shift+F11 non viene intercettata nella funzione esterna.");
                PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") &&
                    string.Equals(GetPrivateField<OpenDocumentCollection>(form, "openDocuments").ActiveDocument?.Session.CurrentPath,
                        mainPath, StringComparison.OrdinalIgnoreCase),
                    4000, "Step Out non riattiva automaticamente la scheda del chiamante.");
                Require(FindControl<RichTextBox>(form, "richTextBoxMainEditor").SelectedText.Contains("ret result"),
                    "Step Out non evidenzia l'istruzione successiva nel chiamante.");
                Require(string.Equals(GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram")?.Flowchart.DocumentPath,
                        mainPath, StringComparison.OrdinalIgnoreCase) &&
                        GetPropertyValue<DdfFlowchartLayout>(flowchartCanvas, "Diagram")?.Flowchart.FunctionName == "main" &&
                        GetPropertyValue<DdfFlowchartNode>(flowchartCanvas, "ExecutionNode")?.Label.Contains("ret result") == true,
                    "Step Out non riporta la Flowchart al nodo del chiamante.");
                RequireRuntimeEditorFlowchartSynchronization(form, "main", "ret result",
                    "Step Out con ritorno al file chiamante", mainPath);
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(debuggerIsIdle, 4000, "Il debug multifile non termina dopo il ritorno al chiamante.");
                InvokePrivate(form, "clearBreakpoints");
                Require((bool)InvokePrivate(form, "openDocument", externalPath),
                    "Il documento esterno al workspace non viene aperto.");
                RichTextBox externalEditor = FindControl<RichTextBox>(form, "richTextBoxMainEditor");
                PumpMessages(200);
                externalEditor.Select(externalEditor.GetFirstCharIndexFromLine(3), 0);
                FindToolbarButton(form, "toolbarBreakpointButton").PerformClick();
                OpenDocumentBuffer externalBuffer = GetPrivateField<OpenDocumentCollection>(form, "openDocuments").ActiveDocument;
                Require(externalBuffer != null && externalBuffer.Session.HasPath &&
                        string.Equals(externalBuffer.Session.CurrentPath, externalPath, StringComparison.OrdinalIgnoreCase) &&
                        externalBuffer.BreakpointLines.Contains(4) && !externalBuffer.UnboundBreakpointLines.Contains(4),
                    "Il breakpoint del documento esterno non viene associato al buffer attivo.");
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(() => FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua") ||
                    !FindToolbarButton(form, "toolbarStopButton").Enabled,
                    4000, "Il debug del documento esterno non raggiunge né la pausa né il completamento.");
                Require(FindMenuItem(form, "runProgramMenuItem").Text.Contains("Continua"),
                    "Con un workspace aperto il debug ignora il breakpoint del documento esterno attivo. Output: " + output.Text);
                Require(externalEditor.SelectedText.Contains("watched++"),
                    "Con un workspace aperto il debugger seleziona l'entry point del progetto invece del documento esterno.");
                FindToolbarButton(form, "toolbarRunButton").PerformClick();
                PumpUntil(debuggerIsIdle, 4000, "Il documento esterno non completa il debug.");
                Require(output.Text.Contains("Valore restituito: 11") && !output.Text.Contains("Valore restituito: 99"),
                    "F5 ha eseguito il workspace invece del documento esterno attivo.");
                FindMenuItem(form, "closeWorkspaceMenuItem").PerformClick();
                PumpMessages(240);
            }
            finally
            {
                if (Directory.Exists(workspaceRoot)) Directory.Delete(workspaceRoot, true);
                if (Directory.Exists(externalRoot)) Directory.Delete(externalRoot, true);
            }
            Console.WriteLine("PASS interprete e debug: file salvati, buffer senza percorso, file esterni e breakpoint isolati per documento");
        }

        private static void AssertMainToolbar(MainForm form)
        {
            Require(!SameToolbarIcon(FindToolbarButton(form, "toolbarLanguageManagerButton"),
                FindToolbarButton(form, "toolbarSettingsButton")),
                "Gestione pacchetti e Impostazioni usano la stessa icona.");
            ToolStripPanel toolbarPanel = FindControl<ToolStripPanel>(form, "toolStripPanelMain");
            ToolStrip[] toolbars = FindToolbars(form).ToArray();
            string[] expected =
            {
                "toolbarNewButton", "toolbarOpenButton", "toolbarSaveButton", "toolbarSaveAllButton", "toolbarCloseDocumentButton",
                "toolbarUndoButton", "toolbarRedoButton", "toolbarCutButton",
                "toolbarCopyButton", "toolbarPasteButton", "toolbarCommentButton",
                "toolbarDuplicateLinesButton", "toolbarMoveLinesUpButton", "toolbarMoveLinesDownButton", "toolbarDeleteLinesButton",
                "toolbarExpandSelectionButton", "toolbarShrinkSelectionButton", "toolbarMatchingDelimiterButton",
                "toolbarSelectNextOccurrenceButton", "toolbarSelectAllOccurrencesButton",
                "toolbarFindButton", "toolbarWorkspaceSearchButton", "toolbarWorkspaceReplaceButton",
                "toolbarCommandPaletteButton", "toolbarSettingsButton",
                "toolbarNavigateBackButton", "toolbarNavigateForwardButton",
                "toolbarGoToFileButton", "toolbarGoToSymbolButton", "toolbarFindReferencesButton",
                "toolbarGoToLineButton", "toolbarGoToLastEditButton",
                "toolbarCompletionButton",
                "toolbarQuickFixButton",
                "toolbarFormatButton", "toolbarFoldButton", "toolbarHighlightCurrentLineButton",
                "toolbarShowWhitespaceButton", "toolbarShowIndentGuidesButton", "toolbarWordWrapButton",
                "toolbarOverviewButton",
                "toolbarValidateButton", "toolbarBuildButton", "toolbarCompileAndRunButton", "toolbarRunLastBuildButton", "toolbarPublishExecutableButton", "toolbarOpenCompiledOutputButton", "toolbarRunWithoutDebugButton",
                "toolbarLanguageManagerButton", "toolbarLanguageStudioButton", "toolbarLanguagePlaygroundButton", "toolbarModuleManagerButton",
                "toolbarBreakpointButton", "toolbarRunButton", "toolbarStepIntoButton", "toolbarStepOverButton", "toolbarStepOutButton", "toolbarStopButton"
            };
            foreach (string name in expected)
            {
                ToolStripButton button = FindToolbarButton(form, name);
                Require(button.DisplayStyle == ToolStripItemDisplayStyle.Image && button.Image != null,
                    "Il comando toolbar non presenta un'icona: " + name);
                Require(!string.IsNullOrWhiteSpace(button.ToolTipText), "Tooltip toolbar mancante: " + name);
                Require(!string.IsNullOrWhiteSpace(button.AccessibleName), "Nome accessibile toolbar mancante: " + name);
                Require(button.Image.Width >= 40 && button.Image.Height >= 40,
                    "L'icona toolbar non ha risoluzione sufficiente: " + name);
                foreach (int targetSize in new[] { 20, 30, 40 })
                {
                    using (var scaled = new Bitmap(button.Image, new Size(targetSize, targetSize)))
                    {
                        bool visiblePixel = false;
                        for (int y = 0; y < targetSize && !visiblePixel; y++)
                            for (int x = 0; x < targetSize && !visiblePixel; x++)
                                visiblePixel = scaled.GetPixel(x, y).A > 0;
                        Require(visiblePixel, "Icona toolbar vuota a " + targetSize + " px: " + name);
                    }
                }
            }
            ToolStripButton[] icons = expected.Select(name => FindToolbarButton(form, name)).ToArray();
            for (int left = 0; left < icons.Length; left++)
                for (int right = left + 1; right < icons.Length; right++)
                    Require(!SameToolbarIcon(icons[left], icons[right]),
                        "Icone toolbar indistinguibili: " + icons[left].Name + " e " + icons[right].Name);
            Require(toolbars.SelectMany(item => item.Items.OfType<ToolStripButton>()).Count() == expected.Length,
                "Numero inatteso di pulsanti nelle barre degli strumenti.");
            Require(toolbars.Length == 7 && toolbars.All(item => item.GripStyle == ToolStripGripStyle.Visible),
                "Le sette barre per categoria non sono indipendenti e trascinabili.");
            Require(toolbarPanel.Top >= form.MainMenuStrip.Bottom,
                "Le barre degli strumenti non sono posizionate sotto il menu principale: panel=" +
                toolbarPanel.Bounds + ", menu=" + form.MainMenuStrip.Bounds + ".");
            Require(toolbarPanel.Height <= 110,
                "La disposizione iniziale delle barre occupa troppo spazio verticale: " + toolbarPanel.Height + " px; " +
                string.Join(", ", toolbars.Select(item => item.Name + "=" + item.Bounds)) + ".");
            ToolStripMenuItem toolbarMenu = FindMenuItem(form, "toolbarsMenuItem");
            Require(toolbarMenu.DropDownItems.OfType<ToolStripMenuItem>().Count() == 7,
                "Visualizza non espone tutte le categorie di barre degli strumenti.");
            string persistedToolbarLayout = null;
            SetPrivateField(form, "saveToolbarLayoutSetting", new Action<string>(value => persistedToolbarLayout = value));
            ToolStripMenuItem codeVisibility = FindMenuItem(form, "showToolbarCodeMenuItem");
            ToolStrip codeToolbar = FindControl<ToolStrip>(form, "toolStripCode");
            codeVisibility.PerformClick();
            Require(!codeToolbar.Visible && persistedToolbarLayout != null && persistedToolbarLayout.Contains("code.visible=False"),
                "La barra Codice non viene nascosta o la preferenza non viene salvata.");
            codeVisibility.PerformClick();
            Require(codeToolbar.Visible && persistedToolbarLayout.Contains("code.visible=True"),
                "La barra Codice non può essere riattivata dal menu Visualizza.");
            Point originalToolbarLocation = codeToolbar.Location;
            codeToolbar.Location = new Point(originalToolbarLocation.X + 6, originalToolbarLocation.Y);
            InvokePrivate(form, "persistToolbarLayout");
            Require(persistedToolbarLayout.Contains("code.x=" + codeToolbar.Left) &&
                    persistedToolbarLayout.Contains("code.y=" + codeToolbar.Top) &&
                    persistedToolbarLayout.Contains("code.row=") &&
                    persistedToolbarLayout.Contains("code.order="),
                "La posizione corrente della barra Codice non viene serializzata.");
            codeToolbar.Location = originalToolbarLocation;
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
            Console.WriteLine("PASS sette toolbar mobili per categoria con 58 comandi, posizione e visibilità persistenti");
        }

        private static bool SameToolbarIcon(ToolStripButton left, ToolStripButton right)
        {
            using (var first = new Bitmap(left.Image))
            using (var second = new Bitmap(right.Image))
            {
                if (first.Size != second.Size) return false;
                for (int y = 0; y < first.Height; y++)
                    for (int x = 0; x < first.Width; x++)
                        if (first.GetPixel(x, y) != second.GetPixel(x, y)) return false;
                return true;
            }
        }

        private static void AssertToolbarDpiGeometry()
        {
            foreach (bool embedded in new[] { false, true })
            using (var form = new MainForm(embedded))
            {
                ToolStrip[] strips = FindToolbars(form).ToArray();
                ToolStripDropDownButton language = strips.SelectMany(strip => strip.Items.OfType<ToolStripDropDownButton>())
                    .Single(item => item.Name == "toolbarLanguageButton");
                foreach (int dpi in new[] { 96, 144, 192, 96 })
                {
                    InvokePrivate(form, "applyToolbarDpi", dpi);
                    int icon = (int)Math.Round(20 * dpi / 96d);
                    int width = (int)Math.Round(30 * dpi / 96d);
                    int height = (int)Math.Round(28 * dpi / 96d);
                    Require(strips.All(strip => strip.ImageScalingSize == new Size(icon, icon)),
                        "Iconbar senza scala immagine " + dpi + " DPI (embedded=" + embedded + ").");
                    Require(strips.SelectMany(strip => strip.Items.OfType<ToolStripButton>()).All(button =>
                            button.Size == new Size(width, height) && button.Image != null &&
                            !string.IsNullOrWhiteSpace(button.AccessibleName)),
                        "Pulsante iconbar non scalato o non accessibile a " + dpi + " DPI (embedded=" + embedded + ").");
                    Require(language.Size == new Size((int)Math.Round(38 * dpi / 96d), height),
                        "Selettore lingua non scalato a " + dpi + " DPI (embedded=" + embedded + ").");
                }
            }
            Console.WriteLine("PASS geometria toolbar standalone/embedded a 96/144/192 DPI e ritorno a 96");
        }

        private static void AssertRunLastBuildCommand(bool visible)
        {
            using (var form = ShowSmokeForm(visible))
            {
                var service = new FakeCompiledBuildService();
                form.AttachStandaloneCompiledBuildService(service);
                ToolStripMenuItem command = FindMenuItem(form,
                    "runLastBuildMenuItem");
                ToolStripButton toolbar = FindToolbarButton(form,
                    "toolbarRunLastBuildButton");
                Require(command.Available && command.Enabled &&
                        toolbar.Available && toolbar.Enabled,
                    "Esegui ultima build non si abilita dopo una build valida.");
                command.PerformClick();
                PumpUntil(() => service.RunCount == 1 && command.Enabled,
                    5000, "Esegui ultima build non ha completato il comando UI.");
                RichTextBox output = FindControl<RichTextBox>(form,
                    "richTextBoxOutput");
                Require(output.Text.Contains("compiled-output",
                            StringComparison.Ordinal) &&
                        output.Text.Contains("Valore restituito: 42",
                            StringComparison.Ordinal) &&
                        output.Text.Contains("17 passi runtime",
                            StringComparison.Ordinal),
                    "L'Output non riporta risultato e passi dell'ultima build.");
            }
            Console.WriteLine("PASS Esegui ultima build da menu e toolbar con Output worker");
        }

        private static void AssertDocumentTabsDoNotCoverSource(MainForm form, RichTextBox editor)
        {
            TabControl tabs = FindControl<TabControl>(form, "documentTabs");
            ToolStripPanel toolbarPanel = FindControl<ToolStripPanel>(form, "toolStripPanelMain");
            Point toolbarBottom = toolbarPanel.PointToScreen(new Point(0, toolbarPanel.ClientSize.Height));
            Point tabsTop = tabs.PointToScreen(Point.Empty);
            Require(tabs.Visible && tabsTop.Y >= toolbarBottom.Y,
                "Le barre degli strumenti coprono le linguette dei documenti: barre=" +
                toolbarPanel.Bounds + ", schede=" + tabs.Bounds + ".");
            Point tabsBottom = tabs.PointToScreen(new Point(0, tabs.ClientSize.Height));
            Point editorTop = editor.PointToScreen(Point.Empty);
            Require(editorTop.Y >= tabsBottom.Y,
                "La barra delle schede copre la prima parte visibile del documento.");

            const string multiline = "prima riga\nseconda riga\nterza riga";
            editor.Text = string.Empty;
            editor.Select(0, 0);
            SetClipboardTextWithRetry(multiline);
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
                "toolStripPanelMain", "toolStripMain", "toolStripEdit", "toolStripCode", "toolStripNavigation",
                "toolStripView", "toolStripProject", "toolStripDebug", "breadcrumbPanel", "panelOutline", "navigationTabs", "workspaceTabPage", "outlineTabPage",
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

        private static void AssertEditorSettings(MainForm form, RichTextBox editor)
        {
            string persisted = null;
            string persistedShortcuts = null;
            string persistedProductUpdates = null;
            int dialogCount = 0;
            SetPrivateField(form, "saveEditorSettingsSetting", new Action<string>(value => persisted = value));
            SetPrivateField(form, "saveShortcutSettingsSetting", new Action<string>(value => persistedShortcuts = value));
            SetPrivateField(form, "saveProductUpdateSettingsSetting",
                new Action<string>(value => persistedProductUpdates = value));
            SetPrivateField(form, "showSettingsDialog", new Func<SettingsForm, DialogResult>(dialog =>
            {
                dialogCount++;
                Require(dialog.StartPosition == FormStartPosition.CenterScreen && IsLight(dialog.BackColor),
                    "Impostazioni non è chiara e centrata sullo schermo.");
                FindControl<ComboBox>(dialog, "settingsFontComboBox").Text = "Consolas";
                FindControl<NumericUpDown>(dialog, "settingsFontSizeNumeric").Value = 12M;
                FindControl<NumericUpDown>(dialog, "settingsZoomNumeric").Value = 125M;
                FindControl<ComboBox>(dialog, "settingsThemeComboBox").SelectedIndex = 1;
                FindControl<ComboBox>(dialog, "settingsIndentStyleComboBox").SelectedIndex = 1;
                FindControl<NumericUpDown>(dialog, "settingsIndentSizeNumeric").Value = 2M;
                FindControl<ComboBox>(dialog, "settingsLineEndingComboBox").SelectedIndex = 1;
                FindControl<CheckBox>(dialog, "settingsFormatOnSaveCheckBox").Checked = true;
                FindControl<CheckBox>(dialog, "settingsAutoSaveCheckBox").Checked = true;
                FindControl<NumericUpDown>(dialog, "settingsAutoSaveDelayNumeric").Value = 1M;
                FindControl<CheckBox>(dialog, "settingsHotExitCheckBox").Checked = true;
                FindControl<CheckBox>(dialog, "settingsRestoreSessionCheckBox").Checked = true;
                FindControl<CheckBox>(dialog, "settingsCrashRecoveryCheckBox").Checked = true;
                FindControl<CheckBox>(dialog, "settingsHighlightCurrentLineCheckBox").Checked = false;
                FindControl<CheckBox>(dialog, "settingsShowWhitespaceCheckBox").Checked = true;
                FindControl<CheckBox>(dialog, "settingsShowIndentGuidesCheckBox").Checked = false;
                FindControl<CheckBox>(dialog, "settingsWordWrapCheckBox").Checked = true;
                FindControl<CheckBox>(dialog, "settingsShowOverviewCheckBox").Checked = false;
                FindControl<CheckBox>(dialog,
                    "settingsAutomaticUpdateCheckBox").Checked = false;
                FindControl<ComboBox>(dialog,
                    "settingsUpdateChannelComboBox").SelectedIndex = 0;
                FindControl<NumericUpDown>(dialog,
                    "settingsUpdateCadenceNumeric").Value = 3M;
                Require(FindControl<DataGridView>(dialog, "settingsShortcutGrid").Rows.Count >= 46,
                    "La scheda Scorciatoie non elenca i comandi configurabili.");
                Require(!dialog.TrySetShortcut("duplicateLinesMenuItem", Keys.Control | Keys.F, out string conflict) &&
                        conflict.Contains("Trova", StringComparison.OrdinalIgnoreCase),
                    "Un conflitto di scorciatoia non viene rilevato e spiegato.");
                Require(dialog.TrySetShortcut("duplicateLinesMenuItem", Keys.Control | Keys.Alt | Keys.D, out conflict),
                    "Una scorciatoia valida e libera non viene accettata: " + conflict);
                return DialogResult.OK;
            }));
            FindMenuItem(form, "settingsMenuItem").PerformClick();
            EditorSettings settings = GetPrivateField<EditorSettings>(form, "editorSettings");
            Require(dialogCount == 1 && settings.UseTabs && settings.IndentSize == 2 && settings.FormatOnSave &&
                    settings.AutoSaveEnabled && settings.AutoSaveDelaySeconds == 1 && settings.HotExitEnabled &&
                    settings.RestoreSessionEnabled &&
                    settings.CrashRecoveryEnabled &&
                    !settings.HighlightCurrentLine && settings.ShowWhitespace &&
                    !settings.ShowIndentGuides && settings.WordWrap && editor.WordWrap && !settings.ShowOverview &&
                    !FindControl<Control>(form, "editorOverviewBar").Visible &&
                    settings.LineEnding == EditorLineEnding.CrLf && persisted != null && persistedShortcuts != null,
                "Le preferenze scelte non vengono applicate e persistite.");
            ProductUpdateSettings productUpdates =
                GetPrivateField<ProductUpdateSettings>(form,
                    "productUpdateSettings");
            Require(!productUpdates.AutomaticCheckEnabled &&
                    productUpdates.Channel == DdfProductUpdateChannel.Stable &&
                    productUpdates.CadenceDays == 3 &&
                    ProductUpdateSettings.Parse(persistedProductUpdates)
                        .Serialize() == productUpdates.Serialize(),
                "Canale e cadenza degli aggiornamenti non vengono persistiti.");
            Require(Math.Abs(editor.Font.Size - 12F) < 0.1F && Math.Abs(editor.ZoomFactor - 1.25F) < 0.02F && IsLight(editor.BackColor),
                "Font, zoom o tema non vengono applicati subito all'editor: font=" +
                editor.Font.Size.ToString("F2") + ", zoom=" + editor.ZoomFactor.ToString("F2") +
                ", background=" + editor.BackColor + ".");
            Require(EditorSettings.Parse(persisted).Serialize() == settings.Serialize() &&
                    settings.ApplyLineEndings("a\nb") == "a\r\nb",
                "Serializzazione o fine riga CRLF delle preferenze non è stabile.");
            Require(FindMenuItem(form, "duplicateLinesMenuItem").ShortcutKeys == (Keys.Control | Keys.Alt | Keys.D) &&
                    FindToolbarButton(form, "toolbarDuplicateLinesButton").ToolTipText.Contains("Ctrl+Alt+D", StringComparison.OrdinalIgnoreCase),
                "Menu e tooltip toolbar non riflettono la scorciatoia personalizzata.");
            IReadOnlyList<ShortcutCommandDefinition> shortcutDefinitions =
                GetPrivateField<IReadOnlyList<ShortcutCommandDefinition>>(form, "shortcutDefinitions");
            ShortcutSettings parsedShortcuts = ShortcutSettings.Parse(persistedShortcuts, shortcutDefinitions);
            Require(parsedShortcuts.Get("duplicateLinesMenuItem") == (Keys.Control | Keys.Alt | Keys.D),
                "La scorciatoia personalizzata non viene serializzata in modo stabile.");
            var paletteCommands = (IReadOnlyList<CommandPaletteCommand>)InvokePrivate(form, "createCommandPaletteCommands");
            Require(paletteCommands.Any(command => command.Title == "Duplica righe" &&
                    command.ShortcutText.Contains("Ctrl+Alt+D", StringComparison.OrdinalIgnoreCase)),
                "La Command Palette non riflette la scorciatoia personalizzata.");

            editor.Text = "value << 1;";
            editor.Select(0, 0);
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.Alt | Keys.D) && editor.Lines.Length == 2,
                "La scorciatoia personalizzata non esegue il comando reale nell'editor.");

            editor.Text = string.Empty;
            editor.Select(0, 0);
            InvokeHandler(form, "richTextBoxMainEditor_KeyDown", editor, new KeyEventArgs(Keys.Tab));
            Require(editor.Text == "\t", "L'indentazione configurata a Tab inserisce ancora spazi.");
            editor.Text = "main() out int{ret 1;}";
            FindMenuItem(form, "formatDocumentMenuItem").PerformClick();
            Require(editor.Text.Contains("\n\tret 1;", StringComparison.Ordinal),
                "Il formatter non usa l'indentazione a Tab configurata.");

            string settingsSavePath = Path.Combine(Path.GetTempPath(), "ddf-settings-" + Guid.NewGuid().ToString("N") + ".ddf");
            try
            {
                editor.Text = "main() out int{ret 2;}";
                SetPrivateField(form, "showSaveFileDialog", new Func<SaveFileDialog, DialogResult>(dialog =>
                {
                    dialog.FileName = settingsSavePath;
                    return DialogResult.OK;
                }));
                Require((bool)InvokePrivate(form, "saveDocument", false),
                    "Il salvataggio configurato non viene completato.");
                string saved = File.ReadAllText(settingsSavePath);
                Require(saved.Contains("\r\n\tret 2;", StringComparison.Ordinal),
                    "Formatta al salvataggio, Tab o fine riga CRLF non vengono applicati al file.");

                DateTime fakeNow = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
                SetPrivateField(form, "getUtcNow", new Func<DateTime>(() => fakeNow));
                editor.Text = "main() out int { ret 3; }";
                fakeNow = fakeNow.AddSeconds(2);
                InvokeHandler(form, "autoSaveTimer_Tick", form, EventArgs.Empty);
                saved = File.ReadAllText(settingsSavePath);
                Require(saved.Contains("ret 3;", StringComparison.Ordinal) &&
                        !GetPrivateField<DocumentSession>(form, "documentSession").IsDirty,
                    "L'autosave non salva il file dopo il ritardo configurato o non aggiorna lo stato dirty.");

                int sessionSelection = editor.Text.IndexOf("ret", StringComparison.Ordinal);
                editor.Select(sessionSelection, 3);
                FindMenuItem(form, "toggleBreakpointMenuItem").PerformClick();
                string workspaceSessionData = null;
                SetPrivateField(form, "saveWorkspaceSessionSetting", new Action<string>(value => workspaceSessionData = value));
                InvokePrivate(form, "persistWorkspaceSession");
                WorkspaceSessionSnapshot workspaceSession = WorkspaceSessionSnapshot.Parse(workspaceSessionData);
                Require(workspaceSession?.Documents.Count == 1 &&
                        workspaceSession.Documents[0].Path == Path.GetFullPath(settingsSavePath) &&
                        workspaceSession.Documents[0].SelectionStart == sessionSelection &&
                        workspaceSession.Documents[0].BreakpointLines.SequenceEqual(new[] { 1 }),
                    "La sessione non serializza scheda, selezione e breakpoint correnti.");

                editor.Text = "contenuto transitorio";
                InvokePrivate(form, "clearBreakpoints");
                GetPrivateField<DocumentSession>(form, "documentSession").MarkSaved(settingsSavePath);
                SetPrivateField(form, "loadWorkspaceSessionSetting", new Func<string>(() => workspaceSessionData));
                InvokePrivate(form, "restoreWorkspaceSession");
                Require(editor.Text.Contains("ret 3;", StringComparison.Ordinal) && editor.SelectedText == "ret" &&
                        FindControl<RichTextBox>(form, "richTextBoxLineNumbers").Text.Contains("● 1", StringComparison.Ordinal),
                    "Il ripristino sessione non ricarica dal disco selezione e breakpoint.");

                string hotExitData = null;
                bool hotExitCleared = false;
                editor.Text = "main() out int {\n\tret 4;\n}";
                editor.Select(editor.Text.IndexOf("ret", StringComparison.Ordinal), 3);
                SetPrivateField(form, "saveHotExitSnapshotSetting", new Action<string>(value => hotExitData = value));
                SetPrivateField(form, "clearHotExitSnapshotSetting", new Action(() => hotExitCleared = true));
                Require((bool)InvokePrivate(form, "prepareHotExit") &&
                        HotExitSnapshot.Parse(hotExitData)?.Documents.Single().SessionDocumentIndex == 0,
                    "Hot Exit non serializza il buffer modificato.");

                SetPrivateField(form, "loadHotExitSnapshotSetting", new Func<string>(() => hotExitData));
                editor.Text = string.Empty;
                GetPrivateField<DocumentSession>(form, "documentSession").MarkSaved(settingsSavePath);
                InvokePrivate(form, "restoreHotExitBuffers");
                Require(editor.Text.Contains("ret 4;", StringComparison.Ordinal) && editor.SelectedText == "ret" &&
                        GetPrivateField<DocumentSession>(form, "documentSession").IsDirty && hotExitCleared,
                    "Hot Exit non ripristina contenuto, selezione e stato dirty oppure non consuma lo snapshot.");

                string recoveryData = null;
                bool recoveryCleared = false;
                editor.Text = "main() out int {\n\tret 5;\n}";
                int recoverySelection = editor.Text.IndexOf("ret", StringComparison.Ordinal);
                editor.Select(recoverySelection, 3);
                SetPrivateField(form, "saveCrashRecoverySetting", new Action<string>(value => recoveryData = value));
                SetPrivateField(form, "clearCrashRecoverySetting", new Action(() => recoveryCleared = true));
                InvokeHandler(form, "crashRecoveryTimer_Tick", form, EventArgs.Empty);
                CrashRecoverySnapshot recovery = CrashRecoverySnapshot.Parse(recoveryData);
                Require(recovery?.Documents.Single().Source.Contains("ret 5;", StringComparison.Ordinal) == true &&
                        recovery.Documents[0].SessionDocumentIndex == 0,
                    "Il journal di recovery non conserva il buffer sporco e la scheda di origine.");

                SetPrivateField(form, "loadCrashRecoverySetting", new Func<string>(() => recoveryData));
                SetPrivateField(form, "showRecoveryDialog", new Func<RecoveryForm, DialogResult>(dialog =>
                {
                    Require(dialog.StartPosition == FormStartPosition.CenterScreen && IsLight(dialog.BackColor) &&
                            FindControl<ListView>(dialog, "recoveryDocumentsListView").Items.Count == 1,
                        "Il popup Recovery non è chiaro, centrato o non elenca i buffer recuperabili.");
                    Require(FindControl<Label>(dialog, "recoveryMessageLabel").Text.Contains("non sovrascriverà", StringComparison.OrdinalIgnoreCase),
                        "Il popup Recovery non chiarisce il comportamento non distruttivo.");
                    return DialogResult.Yes;
                }));
                editor.Text = string.Empty;
                GetPrivateField<DocumentSession>(form, "documentSession").MarkSaved(settingsSavePath);
                Require((bool)InvokePrivate(form, "restoreCrashRecoveryBuffers") &&
                        editor.Text.Contains("ret 5;", StringComparison.Ordinal) && editor.SelectedText == "ret" &&
                        GetPrivateField<DocumentSession>(form, "documentSession").IsDirty && recoveryCleared &&
                        !File.ReadAllText(settingsSavePath).Contains("ret 5;", StringComparison.Ordinal),
                    "Recovery non ripristina il buffer come dirty oppure sovrascrive il file originale.");

                int externalDialogCount = 0;
                File.WriteAllText(settingsSavePath, "main() out int {\n\tret 6;\n}");
                SetPrivateField(form, "showExternalChangeDialog", new Func<ExternalChangeForm, DialogResult>(dialog =>
                {
                    externalDialogCount++;
                    Require(dialog.StartPosition == FormStartPosition.CenterScreen && IsLight(dialog.BackColor) &&
                            FindControl<RichTextBox>(dialog, "externalChangeBufferTextBox").Text.Contains("ret 5;", StringComparison.Ordinal) &&
                            FindControl<RichTextBox>(dialog, "externalChangeDiskTextBox").Text.Contains("ret 6;", StringComparison.Ordinal),
                        "Il confronto delle modifiche esterne non è chiaro, centrato o non mostra entrambe le versioni.");
                    return DialogResult.Yes;
                }));
                InvokeHandler(form, "externalChangeTimer_Tick", form, EventArgs.Empty);
                Require(externalDialogCount == 1 && editor.Text.Contains("ret 6;", StringComparison.Ordinal) &&
                        !GetPrivateField<DocumentSession>(form, "documentSession").IsDirty,
                    "Ricarica da disco non sostituisce il buffer o non ripristina lo stato pulito.");

                editor.Text = "main() out int {\n\tret 10;\n}";
                File.WriteAllText(settingsSavePath, "main() out int {\r\n\tret 10;\r\n}");
                int matchingDialogs = 0;
                SetPrivateField(form, "showExternalChangeDialog", new Func<ExternalChangeForm, DialogResult>(dialog =>
                {
                    matchingDialogs++;
                    return DialogResult.Cancel;
                }));
                InvokeHandler(form, "externalChangeTimer_Tick", form, EventArgs.Empty);
                Require(matchingDialogs == 0 && !GetPrivateField<DocumentSession>(form, "documentSession").IsDirty,
                    "Un file uguale al buffer salvo le terminazioni di riga genera un falso conflitto esterno.");

                editor.Text = "main() out int {\n\tret 7;\n}";
                File.WriteAllText(settingsSavePath, "main() out int {\n\tret 8;\n}");
                SetPrivateField(form, "showExternalChangeDialog", new Func<ExternalChangeForm, DialogResult>(dialog => DialogResult.No));
                InvokeHandler(form, "externalChangeTimer_Tick", form, EventArgs.Empty);
                Require(editor.Text.Contains("ret 7;", StringComparison.Ordinal) &&
                        File.ReadAllText(settingsSavePath).Contains("ret 8;", StringComparison.Ordinal) &&
                        GetPrivateField<DocumentSession>(form, "documentSession").IsDirty,
                    "Mantieni buffer sovrascrive il file esterno o perde lo stato dirty.");

                File.WriteAllText(settingsSavePath, "main() out int {\n\tret 9;\n}");
                int postponedDialogs = 0;
                SetPrivateField(form, "showExternalChangeDialog", new Func<ExternalChangeForm, DialogResult>(dialog =>
                {
                    postponedDialogs++;
                    return DialogResult.Cancel;
                }));
                InvokeHandler(form, "externalChangeTimer_Tick", form, EventArgs.Empty);
                InvokeHandler(form, "externalChangeTimer_Tick", form, EventArgs.Empty);
                Require(postponedDialogs == 1 && File.ReadAllText(settingsSavePath).Contains("ret 9;", StringComparison.Ordinal),
                    "Più tardi ripropone subito il popup oppure modifica il file esterno.");
                SetPrivateField(form, "showExternalChangeDialog", new Func<ExternalChangeForm, DialogResult>(dialog => DialogResult.Cancel));
                fakeNow = fakeNow.AddSeconds(2);
                InvokeHandler(form, "autoSaveTimer_Tick", form, EventArgs.Empty);
                Require(File.ReadAllText(settingsSavePath).Contains("ret 9;", StringComparison.Ordinal) &&
                        GetPrivateField<DocumentSession>(form, "documentSession").IsDirty,
                    "L'autosave sovrascrive una modifica esterna rimandata.");
                Require(!(bool)InvokePrivate(form, "saveDocument", false) &&
                        File.ReadAllText(settingsSavePath).Contains("ret 9;", StringComparison.Ordinal),
                    "Il controllo pre-salvataggio ignora una decisione rimandata e sovrascrive il file esterno.");

                SetPrivateField(form, "showExternalChangeDialog", new Func<ExternalChangeForm, DialogResult>(dialog => DialogResult.No));
                Require((bool)InvokePrivate(form, "saveDocument", false) &&
                        File.ReadAllText(settingsSavePath).Contains("ret 7;", StringComparison.Ordinal),
                    "Mantieni buffer non consente il successivo salvataggio consapevole.");
                File.Delete(settingsSavePath);
                SetPrivateField(form, "showExternalChangeDialog", new Func<ExternalChangeForm, DialogResult>(dialog =>
                {
                    Require(!FindControl<Button>(dialog, "externalChangeReloadButton").Enabled &&
                            FindControl<Label>(dialog, "externalChangeDiskLabel").Text.Contains("non disponibile", StringComparison.OrdinalIgnoreCase),
                        "Il popup non distingue un file eliminato da una normale modifica esterna.");
                    return DialogResult.No;
                }));
                InvokeHandler(form, "externalChangeTimer_Tick", form, EventArgs.Empty);
                Require(editor.Text.Contains("ret 7;", StringComparison.Ordinal) &&
                        GetPrivateField<DocumentSession>(form, "documentSession").IsDirty,
                    "La gestione di un file eliminato perde il buffer corrente.");
            }
            finally
            {
                if (File.Exists(settingsSavePath)) File.Delete(settingsSavePath);
            }
            GetPrivateField<DocumentSession>(form, "documentSession").SetUntitled();
            InvokePrivate(form, "clearBreakpoints");
            InvokePrivate(form, "updateDocumentUi");

            SetPrivateField(form, "showSettingsDialog", new Func<SettingsForm, DialogResult>(dialog =>
            {
                dialogCount++;
                Require(FindControl<Button>(dialog, "settingsDefaultsButton") != null,
                    "Il comando Predefiniti manca dalla finestra Impostazioni.");
                Require(!FindControl<TabControl>(dialog, "settingsTabs").TabPages
                        .Cast<TabPage>().Any(page => page.Name == "settingsLanguageTab"),
                    "La scheda Linguaggio non deve più comparire nelle Impostazioni.");
                dialog.ResetToDefaults();
                return DialogResult.OK;
            }));
            FindToolbarButton(form, "toolbarSettingsButton").PerformClick();
            settings = GetPrivateField<EditorSettings>(form, "editorSettings");
            Require(dialogCount == 2 && !settings.UseTabs && settings.IndentSize == 4 &&
                    !settings.AutoSaveEnabled && !settings.HotExitEnabled && settings.RestoreSessionEnabled &&
                    settings.CrashRecoveryEnabled && settings.HighlightCurrentLine &&
                    !settings.ShowWhitespace && settings.ShowIndentGuides && !settings.WordWrap &&
                    settings.Theme == EditorTheme.Dark && !IsLight(editor.BackColor),
                "Predefiniti o accesso alle Impostazioni dalla toolbar non ripristinano il profilo iniziale.");
            SetPrivateField(form, "saveEditorSettingsSetting", new Action<string>(value => { }));
            SetPrivateField(form, "saveShortcutSettingsSetting", new Action<string>(value => { }));
            Console.WriteLine("PASS preferenze, sessione, recovery e conflitti esterni non distruttivi");
        }

        private static void AssertProductUpdates(MainForm form)
        {
            SetPrivateField(form, "saveProductUpdateSettingsSetting",
                new Action<string>(value => { }));
            DdfProductVersionIdentity identity =
                DdfProductVersionCatalog.Current;
            DateTimeOffset checkedAt = new DateTimeOffset(2026, 10, 24,
                18, 0, 0, TimeSpan.Zero);
            DdfProductUpdateResult available =
                DdfProductUpdateResult.Available(
                    DdfProductUpdateComponentKind.Standalone,
                    identity.ProductId, identity.CurrentVersion, "0.12.10",
                    DdfProductUpdateChannel.Stable,
                    DdfProductUpdateCompatibility.Compatible, checkedAt,
                    checkedAt.AddHours(-1), "Release verificata di prova.",
                    new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.12.10/DDFLanguageEditor.zip"));
            var provider = new FixedProductUpdateProvider(available);
            SetPrivateField(form, "createProductUpdateProvider",
                new Func<IDdfProductUpdateProvider>(() => provider));
            SetPrivateField(form, "productUpdateProvider", null);
            FindMenuItem(form, "checkProductUpdatesMenuItem").PerformClick();
            PumpUntil(() => provider.CheckCount == 1 &&
                    GetPrivateField<DdfProductUpdateResult>(form,
                        "lastProductUpdateResult") != null,
                2000, "Il controllo aggiornamenti manuale non termina.");

            StatusStrip status = FindControl<StatusStrip>(form,
                "statusStripMain");
            var updateStatus = (ToolStripStatusLabel)status.Items[
                "statusUpdateLabel"];
            Require(updateStatus.Visible && updateStatus.IsLink &&
                    updateStatus.AccessibleName.Length > 0 &&
                    updateStatus.Text.Contains("0.12.10",
                        StringComparison.Ordinal),
                "La release verificata non produce una notifica non modale.");
            PumpUntil(() => form.OwnedForms.OfType<ProductUpdateForm>().Any(),
                1000, "Il comando Help non apre i dettagli aggiornamento.");
            ProductUpdateForm details = form.OwnedForms
                .OfType<ProductUpdateForm>().Single();
            Require(details.StartPosition == FormStartPosition.CenterScreen &&
                    details.AccessibleName.Length > 0 &&
                    details.Result.IsActionable &&
                    FindControl<Button>(details,
                        "productUpdateOpenReleaseButton").Enabled &&
                    FindControl<Button>(details,
                        "productUpdateOpenReleaseButton").AccessibleName.Length > 0 &&
                    FindControl<Button>(details,
                        "productUpdateDownloadButton").Enabled &&
                    FindControl<TextBox>(details,
                        "productUpdateDetailsTextBox").AccessibleName.Length > 0,
                "Il popup aggiornamenti non espone la release verificata.");
            Button openButton = FindControl<Button>(details,
                "productUpdateOpenReleaseButton");
            Button downloadButton = FindControl<Button>(details,
                "productUpdateDownloadButton");
            Button closeButton = FindControl<Button>(details,
                "productUpdateCloseButton");
            Rectangle clientBounds = details.RectangleToScreen(
                details.ClientRectangle);
            Rectangle openBounds = openButton.RectangleToScreen(
                openButton.ClientRectangle);
            Rectangle downloadBounds = downloadButton.RectangleToScreen(
                downloadButton.ClientRectangle);
            Rectangle closeBounds = closeButton.RectangleToScreen(
                closeButton.ClientRectangle);
            Require(downloadBounds.Bottom <= clientBounds.Bottom - 12 &&
                    downloadBounds.Right < openBounds.Left &&
                    openBounds.Bottom <= clientBounds.Bottom - 12 &&
                    closeBounds.Bottom <= clientBounds.Bottom - 12 &&
                    openBounds.Right < closeBounds.Left &&
                    closeBounds.Right <= clientBounds.Right - 12,
                "I pulsanti del popup sono tagliati o sovrapposti al bordo.");
            closeButton.PerformClick();
            PumpMessages(50);
            Require(details.IsDisposed || !details.Visible,
                "Chiudi non chiude il popup non modale.");

            Uri openedRelease = null;
            int downloadRequests = 0;
            using (var actionForm = new ProductUpdateForm(available,
                uri => openedRelease = uri,
                result =>
                {
                    downloadRequests++;
                    Require(ReferenceEquals(available, result),
                        "Scarica usa un risultato diverso da quello mostrato.");
                    return Task.CompletedTask;
                }))
            {
                actionForm.Show(form);
                PumpMessages(50);
                ProgressBar updateProgress = FindControl<ProgressBar>(
                    actionForm, "productUpdateProgressBar");
                Label updateHint = FindControl<Label>(
                    actionForm, "productUpdateActionHintLabel");
                Require(!updateProgress.Visible,
                    "L'avanzamento non deve apparire prima del consenso.");
                actionForm.BeginDownloadProgress();
                Require(updateProgress.Visible &&
                    updateProgress.Style == ProgressBarStyle.Marquee,
                    "Il download deve mostrare subito un'attività visibile.");
                actionForm.ReportDownloadProgress(
                    new DdfProductUpdateStagingProgress(5, 10));
                Require(updateProgress.Value == 500 &&
                    updateHint.Text.Contains("50%", StringComparison.Ordinal),
                    "La percentuale del download non segue i byte ricevuti.");
                actionForm.ShowDownloadVerification();
                actionForm.ReportDownloadProgress(
                    new DdfProductUpdateStagingProgress(10, 10));
                Require(updateProgress.Style == ProgressBarStyle.Marquee &&
                    updateHint.Text.Contains("Verifica", StringComparison.Ordinal),
                    "Un evento tardivo sovrascrive la verifica dell'archivio.");
                actionForm.EndDownloadProgress();
                Require(!updateProgress.Visible &&
                    updateHint.Text.Contains("Release verificata",
                        StringComparison.Ordinal),
                    "Il popup non ripristina lo stato dopo il download.");
                FindControl<Button>(actionForm,
                    "productUpdateDownloadButton").PerformClick();
                PumpMessages(50);
                Require(downloadRequests == 1,
                    "Scarica non richiede il flusso autorizzato.");
                FindControl<Button>(actionForm,
                    "productUpdateOpenReleaseButton").PerformClick();
                Require(openedRelease == available.VerifiedReleaseUri,
                    "Apri release non richiama la pagina verificata.");
                FindControl<Button>(actionForm,
                    "productUpdateCloseButton").PerformClick();
                Require(!actionForm.Visible,
                    "Chiudi non funziona dopo Apri release.");
            }

            using (var currentForm = new ProductUpdateForm(
                DdfProductUpdateResult.UpToDate(
                    DdfProductUpdateComponentKind.Standalone,
                    identity.ProductId, identity.CurrentVersion,
                    identity.CurrentVersion, DdfProductUpdateChannel.Beta,
                    checkedAt), uri => throw new InvalidOperationException(
                        "Non si deve aprire una release assente.")))
            {
                Require(!FindControl<Button>(currentForm,
                        "productUpdateOpenReleaseButton").Enabled &&
                    !FindControl<Button>(currentForm,
                        "productUpdateDownloadButton").Enabled &&
                    FindControl<Label>(currentForm,
                        "productUpdateActionHintLabel").Text.Contains(
                            "Nessuna nuova release", StringComparison.Ordinal),
                    "Il popup aggiornato non spiega perché Apri release è disabilitato.");
            }
            using (var blockedForm = new ProductUpdateForm(available,
                uri => { }))
            {
                Require(!FindControl<Button>(blockedForm,
                        "productUpdateDownloadButton").Enabled &&
                    FindControl<Label>(blockedForm,
                        "productUpdateActionHintLabel").Text.Contains(
                            "precedente", StringComparison.Ordinal),
                    "Un aggiornamento precedente non blocca il nuovo download.");
            }

            DateTimeOffset planNow = DateTimeOffset.UtcNow.AddMinutes(-1);
            string planRoot = Path.Combine(Path.GetTempPath(),
                "ddf-update-consent-smoke-" + Guid.NewGuid().ToString("N"));
            var consentPlan = new DdfProductUpdateInstallationPlan(
                "portable-consent", identity.ProductId,
                identity.CurrentVersion, "0.13.6-beta.4", "win-x64", "x64",
                new string('a', 64), "official-2026",
                new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.4/editor.zip"),
                100, new string('b', 64), Path.Combine(planRoot, "target"),
                Path.Combine(planRoot, "staging"), Path.Combine(planRoot, "backup"),
                "DDFLanguageEditor.exe", 1234, planNow.AddMinutes(-1),
                new string('c', 64), false,
                new[] { new DdfProductUpdateInstallationFile(
                    "DDFLanguageEditor.exe", 10, new string('d', 64)) }, planNow);
            Assembly editorAssembly = typeof(MainForm).Assembly;
            Type consentType = editorAssembly.GetType(
                "DDF___Program_Language_Editor.ProductUpdateInstallationConsentForm",
                true);
            Type stageType = editorAssembly.GetType(
                "DDF___Program_Language_Editor.ProductUpdateConsentStage", true);
            using (var installConsent = (Form)Activator.CreateInstance(consentType,
                consentPlan, Enum.Parse(stageType, "Install")))
            {
                bool inspected = false;
                installConsent.Shown += (sender, args) =>
                {
                    inspected = installConsent.StartPosition ==
                            FormStartPosition.CenterScreen &&
                        FindControl<Button>(installConsent,
                            "productUpdateInstallConfirmButton").Text == "Installa" &&
                        FindControl<Button>(installConsent,
                            "productUpdateConsentCancelButton").Text == "Annulla" &&
                        !enumerateControls(installConsent).OfType<CheckBox>()
                            .Any(item => item.Visible);
                    installConsent.BeginInvoke(new Action(() =>
                        FindControl<Button>(installConsent,
                            "productUpdateConsentCancelButton").PerformClick()));
                };
                Require(installConsent.ShowDialog(form) == DialogResult.Cancel &&
                        inspected,
                    "Il secondo consenso non è distinto o Annulla non funziona.");
            }

            ProductUpdateSettings parsed = ProductUpdateSettings.Parse(
                new ProductUpdateSettings(true,
                    DdfProductUpdateChannel.Beta, 5, checkedAt).Serialize());
            Require(parsed.AutomaticCheckEnabled && parsed.CadenceDays == 5 &&
                    parsed.Channel == DdfProductUpdateChannel.Beta &&
                    parsed.LastCheckUtc == checkedAt,
                "Le impostazioni aggiornamenti non sono stabili.");

            using (var embedded = new MainForm(true))
            {
                ToolStripMenuItem embeddedCheck = FindMenuItem(embedded,
                    "checkProductUpdatesMenuItem");
                Require(!embeddedCheck.Available,
                    "L'embedded espone il controllo aggiornamenti standalone.");
            }
            Console.WriteLine("PASS controllo aggiornamenti standalone asincrono, persistente e non modale");
        }

        private static void AssertTextBoxEditingSupport(MainForm form, RichTextBox editor)
        {
            FindMenuItem(form, "workspaceSearchMenuItem").PerformClick();
            TextBox search = FindControl<TextBox>(form, "workspaceSearchTextBox");
            editor.Text = "editor-sentinel";
            search.Text = "alpha beta";
            search.Select(0, 5);
            search.Focus();
            PumpMessages(40);
            Require(search.Focused, "La casella di ricerca workspace non riceve il focus.");

            SetClipboardTextWithRetry("clipboard-before-copy");
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.C) && ClipboardTextEqualsWithRetry("alpha"),
                "Ctrl+C nella ricerca workspace viene ancora inviato all'editor.");
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.X) && search.Text == " beta",
                "Ctrl+X non taglia la selezione nella casella di testo focalizzata.");
            SetClipboardTextWithRetry("gamma");
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.V) && search.Text == "gamma beta",
                "Ctrl+V non incolla nella casella di testo focalizzata.");
            Require(InvokeProcessCmdKey(form, Keys.Control | Keys.A) && search.SelectionLength == search.TextLength,
                "Ctrl+A non seleziona il contenuto della casella focalizzata.");
            Require(editor.Text == "editor-sentinel",
                "Le scorciatoie di una casella di testo hanno modificato il sorgente dell'editor.");

            assertTextControlsConfigured(form, true);
            Assembly assembly = typeof(MainForm).Assembly;
            Type findType = assembly.GetType("DDF___Program_Language_Editor.FindReplaceForm", true);
            Type renameType = assembly.GetType("DDF___Program_Language_Editor.RenameSymbolForm", true);
            Type inputType = assembly.GetType("DDF___Program_Language_Editor.RuntimeInputForm", true);
            Type aboutType = assembly.GetType("DDF___Program_Language_Editor.AboutForm", true);
            using (var find = (Form)Activator.CreateInstance(findType, editor)) assertTextControlsConfigured(find, false);
            using (var rename = (Form)Activator.CreateInstance(renameType, "value")) assertTextControlsConfigured(rename, false);
            using (var input = (Form)Activator.CreateInstance(inputType)) assertTextControlsConfigured(input, false);
            using (var about = (Form)Activator.CreateInstance(aboutType)) assertTextControlsConfigured(about, false);

            var emptySources = new Dictionary<NavigationMode, IReadOnlyList<DdfWorkspaceNavigationLocation>>
            {
                { NavigationMode.File, Array.Empty<DdfWorkspaceNavigationLocation>() },
                { NavigationMode.Symbol, Array.Empty<DdfWorkspaceNavigationLocation>() },
                { NavigationMode.Reference, Array.Empty<DdfWorkspaceNavigationLocation>() },
                { NavigationMode.Line, Array.Empty<DdfWorkspaceNavigationLocation>() }
            };
            using (var navigation = new NavigationForm(emptySources, NavigationMode.File, 1, 1, 1, string.Empty))
                assertTextControlsConfigured(navigation, false);
            using (var commandPalette = new CommandPaletteForm(Array.Empty<CommandPaletteCommand>()))
                assertTextControlsConfigured(commandPalette, false);
            using (var settings = new SettingsForm(EditorSettings.Default))
                assertTextControlsConfigured(settings, false);
            editor.Focus();
            PumpMessages(40);
            Require(editor.Focused, "Lo smoke delle caselle di testo non ripristina il focus dell'editor.");
            Console.WriteLine("PASS taglia/copia/incolla/seleziona tutto in tutte le caselle di testo");
        }

        private static void assertTextControlsConfigured(Form form, bool mainForm)
        {
            foreach (TextBoxBase textBox in enumerateControls(form).OfType<TextBoxBase>())
            {
                if (textBox.Name == "richTextBoxLineNumbers")
                {
                    Require(!textBox.ShortcutsEnabled, "Il gutter ha riacquistato scorciatoie di modifica.");
                    continue;
                }
                Require(textBox.ShortcutsEnabled,
                    "Scorciatoie standard disabilitate nella casella: " + textBox.Name + ".");
                Require(textBox.ContextMenuStrip != null,
                    "Menu contestuale di modifica mancante nella casella: " + textBox.Name + ".");
                if (mainForm && textBox.Name == "richTextBoxMainEditor") continue;
                Require(textBox.ContextMenuStrip.Items.OfType<ToolStripMenuItem>()
                        .Any(item => item.Name == "standardTextCopyItem"),
                    "Il menu contestuale non espone Copia nella casella: " + textBox.Name + ".");
            }
        }

        private static IEnumerable<Control> enumerateControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control nested in enumerateControls(child)) yield return nested;
            }
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
            ToolStripButton button = FindToolbars(form).SelectMany(toolbar => toolbar.Items.OfType<ToolStripButton>())
                .FirstOrDefault(item => item.Name == name);
            if (button == null) throw new InvalidOperationException("Pulsante toolbar non trovato: " + name);
            return button;
        }

        private static IEnumerable<ToolStrip> FindToolbars(Form form) =>
            form.Controls.Find("toolStripPanelMain", true).OfType<ToolStripPanel>()
                .SelectMany(panel => panel.Controls.OfType<ToolStrip>());

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

        private static T GetPrivateValue<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Campo privato non trovato: " + fieldName);
            return (T)field.GetValue(target);
        }

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null) throw new InvalidOperationException("Proprietà non trovata: " + propertyName);
            return (T)property.GetValue(target);
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

        private static bool InvokeFilteredKey(MainForm form, Control control, Keys key)
        {
            const int wmKeyDown = 0x0100;
            var message = Message.Create(control.Handle, wmKeyDown, new IntPtr((int)key), IntPtr.Zero);
            return form.PreFilterMessage(ref message);
        }

        private static object InvokePrivate(MainForm form, string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(MainForm).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Metodo non trovato: " + methodName);
            return method.Invoke(form, arguments);
        }

        private static object InvokePrivateInstance(object target, string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException(
                "Metodo non trovato: " + methodName);
            return method.Invoke(target, arguments);
        }

        private static T GetControlProperty<T>(Control control, string propertyName)
        {
            PropertyInfo property = control.GetType().GetProperty(
                propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null) throw new InvalidOperationException("Proprietà controllo non trovata: " + propertyName);
            return (T)property.GetValue(control);
        }

        private static void InvokeControlMouseDown(Control control, int y)
        {
            MethodInfo method = control.GetType().GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("OnMouseDown non trovato sul controllo.");
            method.Invoke(control, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 2, Math.Max(0, y), 0) });
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

        private static Point GetEditorScrollPosition(RichTextBox editor)
        {
            Point position = Point.Empty;
            SendMessage(editor.Handle, EmGetScrollPos, IntPtr.Zero, ref position);
            return position;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, ref Point lParam);

        [DllImport("user32.dll")]
        private static extern int GetGuiResources(IntPtr process, int flags);

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

        private static IEnumerable<TreeNode> FlattenTree(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                yield return node;
                foreach (TreeNode child in FlattenTree(node.Nodes)) yield return child;
            }
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

        private static void PumpUntil(Func<bool> condition, int timeoutMilliseconds, string failureMessage)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!condition() && stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
            Require(condition(), failureMessage);
        }

        private sealed class CountingPersistenceProvider : IDdfEditorPersistenceProvider
        {
            public int SaveCount { get; private set; }
            public int LoadCount { get; private set; }
            public DdfEditorPersistedState State { get; private set; }

            public void Save(DdfEditorPersistedState state)
            {
                State = state ?? throw new ArgumentNullException(nameof(state));
                SaveCount++;
            }

            public DdfEditorPersistedState Load()
            {
                LoadCount++;
                return State;
            }
        }

        private sealed class FixedProductUpdateProvider :
            IDdfProductUpdateProvider
        {
            private readonly DdfProductUpdateResult result;

            public FixedProductUpdateProvider(DdfProductUpdateResult result)
            {
                this.result = result;
            }

            public int CheckCount { get; private set; }

            public Task<DdfProductUpdateResult> CheckAsync(
                DdfProductUpdateRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckCount++;
                return Task.FromResult(result);
            }
        }

        private sealed class LegacyExecutionService : IDdfEditorExecutionService
        {
            public DdfEditorExecutionState State => DdfEditorExecutionState.Idle;
            public bool IsRunning => false;
            public long Generation => 0;
            public DdfEditorExecutionCompletion LastCompletion => null;
            public event EventHandler<DdfEditorExecutionStateChangedEventArgs>
                StateChanged { add { } remove { } }
            public event EventHandler<DdfEditorExecutionOutputEventArgs>
                OutputReceived { add { } remove { } }
            public event EventHandler<DdfEditorExecutionCompletedEventArgs>
                ExecutionCompleted { add { } remove { } }
            public Task<DdfEditorExecutionCompletion> RunAsync(
                DdfEditorExecutionRequest request = null) =>
                throw new NotSupportedException();
            public Task<DdfEditorExecutionCompletion> InvokeAsync(
                DdfEditorProcedureRequest request) =>
                throw new NotSupportedException();
            public bool Stop() => false;
            public void Dispose() { }
        }

        private sealed class MigratingEditorPersistenceProvider :
            IDdfEditorPersistenceProvider,
            IDdfEditorPersistenceMigrationProvider
        {
            public MigratingEditorPersistenceProvider(
                DdfEditorPersistedState state)
            {
                State = state;
            }

            public DdfEditorPersistedState State { get; private set; }
            public int MigrationCount { get; private set; }
            public void Save(DdfEditorPersistedState state) => State = state;
            public DdfEditorPersistedState Load() => State;

            public DdfEditorPersistedState Migrate(
                DdfEditorPersistedState snapshot,
                string targetSchemaVersion)
            {
                MigrationCount++;
                return new DdfEditorPersistedState(targetSchemaVersion,
                    snapshot.Documents, snapshot.ActiveDocumentIndex,
                    snapshot.Preferences, snapshot.Debugging);
            }
        }

        private sealed class FakeCompiledBuildService :
            IDdfEditorCompiledBuildService
        {
            public bool HasRunnableBuild => true;
            public string LastArtifactDirectory { get; private set; } =
                string.Empty;
            public string LastPublishedDirectory { get; private set; } =
                string.Empty;
            public string LastSuccessfulArtifactDirectory { get; private set; } =
                string.Empty;
            public List<DdfEditorBuildConfiguration> BuildConfigurations
                { get; } = new List<DdfEditorBuildConfiguration>();
            public int LastSourceCount { get; private set; }
            public bool FailNextBuild { get; set; }
            public int BuildCount { get; private set; }
            public int PublishCount { get; private set; }
            public int RunCount { get; private set; }

            public DdfEditorCompiledBuildResult Build(
                DdfEditorCompiledBuildRequest request)
            {
                BuildCount++;
                BuildConfigurations.Add(request.Configuration);
                LastSourceCount = request.Preparation.Artifact?.Sources.Count ??
                    0;
                LastArtifactDirectory = Path.Combine(request.OutputRoot,
                    "artifacts");
                if (FailNextBuild)
                {
                    FailNextBuild = false;
                    string sourcePath = request.Preparation.Artifact?.Sources
                        .FirstOrDefault()?.RelativePath ?? "main.ddf";
                    return new DdfEditorCompiledBuildResult(
                        request.OutputRoot, null, false, new[]
                        {
                            new DdfEditorCompiledBuildIssue("DDFC999",
                                "Errore compilato simulato.", sourcePath, 1, 1)
                        });
                }
                Directory.CreateDirectory(LastArtifactDirectory);
                LastSuccessfulArtifactDirectory = LastArtifactDirectory;
                return new DdfEditorCompiledBuildResult(request.OutputRoot,
                    LastArtifactDirectory, false, null);
            }

            public Task<DdfEditorCompiledRunResult> RunLastBuildAsync(
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                RunCount++;
                return Task.FromResult(new DdfEditorCompiledRunResult(
                    LastArtifactDirectory, "42", 17, true,
                    new[] { "compiled-output" }, null));
            }

            public DdfEditorCompiledPublishResult PublishLastBuild(
                DdfEditorCompiledPublishRequest request)
            {
                string executable = Path.Combine(request.Destination,
                    request.ExecutableName + ".exe");
                Directory.CreateDirectory(request.Destination);
                LastPublishedDirectory = request.Destination;
                var result = new DdfEditorCompiledPublishResult(
                    request.Destination, executable,
                    new[] { executable }, null);
                PublishCount++;
                return result;
            }
        }

        private sealed class CountingProjectContainer : IDdfEditorProjectContainer
        {
            public int SaveCount { get; private set; }
            public int LoadCount { get; private set; }
            public DdfEditorProjectState Project { get; private set; }

            public void Save(DdfEditorProjectState project)
            {
                Project = project ?? throw new ArgumentNullException(nameof(project));
                SaveCount++;
            }

            public DdfEditorProjectState Load()
            {
                LoadCount++;
                return Project;
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
