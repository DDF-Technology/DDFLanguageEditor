using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDF___Program_Language_Editor;
#if UPDATED_HOST
using DDFLanguageEditor.Core;
#endif

namespace DDFLanguageEditor.SdkCompatibilityHost
{
    internal static class Program
    {
        private const string Script =
            "main() out int\r\n" +
            "{\r\n" +
            "    int value << 40;\r\n\r\n" +
            "    value << value + 2;\r\n\r\n" +
            "    ret value;\r\n" +
            "}\r\n";

        [STAThread]
        private static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : string.Empty;
            string expectedContract = args.Length > 1 ? args[1] : string.Empty;
            try
            {
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                using var editor = new DdfEditorView();
                int changed = 0;
                editor.SourceTextChanged += (_, _) => changed++;
                editor.SourceText = Script;
                editor.SetSelection(4, 3);

                Require(NormalizeLineEndings(editor.SourceText) ==
                    NormalizeLineEndings(Script), "round-trip del buffer non riuscito");
                Require(editor.SelectionStart == 4 && editor.SelectionLength == 3,
                    "selezione del contratto base non rispettata");
                Require(changed > 0, "evento SourceTextChanged non ricevuto");
                Require(editor.AnalyzeDocument() != null,
                    "analisi del documento non disponibile");
                Require(editor.ContractVersion == expectedContract,
                    "contratto caricato " + editor.ContractVersion +
                    ", atteso " + expectedContract);

#if UPDATED_HOST
                Require(mode == "updated", "build aggiornata avviata nel modo errato");
                var capabilityProvider = editor as IDdfEditorCapabilityProvider;
                Require(capabilityProvider != null,
                    "provider capability 0.42 assente");
                Require(capabilityProvider.KnownCapabilities.Count == 5 &&
                        capabilityProvider.KnownCapabilities.Where(item =>
                            item.Id != DdfEditorCapabilityIds.ProductUpdates).All(item =>
                            capabilityProvider.InspectCapability(item.Id).IsAvailable) &&
                        !capabilityProvider.InspectCapability(
                            DdfEditorCapabilityIds.ProductUpdates).IsAvailable,
                    "catalogo capability SDK 0.42 o default deny aggiornamenti non valido");
                Require(editor.ExecutionService is IDdfEditorAsyncLifecycleService &&
                        editor.ExecutionService is IDdfEditorCooperativeExecutionService &&
                        editor.DebugService is IDdfEditorAsyncLifecycleService &&
                        editor.DebugService is IDdfEditorCooperativeExecutionService,
                    "capability lifecycle/cooperativa 0.41 assente su Run o Debug");
                var lifecycle = (IDdfEditorAsyncLifecycleService)editor.ExecutionService;
                DdfEditorLifecycleResult stop = lifecycle.StopAsync(
                    TimeSpan.FromSeconds(2), CancellationToken.None).GetAwaiter().GetResult();
                Require(stop.Outcome == DdfEditorLifecycleOutcome.AlreadyIdle,
                    "StopAsync inattivo non deterministico");

                DdfEditorCapabilityResult persistenceCapability =
                    capabilityProvider.InspectCapability(
                        DdfEditorCapabilityIds.ScriptStatePersistence);
                Require(persistenceCapability.TryGetService<
                        IDdfEditorScriptStatePersistenceService>(out var stateService),
                    "capability persistenza state non risolvibile in modo tipizzato");
                var stateProvider =
                    new DdfInMemoryEditorScriptStatePersistenceProvider();
                stateService.AttachScriptStatePersistenceProvider(stateProvider);
                stateService.SavePersistedScriptState();
                Require(stateProvider.Load() != null &&
                        stateService.DetachScriptStatePersistenceProvider(),
                    "capability state persistence non operativa");

                DdfProductComponentVersion sdkComponent =
                    DdfProductVersionCatalog.Current.EmbeddedComponents.Single(
                        item => item.Id == "winforms-sdk");
                var updateProvider = new CompatibilityUpdateProvider();
                editor.AttachProductUpdateService(
                    DdfEditorProductUpdateService.ForEmbeddedComponent(
                        updateProvider, sdkComponent.Id));
                DdfEditorCapabilityResult updateCapability =
                    capabilityProvider.InspectCapability(
                        DdfEditorCapabilityIds.ProductUpdates);
                Require(updateCapability.TryGetService<
                            IDdfEditorProductUpdateService>(out var updateService) &&
                        updateProvider.CheckCount == 0,
                    "capability aggiornamenti 0.42 non collegabile o non opt-in");
                Require(updateService.CheckAsync(DdfProductUpdateChannel.Beta)
                            .GetAwaiter().GetResult().State ==
                            DdfProductUpdateState.UpToDate &&
                        updateProvider.CheckCount == 1 &&
                        editor.DetachProductUpdateService(),
                    "verifica aggiornamenti embedded non esplicita o non revocabile");

                AssertPersistenceCompatibility();
#else
                Require(mode == "legacy" || mode == "source",
                    "build legacy/sorgente avviata nel modo errato");
#endif

                Console.WriteLine("DDF SDK COMPATIBILITY OK: " + mode +
                    " -> contract " + editor.ContractVersion);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static string NormalizeLineEndings(string value) =>
            (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

#if UPDATED_HOST
        private sealed class CompatibilityUpdateProvider :
            IDdfProductUpdateProvider
        {
            public int CheckCount { get; private set; }

            public Task<DdfProductUpdateResult> CheckAsync(
                DdfProductUpdateRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckCount++;
                return Task.FromResult(DdfProductUpdateResult.UpToDate(
                    request.ComponentKind, request.ComponentId,
                    request.CurrentVersion, request.CurrentVersion,
                    request.Channel, DateTimeOffset.UtcNow));
            }
        }

        private static void AssertPersistenceCompatibility()
        {
            var preferences = new DdfEditorPersistedPreferences(
                new DdfEditorHostOptions(),
                Array.Empty<KeyValuePair<string, Keys>>(), false);
            var legacy = new DdfEditorPersistedState("legacy", new[]
            {
                new DdfEditorPersistedDocument("legacy.ddf", string.Empty,
                    "main() out int { ret 17; }", true)
            }, 0, preferences);

            using (var rejecting = new DdfEditorView())
            {
                rejecting.SourceText = Script;
                var provider = new DdfInMemoryEditorPersistenceProvider();
                provider.Save(legacy);
                rejecting.AttachPersistenceProvider(provider);
                bool rejected = false;
                try { rejecting.RestoreState(); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && ReferenceEquals(provider.State, legacy) &&
                        NormalizeLineEndings(rejecting.SourceText) ==
                        NormalizeLineEndings(Script),
                    "snapshot legacy applicato o modificato senza migratore");
            }

            using (var migrating = new DdfEditorView())
            {
                var provider = new CompatibilityMigrationProvider(legacy);
                migrating.AttachPersistenceProvider(provider);
                Require(migrating.RestoreState() && provider.MigrationCount == 1 &&
                        migrating.SourceText.Contains("ret 17",
                            StringComparison.Ordinal) &&
                        ReferenceEquals(provider.State, legacy),
                    "snapshot legacy non migrato atomicamente");

                migrating.SourceText = Script;
                migrating.SaveState();
                Require(provider.State.SchemaVersion ==
                    DdfEditorPersistedState.CurrentSchemaVersion,
                    "snapshot corrente salvato senza schema autorevole");
                migrating.SourceText = "main() out int { ret 0; }";
                Require(migrating.RestoreState() &&
                        NormalizeLineEndings(migrating.SourceText) ==
                        NormalizeLineEndings(Script),
                    "round-trip dello snapshot corrente non riuscito");
            }
        }

        private sealed class CompatibilityMigrationProvider :
            IDdfEditorPersistenceProvider,
            IDdfEditorPersistenceMigrationProvider
        {
            public CompatibilityMigrationProvider(DdfEditorPersistedState state) =>
                State = state;

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
#endif
    }
}
