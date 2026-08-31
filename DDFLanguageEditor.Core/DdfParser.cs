using System;
using System.Collections.Generic;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfParser
    {
        private readonly string text;
        private readonly DdfLanguageDefinition language;
        private readonly List<DdfToken> tokens;
        private readonly List<DdfDiagnostic> diagnostics = new List<DdfDiagnostic>();
        private int position;
        private int activeBraceDepth;

        private DdfParser(string text, DdfLexResult lexResult, DdfLanguageDefinition language)
        {
            this.text = text;
            this.language = language;
            tokens = new List<DdfToken>();
            foreach (DdfToken token in lexResult.Tokens)
            {
                if (token.Kind != DdfTokenKind.LineComment && token.Kind != DdfTokenKind.BlockComment)
                {
                    tokens.Add(token);
                }
            }
        }

        public static DdfParseResult Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return Parse(text, DdfLanguageCatalog.Default);
        }

        public static DdfParseResult Parse(string text, DdfLanguageDefinition language)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (language == null) throw new ArgumentNullException(nameof(language));
            return Parse(text, DdfLexer.Lex(text, language), language);
        }

        public static DdfParseResult Parse(string text, DdfLexResult lexResult)
        {
            return Parse(text, lexResult, DdfLanguageCatalog.Default);
        }

        public static DdfParseResult Parse(string text, DdfLexResult lexResult, DdfLanguageDefinition language)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (lexResult == null) throw new ArgumentNullException(nameof(lexResult));
            if (language == null) throw new ArgumentNullException(nameof(language));

            var parser = new DdfParser(text, lexResult, language);
            CompilationUnitSyntax root = parser.ParseCompilationUnit();
            return new DdfParseResult(root, lexResult.Diagnostics, parser.diagnostics.AsReadOnly());
        }

        private CompilationUnitSyntax ParseCompilationUnit()
        {
            var members = new List<MemberSyntax>();
            while (Current != null)
            {
                int before = position;
                members.Add(ParseMember());
                EnsureProgress(before);
            }

            return new CompilationUnitSyntax(members.AsReadOnly(), text.Length);
        }

        private MemberSyntax ParseMember()
        {
            if (Current.Kind == DdfTokenKind.LibraryDirective)
            {
                DdfToken token = NextToken();
                return new LibraryDirectiveSyntax(TokenText(token), token.Start, token.Length);
            }

            if (IsKeyword(DdfKeywordRole.Structure))
            {
                return ParseStructDeclaration();
            }

            if (Current.Kind == DdfTokenKind.Identifier && IsText(Peek(1), "("))
            {
                return ParseFunctionDeclaration();
            }

            return new GlobalStatementSyntax(ParseStatement());
        }

        private StructDeclarationSyntax ParseStructDeclaration()
        {
            int start = Current.Start;
            NextToken();
            string name = MatchIdentifier(out int nameStart, out int nameLength);
            int openBraceStart = CurrentStart;
            bool hasOpenBrace = IsText("{");
            MatchText("{", start);
            if (hasOpenBrace) activeBraceDepth++;
            var fields = new List<VariableDeclarationStatementSyntax>();
            while (Current != null && !IsText("}"))
            {
                int before = position;
                if (LooksLikeVariableDeclaration())
                {
                    fields.Add(ParseVariableDeclaration(true));
                }
                else
                {
                    Report("DDF101", "In una struttura è attesa una dichiarazione di campo.");
                    SynchronizeStatement();
                }

                EnsureProgress(before);
            }

            int end = MatchClosingBrace(openBraceStart, hasOpenBrace, out int closeBraceStart, out bool hasCloseBrace);
            if (hasOpenBrace) activeBraceDepth--;
            return new StructDeclarationSyntax(name, nameStart, nameLength, openBraceStart, closeBraceStart, hasCloseBrace, fields.AsReadOnly(), start, Math.Max(0, end - start));
        }

        private FunctionDeclarationSyntax ParseFunctionDeclaration()
        {
            int start = Current.Start;
            DdfToken nameToken = NextToken();
            string name = TokenText(nameToken);
            MatchText("(");
            var parameters = new List<ParameterSyntax>();
            while (Current != null && !IsText(")"))
            {
                int parameterStart = Current.Start;
                TypeReferenceSyntax type = ParseType();
                string parameterName = MatchIdentifier(out int parameterNameStart, out int parameterNameLength);
                parameters.Add(new ParameterSyntax(type, parameterName, parameterNameStart, parameterNameLength, parameterStart, Math.Max(0, PreviousEnd - parameterStart)));
                if (!TryMatchText(",")) break;
            }

            MatchText(")");
            MatchKeyword(DdfKeywordRole.ReturnTypeMarker);
            TypeReferenceSyntax returnType = ParseType();
            BlockStatementSyntax body = ParseBlockStatement(start);
            return new FunctionDeclarationSyntax(name, nameToken.Start, nameToken.Length, parameters.AsReadOnly(), returnType, body, start, Math.Max(0, body.End - start));
        }

        private TypeReferenceSyntax ParseType()
        {
            int start = CurrentStart;
            string name;
            if (Current != null && (Current.Kind == DdfTokenKind.DataTypeKeyword || Current.Kind == DdfTokenKind.Identifier))
            {
                name = TokenText(NextToken());
            }
            else
            {
                Report("DDF104", "Tipo atteso.");
                name = string.Empty;
            }

            var lengths = new List<ExpressionSyntax>();
            while (TryMatchText("["))
            {
                ExpressionSyntax length = IsText("]") ? null : ParseExpression();
                lengths.Add(length);
                MatchText("]");
            }

            return new TypeReferenceSyntax(name, lengths.AsReadOnly(), start, Math.Max(0, PreviousEnd - start));
        }

        private StatementSyntax ParseStatement()
        {
            if (IsText("{")) return ParseBlockStatement();
            if (IsKeyword(DdfKeywordRole.If)) return ParseIfStatement();
            if (IsKeyword(DdfKeywordRole.While)) return ParseWhileStatement();
            if (IsKeyword(DdfKeywordRole.Do)) return ParseDoWhileStatement();
            if (IsKeyword(DdfKeywordRole.For)) return ParseForStatement();
            if (IsKeyword(DdfKeywordRole.Return)) return ParseReturnStatement();
            if (IsKeyword(DdfKeywordRole.Break)) return ParseSimpleStatement(true);
            if (IsKeyword(DdfKeywordRole.End)) return ParseSimpleStatement(false);
            if (LooksLikeVariableDeclaration()) return ParseVariableDeclaration(true);
            return ParseExpressionStatement(true);
        }

        private BlockStatementSyntax ParseBlockStatement(int? ownerStart = null)
        {
            int start = CurrentStart;
            int openBraceStart = CurrentStart;
            bool hasOpenBrace = IsText("{");
            MatchText("{", ownerStart ?? start);
            if (hasOpenBrace) activeBraceDepth++;
            var statements = new List<StatementSyntax>();
            while (Current != null && !IsText("}"))
            {
                int before = position;
                statements.Add(ParseStatement());
                EnsureProgress(before);
            }

            int end = MatchClosingBrace(openBraceStart, hasOpenBrace, out int closeBraceStart, out bool hasCloseBrace);
            if (hasOpenBrace) activeBraceDepth--;
            return new BlockStatementSyntax(statements.AsReadOnly(), closeBraceStart, hasCloseBrace, start, Math.Max(0, end - start));
        }

        private VariableDeclarationStatementSyntax ParseVariableDeclaration(bool consumeSemicolon)
        {
            int start = CurrentStart;
            TypeReferenceSyntax type = ParseType();
            string name = MatchIdentifier(out int nameStart, out int nameLength);
            ExpressionSyntax initializer = null;
            if (TryMatchOperator(DdfOperatorRole.DeclarationInitializer)) initializer = ParseExpression();
            int end = consumeSemicolon ? MatchText(";") : PreviousEnd;
            return new VariableDeclarationStatementSyntax(type, name, nameStart, nameLength, initializer, start, Math.Max(0, end - start));
        }

        private IfStatementSyntax ParseIfStatement()
        {
            int start = NextToken().Start;
            MatchText("(");
            ExpressionSyntax condition = ParseExpression();
            MatchText(")");
            StatementSyntax body = ParseStatement();
            return new IfStatementSyntax(condition, body, start, Math.Max(0, body.End - start));
        }

        private WhileStatementSyntax ParseWhileStatement()
        {
            int start = NextToken().Start;
            MatchText("(");
            ExpressionSyntax condition = ParseExpression();
            MatchText(")");
            StatementSyntax body = ParseStatement();
            return new WhileStatementSyntax(condition, body, start, Math.Max(0, body.End - start));
        }

        private DoWhileStatementSyntax ParseDoWhileStatement()
        {
            int start = NextToken().Start;
            StatementSyntax body = ParseStatement();
            MatchKeyword(DdfKeywordRole.While);
            MatchText("(");
            ExpressionSyntax condition = ParseExpression();
            MatchText(")");
            int end = MatchText(";");
            return new DoWhileStatementSyntax(body, condition, start, Math.Max(0, end - start));
        }

        private ForStatementSyntax ParseForStatement()
        {
            int start = NextToken().Start;
            MatchText("(");
            StatementSyntax initializer = null;
            if (!IsText(";"))
            {
                initializer = LooksLikeVariableDeclaration()
                    ? (StatementSyntax)ParseVariableDeclaration(true)
                    : ParseExpressionStatement(true);
            }
            else
            {
                MatchText(";");
            }

            ExpressionSyntax condition = IsText(";") ? null : ParseExpression();
            MatchText(";");
            ExpressionSyntax increment = IsText(")") ? null : ParseExpression();
            MatchText(")");
            StatementSyntax body = ParseStatement();
            return new ForStatementSyntax(initializer, condition, increment, body, start, Math.Max(0, body.End - start));
        }

        private ReturnStatementSyntax ParseReturnStatement()
        {
            int start = NextToken().Start;
            ExpressionSyntax expression = IsText(";") ? null : ParseExpression();
            int end = MatchText(";");
            return new ReturnStatementSyntax(expression, start, Math.Max(0, end - start));
        }

        private StatementSyntax ParseSimpleStatement(bool isBreak)
        {
            int start = NextToken().Start;
            int end = MatchText(";");
            return isBreak
                ? (StatementSyntax)new BreakStatementSyntax(start, Math.Max(0, end - start))
                : new EndStatementSyntax(start, Math.Max(0, end - start));
        }

        private ExpressionStatementSyntax ParseExpressionStatement(bool consumeSemicolon)
        {
            int start = CurrentStart;
            ExpressionSyntax expression = ParseExpression();
            int end = consumeSemicolon ? MatchText(";") : expression.End;
            return new ExpressionStatementSyntax(expression, start, Math.Max(0, end - start));
        }

        private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
        {
            int start = CurrentStart;
            ExpressionSyntax left;
            int unaryPrecedence = GetUnaryPrecedence(CurrentText);
            if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
            {
                string operatorText = TokenText(NextToken());
                ExpressionSyntax operand = ParseExpression(unaryPrecedence);
                left = new UnaryExpressionSyntax(operatorText, operand, start, Math.Max(0, operand.End - start));
            }
            else
            {
                left = ParsePrimaryExpression();
            }

            left = ParsePostfixExpression(left);
            while (Current != null)
            {
                string operatorText = CurrentText;
                int precedence = GetBinaryPrecedence(operatorText);
                if (precedence == 0 || precedence < parentPrecedence) break;

                NextToken();
                int nextPrecedence = IsRightAssociative(operatorText) ? precedence : precedence + 1;
                ExpressionSyntax right = ParseExpression(nextPrecedence);
                left = new BinaryExpressionSyntax(left, operatorText, right, left.Start, Math.Max(0, right.End - left.Start));
            }

            return left;
        }

        private ExpressionSyntax ParsePrimaryExpression()
        {
            if (Current == null || IsExpressionTerminator(CurrentText))
            {
                int at = CurrentStart;
                Report("DDF103", "Espressione attesa.");
                return new MissingExpressionSyntax(at);
            }

            if (TryMatchText("("))
            {
                int start = tokens[position - 1].Start;
                ExpressionSyntax expression = ParseExpression();
                int end = MatchText(")");
                return new ParenthesizedExpressionSyntax(expression, start, Math.Max(0, end - start));
            }

            DdfToken token = Current;
            if (token.Kind == DdfTokenKind.Identifier)
            {
                NextToken();
                return new NameExpressionSyntax(TokenText(token), token.Start, token.Length);
            }

            if (token.Kind == DdfTokenKind.NumberLiteral || token.Kind == DdfTokenKind.StringLiteral || token.Kind == DdfTokenKind.BooleanLiteral)
            {
                NextToken();
                return new LiteralExpressionSyntax(TokenText(token), token.Start, token.Length);
            }

            Report("DDF101", "Token inatteso nell'espressione: '" + TokenText(token) + "'.");
            NextToken();
            return new MissingExpressionSyntax(token.Start);
        }

        private ExpressionSyntax ParsePostfixExpression(ExpressionSyntax expression)
        {
            while (Current != null)
            {
                if (TryGetCurrentOperator(out DdfOperatorDefinition postfix) && postfix.IsPostfix)
                {
                    DdfToken op = NextToken();
                    expression = new PostfixExpressionSyntax(expression, TokenText(op), expression.Start, op.End - expression.Start);
                    continue;
                }

                if (TryMatchText("("))
                {
                    var arguments = new List<ExpressionSyntax>();
                    while (Current != null && !IsText(")"))
                    {
                        arguments.Add(ParseExpression());
                        if (!TryMatchText(",")) break;
                    }

                    int end = MatchText(")");
                    expression = new CallExpressionSyntax(expression, arguments.AsReadOnly(), expression.Start, Math.Max(0, end - expression.Start));
                    continue;
                }

                if (TryMatchText("["))
                {
                    ExpressionSyntax index = ParseExpression();
                    int end = MatchText("]");
                    expression = new IndexExpressionSyntax(expression, index, expression.Start, Math.Max(0, end - expression.Start));
                    continue;
                }

                if (TryMatchText("."))
                {
                    string memberName = MatchIdentifier(out int memberStart, out int memberLength);
                    expression = new MemberAccessExpressionSyntax(
                        expression,
                        memberName,
                        memberStart,
                        memberLength,
                        expression.Start,
                        Math.Max(0, PreviousEnd - expression.Start));
                    continue;
                }

                break;
            }

            return expression;
        }

        private bool LooksLikeVariableDeclaration()
        {
            if (Current == null) return false;
            if (Current.Kind == DdfTokenKind.DataTypeKeyword && !IsKeyword(DdfKeywordRole.Structure)) return true;
            if (Current.Kind != DdfTokenKind.Identifier) return false;

            int lookahead = 1;
            while (IsText(Peek(lookahead), "["))
            {
                int depth = 0;
                do
                {
                    DdfToken token = Peek(lookahead++);
                    if (token == null) return false;
                    if (IsText(token, "[")) depth++;
                    if (IsText(token, "]")) depth--;
                }
                while (depth > 0);
            }

            DdfToken name = Peek(lookahead);
            return name != null && name.Kind == DdfTokenKind.Identifier;
        }

        private string MatchIdentifier(out int nameStart, out int nameLength)
        {
            if (Current != null && Current.Kind == DdfTokenKind.Identifier)
            {
                DdfToken token = NextToken();
                nameStart = token.Start;
                nameLength = token.Length;
                return TokenText(token);
            }

            Report("DDF105", "Identificatore atteso.");
            nameStart = CurrentStart;
            nameLength = 0;
            return string.Empty;
        }

        private int MatchText(string expected, int? contextStart = null)
        {
            if (TryMatchText(expected)) return PreviousEnd;
            Report("DDF102", "Token '" + expected + "' atteso.", PreviousEnd, contextStart);
            return CurrentStart;
        }

        private int MatchClosingBrace(
            int openBraceStart,
            bool hasOpenBrace,
            out int closeBraceStart,
            out bool hasCloseBrace)
        {
            closeBraceStart = CurrentStart;
            hasCloseBrace = IsText("}") &&
                            (!hasOpenBrace || IsCompatibleClosingBrace(openBraceStart, CurrentStart));
            if (hasCloseBrace) return NextToken().End;
            Report(
                "DDF102",
                "Token '}' atteso.",
                PreviousEnd,
                hasOpenBrace ? (int?)openBraceStart : null);
            return CurrentStart;
        }

        private bool IsCompatibleClosingBrace(int openBraceStart, int closeBraceStart)
        {
            if (CountRemainingClosingBraces() >= activeBraceDepth) return true;
            int openLineStart = FindLineStart(openBraceStart);
            int closeLineStart = FindLineStart(closeBraceStart);
            if (openLineStart == closeLineStart) return true;
            return CountIndentation(closeLineStart) >= CountIndentation(openLineStart);
        }

        private int CountRemainingClosingBraces()
        {
            int count = 0;
            for (int index = position; index < tokens.Count; index++)
                if (TokenText(tokens[index]) == "}") count++;
            return count;
        }

        private int FindLineStart(int sourcePosition)
        {
            int start = Math.Min(sourcePosition, text.Length);
            while (start > 0 && text[start - 1] != '\n') start--;
            return start;
        }

        private int CountIndentation(int lineStart)
        {
            int position = lineStart;
            while (position < text.Length && (text[position] == ' ' || text[position] == '\t')) position++;
            return position - lineStart;
        }

        private int MatchKeyword(DdfKeywordRole role)
        {
            if (IsKeyword(role)) return NextToken().End;
            Report("DDF102", "Parola chiave '" + GetKeywordText(role) + "' attesa.", PreviousEnd);
            return CurrentStart;
        }

        private bool TryMatchOperator(DdfOperatorRole role)
        {
            if (!TryGetCurrentOperator(out DdfOperatorDefinition definition) || definition.Role != role) return false;
            NextToken();
            return true;
        }

        private bool TryMatchText(string expected)
        {
            if (!IsText(expected)) return false;
            NextToken();
            return true;
        }

        private void SynchronizeStatement()
        {
            while (Current != null && !IsText(";") && !IsText("}")) NextToken();
            if (IsText(";")) NextToken();
        }

        private void EnsureProgress(int before)
        {
            if (position != before || Current == null) return;
            Report("DDF101", "Token inatteso: '" + CurrentText + "'.");
            NextToken();
        }

        private void Report(
            string code,
            string message,
            int? insertionPosition = null,
            int? contextStart = null)
        {
            int sourcePosition = CurrentStart;
            int diagnosticStart = text.Length == 0 ? 0 : Math.Min(sourcePosition, text.Length - 1);
            int length = Current == null ? 1 : Math.Max(1, Current.Length);
            GetLineAndColumn(sourcePosition, out int line, out int column);
            diagnostics.Add(new DdfDiagnostic(
                code,
                message,
                diagnosticStart,
                length,
                line,
                column,
                DdfDiagnosticSeverity.Error,
                insertionPosition ?? sourcePosition,
                contextStart));
        }

        private void GetLineAndColumn(int sourcePosition, out int line, out int column)
        {
            line = 1;
            column = 1;
            int limit = Math.Min(sourcePosition, text.Length);
            for (int index = 0; index < limit; index++)
            {
                if (text[index] == '\n') { line++; column = 1; }
                else if (text[index] != '\r') column++;
            }
        }

        private int GetUnaryPrecedence(string operatorText)
        {
            return language.TryGetOperator(operatorText, out DdfOperatorDefinition definition)
                ? definition.PrefixPrecedence
                : 0;
        }

        private int GetBinaryPrecedence(string operatorText)
        {
            return language.TryGetOperator(operatorText, out DdfOperatorDefinition definition)
                ? definition.BinaryPrecedence
                : 0;
        }

        private bool IsRightAssociative(string operatorText)
        {
            return language.TryGetOperator(operatorText, out DdfOperatorDefinition definition) &&
                   definition.Associativity == DdfOperatorAssociativity.Right;
        }

        private static bool IsExpressionTerminator(string value)
        {
            return value == ";" || value == ")" || value == "]" || value == "}" || value == ",";
        }

        private DdfToken Current => Peek(0);
        private string CurrentText => Current == null ? string.Empty : TokenText(Current);
        private int CurrentStart => Current == null ? text.Length : Current.Start;
        private int PreviousEnd => position == 0 ? 0 : tokens[position - 1].End;

        private DdfToken Peek(int offset)
        {
            int index = position + offset;
            return index >= 0 && index < tokens.Count ? tokens[index] : null;
        }

        private DdfToken NextToken()
        {
            DdfToken current = Current;
            if (current != null) position++;
            return current;
        }

        private bool IsText(string value) => IsText(Current, value);
        private bool IsText(DdfToken token, string value) => token != null && string.Equals(TokenText(token), value, StringComparison.Ordinal);
        private string TokenText(DdfToken token) => text.Substring(token.Start, token.Length);

        private bool IsKeyword(DdfKeywordRole role)
        {
            return Current != null &&
                   language.TryGetKeyword(CurrentText, out DdfKeywordDefinition definition) &&
                   definition.Role == role;
        }

        private string GetKeywordText(DdfKeywordRole role)
        {
            foreach (DdfKeywordDefinition keyword in language.Keywords)
            {
                if (keyword.Role == role) return keyword.Text;
            }

            return role.ToString();
        }

        private bool TryGetCurrentOperator(out DdfOperatorDefinition definition)
        {
            definition = null;
            return Current != null && language.TryGetOperator(CurrentText, out definition);
        }
    }
}
