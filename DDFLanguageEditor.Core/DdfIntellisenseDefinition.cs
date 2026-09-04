using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
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
            string signatureWorkspaceOrigin, string signatureStandardLibraryOrigin)
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
                new DdfSymbolPresentation(DdfSymbolKind.Function, "funzione", "Funzione dichiarata nel sorgente DDF."),
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
