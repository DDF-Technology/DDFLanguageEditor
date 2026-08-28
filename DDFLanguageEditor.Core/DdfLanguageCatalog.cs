namespace DDFLanguageEditor.Core
{
    public static class DdfLanguageCatalog
    {
        public static DdfLanguageDefinition Default { get; } = CreateDefault();

        private static DdfLanguageDefinition CreateDefault()
        {
            return new DdfLanguageDefinition(
                new[]
                {
                    Type("int"), Type("float"), Type("char"), Type("bool"),
                    Type("void"), Type("string"), Type("dict"),
                    Type("struct", DdfKeywordRole.Structure),
                    Function("ret", DdfKeywordRole.Return),
                    Function("brk", DdfKeywordRole.Break),
                    Function("end", DdfKeywordRole.End),
                    Function("out", DdfKeywordRole.ReturnTypeMarker),
                    Control("if", DdfKeywordRole.If),
                    Control("while", DdfKeywordRole.While),
                    Control("do", DdfKeywordRole.Do),
                    Control("for", DdfKeywordRole.For)
                },
                new[] { "true", "false", "True", "False" },
                new[]
                {
                    Binary("<<", 1, true, DdfOperatorRole.DeclarationInitializer),
                    Binary(">>", 1, true),
                    Binary("||", 2),
                    Binary("|&|", 3),
                    Binary("&", 4),
                    Binary("<", 5), Binary("<=", 5), Binary(">", 5), Binary(">=", 5),
                    Binary("==", 5), Binary("!!", 5), Binary(">><<", 5), Binary("<<>>", 5),
                    PrefixBinary("+", 6), PrefixBinary("-", 6),
                    Binary("*", 7), Binary("/", 7),
                    Binary("^", 8, true), Binary("^/", 8, true),
                    Prefix("!"),
                    Postfix("++"), Postfix("--")
                },
                new[] { '{', '}', '(', ')', '[', ']', '.', ',', ';' });
        }

        private static DdfKeywordDefinition Type(string text, DdfKeywordRole role = DdfKeywordRole.None)
        {
            return new DdfKeywordDefinition(text, DdfTokenKind.DataTypeKeyword, role);
        }

        private static DdfKeywordDefinition Function(string text, DdfKeywordRole role)
        {
            return new DdfKeywordDefinition(text, DdfTokenKind.FunctionKeyword, role);
        }

        private static DdfKeywordDefinition Control(string text, DdfKeywordRole role)
        {
            return new DdfKeywordDefinition(text, DdfTokenKind.ControlFlowKeyword, role);
        }

        private static DdfOperatorDefinition Binary(string text, int precedence, bool rightAssociative = false, DdfOperatorRole role = DdfOperatorRole.None)
        {
            return new DdfOperatorDefinition(
                text,
                precedence,
                rightAssociative ? DdfOperatorAssociativity.Right : DdfOperatorAssociativity.Left,
                role: role);
        }

        private static DdfOperatorDefinition PrefixBinary(string text, int precedence)
        {
            return new DdfOperatorDefinition(text, precedence, prefixPrecedence: 9);
        }

        private static DdfOperatorDefinition Prefix(string text)
        {
            return new DdfOperatorDefinition(text, prefixPrecedence: 9);
        }

        private static DdfOperatorDefinition Postfix(string text)
        {
            return new DdfOperatorDefinition(text, postfixPrecedence: 10);
        }
    }
}
