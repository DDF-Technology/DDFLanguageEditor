using System;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    public enum DdfExtensionCallAssessmentStatus
    {
        Succeeded,
        PluginFailure,
        InvalidResponse,
        Expired,
        UnknownCall
    }

    // Host diagnostics are assigned here; a worker's errorId is never accepted as a DDFX code.
    public sealed class DdfExtensionCallAssessment
    {
        private DdfExtensionCallAssessment(DdfExtensionCallAssessmentStatus status,
            DdfExtensionCallResponse response, DdfExtensionPluginDiagnostic diagnostic)
        {
            Status = status;
            Response = response;
            Diagnostic = diagnostic;
        }

        public DdfExtensionCallAssessmentStatus Status { get; }
        public DdfExtensionCallResponse Response { get; }
        public DdfExtensionPluginDiagnostic Diagnostic { get; }
        public bool Success => Status == DdfExtensionCallAssessmentStatus.Succeeded;

        public static DdfExtensionCallAssessment FromResponse(
            DdfExtensionCallResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (response.Status == DdfExtensionCallStatus.Succeeded)
                return new DdfExtensionCallAssessment(
                    DdfExtensionCallAssessmentStatus.Succeeded, response, null);
            return new DdfExtensionCallAssessment(
                DdfExtensionCallAssessmentStatus.PluginFailure, response,
                DiagnosticFor("DDFX601", "Il plug-in ha segnalato " +
                    response.ErrorId + "."));
        }

        public static DdfExtensionCallAssessment Accept(
            DdfExtensionCallCorrelationLedger ledger, string responseFrameJson)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            try
            {
                return FromResponse(ledger.Accept(responseFrameJson));
            }
            catch (DdfExtensionCallExpiredException)
            {
                return Failure(DdfExtensionCallAssessmentStatus.Expired,
                    "DDFX603", "La risposta del plug-in è scaduta.");
            }
            catch (DdfExtensionCallUnknownException)
            {
                return Failure(DdfExtensionCallAssessmentStatus.UnknownCall,
                    "DDFX604", "Chiamata plug-in sconosciuta o già consumata.");
            }
            catch (Exception exception) when (exception is JsonException ||
                exception is FormatException || exception is ArgumentException)
            {
                return Failure(DdfExtensionCallAssessmentStatus.InvalidResponse,
                    "DDFX602", "Risposta del plug-in non valida.");
            }
        }

        private static DdfExtensionCallAssessment Failure(
            DdfExtensionCallAssessmentStatus status, string code, string message) =>
            new DdfExtensionCallAssessment(status, null, DiagnosticFor(code, message));

        private static DdfExtensionPluginDiagnostic DiagnosticFor(string code,
            string message) => new DdfExtensionPluginDiagnostic(code,
                DdfExtensionPluginDiagnosticDomain.Execution, message);
    }
}
