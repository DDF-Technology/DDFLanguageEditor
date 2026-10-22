using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfErrorKind
    {
        Recoverable,
        EngineFault
    }

    /// <summary>
    /// Backend-neutral identity of an error that can cross DDF functions and
    /// host APIs. It contains no CLR exception type or backend-specific data.
    /// </summary>
    public sealed class DdfErrorDefinition
    {
        public DdfErrorDefinition(string typeId, string code,
            DdfErrorKind kind, string documentation)
        {
            string normalizedType = (typeId ?? string.Empty).Trim();
            string normalizedCode = (code ?? string.Empty).Trim();
            if (!Regex.IsMatch(normalizedType,
                    "^[a-z][A-Za-z0-9]*(?:\\.[a-z][A-Za-z0-9]*)*$"))
                throw new ArgumentException(
                    "L'identità del tipo errore DDF non è valida.",
                    nameof(typeId));
            if (!Regex.IsMatch(normalizedCode, "^DDF[0-9]{3}$"))
                throw new ArgumentException(
                    "Il codice errore DDF deve usare il formato DDFnnn.",
                    nameof(code));
            if (!Enum.IsDefined(typeof(DdfErrorKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            TypeId = normalizedType;
            Code = normalizedCode;
            Kind = kind;
            Documentation = (documentation ?? string.Empty).Trim();
        }

        public string TypeId { get; }
        public string Code { get; }
        public DdfErrorKind Kind { get; }
        public string Documentation { get; }
        public bool IsRecoverable => Kind == DdfErrorKind.Recoverable;
    }

    public sealed class DdfErrorStackFrame
    {
        public DdfErrorStackFrame(string functionName, string documentPath,
            int start, int length)
            : this(functionName, documentPath, start, length, 0, 0,
                string.Empty)
        {
        }

        public DdfErrorStackFrame(string functionName, string documentPath,
            int start, int length, int line, int column, string symbolId)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("La funzione è obbligatoria.",
                    nameof(functionName));
            if (start < 0 || length < 0)
                throw new ArgumentOutOfRangeException(nameof(start));
            if (line < 0 || column < 0)
                throw new ArgumentOutOfRangeException(nameof(line));
            FunctionName = functionName.Trim();
            DocumentPath = (documentPath ?? string.Empty).Replace('\\', '/');
            Start = start;
            Length = length;
            Line = line;
            Column = column;
            SymbolId = (symbolId ?? string.Empty).Trim();
        }

        public string FunctionName { get; }
        public string DocumentPath { get; }
        public int Start { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
        public string SymbolId { get; }
    }

    /// <summary>
    /// Immutable script-visible error value. Creation does not imply that the
    /// current language syntax can catch it yet.
    /// </summary>
    public sealed class DdfErrorValue
    {
        public DdfErrorValue(DdfErrorDefinition definition, string message,
            string documentPath, int start, int length,
            IEnumerable<DdfErrorStackFrame> stack = null)
        {
            Definition = definition ?? throw new ArgumentNullException(
                nameof(definition));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Il messaggio è obbligatorio.",
                    nameof(message));
            if (start < 0 || length < 0)
                throw new ArgumentOutOfRangeException(nameof(start));
            Message = message.Trim();
            DocumentPath = (documentPath ?? string.Empty).Replace('\\', '/');
            Start = start;
            Length = length;
            Stack = (stack ?? Array.Empty<DdfErrorStackFrame>()).ToList()
                .AsReadOnly();
        }

        public DdfErrorDefinition Definition { get; }
        public string TypeId => Definition.TypeId;
        public string Code => Definition.Code;
        public DdfErrorKind Kind => Definition.Kind;
        public string Message { get; }
        public string DocumentPath { get; }
        public int Start { get; }
        public int Length { get; }
        public IReadOnlyList<DdfErrorStackFrame> Stack { get; }
    }

    public static class DdfErrorCatalog
    {
        public static readonly DdfErrorDefinition DivisionByZero =
            Recoverable("arithmetic.divisionZero", "DDF404",
                "Divisione per zero durante un'operazione DDF.");
        public static readonly DdfErrorDefinition InvalidAccess =
            Recoverable("access.invalid", "DDF405",
                "Indice, membro o destinazione non validi.");
        public static readonly DdfErrorDefinition InvalidInput =
            Recoverable("operation.invalidInput", "DDF408",
                "Input, conversione o operazione standard non valida.");
        public static readonly DdfErrorDefinition NumericOverflow =
            Recoverable("arithmetic.overflow", "DDF417",
                "Risultato intero non rappresentabile.");
        public static readonly DdfErrorDefinition NonFiniteNumber =
            Recoverable("arithmetic.nonFinite", "DDF418",
                "Risultato numerico non finito.");
        public static readonly DdfErrorDefinition NegativeIntegerExponent =
            Recoverable("arithmetic.negativeExponent", "DDF419",
                "Esponente intero negativo non ammesso.");

        public static readonly DdfErrorDefinition MissingEntryPoint =
            Fault("engine.missingEntryPoint", "DDF401",
                "Punto di ingresso dell'applicazione assente.");
        public static readonly DdfErrorDefinition InvalidCallContract =
            Fault("engine.invalidCallContract", "DDF402",
                "Funzione, binding o arità non coerenti con l'artefatto validato.");
        public static readonly DdfErrorDefinition InvalidRuntimeState =
            Fault("engine.invalidRuntimeState", "DDF403",
                "Controllo di flusso o valore runtime interno non valido.");
        public static readonly DdfErrorDefinition UndefinedRuntimeValue =
            Fault("engine.undefinedRuntimeValue", "DDF406",
                "Simbolo richiesto dall'artefatto non disponibile a runtime.");
        public static readonly DdfErrorDefinition InstructionLimit =
            Fault("engine.instructionLimit", "DDF407",
                "Budget di istruzioni esaurito.");
        public static readonly DdfErrorDefinition CapabilityUnavailable =
            Fault("engine.capabilityUnavailable", "DDF409",
                "Capability assente, revocata o non autorizzata.");
        public static readonly DdfErrorDefinition HostAdapterFailure =
            Fault("engine.hostAdapterFailure", "DDF410",
                "Adapter host fallito senza un errore DDF dichiarato.");
        public static readonly DdfErrorDefinition HostWriteDenied =
            Fault("engine.hostWriteDenied", "DDF411",
                "Scrittura richiesta su una superficie host non autorizzata.");
        public static readonly DdfErrorDefinition SimulationUnavailable =
            Fault("engine.simulationUnavailable", "DDF412",
                "Simulazione richiesta ma non fornita dalla capability host.");
        public static readonly DdfErrorDefinition ResourceBudget =
            Fault("engine.resourceBudget", "DDF413",
                "Budget di chiamate o risorse capability esaurito.");
        public static readonly DdfErrorDefinition CapabilityTimeout =
            Fault("engine.capabilityTimeout", "DDF414",
                "Timeout governato di una capability host.");
        public static readonly DdfErrorDefinition CleanupFailure =
            Fault("engine.cleanupFailure", "DDF415",
                "Rilascio di una risorsa capability non riuscito.");
        public static readonly DdfErrorDefinition InvocationContract =
            Fault("engine.invocationContract", "DDF416",
                "Argomenti dell'invocazione host non conformi al contratto.");
        public static readonly DdfErrorDefinition TransactionFailure =
            Fault("engine.transactionFailure", "DDF420",
                "Lifecycle della transazione capability non riuscito.");
        public static readonly DdfErrorDefinition ConcurrentStateUse =
            Fault("engine.concurrentState", "DDF423",
                "Store state già impegnato da un'altra esecuzione.");
        public static readonly DdfErrorDefinition ExecutionCancelled =
            Fault("engine.executionCancelled", "DDF424",
                "Esecuzione annullata dal proprietario o dall'host.");
        public static readonly DdfErrorDefinition SecurityViolation =
            Fault("engine.securityViolation", "DDF425",
                "Violazione di una policy di sicurezza o isolamento.");
        public static readonly DdfErrorDefinition InvalidArtifact =
            Fault("engine.invalidArtifact", "DDF426",
                "Artefatto runtime corrotto, alterato o incompatibile.");
        public static readonly DdfErrorDefinition InternalFailure =
            Fault("engine.internalFailure", "DDF499",
                "Errore interno non previsto del motore DDF.");

        private static readonly IReadOnlyList<DdfErrorDefinition> all =
            Array.AsReadOnly(new[]
            {
                DivisionByZero, InvalidAccess, InvalidInput, NumericOverflow,
                NonFiniteNumber, NegativeIntegerExponent, MissingEntryPoint,
                InvalidCallContract, InvalidRuntimeState, UndefinedRuntimeValue,
                InstructionLimit, CapabilityUnavailable, HostAdapterFailure,
                HostWriteDenied, SimulationUnavailable, ResourceBudget,
                CapabilityTimeout, CleanupFailure, InvocationContract,
                TransactionFailure, ConcurrentStateUse, ExecutionCancelled,
                SecurityViolation, InvalidArtifact, InternalFailure
            });

        private static readonly IReadOnlyDictionary<string, DdfErrorDefinition>
            byCode = all.ToDictionary(item => item.Code, item => item,
                StringComparer.Ordinal);
        private static readonly IReadOnlyDictionary<string, DdfErrorDefinition>
            byTypeId = all.ToDictionary(item => item.TypeId, item => item,
                StringComparer.Ordinal);

        public static IReadOnlyList<DdfErrorDefinition> All => all;

        public static bool TryGetByCode(string code,
            out DdfErrorDefinition definition) => byCode.TryGetValue(
                code ?? string.Empty, out definition);

        public static bool TryGetByTypeId(string typeId,
            out DdfErrorDefinition definition) => byTypeId.TryGetValue(
                typeId ?? string.Empty, out definition);

        public static IReadOnlyList<DdfErrorDefinition> ValidateRecoverable(
            IEnumerable<DdfErrorDefinition> definitions,
            string parameterName)
        {
            DdfErrorDefinition[] values = (definitions ??
                Array.Empty<DdfErrorDefinition>()).ToArray();
            if (values.Any(item => item == null) ||
                values.Select(item => item.TypeId).Distinct(
                    StringComparer.Ordinal).Count() != values.Length ||
                values.Select(item => item.Code).Distinct(
                    StringComparer.Ordinal).Count() != values.Length)
                throw new ArgumentException(
                    "Gli errori recuperabili devono essere non nulli e univoci.",
                    parameterName);
            if (values.Any(item => !item.IsRecoverable))
                throw new ArgumentException(
                    "Un fault del motore non può essere dichiarato recuperabile.",
                    parameterName);
            return Array.AsReadOnly(values);
        }

        private static DdfErrorDefinition Recoverable(string typeId,
            string code, string documentation) => new(typeId, code,
                DdfErrorKind.Recoverable, documentation);

        private static DdfErrorDefinition Fault(string typeId, string code,
            string documentation) => new(typeId, code,
                DdfErrorKind.EngineFault, documentation);
    }

    public static class DdfErrorPresentation
    {
        public static string FormatDeclarations(
            IEnumerable<DdfErrorDefinition> definitions)
        {
            string[] values = (definitions ??
                    Array.Empty<DdfErrorDefinition>())
                .Where(item => item != null)
                .Select(item => item.TypeId + " (" + item.Code + ")")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return values.Length == 0 ? string.Empty :
                "Errori recuperabili: " + string.Join(", ", values);
        }

        public static string AppendDeclarations(string text,
            IEnumerable<DdfErrorDefinition> definitions)
        {
            string declarations = FormatDeclarations(definitions);
            if (string.IsNullOrEmpty(declarations)) return text ?? string.Empty;
            return string.IsNullOrWhiteSpace(text) ? declarations :
                text.TrimEnd() + Environment.NewLine + declarations;
        }
    }
}
