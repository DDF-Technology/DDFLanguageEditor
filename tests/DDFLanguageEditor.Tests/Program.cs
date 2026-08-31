using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Tests
{
    internal static class Program
    {
        private static readonly List<string> Failures = new List<string>();
        private static int passedTests;

        private static int Main()
        {
            Run("classifies complete line comments", ClassifiesCompleteLineComments);
            Run("classifies complete numbers", ClassifiesCompleteNumbers);
            Run("protects strings from nested classification", ProtectsStrings);
            Run("classifies unterminated block comments", ClassifiesUnterminatedBlockComments);
            Run("prefers longest operators", PrefersLongestOperators);
            Run("inserts four spaces for Tab", InsertsTab);
            Run("indents and outdents multiline selections", IndentsAndOutdentsSelection);
            Run("indents after an opening block", IndentsAfterOpeningBlock);
            Run("replaces the selection on Enter", ReplacesSelectionOnEnter);
            Run("aligns a closing brace with its block", AlignsClosingBraceWithItsBlock);
            Run("ignores braces in comments and strings while aligning", IgnoresProtectedBracesWhileAligning);
            Run("inserts and skips paired characters", InsertsAndSkipsPairedCharacters);
            Run("handles enter and backspace inside pairs", HandlesEnterAndBackspaceInsidePairs);
            Run("toggles line comments while preserving indentation", TogglesLineComments);
            Run("duplicates selected lines", DuplicatesSelectedLines);
            Run("moves selected lines up and down", MovesSelectedLines);
            Run("deletes complete selected lines", DeletesSelectedLines);
            Run("reindents multiline pasted code", ReindentsMultilinePastedCode);
            Run("round-trips UTF-8 documents", RoundTripsUtf8Documents);
            Run("accepts the optional UTF-8 BOM", AcceptsUtf8Bom);
            Run("rejects invalid UTF-8 documents", RejectsInvalidUtf8Documents);
            Run("tracks document session state", TracksDocumentSessionState);
            Run("tracks independent open document buffers", TracksIndependentOpenDocumentBuffers);
            Run("remaps and validates line breakpoints", RemapsAndValidatesLineBreakpoints);
            Run("deduplicates recent files", DeduplicatesRecentFiles);
            Run("wraps find-next searches", WrapsFindNextSearches);
            Run("replaces all matches", ReplacesAllMatches);
            Run("produces formal tokens", ProducesFormalTokens);
            Run("reports diagnostic positions", ReportsDiagnosticPositions);
            Run("exposes diagnostic severity and hover text", ExposesDiagnosticSeverityAndHoverText);
            Run("offers safe lexical quick fixes", OffersSafeLexicalQuickFixes);
            Run("inserts parser expected tokens", InsertsParserExpectedTokens);
            Run("anchors missing tokens before intervening trivia", AnchorsMissingTokensBeforeTrivia);
            Run("restores mandatory opening braces structurally", RestoresMandatoryOpeningBracesStructurally);
            Run("restores nested closing braces structurally", RestoresNestedClosingBracesStructurally);
            Run("keeps valid braces independent from indentation", KeepsValidBracesIndependentFromIndentation);
            Run("accepts custom quick-fix providers", AcceptsCustomQuickFixProviders);
            Run("matches full lexing after incremental edits", MatchesFullLexingAfterIncrementalEdits);
            Run("relexes across a shared inserted prefix", RelexesAcrossSharedInsertedPrefix);
            Run("survives deterministic dynamic editing", SurvivesDeterministicDynamicEditing);
            Run("accepts the valid lexer corpus", AcceptsValidLexerCorpus);
            Run("reports the invalid lexer corpus", ReportsInvalidLexerCorpus);
            Run("builds an AST for the parser corpus", BuildsAstForParserCorpus);
            Run("honors expression precedence", HonorsExpressionPrecedence);
            Run("honors right associativity", HonorsRightAssociativity);
            Run("recovers from multiple syntax errors", RecoversFromMultipleSyntaxErrors);
            Run("keeps AST spans within the source", KeepsAstSpansWithinSource);
            Run("parses the editor smoke sample", ParsesEditorSmokeSample);
            Run("drives lexing from a language catalog", DrivesLexingFromLanguageCatalog);
            Run("drives parsing from syntax roles", DrivesParsingFromSyntaxRoles);
            Run("rejects duplicate catalog entries", RejectsDuplicateCatalogEntries);
            Run("indexes document symbols hierarchically", IndexesDocumentSymbolsHierarchically);
            Run("builds contextual breadcrumb paths", BuildsContextualBreadcrumbPaths);
            Run("keeps breadcrumb paths usable for incomplete code", KeepsBreadcrumbPathsUsableForIncompleteCode);
            Run("keeps exact symbol selection spans", KeepsExactSymbolSelectionSpans);
            Run("indexes incomplete documents safely", IndexesIncompleteDocumentsSafely);
            Run("resolves symbol declarations and references", ResolvesSymbolDeclarationsAndReferences);
            Run("keeps shadowed symbols in separate scopes", KeepsShadowedSymbolsInSeparateScopes);
            Run("renames only the resolved symbol", RenamesOnlyTheResolvedSymbol);
            Run("reports unresolved and duplicate symbols", ReportsUnresolvedAndDuplicateSymbols);
            Run("validates rename identifiers and keywords", ValidatesRenameIdentifiersAndKeywords);
            Run("renames library names without damaging directives", RenamesLibraryNamesWithoutDamagingDirectives);
            Run("renames structure declarations and type references", RenamesStructureDeclarationsAndTypeReferences);
            Run("indexes DDF workspace documents recursively", IndexesDdfWorkspaceDocumentsRecursively);
            Run("updates an in-memory workspace document", UpdatesInMemoryWorkspaceDocument);
            Run("completes symbols from another workspace document", CompletesSymbolsFromAnotherWorkspaceDocument);
            Run("searches text across workspace documents", SearchesTextAcrossWorkspaceDocuments);
            Run("searches nested workspace symbols", SearchesNestedWorkspaceSymbols);
            Run("creates selective workspace replacement changes", CreatesSelectiveWorkspaceReplacementChanges);
            Run("lists workspace files and symbols for navigation", ListsWorkspaceFilesAndSymbolsForNavigation);
            Run("finds semantic workspace references", FindsSemanticWorkspaceReferences);
            Run("parses line and column navigation", ParsesLineAndColumnNavigation);
            Run("accepts semantically valid typed code", AcceptsSemanticallyValidTypedCode);
            Run("reports incompatible initializers and assignments", ReportsIncompatibleInitializersAndAssignments);
            Run("reports invalid typed operators and conditions", ReportsInvalidTypedOperatorsAndConditions);
            Run("checks function arguments and return values", ChecksFunctionArgumentsAndReturnValues);
            Run("resolves structure members and array element types", ResolvesStructureMembersAndArrayElementTypes);
            Run("reports unknown types and members", ReportsUnknownTypesAndMembers);
            Run("checks function signatures from another document", ChecksFunctionSignaturesFromAnotherDocument);
            Run("reports missing returns and invalid assignment targets", ReportsMissingReturnsAndInvalidAssignmentTargets);
            Run("type-checks the editor smoke sample", TypeChecksEditorSmokeSample);
            Run("executes functions control flow and output", ExecutesFunctionsControlFlowAndOutput);
            Run("executes arrays and structure members", ExecutesArraysAndStructureMembers);
            Run("stops infinite execution at the instruction limit", StopsInfiniteExecutionAtInstructionLimit);
            Run("honors runtime cancellation", HonorsRuntimeCancellation);
            Run("pauses and continues at line breakpoints", PausesAndContinuesAtLineBreakpoints);
            Run("reports runtime failures with source positions", ReportsRuntimeFailuresWithSourcePositions);
            Run("captures navigable DDF runtime stack traces", CapturesNavigableRuntimeStackTraces);
            Run("executes the minimal standard library with input", ExecutesMinimalStandardLibraryWithInput);
            Run("reports invalid standard conversions", ReportsInvalidStandardConversions);
            Run("matches nested delimiters", MatchesNestedDelimiters);
            Run("ignores delimiters in comments and strings", IgnoresDelimitersInCommentsAndStrings);
            Run("handles unmatched delimiters", HandlesUnmatchedDelimiters);
            Run("handles stale delimiter tokens safely", HandlesStaleDelimiterTokensSafely);
            Run("expands selections through syntax levels", ExpandsSelectionsThroughSyntaxLevels);
            Run("expands protected tokens without reading nested delimiters", ExpandsProtectedTokensSafely);
            Run("navigates between matching delimiters", NavigatesBetweenMatchingDelimiters);
            Run("handles incomplete syntax selection safely", HandlesIncompleteSyntaxSelectionSafely);
            Run("finds token occurrences outside protected text", FindsTokenOccurrencesOutsideProtectedText);
            Run("replaces multiple selections in one transformation", ReplacesMultipleSelections);
            Run("deletes at multiple cursors with mapped positions", DeletesAtMultipleCursors);
            Run("applies contextual edits at multiple selections", AppliesContextualEditsAtMultipleSelections);
            Run("derives multiline folding ranges", DerivesMultilineFoldingRanges);
            Run("handles incomplete folding trees", HandlesIncompleteFoldingTrees);
            Run("creates a source-preserving fold projection", CreatesSourcePreservingFoldProjection);
            Run("creates a multi-range fold projection", CreatesMultiRangeFoldProjection);
            Run("completes catalog keywords by prefix", CompletesCatalogKeywordsByPrefix);
            Run("suppresses completion in comments and strings", SuppressesCompletionInProtectedTokens);
            Run("cancels obsolete completion work", CancelsObsoleteCompletionWork);
            Run("offers only visible local symbols", OffersOnlyVisibleLocalSymbols);
            Run("completes known library names", CompletesKnownLibraryNames);
            Run("drives completion from an alternative catalog", DrivesCompletionFromAlternativeCatalog);
            Run("ranks completion by expected boolean type", RanksCompletionByExpectedBooleanType);
            Run("ranks completion by function return type", RanksCompletionByFunctionReturnType);
            Run("recognizes type completion context", RecognizesTypeCompletionContext);
            Run("ranks frequently used symbols before nearby alternatives", RanksFrequentlyUsedSymbols);
            Run("exposes completion category type and origin", ExposesCompletionMetadata);
            Run("offers snippets only in statement context", OffersSnippetsInStatementContext);
            Run("expands snippets with indentation and ordered fields", ExpandsSnippetsWithIndentation);
            Run("shows the active local function parameter", ShowsActiveLocalFunctionParameter);
            Run("resolves standard function signature help", ResolvesStandardFunctionSignatureHelp);
            Run("tracks nested call signature help", TracksNestedCallSignatureHelp);
            Run("resolves workspace function signature help", ResolvesWorkspaceFunctionSignatureHelp);
            Run("suppresses signature help outside calls and declarations", SuppressesSignatureHelpOutsideCalls);
            Run("builds structured hover information", BuildsStructuredHoverInformation);
            Run("maps hover references from an independent symbol index", MapsHoverReferencesFromIndependentIndex);
            Run("documents standard functions in hover", DocumentsStandardFunctionsInHover);
            Run("formats a complete DDF document", FormatsCompleteDocument);
            Run("formats idempotently", FormatsIdempotently);
            Run("preserves comments strings and libraries while formatting", PreservesProtectedTextWhileFormatting);
            Run("formats incomplete source without losing tokens", FormatsIncompleteSourceSafely);
            Run("maps the caret through formatting", MapsCaretThroughFormatting);
            Run("drives formatting from an alternative catalog", DrivesFormattingFromAlternativeCatalog);
            Run("formats control flow calls and arrays", FormatsControlFlowCallsAndArrays);
            Run("separates formatted statements and contexts", SeparatesFormattedStatementsAndContexts);

            if (Failures.Count == 0)
            {
                Console.WriteLine("All " + passedTests + " tests passed.");
                return 0;
            }

            Console.Error.WriteLine(Failures.Count + " test(s) failed:");
            foreach (string failure in Failures)
            {
                Console.Error.WriteLine("- " + failure);
            }

            return 1;
        }

        private static void ClassifiesCompleteLineComments()
        {
            const string text = "int value; // int 42";
            ClassifiedSpan comment = DdfSyntaxClassifier.Classify(text)
                .Single(span => span.Kind == SyntaxKind.Comment);
            Equal("// int 42", Slice(text, comment));
            False(DdfSyntaxClassifier.Classify(text).Any(span => span.Start > comment.Start));
        }

        private static void ClassifiesCompleteNumbers()
        {
            const string text = "int value << 123.45;";
            ClassifiedSpan number = DdfSyntaxClassifier.Classify(text)
                .Single(span => span.Kind == SyntaxKind.Number);
            Equal("123.45", Slice(text, number));
        }

        private static void ProtectsStrings()
        {
            const string text = "\"if // 12\"";
            IReadOnlyList<ClassifiedSpan> spans = DdfSyntaxClassifier.Classify(text);
            Equal(1, spans.Count);
            Equal(SyntaxKind.String, spans[0].Kind);
            Equal(text, Slice(text, spans[0]));
        }

        private static void ClassifiesUnterminatedBlockComments()
        {
            const string text = "/* int value";
            ClassifiedSpan span = DdfSyntaxClassifier.Classify(text).Single();
            Equal(SyntaxKind.Comment, span.Kind);
            Equal(text, Slice(text, span));
        }

        private static void PrefersLongestOperators()
        {
            const string text = "a >><< b <<>> c |&| d";
            string[] operators = DdfSyntaxClassifier.Classify(text)
                .Where(span => span.Kind == SyntaxKind.Operator)
                .Select(span => Slice(text, span))
                .ToArray();
            SequenceEqual(new[] { ">><<", "<<>>", "|&|" }, operators);
        }

        private static void InsertsTab()
        {
            EditorEdit edit = EditorEditing.CreateTabEdit("ab", 1, 0, false);
            Equal("a    b", Apply("ab", edit));
            Equal(5, edit.SelectionStart);
        }

        private static void IndentsAndOutdentsSelection()
        {
            const string source = "one\ntwo";
            EditorEdit indent = EditorEditing.CreateTabEdit(source, 0, source.Length, false);
            string indented = Apply(source, indent);
            Equal("    one\n    two", indented);

            EditorEdit outdent = EditorEditing.CreateTabEdit(
                indented,
                indent.SelectionStart,
                indent.SelectionLength,
                true);
            Equal(source, Apply(indented, outdent));
        }

        private static void IndentsAfterOpeningBlock()
        {
            const string source = "if(condition){";
            EditorEdit edit = EditorEditing.CreateNewLineEdit(source, source.Length, 0);
            Equal("if(condition){\n    ", Apply(source, edit));
            Equal(source.Length + 5, edit.SelectionStart);
        }

        private static void ReplacesSelectionOnEnter()
        {
            const string source = "    old value";
            EditorEdit edit = EditorEditing.CreateNewLineEdit(source, 4, 4);
            Equal("    \n    value", Apply(source, edit));
        }

        private static void AlignsClosingBraceWithItsBlock()
        {
            const string source = "main() out int\n{\n    if(true)\n    {\n        int value;\n            ";
            EditorEdit edit = EditorEditing.CreateClosingBraceEdit(source, source.Length, 0);
            Equal("main() out int\n{\n    if(true)\n    {\n        int value;\n    }", Apply(source, edit));
            Equal(source.LastIndexOf('\n') + 6, edit.SelectionStart);
        }

        private static void IgnoresProtectedBracesWhileAligning()
        {
            const string source = "// { ignored\nmain() out int\n{\n    string text << \"}\";\n        ";
            EditorEdit edit = EditorEditing.CreateClosingBraceEdit(source, source.Length, 0);
            Equal("// { ignored\nmain() out int\n{\n    string text << \"}\";\n}", Apply(source, edit));
        }

        private static void InsertsAndSkipsPairedCharacters()
        {
            EditorEdit parentheses = EditorEditing.CreatePairedCharacterEdit(string.Empty, 0, 0, '(');
            Equal("()", Apply(string.Empty, parentheses));
            Equal(1, parentheses.SelectionStart);

            EditorEdit wrapped = EditorEditing.CreatePairedCharacterEdit("value", 0, 5, '[');
            Equal("[value]", Apply("value", wrapped));
            Equal(1, wrapped.SelectionStart);
            Equal(5, wrapped.SelectionLength);

            EditorEdit skipped = EditorEditing.CreatePairedCharacterEdit("()", 1, 0, ')');
            Equal("()", Apply("()", skipped));
            Equal(2, skipped.SelectionStart);

            EditorEdit escapedQuote = EditorEditing.CreatePairedCharacterEdit("\\", 1, 0, '"');
            Equal("\\\"", Apply("\\", escapedQuote));
        }

        private static void HandlesEnterAndBackspaceInsidePairs()
        {
            EditorEdit newLine = EditorEditing.CreateNewLineEdit("{}", 1, 0);
            Equal("{\n    \n}", Apply("{}", newLine));
            Equal(6, newLine.SelectionStart);

            EditorEdit backspace = EditorEditing.CreatePairedBackspaceEdit("call()", 5, 0);
            Equal("call", Apply("call()", backspace));
            Equal(4, backspace.SelectionStart);
            Equal<EditorEdit>(null, EditorEditing.CreatePairedBackspaceEdit("(value)", 1, 0));
        }

        private static void TogglesLineComments()
        {
            const string source = "    int value;\n    ret value;";
            EditorEdit comment = EditorEditing.CreateToggleLineCommentEdit(source, 0, source.Length);
            string commented = Apply(source, comment);
            Equal("    // int value;\n    // ret value;", commented);
            EditorEdit uncomment = EditorEditing.CreateToggleLineCommentEdit(
                commented, comment.SelectionStart, comment.SelectionLength);
            Equal(source, Apply(commented, uncomment));

            EditorEdit caretLine = EditorEditing.CreateToggleLineCommentEdit("one\n    two", 8, 0);
            Equal("one\n    // two", Apply("one\n    two", caretLine));
            Equal(11, caretLine.SelectionStart);

            EditorEdit shortLines = EditorEditing.CreateToggleLineCommentEdit("a\nb", 0, 3);
            Equal("// a\n// b", Apply("a\nb", shortLines));
            Equal(3, shortLines.SelectionStart);
            Equal(6, shortLines.SelectionLength);
        }

        private static void DuplicatesSelectedLines()
        {
            const string source = "one\ntwo\nthree";
            EditorEdit line = EditorEditing.CreateDuplicateLinesEdit(source, 5, 0);
            Equal("one\ntwo\ntwo\nthree", Apply(source, line));
            Equal(9, line.SelectionStart);

            EditorEdit block = EditorEditing.CreateDuplicateLinesEdit(source, 0, 7);
            Equal("one\ntwo\none\ntwo\nthree", Apply(source, block));
            Equal(8, block.SelectionStart);
            Equal(7, block.SelectionLength);
        }

        private static void MovesSelectedLines()
        {
            const string source = "one\ntwo\nthree";
            EditorEdit up = EditorEditing.CreateMoveLinesEdit(source, 4, 3, true);
            string movedUp = Apply(source, up);
            Equal("two\none\nthree", movedUp);
            Equal(0, up.SelectionStart);
            Equal<EditorEdit>(null, EditorEditing.CreateMoveLinesEdit(movedUp, 0, 3, true));

            EditorEdit down = EditorEditing.CreateMoveLinesEdit(movedUp, 0, 3, false);
            Equal(source, Apply(movedUp, down));
            Equal(4, down.SelectionStart);
        }

        private static void DeletesSelectedLines()
        {
            const string source = "one\ntwo\nthree";
            Equal("one\nthree", Apply(source, EditorEditing.CreateDeleteLinesEdit(source, 5, 0)));
            Equal("one\ntwo", Apply(source, EditorEditing.CreateDeleteLinesEdit(source, 8, 5)));
            Equal(string.Empty, Apply("only", EditorEditing.CreateDeleteLinesEdit("only", 2, 0)));
        }

        private static void ReindentsMultilinePastedCode()
        {
            const string target = "main()\n{\n    \n}";
            const string clipboard = "    if(true)\r\n    {\r\n        ret 1;\r\n    }";
            EditorEdit paste = EditorEditing.CreatePasteEdit(target, 13, 0, clipboard);
            Equal("main()\n{\n    if(true)\n    {\n        ret 1;\n    }\n}", Apply(target, paste));

            EditorEdit inline = EditorEditing.CreatePasteEdit("    value", 9, 0, "one\ntwo");
            Equal("    valueone\n    two", Apply("    value", inline));
        }

        private static void RoundTripsUtf8Documents()
        {
            string directory = CreateTemporaryDirectory();
            string path = Path.Combine(directory, "unicode.ddf");
            const string first = "string saluto << \"Ciao, 世界 🌍\";\r\nint valore << 42;";
            const string second = "// seconda versione\nfloat valore << 1.5;";
            try
            {
                DdfDocumentFile.Save(path, first);
                Equal(first, DdfDocumentFile.Load(path));
                byte[] bytes = File.ReadAllBytes(path);
                False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

                DdfDocumentFile.Save(path, second);
                Equal(second, DdfDocumentFile.Load(path));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void RejectsInvalidUtf8Documents()
        {
            string directory = CreateTemporaryDirectory();
            string path = Path.Combine(directory, "invalid.ddf");
            try
            {
                File.WriteAllBytes(path, new byte[] { 0xC3, 0x28 });
                Throws<DecoderFallbackException>(() => DdfDocumentFile.Load(path));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void AcceptsUtf8Bom()
        {
            string directory = CreateTemporaryDirectory();
            string path = Path.Combine(directory, "bom.ddf");
            const string content = "string value << \"UTF-8\";";
            try
            {
                byte[] preamble = Encoding.UTF8.GetPreamble();
                byte[] payload = Encoding.UTF8.GetBytes(content);
                File.WriteAllBytes(path, preamble.Concat(payload).ToArray());
                Equal(content, DdfDocumentFile.Load(path));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void TracksDocumentSessionState()
        {
            var session = new DocumentSession();
            session.SetUntitled();
            Equal("Senza titolo.ddf", session.DisplayName);
            False(session.HasPath);
            False(session.IsDirty);

            session.MarkDirty();
            Equal(true, session.IsDirty);

            string path = Path.Combine(Path.GetTempPath(), "example.ddf");
            session.MarkSaved(path);
            Equal(Path.GetFullPath(path), session.CurrentPath);
            Equal("example.ddf", session.DisplayName);
            False(session.IsDirty);
        }

        private static void TracksIndependentOpenDocumentBuffers()
        {
            string firstPath = Path.Combine(Path.GetTempPath(), "first-buffer.ddf");
            string secondPath = Path.Combine(Path.GetTempPath(), "second-buffer.ddf");
            var documents = new OpenDocumentCollection();
            OpenDocumentBuffer untitled = documents.CreateUntitled();
            untitled.UpdateSource("main() out int { ret 1; }");
            untitled.BreakpointLines.Add(1);

            OpenDocumentBuffer first = documents.Open(firstPath, "first() out int { ret 1; }");
            first.BreakpointLines.Add(7);
            OpenDocumentBuffer second = documents.Open(secondPath, "second() out int { ret 2; }");
            second.UpdateSource("second() out int { ret 3; }");

            Equal(3, documents.Documents.Count);
            Equal(second.Id, documents.ActiveDocument.Id);
            Equal(true, second.Session.IsDirty);
            Equal(false, first.Session.IsDirty);
            Equal(true, documents.Activate(untitled.Id));
            Equal("main() out int { ret 1; }", documents.ActiveDocument.Source);
            Equal(true, documents.ActiveDocument.BreakpointLines.Contains(1));
            Equal(false, documents.ActiveDocument.BreakpointLines.Contains(7));
            Equal(first.Id, documents.Open(firstPath, "ignored").Id);
            Equal("first() out int { ret 1; }", first.Source);
            Equal(true, documents.Remove(first.Id));
            Equal(2, documents.Documents.Count);
        }

        private static void RemapsAndValidatesLineBreakpoints()
        {
            const string source = "main() out int\n{\n    int value << 1;\n    ret value;\n}";
            string inserted = "// heading\n" + source;
            DdfBreakpointRemapResult insertedResult = DdfBreakpointService.Remap(source, inserted, new[] { 3, 4 });
            SequenceEqual(new[] { 4, 5 }, insertedResult.Lines);
            Equal(0, insertedResult.UnboundLines.Count);

            string deleted = "main() out int\n{\n    ret value;\n}";
            DdfBreakpointRemapResult deletedResult = DdfBreakpointService.Remap(source, deleted, new[] { 3 });
            Equal(1, deletedResult.Lines.Count);
            Equal(1, deletedResult.UnboundLines.Count);

            DdfParseResult parse = DdfParser.Parse(source);
            IReadOnlyCollection<int> executable = DdfBreakpointService.GetExecutableLines(source, parse.Root);
            Equal(true, executable.Contains(3));
            Equal(true, executable.Contains(4));
            Equal(false, executable.Contains(1));
        }

        private static void DeduplicatesRecentFiles()
        {
            string first = Path.Combine(Path.GetTempPath(), "first.ddf");
            string second = Path.Combine(Path.GetTempPath(), "second.ddf");
            IReadOnlyList<string> recent = RecentFileList.Add(new[] { second, first }, first);
            SequenceEqual(new[] { Path.GetFullPath(first), Path.GetFullPath(second) }, recent);
            SequenceEqual(recent, RecentFileList.Parse(RecentFileList.Serialize(recent)));
        }

        private static void WrapsFindNextSearches()
        {
            const string text = "Alpha beta alpha";
            Equal(11, TextSearch.FindNext(text, "alpha", 6, false));
            Equal(0, TextSearch.FindNext(text, "alpha", 12, false));
            Equal(-1, TextSearch.FindNext(text, "ALPHA", 0, true));
        }

        private static void ReplacesAllMatches()
        {
            ReplaceResult result = TextSearch.ReplaceAll("one ONE two", "one", "1", false);
            Equal("1 1 two", result.Text);
            Equal(2, result.ReplacementCount);
        }

        private static void ProducesFormalTokens()
        {
            const string text = "int value << 12.5; // ok";
            DdfLexResult result = DdfLexer.Lex(text);
            DdfToken[] tokens = result.Tokens.ToArray();
            SequenceEqual(
                new[]
                {
                    DdfTokenKind.DataTypeKeyword,
                    DdfTokenKind.Identifier,
                    DdfTokenKind.Operator,
                    DdfTokenKind.NumberLiteral,
                    DdfTokenKind.Punctuation,
                    DdfTokenKind.LineComment
                },
                tokens.Select(token => token.Kind));
            Equal("12.5", text.Substring(tokens[3].Start, tokens[3].Length));
            Equal(0, result.Diagnostics.Count);
        }

        private static void ReportsDiagnosticPositions()
        {
            const string text = "int value;\n  \"not closed";
            DdfDiagnostic diagnostic = DdfLexer.Lex(text).Diagnostics.Single();
            Equal("DDF001", diagnostic.Code);
            Equal(2, diagnostic.Line);
            Equal(3, diagnostic.Column);
        }

        private static void ExposesDiagnosticSeverityAndHoverText()
        {
            var error = new DdfDiagnostic("DDF999", "Errore di prova.", 2, 3, 4, 5);
            Equal(DdfDiagnosticSeverity.Error, error.Severity);
            Equal(true, error.ToHoverText().Contains("Errore DDF999"));
            Equal(true, error.ToHoverText().Contains("riga 4, colonna 5"));

            var warning = new DdfDiagnostic(
                "DDF998",
                "Avviso di prova.",
                0,
                1,
                1,
                1,
                DdfDiagnosticSeverity.Warning);
            Equal(DdfDiagnosticSeverity.Warning, warning.Severity);
            Equal(true, warning.ToHoverText().StartsWith("Avviso DDF998", StringComparison.Ordinal));
        }

        private static void OffersSafeLexicalQuickFixes()
        {
            DdfQuickFixService service = DdfQuickFixService.CreateDefault();
            var cases = new Dictionary<string, string>
            {
                { "\"testo", "\"testo\"" },
                { "/* commento", "/* commento*/" },
                { "@@'Console", "@@'Console'" },
                { "int value; §", "int value; " }
            };

            foreach (KeyValuePair<string, string> item in cases)
            {
                DdfDiagnostic diagnostic = DdfLexer.Lex(item.Key).Diagnostics.Single();
                DdfQuickFix fix = service.GetFixes(item.Key, diagnostic).Single();
                Equal(item.Value, ApplyQuickFix(item.Key, fix));
                Equal(diagnostic.Code, fix.Diagnostic.Code);
            }
        }

        private static void InsertsParserExpectedTokens()
        {
            const string source = "main() out int { ret 1 }";
            DdfParseResult parse = DdfParser.Parse(source);
            DdfDiagnostic diagnostic = parse.Diagnostics.First(item =>
                item.Code == "DDF102" && item.Message.Contains("';'"));
            DdfQuickFix fix = DdfQuickFixService.CreateDefault().GetFixes(source, diagnostic).Single();
            Equal("Inserisci il token ';'", fix.Title);
            string fixedSource = ApplyQuickFix(source, fix);
            IReadOnlyList<DdfDiagnostic> remaining = DdfParser.Parse(fixedSource).Diagnostics;
            if (remaining.Any(item => item.Code == "DDF102"))
                throw new InvalidOperationException(fixedSource + " :: " + string.Join(" | ", remaining));
        }

        private static void AnchorsMissingTokensBeforeTrivia()
        {
            const string source = "main() out int\n{\n    int value << 1\n\n    ret value;\n}";
            DdfDiagnostic diagnostic = DdfParser.Parse(source).Diagnostics.First(item =>
                item.Code == "DDF102" && item.Message.Contains("';'"));
            DdfQuickFix fix = DdfQuickFixService.CreateDefault().GetFixes(source, diagnostic).Single();
            int expectedInsertion = source.IndexOf('1') + 1;
            Equal(expectedInsertion, fix.Start);
            Equal(';', ApplyQuickFix(source, fix)[expectedInsertion]);
            Equal(true, diagnostic.Start > fix.Start);
        }

        private static void RestoresMandatoryOpeningBracesStructurally()
        {
            const string valid =
                "main() out int\n{\n    int value << 1;\n    ret value;\n}";
            string broken = valid.Remove(valid.IndexOf("{\n", StringComparison.Ordinal), 2);
            DdfDiagnostic diagnostic = DdfParser.Parse(broken).Diagnostics.First(item =>
                item.Code == "DDF102" && item.Message.Contains("'{'"));
            DdfQuickFix fix = DdfQuickFixService.CreateDefault().GetFixes(broken, diagnostic).Single();
            Equal(valid, ApplyQuickFix(broken, fix));
            Equal(true, diagnostic.ContextStart.HasValue);
        }

        private static void RestoresNestedClosingBracesStructurally()
        {
            const string valid =
                "main() out int\n{\n    if(true)\n    {\n        ret 1;\n    }\n}";
            string brokenInner = valid.Remove(valid.IndexOf("    }\n", StringComparison.Ordinal), "    }\n".Length);
            DdfDiagnostic innerDiagnostic = DdfParser.Parse(brokenInner).Diagnostics.First(item =>
                item.Code == "DDF102" && item.Message.Contains("'}'"));
            DdfQuickFix innerFix = DdfQuickFixService.CreateDefault().GetFixes(brokenInner, innerDiagnostic).Single();
            Equal(valid, ApplyQuickFix(brokenInner, innerFix));

            string brokenOuter = valid.Substring(0, valid.Length - 2);
            DdfDiagnostic outerDiagnostic = DdfParser.Parse(brokenOuter).Diagnostics.First(item =>
                item.Code == "DDF102" && item.Message.Contains("'}'"));
            DdfQuickFix outerFix = DdfQuickFixService.CreateDefault().GetFixes(brokenOuter, outerDiagnostic).Single();
            Equal(valid, ApplyQuickFix(brokenOuter, outerFix));
        }

        private static void KeepsValidBracesIndependentFromIndentation()
        {
            const string source =
                "main() out int\n{\n    if(true)\n    {\n        ret 1;\n}\n}";
            Equal(0, DdfParser.Parse(source).Diagnostics.Count);
        }

        private static void AcceptsCustomQuickFixProviders()
        {
            const string source = "value";
            var diagnostic = new DdfDiagnostic("CUSTOM", "Custom diagnostic.", 0, 5, 1, 1);
            var service = new DdfQuickFixService(new[] { new CustomQuickFixProvider() });
            DdfQuickFix fix = service.GetFixesAt(source, new[] { diagnostic }, 2).Single();
            Equal("Sostituisci con fixed", fix.Title);
            Equal("fixed", ApplyQuickFix(source, fix));
        }

        private static string ApplyQuickFix(string source, DdfQuickFix fix)
        {
            return source.Remove(fix.Start, fix.Length).Insert(fix.Start, fix.Replacement);
        }

        private static void MatchesFullLexingAfterIncrementalEdits()
        {
            var lexer = new IncrementalDdfLexer();
            const string first = "int first << 1;\nstring second << \"ok\";";
            const string second = "int first << 1;\nstring second << \"changed\";";
            const string third = "int first << 1;\n/* string second << \"changed\";";

            lexer.Update(first);
            DdfLexUpdate secondUpdate = lexer.Update(second);
            Equal(true, secondUpdate.RelexStart > 0);
            AssertLexResultsEqual(DdfLexer.Lex(second), secondUpdate.Result);

            DdfLexUpdate thirdUpdate = lexer.Update(third);
            Equal(true, thirdUpdate.RelexStart > 0);
            AssertLexResultsEqual(DdfLexer.Lex(third), thirdUpdate.Result);
            Equal("DDF002", thirdUpdate.Result.Diagnostics.Single().Code);
        }

        private static void SurvivesDeterministicDynamicEditing()
        {
            const int editCount = 400;
            var random = new Random(50201);
            var lexer = new IncrementalDdfLexer();
            string text = "@@'Console'\nmain() out int {\n    int value << 1;\n    ret value;\n}";
            string[] fragments = { "@@'Math'", "@@", "'", "/*", "*/", "\"", "int", " value", "{", "}", ";", "\n" };
            lexer.Update(text);

            for (int edit = 0; edit < editCount; edit++)
            {
                string previousText = text;
                bool insert = text.Length == 0 || random.Next(100) < 58;
                int position = random.Next(text.Length + 1);
                string operation;
                if (insert)
                {
                    string fragment = fragments[random.Next(fragments.Length)];
                    text = text.Insert(position, fragment);
                    operation = "insert <" + fragment.Replace("\n", "\\n") + ">";
                }
                else
                {
                    int length = Math.Min(1 + random.Next(8), text.Length - position);
                    if (length == 0)
                    {
                        position--;
                        length = 1;
                    }

                    operation = "delete <" + text.Substring(position, length).Replace("\n", "\\n") + ">";
                    text = text.Remove(position, length);
                }

                DdfLexUpdate update = lexer.Update(text);
                try
                {
                    AssertLexResultsEqual(DdfLexer.Lex(text), update.Result);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "edit " + edit + ", " + operation + ", position " + position +
                        ", previous <" + previousText.Replace("\r", "\\r").Replace("\n", "\\n") +
                        ">, source <" +
                        text.Replace("\r", "\\r").Replace("\n", "\\n") + ">: " + exception.Message,
                        exception);
                }

                AssertDiagnosticSpans(text, DdfParser.Parse(text, update.Result).Diagnostics);
            }
        }

        private static void RelexesAcrossSharedInsertedPrefix()
        {
            var lexer = new IncrementalDdfLexer();
            lexer.Update("@u( @@/}");
            const string edited = "@u( @@'Math'@@/}";
            DdfLexUpdate update = lexer.Update(edited);
            AssertLexResultsEqual(DdfLexer.Lex(edited), update.Result);
            Equal(true, update.Result.Tokens.Any(token => token.Kind == DdfTokenKind.LibraryDirective));
        }

        private static void AssertDiagnosticSpans(string text, IReadOnlyList<DdfDiagnostic> diagnostics)
        {
            foreach (DdfDiagnostic diagnostic in diagnostics)
            {
                Equal(true, diagnostic.Start >= 0);
                Equal(true, diagnostic.Start <= text.Length);
                Equal(true, diagnostic.Length >= 0);
                Equal(true, diagnostic.End <= text.Length);
            }
        }

        private static void AcceptsValidLexerCorpus()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Corpus", "valid.ddf");
            DdfLexResult result = DdfLexer.Lex(File.ReadAllText(path));
            Equal(0, result.Diagnostics.Count);
        }

        private static void ReportsInvalidLexerCorpus()
        {
            string corpus = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Corpus");
            var expected = new Dictionary<string, string>
            {
                { "invalid-character.ddf", "DDF004" },
                { "unterminated-string.ddf", "DDF001" },
                { "unterminated-comment.ddf", "DDF002" },
                { "unterminated-library.ddf", "DDF003" }
            };

            foreach (KeyValuePair<string, string> item in expected)
            {
                DdfDiagnostic diagnostic = DdfLexer.Lex(File.ReadAllText(Path.Combine(corpus, item.Key)))
                    .Diagnostics.Single();
                Equal(item.Value, diagnostic.Code);
            }
        }

        private static void BuildsAstForParserCorpus()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Corpus", "Parser", "valid.ddf");
            DdfParseResult result = DdfParser.Parse(File.ReadAllText(path));
            Equal(0, result.Diagnostics.Count);
            Equal(3, result.Root.Members.Count);
            Equal(true, result.Root.Members[0] is LibraryDirectiveSyntax);
            Equal(true, result.Root.Members[1] is StructDeclarationSyntax);
            var function = result.Root.Members[2] as FunctionDeclarationSyntax;
            Equal(true, function != null);
            Equal("main", function.Name);
            Equal("int", function.ReturnType.Name);
        }

        private static void HonorsExpressionPrecedence()
        {
            DdfParseResult result = DdfParser.Parse("main() out int { int value << 1 + 2 * 3; }");
            Equal(0, result.Diagnostics.Count);
            var function = (FunctionDeclarationSyntax)result.Root.Members.Single();
            var declaration = (VariableDeclarationStatementSyntax)function.Body.Statements.Single();
            var addition = (BinaryExpressionSyntax)declaration.Initializer;
            Equal("+", addition.OperatorText);
            Equal("*", ((BinaryExpressionSyntax)addition.Right).OperatorText);
        }

        private static void HonorsRightAssociativity()
        {
            DdfParseResult result = DdfParser.Parse("main() out int { first << second << third; ret 2 ^ 3 ^ 4; }");
            Equal(0, result.Diagnostics.Count);
            var function = (FunctionDeclarationSyntax)result.Root.Members.Single();
            var assignment = (BinaryExpressionSyntax)((ExpressionStatementSyntax)function.Body.Statements[0]).Expression;
            Equal("<<", assignment.OperatorText);
            Equal("<<", ((BinaryExpressionSyntax)assignment.Right).OperatorText);
            var power = (BinaryExpressionSyntax)((ReturnStatementSyntax)function.Body.Statements[1]).Expression;
            Equal("^", power.OperatorText);
            Equal("^", ((BinaryExpressionSyntax)power.Right).OperatorText);
        }

        private static void RecoversFromMultipleSyntaxErrors()
        {
            string corpus = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Corpus", "Parser");
            string[] files = { "invalid-missing-semicolon.ddf", "invalid-delimiters.ddf", "invalid-expression.ddf" };
            foreach (string file in files)
            {
                DdfParseResult result = DdfParser.Parse(File.ReadAllText(Path.Combine(corpus, file)));
                Equal(true, result.SyntaxDiagnostics.Count > 0);
                Equal(true, result.Root.Members.Count > 0);
            }

            DdfParseResult multiple = DdfParser.Parse(File.ReadAllText(Path.Combine(corpus, "invalid-expression.ddf")));
            Equal(true, multiple.SyntaxDiagnostics.Count >= 2);
            Equal(true, multiple.SyntaxDiagnostics.All(diagnostic => diagnostic.Code.StartsWith("DDF1", StringComparison.Ordinal)));
        }

        private static void KeepsAstSpansWithinSource()
        {
            const string source = "main() out int { int value << 1; ret value; }";
            DdfParseResult result = DdfParser.Parse(source);
            var function = (FunctionDeclarationSyntax)result.Root.Members.Single();
            Equal(0, result.Root.Start);
            Equal(source.Length, result.Root.End);
            Equal(true, function.Start >= 0 && function.End <= source.Length);
            Equal(true, function.Body.Start >= function.Start && function.Body.End <= function.End);
            Equal(true, function.Body.Statements.All(statement => statement.Start >= function.Body.Start && statement.End <= function.Body.End));
        }

        private static void ParsesEditorSmokeSample()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string source = File.ReadAllText(Path.Combine(projectRoot, "samples", "editor-smoke-test.ddf"));
            DdfParseResult result = DdfParser.Parse(source);
            Equal(0, result.Diagnostics.Count);
        }

        private static void DrivesLexingFromLanguageCatalog()
        {
            DdfLanguageDefinition language = CreateAlternativeLanguage();
            const string source = "record value returns whole when give := %% yes";
            DdfLexResult result = DdfLexer.Lex(source, language);
            Equal(0, result.Diagnostics.Count);
            SequenceEqual(
                new[]
                {
                    DdfTokenKind.DataTypeKeyword,
                    DdfTokenKind.Identifier,
                    DdfTokenKind.FunctionKeyword,
                    DdfTokenKind.DataTypeKeyword,
                    DdfTokenKind.ControlFlowKeyword,
                    DdfTokenKind.FunctionKeyword,
                    DdfTokenKind.Operator,
                    DdfTokenKind.Operator,
                    DdfTokenKind.BooleanLiteral
                },
                result.Tokens.Select(token => token.Kind));
        }

        private static void DrivesParsingFromSyntaxRoles()
        {
            DdfLanguageDefinition language = CreateAlternativeLanguage();
            const string source =
                "record Item { whole member; } " +
                "main() returns whole { whole value := 1 %% 2; when(value) give value; }";
            DdfParseResult result = DdfParser.Parse(source, language);
            Equal(0, result.Diagnostics.Count);
            Equal(true, result.Root.Members[0] is StructDeclarationSyntax);
            var function = (FunctionDeclarationSyntax)result.Root.Members[1];
            var declaration = (VariableDeclarationStatementSyntax)function.Body.Statements[0];
            Equal("%%", ((BinaryExpressionSyntax)declaration.Initializer).OperatorText);
            Equal(true, function.Body.Statements[1] is IfStatementSyntax);
        }

        private static void RejectsDuplicateCatalogEntries()
        {
            Throws<ArgumentException>(() => new DdfLanguageDefinition(
                new[]
                {
                    new DdfKeywordDefinition("whole", DdfTokenKind.DataTypeKeyword),
                    new DdfKeywordDefinition("whole", DdfTokenKind.DataTypeKeyword)
                },
                new string[0],
                new DdfOperatorDefinition[0],
                new[] { ';' }));
        }

        private static DdfLanguageDefinition CreateAlternativeLanguage()
        {
            return new DdfLanguageDefinition(
                new[]
                {
                    new DdfKeywordDefinition("whole", DdfTokenKind.DataTypeKeyword),
                    new DdfKeywordDefinition("record", DdfTokenKind.DataTypeKeyword, DdfKeywordRole.Structure),
                    new DdfKeywordDefinition("returns", DdfTokenKind.FunctionKeyword, DdfKeywordRole.ReturnTypeMarker),
                    new DdfKeywordDefinition("give", DdfTokenKind.FunctionKeyword, DdfKeywordRole.Return),
                    new DdfKeywordDefinition("stop", DdfTokenKind.FunctionKeyword, DdfKeywordRole.Break),
                    new DdfKeywordDefinition("finish", DdfTokenKind.FunctionKeyword, DdfKeywordRole.End),
                    new DdfKeywordDefinition("when", DdfTokenKind.ControlFlowKeyword, DdfKeywordRole.If),
                    new DdfKeywordDefinition("during", DdfTokenKind.ControlFlowKeyword, DdfKeywordRole.While),
                    new DdfKeywordDefinition("repeat", DdfTokenKind.ControlFlowKeyword, DdfKeywordRole.Do),
                    new DdfKeywordDefinition("cycle", DdfTokenKind.ControlFlowKeyword, DdfKeywordRole.For)
                },
                new[] { "yes", "no" },
                new[]
                {
                    new DdfOperatorDefinition(
                        ":=",
                        1,
                        DdfOperatorAssociativity.Right,
                        role: DdfOperatorRole.DeclarationInitializer),
                    new DdfOperatorDefinition("%%", 7),
                    new DdfOperatorDefinition("!", prefixPrecedence: 9),
                    new DdfOperatorDefinition("++", postfixPrecedence: 10),
                    new DdfOperatorDefinition("<", 5)
                },
                new[] { '{', '}', '(', ')', '[', ']', '.', ',', ';' });
        }

        private static void IndexesDocumentSymbolsHierarchically()
        {
            const string source =
                "@@'Console'\n" +
                "struct Point { int x; int y; }\n" +
                "sum(int left, int right) out int {\n" +
                "  int total << left + right;\n" +
                "  if(total > 0) { int nested; }\n" +
                "  for(int index; index < 2; index++) { float ratio; }\n" +
                "  ret total;\n" +
                "}";
            DdfParseResult parseResult = DdfParser.Parse(source);
            Equal(0, parseResult.Diagnostics.Count);
            DdfSymbolIndex index = DdfSymbolIndex.Create(parseResult.Root);
            Equal(3, index.Symbols.Count);
            Equal(DdfSymbolKind.Library, index.Symbols[0].Kind);
            Equal("Console", index.Symbols[0].Name);
            Equal(DdfSymbolKind.Structure, index.Symbols[1].Kind);
            SequenceEqual(new[] { "x", "y" }, index.Symbols[1].Children.Select(symbol => symbol.Name));
            Equal(DdfSymbolKind.Function, index.Symbols[2].Kind);
            SequenceEqual(
                new[] { "left", "right", "total", "nested", "index", "ratio" },
                index.Symbols[2].Children.Select(symbol => symbol.Name));
        }

        private static void KeepsExactSymbolSelectionSpans()
        {
            const string source = "main(int argument) out int { string local; ret argument; }";
            DdfDocumentSymbol function = DdfSymbolIndex.Create(DdfParser.Parse(source).Root).Symbols.Single();
            Equal("main", source.Substring(function.SelectionStart, function.SelectionLength));
            Equal("argument", source.Substring(function.Children[0].SelectionStart, function.Children[0].SelectionLength));
            Equal("local", source.Substring(function.Children[1].SelectionStart, function.Children[1].SelectionLength));
        }

        private static void BuildsContextualBreadcrumbPaths()
        {
            const string source =
                "struct Point { int x; }\n" +
                "main() out int { if(true) { while(false) { ret 1; } } }";
            DdfParseResult parseResult = DdfParser.Parse(source);
            Equal(0, parseResult.Diagnostics.Count);

            IReadOnlyList<DdfBreadcrumbItem> structurePath = DdfBreadcrumbService.GetPath(
                parseResult.Root, source.IndexOf("x;", StringComparison.Ordinal));
            SequenceEqual(new[] { "Point" }, structurePath.Select(item => item.Label));
            Equal(DdfBreadcrumbKind.Structure, structurePath[0].Kind);

            int returnPosition = source.IndexOf("ret 1", StringComparison.Ordinal);
            IReadOnlyList<DdfBreadcrumbItem> controlPath = DdfBreadcrumbService.GetPath(parseResult.Root, returnPosition);
            SequenceEqual(new[] { "main()", "if", "while" }, controlPath.Select(item => item.Label));
            SequenceEqual(
                new[] { DdfBreadcrumbKind.Function, DdfBreadcrumbKind.If, DdfBreadcrumbKind.While },
                controlPath.Select(item => item.Kind));
            Equal("main", source.Substring(controlPath[0].SelectionStart, controlPath[0].SelectionLength));
            Equal("if", source.Substring(controlPath[1].SelectionStart, controlPath[1].SelectionLength));
            Equal("while", source.Substring(controlPath[2].SelectionStart, controlPath[2].SelectionLength));
        }

        private static void KeepsBreadcrumbPathsUsableForIncompleteCode()
        {
            const string source = "main() out int { do { if(true) { ret 1;";
            DdfParseResult parseResult = DdfParser.Parse(source);
            Equal(true, parseResult.SyntaxDiagnostics.Count > 0);
            IReadOnlyList<DdfBreadcrumbItem> path = DdfBreadcrumbService.GetPath(parseResult.Root, source.Length);
            SequenceEqual(new[] { "main()", "do…while", "if" }, path.Select(item => item.Label));
        }

        private static void IndexesIncompleteDocumentsSafely()
        {
            const string source = "main() out int { int complete; int";
            DdfParseResult parseResult = DdfParser.Parse(source);
            Equal(true, parseResult.SyntaxDiagnostics.Count > 0);
            DdfDocumentSymbol function = DdfSymbolIndex.Create(parseResult.Root).Symbols.Single();
            SequenceEqual(new[] { "complete" }, function.Children.Select(symbol => symbol.Name));
        }

        private static void ResolvesSymbolDeclarationsAndReferences()
        {
            const string source =
                "sum(int left, int right) out int { int total << left + right; ret total; }";
            DdfSemanticModel model = DdfSemanticModel.Create(source, DdfParser.Parse(source).Root);
            Equal(0, model.Diagnostics.Count);

            DdfSymbolOccurrence reference = model.FindOccurrence(source.LastIndexOf("total", StringComparison.Ordinal));
            Equal(false, reference.IsDeclaration);
            Equal("total", reference.Symbol.Name);
            Equal("total", source.Substring(reference.Symbol.SelectionStart, reference.Symbol.SelectionLength));
            Equal(2, model.FindOccurrences(reference.Symbol).Count);

            DdfSymbolOccurrence parameterReference = model.FindOccurrence(source.IndexOf("left +", StringComparison.Ordinal));
            Equal(DdfSymbolKind.Parameter, parameterReference.Symbol.Kind);
            Equal(2, model.FindOccurrences(parameterReference.Symbol).Count);
        }

        private static void KeepsShadowedSymbolsInSeparateScopes()
        {
            const string source =
                "main() out int { int value; if(true) { int value; value++; } ret value; }";
            DdfSemanticModel model = DdfSemanticModel.Create(source, DdfParser.Parse(source).Root);
            DdfSymbolOccurrence innerReference = model.FindOccurrence(source.IndexOf("value++", StringComparison.Ordinal));
            DdfSymbolOccurrence outerReference = model.FindOccurrence(source.LastIndexOf("value", StringComparison.Ordinal));
            False(ReferenceEquals(innerReference.Symbol, outerReference.Symbol));
            Equal(source.IndexOf("value; value", StringComparison.Ordinal), innerReference.Symbol.SelectionStart);
            Equal(source.IndexOf("value; if", StringComparison.Ordinal), outerReference.Symbol.SelectionStart);
        }

        private static void RenamesOnlyTheResolvedSymbol()
        {
            const string source =
                "first() out int { int value; ret value; } second() out int { int value; ret value; }";
            DdfSemanticModel model = DdfSemanticModel.Create(source, DdfParser.Parse(source).Root);
            int firstReference = source.IndexOf("ret value", StringComparison.Ordinal) + 4;
            DdfRenameResult result = model.Rename(firstReference, "result");
            Equal(2, result.ReplacementCount);
            Equal(
                "first() out int { int result; ret result; } second() out int { int value; ret value; }",
                result.Text);
            Equal("result", result.Text.Substring(result.SelectionStart, result.SelectionLength));
        }

        private static void ReportsUnresolvedAndDuplicateSymbols()
        {
            const string source = "main() out int { int value; int value; ret missing; }";
            DdfSemanticModel model = DdfSemanticModel.Create(source, DdfParser.Parse(source).Root);
            Equal(true, model.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF201" && diagnostic.Message.Contains("missing")));
            Equal(true, model.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF202" && diagnostic.Message.Contains("value")));
        }

        private static void ValidatesRenameIdentifiersAndKeywords()
        {
            const string source = "main() out int { int value; ret value; }";
            DdfSemanticModel model = DdfSemanticModel.Create(source, DdfParser.Parse(source).Root);
            int position = source.LastIndexOf("value", StringComparison.Ordinal);
            Throws<ArgumentException>(() => model.Rename(position, "2invalid"));
            Throws<ArgumentException>(() => model.Rename(position, "while"));
            Equal(true, DdfSemanticModel.IsValidIdentifier("valid_name2"));
        }

        private static void RenamesLibraryNamesWithoutDamagingDirectives()
        {
            const string source = "@@'Console'\nmain() out int { ret 0; }";
            DdfSemanticModel model = DdfSemanticModel.Create(source, DdfParser.Parse(source).Root);
            DdfSymbolOccurrence library = model.FindOccurrence(source.IndexOf("Console", StringComparison.Ordinal));
            Equal(DdfSymbolKind.Library, library.Symbol.Kind);
            Equal("Console", source.Substring(library.Symbol.SelectionStart, library.Symbol.SelectionLength));
            Equal("@@'Terminal'\nmain() out int { ret 0; }", model.Rename(library.Start, "Terminal").Text);
        }

        private static void RenamesStructureDeclarationsAndTypeReferences()
        {
            const string source = "struct Point { int x; } main(Point point) out Point { Point local; ret point; }";
            DdfSemanticModel model = DdfSemanticModel.Create(source, DdfParser.Parse(source).Root);
            DdfRenameResult result = model.Rename(source.IndexOf("Point", StringComparison.Ordinal), "Coordinate");
            Equal(4, result.ReplacementCount);
            Equal(
                "struct Coordinate { int x; } main(Coordinate point) out Coordinate { Coordinate local; ret point; }",
                result.Text);
        }

        private static void IndexesDdfWorkspaceDocumentsRecursively()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ddf-workspace-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(directory, "nested"));
                DdfDocumentFile.Save(Path.Combine(directory, "main.ddf"), "main() out int { ret helper(); }");
                DdfDocumentFile.Save(Path.Combine(directory, "nested", "helpers.ddf"), "helper() out int { ret 1; }");
                File.WriteAllText(Path.Combine(directory, "ignored.txt"), "not DDF");

                DdfWorkspaceIndex workspace = DdfWorkspaceIndex.Load(directory);
                Equal(2, workspace.Documents.Count);
                Equal(1, workspace.FindDefinitions("helper").Count);
                Equal("nested" + Path.DirectorySeparatorChar + "helpers.ddf",
                    workspace.FindDefinitions("helper")[0].Document.RelativePath);
                Equal(true, workspace.ContainsPath(Path.Combine(directory, "main.ddf")));
                Equal(false, workspace.ContainsPath(Path.Combine(Path.GetTempPath(), "outside.ddf")));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void UpdatesInMemoryWorkspaceDocument()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ddf-workspace-update-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "main.ddf");
                DdfDocumentFile.Save(path, "oldName() out int { ret 0; }");
                DdfWorkspaceIndex workspace = DdfWorkspaceIndex.Load(directory)
                    .WithDocument(path, "newName() out int { ret 0; }");
                Equal(0, workspace.FindDefinitions("oldName").Count);
                Equal(1, workspace.FindDefinitions("newName").Count);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void CompletesSymbolsFromAnotherWorkspaceDocument()
        {
            const string externalSource = "helper() out int { ret 1; } struct Point { int x; }";
            IReadOnlyList<DdfDocumentSymbol> external = DdfSymbolIndex.Create(DdfParser.Parse(externalSource).Root).Symbols;
            DdfCompletionResult completion = DdfCompletionService.GetCompletions(
                "hel",
                3,
                externalSymbols: external);
            Equal(true, completion.Items.Any(item => item.DisplayText == "helper" && item.Kind == DdfCompletionKind.Function));
        }

        private static void SearchesTextAcrossWorkspaceDocuments()
        {
            var documents = new[]
            {
                new DdfWorkspaceSearchDocument("one", "one.ddf", "one.ddf", "alpha\nBeta alpha"),
                new DdfWorkspaceSearchDocument("two", "two.ddf", "two.ddf", "ALPHA")
            };
            IReadOnlyList<DdfWorkspaceSearchResult> insensitive = DdfWorkspaceSearchService.Search(
                documents, "alpha", DdfWorkspaceSearchKind.Text);
            Equal(3, insensitive.Count);
            Equal(1, insensitive[0].Line);
            Equal(1, insensitive[0].Column);
            Equal(2, insensitive[1].Line);
            Equal(6, insensitive[1].Column);
            Equal("Beta alpha", insensitive[1].Preview);
            Equal(2, DdfWorkspaceSearchService.Search(
                documents, "alpha", DdfWorkspaceSearchKind.Text, true).Count);
        }

        private static void SearchesNestedWorkspaceSymbols()
        {
            const string source = "helper(int value) out int { int localValue; ret value; }";
            var documents = new[]
            {
                new DdfWorkspaceSearchDocument("buffer-1", null, "Senza titolo.ddf", source)
            };
            IReadOnlyList<DdfWorkspaceSearchResult> results = DdfWorkspaceSearchService.Search(
                documents, "value", DdfWorkspaceSearchKind.Symbol);
            Equal(2, results.Count);
            Equal(true, results.Any(result => result.SymbolKind == DdfSymbolKind.Parameter));
            Equal(true, results.Any(result => result.SymbolKind == DdfSymbolKind.Variable));

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Throws<OperationCanceledException>(() => DdfWorkspaceSearchService.Search(
                    documents, "value", DdfWorkspaceSearchKind.Symbol, cancellationToken: cancellation.Token));
            }
        }

        private static void CreatesSelectiveWorkspaceReplacementChanges()
        {
            var documents = new[]
            {
                new DdfWorkspaceSearchDocument("one", "one.ddf", "one.ddf", "alpha alpha"),
                new DdfWorkspaceSearchDocument("two", "two.ddf", "two.ddf", "alpha")
            };
            IReadOnlyList<DdfWorkspaceSearchResult> matches = DdfWorkspaceSearchService.Search(
                documents, "alpha", DdfWorkspaceSearchKind.Text);
            IReadOnlyList<DdfWorkspaceReplacementChange> changes = DdfWorkspaceSearchService.CreateReplacementChanges(
                new[] { matches[1], matches[2] }, "beta");
            Equal(2, changes.Count);
            Equal("alpha beta", changes.Single(change => change.Document.Id == "one").UpdatedSource);
            Equal("beta", changes.Single(change => change.Document.Id == "two").UpdatedSource);
            Equal("alpha alpha  →  alpha beta", DdfWorkspaceSearchService.CreateReplacementPreview(matches[1], "beta"));
        }

        private static void ListsWorkspaceFilesAndSymbolsForNavigation()
        {
            var documents = new[]
            {
                new DdfWorkspaceSearchDocument("main", "main.ddf", "main.ddf", "main() out int { int value; ret value; }"),
                new DdfWorkspaceSearchDocument("lib", "lib.ddf", "lib.ddf", "helper() out int { ret 1; }")
            };
            IReadOnlyList<DdfWorkspaceNavigationLocation> files = DdfWorkspaceNavigationService.ListFiles(documents);
            IReadOnlyList<DdfWorkspaceNavigationLocation> symbols = DdfWorkspaceNavigationService.ListSymbols(documents);
            Equal(2, files.Count);
            Equal("lib.ddf", files[0].Name);
            Equal(3, symbols.Count);
            Equal(true, symbols.Any(item => item.Name == "helper" && item.Line == 1 && item.Column == 1));
            Equal(true, symbols.Any(item => item.Name == "value" && item.SymbolKind == DdfSymbolKind.Variable));
        }

        private static void FindsSemanticWorkspaceReferences()
        {
            const string main = "main() out int { int external; external << 1; ret helper(); }";
            const string library = "helper() out int { ret 1; } // helper ignored";
            var documents = new[]
            {
                new DdfWorkspaceSearchDocument("main", "main.ddf", "main.ddf", main),
                new DdfWorkspaceSearchDocument("lib", "lib.ddf", "lib.ddf", library)
            };
            IReadOnlyList<DdfWorkspaceNavigationLocation> global = DdfWorkspaceNavigationService.FindReferences(
                documents, "lib", library.IndexOf("helper", StringComparison.Ordinal));
            Equal(2, global.Count);
            Equal(true, global.Any(item => item.Document.Id == "main" && item.Detail == "riferimento"));
            Equal(true, global.Any(item => item.Document.Id == "lib" && item.Detail == "dichiarazione"));

            int localStart = main.IndexOf("external", StringComparison.Ordinal);
            IReadOnlyList<DdfWorkspaceNavigationLocation> local = DdfWorkspaceNavigationService.FindReferences(
                documents, "main", localStart);
            Equal(2, local.Count);
            Equal(true, local.All(item => item.Document.Id == "main"));
        }

        private static void ParsesLineAndColumnNavigation()
        {
            Equal(true, DdfWorkspaceNavigationService.TryParseLineColumn("2:3", 3, out int line, out int column));
            Equal(2, line);
            Equal(3, column);
            Equal(6, DdfWorkspaceNavigationService.GetPosition("one\ntwo\nthree", line, column));
            False(DdfWorkspaceNavigationService.TryParseLineColumn("4", 3, out line, out column));
            False(DdfWorkspaceNavigationService.TryParseLineColumn("2:0", 3, out line, out column));
        }

        private static void AcceptsSemanticallyValidTypedCode()
        {
            const string source =
                "sum(int left, int right) out float { float total << left + right; if(total > 0) { ret total; } ret 0; }";
            DdfParseResult parse = DdfParser.Parse(source);
            Equal(0, parse.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(source, parse.Root).Diagnostics.Count);
        }

        private static void ReportsIncompatibleInitializersAndAssignments()
        {
            const string source = "main() out int { int value << \"text\"; value << false; ret value; }";
            DdfTypeCheckResult result = DdfTypeChecker.Check(source, DdfParser.Parse(source).Root);
            Equal(2, result.Diagnostics.Count(diagnostic => diagnostic.Code == "DDF301"));
        }

        private static void ReportsInvalidTypedOperatorsAndConditions()
        {
            const string source = "main() out int { string text; int number; text + number; if(number) { ret 1; } ret 0; }";
            DdfTypeCheckResult result = DdfTypeChecker.Check(source, DdfParser.Parse(source).Root);
            Equal(true, result.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF302"));
            Equal(true, result.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF306"));
        }

        private static void ChecksFunctionArgumentsAndReturnValues()
        {
            const string source =
                "sum(int left, int right) out int { ret left + right; } " +
                "main() out bool { int value << sum(1); ret value; }";
            DdfTypeCheckResult result = DdfTypeChecker.Check(source, DdfParser.Parse(source).Root);
            Equal(true, result.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF303"));
            Equal(true, result.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF304"));
        }

        private static void ResolvesStructureMembersAndArrayElementTypes()
        {
            const string source =
                "struct Point { int x; } main(Point point) out int { int[2] values; values[0] << point.x; ret point.x; }";
            DdfParseResult parse = DdfParser.Parse(source);
            Equal(0, parse.Diagnostics.Count);
            DdfTypeCheckResult result = DdfTypeChecker.Check(source, parse.Root);
            Equal(0, result.Diagnostics.Count);
            int memberStart = source.LastIndexOf(".x", StringComparison.Ordinal) + 1;
            Equal("int", result.FindTypeAt(memberStart).Type.Name);
        }

        private static void ReportsUnknownTypesAndMembers()
        {
            const string source = "main(Missing item) out int { int value << item.unknown; ret value; }";
            DdfTypeCheckResult result = DdfTypeChecker.Check(source, DdfParser.Parse(source).Root);
            Equal(true, result.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF305" && diagnostic.Message.Contains("Missing")));
        }

        private static void ChecksFunctionSignaturesFromAnotherDocument()
        {
            const string library = "helper(int value) out int { ret value; }";
            const string source = "main() out int { ret helper(\"wrong\"); }";
            CompilationUnitSyntax external = DdfParser.Parse(library).Root;
            DdfTypeCheckResult result = DdfTypeChecker.Check(source, DdfParser.Parse(source).Root, new[] { external });
            Equal(true, result.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF303"));
        }

        private static void ReportsMissingReturnsAndInvalidAssignmentTargets()
        {
            const string source = "main() out int { 1 << 2; }";
            DdfTypeCheckResult result = DdfTypeChecker.Check(source, DdfParser.Parse(source).Root);
            Equal(true, result.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF307"));
            Equal(true, result.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF308"));
        }

        private static void TypeChecksEditorSmokeSample()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string source = File.ReadAllText(Path.Combine(projectRoot, "samples", "editor-smoke-test.ddf"));
            DdfParseResult parse = DdfParser.Parse(source);
            Equal(0, parse.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(source, parse.Root).Diagnostics.Count);
        }

        private static void MatchesNestedDelimiters()
        {
            const string source = "main() out int { int[2] values; if(values[0] > 0) { ret values[0]; } }";
            DdfLexResult lexResult = DdfLexer.Lex(source);
            int functionOpen = source.IndexOf('(');
            DdfDelimiterMatch functionPair = DdfDelimiterMatcher.FindMatch(source, functionOpen, lexResult);
            Equal('(', functionPair.OpenCharacter);
            Equal(source.IndexOf(')'), functionPair.CloseStart);

            int arrayOpen = source.IndexOf('[');
            DdfDelimiterMatch arrayPair = DdfDelimiterMatcher.FindMatch(source, arrayOpen + 1, lexResult);
            Equal(arrayOpen, arrayPair.OpenStart);
            Equal(source.IndexOf(']', arrayOpen), arrayPair.CloseStart);

            int outerOpen = source.IndexOf('{');
            DdfDelimiterMatch outerPair = DdfDelimiterMatcher.FindMatch(source, outerOpen, lexResult);
            Equal(source.LastIndexOf('}'), outerPair.CloseStart);
        }

        private static void IgnoresDelimitersInCommentsAndStrings()
        {
            const string source = "\"( [ {\" // ) ] }\n/* { nested } */ main() out int { ret 0; }";
            DdfLexResult lexResult = DdfLexer.Lex(source);
            IReadOnlyList<DdfDelimiterMatch> pairs = DdfDelimiterMatcher.FindPairs(source, lexResult);
            Equal(2, pairs.Count);
            Equal(true, pairs.All(pair => pair.OpenStart >= source.IndexOf("main", StringComparison.Ordinal)));
            Equal(null, DdfDelimiterMatcher.FindMatch(source, 1, lexResult));
        }

        private static void HandlesUnmatchedDelimiters()
        {
            const string source = "main( out int { ret 0;";
            DdfLexResult lexResult = DdfLexer.Lex(source);
            Equal(null, DdfDelimiterMatcher.FindMatch(source, source.IndexOf('('), lexResult));
            Equal(0, DdfDelimiterMatcher.FindPairs(source, lexResult).Count);
        }

        private static void HandlesStaleDelimiterTokensSafely()
        {
            DdfLexResult staleResult = DdfLexer.Lex("main() out int { ret 0; }");
            IReadOnlyList<DdfDelimiterMatch> pairs = DdfDelimiterMatcher.FindPairs("main", staleResult);
            Equal(0, pairs.Count);
            Equal(null, DdfDelimiterMatcher.FindMatch("main", 4, staleResult));
        }

        private static void ExpandsSelectionsThroughSyntaxLevels()
        {
            const string source = "@@'Console'\nmain() out int { ret value + 1; }";
            int caret = source.IndexOf("value", StringComparison.Ordinal) + 2;
            DdfTextRange token = DdfSelectionService.GetNextExpansion(source, caret, 0);
            Equal("value", source.Substring(token.Start, token.Length));
            DdfTextRange expression = DdfSelectionService.GetNextExpansion(source, token.Start, token.Length);
            Equal("value + 1", source.Substring(expression.Start, expression.Length));
            DdfTextRange statement = DdfSelectionService.GetNextExpansion(source, expression.Start, expression.Length);
            Equal("ret value + 1;", source.Substring(statement.Start, statement.Length));
            DdfTextRange block = DdfSelectionService.GetNextExpansion(source, statement.Start, statement.Length);
            Equal("{ ret value + 1; }", source.Substring(block.Start, block.Length));
            DdfTextRange function = DdfSelectionService.GetNextExpansion(source, block.Start, block.Length);
            Equal("main() out int { ret value + 1; }", source.Substring(function.Start, function.Length));
            DdfTextRange document = DdfSelectionService.GetNextExpansion(source, function.Start, function.Length);
            Equal(source, source.Substring(document.Start, document.Length));
            Equal(null, DdfSelectionService.GetNextExpansion(source, document.Start, document.Length));
        }

        private static void ExpandsProtectedTokensSafely()
        {
            const string source = "main() out string { string text << \"(not a pair)\"; ret text; }";
            int caret = source.IndexOf("not", StringComparison.Ordinal);
            DdfTextRange range = DdfSelectionService.GetNextExpansion(source, caret, 0);
            Equal("\"(not a pair)\"", source.Substring(range.Start, range.Length));
            Equal(null, DdfDelimiterNavigation.GetMatchingPosition(source, source.IndexOf("\"(", StringComparison.Ordinal) + 1));
        }

        private static void NavigatesBetweenMatchingDelimiters()
        {
            const string source = "main() out int { ret (1 + 2); }";
            int open = source.IndexOf('(', source.IndexOf("ret", StringComparison.Ordinal));
            int close = source.IndexOf(')', open);
            Equal(close, DdfDelimiterNavigation.GetMatchingPosition(source, open));
            Equal(close, DdfDelimiterNavigation.GetMatchingPosition(source, open + 1));
            Equal(open, DdfDelimiterNavigation.GetMatchingPosition(source, close));
            Equal(open, DdfDelimiterNavigation.GetMatchingPosition(source, close + 1));
        }

        private static void HandlesIncompleteSyntaxSelectionSafely()
        {
            const string source = "main() out int { if(value > 0) { ret value;";
            int caret = source.LastIndexOf("value", StringComparison.Ordinal);
            DdfTextRange range = DdfSelectionService.GetNextExpansion(source, caret, 0);
            Equal("value", source.Substring(range.Start, range.Length));
        }

        private static void FindsTokenOccurrencesOutsideProtectedText()
        {
            const string source = "int value; value << 1; // value\nstring text << \"value\";";
            int caret = source.IndexOf("value", StringComparison.Ordinal) + 2;
            IReadOnlyList<DdfTextRange> ranges = DdfMultiSelectionService.FindOccurrences(source, caret, 0);
            Equal(2, ranges.Count);
            Equal(true, ranges.All(range => source.Substring(range.Start, range.Length) == "value"));
            Equal(true, ranges.All(range => range.Start < source.IndexOf("//", StringComparison.Ordinal)));
        }

        private static void ReplacesMultipleSelections()
        {
            const string source = "value + value";
            var ranges = new[] { new DdfTextRange(0, 5), new DdfTextRange(8, 5) };
            DdfMultiEditResult result = DdfMultiSelectionService.Replace(source, ranges, "item");
            Equal("item + item", result.Text);
            SequenceEqual(new[] { 4, 11 }, result.Selections.Select(range => range.Start));
            Equal(true, result.Selections.All(range => range.Length == 0));
        }

        private static void DeletesAtMultipleCursors()
        {
            const string source = "ab cd";
            var ranges = new[] { new DdfTextRange(2, 0), new DdfTextRange(5, 0) };
            DdfMultiEditResult backspace = DdfMultiSelectionService.Backspace(source, ranges);
            Equal("a c", backspace.Text);
            SequenceEqual(new[] { 1, 3 }, backspace.Selections.Select(range => range.Start));

            DdfMultiEditResult delete = DdfMultiSelectionService.Delete(source,
                new[] { new DdfTextRange(0, 0), new DdfTextRange(3, 0) });
            Equal("b d", delete.Text);
            SequenceEqual(new[] { 0, 2 }, delete.Selections.Select(range => range.Start));
        }

        private static void AppliesContextualEditsAtMultipleSelections()
        {
            const string source = "a\nb";
            var ranges = new[] { new DdfTextRange(0, 0), new DdfTextRange(2, 0) };
            IReadOnlyList<EditorEdit> edits = ranges.Select(range =>
                EditorEditing.CreateTabEdit(source, range.Start, range.Length, false)).ToList();
            DdfMultiEditResult result = DdfMultiSelectionService.ApplyEdits(source, edits);
            Equal("    a\n    b", result.Text);
            SequenceEqual(new[] { 4, 10 }, result.Selections.Select(range => range.Start));
        }

        private static void DerivesMultilineFoldingRanges()
        {
            const string source =
                "struct Item\n{\n int value;\n}\n" +
                "main() out int\n{\n if(true)\n {\n  int nested;\n }\n { int oneLine; }\n ret 0;\n}";
            DdfParseResult parseResult = DdfParser.Parse(source);
            Equal(0, parseResult.Diagnostics.Count);
            IReadOnlyList<DdfFoldingRange> ranges = DdfFoldingRangeProvider.Create(parseResult.Root, source);
            Equal(3, ranges.Count);
            SequenceEqual(
                new[] { DdfFoldingKind.Structure, DdfFoldingKind.Function, DdfFoldingKind.Block },
                ranges.Select(range => range.Kind));
            Equal(true, ranges.All(range => source[range.Start] == '{' && source[range.End - 1] == '}'));
            Equal(true, ranges.All(range => source.Substring(range.ContentStart, range.ContentLength).Contains("\n")));
        }

        private static void HandlesIncompleteFoldingTrees()
        {
            const string source = "main() out int\n{\n if(true)\n {\n  ret 0;\n }";
            DdfParseResult parseResult = DdfParser.Parse(source);
            Equal(true, parseResult.SyntaxDiagnostics.Count > 0);
            IReadOnlyList<DdfFoldingRange> ranges = DdfFoldingRangeProvider.Create(parseResult.Root, source);
            Equal(1, ranges.Count);
            Equal(DdfFoldingKind.Block, ranges[0].Kind);
        }

        private static void CreatesSourcePreservingFoldProjection()
        {
            const string source = "main() out int\n{\n    int value;\n    ret 0;\n}";
            DdfFoldingRange range = DdfFoldingRangeProvider.Create(DdfParser.Parse(source).Root, source).Single();
            DdfFoldProjection projection = DdfFoldProjection.Create(source, range);
            Equal("main() out int\n{\n    int value;\n    ret 0;\n}", source);
            Equal(true, projection.Text.Contains("⋯ blocco compresso — 3 righe ⋯"));
            Equal(false, projection.Text.Contains("int value"));
            Equal('{', projection.Text[range.Start]);
            Equal('}', projection.Text[projection.Text.Length - 1]);
            Equal(3, projection.HiddenLineCount);

            int returnTypeStart = source.IndexOf("int", StringComparison.Ordinal);
            Equal(true, projection.TryProjectSpan(returnTypeStart, 3, out int projectedReturnType, out int returnTypeLength));
            Equal("int", projection.Text.Substring(projectedReturnType, returnTypeLength));

            int closeBrace = source.LastIndexOf('}');
            Equal(true, projection.TryProjectSpan(closeBrace, 1, out int projectedCloseBrace, out int closeBraceLength));
            Equal("}", projection.Text.Substring(projectedCloseBrace, closeBraceLength));

            int hiddenDeclaration = source.IndexOf("value", StringComparison.Ordinal);
            Equal(false, projection.TryProjectSpan(hiddenDeclaration, "value".Length, out _, out _));
            SequenceEqual(new[] { "1", "2", "⋯", "5" }, projection.LineNumberLabels);
        }

        private static void CreatesMultiRangeFoldProjection()
        {
            const string source =
                "first() out int\n{\n    int firstValue << 1;\n    ret firstValue;\n}\n" +
                "second() out int\n{\n    int secondValue << 2;\n    ret secondValue;\n}";
            IReadOnlyList<DdfFoldingRange> ranges = DdfFoldingRangeProvider
                .Create(DdfParser.Parse(source).Root, source)
                .Where(range => range.Kind == DdfFoldingKind.Function)
                .ToList();
            Equal(2, ranges.Count);

            DdfFoldProjection projection = DdfFoldProjection.Create(source, ranges);
            Equal(2, projection.Markers.Count);
            Equal(false, projection.Text.Contains("firstValue"));
            Equal(false, projection.Text.Contains("secondValue"));
            Equal(2, projection.LineNumberLabels.Count(label => label == "⋯"));

            int secondHeader = source.IndexOf("second()", StringComparison.Ordinal);
            Equal(true, projection.TryProjectSpan(secondHeader, "second".Length,
                out int projectedSecond, out int projectedLength));
            Equal("second", projection.Text.Substring(projectedSecond, projectedLength));
            Equal(true, projection.TryMapProjectedPosition(projection.Markers[1].ProjectedStart + 1,
                out int sourcePosition, out int markerRangeStart));
            Equal(ranges[1].ContentStart, sourcePosition);
            Equal(ranges[1].Start, markerRangeStart);
        }

        private static void CompletesCatalogKeywordsByPrefix()
        {
            DdfCompletionResult result = DdfCompletionService.GetCompletions("wh", 2);
            Equal(0, result.ReplacementStart);
            Equal(2, result.ReplacementLength);
            SequenceEqual(new[] { "while" }, result.Items
                .Where(item => item.Kind == DdfCompletionKind.Keyword)
                .Select(item => item.DisplayText));
        }

        private static void SuppressesCompletionInProtectedTokens()
        {
            Equal(0, DdfCompletionService.GetCompletions("// wh", 5).Items.Count);
            Equal(0, DdfCompletionService.GetCompletions("\"wh\"", 3).Items.Count);
            Equal(0, DdfCompletionService.GetCompletions("// @@'Con", 9).Items.Count);
        }

        private static void CancelsObsoleteCompletionWork()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Throws<OperationCanceledException>(() => DdfCompletionService.GetCompletions(
                    "main() out int { ret 0; }",
                    4,
                    cancellationToken: cancellation.Token));
            }
        }

        private static void OffersOnlyVisibleLocalSymbols()
        {
            const string source = "main() out int { int before;  int later; ret before; }";
            int position = source.IndexOf("int later", StringComparison.Ordinal);
            DdfCompletionResult inside = DdfCompletionService.GetCompletions(source, position, true);
            Equal(true, inside.Items.Any(item => item.DisplayText == "before" && item.Kind == DdfCompletionKind.Variable));
            Equal(false, inside.Items.Any(item => item.DisplayText == "later"));

            DdfCompletionResult outside = DdfCompletionService.GetCompletions(source, source.Length, true);
            Equal(false, outside.Items.Any(item => item.DisplayText == "before"));
            Equal(true, outside.Items.Any(item => item.DisplayText == "main" && item.Kind == DdfCompletionKind.Function));
        }

        private static void CompletesKnownLibraryNames()
        {
            const string source = "@@'Console'\n@@'Con";
            DdfCompletionResult result = DdfCompletionService.GetCompletions(source, source.Length);
            Equal(source.LastIndexOf("Con", StringComparison.Ordinal), result.ReplacementStart);
            Equal(3, result.ReplacementLength);
            SequenceEqual(new[] { "Console" }, result.Items.Select(item => item.DisplayText));
            Equal(DdfCompletionKind.Library, result.Items[0].Kind);
        }

        private static void DrivesCompletionFromAlternativeCatalog()
        {
            DdfCompletionResult result = DdfCompletionService.GetCompletions("wh", 2, false, CreateAlternativeLanguage());
            SequenceEqual(new[] { "whole", "when" }, result.Items.Select(item => item.DisplayText));
            Equal(false, result.Items.Any(item => item.DisplayText == "while"));
        }

        private static void RanksCompletionByExpectedBooleanType()
        {
            const string source = "main() out int { bool flag; int count; if(f) { ret 0; } }";
            int position = source.IndexOf("if(f", StringComparison.Ordinal) + 4;
            DdfCompletionResult result = DdfCompletionService.GetCompletions(source, position, true);
            Equal(DdfCompletionContextKind.Expression, result.Context);
            Equal("bool", result.ExpectedType);
            Equal("flag", result.Items[0].DisplayText);
            Equal("bool", result.Items[0].TypeName);
        }

        private static void RanksCompletionByFunctionReturnType()
        {
            const string source = "main() out string { int count; string text; ret  }";
            int position = source.IndexOf("ret  ", StringComparison.Ordinal) + 5;
            DdfCompletionResult result = DdfCompletionService.GetCompletions(source, position, true);
            Equal("string", result.ExpectedType);
            int textRank = result.Items.ToList().FindIndex(item => item.DisplayText == "text");
            int countRank = result.Items.ToList().FindIndex(item => item.DisplayText == "count");
            Equal(true, textRank >= 0 && countRank > textRank);
            Equal(true, result.Items.TakeWhile(item => item.DisplayText != "count")
                .Any(item => item.DisplayText == "readLine" && item.TypeName == "string"));
        }

        private static void RecognizesTypeCompletionContext()
        {
            const string source = "main() out in";
            DdfCompletionResult result = DdfCompletionService.GetCompletions(source, source.Length);
            Equal(DdfCompletionContextKind.Type, result.Context);
            Equal("int", result.Items[0].DisplayText);
            Equal(DdfCompletionKind.Type, result.Items[0].Kind);
        }

        private static void RanksFrequentlyUsedSymbols()
        {
            const string source = "main() out int { int alpha; int amber; alpha; alpha; a }";
            int position = source.LastIndexOf('a') + 1;
            DdfCompletionResult result = DdfCompletionService.GetCompletions(source, position);
            Equal("alpha", result.Items[0].DisplayText);
            Equal(true, result.Items[0].Proximity > result.Items.Single(item => item.DisplayText == "amber").Proximity);
        }

        private static void ExposesCompletionMetadata()
        {
            DdfDocumentSymbol external = DdfSymbolIndex.Create(
                DdfParser.Parse("helper() out int { ret 1; }").Root).Symbols.Single();
            DdfCompletionItem externalItem = DdfCompletionService.CreateSymbolItem(external, "lib/helpers.ddf");
            DdfCompletionResult result = DdfCompletionService.GetCompletions(
                "main() out int { ret hel; }", "main() out int { ret hel".Length,
                externalItems: new[] { externalItem });
            DdfCompletionItem helper = result.Items.Single(item => item.DisplayText == "helper");
            Equal("funzione", helper.CategoryLabel);
            Equal("int", helper.TypeName);
            Equal("lib/helpers.ddf", helper.Origin);
            Equal(true, helper.ToString().Contains("ƒ") && helper.ToString().Contains("lib/helpers.ddf"));
        }

        private static void OffersSnippetsInStatementContext()
        {
            const string statement = "main() out int {\n    i";
            DdfCompletionResult statementResult = DdfCompletionService.GetCompletions(statement, statement.Length);
            DdfCompletionItem ifSnippet = statementResult.Items.Single(item =>
                item.Kind == DdfCompletionKind.Snippet && item.Snippet.Prefix == "if");
            Equal("if — blocco", ifSnippet.DisplayText);
            Equal("snippet", ifSnippet.CategoryLabel);

            const string expression = "main() out int { ret i";
            DdfCompletionResult expressionResult = DdfCompletionService.GetCompletions(expression, expression.Length);
            Equal(false, expressionResult.Items.Any(item => item.Kind == DdfCompletionKind.Snippet));
        }

        private static void ExpandsSnippetsWithIndentation()
        {
            DdfSnippetTemplate template = DdfSnippetCatalog.Templates.Single(item => item.Prefix == "if");
            DdfSnippetExpansion expansion = DdfSnippetService.Expand(template, "    ");
            const string expected =
                "if(condition)\n" +
                "    {\n" +
                "        statement;\n" +
                "    }";
            Equal(expected, expansion.Text);
            Equal(2, expansion.Placeholders.Count);
            Equal("condition", expansion.Text.Substring(
                expansion.Placeholders[0].Start, expansion.Placeholders[0].Length));
            Equal("statement", expansion.Text.Substring(
                expansion.Placeholders[1].Start, expansion.Placeholders[1].Length));
            Equal(expansion.Text.Length, expansion.FinalCaret);
            Equal("    ", DdfSnippetService.GetLineIndent("main()\n    if", 11));
        }

        private static void ShowsActiveLocalFunctionParameter()
        {
            const string source =
                "sum(int left, int right) out int { ret left + right; } " +
                "main() out int { ret sum(1, ); }";
            int position = source.IndexOf(", )", StringComparison.Ordinal) + 2;
            DdfSignatureHelpResult result = DdfSignatureHelpService.GetSignatureHelp(source, position);
            Equal(true, result != null);
            Equal("sum(int left, int right) out int", result.Signature.Signature);
            Equal("documento corrente", result.Signature.Origin);
            Equal(1, result.ActiveParameter);
            Equal("int right", result.ActiveParameterInformation.DisplayText);
        }

        private static void ResolvesStandardFunctionSignatureHelp()
        {
            const string source = "main() out int { print(\"a,b\"); ret 0; }";
            int position = source.IndexOf("b\"", StringComparison.Ordinal) + 1;
            DdfSignatureHelpResult result = DdfSignatureHelpService.GetSignatureHelp(source, position);
            Equal(true, result != null);
            Equal("print(string) out void", result.Signature.Signature);
            Equal("libreria standard", result.Signature.Origin);
            Equal(0, result.ActiveParameter);
            Equal("string", result.ActiveParameterInformation.DisplayText);
        }

        private static void TracksNestedCallSignatureHelp()
        {
            const string source =
                "inner(int first, int second) out int { ret first; } " +
                "outer(int left, int right) out int { ret right; } " +
                "main() out int { ret outer(inner(1, 2), 3); }";
            int innerPosition = source.IndexOf("2),", StringComparison.Ordinal) + 1;
            DdfSignatureHelpResult inner = DdfSignatureHelpService.GetSignatureHelp(source, innerPosition);
            Equal("inner", inner.Signature.Name);
            Equal(1, inner.ActiveParameter);

            int outerPosition = source.IndexOf("), 3", StringComparison.Ordinal) + 3;
            DdfSignatureHelpResult outer = DdfSignatureHelpService.GetSignatureHelp(source, outerPosition);
            Equal("outer", outer.Signature.Name);
            Equal(1, outer.ActiveParameter);
        }

        private static void ResolvesWorkspaceFunctionSignatureHelp()
        {
            const string source = "main() out int { ret helper(1, ); }";
            CompilationUnitSyntax external = DdfParser.Parse(
                "helper(int id, string name) out int { ret id; }").Root;
            int position = source.IndexOf(", )", StringComparison.Ordinal) + 2;
            DdfSignatureHelpResult result = DdfSignatureHelpService.GetSignatureHelp(
                source, position, new[] { external });
            Equal(true, result != null);
            Equal("workspace", result.Signature.Origin);
            Equal("string name", result.ActiveParameterInformation.DisplayText);
        }

        private static void SuppressesSignatureHelpOutsideCalls()
        {
            const string declaration = "sum(int left, int right) out int { ret left + right; }";
            int declarationPosition = declaration.IndexOf("left", StringComparison.Ordinal);
            Equal<DdfSignatureHelpResult>(null,
                DdfSignatureHelpService.GetSignatureHelp(declaration, declarationPosition));
            Equal<DdfSignatureHelpResult>(null,
                DdfSignatureHelpService.GetSignatureHelp("main() out int { if(true) { ret 1; } }", 24));
            const string comment = "main() out int { print(1); // print(2, 3)\n ret 0; }";
            int commentPosition = comment.IndexOf("3)", StringComparison.Ordinal);
            Equal<DdfSignatureHelpResult>(null,
                DdfSignatureHelpService.GetSignatureHelp(comment, commentPosition));
        }

        private static void BuildsStructuredHoverInformation()
        {
            const string source =
                "/// Somma due valori.\n" +
                "/// Restituisce il risultato intero.\n" +
                "sum(int left, int right) out int { ret left + right; }\n" +
                "main() out int { sum(1, 2); ret sum(3, 4); }";
            CompilationUnitSyntax root = DdfParser.Parse(source).Root;
            DdfSemanticModel model = DdfSemanticModel.Create(source, root);
            DdfDocumentSymbol symbol = model.FindOccurrence(source.IndexOf("sum(int", StringComparison.Ordinal)).Symbol;
            DdfHoverInfo hover = DdfHoverService.CreateForSymbol(symbol, model, source, "math.ddf");
            Equal("sum(int left, int right) out int", hover.Signature);
            Equal("int", hover.TypeName);
            Equal("math.ddf", hover.Origin);
            Equal("Somma due valori. Restituisce il risultato intero.", hover.Documentation);
            Equal(3, hover.DeclarationLine);
            Equal(1, hover.DeclarationColumn);
            Equal(2, hover.ReferenceCount);
            Equal(true, hover.ToDisplayText().Contains("Riferimenti: 2") &&
                        hover.ToDisplayText().Contains("Firma:"));
        }

        private static void MapsHoverReferencesFromIndependentIndex()
        {
            const string source = "value() out int { ret 1; } main() out int { ret value(); }";
            CompilationUnitSyntax root = DdfParser.Parse(source).Root;
            DdfDocumentSymbol independent = DdfSymbolIndex.Create(root).Symbols.First();
            DdfSemanticModel model = DdfSemanticModel.Create(source, root);
            DdfHoverInfo hover = DdfHoverService.CreateForSymbol(independent, model, source, "workspace/value.ddf");
            Equal(1, hover.ReferenceCount);
            Equal("workspace/value.ddf", hover.Origin);
        }

        private static void DocumentsStandardFunctionsInHover()
        {
            Equal(true, DdfRuntimeCatalog.TryGetStandardFunction("length", out DdfStandardFunction function));
            DdfHoverInfo hover = DdfHoverService.CreateForStandardFunction(function);
            Equal("length(string) out int", hover.Signature);
            Equal("int", hover.TypeName);
            Equal("libreria standard", hover.Origin);
            Equal(true, hover.Documentation.Contains("caratteri"));
            Equal(0, hover.ReferenceCount);
        }

        private static void FormatsCompleteDocument()
        {
            const string source = "@@'Console'\nmain()out int{int value<<1+2*3;ret value;}";
            const string expected =
                "@@'Console'\n\n" +
                "main() out int\n" +
                "{\n" +
                "    int value << 1 + 2 * 3;\n" +
                "\n" +
                "    ret value;\n" +
                "}";
            Equal(expected, DdfFormatter.Format(source).Text);
        }

        private static void FormatsIdempotently()
        {
            const string source = "main() out int\n{\n    int value << 1;\n\n    ret value;\n}";
            string once = DdfFormatter.Format(source).Text;
            Equal(source, once);
            Equal(once, DdfFormatter.Format(once).Text);
        }

        private static void PreservesProtectedTextWhileFormatting()
        {
            const string source =
                "@@'Console'\nmain()out int{/* keep   { spacing } */string text<<\"a  +  b\";// note   here\nret 0;}";
            string formatted = DdfFormatter.Format(source).Text;
            Equal(true, formatted.Contains("@@'Console'"));
            Equal(true, formatted.Contains("/* keep   { spacing } */"));
            Equal(true, formatted.Contains("\"a  +  b\""));
            Equal(true, formatted.Contains("; // note   here"));
        }

        private static void FormatsIncompleteSourceSafely()
        {
            const string source = "main( out int{int value<<\"unfinished";
            string formatted = DdfFormatter.Format(source).Text;
            Equal(true, formatted.Contains("main("));
            Equal(true, formatted.Contains("int value << \"unfinished"));
            Equal("main(out int\n{\n    int value << \"unfinished", formatted);
        }

        private static void MapsCaretThroughFormatting()
        {
            const string source = "main()out int{ret value;}";
            int caret = source.IndexOf("value", StringComparison.Ordinal) + "value".Length;
            DdfFormatResult result = DdfFormatter.Format(source);
            int mapped = result.MapPosition(caret);
            Equal("value", result.Text.Substring(mapped - "value".Length, "value".Length));
            EditorEdit edit = result.CreateEdit(source, caret, 0);
            Equal(mapped, edit.SelectionStart);
            Equal(0, edit.SelectionLength);
        }

        private static void DrivesFormattingFromAlternativeCatalog()
        {
            const string source = "main()returns whole{whole value:=1%%2;give value;}";
            const string expected =
                "main() returns whole\n" +
                "{\n" +
                "    whole value := 1 %% 2;\n" +
                "\n" +
                "    give value;\n" +
                "}";
            Equal(expected, DdfFormatter.Format(source, CreateAlternativeLanguage()).Text);
        }

        private static void FormatsControlFlowCallsAndArrays()
        {
            const string source =
                "main()out int{int[3] values;for(int index;index<3;index++){if(true){values[index]<<call(index,2);}}ret 0;}";
            const string expected =
                "main() out int\n" +
                "{\n" +
                "    int[3] values;\n" +
                "\n" +
                "    for(int index; index < 3; index++)\n" +
                "    {\n" +
                "        if(true)\n" +
                "        {\n" +
                "            values[index] << call(index, 2);\n" +
                "        }\n" +
                "    }\n" +
                "\n" +
                "    ret 0;\n" +
                "}";
            string formatted = DdfFormatter.Format(source).Text;
            Equal(expected, formatted);
            Equal(formatted, DdfFormatter.Format(formatted).Text);
        }

        private static void SeparatesFormattedStatementsAndContexts()
        {
            const string source =
                "main()out int{int value<<0;if(value<1){value++;}while(value<2){value++;}" +
                "do{value++;}while(value<3);ret value;}";
            const string expected =
                "main() out int\n" +
                "{\n" +
                "    int value << 0;\n" +
                "\n" +
                "    if(value < 1)\n" +
                "    {\n" +
                "        value++;\n" +
                "    }\n" +
                "\n" +
                "    while(value < 2)\n" +
                "    {\n" +
                "        value++;\n" +
                "    }\n" +
                "\n" +
                "    do\n" +
                "    {\n" +
                "        value++;\n" +
                "    } while(value < 3);\n" +
                "\n" +
                "    ret value;\n" +
                "}";
            string formatted = DdfFormatter.Format(source).Text;
            Equal(expected, formatted);
            Equal(formatted, DdfFormatter.Format(formatted).Text);
        }

        private static void AssertLexResultsEqual(DdfLexResult expected, DdfLexResult actual)
        {
            Equal(expected.Tokens.Count, actual.Tokens.Count);
            for (int index = 0; index < expected.Tokens.Count; index++)
            {
                Equal(expected.Tokens[index].Kind, actual.Tokens[index].Kind);
                Equal(expected.Tokens[index].Start, actual.Tokens[index].Start);
                Equal(expected.Tokens[index].Length, actual.Tokens[index].Length);
            }

            Equal(expected.Diagnostics.Count, actual.Diagnostics.Count);
            for (int index = 0; index < expected.Diagnostics.Count; index++)
            {
                Equal(expected.Diagnostics[index].Code, actual.Diagnostics[index].Code);
                Equal(expected.Diagnostics[index].Start, actual.Diagnostics[index].Start);
                Equal(expected.Diagnostics[index].Length, actual.Diagnostics[index].Length);
                Equal(expected.Diagnostics[index].Line, actual.Diagnostics[index].Line);
                Equal(expected.Diagnostics[index].Column, actual.Diagnostics[index].Column);
            }
        }

        private static string CreateTemporaryDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "DDFLanguageEditor.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string Apply(string source, EditorEdit edit)
        {
            return source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
        }

        private static string Slice(string source, ClassifiedSpan span)
        {
            return source.Substring(span.Start, span.Length);
        }

        private static void ExecutesFunctionsControlFlowAndOutput()
        {
            const string source = @"sum(int limit) out int
{
    int total << 0;
    for(int index << 0; index < limit; index++)
    {
        total << total + index;
    }
    ret total;
}
main() out int
{
    int result << sum(5);
    result >> Console;
    ret result;
}";
            var output = new List<string>();
            DdfExecutionResult result = DdfInterpreter.Execute(source, new DdfExecutionOptions { Output = output.Add });
            Equal(true, result.Succeeded);
            Equal(10, result.ReturnValue);
            Equal("10", output.Single());
            Equal(0, DdfSemanticModel.Create(source, DdfParser.Parse(source).Root).Diagnostics.Count);
        }

        private static void ExecutesArraysAndStructureMembers()
        {
            const string source = @"struct Point
{
    int x;
    int y;
}
main() out int
{
    int[2] values;
    Point point;
    values[0] << 4;
    point.x << values[0] + 3;
    ret point.x;
}";
            DdfExecutionResult result = DdfInterpreter.Execute(source);
            Equal(true, result.Succeeded);
            Equal(7, result.ReturnValue);
        }

        private static void StopsInfiniteExecutionAtInstructionLimit()
        {
            const string source = "main() out void { while(true) { } }";
            DdfExecutionResult result = DdfInterpreter.Execute(source, new DdfExecutionOptions { MaxInstructions = 50 });
            Equal(false, result.Succeeded);
            Equal("DDF407", result.Diagnostics.Single().Code);
        }

        private static void HonorsRuntimeCancellation()
        {
            bool cancel = false;
            DdfExecutionResult result = DdfInterpreter.Execute(
                "main() out void { while(true) { } }",
                new DdfExecutionOptions
                {
                    MaxInstructions = 1000,
                    CancellationRequested = () => { cancel = true; return cancel; }
                });
            Equal(true, result.WasCancelled);
            Equal(0, result.Diagnostics.Count);
        }

        private static void PausesAndContinuesAtLineBreakpoints()
        {
            const string source = "main() out int\n{\n    int value << 1;\n    value << value + 1;\n    ret value;\n}";
            var pauseReached = new ManualResetEventSlim(false);
            DdfDebugPauseInfo pause = null;
            using (var debugger = new DdfDebuggerSession())
            {
                debugger.SetBreakpoints(new[] { 4 });
                debugger.Paused = info => { pause = info; pauseReached.Set(); };
                Task<DdfExecutionResult> execution = Task.Run(() => DdfInterpreter.Execute(source, new DdfExecutionOptions
                {
                    DebuggerSession = debugger
                }));

                Equal(true, pauseReached.Wait(2000));
                Equal(true, debugger.IsPaused);
                Equal(false, execution.IsCompleted);
                Equal(4, pause.Line);
                Equal("value << value + 1;", source.Substring(pause.Start, pause.Length));

                debugger.Continue();
                Equal(true, execution.Wait(2000));
                Equal(true, execution.Result.Succeeded);
                Equal(2, execution.Result.ReturnValue);
                Equal(false, debugger.IsPaused);
            }
            pauseReached.Dispose();

            const string loopSource = "main() out int\n{\n    int value << 0;\n    while(value < 3)\n    {\n        value++;\n    }\n    ret value;\n}";
            int pauses = 0;
            using (var loopDebugger = new DdfDebuggerSession())
            {
                loopDebugger.SetBreakpoints(new[] { 6 });
                loopDebugger.Paused = info =>
                {
                    Interlocked.Increment(ref pauses);
                    loopDebugger.Continue();
                };
                DdfExecutionResult loopResult = DdfInterpreter.Execute(loopSource, new DdfExecutionOptions
                {
                    DebuggerSession = loopDebugger
                });
                Equal(true, loopResult.Succeeded);
                Equal(3, loopResult.ReturnValue);
                Equal(3, pauses);
            }
        }

        private static void ReportsRuntimeFailuresWithSourcePositions()
        {
            const string source = "main() out int\n{\n    int zero << 0;\n    ret 4 / zero;\n}";
            DdfExecutionResult result = DdfInterpreter.Execute(source);
            DdfDiagnostic diagnostic = result.Diagnostics.Single();
            Equal("DDF404", diagnostic.Code);
            Equal(4, diagnostic.Line);
        }

        private static void CapturesNavigableRuntimeStackTraces()
        {
            const string source = @"fail() out int
{
    int zero << 0;
    ret 10 / zero;
}
middle() out int
{
    ret fail();
}
main() out int
{
    ret middle();
}";
            DdfExecutionResult result = DdfInterpreter.Execute(source);
            Equal(false, result.Succeeded);
            Equal("DDF404", result.Diagnostics.Single().Code);
            Equal(3, result.StackTrace.Count);
            Equal("fail", result.StackTrace[0].FunctionName);
            Equal("middle", result.StackTrace[1].FunctionName);
            Equal("main", result.StackTrace[2].FunctionName);
            Equal(8, result.StackTrace[0].Line);
            Equal("fail", source.Substring(result.StackTrace[0].Start, result.StackTrace[0].Length));
            Equal("middle", source.Substring(result.StackTrace[1].Start, result.StackTrace[1].Length));

            DdfExecutionResult standardFailure = DdfInterpreter.Execute("main() out int { ret toInt(\"abc\"); }");
            Equal(2, standardFailure.StackTrace.Count);
            Equal("toInt", standardFailure.StackTrace[0].FunctionName);
            Equal("main", standardFailure.StackTrace[1].FunctionName);
        }

        private static void ExecutesMinimalStandardLibraryWithInput()
        {
            const string source = @"main() out int
{
    string text << readLine();
    print(text);
    int size << length(text);
    ret toInt(text) + size;
}";
            var output = new List<string>();
            DdfParseResult parse = DdfParser.Parse(source);
            Equal(0, DdfSemanticModel.Create(source, parse.Root).Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(source, parse.Root).Diagnostics.Count);
            DdfExecutionResult result = DdfInterpreter.Execute(source, parse.Root, new DdfExecutionOptions
            {
                Input = () => "41",
                Output = output.Add
            });
            Equal(true, result.Succeeded);
            Equal(43, result.ReturnValue);
            Equal("41", output.Single());
            Equal(true, DdfCompletionService.GetCompletions("rea", 3).Items.Any(item => item.DisplayText == "readLine"));
        }

        private static void ReportsInvalidStandardConversions()
        {
            DdfExecutionResult result = DdfInterpreter.Execute("main() out int { ret toInt(\"abc\"); }");
            Equal(false, result.Succeeded);
            Equal("DDF408", result.Diagnostics.Single().Code);
        }

        private sealed class CustomQuickFixProvider : IDdfQuickFixProvider
        {
            public IEnumerable<DdfQuickFix> GetFixes(string source, DdfDiagnostic diagnostic)
            {
                if (diagnostic.Code == "CUSTOM")
                    yield return new DdfQuickFix(diagnostic, "Sostituisci con fixed", 0, source.Length, "fixed", 5);
            }
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                passedTests++;
                Console.WriteLine("PASS " + name);
            }
            catch (Exception exception)
            {
                Failures.Add(name + ": " + exception.Message);
            }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException("expected <" + expected + "> but was <" + actual + ">");
            }
        }

        private static void False(bool value)
        {
            if (value)
            {
                throw new InvalidOperationException("expected false but was true");
            }
        }

        private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            if (!expected.SequenceEqual(actual))
            {
                throw new InvalidOperationException(
                    "expected <" + string.Join(", ", expected) + "> but was <" +
                    string.Join(", ", actual) + ">");
            }
        }

        private static void Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException("expected exception " + typeof(TException).Name);
        }
    }
}
