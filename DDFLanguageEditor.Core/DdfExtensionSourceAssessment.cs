using System;
using System.Text.Json;

namespace DDFLanguageEditor.Core
{
    // Classifies protocol outcomes under host-owned DDFX codes; no plug-in is invoked.
    public sealed class DdfExtensionSourceAssessment
    {
        private DdfExtensionSourceAssessment(DdfExtensionSourceOperationKind kind,
            DdfExtensionCallAssessmentStatus status,
            DdfExtensionSemanticResponse semantic,
            DdfExtensionBuildResponse build,
            DdfExtensionPluginDiagnostic diagnostic)
        {
            Kind = kind;
            Status = status;
            Semantic = semantic;
            Build = build;
            Diagnostic = diagnostic;
        }

        public DdfExtensionSourceOperationKind Kind { get; }
        public DdfExtensionCallAssessmentStatus Status { get; }
        public DdfExtensionSemanticResponse Semantic { get; }
        public DdfExtensionBuildResponse Build { get; }
        public DdfExtensionPluginDiagnostic Diagnostic { get; }
        public bool Success => Status == DdfExtensionCallAssessmentStatus.Succeeded;

        public static DdfExtensionSourceAssessment FromSemantic(
            DdfExtensionSemanticResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            return response.Status == DdfExtensionCallStatus.Succeeded
                ? new DdfExtensionSourceAssessment(DdfExtensionSourceOperationKind.Semantic,
                    DdfExtensionCallAssessmentStatus.Succeeded, response, null, null)
                : Failure(DdfExtensionSourceOperationKind.Semantic,
                    DdfExtensionCallAssessmentStatus.PluginFailure, "DDFX601",
                    "L'estensione semantica ha segnalato un errore.", response, null);
        }

        public static DdfExtensionSourceAssessment FromBuild(
            DdfExtensionBuildResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            return response.Status == DdfExtensionCallStatus.Succeeded
                ? new DdfExtensionSourceAssessment(DdfExtensionSourceOperationKind.Build,
                    DdfExtensionCallAssessmentStatus.Succeeded, null, response, null)
                : Failure(DdfExtensionSourceOperationKind.Build,
                    DdfExtensionCallAssessmentStatus.PluginFailure, "DDFX601",
                    "Il backend di build ha segnalato un errore.", null, response);
        }

        public static DdfExtensionSourceAssessment AcceptSemantic(
            DdfExtensionSourceCorrelationLedger ledger, string responseFrameJson)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            try { return FromSemantic(ledger.AcceptSemantic(responseFrameJson)); }
            catch (DdfExtensionCallExpiredException)
            {
                return Failure(DdfExtensionSourceOperationKind.Semantic,
                    DdfExtensionCallAssessmentStatus.Expired, "DDFX603",
                    "La risposta semantica è scaduta.");
            }
            catch (DdfExtensionCallUnknownException)
            {
                return Failure(DdfExtensionSourceOperationKind.Semantic,
                    DdfExtensionCallAssessmentStatus.UnknownCall, "DDFX604",
                    "Richiesta semantica sconosciuta o già consumata.");
            }
            catch (Exception exception) when (InvalidPayload(exception))
            {
                return Failure(DdfExtensionSourceOperationKind.Semantic,
                    DdfExtensionCallAssessmentStatus.InvalidResponse, "DDFX602",
                    "Risposta semantica non valida.");
            }
        }

        public static DdfExtensionSourceAssessment AcceptBuild(
            DdfExtensionSourceCorrelationLedger ledger, string responseFrameJson)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            try { return FromBuild(ledger.AcceptBuild(responseFrameJson)); }
            catch (DdfExtensionCallExpiredException)
            {
                return Failure(DdfExtensionSourceOperationKind.Build,
                    DdfExtensionCallAssessmentStatus.Expired, "DDFX603",
                    "La risposta build è scaduta.");
            }
            catch (DdfExtensionCallUnknownException)
            {
                return Failure(DdfExtensionSourceOperationKind.Build,
                    DdfExtensionCallAssessmentStatus.UnknownCall, "DDFX604",
                    "Richiesta build sconosciuta o già consumata.");
            }
            catch (Exception exception) when (InvalidPayload(exception))
            {
                return Failure(DdfExtensionSourceOperationKind.Build,
                    DdfExtensionCallAssessmentStatus.InvalidResponse, "DDFX602",
                    "Risposta build non valida.");
            }
        }

        private static bool InvalidPayload(Exception exception) =>
            exception is JsonException || exception is FormatException ||
            exception is ArgumentException;

        private static DdfExtensionSourceAssessment Failure(
            DdfExtensionSourceOperationKind kind,
            DdfExtensionCallAssessmentStatus status, string code, string message,
            DdfExtensionSemanticResponse semantic = null,
            DdfExtensionBuildResponse build = null) =>
            new DdfExtensionSourceAssessment(kind, status, semantic, build,
                new DdfExtensionPluginDiagnostic(code,
                    DdfExtensionPluginDiagnosticDomain.Execution, message));
    }
}
