using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfFlowchartPresentation
    {
        private readonly IReadOnlyDictionary<DdfFlowchartNodeKind, string> explanations;

        public DdfFlowchartPresentation(string trueLabel, string falseLabel, string exitLabelFormat,
            string guideTitle = null, string guidePrompt = null, string debugTitle = null,
            string selectedTitle = null, string debugPrefix = null, string valuesPrefix = null,
            IEnumerable<KeyValuePair<DdfFlowchartNodeKind, string>> explanations = null)
        {
            TrueLabel = string.IsNullOrWhiteSpace(trueLabel) ? "Sì" : trueLabel;
            FalseLabel = string.IsNullOrWhiteSpace(falseLabel) ? "No" : falseLabel;
            ExitLabelFormat = string.IsNullOrWhiteSpace(exitLabelFormat) ? "Fine {function}" : exitLabelFormat;
            GuideTitle = guideTitle ?? "GUIDA DIDATTICA";
            GuidePrompt = guidePrompt ?? "Posiziona il cursore su un'istruzione oppure seleziona un nodo per leggerne la funzione nel flusso.";
            DebugTitle = debugTitle ?? "PASSO CORRENTE DEL DEBUG";
            SelectedTitle = selectedTitle ?? "NODO SELEZIONATO";
            DebugPrefix = debugPrefix ?? "L'esecuzione è sospesa su questo punto. ";
            ValuesPrefix = valuesPrefix ?? "Valori coinvolti: ";
            this.explanations = (explanations ?? Array.Empty<KeyValuePair<DdfFlowchartNodeKind, string>>())
                .GroupBy(item => item.Key).ToDictionary(group => group.Key, group => group.Last().Value);
        }

        public string TrueLabel { get; }
        public string FalseLabel { get; }
        public string ExitLabelFormat { get; }
        public string GuideTitle { get; }
        public string GuidePrompt { get; }
        public string DebugTitle { get; }
        public string SelectedTitle { get; }
        public string DebugPrefix { get; }
        public string ValuesPrefix { get; }
        public string FormatExit(string functionName) => ExitLabelFormat.Replace("{function}", functionName ?? string.Empty);
        public string Explain(DdfFlowchartNode node)
        {
            if (node == null) return GuidePrompt;
            string code = string.IsNullOrWhiteSpace(node.Label) ? string.Empty : "«" + node.Label + "»";
            if (explanations.TryGetValue(node.Kind, out string template) && !string.IsNullOrWhiteSpace(template))
                return template.Replace("{code}", code).Replace("{true}", TrueLabel).Replace("{false}", FalseLabel);
            return "Elemento del flusso " + code + ".";
        }

        public static DdfFlowchartPresentation CreateLegacyDefault() => new DdfFlowchartPresentation(
            "Sì", "No", "Fine {function}", explanations: new Dictionary<DdfFlowchartNodeKind, string>
            {
                [DdfFlowchartNodeKind.Entry] = "Ingresso della funzione: da qui comincia il suo flusso di esecuzione.",
                [DdfFlowchartNodeKind.Exit] = "Uscita della funzione: il flusso termina e il controllo torna all'eventuale chiamante.",
                [DdfFlowchartNodeKind.Process] = "Istruzione sequenziale {code}: viene eseguita una volta, poi il flusso continua verso il nodo successivo.",
                [DdfFlowchartNodeKind.Call] = "Chiamata {code}: il controllo entra nella funzione indicata e, al termine, riprende dal passo seguente.",
                [DdfFlowchartNodeKind.Decision] = "Decisione {code}: la condizione viene valutata; il ramo {true} è seguito quando è vera, il ramo {false} quando è falsa.",
                [DdfFlowchartNodeKind.LoopDecision] = "Controllo di ciclo {code}: la condizione decide se eseguire o ripetere il corpo del ciclo oppure proseguire dopo di esso.",
                [DdfFlowchartNodeKind.Return] = "Ritorno {code}: termina la funzione e restituisce il valore al chiamante.",
                [DdfFlowchartNodeKind.Break] = "Interruzione del ciclo: il flusso esce immediatamente dal ciclo più vicino.",
                [DdfFlowchartNodeKind.Continue] = "Continuazione del ciclo: il flusso salta il resto dell'iterazione e passa alla verifica successiva.",
                [DdfFlowchartNodeKind.Terminate] = "Terminazione esplicita: l'esecuzione del percorso corrente viene conclusa.",
                [DdfFlowchartNodeKind.Partial] = "Nodo parziale: il codice è ancora incompleto e questa rappresentazione potrebbe cambiare durante la correzione.",
                [DdfFlowchartNodeKind.Group] = "Gruppo compatto: raccoglie più istruzioni sequenziali. Un doppio clic mostra i singoli passaggi."
            });
    }

    public sealed class DdfDiagnosticPresentation
    {
        private readonly IReadOnlyDictionary<string, string> messages;

        public DdfDiagnosticPresentation(IEnumerable<KeyValuePair<string, string>> messages = null)
        {
            this.messages = (messages ?? Array.Empty<KeyValuePair<string, string>>())
                .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                .GroupBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
        }

        public string GetMessage(string code, string fallback) =>
            messages.TryGetValue(code ?? string.Empty, out string message) ? message : fallback;

        public DdfDiagnostic Apply(DdfDiagnostic diagnostic)
        {
            if (diagnostic == null) return null;
            return diagnostic.WithDisplayMessage(GetMessage(diagnostic.Code, diagnostic.Message));
        }

        internal static DdfDiagnosticPresentation CreateLegacyDefault() => new DdfDiagnosticPresentation();
    }

    public sealed class DdfSymbolPresentation
    {
        public DdfSymbolPresentation(DdfSymbolKind kind, string label, string documentation)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Documentation = documentation ?? string.Empty;
        }
        public DdfSymbolKind Kind { get; }
        public string Label { get; }
        public string Documentation { get; }
    }

    public sealed class DdfIntellisenseDefinition
    {
        private readonly IReadOnlyDictionary<DdfSymbolKind, DdfSymbolPresentation> symbols;

        public DdfIntellisenseDefinition(IEnumerable<DdfSnippetTemplate> snippets,
            IEnumerable<DdfSymbolPresentation> symbolPresentations,
            string keywordOrigin, string booleanOrigin, string snippetOrigin,
            string standardLibraryOrigin, string currentDocumentOrigin, string workspaceOrigin,
            string typeDetail, string controlFlowDetail, string keywordDetail, string booleanDetail,
            string standardFunctionDetailPrefix, string signatureCurrentDocumentOrigin,
            string signatureWorkspaceOrigin, string signatureStandardLibraryOrigin,
            DdfFlowchartPresentation flowchart = null,
            DdfDiagnosticPresentation diagnostics = null)
        {
            Snippets = (snippets ?? Array.Empty<DdfSnippetTemplate>()).ToList().AsReadOnly();
            symbols = (symbolPresentations ?? Array.Empty<DdfSymbolPresentation>())
                .ToDictionary(item => item.Kind);
            KeywordOrigin = keywordOrigin ?? string.Empty;
            BooleanOrigin = booleanOrigin ?? string.Empty;
            SnippetOrigin = snippetOrigin ?? string.Empty;
            StandardLibraryOrigin = standardLibraryOrigin ?? string.Empty;
            CurrentDocumentOrigin = currentDocumentOrigin ?? string.Empty;
            WorkspaceOrigin = workspaceOrigin ?? string.Empty;
            TypeDetail = typeDetail ?? string.Empty;
            ControlFlowDetail = controlFlowDetail ?? string.Empty;
            KeywordDetail = keywordDetail ?? string.Empty;
            BooleanDetail = booleanDetail ?? string.Empty;
            StandardFunctionDetailPrefix = standardFunctionDetailPrefix ?? string.Empty;
            SignatureCurrentDocumentOrigin = signatureCurrentDocumentOrigin ?? string.Empty;
            SignatureWorkspaceOrigin = signatureWorkspaceOrigin ?? string.Empty;
            SignatureStandardLibraryOrigin = signatureStandardLibraryOrigin ?? string.Empty;
            Flowchart = flowchart ?? DdfFlowchartPresentation.CreateLegacyDefault();
            Diagnostics = diagnostics ?? DdfDiagnosticPresentation.CreateLegacyDefault();
        }

        public IReadOnlyList<DdfSnippetTemplate> Snippets { get; }
        public string KeywordOrigin { get; }
        public string BooleanOrigin { get; }
        public string SnippetOrigin { get; }
        public string StandardLibraryOrigin { get; }
        public string CurrentDocumentOrigin { get; }
        public string WorkspaceOrigin { get; }
        public string TypeDetail { get; }
        public string ControlFlowDetail { get; }
        public string KeywordDetail { get; }
        public string BooleanDetail { get; }
        public string StandardFunctionDetailPrefix { get; }
        public string SignatureCurrentDocumentOrigin { get; }
        public string SignatureWorkspaceOrigin { get; }
        public string SignatureStandardLibraryOrigin { get; }
        public DdfFlowchartPresentation Flowchart { get; }
        public DdfDiagnosticPresentation Diagnostics { get; }

        public string GetSymbolLabel(DdfSymbolKind kind) =>
            symbols.TryGetValue(kind, out DdfSymbolPresentation value) ? value.Label : kind.ToString();

        public string GetSymbolDocumentation(DdfSymbolKind kind) =>
            symbols.TryGetValue(kind, out DdfSymbolPresentation value) ? value.Documentation : string.Empty;

        internal static DdfIntellisenseDefinition CreateLegacyDefault()
        {
            return new DdfIntellisenseDefinition(new[]
            {
                new DdfSnippetTemplate("if", "if — blocco", "Blocco condizionale", "if(${1:condition})\n{\n    ${2:statement};\n}${0}", true),
                new DdfSnippetTemplate("while", "while — ciclo", "Ciclo con condizione iniziale", "while(${1:condition})\n{\n    ${2:statement};\n}${0}", true),
                new DdfSnippetTemplate("do", "do — ciclo", "Ciclo con condizione finale", "do\n{\n    ${1:statement};\n} while(${2:condition});${0}", true),
                new DdfSnippetTemplate("for", "for — ciclo", "Ciclo indicizzato", "for(int ${1:index}; ${2:index} < ${3:limit}; ${4:index}++)\n{\n    ${5:statement};\n}${0}", true),
                new DdfSnippetTemplate("function", "function — dichiarazione", "Dichiarazione di funzione", "${1:name}(${2:int} ${3:parameter}) out ${4:void}\n{\n    ${5:statement};\n}${0}"),
                new DdfSnippetTemplate("main", "main — entry point", "Funzione principale", "main() out ${1:int}\n{\n    ${2:ret 0};\n}${0}"),
                new DdfSnippetTemplate("struct", "struct — dichiarazione", "Dichiarazione di struttura", "struct ${1:Name}\n{\n    ${2:int} ${3:field};\n}${0}", true)
            }, new[]
            {
                new DdfSymbolPresentation(DdfSymbolKind.Library, "libreria", "Direttiva di libreria DDF."),
                new DdfSymbolPresentation(DdfSymbolKind.Structure, "struttura", "Struttura dati dichiarata nel sorgente DDF."),
                new DdfSymbolPresentation(DdfSymbolKind.Enumeration, "enum", "Insieme nominale di valori simbolici tipizzati."),
                new DdfSymbolPresentation(DdfSymbolKind.EnumerationMember, "valore enum", "Valore simbolico appartenente a un enum DDF."),
                new DdfSymbolPresentation(DdfSymbolKind.Function, "funzione di modulo", "Funzione top-level appartenente al modulo DDF; non richiede né ammette il modificatore static."),
                new DdfSymbolPresentation(DdfSymbolKind.Parameter, "parametro", "Parametro della funzione corrente."),
                new DdfSymbolPresentation(DdfSymbolKind.Field, "campo", "Campo di una struttura DDF."),
                new DdfSymbolPresentation(DdfSymbolKind.Variable, "variabile", "Variabile visibile nel relativo ambito DDF.")
            }, "linguaggio DDF", "linguaggio DDF", "snippet DDF", "libreria standard",
                "documento corrente", "workspace", "tipo", "controllo di flusso", "parola chiave",
                "valore booleano", "funzione standard — ", "documento corrente", "workspace",
                "libreria standard");
        }
    }
}
