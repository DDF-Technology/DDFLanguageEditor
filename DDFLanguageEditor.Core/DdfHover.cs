using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfHoverReference
    {
        public DdfHoverReference(int line, int column)
        {
            Line = Math.Max(1, line);
            Column = Math.Max(1, column);
        }

        public int Line { get; }
        public int Column { get; }
        public override string ToString() => "riga " + Line + ", colonna " + Column;
    }

    public sealed class DdfHoverInfo
    {
        internal DdfHoverInfo(
            string name,
            string kind,
            string typeName,
            string signature,
            string origin,
            string documentation,
            int declarationLine,
            int declarationColumn,
            IReadOnlyList<DdfHoverReference> references)
        {
            Name = name ?? string.Empty;
            Kind = kind ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            Signature = signature ?? string.Empty;
            Origin = origin ?? string.Empty;
            Documentation = documentation ?? string.Empty;
            DeclarationLine = Math.Max(0, declarationLine);
            DeclarationColumn = Math.Max(0, declarationColumn);
            References = references ?? new List<DdfHoverReference>().AsReadOnly();
        }

        public string Name { get; }
        public string Kind { get; }
        public string TypeName { get; }
        public string Signature { get; }
        public string Origin { get; }
        public string Documentation { get; }
        public int DeclarationLine { get; }
        public int DeclarationColumn { get; }
        public IReadOnlyList<DdfHoverReference> References { get; }
        public int ReferenceCount => References.Count;

        public string ToDisplayText()
        {
            var lines = new List<string> { Name + "  ·  " + Kind };
            if (!string.IsNullOrEmpty(Signature)) lines.Add("Firma: " + Signature);
            if (!string.IsNullOrEmpty(TypeName)) lines.Add("Tipo: " + TypeName);
            if (!string.IsNullOrEmpty(Origin)) lines.Add("Origine: " + Origin);
            if (DeclarationLine > 0)
                lines.Add("Dichiarazione: riga " + DeclarationLine + ", colonna " + DeclarationColumn);
            if (ReferenceCount > 0)
            {
                string principal = string.Join("; ", References.Take(3));
                lines.Add("Riferimenti: " + ReferenceCount + (principal.Length == 0 ? string.Empty : " — " + principal));
            }
            else if (DeclarationLine > 0)
            {
                lines.Add("Riferimenti: nessuno");
            }
            if (!string.IsNullOrEmpty(Documentation)) lines.Add(Documentation);
            return string.Join("\n", lines);
        }
    }

    public static class DdfHoverService
    {
        public static DdfHoverInfo CreateForSymbol(
            DdfDocumentSymbol symbol,
            DdfSemanticModel semanticModel,
            string source,
            string origin,
            string calculatedType = null)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            source = source ?? string.Empty;
            DdfDocumentSymbol semanticSymbol = FindSemanticSymbol(symbol, semanticModel);
            IReadOnlyList<DdfSymbolOccurrence> occurrences = semanticSymbol == null || semanticModel == null
                ? new List<DdfSymbolOccurrence>().AsReadOnly()
                : semanticModel.FindOccurrences(semanticSymbol);
            TextPosition declaration = GetPosition(source, symbol.SelectionStart);
            var references = occurrences
                .Where(occurrence => !occurrence.IsDeclaration)
                .Select(occurrence => GetPosition(source, occurrence.Start))
                .Select(position => new DdfHoverReference(position.Line, position.Column))
                .ToList()
                .AsReadOnly();
            string typeName = string.IsNullOrEmpty(calculatedType) ? GetTypeName(symbol) : calculatedType;
            return new DdfHoverInfo(
                symbol.Name,
                GetKindLabel(symbol.Kind),
                typeName,
                GetSignature(symbol),
                origin,
                ExtractDocumentation(source, symbol.Start) ?? GetFallbackDocumentation(symbol.Kind),
                declaration.Line,
                declaration.Column,
                references);
        }

        public static DdfHoverInfo CreateForStandardFunction(DdfStandardFunction function)
        {
            if (function == null) throw new ArgumentNullException(nameof(function));
            return new DdfHoverInfo(
                function.Name,
                "funzione standard",
                function.ReturnType,
                function.Signature,
                "libreria standard",
                function.Documentation,
                0,
                0,
                new List<DdfHoverReference>().AsReadOnly());
        }

        public static DdfHoverInfo CreateForType(string typeName)
        {
            return new DdfHoverInfo(
                typeName,
                "espressione",
                typeName,
                string.Empty,
                "tipo calcolato",
                "Tipo dedotto dal controllo semantico dell'espressione.",
                0,
                0,
                new List<DdfHoverReference>().AsReadOnly());
        }

        private static DdfDocumentSymbol FindSemanticSymbol(DdfDocumentSymbol symbol, DdfSemanticModel model)
        {
            if (model == null) return null;
            DdfSymbolOccurrence declaration = model.Occurrences.FirstOrDefault(occurrence =>
                occurrence.IsDeclaration && occurrence.Start == symbol.SelectionStart &&
                string.Equals(occurrence.Symbol.Name, symbol.Name, StringComparison.Ordinal));
            return declaration?.Symbol;
        }

        private static string GetSignature(DdfDocumentSymbol symbol)
        {
            if (symbol.Kind != DdfSymbolKind.Function) return string.Empty;
            string returnType = GetTypeName(symbol);
            string parameters = string.Join(", ", symbol.Children
                .Where(child => child.Kind == DdfSymbolKind.Parameter)
                .Select(parameter => parameter.Detail + " " + parameter.Name));
            return symbol.Name + "(" + parameters + ") out " + returnType;
        }

        private static string GetTypeName(DdfDocumentSymbol symbol)
        {
            if (symbol.Kind == DdfSymbolKind.Structure) return symbol.Name;
            if (symbol.Kind != DdfSymbolKind.Function) return symbol.Detail;
            int arrow = symbol.Detail.LastIndexOf('→');
            return arrow < 0 ? string.Empty : symbol.Detail.Substring(arrow + 1).Trim();
        }

        private static string GetKindLabel(DdfSymbolKind kind)
        {
            switch (kind)
            {
                case DdfSymbolKind.Library: return "libreria";
                case DdfSymbolKind.Structure: return "struttura";
                case DdfSymbolKind.Function: return "funzione";
                case DdfSymbolKind.Parameter: return "parametro";
                case DdfSymbolKind.Field: return "campo";
                default: return "variabile";
            }
        }

        private static string GetFallbackDocumentation(DdfSymbolKind kind)
        {
            switch (kind)
            {
                case DdfSymbolKind.Library: return "Direttiva di libreria DDF.";
                case DdfSymbolKind.Structure: return "Struttura dati dichiarata nel sorgente DDF.";
                case DdfSymbolKind.Function: return "Funzione dichiarata nel sorgente DDF.";
                case DdfSymbolKind.Parameter: return "Parametro della funzione corrente.";
                case DdfSymbolKind.Field: return "Campo di una struttura DDF.";
                default: return "Variabile visibile nel relativo ambito DDF.";
            }
        }

        private static string ExtractDocumentation(string source, int declarationStart)
        {
            if (declarationStart <= 0 || declarationStart > source.Length) return null;
            int lineStart = source.LastIndexOf('\n', Math.Max(0, declarationStart - 1));
            if (lineStart < 0) return null;
            int cursor = lineStart;
            var lines = new List<string>();
            while (cursor > 0)
            {
                int previousEnd = cursor;
                if (previousEnd > 0 && source[previousEnd - 1] == '\r') previousEnd--;
                int previousStart = source.LastIndexOf('\n', Math.Max(0, cursor - 1));
                previousStart = previousStart < 0 ? 0 : previousStart + 1;
                string line = source.Substring(previousStart, previousEnd - previousStart).Trim();
                if (!line.StartsWith("///", StringComparison.Ordinal)) break;
                lines.Insert(0, line.Substring(3).Trim());
                if (previousStart == 0) break;
                cursor = previousStart - 1;
            }
            return lines.Count == 0 ? null : string.Join(" ", lines);
        }

        private static TextPosition GetPosition(string source, int offset)
        {
            int line = 1;
            int column = 1;
            for (int index = 0; index < Math.Min(offset, source.Length); index++)
            {
                if (source[index] == '\n') { line++; column = 1; }
                else if (source[index] != '\r') column++;
            }
            return new TextPosition(line, column);
        }

        private sealed class TextPosition
        {
            public TextPosition(int line, int column) { Line = line; Column = column; }
            public int Line { get; }
            public int Column { get; }
        }
    }
}
