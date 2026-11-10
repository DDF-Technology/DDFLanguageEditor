using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    public partial class MainForm
    {
        private ProductUpdateSettings productUpdateSettings;
        private ToolStripMenuItem checkProductUpdatesMenuItem;
        private ToolStripStatusLabel statusUpdateLabel;
        private System.Windows.Forms.Timer productUpdateDelayTimer;
        private CancellationTokenSource productUpdateCancellation;
        private IDdfProductUpdateProvider productUpdateProvider;
        private Func<IDdfProductUpdateProvider> createProductUpdateProvider;
        private Func<DateTimeOffset> productUpdateClock;
        private Action<Uri> openProductUpdateUri;
        private Action<string> saveProductUpdateSettingsSetting;
        private DdfProductUpdateResult lastProductUpdateResult;
        private DdfProductUpdateStartupRecoveryStatus productUpdateRecoveryStatus;
        private ProductUpdateForm productUpdateForm;
        private bool productUpdateCheckRunning;
        private bool productUpdateDownloadRunning;
        private CancellationTokenSource productUpdateDownloadCancellation;
        private bool productUpdateInstallCloseApproved;

        private void initializeProductUpdates()
        {
            productUpdateSettings = ProductUpdateSettings.Parse(
                embeddedHostMode ? string.Empty :
                AppSettingsStore.LoadProductUpdateSettings());
            productUpdateClock = () => DateTimeOffset.UtcNow;
            createProductUpdateProvider = () => new DdfGitHubReleaseProvider(
                new DdfSignedGitHubReleaseEvaluator(
                    DdfOfficialReleaseTrustStore.Create()));
            saveProductUpdateSettingsSetting =
                AppSettingsStore.SaveProductUpdateSettings;
            openProductUpdateUri = uri => Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });

            checkProductUpdatesMenuItem = new ToolStripMenuItem(
                "Controlla &aggiornamenti...")
            {
                Name = "checkProductUpdatesMenuItem",
                AccessibleName = "Controlla aggiornamenti del prodotto",
                Available = !embeddedHostMode
            };
            checkProductUpdatesMenuItem.Click +=
                checkProductUpdatesMenuItem_Click;
            int separatorIndex = helpMenuItem.DropDownItems.IndexOf(helpSeparator);
            helpMenuItem.DropDownItems.Insert(Math.Max(0, separatorIndex),
                checkProductUpdatesMenuItem);

            statusUpdateLabel = new ToolStripStatusLabel
            {
                Name = "statusUpdateLabel",
                AccessibleName = "Aggiornamento del prodotto disponibile",
                IsLink = true,
                Visible = false,
                Available = !embeddedHostMode,
                ToolTipText = "Apri i dettagli dell'aggiornamento"
            };
            statusUpdateLabel.Click += (sender, args) =>
            {
                if (productUpdateRecoveryStatus?.HasState == true)
                {
                    showProductUpdateRecoveryDialog();
                    return;
                }
                showProductUpdateResult(lastProductUpdateResult);
            };
            int positionIndex = statusStripMain.Items.IndexOf(statusPositionLabel);
            statusStripMain.Items.Insert(Math.Max(0, positionIndex),
                statusUpdateLabel);

            productUpdateDelayTimer = new System.Windows.Forms.Timer
            {
                Interval = 5000
            };
            productUpdateDelayTimer.Tick += async (sender, args) =>
            {
                productUpdateDelayTimer.Stop();
                await checkProductUpdatesAsync(false);
            };
        }

        private void scheduleAutomaticProductUpdateCheck()
        {
            productUpdateDelayTimer.Stop();
            if (embeddedHostMode || !productUpdateSettings.AutomaticCheckEnabled ||
                productUpdateCheckRunning || IsDisposed) return;
            DateTimeOffset now = productUpdateClock();
            if (productUpdateSettings.LastCheckUtc.HasValue &&
                now - productUpdateSettings.LastCheckUtc.Value <
                    TimeSpan.FromDays(productUpdateSettings.CadenceDays))
                return;
            productUpdateDelayTimer.Start();
        }

        private async void checkProductUpdatesMenuItem_Click(object sender,
            EventArgs e) => await checkProductUpdatesAsync(true);

        private async Task checkProductUpdatesAsync(bool manual)
        {
            if (embeddedHostMode || productUpdateCheckRunning) return;
            productUpdateDelayTimer.Stop();
            productUpdateCheckRunning = true;
            checkProductUpdatesMenuItem.Enabled = false;
            checkProductUpdatesMenuItem.Text = "Controllo aggiornamenti...";
            try
            {
                productUpdateCancellation?.Cancel();
                productUpdateCancellation?.Dispose();
                productUpdateCancellation = new CancellationTokenSource();
                productUpdateProvider ??= createProductUpdateProvider?.Invoke();
                DdfProductUpdateResult result = productUpdateProvider == null
                    ? unavailableProductUpdateResult()
                    : await productUpdateProvider.CheckAsync(
                        DdfProductUpdateRequest.ForStandalone(
                            productUpdateSettings.Channel),
                        productUpdateCancellation.Token);
                if (IsDisposed || Disposing) return;
                lastProductUpdateResult = result;
                productUpdateSettings = productUpdateSettings.WithLastCheck(
                    result.CheckedAtUtc);
                persistProductUpdateSettings();
                presentProductUpdateResult(result, manual);
            }
            catch (OperationCanceledException)
            {
                if (manual && !IsDisposed)
                    appendOutput("Controllo aggiornamenti annullato.");
            }
            catch (Exception exception)
            {
                DdfProductUpdateResult result = DdfProductUpdateResult.Unknown(
                    DdfProductUpdateComponentKind.Standalone,
                    DdfProductVersionCatalog.Current.ProductId,
                    DdfProductVersionCatalog.Current.CurrentVersion,
                    productUpdateSettings.Channel, productUpdateClock(),
                    "Controllo aggiornamenti non riuscito: " + exception.Message);
                lastProductUpdateResult = result;
                productUpdateSettings = productUpdateSettings.WithLastCheck(
                    result.CheckedAtUtc);
                persistProductUpdateSettings();
                presentProductUpdateResult(result, manual);
            }
            finally
            {
                productUpdateCheckRunning = false;
                if (!IsDisposed)
                {
                    checkProductUpdatesMenuItem.Enabled = true;
                    checkProductUpdatesMenuItem.Text =
                        "Controlla &aggiornamenti...";
                }
            }
        }

        private DdfProductUpdateResult unavailableProductUpdateResult() =>
            DdfProductUpdateResult.Unknown(
                DdfProductUpdateComponentKind.Standalone,
                DdfProductVersionCatalog.Current.ProductId,
                DdfProductVersionCatalog.Current.CurrentVersion,
                productUpdateSettings.Channel, productUpdateClock(),
                "Il provider firmato degli aggiornamenti non è disponibile.");

        private void presentProductUpdateResult(DdfProductUpdateResult result,
            bool manual)
        {
            if (productUpdateRecoveryStatus?.HasState == true)
                presentProductUpdateRecoveryStatus();
            else
            {
                statusUpdateLabel.Visible = result.IsActionable;
                statusUpdateLabel.Text = result.IsActionable
                    ? "Aggiornamento " + result.AvailableVersion + " disponibile"
                    : string.Empty;
                statusUpdateLabel.ToolTipText = result.IsActionable
                    ? "Apri i dettagli della release verificata"
                    : result.StatusDetail;
            }

            if (result.State == DdfProductUpdateState.Unknown)
                appendOutput("Aggiornamenti: " + result.StatusDetail);
            else if (result.State == DdfProductUpdateState.Incompatible)
                appendOutput("Aggiornamenti: release " + result.AvailableVersion +
                    " non compatibile con questa installazione.");
            if (manual) showProductUpdateResult(result);
        }

        private void inspectPendingProductUpdateRecovery()
        {
            if (embeddedHostMode) return;
            string stateDirectory = AppSettingsStore.GetProductUpdateStateDirectory();
            productUpdateRecoveryStatus = DdfProductUpdateStartupRecovery.Inspect(
                Path.Combine(stateDirectory, "plan.json"),
                Path.Combine(stateDirectory, "journal.json"));
            if (!productUpdateRecoveryStatus.HasState) return;

            appendOutput("Aggiornamenti: stato precedente da esaminare. " +
                productUpdateRecoveryStatus.Detail + " Azione prevista: " +
                describeProductUpdateRecoveryAction(
                    productUpdateRecoveryStatus.Action) + ".");
            productUpdateForm?.Close();
            presentProductUpdateRecoveryStatus();
        }

        private void presentProductUpdateRecoveryStatus()
        {
            statusUpdateLabel.Visible = true;
            statusUpdateLabel.Text = "Aggiornamento da recuperare";
            statusUpdateLabel.ToolTipText =
                "Apri lo stato dell'aggiornamento precedente";
            statusUpdateLabel.AccessibleName =
                "Aggiornamento incompleto che richiede attenzione";
        }

        private static string describeProductUpdateRecoveryAction(
            DdfProductUpdateRecoveryAction action)
        {
            switch (action)
            {
                case DdfProductUpdateRecoveryAction.DiscardStaging:
                    return "rimozione dello staging incompleto";
                case DdfProductUpdateRecoveryAction.RestoreBackup:
                    return "ripristino del backup";
                case DdfProductUpdateRecoveryAction.VerifyOrRollback:
                    return "verifica o rollback";
                case DdfProductUpdateRecoveryAction.CleanupDeferred:
                    return "pulizia differita";
                case DdfProductUpdateRecoveryAction.ManualIntervention:
                    return "intervento manuale";
                default:
                    return "nessuna";
            }
        }

        private void showProductUpdateRecoveryDialog()
        {
            if (embeddedHostMode) return;
            string state = AppSettingsStore.GetProductUpdateStateDirectory();
            string plan = Path.Combine(state, "plan.json");
            string journal = Path.Combine(state, "journal.json");
            productUpdateRecoveryStatus = DdfProductUpdateStartupRecovery.Inspect(
                plan, journal);
            if (!productUpdateRecoveryStatus.HasState)
            {
                if (lastProductUpdateResult != null)
                    presentProductUpdateResult(lastProductUpdateResult, false);
                else statusUpdateLabel.Visible = false;
                return;
            }
            if (DdfProductUpdateRetryCleanup.CanFinalizeCompleted(
                    productUpdateRecoveryStatus))
            {
                DialogResult finish = MessageBox.Show(this,
                    "La nuova installazione risulta completata. Dopo un'ultima " +
                    "verifica dei suoi file, eliminare il backup della versione " +
                    "precedente e archiviare lo stato dell'aggiornamento?\n\n" +
                    "Questa operazione elimina definitivamente la copia " +
                    "precedente; non sarà più disponibile per il rollback.",
                    "Finalizza aggiornamento completato",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (finish != DialogResult.Yes) return;
                try
                {
                    DdfProductUpdateRetryCleanup.FinalizeCompleted(state,
                        Environment.ProcessPath);
                    productUpdateRecoveryStatus =
                        DdfProductUpdateStartupRecovery.Inspect(plan, journal);
                    appendOutput("Aggiornamenti: backup precedente eliminato " +
                        "dopo verifica della nuova installazione.");
                    if (lastProductUpdateResult != null)
                        presentProductUpdateResult(lastProductUpdateResult, false);
                    else statusUpdateLabel.Visible = false;
                }
                catch (Exception exception) when (exception is IOException ||
                    exception is InvalidDataException ||
                    exception is InvalidOperationException ||
                    exception is UnauthorizedAccessException)
                {
                    productUpdateRecoveryStatus =
                        DdfProductUpdateStartupRecovery.Inspect(plan, journal);
                    presentProductUpdateRecoveryStatus();
                    MessageBox.Show(this, exception.Message,
                        "Finalizzazione aggiornamento non riuscita",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }
            if (!DdfProductUpdateRetryCleanup.CanDiscard(
                    productUpdateRecoveryStatus))
            {
                MessageBox.Show(this,
                    "Questo stato non consente un ritentativo automatico. " +
                    "Il backup e l'installazione non verranno rimossi.\n\n" +
                    productUpdateRecoveryStatus.Detail + "\nAzione: " +
                    describeProductUpdateRecoveryAction(
                        productUpdateRecoveryStatus.Action),
                    "Aggiornamento da recuperare", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            DialogResult consent = MessageBox.Show(this,
                "Eliminare soltanto lo staging, il worker temporaneo e lo " +
                "stato del tentativo precedente? L'installazione corrente " +
                "e l'eventuale backup non saranno toccati.\n\n" +
                "Dopo la pulizia potrai riprovare il download.",
                "Ripulisci aggiornamento e ritenta",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (consent != DialogResult.Yes) return;
            try
            {
                DdfProductUpdateRetryCleanup.Discard(state,
                    Environment.ProcessPath);
                productUpdateRecoveryStatus =
                    DdfProductUpdateStartupRecovery.Inspect(plan, journal);
                appendOutput("Aggiornamenti: tentativo precedente ripulito; " +
                    "l'installazione corrente è rimasta intatta.");
                if (lastProductUpdateResult != null)
                    presentProductUpdateResult(lastProductUpdateResult, false);
                else statusUpdateLabel.Visible = false;
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException ||
                exception is InvalidOperationException ||
                exception is UnauthorizedAccessException)
            {
                productUpdateRecoveryStatus =
                    DdfProductUpdateStartupRecovery.Inspect(plan, journal);
                presentProductUpdateRecoveryStatus();
                MessageBox.Show(this, exception.Message,
                    "Pulizia aggiornamento non riuscita", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void showProductUpdateResult(DdfProductUpdateResult result)
        {
            if (result == null || embeddedHostMode) return;
            productUpdateForm?.Close();
            productUpdateForm = new ProductUpdateForm(result,
                openProductUpdateUri,
                productUpdateRecoveryStatus?.HasState == true
                    ? null : downloadProductUpdateAsync);
            productUpdateForm.FormClosed += (sender, args) =>
                productUpdateForm = null;
            productUpdateForm.Show(this);
        }

        private async Task downloadProductUpdateAsync(DdfProductUpdateResult result)
        {
            if (embeddedHostMode || productUpdateDownloadRunning ||
                result == null || !result.IsActionable ||
                productUpdateRecoveryStatus?.HasState == true)
                return;
            productUpdateDownloadRunning = true;
            var cancellation = new CancellationTokenSource();
            productUpdateDownloadCancellation = cancellation;
            string preparedJournalPath = null;
            bool installationWorkerStarted = false;
            try
            {
                var request = DdfProductUpdateRequest.ForStandalone(result.Channel);
                string runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
                string architecture = RuntimeInformation.ProcessArchitecture
                    .ToString().ToLowerInvariant();
                using var downloader = new DdfProductUpdatePackageDownloader(
                    DdfOfficialReleaseTrustStore.Create());
                DdfProductUpdateVerifiedOffer offer = await downloader
                    .InspectOfferAsync(result, request, runtimeIdentifier,
                        architecture, cancellation.Token);
                if (IsDisposed || Disposing) return;
                using (var consent = new ProductUpdateDownloadConsentForm(
                    result, offer))
                    if (consent.ShowDialog(this) != DialogResult.OK) return;

                string destination = Path.Combine(
                    AppSettingsStore.GetProductUpdateStateDirectory(),
                    "downloads");
                DdfProductUpdatePreparedPackage prepared = await downloader
                    .DownloadAuthorizedAsync(result, request, runtimeIdentifier,
                        architecture, destination, cancellation.Token);
                prepared = DdfProductUpdatePackageDownloader.ReopenVerifiedLocal(
                    prepared.ArchivePath, DdfOfficialReleaseTrustStore.Create(),
                    request, runtimeIdentifier, architecture,
                    cancellationToken: cancellation.Token);
                if (IsDisposed || Disposing) return;
                appendOutput("Aggiornamento " + offer.Version +
                    " scaricato e verificato in " + prepared.ArchivePath +
                    ". Ricevuta firmata: " + prepared.ManifestPath + ".");
                if (!prepared.Inventory.Any(file =>
                    file.RelativePath.Equals(
                        DdfProductUpdateWorkerDeployment.WorkerRelativePath,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    appendOutput("Installazione automatica non disponibile: " +
                        "la release non contiene il worker firmato.");
                    MessageBox.Show(this,
                        "Download verificato. Questa release non include il " +
                        "worker necessario all'installazione automatica.",
                        "Aggiornamento scaricato", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                DdfProductUpdateInstallationBinding binding;
                try
                {
                    binding = DdfProductUpdateInstallationBinding.Resolve(
                        Environment.ProcessPath);
                    appendOutput("Preflight installazione: destinazione " +
                        binding.TargetRoot + ".");
                }
                catch (InvalidDataException exception)
                {
                    appendOutput("Preflight installazione non disponibile: " +
                        exception.Message);
                    MessageBox.Show(this,
                        "Download verificato. Installazione automatica non " +
                        "disponibile per questa copia: " + exception.Message,
                        "Aggiornamento scaricato", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                using Process current = Process.GetCurrentProcess();
                DateTimeOffset startedUtc = new DateTimeOffset(
                    current.StartTime.ToUniversalTime(), TimeSpan.Zero);
                string stateDirectory =
                    AppSettingsStore.GetProductUpdateStateDirectory();
                DdfProductUpdateInstallationPlan previewPlan =
                    DdfProductUpdateLocalPlanFactory.Create(
                        prepared.ArchivePath,
                        DdfOfficialReleaseTrustStore.Create(), request,
                        runtimeIdentifier, architecture,
                        binding.InstallationId, binding.TargetRoot,
                        binding.StagingRoot, binding.BackupRoot,
                        binding.ApplicationExecutable, current.Id,
                        startedUtc, DateTimeOffset.UtcNow);
                using (var consent = new ProductUpdateInstallationConsentForm(
                    previewPlan, ProductUpdateConsentStage.Install))
                    if (consent.ShowDialog(this) != DialogResult.OK) return;
                if (!confirmAllDocumentChanges()) return;
                DdfProductUpdateLocalPreparationResult staged;
                Enabled = false;
                UseWaitCursor = true;
                try
                {
                    staged = await DdfProductUpdateLocalPreparation.PrepareAsync(
                        prepared.ArchivePath,
                        DdfOfficialReleaseTrustStore.Create(), request,
                        runtimeIdentifier, architecture, binding,
                        stateDirectory, current.Id, startedUtc,
                        cancellation.Token);
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        Enabled = true;
                        UseWaitCursor = false;
                    }
                }
                preparedJournalPath = staged.JournalPath;
                if (IsDisposed || Disposing)
                {
                    tryCancelProductUpdateInstallation(staged.JournalPath);
                    return;
                }
                string workerPath = DdfProductUpdateWorkerDeployment.CopyVerified(
                    staged.Plan, stateDirectory);
                bool authorized = DdfProductUpdateInstallAuthorization.TryAuthorize(
                    staged.PlanPath, staged.JournalPath, prepared.ArchivePath,
                    DdfOfficialReleaseTrustStore.Create(), request,
                    runtimeIdentifier, architecture, binding,
                    Environment.ProcessPath, current.Id, startedUtc, true);
                if (!authorized) return;
                Process worker = null;
                try
                {
                    using (DdfProductUpdateWorkerDeployment.OpenVerifiedForLaunch(
                        staged.Plan, workerPath))
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = workerPath,
                            WorkingDirectory = Path.GetDirectoryName(workerPath),
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        startInfo.ArgumentList.Add("--plan");
                        startInfo.ArgumentList.Add(staged.PlanPath);
                        startInfo.ArgumentList.Add("--journal");
                        startInfo.ArgumentList.Add(staged.JournalPath);
                        startInfo.ArgumentList.Add("--nonce");
                        startInfo.ArgumentList.Add(staged.Plan.Nonce);
                        worker = Process.Start(startInfo) ?? throw new
                            InvalidOperationException("Avvio worker non riuscito.");
                        installationWorkerStarted = true;
                    }
                    productUpdateInstallCloseApproved = true;
                    Close();
                    if (!IsDisposed)
                    {
                        if (!stopProductUpdateWorker(worker))
                            throw new InvalidOperationException(
                                "Impossibile arrestare il worker dopo " +
                                "l'annullamento della chiusura.");
                        cancelProductUpdateInstallation(staged.JournalPath);
                        installationWorkerStarted = false;
                        appendOutput("Installazione annullata: l'editor è rimasto aperto.");
                    }
                }
                catch
                {
                    if (worker == null)
                        cancelProductUpdateInstallation(staged.JournalPath);
                    else if (!IsDisposed && stopProductUpdateWorker(worker))
                    {
                        installationWorkerStarted = false;
                        tryCancelProductUpdateInstallation(staged.JournalPath);
                    }
                    throw;
                }
                finally
                {
                    productUpdateInstallCloseApproved = false;
                    worker?.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                if (!installationWorkerStarted)
                    tryCancelProductUpdateInstallation(preparedJournalPath);
                if (!IsDisposed)
                    appendOutput("Download aggiornamento annullato.");
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException ||
                exception is InvalidOperationException ||
                exception is System.ComponentModel.Win32Exception ||
                exception is System.Net.Http.HttpRequestException ||
                exception is UnauthorizedAccessException ||
                exception is TimeoutException)
            {
                if (!installationWorkerStarted)
                    tryCancelProductUpdateInstallation(preparedJournalPath);
                if (!IsDisposed)
                {
                    appendOutput("Download aggiornamento non riuscito: " +
                        exception.Message);
                    MessageBox.Show(this, exception.Message,
                        "Download aggiornamento", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            finally
            {
                if (!IsDisposed && preparedJournalPath != null &&
                    !installationWorkerStarted)
                    inspectPendingProductUpdateRecovery();
                productUpdateDownloadRunning = false;
                if (ReferenceEquals(productUpdateDownloadCancellation,
                        cancellation))
                    productUpdateDownloadCancellation = null;
                cancellation.Dispose();
            }
        }

        private static void cancelProductUpdateInstallation(string journalPath)
        {
            var store = new DdfProductUpdateInstallationJournalStore(journalPath);
            DdfProductUpdateInstallationJournal journal = store.Read();
            if (DdfProductUpdateInstallationTransitions.CanTransition(
                    journal.State, DdfProductUpdateInstallationState.Cancelled))
                store.Transition(journal.Revision,
                    DdfProductUpdateInstallationState.Cancelled,
                    DateTimeOffset.UtcNow);
        }

        private static void tryCancelProductUpdateInstallation(string journalPath)
        {
            if (string.IsNullOrWhiteSpace(journalPath)) return;
            try { cancelProductUpdateInstallation(journalPath); }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException ||
                exception is InvalidOperationException ||
                exception is UnauthorizedAccessException) { }
        }

        private static bool stopProductUpdateWorker(Process worker)
        {
            try
            {
                if (!worker.HasExited)
                    worker.Kill(entireProcessTree: true);
                return worker.WaitForExit(2000);
            }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }

        private void persistProductUpdateSettings()
        {
            if (embeddedHostMode || productUpdateSettings == null) return;
            try
            {
                saveProductUpdateSettingsSetting?.Invoke(
                    productUpdateSettings.Serialize());
            }
            catch (Exception exception) when (exception is System.IO.IOException ||
                exception is UnauthorizedAccessException)
            {
                statusFileLabel.ToolTipText =
                    "Impossibile memorizzare le impostazioni aggiornamenti: " +
                    exception.Message;
            }
        }

        private void stopProductUpdates()
        {
            productUpdateDelayTimer?.Stop();
            productUpdateDelayTimer?.Dispose();
            productUpdateCancellation?.Cancel();
            productUpdateCancellation?.Dispose();
            productUpdateCancellation = null;
            productUpdateDownloadCancellation?.Cancel();
            if (productUpdateProvider is IDisposable disposable)
                disposable.Dispose();
            productUpdateProvider = null;
            productUpdateForm?.Close();
        }
    }
}
