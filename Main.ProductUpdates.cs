using System;
using System.Diagnostics;
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
        private ProductUpdateForm productUpdateForm;
        private bool productUpdateCheckRunning;

        private void initializeProductUpdates()
        {
            productUpdateSettings = ProductUpdateSettings.Parse(
                embeddedHostMode ? string.Empty :
                AppSettingsStore.LoadProductUpdateSettings());
            productUpdateClock = () => DateTimeOffset.UtcNow;
            createProductUpdateProvider = () => null;
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
                IsLink = true,
                Visible = false,
                Available = !embeddedHostMode,
                ToolTipText = "Apri i dettagli dell'aggiornamento"
            };
            statusUpdateLabel.Click += (sender, args) =>
                showProductUpdateResult(lastProductUpdateResult);
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
                "La verifica firmata non è ancora attiva: la chiave pubblica " +
                "ufficiale della release non è stata provisionata.");

        private void presentProductUpdateResult(DdfProductUpdateResult result,
            bool manual)
        {
            statusUpdateLabel.Visible = result.IsActionable;
            statusUpdateLabel.Text = result.IsActionable
                ? "Aggiornamento " + result.AvailableVersion + " disponibile"
                : string.Empty;
            statusUpdateLabel.ToolTipText = result.IsActionable
                ? "Apri i dettagli della release verificata"
                : result.StatusDetail;

            if (result.State == DdfProductUpdateState.Unknown)
                appendOutput("Aggiornamenti: " + result.StatusDetail);
            else if (result.State == DdfProductUpdateState.Incompatible)
                appendOutput("Aggiornamenti: release " + result.AvailableVersion +
                    " non compatibile con questa installazione.");
            if (manual) showProductUpdateResult(result);
        }

        private void showProductUpdateResult(DdfProductUpdateResult result)
        {
            if (result == null || embeddedHostMode) return;
            productUpdateForm?.Close();
            productUpdateForm = new ProductUpdateForm(result,
                openProductUpdateUri);
            productUpdateForm.FormClosed += (sender, args) =>
                productUpdateForm = null;
            productUpdateForm.Show(this);
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
            if (productUpdateProvider is IDisposable disposable)
                disposable.Dispose();
            productUpdateProvider = null;
            productUpdateForm?.Close();
        }
    }
}
