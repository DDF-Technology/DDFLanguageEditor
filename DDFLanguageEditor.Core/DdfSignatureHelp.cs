using System;
using System.Collections.Generic;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfSignatureParameter
    {
        public DdfSignatureParameter(string name, string typeName)
        {
            Name = name ?? string.Empty;
            TypeName = typeName ?? string.Empty;
        }

        public string Name { get; }
        public string TypeName { get; }
        public string DisplayText => string.IsNullOrEmpty(Name) ? TypeName : TypeName + " " + Name;
    }

    public sealed class DdfSignatureInformation
    {
        public DdfSignatureInformation(
            string name,
            string returnType,
            IReadOnlyList<DdfSignatureParameter> parameters,
            string origin)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A function name is required.", nameof(name));
            Name = name;
            ReturnType = returnType ?? string.Empty;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Origin = origin ?? string.Empty;
        }

        public string Name { get; }
        public string ReturnType { get; }
        public IReadOnlyList<DdfSignatureParameter> Parameters { get; }
        public string Origin { get; }
        public string Signature => Name + "(" + string.Join(", ", Parameters.Select(parameter => parameter.DisplayText)) +
                                   ") out " + ReturnType;
    }

    public sealed class DdfSignatureHelpResult
    {
        public DdfSignatureHelpResult(int callStart, int activeParameter, DdfSignatureInformation signature)
        {
            if (callStart < 0) throw new ArgumentOutOfRangeException(nameof(callStart));
            if (activeParameter < 0) throw new ArgumentOutOfRangeException(nameof(activeParameter));
            CallStart = callStart;
            ActiveParameter = activeParameter;
            Signature = signature ?? throw new ArgumentNullException(nameof(signature));
        }

        public int CallStart { get; }
        public int ActiveParameter { get; }
        public DdfSignatureInformation Signature { get; }
        public DdfSignatureParameter ActiveParameterInformation =>
            ActiveParameter < Signature.Parameters.Count ? Signature.Parameters[ActiveParameter] : null;
    }

    public static class DdfSignatureHelpService
    {
        public static DdfSignatureHelpResult GetSignatureHelp(
            string source,
            int position,
            IEnumerable<CompilationUnitSyntax> externalRoots = null,
            DdfLanguageDefinition language = null)
        {
            source = source ?? string.Empty;
            if (position < 0 || position > source.Length) throw new ArgumentOutOfRangeException(nameof(position));
            language = language ?? DdfLanguageCatalog.Default;

            DdfLexResult lexResult = DdfLexer.Lex(source, language);
            if (IsInCommentOrLibrary(lexResult, position)) return null;

            var stack = new List<CallFrame>();
            DdfToken previous = null;
            foreach (DdfToken token in lexResult.Tokens)
            {
                if (token.Start >= position) break;
                string text = source.Substring(token.Start, token.Length);
                if (token.Kind == DdfTokenKind.Punctuation && text == "(")
                {
                    bool isCall = previous != null && previous.Kind == DdfTokenKind.Identifier;
                    stack.Add(new CallFrame(
                        token.Start,
                        isCall ? source.Substring(previous.Start, previous.Length) : string.Empty,
                        isCall ? previous.Start : -1));
                }
                else if (token.Kind == DdfTokenKind.Punctuation && text == ",")
                {
                    if (stack.Count > 0 && stack[stack.Count - 1].IsCall)
                        stack[stack.Count - 1].ActiveParameter++;
                }
                else if (token.Kind == DdfTokenKind.Punctuation && text == ")")
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                }

                previous = token;
            }

            CallFrame activeCall = stack.LastOrDefault(frame => frame.IsCall);
            if (activeCall == null) return null;

            CompilationUnitSyntax root = DdfParser.Parse(source, lexResult, language).Root;
            FunctionDeclarationSyntax local = FindFunction(root, activeCall.Name);
            if (local != null && local.NameStart == activeCall.NameStart) return null;
            if (local != null)
                return CreateResult(activeCall, local, "documento corrente");

            if (externalRoots != null)
            {
                foreach (CompilationUnitSyntax externalRoot in externalRoots)
                {
                    FunctionDeclarationSyntax external = FindFunction(externalRoot, activeCall.Name);
                    if (external != null) return CreateResult(activeCall, external, "workspace");
                }
            }

            if (DdfRuntimeCatalog.TryGetStandardFunction(activeCall.Name, out DdfStandardFunction standard))
            {
                var parameters = standard.ParameterTypes
                    .Select(type => new DdfSignatureParameter(string.Empty, type))
                    .ToList()
                    .AsReadOnly();
                return new DdfSignatureHelpResult(
                    activeCall.OpenParenthesis,
                    activeCall.ActiveParameter,
                    new DdfSignatureInformation(standard.Name, standard.ReturnType, parameters, "libreria standard"));
            }

            return null;
        }

        private static DdfSignatureHelpResult CreateResult(CallFrame call, FunctionDeclarationSyntax function, string origin)
        {
            var parameters = function.Parameters
                .Select(parameter => new DdfSignatureParameter(parameter.Name, FormatType(parameter.Type)))
                .ToList()
                .AsReadOnly();
            return new DdfSignatureHelpResult(
                call.OpenParenthesis,
                call.ActiveParameter,
                new DdfSignatureInformation(function.Name, FormatType(function.ReturnType), parameters, origin));
        }

        private static FunctionDeclarationSyntax FindFunction(CompilationUnitSyntax root, string name)
        {
            return root?.Members.OfType<FunctionDeclarationSyntax>()
                .FirstOrDefault(function => string.Equals(function.Name, name, StringComparison.Ordinal));
        }

        private static string FormatType(TypeReferenceSyntax type)
        {
            if (type == null || string.IsNullOrWhiteSpace(type.Name)) return "tipo sconosciuto";
            return type.Name + string.Concat(Enumerable.Repeat("[]", type.ArrayLengths.Count));
        }

        private static bool IsInCommentOrLibrary(DdfLexResult lexResult, int position)
        {
            foreach (DdfToken token in lexResult.Tokens)
            {
                if (position <= token.Start || position > token.End) continue;
                return token.Kind == DdfTokenKind.LineComment ||
                       token.Kind == DdfTokenKind.BlockComment ||
                       token.Kind == DdfTokenKind.LibraryDirective;
            }

            return false;
        }

        private sealed class CallFrame
        {
            public CallFrame(int openParenthesis, string name, int nameStart)
            {
                OpenParenthesis = openParenthesis;
                Name = name;
                NameStart = nameStart;
            }

            public int OpenParenthesis { get; }
            public string Name { get; }
            public int NameStart { get; }
            public int ActiveParameter { get; set; }
            public bool IsCall => NameStart >= 0;
        }
    }
}
