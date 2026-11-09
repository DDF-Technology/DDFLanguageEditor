using System;
using System.Diagnostics;
using System.IO;
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
                if (productUpdateRecoveryStatus?.RequiresAttention == true)
                {
                    MessageBox.Show(this, productUpdateRecoveryStatus.Detail,
                        "Aggiornamento da recuperare", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
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
            if (productUpdateRecoveryStatus?.RequiresAttention == true)
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
            if (!productUpdateRecoveryStatus.HasState ||
                productUpdateRecoveryStatus.Action ==
                    DdfProductUpdateRecoveryAction.None) return;

            appendOutput("Aggiornamenti: " +
                productUpdateRecoveryStatus.Detail + " Azione prevista: " +
                describeProductUpdateRecoveryAction(
                    productUpdateRecoveryStatus.Action) + ".");
            if (productUpdateRecoveryStatus.RequiresAttention)
                presentProductUpdateRecoveryStatus();
        }

        private void presentProductUpdateRecoveryStatus()
        {
            statusUpdateLabel.Visible = true;
            statusUpdateLabel.Text = "Aggiornamento da recuperare";
            statusUpdateLabel.ToolTipText = productUpdateRecoveryStatus.Detail;
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

        private void showProductUpdateResult(DdfProductUpdateResult result)
        {
            if (result == null || embeddedHostMode) return;
            productUpdateForm?.Close();
            productUpdateForm = new ProductUpdateForm(result,
                openProductUpdateUri, downloadProductUpdateAsync);
            productUpdateForm.FormClosed += (sender, args) =>
                productUpdateForm = null;
            productUpdateForm.Show(this);
        }

        private async Task downloadProductUpdateAsync(DdfProductUpdateResult result)
        {
            if (embeddedHostMode || productUpdateDownloadRunning ||
                result == null || !result.IsActionable ||
                productUpdateRecoveryStatus?.RequiresAttention == true)
                return;
            productUpdateDownloadRunning = true;
            var cancellation = new CancellationTokenSource();
            productUpdateDownloadCancellation = cancellation;
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
                    ". Ricevuta firmata: " + prepared.ManifestPath +
                    ". Installazione non ancora disponibile.");
                MessageBox.Show(this,
                    "Download e verifica completati. L'installazione " +
                    "automatica non è ancora disponibile in questa beta.",
                    "Aggiornamento scaricato", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed)
                    appendOutput("Download aggiornamento annullato.");
            }
            catch (Exception exception) when (exception is IOException ||
                exception is InvalidDataException ||
                exception is System.Net.Http.HttpRequestException ||
                exception is UnauthorizedAccessException ||
                exception is TimeoutException)
            {
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
                productUpdateDownloadRunning = false;
                if (ReferenceEquals(productUpdateDownloadCancellation,
                        cancellation))
                    productUpdateDownloadCancellation = null;
                cancellation.Dispose();
            }
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
