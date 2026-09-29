using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
            Run("enforces the DDF 0.12 operator contract", EnforcesDdf012OperatorContract);
            Run("enforces the DDF 0.12 lexical contract", EnforcesDdf012LexicalContract);
            Run("inserts four spaces for Tab", InsertsTab);
            Run("uses configured tabs for editing and formatting", UsesConfiguredTabs);
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
            Run("exposes isolated host document services", ExposesIsolatedHostDocumentServices);
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
            Run("enforces the DDF 0.12 compilation-unit syntax", EnforcesDdf012CompilationUnitSyntax);
            Run("recovers from multiple syntax errors", RecoversFromMultipleSyntaxErrors);
            Run("keeps AST spans within the source", KeepsAstSpansWithinSource);
            Run("parses the editor smoke sample", ParsesEditorSmokeSample);
            Run("drives lexing from a language catalog", DrivesLexingFromLanguageCatalog);
            Run("drives parsing from syntax roles", DrivesParsingFromSyntaxRoles);
            Run("rejects duplicate catalog entries", RejectsDuplicateCatalogEntries);
            Run("loads the embedded DDF language package", LoadsEmbeddedDdfLanguagePackage);
            Run("parses the canonical EBNF grammar", ParsesCanonicalEbnfGrammar);
            Run("drives structural IDE services from grammar metadata", DrivesStructuralIdeServicesFromGrammarMetadata);
            Run("drives IntelliSense presentation from its package section", DrivesIntellisenseFromPackageSection);
            Run("drives formatting editing and Quick Fixes from the package", DrivesFormattingFromPackageSection);
            Run("drives type checking from the package", DrivesTypeCheckingFromPackageSection);
            Run("rejects invalid type systems atomically", RejectsInvalidTypeSystemsAtomically);
            Run("plans safe semantic operations from the package", PlansSafeSemanticOperationsFromPackage);
            Run("rejects unsafe semantic operations atomically", RejectsUnsafeSemanticOperationsAtomically);
            Run("separates diagnostic domains", SeparatesDiagnosticDomains);
            Run("requires explicit authorization for native extensions", RequiresExplicitNativeExtensionAuthorization);
            Run("rejects invalid native extension identities atomically", RejectsInvalidNativeExtensionIdentitiesAtomically);
            Run("lowers DDF to a common intermediate representation", LowersDdfToCommonIntermediateRepresentation);
            Run("builds a source-linked flowchart from intermediate representation", BuildsSourceLinkedFlowchart);
            Run("models pre-test post-test and counted loops in flowcharts", ModelsAllFlowchartLoopForms);
            Run("validates the complex flowchart regression matrix", ValidatesComplexFlowchartRegressionMatrix);
            Run("collapses linear flowchart sequences without losing source links", CollapsesLinearFlowchartSequences);
            Run("keeps only the newest asynchronous flowchart snapshot", KeepsNewestFlowchartSnapshot);
            Run("marks recoverable flowchart snapshots as partial", MarksRecoverableFlowchartSnapshotsAsPartial);
            Run("builds coherent versioned editor analysis snapshots", BuildsCoherentEditorAnalysisSnapshots);
            Run("keeps only the newest asynchronous editor analysis snapshot", KeepsNewestEditorAnalysisSnapshot);
            Run("resolves editor definitions across supplied buffers", ResolvesEditorDefinitionsAcrossBuffers);
            Run("lays out flowcharts deterministically without overlapping ranks", LaysOutFlowchartsDeterministically);
            Run("executes and diagnoses from intermediate representation", ExecutesAndDiagnosesFromIntermediateRepresentation);
            Run("runs isolated host execution sessions", RunsIsolatedHostExecutionSessions);
            Run("shares an explicit host catalog across IntelliSense type checking and runtime", SharesHostCatalogAcrossPipelines);
            Run("enforces host value adapters access and lifetimes", EnforcesHostValueAdaptersAccessAndLifetimes);
            Run("returns immutable typed host values after execution", ReturnsImmutableTypedHostValuesAfterExecution);
            Run("invokes typed procedures manually and from host events", InvokesTypedProceduresManuallyAndFromHostEvents);
            Run("separates analysis simulation and host effects", SeparatesAnalysisSimulationAndHostEffects);
            Run("bounds asynchronous host operations and releases resources", BoundsAsyncHostOperationsAndReleasesResources);
            Run("executes parameterized typed database services", ExecutesParameterizedTypedDatabaseServices);
            Run("registers typed host function libraries atomically", RegistersTypedHostFunctionLibrariesAtomically);
            Run("reports and rolls back partial host effects", ReportsAndRollsBackPartialHostEffects);
            Run("builds immutable host document snapshots", BuildsImmutableHostDocumentSnapshots);
            Run("routes host builds through a custom public handler", RoutesHostBuildsThroughCustomHandler);
            Run("routes host execution through a custom public build handler", RoutesHostExecutionThroughCustomBuildHandler);
            Run("debugs host buffers with document breakpoints", DebugsHostBuffersWithDocumentBreakpoints);
            Run("executes package operators through syntax-independent VM operations", ExecutesPackageOperatorsThroughVmOperations);
            Run("loads and executes package-defined standard functions", LoadsAndExecutesPackageDefinedStandardFunctions);
            Run("enforces the versioned minimum standard library contract", EnforcesMinimumStandardLibraryContract);
            Run("keeps the autonomous specification aligned with the canonical package", KeepsAutonomousSpecificationAligned);
            Run("keeps the DDF version policy aligned", KeepsDdfVersionPolicyAligned);
            Run("keeps the DDF candidate baseline frozen", KeepsDdfCandidateBaselineFrozen);
            Run("verifies the DDF candidate toolchain against the normative corpus", VerifiesDdfCandidateToolchainAgainstNormativeCorpus);
            Run("rejects unsupported VM and host operations atomically", RejectsUnsupportedVmAndHostOperationsAtomically);
            Run("loads a project manifest independently from the language package", LoadsProjectManifestIndependentlyFromLanguagePackage);
            Run("builds reproducible VM artifacts from unsaved buffers", BuildsReproducibleVmArtifactsFromUnsavedBuffers);
            Run("rejects unsafe or mismatched project configurations", RejectsUnsafeOrMismatchedProjectConfigurations);
            Run("resolves versioned modules with export visibility and cycle checks", ResolvesVersionedModulesWithExportVisibilityAndCycleChecks);
            Run("validates the shipped sample project", ValidatesShippedSampleProject);
            Run("validates the shipped multi-file flowchart workspace", ValidatesShippedFlowchartWorkspace);
            Run("validates the shipped versioned module workspace", ValidatesShippedModuleWorkspace);
            Run("exposes direct module exports to live editor analysis", ExposesModuleExportsToEditorAnalysis);
            Run("packs installs replaces and removes versioned modules safely", ManagesVersionedModulePackagesSafely);
            Run("installs and updates versioned language packages", InstallsAndUpdatesVersionedLanguagePackages);
            Run("rejects failing declarative package tests", RejectsFailingDeclarativePackageTests);
            Run("accepts extensible keyword roles and else branches", AcceptsExtensibleKeywordRolesAndElseBranches);
            Run("rejects invalid EBNF grammars atomically", RejectsInvalidEbnfGrammarsAtomically);
            Run("loads directory and archive language packages", LoadsDirectoryAndArchiveLanguagePackages);
            Run("switches active language packages atomically", SwitchesActiveLanguagePackagesAtomically);
            Run("isolates language package sessions per host", IsolatesLanguagePackageSessionsPerHost);
            Run("drives lexical services from a language package", DrivesLexicalServicesFromLanguagePackage);
            Run("executes a syntax-distinct demonstration language", ExecutesSyntaxDistinctDemonstrationLanguage);
            Run("keeps the shipped syntax profiles semantically equivalent", KeepsShippedSyntaxProfilesSemanticallyEquivalent);
            Run("converts equivalent syntax profiles without touching protected text", ConvertsEquivalentSyntaxProfilesSafely);
            Run("drives diagnostics and flowchart presentation from language packages", DrivesPresentationFromLanguagePackages);
            Run("validates the normative syntax profile corpus", ValidatesNormativeSyntaxProfileCorpus);
            Run("rejects unreachable lexical rules", RejectsUnreachableLexicalRules);
            Run("rejects invalid language packages atomically", RejectsInvalidLanguagePackagesAtomically);
            Run("falls back to the embedded language package", FallsBackToEmbeddedLanguagePackage);
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
            Run("indexes workspace extensions from the active package", IndexesConfiguredWorkspaceExtensions);
            Run("updates an in-memory workspace document", UpdatesInMemoryWorkspaceDocument);
            Run("updates workspace source without synchronous parsing", UpdatesWorkspaceSourceWithoutParsing);
            Run("honors workspace indexing cancellation", HonorsWorkspaceIndexingCancellation);
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
            Run("enforces the DDF 0.12 compound type and scope rules", EnforcesDdf012CompoundTypeAndScopeRules);
            Run("validates the normative DDF type corpus", ValidatesNormativeDdfTypeCorpus);
            Run("validates the complete normative DDF language corpus", ValidatesCompleteNormativeDdfLanguageCorpus);
            Run("enforces the DDF 0.12 runtime numeric contract", EnforcesDdf012RuntimeNumericContract);
            Run("type-checks the editor smoke sample", TypeChecksEditorSmokeSample);
            Run("executes functions control flow and output", ExecutesFunctionsControlFlowAndOutput);
            Run("executes arrays and structure members", ExecutesArraysAndStructureMembers);
            Run("stops infinite execution at the instruction limit", StopsInfiniteExecutionAtInstructionLimit);
            Run("honors runtime cancellation", HonorsRuntimeCancellation);
            Run("disposes paused debugger without racing its waiter", DisposesPausedDebuggerWithoutRacingWaiter);
            Run("pauses and continues at line breakpoints", PausesAndContinuesAtLineBreakpoints);
            Run("steps into and over calls and steps out to callers", StepsThroughFunctionCalls);
            Run("executes and debugs functions across project files", ExecutesAndDebugsProjectFiles);
            Run("evaluates read-only watches from immutable pause snapshots", EvaluatesWatches);
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
            const string text = "a != b && c ^^ d";
            string[] operators = DdfSyntaxClassifier.Classify(text)
                .Where(span => span.Kind == SyntaxKind.Operator)
                .Select(span => Slice(text, span))
                .ToArray();
            SequenceEqual(new[] { "!=", "&&", "^^" }, operators);
        }

        private static void EnforcesDdf012OperatorContract()
        {
            DdfLanguageDefinition language = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package.Language;
            string[] operators = language.Operators.Select(item => item.Text).ToArray();
            Equal(true, operators.Contains("<<"));
            Equal(true, operators.Contains(">>"));
            Equal(true, operators.Contains("&&"));
            Equal(true, operators.Contains("||"));
            Equal(true, operators.Contains("^^"));
            Equal(true, operators.Contains("!="));
            Equal(false, operators.Contains("&"));
            Equal(false, operators.Contains("|&|"));
            Equal(false, operators.Contains("!!"));
            Equal(false, operators.Contains(">><<"));
            Equal(false, operators.Contains("<<>>"));
            Equal(false, operators.Contains("^/"));

            const string source = "main() out bool { ret true || false ^^ true && 1 < 2 == false; }";
            DdfParseResult parse = DdfParser.Parse(source, language);
            Equal(0, parse.Diagnostics.Count);
            var function = (FunctionDeclarationSyntax)parse.Root.Members.Single();
            var logicalOr = (BinaryExpressionSyntax)((ReturnStatementSyntax)function.Body.Statements.Single()).Expression;
            Equal("||", logicalOr.OperatorText);
            var logicalXor = (BinaryExpressionSyntax)logicalOr.Right;
            Equal("^^", logicalXor.OperatorText);
            var logicalAnd = (BinaryExpressionSyntax)logicalXor.Right;
            Equal("&&", logicalAnd.OperatorText);
            Equal("==", ((BinaryExpressionSyntax)logicalAnd.Right).OperatorText);
        }

        private static void EnforcesDdf012LexicalContract()
        {
            DdfLanguageDefinition language = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package.Language;

            DdfLexResult booleans = DdfLexer.Lex("true false True False", language);
            SequenceEqual(new[]
            {
                DdfTokenKind.BooleanLiteral, DdfTokenKind.BooleanLiteral,
                DdfTokenKind.Identifier, DdfTokenKind.Identifier
            }, booleans.Tokens.Select(item => item.Kind));

            DdfLexResult validString = DdfLexer.Lex("\"testo\\n\\t\\\"\\\\\"", language);
            Equal(0, validString.Diagnostics.Count);

            DdfLexResult invalidEscape = DdfLexer.Lex("\"testo\\q\"", language);
            Equal(true, invalidEscape.Diagnostics.Any(item => item.Code == "DDF005"));

            DdfLexResult multiline = DdfLexer.Lex("\"prima\nseconda\"", language);
            Equal(true, multiline.Diagnostics.Any(item => item.Code == "DDF001"));

            DdfLexResult nonAsciiDigit = DdfLexer.Lex("١", language);
            Equal(DdfTokenKind.BadToken, nonAsciiDigit.Tokens.Single().Kind);
            Equal("DDF004", nonAsciiDigit.Diagnostics.Single().Code);

            DdfLexResult unicodeIdentifier = DdfLexer.Lex("temperatura_è2", language);
            Equal(DdfTokenKind.Identifier, unicodeIdentifier.Tokens.Single().Kind);
        }

        private static void InsertsTab()
        {
            EditorEdit edit = EditorEditing.CreateTabEdit("ab", 1, 0, false);
            Equal("a    b", Apply("ab", edit));
            Equal(5, edit.SelectionStart);
        }

        private static void UsesConfiguredTabs()
        {
            EditorEdit tab = EditorEditing.CreateTabEdit("ab", 1, 0, false, 2, true);
            Equal("a\tb", Apply("ab", tab));
            EditorEdit enter = EditorEditing.CreateNewLineEdit("if(true){", 9, 0, 2, true);
            Equal("if(true){\n\t", Apply("if(true){", enter));
            const string lines = "one\ntwo";
            EditorEdit indent = EditorEditing.CreateTabEdit(lines, 0, lines.Length, false, 2, true);
            string indented = Apply(lines, indent);
            Equal("\tone\n\ttwo", indented);
            Equal(lines, Apply(indented, EditorEditing.CreateTabEdit(
                indented, indent.SelectionStart, indent.SelectionLength, true, 2, true)));
            string formatted = DdfFormatter.Format("main()out int{ret 1;}", null, 2, true).Text;
            Equal(true, formatted.Contains("\n\tret 1;"));
            DdfSnippetExpansion snippet = DdfSnippetService.Expand(
                DdfSnippetCatalog.Templates.Single(item => item.Prefix == "if"), string.Empty, "\t");
            Equal(true, snippet.Text.Contains("\n\tstatement;"));
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

        private static void ExposesIsolatedHostDocumentServices()
        {
            IDdfEditorDocumentService first = new OpenDocumentCollection();
            IDdfEditorDocumentService second = new OpenDocumentCollection();
            var events = new List<string>();
            first.DocumentAdded += (sender, args) => events.Add("added:" + args.Document.Id);
            first.ActiveDocumentChanged += (sender, args) =>
                events.Add("active:" + (args.Document?.Id ?? "none"));
            first.DocumentChanged += (sender, args) =>
                events.Add("changed:" + args.Document.Version + ":" + args.SourceChanged);
            first.DocumentClosed += (sender, args) => events.Add("closed:" + args.Document.Id);

            IDdfEditorDocument virtualDocument = first.CreateVirtual("Calcolo temporaneo.ddf", "int value << 1;");
            Equal("Calcolo temporaneo.ddf", virtualDocument.DisplayName);
            Equal(true, virtualDocument.IsVirtual);
            Equal(null, virtualDocument.FilePath);
            Equal(1L, virtualDocument.Version);
            Equal(false, virtualDocument.IsDirty);
            Equal(0, second.OpenDocuments.Count);

            Equal(true, first.UpdateDocument(virtualDocument.Id, "int value << 2;"));
            Equal(2L, virtualDocument.Version);
            Equal(true, virtualDocument.IsDirty);
            Equal(false, first.TryCloseDocument(virtualDocument.Id));
            Equal(1, first.OpenDocuments.Count);

            string virtualPath = Path.Combine(Path.GetTempPath(), "host-virtual.ddf");
            Equal(true, first.MarkDocumentSaved(virtualDocument.Id, virtualPath));
            Equal(false, virtualDocument.IsVirtual);
            Equal(false, virtualDocument.IsDirty);
            Equal(Path.GetFullPath(virtualPath), virtualDocument.FilePath);
            IDdfEditorDocument diskDocument = first.OpenDocument(
                Path.Combine(Path.GetTempPath(), "host-disk.ddf"), "main() out int { ret 3; }");
            Equal(false, diskDocument.IsVirtual);
            Equal(diskDocument.Id, first.ActiveDocumentInfo.Id);
            Equal(true, first.ActivateDocument(virtualDocument.Id));
            Equal(virtualDocument.Id, first.ActiveDocumentInfo.Id);
            Equal(true, first.UpdateDocument(virtualDocument.Id, "int value << 4;"));
            Equal(false, first.TryCloseDocument(virtualDocument.Id));
            Equal(true, first.TryCloseDocument(virtualDocument.Id, true));
            Equal(diskDocument.Id, first.ActiveDocumentInfo.Id);
            Equal(true, first.TryCloseDocument(diskDocument.Id));
            Equal(0, first.OpenDocuments.Count);
            Equal(null, first.ActiveDocumentInfo);

            SequenceEqual(new[]
            {
                "added:" + virtualDocument.Id,
                "active:" + virtualDocument.Id,
                "changed:2:True",
                "changed:2:False",
                "added:" + diskDocument.Id,
                "active:" + diskDocument.Id,
                "active:" + virtualDocument.Id,
                "changed:3:True",
                "closed:" + virtualDocument.Id,
                "active:" + diskDocument.Id,
                "closed:" + diskDocument.Id,
                "active:none"
            }, events);
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

        private static void EnforcesDdf012CompilationUnitSyntax()
        {
            DdfLanguageDefinition language = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package.Language;
            const string canonical =
                "@@'Console'\n" +
                "int shared << 1;\n" +
                "main() out int { if(shared > 0) { ret shared; } else if(shared == 0) { ret 0; } else { ret -1; } }";
            DdfParseResult valid = DdfParser.Parse(canonical, language);
            Equal(0, valid.ValidationDiagnostics.Count);

            const string invalidTopLevel = "1; main() out int { ret 0; }";
            DdfParseResult topLevelExpression = DdfParser.Parse(invalidTopLevel, language);
            Equal(0, topLevelExpression.Diagnostics.Count);
            Equal(true, topLevelExpression.GrammarDiagnostics.Any(item => item.Code == "DDFG101"));
            Equal(true, topLevelExpression.ValidationDiagnostics.Any(item => item.Code == "DDFG101"));
            Equal(true, DdfIrLowerer.Lower(invalidTopLevel, topLevelExpression, language)
                .Diagnostics.Any(item => item.Code == "DDFG101"));

            DdfParseResult unbracedBody = DdfParser.Parse(
                "main() out int { if(true) ret 1; ret 0; }", language);
            Equal(true, unbracedBody.ValidationDiagnostics.Any(item => item.Code == "DDFG101"));
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

        private static void LoadsEmbeddedDdfLanguagePackage()
        {
            DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.LoadEmbeddedDefault();
            Equal(true, result.Success);
            Equal(false, result.HasErrors);
            Equal(DdfLanguagePackageOrigin.Embedded, result.Package.Origin);
            Equal("ddf", result.Package.Id);
            Equal("0.12.3", result.Package.PackageVersion);
            Equal("0.12", result.Package.LanguageVersion);
            Equal(16, result.Package.Language.Keywords.Count);
            Equal(19, result.Package.Language.Operators.Count);
            Equal(0, result.Package.NativeExtensions.Count);
            Equal(true, result.Package.Grammar != null);
            Equal("compilation-unit", result.Package.Grammar.StartRule);

            const string source = "main() out int { int value << 1; ret value; }";
            SequenceEqual(
                DdfLexer.Lex(source).Tokens.Select(token => token.Kind),
                DdfLexer.Lex(source, result.Package.Language).Tokens.Select(token => token.Kind));
            Equal(DdfParser.Parse(source).Diagnostics.Count,
                DdfParser.Parse(source, result.Package.Language).Diagnostics.Count);
            Equal(DdfFormatter.Format(source).Text,
                DdfFormatter.Format(source, result.Package.Language).Text);
        }

        private static void ParsesCanonicalEbnfGrammar()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string source = File.ReadAllText(Path.Combine(projectRoot, "samples", "parser", "valid.ddf"));
            DdfParseResult result = DdfParser.Parse(source, package.Language);
            if (result.GrammarDiagnostics.Count > 0)
            {
                string functionOnly = source.Substring(source.IndexOf("main()", StringComparison.Ordinal));
                DdfParseResult isolated = DdfParser.Parse(functionOnly, package.Language);
                throw new InvalidOperationException(string.Join(" | ", result.GrammarDiagnostics.Select(item => item.ToString())) +
                    " ISOLATED: " + string.Join(" | ", isolated.GrammarDiagnostics.Select(item => item.ToString())));
            }
            Equal(0, result.GrammarDiagnostics.Count);
            Equal("CompilationUnit", result.GenericRoot.Kind);
            Equal(true, result.GenericRoot.Fields["members"].Count > 0);

            DdfParseResult invalid = DdfParser.Parse("main() out int { ret 1 }", package.Language);
            Equal(true, invalid.GrammarDiagnostics.Any(item => item.Code == "DDFG101"));
            Equal(true, invalid.GenericRoot.Children.Any(item => item.IsMissing));
        }

        private static void DrivesStructuralIdeServicesFromGrammarMetadata()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            const string source = "@@'Console'\nmain(int input) out int\n{\n if(input > 0)\n {\n  int value;\n  ret value;\n }\n ret input;\n}";
            DdfParseResult parse = DdfParser.Parse(source, package.Language);
            Equal(0, parse.GrammarDiagnostics.Count);

            DdfSymbolIndex symbols = DdfSymbolIndex.Create(parse, source);
            SequenceEqual(new[] { "Console", "main" }, symbols.Symbols.Select(item => item.Name));
            SequenceEqual(new[] { "input", "value" }, symbols.Symbols[1].Children.Select(item => item.Name));
            Equal("(int) → int", symbols.Symbols[1].Detail);
            Equal("int", symbols.Symbols[1].Children[0].Detail);

            IReadOnlyList<DdfFoldingRange> folding = DdfFoldingRangeProvider.Create(parse, source);
            Equal(2, folding.Count);
            SequenceEqual(new[] { DdfFoldingKind.Function, DdfFoldingKind.Block }, folding.Select(item => item.Kind));

            IReadOnlyList<DdfBreadcrumbItem> breadcrumb = DdfBreadcrumbService.GetPath(
                parse, source, source.IndexOf("value;", StringComparison.Ordinal));
            SequenceEqual(new[] { "main()", "if" }, breadcrumb.Select(item => item.Label));

            string root = Path.Combine(Path.GetTempPath(), "ddf-ide-metadata-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string grammarPath = Path.Combine(root, "grammar.json");
                JsonObject grammar = JsonNode.Parse(File.ReadAllText(grammarPath, Encoding.UTF8)).AsObject();
                JsonObject ifNode = grammar["nodes"].AsArray().Select(item => item.AsObject())
                    .Single(item => item["rule"].GetValue<string>() == "if-statement");
                ifNode["ide"].AsObject()["breadcrumbLabel"] = "decisione";
                File.WriteAllText(grammarPath,
                    grammar.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult custom = DdfLanguagePackageLoader.Load(root);
                Equal(true, custom.Success);
                DdfParseResult customParse = DdfParser.Parse(source, custom.Package.Language);
                IReadOnlyList<DdfBreadcrumbItem> customPath = DdfBreadcrumbService.GetPath(
                    customParse, source, source.IndexOf("value;", StringComparison.Ordinal));
                SequenceEqual(new[] { "main()", "decisione" }, customPath.Select(item => item.Label));

                ifNode["ide"].AsObject()["breadcrumbSelectionField"] = "campo-inesistente";
                File.WriteAllText(grammarPath,
                    grammar.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                DdfLanguagePackageLoadResult invalid = DdfLanguagePackageLoader.Load(root);
                Equal(false, invalid.Success);
                Equal(true, invalid.Diagnostics.Any(item => item.Code == "DDFL064"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void DrivesIntellisenseFromPackageSection()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-intellisense-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string path = Path.Combine(root, "intellisense.json");
                JsonObject document = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)).AsObject();
                document["completion"].AsObject()["controlFlowDetail"] = "costrutto configurato";
                document["completion"].AsObject()["snippetOrigin"] = "pacchetto utente";
                document["signatureHelp"].AsObject()["currentDocumentOrigin"] = "sorgente configurata";
                JsonObject functionPresentation = document["symbolKinds"].AsArray()
                    .Select(item => item.AsObject())
                    .Single(item => item["kind"].GetValue<string>() == "Function");
                functionPresentation["label"] = "routine";
                functionPresentation["documentation"] = "Documentazione definita dal pacchetto.";
                document["snippets"].AsArray().Add(new JsonObject
                {
                    ["prefix"] = "choose",
                    ["displayText"] = "choose — personalizzato",
                    ["description"] = "Snippet aggiunto senza ricompilazione",
                    ["body"] = "if(${1:true})\n{\n    ${2:ret 0};\n}${0}",
                    ["requiresKeyword"] = false
                });
                File.WriteAllText(path,
                    document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(root);
                if (!load.Success)
                    throw new InvalidOperationException(string.Join(" | ", load.Diagnostics.Select(item => item.ToString())));
                DdfLanguageDefinition language = load.Package.Language;

                DdfCompletionItem keyword = DdfCompletionService.GetCompletions("i", 1, false, language)
                    .Items.Single(item => item.DisplayText == "if");
                Equal("costrutto configurato", keyword.Detail);
                const string snippetSource = "main() out int { choose";
                DdfCompletionItem snippet = DdfCompletionService.GetCompletions(
                    snippetSource, snippetSource.Length, false, language).Items
                    .Single(item => item.DisplayText == "choose — personalizzato");
                Equal("pacchetto utente", snippet.Origin);

                const string source = "sum(int value) out int { ret value; }\nmain() out int { ret sum(1); }";
                DdfParseResult parse = DdfParser.Parse(source, language);
                DdfDocumentSymbol function = DdfSymbolIndex.Create(parse, source).Symbols.First();
                DdfHoverInfo hover = DdfHoverService.CreateForSymbol(function, null, source,
                    "test", null, language);
                Equal("routine", hover.Kind);
                Equal("Documentazione definita dal pacchetto.", hover.Documentation);
                int callPosition = source.LastIndexOf("sum(1", StringComparison.Ordinal) + 5;
                DdfSignatureHelpResult signature = DdfSignatureHelpService.GetSignatureHelp(
                    source, callPosition, null, language);
                Equal("sorgente configurata", signature.Signature.Origin);
                Equal("sum(int value) out int", signature.Signature.Signature);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void DrivesFormattingFromPackageSection()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-formatting-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string path = Path.Combine(root, "formatting.json");
                JsonObject document = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)).AsObject();
                document["blankLineBetweenStatements"] = false;
                document["indentAfter"].AsArray().Add(":");
                document["autoPairs"].AsArray().Add(new JsonObject { ["open"] = "<", ["close"] = ">" });
                JsonObject stringFix = document["quickFixes"].AsArray().Select(item => item.AsObject())
                    .Single(item => item["diagnosticCode"].GetValue<string>() == "DDF001");
                stringFix["title"] = "Completa stringa dal pacchetto";
                File.WriteAllText(path,
                    document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(root);
                Equal(true, load.Success);
                DdfLanguageDefinition language = load.Package.Language;
                Equal("main() out int\n{\n    int a;\n    int b;\n    ret 0;\n}",
                    DdfFormatter.Format("main()out int{int a;int b;ret 0;}", language).Text);

                EditorEdit indentation = EditorEditing.CreateNewLineEdit("label:", 6, 0, 4, false,
                    language.Formatting);
                Equal("\n    ", indentation.Replacement);
                EditorEdit pair = EditorEditing.CreatePairedCharacterEdit(string.Empty, 0, 0, '<',
                    language.Formatting);
                Equal("<>", pair.Replacement);

                DdfDiagnostic diagnostic = DdfLexer.Lex("\"open", language).Diagnostics.Single();
                DdfQuickFix fix = DdfQuickFixService.CreateDefault(language.Formatting)
                    .GetFixes("\"open", diagnostic).Single();
                Equal("Completa stringa dal pacchetto", fix.Title);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void DrivesTypeCheckingFromPackageSection()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-types-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string path = Path.Combine(root, "types.json");
                JsonObject document = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)).AsObject();
                document["conversions"].AsArray().Add(new JsonObject
                {
                    ["from"] = "float",
                    ["to"] = "int",
                    ["context"] = "General"
                });
                File.WriteAllText(path,
                    document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(root);
                if (!load.Success)
                    throw new InvalidOperationException(string.Join(" | ", load.Diagnostics.Select(item => item.ToString())));
                DdfTypeSystemDefinition types = load.Package.Language.Types;
                Equal(6, types.Primitives.Count);
                Equal(false, types.CanDeclareVariable("void"));
                Equal("char", types.GetIndexedElementType("string"));
                Equal(false, types.Scopes.AllowShadowing);
                Equal(true, types.Mutability.VariablesMutable);
                Equal(true, types.Signatures.ExactArity);
                Equal("dotnet-roslyn-v1", types.ClrMapping.Backend);
                Equal("1.0", types.ClrMapping.MappingVersion);
                Equal("SZArray", types.ClrMapping.ArrayRepresentation);
                Equal("SealedClass", types.ClrMapping.StructureRepresentation);
                Equal(true, types.ClrMapping.TryGetPrimitive("float",
                    out DdfClrPrimitiveTypeMapping floatMapping));
                Equal("System.Double", floatMapping.ClrTypeName);
                Equal("double", floatMapping.CSharpTypeName);

                const string source = "main() out int { int value << 1.5; ret value; }";
                DdfParseResult parse = DdfParser.Parse(source, load.Package.Language);
                Equal(true, DdfTypeChecker.Check(source, parse.Root, null,
                    DdfTypeSystemDefinition.CreateDdfCompatibleDefault()).Diagnostics.Any(item => item.Code == "DDF301"));
                Equal(0, DdfTypeChecker.Check(source, parse.Root, null, types).Diagnostics.Count);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RejectsInvalidTypeSystemsAtomically()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-invalid-types-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string path = Path.Combine(root, "types.json");
                JsonObject document = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)).AsObject();
                document["numeric"].AsObject()["dominantType"] = "string";
                File.WriteAllText(path,
                    document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.Load(root);
                Equal(false, result.Success);
                Equal(true, result.Package == null);
                Equal(true, result.Diagnostics.Any(item => item.Code == "DDFL095" && item.File == "types.json"));

                string incompatibleRoot = Path.Combine(root, "incompatible");
                DdfLanguagePackageLoader.ExportEmbeddedDefault(incompatibleRoot);
                string incompatiblePath = Path.Combine(incompatibleRoot, "types.json");
                JsonObject incompatible = JsonNode.Parse(File.ReadAllText(
                    incompatiblePath, Encoding.UTF8)).AsObject();
                JsonObject floatMapping = incompatible["clrMapping"].AsObject()["primitives"]
                    .AsArray().Select(item => item.AsObject())
                    .Single(item => item["type"].GetValue<string>() == "float");
                floatMapping["clrType"] = "System.Single";
                floatMapping["csharpType"] = "float";
                File.WriteAllText(incompatiblePath, incompatible.ToJsonString(
                    new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult incompatibleResult =
                    DdfLanguagePackageLoader.Load(incompatibleRoot);
                Equal(false, incompatibleResult.Success);
                Equal(true, incompatibleResult.Diagnostics.Any(item =>
                    item.Code == "DDFL096" && item.File == "types.json" &&
                    item.Domain == DdfDiagnosticDomain.EngineCapability));

                string missingRoot = Path.Combine(root, "missing");
                DdfLanguagePackageLoader.ExportEmbeddedDefault(missingRoot);
                string missingPath = Path.Combine(missingRoot, "types.json");
                JsonObject missing = JsonNode.Parse(File.ReadAllText(
                    missingPath, Encoding.UTF8)).AsObject();
                missing.Remove("clrMapping");
                File.WriteAllText(missingPath, missing.ToJsonString(
                    new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult missingResult =
                    DdfLanguagePackageLoader.Load(missingRoot);
                Equal(false, missingResult.Success);
                Equal(true, missingResult.Diagnostics.Any(item =>
                    item.Code == "DDFL092" && item.File == "types.json"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void PlansSafeSemanticOperationsFromPackage()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            Equal(DdfSafeSemanticOperationCatalog.OperationSet, package.Language.Semantics.OperationSet);
            Equal("1.0", package.Language.Semantics.OperationSetVersion);
            Equal(14, package.Language.Semantics.Bindings.Count);

            const string source = "main() out int { int value << 1; if(value > 0) { ret value; } ret 0; }";
            DdfParseResult parse = DdfParser.Parse(source, package.Language);
            IReadOnlyList<DdfPlannedSemanticOperation> plan = DdfSemanticOperationPlanner.Create(
                parse.GenericRoot, package.Language.Semantics);
            DdfPlannedSemanticOperation function = plan.Single(item => item.Id == "DeclareFunction");
            Equal("function-declaration", function.Rule);
            DdfGenericSyntaxNode name = function.Arguments["name"].Single();
            DdfGenericSyntaxNode returnType = function.Arguments["returnType"].Single();
            Equal("main", source.Substring(name.Start, name.Length));
            Equal("int", source.Substring(returnType.Start, returnType.Length));
            Equal(true, plan.Any(item => item.Id == "DeclareVariable"));
            Equal(true, plan.Any(item => item.Id == "Branch"));
            Equal(true, plan.Count(item => item.Id == "Return") == 2);
            Equal(false, typeof(DdfSemanticOperationDefinition).GetProperties()
                .Any(property => typeof(Delegate).IsAssignableFrom(property.PropertyType)));

            string root = Path.Combine(Path.GetTempPath(), "ddf-custom-semantics-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string path = Path.Combine(root, "semantics.json");
                JsonObject document = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)).AsObject();
                JsonObject statementBinding = document["bindings"].AsArray().Select(item => item.AsObject())
                    .Single(item => item["rule"].GetValue<string>() == "expression-statement");
                statementBinding["operations"].AsArray()[0].AsObject()["id"] = "Return";
                File.WriteAllText(path,
                    document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult custom = DdfLanguagePackageLoader.Load(root);
                Equal(true, custom.Success);
                DdfParseResult customParse = DdfParser.Parse("main() out int { 1; ret 0; }", custom.Package.Language);
                IReadOnlyList<DdfPlannedSemanticOperation> customPlan = DdfSemanticOperationPlanner.Create(
                    customParse.GenericRoot, custom.Package.Language.Semantics);
                Equal(true, customPlan.Any(item => item.Rule == "expression-statement" && item.Id == "Return"));
                Equal(false, customPlan.Any(item => item.Id == "EvaluateStatement"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RejectsUnsafeSemanticOperationsAtomically()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-unsafe-semantics-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string path = Path.Combine(root, "semantics.json");
                JsonObject document = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)).AsObject();
                JsonObject operation = document["bindings"].AsArray()[0].AsObject()["operations"]
                    .AsArray()[0].AsObject();
                operation["id"] = "ExecuteDotNet";
                operation["arguments"] = new JsonObject();
                File.WriteAllText(path,
                    document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.Load(root);
                Equal(false, result.Success);
                Equal(true, result.Package == null);
                Equal(true, result.Diagnostics.Any(item => item.Code == "DDFL106" &&
                    item.Domain == DdfDiagnosticDomain.EngineCapability &&
                    item.Message.Contains("Unsupported safe semantic operation", StringComparison.Ordinal)));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void SeparatesDiagnosticDomains()
        {
            var source = new DdfDiagnostic("DDF100", "Errore sintattico.", 0, 1, 1, 1);
            var runtime = new DdfDiagnostic("DDF404", "Divisione per zero.", 0, 1, 1, 1,
                domain: DdfDiagnosticDomain.Runtime);
            var language = new DdfLanguagePackageDiagnostic("DDFL040", "Voce duplicata.");
            var capability = new DdfLanguagePackageDiagnostic("DDFL021", "API motore non supportata.",
                domain: DdfDiagnosticDomain.EngineCapability);

            Equal(DdfDiagnosticDomain.Source, source.Domain);
            Equal(DdfDiagnosticDomain.Runtime, runtime.Domain);
            Equal(DdfDiagnosticDomain.LanguageDefinition, language.Domain);
            Equal(DdfDiagnosticDomain.EngineCapability, capability.Domain);
            Equal(true, source.ToString().Contains("[Codice sorgente]", StringComparison.Ordinal));
            Equal(true, runtime.ToHoverText().Contains("[Runtime]", StringComparison.Ordinal));
            Equal(true, language.ToString().Contains("[Definizione linguaggio]", StringComparison.Ordinal));
            Equal(true, capability.ToString().Contains("[Funzionalità motore]", StringComparison.Ordinal));
        }

        private static void RequiresExplicitNativeExtensionAuthorization()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-native-extension-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string manifestPath = Path.Combine(root, "manifest.json");
                JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath, Encoding.UTF8)).AsObject();
                manifest["nativeExtensions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "ddf.console-plus",
                        ["publisher"] = "ddf.technology",
                        ["version"] = "1.2.0",
                        ["sha256"] = new string('a', 64),
                        ["capabilities"] = new JsonArray("runtime:function", "runtime:output")
                    }
                };
                File.WriteAllText(manifestPath,
                    manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(root);
                Equal(true, load.Success);
                DdfNativeExtensionRequirement requirement = load.Package.NativeExtensions.Single();
                Equal("ddf.console-plus", requirement.Id);
                Equal("ddf.technology", requirement.Publisher);
                Equal("1.2.0", requirement.Version);
                Equal(new string('a', 64), requirement.Sha256);

                DdfNativeExtensionAuthorizationResult result =
                    DdfNativeExtensionAuthorizationPolicy.Evaluate(load.Package.NativeExtensions, null).Single();
                Equal(DdfNativeExtensionAuthorizationStatus.AuthorizationRequired, result.Status);
                Equal(2, result.MissingCapabilities.Count);

                var mismatched = new DdfNativeExtensionAuthorization(requirement.Id, requirement.Publisher,
                    requirement.Version, new string('b', 64), requirement.Capabilities);
                result = DdfNativeExtensionAuthorizationPolicy.Evaluate(load.Package.NativeExtensions,
                    new[] { mismatched }).Single();
                Equal(DdfNativeExtensionAuthorizationStatus.IdentityMismatch, result.Status);

                var partial = new DdfNativeExtensionAuthorization(requirement.Id, requirement.Publisher,
                    requirement.Version, requirement.Sha256, new[] { "runtime:function" });
                result = DdfNativeExtensionAuthorizationPolicy.Evaluate(load.Package.NativeExtensions,
                    new[] { partial }).Single();
                Equal(DdfNativeExtensionAuthorizationStatus.CapabilityNotAuthorized, result.Status);
                SequenceEqual(new[] { "runtime:output" }, result.MissingCapabilities);

                var complete = new DdfNativeExtensionAuthorization(requirement.Id, requirement.Publisher,
                    requirement.Version, requirement.Sha256, requirement.Capabilities);
                result = DdfNativeExtensionAuthorizationPolicy.Evaluate(load.Package.NativeExtensions,
                    new[] { complete }).Single();
                Equal(DdfNativeExtensionAuthorizationStatus.Authorized, result.Status);
                Equal(true, result.IsAuthorized);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RejectsInvalidNativeExtensionIdentitiesAtomically()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-invalid-native-extension-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string manifestPath = Path.Combine(root, "manifest.json");
                JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath, Encoding.UTF8)).AsObject();
                manifest["nativeExtensions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "ddf.console-plus",
                        ["publisher"] = "ddf.technology",
                        ["version"] = "1.0",
                        ["sha256"] = "not-a-hash",
                        ["capabilities"] = new JsonArray("runtime:function", "runtime:function")
                    },
                    new JsonObject
                    {
                        ["id"] = "ddf.console-plus",
                        ["publisher"] = "ddf.technology",
                        ["version"] = "1.0",
                        ["sha256"] = new string('b', 64),
                        ["capabilities"] = new JsonArray("runtime:output")
                    }
                };
                File.WriteAllText(manifestPath,
                    manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(root);
                Equal(false, load.Success);
                Equal(true, load.Package == null);
                Equal(true, load.Diagnostics.Any(item => item.Code == "DDFL110" &&
                    item.JsonPath == "$.nativeExtensions[1].id"));
                Equal(true, load.Diagnostics.Any(item => item.Code == "DDFL113"));
                Equal(true, load.Diagnostics.Any(item => item.Code == "DDFL114"));
                Equal(true, load.Diagnostics.Where(item => item.Code.StartsWith("DDFL11", StringComparison.Ordinal))
                    .All(item => item.Domain == DdfDiagnosticDomain.LanguageDefinition));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void LowersDdfToCommonIntermediateRepresentation()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            const string source = "struct Box { int value; }\n" +
                "increment(int value) out int { ret value + 1; }\n" +
                "main() out int { int current << 0; while(current < 2) { current++; } ret increment(current); }";
            DdfParseResult parse = DdfParser.Parse(source, package.Language);
            DdfIrBuildResult result = DdfIrLowerer.Lower(source, parse, package.Language);

            Equal(true, result.Success);
            Equal("1.0", DdfIrProgram.FormatVersion);
            Equal(true, result.Program.SemanticOperations.Count > 0);
            Equal(1, result.Program.Members.OfType<DdfIrStructure>().Count());
            Equal(2, result.Program.Members.OfType<DdfIrFunction>().Count());
            DdfIrFunction main = result.Program.Members.OfType<DdfIrFunction>()
                .Single(item => item.Name == "main");
            Equal(source.IndexOf("main", StringComparison.Ordinal), main.NameStart);
            Equal(true, main.Body.Statements.Any(item => item is DdfIrWhile));
            Equal(true, main.Body.Statements.Any(item => item is DdfIrReturn));
        }

        private static void BuildsSourceLinkedFlowchart()
        {
            const string source =
                "helper(int value) out int { ret value + 1; }\n" +
                "main() out int\n{\n" +
                "    int value << 0;\n" +
                "    while(value < 3)\n    {\n" +
                "        if(value == 1) { value << helper(value); }\n" +
                "        else { value++; }\n" +
                "        if(value > 2) { brk; }\n" +
                "    }\n" +
                "    ret value;\n" +
                "}";
            DdfFlowchartBuildResult result = DdfFlowchartBuilder.Build(source, "main", documentPath: "main.ddf");

            if (!result.Success)
                throw new InvalidOperationException(string.Join(" | ", result.Diagnostics.Select(item => item.Code + ": " + item.Message)));
            Equal(true, result.Success);
            Equal(0, result.Diagnostics.Count);
            Equal("1.0", DdfFlowchart.FormatVersion);
            Equal("main", result.Flowchart.FunctionName);
            Equal("main.ddf", result.Flowchart.DocumentPath);
            Equal(DdfFlowchartNodeKind.Entry, result.Flowchart.FindNode(result.Flowchart.EntryNodeId).Kind);
            Equal(DdfFlowchartNodeKind.Exit, result.Flowchart.FindNode(result.Flowchart.ExitNodeId).Kind);
            Equal(1, result.Flowchart.Nodes.Count(node => node.Kind == DdfFlowchartNodeKind.LoopDecision));
            Equal(2, result.Flowchart.Nodes.Count(node => node.Kind == DdfFlowchartNodeKind.Decision));
            DdfFlowchartNode call = result.Flowchart.Nodes.Single(node => node.Kind == DdfFlowchartNodeKind.Call);
            SequenceEqual(new[] { "helper" }, call.CalledFunctions);
            Equal(true, call.Label.Contains("helper(value)"));
            Equal(true, result.Flowchart.Edges.Any(edge => edge.Kind == DdfFlowchartEdgeKind.True));
            Equal(true, result.Flowchart.Edges.Any(edge => edge.Kind == DdfFlowchartEdgeKind.False));
            Equal(true, result.Flowchart.Edges.Any(edge => edge.IsBackEdge));
            Equal(true, result.Flowchart.Edges.Any(edge => edge.Kind == DdfFlowchartEdgeKind.Break));
            Equal(true, result.Flowchart.Edges.Any(edge => edge.Kind == DdfFlowchartEdgeKind.Return));
            Equal(true, result.Flowchart.Nodes.All(node => node.Start >= 0 && node.Start + node.Length <= source.Length));

            DdfIrProgram program = DdfIrLowerer.Lower(source, DdfParser.Parse(source)).Program;
            DdfFlowchartBuildResult moduleResult = DdfFlowchartBuilder.Build(
                new DdfIrModule("workspace/main.ddf", source, program), "main");
            Equal(true, moduleResult.Success);
            Equal("workspace/main.ddf", moduleResult.Flowchart.DocumentPath);

            DdfFlowchartBuildResult missing = DdfFlowchartBuilder.Build(source, "missing");
            Equal(false, missing.Success);
            Equal("DDFC001", missing.Diagnostics.Single().Code);
        }

        private static void ModelsAllFlowchartLoopForms()
        {
            const string source =
                "main() out int\n{\n" +
                "    int value << 0;\n" +
                "    while(value < 1) { value++; }\n" +
                "    do { value++; } while(value < 2);\n" +
                "    for(int index << 0; index < 2; index++) { value << value + index; }\n" +
                "    ret value;\n" +
                "}";
            DdfFlowchartBuildResult result = DdfFlowchartBuilder.Build(source, "main");

            Equal(true, result.Success);
            Equal(3, result.Flowchart.Nodes.Count(node => node.Kind == DdfFlowchartNodeKind.LoopDecision));
            Equal(true, result.Flowchart.Edges.Count(edge => edge.IsBackEdge) >= 3);
            Equal(true, result.Flowchart.Nodes.Any(node => node.Kind == DdfFlowchartNodeKind.Process && node.Label.Contains("index++")));
        }

        private static void ValidatesComplexFlowchartRegressionMatrix()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            string path = Path.Combine(projectRoot, "samples", "flowchart-complex-smoke-test.ddf");
            string source = File.ReadAllText(path);
            string[] functions = { "adjust", "classify", "main" };

            foreach (string functionName in functions)
            {
                DdfFlowchartBuildResult result = DdfFlowchartBuilder.Build(source, functionName, documentPath: path);
                Equal(true, result.Success);
                Equal(0, result.Diagnostics.Count);
                DdfFlowchart graph = result.Flowchart;
                var nodeIds = new HashSet<string>(graph.Nodes.Select(node => node.Id), StringComparer.Ordinal);
                Equal(graph.Nodes.Count, nodeIds.Count);
                Equal(true, nodeIds.Contains(graph.EntryNodeId));
                Equal(true, nodeIds.Contains(graph.ExitNodeId));
                Equal(true, graph.Edges.All(edge => nodeIds.Contains(edge.From) && nodeIds.Contains(edge.To)));
                Equal(true, graph.Nodes.All(node => node.Start >= 0 && node.Length >= 0 &&
                    node.Start + node.Length <= source.Length));

                var reachable = new HashSet<string>(StringComparer.Ordinal) { graph.EntryNodeId };
                var pending = new Queue<string>();
                pending.Enqueue(graph.EntryNodeId);
                while (pending.Count > 0)
                {
                    string current = pending.Dequeue();
                    foreach (string target in graph.Edges.Where(edge => edge.From == current)
                        .Select(edge => edge.To))
                        if (reachable.Add(target)) pending.Enqueue(target);
                }
                Equal(graph.Nodes.Count, reachable.Count);

                foreach (DdfFlowchartNode decision in graph.Nodes.Where(node =>
                    node.Kind == DdfFlowchartNodeKind.Decision ||
                    node.Kind == DdfFlowchartNodeKind.LoopDecision))
                {
                    IReadOnlyList<DdfFlowchartEdge> outgoing = graph.Edges
                        .Where(edge => edge.From == decision.Id).ToList();
                    Equal(2, outgoing.Count);
                    Equal(true, outgoing.Any(edge => edge.Kind == DdfFlowchartEdgeKind.True));
                    Equal(true, outgoing.Any(edge => edge.Kind == DdfFlowchartEdgeKind.False));
                }
                foreach (DdfFlowchartNode terminal in graph.Nodes.Where(node =>
                    node.Kind == DdfFlowchartNodeKind.Return ||
                    node.Kind == DdfFlowchartNodeKind.Terminate))
                {
                    DdfFlowchartEdge outgoing = graph.Edges.Single(edge => edge.From == terminal.Id);
                    Equal(graph.ExitNodeId, outgoing.To);
                    Equal(terminal.Kind == DdfFlowchartNodeKind.Return
                        ? DdfFlowchartEdgeKind.Return : DdfFlowchartEdgeKind.Terminate, outgoing.Kind);
                }

                DdfFlowchartLayout layout = DdfFlowchartLayouter.Layout(graph);
                Equal(graph.Nodes.Count, layout.Nodes.Count);
                for (int left = 0; left < layout.Nodes.Count; left++)
                for (int right = left + 1; right < layout.Nodes.Count; right++)
                {
                    DdfFlowchartLayoutNode first = layout.Nodes[left];
                    DdfFlowchartLayoutNode second = layout.Nodes[right];
                    bool overlaps = first.X < second.X + second.Width &&
                        first.X + first.Width > second.X && first.Y < second.Y + second.Height &&
                        first.Y + first.Height > second.Y;
                    Equal(false, overlaps);
                }
                Equal(true, graph.Edges.Where(edge => !edge.IsBackEdge).All(edge =>
                    layout.FindNode(edge.To).Rank > layout.FindNode(edge.From).Rank));
                Equal(true, graph.Edges.Where(edge => edge.IsBackEdge).All(edge =>
                    layout.FindNode(edge.To).Rank <= layout.FindNode(edge.From).Rank));
            }

            DdfFlowchart main = DdfFlowchartBuilder.Build(source, "main", documentPath: path).Flowchart;
            Equal(3, main.Nodes.Count(node => node.Kind == DdfFlowchartNodeKind.LoopDecision));
            Equal(true, main.Nodes.Count(node => node.Kind == DdfFlowchartNodeKind.Decision) >= 8);
            Equal(2, main.Nodes.Count(node => node.Kind == DdfFlowchartNodeKind.Break));
            Equal(1, main.Nodes.Count(node => node.Kind == DdfFlowchartNodeKind.Terminate));
            Equal(true, main.Nodes.Count(node => node.Kind == DdfFlowchartNodeKind.Return) >= 4);
            Equal(true, new HashSet<string>(main.Nodes.SelectMany(node => node.CalledFunctions),
                StringComparer.Ordinal).SetEquals(new[] { "adjust", "classify" }));
        }

        private static void CollapsesLinearFlowchartSequences()
        {
            const string source =
                "helper(int value) out int { ret value + 1; } " +
                "main() out int { int first << 1; int second << helper(first); " +
                "second++; second << second + 2; ret second; }";
            DdfFlowchart original = DdfFlowchartBuilder.Build(source, "main", documentPath: "main.ddf").Flowchart;
            DdfFlowchart collapsed = DdfFlowchartProjection.CollapseLinearSequences(original, 3);
            DdfFlowchartNode group = collapsed.Nodes.Single(node => node.IsCollapsedGroup);

            Equal(4, group.GroupedNodeIds.Count);
            Equal(true, group.CalledFunctions.Contains("helper"));
            Equal(true, group.Start >= 0 && group.Start + group.Length <= source.Length);
            Equal(original.EntryNodeId, collapsed.EntryNodeId);
            Equal(original.ExitNodeId, collapsed.ExitNodeId);
            Equal(true, collapsed.Edges.Any(edge => edge.From == original.EntryNodeId && edge.To == group.Id));
            Equal(true, collapsed.Edges.Any(edge => edge.From == group.Id));
            Equal(false, collapsed.Edges.Any(edge => edge.From == edge.To));

            DdfFlowchart expanded = DdfFlowchartProjection.CollapseLinearSequences(original, 3,
                new[] { group.Id });
            Equal(original.Nodes.Count, expanded.Nodes.Count);
            Equal(false, expanded.Nodes.Any(node => node.IsCollapsedGroup));
        }

        private static void KeepsNewestFlowchartSnapshot()
        {
            const string firstSource = "main() out int { ret 1; }";
            const string latestSource = "main() out int { int value << 2; ret value; }";
            using (var service = new DdfFlowchartSnapshotService())
            {
                Task<DdfFlowchartSnapshot> obsolete = service.RequestAsync(firstSource, "main", 10,
                    documentPath: "main.ddf", debounceDelay: TimeSpan.FromMilliseconds(200));
                Task<DdfFlowchartSnapshot> latest = service.RequestAsync(latestSource, "main", 11,
                    documentPath: "main.ddf", debounceDelay: TimeSpan.Zero);

                DdfFlowchartSnapshot snapshot = latest.GetAwaiter().GetResult();
                Equal(true, snapshot.Success);
                Equal(11L, snapshot.DocumentVersion);
                Equal(latestSource, snapshot.Source);
                Equal("main.ddf", snapshot.DocumentPath);
                Equal(true, service.IsCurrent(snapshot));
                Equal(snapshot.RequestId, service.LatestRequestId);
                Equal(true, snapshot.Flowchart.Nodes.Any(node => node.Label.Contains("value << 2")));

                bool canceled = false;
                try { obsolete.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { canceled = true; }
                Equal(true, canceled);

                using (var externalCancellation = new CancellationTokenSource())
                {
                    Task<DdfFlowchartSnapshot> externallyCanceled = service.RequestAsync(
                        latestSource, "main", 12, debounceDelay: TimeSpan.FromSeconds(1),
                        cancellationToken: externalCancellation.Token);
                    externalCancellation.Cancel();
                    canceled = false;
                    try { externallyCanceled.GetAwaiter().GetResult(); }
                    catch (OperationCanceledException) { canceled = true; }
                    Equal(true, canceled);
                }
            }
        }

        private static void MarksRecoverableFlowchartSnapshotsAsPartial()
        {
            using (var service = new DdfFlowchartSnapshotService())
            {
                string[] incompleteSources =
                {
                    "main() out int { int value << ; value++; ret value; }",
                    "main() out int { int value << 0; if(value < ) { value++; } ret value; }",
                    "main() out int { int value << helper(; ret value; }",
                    "main() out int { int value << 0; value++; ret value;"
                };
                for (int index = 0; index < incompleteSources.Length; index++)
                {
                    string incompleteSource = incompleteSources[index];
                    DdfParseResult parse = DdfParser.Parse(incompleteSource);
                    DdfIrBuildResult ir = DdfIrLowerer.Lower(incompleteSource, parse);
                    var diagnostics = parse.Diagnostics.Concat(ir.Diagnostics).ToList();
                    Equal(true, diagnostics.Any(item => item.Severity == DdfDiagnosticSeverity.Error));
                    DdfFlowchartSnapshot partial = service.RequestAsync(
                        new DdfIrModule("main.ddf", incompleteSource, ir.Program), "main", 20 + index,
                        diagnostics, TimeSpan.Zero).GetAwaiter().GetResult();
                    Equal(DdfFlowchartSnapshotState.Partial, partial.State);
                    Equal(false, partial.Success);
                    Equal(true, partial.Layout != null);
                    if (index == 0)
                        Equal(true, partial.Flowchart.Nodes.Any(node => node.Kind == DdfFlowchartNodeKind.Partial));
                }

                const string completeSource =
                    "main() out int { int value << 0; value++; ret value; }";
                DdfParseResult completeParse = DdfParser.Parse(completeSource);
                DdfIrBuildResult completeIr = DdfIrLowerer.Lower(completeSource, completeParse);
                DdfFlowchartSnapshot recovered = service.RequestAsync(
                    new DdfIrModule("main.ddf", completeSource, completeIr.Program), "main", 30,
                    completeParse.Diagnostics.Concat(completeIr.Diagnostics), TimeSpan.Zero)
                    .GetAwaiter().GetResult();
                Equal(DdfFlowchartSnapshotState.Current, recovered.State);
                Equal(true, recovered.Success);
                Equal(true, recovered.Layout != null);

                DdfFlowchartSnapshot unavailable = service.RequestAsync(
                    "int value << 0;", "main", 31, debounceDelay: TimeSpan.Zero)
                    .GetAwaiter().GetResult();
                Equal(DdfFlowchartSnapshotState.Unavailable, unavailable.State);
                Equal(true, unavailable.Layout == null);
            }
        }

        private static void BuildsCoherentEditorAnalysisSnapshots()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            const string source = "main() out int { int value << 1; value; missing; ret value; }";
            int completionPosition = source.IndexOf("value;", StringComparison.Ordinal) + 3;
            using (var service = new DdfEditorAnalysisService())
            {
                var request = new DdfEditorAnalysisRequest(
                    source, completionPosition, 42, package, 7, "document-1", "main.ddf");
                DdfEditorAnalysisSnapshot snapshot = service.Analyze(request);

                Equal(source, snapshot.Source);
                Equal(completionPosition, snapshot.Position);
                Equal(42L, snapshot.DocumentVersion);
                Equal(7L, snapshot.LanguageRevision);
                Equal("document-1", snapshot.DocumentId);
                Equal("main.ddf", snapshot.DocumentPath);
                Equal(true, snapshot.Symbols.Any(item => item.Name == "main"));
                Equal(true, snapshot.Symbols.SelectMany(item => item.Children)
                    .Any(item => item.Name == "value"));
                Equal(true, snapshot.Completions.Items.Any(item => item.DisplayText == "value"));
                Equal(true, snapshot.CompletionsComputed);
                Equal(true, snapshot.Diagnostics.Count > 0);
                Equal(snapshot.Diagnostics.Count, snapshot.Diagnostics
                    .GroupBy(item => new { item.Code, item.Message, item.Start, item.Length,
                        item.Severity, item.Domain, item.DocumentPath }).Count());
                Equal(true, service.IsCurrent(snapshot));

                var diagnosticsOnlyRequest = new DdfEditorAnalysisRequest(
                    source, completionPosition, 43, package, 7,
                    computeCompletions: false);
                DdfEditorAnalysisSnapshot diagnosticsOnly = service.Analyze(diagnosticsOnlyRequest);
                Equal(false, diagnosticsOnly.CompletionsComputed);
                Equal(0, diagnosticsOnly.Completions.Items.Count);
            }
        }

        private static void KeepsNewestEditorAnalysisSnapshot()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            const string firstSource = "main() out int { ret 1; }";
            const string latestSource = "main() out int { int result << 2; result; ret result; }";
            int completionPosition = latestSource.IndexOf("result;", StringComparison.Ordinal) + 3;
            using (var service = new DdfEditorAnalysisService())
            {
                Task<DdfEditorAnalysisSnapshot> obsolete = service.RequestAsync(
                    new DdfEditorAnalysisRequest(firstSource, firstSource.Length, 10, package, 3),
                    TimeSpan.FromMilliseconds(200));
                Task<DdfEditorAnalysisSnapshot> latest = service.RequestAsync(
                    new DdfEditorAnalysisRequest(latestSource, completionPosition, 11, package, 3),
                    TimeSpan.Zero);

                DdfEditorAnalysisSnapshot snapshot = latest.GetAwaiter().GetResult();
                Equal(11L, snapshot.DocumentVersion);
                Equal(latestSource, snapshot.Source);
                Equal(true, snapshot.Completions.Items.Any(item => item.DisplayText == "result"));
                Equal(true, service.IsCurrent(snapshot));
                Equal(snapshot.RequestId, service.LatestRequestId);

                bool canceled = false;
                try { obsolete.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { canceled = true; }
                Equal(true, canceled);
            }
        }

        private static void ResolvesEditorDefinitionsAcrossBuffers()
        {
            const string mainSource =
                "main() out int { int local << 1; local++; ret helper(local); }";
            const string helperSource =
                "helper(int value) out int { ret value + 1; }";
            var documents = new OpenDocumentCollection();
            IDdfEditorDocument main = documents.CreateVirtual("main.ddf", mainSource);
            IDdfEditorDocument helper = documents.CreateVirtual("helper.ddf", helperSource);
            documents.ActivateDocument(main.Id);
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            using (var service = new DdfEditorAnalysisService())
            {
                int localReference = mainSource.IndexOf("local++", StringComparison.Ordinal);
                DdfEditorAnalysisSnapshot localSnapshot = service.Analyze(
                    new DdfEditorAnalysisRequest(mainSource, localReference, main.Version,
                        package, 1, main.Id));
                DdfEditorNavigationTarget local = DdfEditorNavigationService.FindDefinition(
                    localSnapshot, documents.OpenDocuments);
                Equal(main.Id, local.DocumentId);
                Equal("local", local.Name);
                Equal(mainSource.IndexOf("local <<", StringComparison.Ordinal), local.Start);

                int helperReference = mainSource.LastIndexOf("helper", StringComparison.Ordinal);
                DdfEditorAnalysisSnapshot externalSnapshot = service.Analyze(
                    new DdfEditorAnalysisRequest(mainSource, helperReference, main.Version,
                        package, 1, main.Id));
                DdfEditorNavigationTarget external = DdfEditorNavigationService.FindDefinition(
                    externalSnapshot, documents.OpenDocuments);
                Equal(helper.Id, external.DocumentId);
                Equal("helper", external.Name);
                Equal(0, external.Start);

                DdfEditorAnalysisSnapshot emptySnapshot = service.Analyze(
                    new DdfEditorAnalysisRequest(mainSource, mainSource.IndexOf("ret ",
                        StringComparison.Ordinal), main.Version, package, 1, main.Id));
                Equal(null, DdfEditorNavigationService.FindDefinition(
                    emptySnapshot, documents.OpenDocuments));
            }
        }

        private static void LaysOutFlowchartsDeterministically()
        {
            const string source = "main() out int { int value << 0; if(value == 0) { value++; } else { value << 2; } ret value; }";
            DdfFlowchart graph = DdfFlowchartBuilder.Build(source, "main").Flowchart;
            DdfFlowchartLayout first = DdfFlowchartLayouter.Layout(graph);
            DdfFlowchartLayout second = DdfFlowchartLayouter.Layout(graph);

            Equal(graph.Nodes.Count, first.Nodes.Count);
            Equal(true, first.Width > 0 && first.Height > 0);
            foreach (DdfFlowchartLayoutNode node in first.Nodes)
            {
                DdfFlowchartLayoutNode repeated = second.FindNode(node.Node.Id);
                Equal(node.X, repeated.X);
                Equal(node.Y, repeated.Y);
                Equal(node.Rank, repeated.Rank);
            }
            Equal(true, first.Nodes.GroupBy(node => node.Rank).All(group =>
                group.OrderBy(node => node.X).Zip(group.OrderBy(node => node.X).Skip(1),
                    (left, right) => left.X + left.Width <= right.X).All(value => value)));
            Equal(0, first.FindNode(graph.EntryNodeId).Rank);
            Equal(true, first.FindNode(graph.ExitNodeId).Rank > 0);

            const string complexSource =
                "main() out float { float x << 45; float y << 15; float sumFunc << sum(x,y); " +
                "do { sumFunc++; } while(sumFunc < 100); " +
                "while(sumFunc > 0) { x << 0; } " +
                "for(int index; index < 100; index++) { y << 0; } " +
                "if(sumFunc > 50) { ret sumFunc; } else { ret 0; } }";
            DdfFlowchart complexGraph = DdfFlowchartBuilder.Build(complexSource, "main").Flowchart;
            DdfFlowchartLayout complex = DdfFlowchartLayouter.Layout(complexGraph);
            Equal(3, complex.Nodes.Count(node => node.Node.Kind == DdfFlowchartNodeKind.LoopDecision));
            DdfFlowchartLayoutNode decision = complex.Nodes.Single(node => node.Node.Kind == DdfFlowchartNodeKind.Decision);
            DdfFlowchartEdge trueEdge = complexGraph.Edges.Single(edge => edge.From == decision.Node.Id && edge.Kind == DdfFlowchartEdgeKind.True);
            DdfFlowchartEdge falseEdge = complexGraph.Edges.Single(edge => edge.From == decision.Node.Id && edge.Kind == DdfFlowchartEdgeKind.False);
            Equal(true, complex.FindNode(trueEdge.To).Lane < decision.Lane);
            Equal(true, complex.FindNode(falseEdge.To).Lane > decision.Lane);
            foreach (DdfFlowchartLayoutNode loop in complex.Nodes.Where(node =>
                node.Node.Kind == DdfFlowchartNodeKind.LoopDecision))
            {
                DdfFlowchartEdge exitEdge = complexGraph.Edges.Single(edge =>
                    edge.From == loop.Node.Id && edge.Kind == DdfFlowchartEdgeKind.False);
                Equal(loop.Lane, complex.FindNode(exitEdge.To).Lane);
            }
        }

        private static void ExecutesAndDiagnosesFromIntermediateRepresentation()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            const string valid = "main() out int\n{\n int value << 2;\n ret value + 3;\n}";
            DdfParseResult validParse = DdfParser.Parse(valid, package.Language);
            DdfIrProgram validIr = DdfIrLowerer.Lower(valid, validParse, package.Language).Program;
            DdfExecutionResult validResult = DdfInterpreter.Execute(valid, validIr);
            Equal(5L, Convert.ToInt64(validResult.ReturnValue));
            Equal(0, validResult.Diagnostics.Count);

            const string invalidRuntime = "divide() out int\n{\n ret 1 / 0;\n}\nmain() out int\n{\n ret divide();\n}";
            DdfParseResult invalidParse = DdfParser.Parse(invalidRuntime, package.Language);
            DdfIrProgram invalidIr = DdfIrLowerer.Lower(invalidRuntime, invalidParse, package.Language).Program;
            DdfExecutionResult failed = DdfInterpreter.Execute(invalidRuntime, invalidIr);
            DdfDiagnostic diagnostic = failed.Diagnostics.Single();
            Equal("DDF404", diagnostic.Code);
            Equal(DdfDiagnosticDomain.Runtime, diagnostic.Domain);
            Equal(3, diagnostic.Line);
            Equal(true, failed.StackTrace.Count >= 2);
            Equal("divide", failed.StackTrace[0].FunctionName);
            Equal("main", failed.StackTrace[1].FunctionName);
        }

        private static void RunsIsolatedHostExecutionSessions()
        {
            var leftDocuments = new OpenDocumentCollection();
            leftDocuments.CreateVirtual("main.ddf",
                "main() out int { int result << helper(20); result >> Console; ret result; }");
            leftDocuments.CreateVirtual("helpers.ddf",
                "helper(int value) out int { ret value + 22; }");
            var rightDocuments = new OpenDocumentCollection();
            rightDocuments.CreateVirtual("exercise.ddf",
                "main() out int { ret 7; }");
            using var left = new DdfEditorExecutionService(leftDocuments,
                new DdfLanguagePackageSession());
            using var right = new DdfEditorExecutionService(rightDocuments,
                new DdfLanguagePackageSession());
            var leftOutput = new List<string>();
            var leftStates = new List<DdfEditorExecutionState>();
            left.OutputReceived += (sender, args) => leftOutput.Add(args.Text);
            left.StateChanged += (sender, args) => leftStates.Add(args.State);

            DdfEditorExecutionCompletion leftCompletion = left.RunAsync()
                .GetAwaiter().GetResult();
            DdfEditorExecutionCompletion rightCompletion = right.RunAsync()
                .GetAwaiter().GetResult();
            Equal(true, leftCompletion.Succeeded);
            Equal(42L, Convert.ToInt64(leftCompletion.ExecutionResult.ReturnValue));
            Equal("42", leftOutput.Single());
            Equal(true, rightCompletion.Succeeded);
            Equal(7L, Convert.ToInt64(rightCompletion.ExecutionResult.ReturnValue));
            Equal(DdfEditorExecutionState.Idle, left.State);
            Equal(DdfEditorExecutionState.Idle, right.State);
            Equal(true, leftStates.Contains(DdfEditorExecutionState.Running));
            Equal(true, leftStates.Last() == DdfEditorExecutionState.Idle);
            Equal(false, ReferenceEquals(left.LastCompletion, right.LastCompletion));

            var loopDocuments = new OpenDocumentCollection();
            loopDocuments.CreateVirtual("loop.ddf",
                "main() out int { int value << 0; while(true) { value++; } ret value; }");
            using var loop = new DdfEditorExecutionService(loopDocuments,
                new DdfLanguagePackageSession());
            Task<DdfEditorExecutionCompletion> running = loop.RunAsync(
                new DdfEditorExecutionRequest(maxInstructions: int.MaxValue));
            Equal(true, loop.Stop());
            DdfEditorExecutionCompletion stopped = running.GetAwaiter().GetResult();
            Equal(true, stopped.WasCancelled);
            Equal(false, stopped.Succeeded);
            Equal(DdfEditorExecutionState.Idle, loop.State);
        }

        private static void SharesHostCatalogAcrossPipelines()
        {
            var catalog = new DdfEditorHostCatalog();
            int catalogEvents = 0;
            catalog.Changed += (sender, args) => catalogEvents++;
            catalog.RegisterPermission(new DdfHostPermissionDefinition(
                "math.use", "Usa calcoli host", "Autorizza funzioni matematiche dell'host."));
            catalog.RegisterType(new DdfHostTypeDefinition("Sensor", new[]
            {
                new DdfHostFieldDefinition("value", "int", "Valore corrente.")
            }, "Sensore esposto dall'applicazione."));
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "threshold", "int", () => 21, "Soglia configurata dall'host."));
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "hostScale", "int", new[]
                {
                    new DdfHostParameterDefinition("value", "int")
                }, arguments => Convert.ToInt32(arguments[0]) * 2,
                "Raddoppia un valore tramite il servizio host.",
                new[] { "math.use" }));

            DdfHostCatalogSnapshot deniedSnapshot = catalog.Snapshot;
            Equal(4L, deniedSnapshot.Revision);
            Equal(false, deniedSnapshot.IsGranted("math.use"));
            Equal(4, catalogEvents);

            string source = "main() out int { ret hostScale(threshold); }";
            DdfLanguagePackage package = new DdfLanguagePackageSession().ActivePackage;
            var request = new DdfEditorAnalysisRequest(source,
                source.IndexOf("hostScale", StringComparison.Ordinal), 1,
                package, 1, includeAllCompletions: true,
                hostCatalog: deniedSnapshot);
            using (var analysis = new DdfEditorAnalysisService())
            {
                DdfEditorAnalysisSnapshot snapshot = analysis.Analyze(request);
                Equal(false, snapshot.TypeCheckResult.Diagnostics.Any());
                Equal(false, snapshot.Diagnostics.Any(item => item.Code == "DDF201"));
                DdfCompletionItem function = snapshot.Completions.Items.Single(item =>
                    item.DisplayText == "hostScale");
                Equal("int", function.TypeName);
                Equal(true, function.Detail.Contains("Raddoppia un valore"));
                Equal(true, snapshot.Completions.Items.Any(item =>
                    item.DisplayText == "threshold" && item.TypeName == "int"));
            }

            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("host.ddf", source);
            var language = new DdfLanguagePackageSession();
            using (var execution = new DdfEditorExecutionService(
                () => documents, () => language, () => catalog.Snapshot))
            {
                DdfEditorExecutionCompletion denied = execution.RunAsync()
                    .GetAwaiter().GetResult();
                Equal(false, denied.Succeeded);
                Equal(true, denied.ExecutionResult.Diagnostics.Any(item =>
                    item.Code == "DDF409"));

                Equal(true, catalog.SetPermission("math.use", true));
                DdfEditorExecutionCompletion granted = execution.RunAsync()
                    .GetAwaiter().GetResult();
                Equal(true, granted.Succeeded);
                Equal(42L, Convert.ToInt64(granted.ExecutionResult.ReturnValue));
            }

            Equal(false, deniedSnapshot.IsGranted("math.use"));
            Equal(true, catalog.Snapshot.IsGranted("math.use"));
            Throws<ArgumentException>(() => catalog.RegisterFunction(
                new DdfHostFunctionDefinition("hostScale", "int",
                    Array.Empty<DdfHostParameterDefinition>(), arguments => 0)));
        }

        private static void EnforcesHostValueAdaptersAccessAndLifetimes()
        {
            var catalog = new DdfEditorHostCatalog();
            int snapshotReads = 0;
            int snapshotWrites = 0;
            string snapshotBacking = "5";
            var integerTextAdapter = new DdfHostValueAdapter(
                value => int.Parse((string)value),
                value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "snapshotValue", "int", () =>
                {
                    snapshotReads++;
                    return snapshotBacking;
                }, "Valore acquisito una volta per esecuzione.", null,
                value =>
                {
                    snapshotWrites++;
                    snapshotBacking = (string)value;
                }, DdfHostObjectLifetime.ExecutionSnapshot, integerTextAdapter));

            int liveReads = 0;
            int liveBacking = 3;
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "liveValue", "int", () =>
                {
                    liveReads++;
                    return liveBacking;
                }, "Valore riletto a ogni accesso.", null,
                value => liveBacking = Convert.ToInt32(value), DdfHostObjectLifetime.Live));
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "readOnlyValue", "int", () => 9));

            string adaptedArgument = null;
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "adapted", "int", new[]
                {
                    new DdfHostParameterDefinition("value", "int", null,
                        new DdfHostValueAdapter(value => value,
                            value => Convert.ToString(value,
                                System.Globalization.CultureInfo.InvariantCulture)))
                }, arguments =>
                {
                    adaptedArgument = (string)arguments[0];
                    return "11";
                }, returnValueAdapter: integerTextAdapter));

            string source = "main() out int { int first << snapshotValue; " +
                "int second << snapshotValue; snapshotValue << first + second; " +
                "int live << liveValue; liveValue << live + 1; " +
                "ret snapshotValue + liveValue + adapted(7); }";
            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("host-contract.ddf", source);
            var session = new DdfLanguagePackageSession();
            using (var execution = new DdfEditorExecutionService(() => documents,
                () => session, () => catalog.Snapshot))
            {
                DdfEditorExecutionCompletion result = execution.RunAsync()
                    .GetAwaiter().GetResult();
                Equal(true, result.Succeeded);
                Equal(25L, Convert.ToInt64(result.ExecutionResult.ReturnValue));
            }
            Equal(1, snapshotReads);
            Equal(1, snapshotWrites);
            Equal("10", snapshotBacking);
            Equal(2, liveReads);
            Equal(4, liveBacking);
            Equal("7", adaptedArgument);

            string invalid = "main() out int { readOnlyValue << 2; ret readOnlyValue; }";
            DdfLanguagePackage package = new DdfLanguagePackageSession().ActivePackage;
            DdfParseResult parse = DdfParser.Parse(invalid, package.Language);
            DdfTypeCheckResult checkedResult = DdfTypeChecker.Check(invalid, parse.Root,
                typeSystem: package.Language.Types,
                standardLibrary: package.Language.StandardLibrary,
                hostCatalog: catalog.Snapshot);
            Equal(true, checkedResult.Diagnostics.Any(item => item.Code == "DDF309"));

            DdfIrProgram program = DdfIrLowerer.Lower(invalid, parse,
                package.Language).Program;
            DdfExecutionResult runtimeResult = DdfVirtualMachine.Execute(invalid, program,
                package.Language.Runtime, package.Language.Types,
                package.Language.StandardLibrary,
                new DdfExecutionOptions { HostCatalog = catalog.Snapshot });
            Equal(true, runtimeResult.Diagnostics.Any(item => item.Code == "DDF411"));
        }

        private static void SeparatesAnalysisSimulationAndHostEffects()
        {
            int hostValue = 10;
            int reads = 0;
            int realCalls = 0;
            int simulatedCalls = 0;
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "hostValue", "int", () =>
                {
                    reads++;
                    return hostValue;
                }, writeValue: value => hostValue = Convert.ToInt32(value),
                lifetime: DdfHostObjectLifetime.Live));
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "apply", "int", new[] { new DdfHostParameterDefinition("value", "int") },
                arguments =>
                {
                    realCalls++;
                    hostValue += Convert.ToInt32(arguments[0]);
                    return hostValue;
                }, effect: DdfHostFunctionEffect.HostMutation,
                simulate: arguments =>
                {
                    simulatedCalls++;
                    return Convert.ToInt32(arguments[0]) + 100;
                }));

            var documents = new OpenDocumentCollection();
            IDdfEditorDocument document = documents.CreateVirtual("modes.ddf",
                "main() out int { hostValue << hostValue + 1; ret apply(hostValue); }");
            var language = new DdfLanguagePackageSession();
            using var execution = new DdfEditorExecutionService(() => documents,
                () => language, () => catalog.Snapshot);

            DdfEditorExecutionCompletion analysis = execution.RunAsync(
                new DdfEditorExecutionRequest(mode: DdfEditorExecutionMode.AnalysisOnly))
                .GetAwaiter().GetResult();
            Equal(true, analysis.Succeeded);
            Equal(DdfEditorExecutionMode.AnalysisOnly, analysis.Mode);
            Equal(null, analysis.ExecutionResult);
            Equal(0, reads);
            Equal(10, hostValue);

            DdfEditorExecutionCompletion simulation = execution.RunAsync(
                new DdfEditorExecutionRequest(mode: DdfEditorExecutionMode.Simulation))
                .GetAwaiter().GetResult();
            Equal(true, simulation.Succeeded);
            Equal(DdfEditorExecutionMode.Simulation, simulation.Mode);
            Equal(111L, Convert.ToInt64(simulation.ExecutionResult.ReturnValue));
            Equal(1, reads);
            Equal(10, hostValue);
            Equal(0, realCalls);
            Equal(1, simulatedCalls);

            DdfEditorExecutionCompletion effects = execution.RunAsync(
                new DdfEditorExecutionRequest(mode: DdfEditorExecutionMode.HostEffects))
                .GetAwaiter().GetResult();
            Equal(true, effects.Succeeded);
            Equal(22L, Convert.ToInt64(effects.ExecutionResult.ReturnValue));
            Equal(3, reads);
            Equal(22, hostValue);
            Equal(1, realCalls);
            Equal(1, simulatedCalls);

            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "danger", "int", Array.Empty<DdfHostParameterDefinition>(),
                arguments => 1, effect: DdfHostFunctionEffect.HostMutation));
            documents.UpdateDocument(document.Id,
                "main() out int { ret danger(); }");
            DdfEditorExecutionCompletion unavailable = execution.RunAsync(
                new DdfEditorExecutionRequest(mode: DdfEditorExecutionMode.Simulation))
                .GetAwaiter().GetResult();
            Equal(false, unavailable.Succeeded);
            Equal(true, unavailable.ExecutionResult.Diagnostics.Any(item =>
                item.Code == "DDF412"));

            using var debugger = new DdfEditorDebugService(() => documents,
                () => language, () => catalog.Snapshot);
            Throws<ArgumentException>(() => debugger.RunAsync(
                new DdfEditorExecutionRequest(mode: DdfEditorExecutionMode.AnalysisOnly)));
        }

        private static void ReturnsImmutableTypedHostValuesAfterExecution()
        {
            string counter = "5";
            var adapter = new DdfHostValueAdapter(
                value => int.Parse((string)value,
                    System.Globalization.CultureInfo.InvariantCulture),
                value => Convert.ToString(value,
                    System.Globalization.CultureInfo.InvariantCulture));
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterType(new DdfHostTypeDefinition("Device", new[]
            {
                new DdfHostFieldDefinition("name", "string"),
                new DdfHostFieldDefinition("increment", "int")
            }));
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "hostCounter", "int", () => counter,
                writeValue: value => counter = (string)value,
                valueAdapter: adapter));
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "device", "Device", () => new Dictionary<string, object>
                {
                    ["name"] = "demo",
                    ["increment"] = 3
                }));

            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("host-results.ddf",
                "main() out int { hostCounter << hostCounter + device.increment; ret hostCounter; }");
            var language = new DdfLanguagePackageSession();
            using var execution = new DdfEditorExecutionService(() => documents,
                () => language, () => catalog.Snapshot);

            DdfEditorExecutionCompletion completion = execution.RunAsync()
                .GetAwaiter().GetResult();
            Equal(true, completion.Succeeded);
            Equal("8", counter);
            Equal(2, completion.HostValues.Count);
            Equal(true, ReferenceEquals(completion.HostValues,
                completion.ExecutionResult.HostValues));

            DdfHostValueResult counterResult = completion.HostValues.Single(item =>
                item.Name == "hostCounter");
            Equal("int", counterResult.TypeName);
            Equal("5", counterResult.InitialValue);
            Equal("8", counterResult.Value);
            Equal(DdfHostValueAccess.ReadWrite, counterResult.Access);
            Equal(true, counterResult.WasWritten);

            DdfHostValueResult deviceResult = completion.HostValues.Single(item =>
                item.Name == "device");
            Equal(DdfHostValueAccess.ReadOnly, deviceResult.Access);
            Equal(false, deviceResult.WasWritten);
            var fields = (IReadOnlyDictionary<string, object>)deviceResult.Value;
            Equal("demo", fields["name"]);
            Equal(3, fields["increment"]);
            Throws<NotSupportedException>(() =>
                ((IDictionary<string, object>)fields).Add("unexpected", 1));

            DdfEditorExecutionCompletion simulation = execution.RunAsync(
                new DdfEditorExecutionRequest(mode: DdfEditorExecutionMode.Simulation))
                .GetAwaiter().GetResult();
            Equal(true, simulation.Succeeded);
            Equal("8", counter);
            DdfHostValueResult simulatedCounter = simulation.HostValues.Single(item =>
                item.Name == "hostCounter");
            Equal("8", simulatedCounter.InitialValue);
            Equal("11", simulatedCounter.Value);
            Equal(true, simulatedCounter.WasWritten);
        }

        private static void InvokesTypedProceduresManuallyAndFromHostEvents()
        {
            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("procedures.ddf",
                "main() out int { ret 0; } " +
                "calculate(int value, int factor) out int { ret value * factor; } " +
                "busy(int limit) out int { int index << 0; while(index < limit) { index++; } ret index; }");
            var language = new DdfLanguagePackageSession();
            using var execution = new DdfEditorExecutionService(() => documents,
                () => language);

            var arguments = new Dictionary<string, object>
            {
                ["value"] = 6,
                ["factor"] = 7
            };
            var manualRequest = new DdfEditorProcedureRequest("calculate", arguments);
            arguments["value"] = 99;
            DdfEditorExecutionCompletion manual = execution.InvokeAsync(manualRequest)
                .GetAwaiter().GetResult();
            Equal(true, manual.Succeeded);
            Equal(42L, Convert.ToInt64(manual.ExecutionResult.ReturnValue));
            Equal("int", manual.ExecutionResult.ReturnTypeName);
            Equal(DdfEditorProcedureTrigger.Manual,
                manual.ProcedureInvocation.Trigger);
            Equal(6, manual.ProcedureInvocation.Arguments["value"]);

            DdfEditorExecutionCompletion hostEvent = execution.InvokeAsync(
                new DdfEditorProcedureRequest("calculate",
                    new Dictionary<string, object>
                    {
                        ["value"] = 8,
                        ["factor"] = 5
                    }, DdfEditorProcedureTrigger.HostEvent, "order.updated"))
                .GetAwaiter().GetResult();
            Equal(true, hostEvent.Succeeded);
            Equal(40L, Convert.ToInt64(hostEvent.ExecutionResult.ReturnValue));
            Equal("order.updated", hostEvent.ProcedureInvocation.TriggerId);

            DdfEditorExecutionCompletion missing = execution.InvokeAsync(
                new DdfEditorProcedureRequest("calculate",
                    new Dictionary<string, object> { ["value"] = 2 }))
                .GetAwaiter().GetResult();
            Equal(false, missing.Succeeded);
            Equal(true, missing.ExecutionResult.Diagnostics.Any(item =>
                item.Code == "DDF416"));

            DdfEditorExecutionCompletion unexpected = execution.InvokeAsync(
                new DdfEditorProcedureRequest("calculate",
                    new Dictionary<string, object>
                    {
                        ["value"] = 2,
                        ["factor"] = 3,
                        ["other"] = 4
                    })).GetAwaiter().GetResult();
            Equal(true, unexpected.ExecutionResult.Diagnostics.Any(item =>
                item.Code == "DDF416"));

            DdfEditorExecutionCompletion invalidType = execution.InvokeAsync(
                new DdfEditorProcedureRequest("calculate",
                    new Dictionary<string, object>
                    {
                        ["value"] = 2,
                        ["factor"] = "not-a-number"
                    })).GetAwaiter().GetResult();
            Equal(true, invalidType.ExecutionResult.Diagnostics.Any(item =>
                item.Code == "DDF416"));

            Throws<ArgumentException>(() => new DdfEditorProcedureRequest(
                "calculate", trigger: DdfEditorProcedureTrigger.HostEvent));

            Task<DdfEditorExecutionCompletion> busy = execution.InvokeAsync(
                new DdfEditorProcedureRequest("busy",
                    new Dictionary<string, object> { ["limit"] = int.MaxValue },
                    execution: new DdfEditorExecutionRequest(
                        maxInstructions: int.MaxValue)));
            SpinWait.SpinUntil(() => execution.IsRunning, 1000);
            Throws<InvalidOperationException>(() => execution.InvokeAsync(
                new DdfEditorProcedureRequest("calculate",
                    new Dictionary<string, object>
                    {
                        ["value"] = 1,
                        ["factor"] = 1
                    })));
            Equal(true, execution.Stop());
            Equal(true, busy.GetAwaiter().GetResult().WasCancelled);
        }

        private static void ExecutesParameterizedTypedDatabaseServices()
        {
            var service = new FakeDatabaseService();
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterPermission(new DdfHostPermissionDefinition(
                "db.access", "Accesso database"));
            catalog.RegisterPermission(new DdfHostPermissionDefinition(
                "db.read", "Lettura database"));
            catalog.RegisterPermission(new DdfHostPermissionDefinition(
                "db.write", "Scrittura database"));
            catalog.RegisterType(new DdfHostTypeDefinition("CustomerRow", new[]
            {
                new DdfHostFieldDefinition("id", "int"),
                new DdfHostFieldDefinition("name", "string")
            }));
            catalog.RegisterDatabase(new DdfHostDatabaseDefinition("crm", service,
                new[]
                {
                    new DdfHostDatabaseOperationDefinition("loadCustomer",
                        "customer.byId", DdfHostDatabaseOperationKind.Query,
                        "CustomerRow", new[]
                        {
                            new DdfHostParameterDefinition("customerId", "int")
                        }, requiredPermissions: new[] { "db.read" }),
                    new DdfHostDatabaseOperationDefinition("updateCustomerName",
                        "customer.updateName", DdfHostDatabaseOperationKind.Command,
                        "int", new[]
                        {
                            new DdfHostParameterDefinition("customerId", "int"),
                            new DdfHostParameterDefinition("name", "string")
                        }, requiredPermissions: new[] { "db.write" },
                        supportsSimulation: true),
                    new DdfHostDatabaseOperationDefinition("slowCustomer",
                        "customer.slow", DdfHostDatabaseOperationKind.Query,
                        "CustomerRow", new[]
                        {
                            new DdfHostParameterDefinition("customerId", "int")
                        }, requiredPermissions: new[] { "db.read" })
                }, requiredPermissions: new[] { "db.access" }));
            Equal(1, catalog.Snapshot.Databases.Count);
            Equal(3, catalog.Snapshot.Functions.Count);
            catalog.SetPermission("db.access", true);
            catalog.SetPermission("db.read", true);
            catalog.SetPermission("db.write", true);

            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("database.ddf",
                "main() out int { ret 0; } " +
                "lookup(int id) out string { CustomerRow row << loadCustomer(id); ret row.name; } " +
                "rename(int id, string nextName) out int { ret updateCustomerName(id, nextName); } " +
                "slow(int id) out string { CustomerRow row << slowCustomer(id); ret row.name; }");
            var language = new DdfLanguagePackageSession();
            using var execution = new DdfEditorExecutionService(() => documents,
                () => language, () => catalog.Snapshot);
            var transactions = new Dictionary<string, string>
            {
                ["crm"] = "tx-demo-42"
            };

            DdfEditorExecutionCompletion lookup = execution.InvokeAsync(
                new DdfEditorProcedureRequest("lookup",
                    new Dictionary<string, object> { ["id"] = 7 },
                    execution: new DdfEditorExecutionRequest(
                        databaseTransactions: transactions)))
                .GetAwaiter().GetResult();
            Equal(true, lookup.Succeeded);
            Equal("Ada", lookup.ExecutionResult.ReturnValue);
            DdfHostDatabaseRequest query = service.Requests.Single();
            Equal("crm", query.DatabaseId);
            Equal("customer.byId", query.StatementId);
            Equal(7, query.Parameters["customerId"]);
            Equal("tx-demo-42", query.TransactionId);
            Equal(true, query.Invocation.CancellationToken.CanBeCanceled);

            DdfEditorExecutionCompletion simulated = execution.InvokeAsync(
                new DdfEditorProcedureRequest("rename",
                    new Dictionary<string, object>
                    {
                        ["id"] = 7,
                        ["nextName"] = "Grace"
                    }, execution: new DdfEditorExecutionRequest(
                        mode: DdfEditorExecutionMode.Simulation,
                        databaseTransactions: transactions)))
                .GetAwaiter().GetResult();
            Equal(true, simulated.Succeeded);
            Equal("Ada", service.CustomerName);
            Equal(DdfEditorExecutionMode.Simulation,
                service.Requests.Last().Invocation.Mode);

            DdfEditorExecutionCompletion updated = execution.InvokeAsync(
                new DdfEditorProcedureRequest("rename",
                    new Dictionary<string, object>
                    {
                        ["id"] = 7,
                        ["nextName"] = "Grace"
                    }, execution: new DdfEditorExecutionRequest(
                        databaseTransactions: transactions)))
                .GetAwaiter().GetResult();
            Equal(true, updated.Succeeded);
            Equal("Grace", service.CustomerName);

            DdfEditorExecutionCompletion timedOut = execution.InvokeAsync(
                new DdfEditorProcedureRequest("slow",
                    new Dictionary<string, object> { ["id"] = 7 },
                    execution: new DdfEditorExecutionRequest(
                        hostCallTimeout: TimeSpan.FromMilliseconds(20))))
                .GetAwaiter().GetResult();
            Equal(true, timedOut.ExecutionResult.Diagnostics.Any(item =>
                item.Code == "DDF414"));

            Throws<ArgumentException>(() => catalog.RegisterDatabase(
                new DdfHostDatabaseDefinition("other", service, new[]
                {
                    new DdfHostDatabaseOperationDefinition("loadCustomer",
                        "duplicate", DdfHostDatabaseOperationKind.Query,
                        "int", Array.Empty<DdfHostParameterDefinition>())
                })));
        }

        private static void RegistersTypedHostFunctionLibrariesAtomically()
        {
            int hostScore = 10;
            int realApplications = 0;
            int simulatedApplications = 0;
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterPermission(new DdfHostPermissionDefinition(
                "algorithms.use", "Uso algoritmi applicativi"));
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "hostScore", "int", () => hostScore,
                writeValue: value => hostScore = Convert.ToInt32(value)));
            catalog.RegisterFunctionLibrary(new DdfHostFunctionLibraryDefinition(
                "demo.algorithms", "Algoritmi dimostrativi", new[]
                {
                    new DdfHostFunctionDefinition("weightedScore", "int", new[]
                    {
                        new DdfHostParameterDefinition("current", "int"),
                        new DdfHostParameterDefinition("sample", "int"),
                        new DdfHostParameterDefinition("weight", "int")
                    }, arguments => Convert.ToInt32(arguments[0]) +
                        Convert.ToInt32(arguments[1]) * Convert.ToInt32(arguments[2]),
                        "Combina matematicamente un valore corrente e un campione."),
                    new DdfHostFunctionDefinition("isWithinRange", "bool", new[]
                    {
                        new DdfHostParameterDefinition("value", "int"),
                        new DdfHostParameterDefinition("minimum", "int"),
                        new DdfHostParameterDefinition("maximum", "int")
                    }, arguments => Convert.ToInt32(arguments[0]) >=
                            Convert.ToInt32(arguments[1]) &&
                        Convert.ToInt32(arguments[0]) <= Convert.ToInt32(arguments[2]),
                        "Applica una regola logica di appartenenza a un intervallo."),
                    new DdfHostFunctionDefinition("applyHostScore", "int", new[]
                    {
                        new DdfHostParameterDefinition("value", "int")
                    }, arguments =>
                    {
                        realApplications++;
                        hostScore = Math.Max(0, Math.Min(100,
                            Convert.ToInt32(arguments[0])));
                        return hostScore;
                    }, "Normalizza e aggiorna lo stato dell'applicazione host.",
                    effect: DdfHostFunctionEffect.HostMutation,
                    simulate: arguments =>
                    {
                        simulatedApplications++;
                        return Math.Max(0, Math.Min(100,
                            Convert.ToInt32(arguments[0])));
                    })
                }, "Matematica, logica e mutazioni controllate dell'host.",
                new[] { "algorithms.use" }));

            Equal(1, catalog.Snapshot.FunctionLibraries.Count);
            Equal(3, catalog.Snapshot.Functions.Count);
            Equal(true, catalog.Snapshot.Functions.All(function =>
                function.RequiredPermissions.Contains("algorithms.use")));

            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("algorithms.ddf",
                "main() out int { " +
                "int score << weightedScore(hostScore, 20, 2); " +
                "bool accepted << isWithinRange(score, 0, 100); " +
                "if(accepted) { hostScore << applyHostScore(score); } " +
                "ret hostScore; }");
            var language = new DdfLanguagePackageSession();
            using var execution = new DdfEditorExecutionService(() => documents,
                () => language, () => catalog.Snapshot);

            DdfEditorExecutionCompletion denied = execution.RunAsync()
                .GetAwaiter().GetResult();
            Equal(false, denied.Succeeded);
            Equal(true, denied.ExecutionResult.Diagnostics.Any(item =>
                item.Code == "DDF409"));

            catalog.SetPermission("algorithms.use", true);
            DdfEditorExecutionCompletion simulated = execution.RunAsync(
                new DdfEditorExecutionRequest(mode: DdfEditorExecutionMode.Simulation))
                .GetAwaiter().GetResult();
            Equal(true, simulated.Succeeded);
            Equal(50L, Convert.ToInt64(simulated.ExecutionResult.ReturnValue));
            Equal(10, hostScore);
            Equal(0, realApplications);
            Equal(1, simulatedApplications);

            DdfEditorExecutionCompletion applied = execution.RunAsync()
                .GetAwaiter().GetResult();
            Equal(true, applied.Succeeded);
            Equal(50L, Convert.ToInt64(applied.ExecutionResult.ReturnValue));
            Equal(50, hostScore);
            Equal(1, realApplications);

            Throws<ArgumentException>(() => catalog.RegisterFunctionLibrary(
                new DdfHostFunctionLibraryDefinition("invalid.library",
                    "Libreria non atomica", new[]
                    {
                        new DdfHostFunctionDefinition("uniqueFunction", "int",
                            Array.Empty<DdfHostParameterDefinition>(), arguments => 1),
                        new DdfHostFunctionDefinition("weightedScore", "int",
                            Array.Empty<DdfHostParameterDefinition>(), arguments => 2)
                    })));
            Equal(1, catalog.Snapshot.FunctionLibraries.Count);
            Equal(false, catalog.Snapshot.Functions.Any(function =>
                function.Name == "uniqueFunction"));
        }

        private static void ReportsAndRollsBackPartialHostEffects()
        {
            int hostValue = 10;
            string observedTransaction = null;
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterObject(new DdfHostObjectDefinition(
                "hostValue", "int", () => hostValue,
                writeValue: value => hostValue = Convert.ToInt32(value)));
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "mutateHost", "int", new[]
                {
                    new DdfHostParameterDefinition("value", "int")
                }, null, effect: DdfHostFunctionEffect.HostMutation,
                simulate: arguments => Convert.ToInt32(arguments[0]),
                invokeAsync: (context, arguments) =>
                {
                    observedTransaction = context.HostEffectTransactionId;
                    hostValue = Convert.ToInt32(arguments[0]);
                    return ValueTask.FromResult<object>(hostValue);
                }));
            const string failingSource =
                "main() out int { hostValue << mutateHost(20); int zero << 0; ret 1 / zero; }";
            var documents = new OpenDocumentCollection();
            IDdfEditorDocument document = documents.CreateVirtual(
                "host-effects.ddf", failingSource);
            var language = new DdfLanguagePackageSession();
            using var execution = new DdfEditorExecutionService(() => documents,
                () => language, () => catalog.Snapshot);

            DdfEditorExecutionCompletion immediate = execution.RunAsync()
                .GetAwaiter().GetResult();
            Equal(false, immediate.Succeeded);
            Equal(20, hostValue);
            Equal(DdfHostEffectDisposition.PartiallyApplied,
                immediate.HostEffects.Disposition);
            Equal(2, immediate.HostEffects.Operations.Count);
            Equal(true, immediate.HostEffects.Operations.All(item =>
                item.Status == DdfHostEffectStatus.Applied));
            Equal(string.Empty, observedTransaction);

            hostValue = 10;
            var rollbackFactory = new SnapshotEffectTransactionFactory(
                () => hostValue, value => hostValue = value);
            DdfEditorExecutionCompletion rolledBack = execution.RunAsync(
                new DdfEditorExecutionRequest(
                    hostEffectPolicy: DdfHostEffectPolicy.RollbackOnFailure,
                    hostEffectTransactionFactory: rollbackFactory))
                .GetAwaiter().GetResult();
            Equal(false, rolledBack.Succeeded);
            Equal(10, hostValue);
            Equal(DdfHostEffectDisposition.RolledBack,
                rolledBack.HostEffects.Disposition);
            Equal(true, rollbackFactory.LastTransaction.RolledBack);
            Equal(false, rollbackFactory.LastTransaction.Committed);
            Equal(true, rollbackFactory.LastTransaction.Disposed);
            Equal(rollbackFactory.LastTransaction.Id, observedTransaction);

            DdfEditorExecutionCompletion simulated = execution.RunAsync(
                new DdfEditorExecutionRequest(mode: DdfEditorExecutionMode.Simulation))
                .GetAwaiter().GetResult();
            Equal(false, simulated.Succeeded);
            Equal(10, hostValue);
            Equal(DdfHostEffectDisposition.Simulated,
                simulated.HostEffects.Disposition);
            Equal(true, simulated.HostEffects.Operations.All(item =>
                item.Status == DdfHostEffectStatus.Simulated));

            documents.UpdateDocument(document.Id,
                "main() out int { hostValue << mutateHost(30); ret hostValue; }");
            var commitFactory = new SnapshotEffectTransactionFactory(
                () => hostValue, value => hostValue = value);
            DdfEditorExecutionCompletion committed = execution.RunAsync(
                new DdfEditorExecutionRequest(
                    hostEffectPolicy: DdfHostEffectPolicy.RollbackOnFailure,
                    hostEffectTransactionFactory: commitFactory))
                .GetAwaiter().GetResult();
            Equal(true, committed.Succeeded);
            Equal(30, hostValue);
            Equal(DdfHostEffectDisposition.Committed,
                committed.HostEffects.Disposition);
            Equal(true, commitFactory.LastTransaction.Committed);
            Equal(false, commitFactory.LastTransaction.RolledBack);
            Equal(true, commitFactory.LastTransaction.Disposed);
            Equal("main() out int { hostValue << mutateHost(30); ret hostValue; }",
                documents.ActiveDocumentInfo.SourceText);

            Throws<ArgumentException>(() => new DdfEditorExecutionRequest(
                hostEffectPolicy: DdfHostEffectPolicy.RollbackOnFailure));
        }

        private static void BoundsAsyncHostOperationsAndReleasesResources()
        {
            var releases = new List<string>();
            Guid executionId = Guid.Empty;
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "asyncDouble", "int",
                new[] { new DdfHostParameterDefinition("value", "int") }, null,
                invokeAsync: async (context, arguments) =>
                {
                    executionId = context.ExecutionId;
                    Equal(DdfEditorExecutionMode.HostEffects, context.Mode);
                    await Task.Delay(5, context.CancellationToken);
                    return Convert.ToInt32(arguments[0]) * 2;
                }));
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "acquire", "int", Array.Empty<DdfHostParameterDefinition>(), null,
                invokeAsync: (context, arguments) =>
                {
                    context.RegisterResource(new TrackingHostResource("first", releases));
                    context.RegisterAsyncResource(new TrackingAsyncHostResource("second", releases));
                    return ValueTask.FromResult<object>(7);
                }));
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "slowHost", "int", Array.Empty<DdfHostParameterDefinition>(), null,
                invokeAsync: async (context, arguments) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken);
                    return 1;
                }));

            var documents = new OpenDocumentCollection();
            IDdfEditorDocument document = documents.CreateVirtual("host-async.ddf",
                "main() out int { ret acquire() + asyncDouble(2); }");
            var language = new DdfLanguagePackageSession();
            using var execution = new DdfEditorExecutionService(() => documents,
                () => language, () => catalog.Snapshot);

            DdfEditorExecutionCompletion completed = execution.RunAsync(
                new DdfEditorExecutionRequest(hostCallTimeout: TimeSpan.FromSeconds(1)))
                .GetAwaiter().GetResult();
            if (!completed.Succeeded)
            {
                IEnumerable<string> runtimeMessages = completed.ExecutionResult == null
                    ? Array.Empty<string>()
                    : completed.ExecutionResult.Diagnostics.Select(item => item.Message);
                throw new InvalidOperationException("host async failure: " +
                    completed.InternalError + " " + string.Join(" | ",
                        completed.BuildIssues.Select(item => item.Message).Concat(
                            runtimeMessages)));
            }
            Equal(true, completed.Succeeded);
            Equal(11L, Convert.ToInt64(completed.ExecutionResult.ReturnValue));
            Equal(true, executionId != Guid.Empty);
            SequenceEqual(new[] { "second", "first" }, releases);

            documents.UpdateDocument(document.Id,
                "main() out int { ret slowHost(); }");
            DdfEditorExecutionCompletion timedOut = execution.RunAsync(
                new DdfEditorExecutionRequest(hostCallTimeout: TimeSpan.FromMilliseconds(20)))
                .GetAwaiter().GetResult();
            Equal(false, timedOut.Succeeded);
            if (!timedOut.ExecutionResult.Diagnostics.Any(item => item.Code == "DDF414"))
                throw new InvalidOperationException("expected DDF414 but found " +
                    string.Join(",", timedOut.ExecutionResult.Diagnostics.Select(item => item.Code)));

            documents.UpdateDocument(document.Id,
                "main() out int { ret asyncDouble(1) + asyncDouble(2); }");
            DdfEditorExecutionCompletion limited = execution.RunAsync(
                new DdfEditorExecutionRequest(maxHostCalls: 1))
                .GetAwaiter().GetResult();
            Equal(false, limited.Succeeded);
            if (!limited.ExecutionResult.Diagnostics.Any(item => item.Code == "DDF413"))
                throw new InvalidOperationException("expected DDF413 but found " +
                    string.Join(",", limited.ExecutionResult.Diagnostics.Select(item => item.Code)));

            releases.Clear();
            documents.UpdateDocument(document.Id,
                "main() out int { ret acquire(); }");
            DdfEditorExecutionCompletion resourceLimited = execution.RunAsync(
                new DdfEditorExecutionRequest(maxHostResources: 1))
                .GetAwaiter().GetResult();
            Equal(false, resourceLimited.Succeeded);
            Equal(true, resourceLimited.ExecutionResult.Diagnostics.Any(item =>
                item.Code == "DDF413"));
            SequenceEqual(new[] { "second", "first" }, releases);

            documents.UpdateDocument(document.Id,
                "main() out int { while(true) { } ret 0; }");
            DdfEditorExecutionCompletion cancelled = execution.RunAsync(
                new DdfEditorExecutionRequest(maxInstructions: int.MaxValue,
                    timeout: TimeSpan.FromMilliseconds(25)))
                .GetAwaiter().GetResult();
            if (!cancelled.WasCancelled)
                throw new InvalidOperationException("expected cancellation but found " +
                    string.Join(",", cancelled.ExecutionResult.Diagnostics.Select(item => item.Code)));

            Throws<ArgumentOutOfRangeException>(() => new DdfEditorExecutionRequest(
                hostCallTimeout: TimeSpan.Zero));
            Throws<ArgumentOutOfRangeException>(() => new DdfEditorExecutionRequest(
                maxHostResources: 0));
        }

        private static void BuildsImmutableHostDocumentSnapshots()
        {
            var documents = new OpenDocumentCollection();
            IDdfEditorDocument main = documents.CreateVirtual("main.ddf",
                "main() out int { ret helper(20); }");
            documents.CreateVirtual("helpers.ddf",
                "helper(int value) out int { ret value + 22; }");
            var build = new DdfEditorBuildService(documents,
                new DdfLanguagePackageSession());
            var states = new List<DdfEditorBuildState>();
            int completions = 0;
            build.StateChanged += (sender, args) => states.Add(args.State);
            build.BuildCompleted += (sender, args) => completions++;

            DdfEditorBuildSnapshot validation = build.Validate(
                new DdfEditorBuildRequest(entryDocumentId: main.Id));
            Equal(true, validation.Succeeded);
            Equal(DdfEditorBuildMode.Validate, validation.Mode);
            Equal(2, validation.Documents.Count);
            Equal(1L, validation.Documents[0].Version);
            Equal(true, validation.Artifact.UseUnsavedBuffers);
            Equal(1, completions);
            Equal(DdfEditorBuildState.Idle, build.State);
            Equal(true, states.Contains(DdfEditorBuildState.Building));
            Equal(DdfEditorBuildState.Idle, states.Last());

            documents.UpdateDocument(main.Id, "main() out int { ret 7; }");
            Equal("main() out int { ret helper(20); }",
                validation.Documents[0].SourceText);
            DdfEditorBuildSnapshot compiled = build.Build(
                new DdfEditorBuildRequest(entryDocumentId: main.Id));
            Equal(true, compiled.Succeeded);
            Equal(DdfEditorBuildMode.Build, compiled.Mode);
            Equal(2L, compiled.Generation);
            Equal(2, completions);
            Equal(true, ReferenceEquals(compiled, build.LastSnapshot));

            documents.UpdateDocument(main.Id, "main() out int { ret \"invalid\"; }");
            DdfEditorBuildSnapshot invalid = build.Validate();
            Equal(false, invalid.Succeeded);
            Equal(true, invalid.Issues.Any(item =>
                item.Severity == DdfDiagnosticSeverity.Error));
        }

        private static void RoutesHostBuildsThroughCustomHandler()
        {
            var documents = new OpenDocumentCollection();
            IDdfEditorDocument main = documents.CreateVirtual("main.ddf",
                "main() out int { ret 0; }");
            var session = new DdfLanguagePackageSession();
            var modes = new List<DdfEditorBuildMode>();
            var requests = new List<DdfEditorBuildRequest>();
            var documentCounts = new List<int>();
            var service = new DdfEditorBuildService(
                () => documents,
                () => session,
                null,
                (mode, request, snapshots, package, hostCatalog) =>
                {
                    modes.Add(mode);
                    requests.Add(request);
                    documentCounts.Add(snapshots.Count);
                    return DdfBuildResult.Failure(DdfBuildIssue.Configuration(
                        "DDFTEST", "Percorso di build personalizzato."));
                });

            DdfEditorBuildSnapshot validation = service.Validate(
                new DdfEditorBuildRequest("customMain", main.Id, 123));
            DdfEditorBuildSnapshot build = service.Build();

            SequenceEqual(new[] { DdfEditorBuildMode.Validate,
                DdfEditorBuildMode.Build }, modes);
            Equal("customMain", requests[0].EntryPoint);
            Equal(main.Id, requests[0].EntryDocumentId);
            Equal(123, requests[0].MaxInstructions);
            SequenceEqual(new[] { 1, 1 }, documentCounts);
            Equal("DDFTEST", validation.Issues.Single().Code);
            Equal(false, validation.Succeeded);
            Equal(2L, build.Generation);
            Equal(true, ReferenceEquals(build, service.LastSnapshot));
        }

        private static void RoutesHostExecutionThroughCustomBuildHandler()
        {
            var documents = new OpenDocumentCollection();
            IDdfEditorDocument main = documents.CreateVirtual("main.ddf",
                "main() out int { ret 42; }");
            var session = new DdfLanguagePackageSession();
            int callerThread = Environment.CurrentManagedThreadId;
            int handlerThread = -1;
            int calls = 0;
            using var execution = new DdfEditorExecutionService(
                () => documents,
                () => session,
                null,
                (request, snapshots, package, hostCatalog) =>
                {
                    calls++;
                    handlerThread = Environment.CurrentManagedThreadId;
                    string root = Path.Combine(Path.GetTempPath(),
                        "ddf-custom-execution");
                    var manifest = DdfProjectManifest.CreateImplicit(
                        "Custom execution", package, "main.ddf");
                    manifest.Sources = new List<string> { "main.ddf" };
                    var sources = snapshots.Select(item => new DdfBuildSource(
                        Path.Combine(root, item.DisplayName), item.DisplayName,
                        item.SourceText, true)).ToList().AsReadOnly();
                    DdfBuildResult result = new DdfVmBuildBackend().Build(root,
                        manifest, package, sources);
                    return new DdfEditorExecutionBuildContext(result, package,
                        new Dictionary<string, string>
                        {
                            [main.Id] = Path.Combine(root, "main.ddf")
                        });
                });

            DdfEditorExecutionCompletion completion = execution.RunAsync()
                .GetAwaiter().GetResult();

            Equal(1, calls);
            Equal(callerThread, handlerThread);
            Equal(true, completion.Succeeded);
            Equal(42L, Convert.ToInt64(completion.ExecutionResult.ReturnValue));
            Equal("main.ddf", completion.Artifact.EntrySource);
        }

        private static void DebugsHostBuffersWithDocumentBreakpoints()
        {
            var documents = new OpenDocumentCollection();
            IDdfEditorDocument main = documents.CreateVirtual("main.ddf",
                "main() out int\n{\n    int seed << 3;\n    int result << helper();\n    result++;\n    ret result;\n}");
            IDdfEditorDocument helper = documents.CreateVirtual("helper.ddf",
                "int globalValue << 2;\nhelper() out int\n{\n    int value << 4;\n    value++;\n    ret value;\n}");
            using var debugger = new DdfEditorDebugService(documents,
                new DdfLanguagePackageSession());
            using var pauseReached = new SemaphoreSlim(0);
            var states = new List<DdfEditorDebugState>();
            var pausedLocations = new List<DdfEditorDebugLocation>();
            var pauseSnapshots = new List<DdfEditorDebugSnapshot>();
            debugger.SetBreakpoints(new[]
            {
                new DdfEditorBreakpoint(main.Id, 4),
                new DdfEditorBreakpoint(main.Id, 99, false)
            });
            debugger.SetWatchExpressions(new[]
                { " value ", "value", "seed + globalValue" });
            SequenceEqual(new[] { "value", "seed + globalValue" },
                debugger.WatchExpressions);
            debugger.StateChanged += (sender, args) => states.Add(args.State);
            debugger.Paused += (sender, args) =>
            {
                pausedLocations.Add(args.Location);
                pauseSnapshots.Add(args.Snapshot);
                pauseReached.Release();
            };

            Task<DdfEditorExecutionCompletion> execution = debugger.RunAsync(
                new DdfEditorExecutionRequest(entryDocumentId: main.Id));
            Equal(true, pauseReached.Wait(TimeSpan.FromSeconds(5)));
            Equal(DdfEditorDebugState.Paused, debugger.State);
            Equal(true, debugger.IsActive);
            Equal(true, debugger.IsPaused);
            DdfEditorDebugLocation pausedLocation = pausedLocations.Last();
            Equal(main.Id, pausedLocation.DocumentId);
            Equal("main.ddf", pausedLocation.DisplayName);
            Equal(4, pausedLocation.Line);
            Equal(true, pausedLocation.Start >= 0);
            Equal(true, pausedLocation.Length > 0);
            Equal(true, debugger.StepInto());

            Equal(true, pauseReached.Wait(TimeSpan.FromSeconds(5)));
            pausedLocation = pausedLocations.Last();
            Equal(helper.Id, pausedLocation.DocumentId);
            Equal(4, pausedLocation.Line);
            Equal(true, debugger.StepOver());

            Equal(true, pauseReached.Wait(TimeSpan.FromSeconds(5)));
            pausedLocation = pausedLocations.Last();
            Equal(helper.Id, pausedLocation.DocumentId);
            Equal(5, pausedLocation.Line);
            DdfEditorDebugSnapshot helperSnapshot = pauseSnapshots.Last();
            Equal(true, ReferenceEquals(helperSnapshot, debugger.CurrentSnapshot));
            Equal(2, helperSnapshot.CallStack.Count);
            Equal("helper", helperSnapshot.CallStack[0].FunctionName);
            Equal(helper.Id, helperSnapshot.CallStack[0].Location.DocumentId);
            Equal("main", helperSnapshot.CallStack[1].FunctionName);
            Equal(main.Id, helperSnapshot.CallStack[1].Location.DocumentId);
            DdfEditorDebugVariableSnapshot value = helperSnapshot.Variables
                .Single(item => item.Name == "value");
            Equal("int", value.TypeName);
            Equal("4", value.DisplayValue);
            Equal(DdfRuntimeVariableKind.Local, value.Kind);
            Equal(1, helperSnapshot.Globals.Count);
            Equal("globalValue", helperSnapshot.Globals[0].Name);
            Equal("2", helperSnapshot.Globals[0].DisplayValue);
            DdfEditorWatchResult localWatch = debugger.EvaluateWatch(
                "value + globalValue");
            Equal(true, localWatch.Available);
            Equal("6", localWatch.Value.DisplayValue);
            Equal("int", localWatch.Value.TypeName);
            DdfEditorWatchResult callerWatch = debugger.EvaluateWatch(
                "seed + globalValue", 1);
            Equal(true, callerWatch.Available);
            Equal("5", callerWatch.Value.DisplayValue);
            Equal(false, debugger.EvaluateWatch("value++").Available);
            Equal(false, debugger.EvaluateWatch("helper()").Available);
            Equal(false, debugger.EvaluateWatch("value", 9).Available);
            Equal(true, debugger.StepOut());

            Equal(true, pauseReached.Wait(TimeSpan.FromSeconds(5)));
            pausedLocation = pausedLocations.Last();
            Equal(main.Id, pausedLocation.DocumentId);
            Equal(5, pausedLocation.Line);
            Equal(true, debugger.Continue());
            Equal(false, debugger.EvaluateWatch("result").Available);

            DdfEditorExecutionCompletion completion = execution.GetAwaiter().GetResult();
            Equal(true, completion.Succeeded);
            Equal(6L, Convert.ToInt64(completion.ExecutionResult.ReturnValue));
            Equal(DdfEditorDebugState.Idle, debugger.State);
            Equal(null, debugger.CurrentLocation);
            Equal(null, debugger.CurrentSnapshot);
            Equal(true, ReferenceEquals(completion, debugger.LastCompletion));
            Equal(true, states.Contains(DdfEditorDebugState.Running));
            Equal(true, states.Contains(DdfEditorDebugState.Paused));
            Equal(4, pausedLocations.Count);
            Equal(DdfEditorDebugState.Idle, states.Last());

            var loopDocuments = new OpenDocumentCollection();
            IDdfEditorDocument loopDocument = loopDocuments.CreateVirtual("loop.ddf",
                "main() out int\n{\n    int value << 0;\n    while(true)\n    {\n        value++;\n    }\n    ret value;\n}");
            var pausedDebugger = new DdfEditorDebugService(loopDocuments,
                new DdfLanguagePackageSession());
            using var loopPauseReached = new ManualResetEventSlim(false);
            pausedDebugger.SetBreakpoints(new[]
                { new DdfEditorBreakpoint(loopDocument.Id, 6) });
            pausedDebugger.Paused += (sender, args) => loopPauseReached.Set();
            Task<DdfEditorExecutionCompletion> pausedExecution = pausedDebugger.RunAsync(
                new DdfEditorExecutionRequest(maxInstructions: int.MaxValue));
            Equal(true, loopPauseReached.Wait(TimeSpan.FromSeconds(5)));
            pausedDebugger.Dispose();
            DdfEditorExecutionCompletion cancelled = pausedExecution.GetAwaiter().GetResult();
            Equal(true, cancelled.WasCancelled);
        }

        private static void ExecutesPackageOperatorsThroughVmOperations()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-runtime-operations-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                KeepOnlyMinimalDeclarativePackageTest(root);
                string lexiconPath = Path.Combine(root, "lexicon.json");
                JsonObject lexicon = JsonNode.Parse(File.ReadAllText(lexiconPath, Encoding.UTF8)).AsObject();
                JsonObject assignment = lexicon["operators"].AsArray().Select(item => item.AsObject())
                    .Single(item => item["text"].GetValue<string>() == "<<");
                assignment["text"] = ":=";
                File.WriteAllText(lexiconPath, lexicon.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                string runtimePath = Path.Combine(root, "runtime.json");
                JsonObject runtime = JsonNode.Parse(File.ReadAllText(runtimePath, Encoding.UTF8)).AsObject();
                JsonObject runtimeAssignment = runtime["expressionOperations"].AsObject()["binary"].AsArray()
                    .Select(item => item.AsObject()).Single(item => item["operation"].GetValue<string>() == "assign");
                runtimeAssignment["token"] = ":=";
                File.WriteAllText(runtimePath, runtime.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(root);
                if (!load.Success) throw new InvalidOperationException(string.Join(" | ", load.Diagnostics));
                const string source = "main() out int { int value := 2; value := value + 3; ret value; }";
                DdfParseResult parse = DdfParser.Parse(source, load.Package.Language);
                Equal(0, parse.Diagnostics.Count);
                Equal(0, DdfTypeChecker.Check(source, parse.Root, null,
                    load.Package.Language.Types, load.Package.Language.StandardLibrary, null,
                    load.Package.Language.Runtime).Diagnostics.Count);
                DdfIrProgram ir = DdfIrLowerer.Lower(source, parse, load.Package.Language).Program;
                DdfIrFunction main = ir.Members.OfType<DdfIrFunction>().Single();
                DdfIrBinary operation = (DdfIrBinary)((DdfIrExpressionStatement)main.Body.Statements[1]).Expression;
                Equal(":=", operation.OperatorText);
                Equal(DdfVmExpressionOperations.Assign, operation.OperationId);
                DdfExecutionResult result = DdfVirtualMachine.Execute(source, ir,
                    load.Package.Language.Runtime, load.Package.Language.Types,
                    load.Package.Language.StandardLibrary);
                Equal(true, result.Succeeded);
                Equal(5L, Convert.ToInt64(result.ReturnValue));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void LoadsAndExecutesPackageDefinedStandardFunctions()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-standard-library-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                KeepOnlyMinimalDeclarativePackageTest(root);
                string libraryPath = Path.Combine(root, "standard-library.json");
                JsonObject library = JsonNode.Parse(File.ReadAllText(libraryPath, Encoding.UTF8)).AsObject();
                JsonObject print = library["functions"].AsArray().Select(item => item.AsObject())
                    .Single(item => item["binding"].GetValue<string>() == DdfHostFunctionBindings.Output);
                print["name"] = "emit";
                print["documentation"] = "Output definito dal pacchetto di test.";
                File.WriteAllText(libraryPath, library.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(root);
                Equal(true, load.Success);
                Equal(false, load.Package.Language.StandardLibrary.TryGetFunction("print", out _));
                Equal(true, load.Package.Language.StandardLibrary.TryGetFunction("emit", out DdfStandardFunction emit));
                Equal(DdfHostFunctionBindings.Output, emit.BindingId);
                const string source = "main() out int { emit(\"ready\"); ret 7; }";
                DdfParseResult parse = DdfParser.Parse(source, load.Package.Language);
                Equal(0, DdfTypeChecker.Check(source, parse.Root, null, load.Package.Language.Types,
                    load.Package.Language.StandardLibrary, null, load.Package.Language.Runtime).Diagnostics.Count);
                Equal(true, DdfCompletionService.GetCompletions("emi", 3, false,
                    load.Package.Language).Items.Any(item => item.DisplayText == "emit"));
                DdfIrProgram ir = DdfIrLowerer.Lower(source, parse, load.Package.Language).Program;
                var output = new List<string>();
                DdfExecutionResult result = DdfVirtualMachine.Execute(source, ir,
                    load.Package.Language.Runtime, load.Package.Language.Types,
                    load.Package.Language.StandardLibrary,
                    new DdfExecutionOptions { Output = output.Add });
                Equal(true, result.Succeeded);
                Equal("ready", output.Single());
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void EnforcesMinimumStandardLibraryContract()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            DdfStandardLibraryDefinition library = package.Language.StandardLibrary;
            Equal("ddf.standard", library.Id);
            Equal("0.12.3", library.Version);
            SequenceEqual(new[] { "length", "print", "readLine", "toFloat", "toInt" },
                library.Functions.Select(function => function.Name).OrderBy(name => name, StringComparer.Ordinal));
            Equal(DdfStandardFunctionEffect.Output, library.Functions.Single(item => item.Name == "print").Effect);
            Equal(DdfStandardFunctionEffect.Input, library.Functions.Single(item => item.Name == "readLine").Effect);
            Equal(DdfStandardFunctionEffect.Pure, library.Functions.Single(item => item.Name == "length").Effect);
            SequenceEqual(new[] { "DDF408" }, library.Functions.Single(item => item.Name == "toInt").ErrorCodes);
            Equal(0, library.Functions.Single(item => item.Name == "length").ErrorCodes.Count);

            DdfExecutionResult utf16 = DdfInterpreter.Execute(
                "main() out int { ret length(\"😀\"); }");
            Equal(true, utf16.Succeeded);
            Equal(2, utf16.ReturnValue);
            DdfExecutionResult unavailableInput = DdfInterpreter.Execute(
                "main() out string { ret readLine(); }");
            Equal(false, unavailableInput.Succeeded);
            Equal("DDF408", unavailableInput.Diagnostics.Single().Code);

            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string path = Path.Combine(root, "standard-library.json");
                JsonObject document = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)).AsObject();
                document["functions"].AsArray()[0].AsObject()["effect"] = "pure";
                File.WriteAllText(path,
                    document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
                DdfLanguagePackageLoadResult invalid = DdfLanguagePackageLoader.Load(root);
                Equal(false, invalid.Success);
                Equal(true, invalid.Diagnostics.Any(item => item.Code == "DDFL134"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void KeepsAutonomousSpecificationAligned()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            string specification = File.ReadAllText(
                Path.Combine(projectRoot, "DDF_LANGUAGE_SPEC.md"), Encoding.UTF8);
            const string startMarker = "<!-- ddf-canonical-grammar:start -->";
            const string endMarker = "<!-- ddf-canonical-grammar:end -->";
            int start = specification.IndexOf(startMarker, StringComparison.Ordinal);
            int end = specification.IndexOf(endMarker, StringComparison.Ordinal);
            if (start < 0 || end <= start)
                throw new InvalidOperationException("Blocco grammaticale canonico assente dalla specifica autonoma.");
            string block = specification.Substring(start + startMarker.Length,
                end - start - startMarker.Length);
            int fenceStart = block.IndexOf("```ebnf", StringComparison.Ordinal);
            int grammarStart = block.IndexOf('\n', fenceStart) + 1;
            int fenceEnd = block.IndexOf("```", grammarStart, StringComparison.Ordinal);
            if (fenceStart < 0 || grammarStart <= 0 || fenceEnd < grammarStart)
                throw new InvalidOperationException("Fence EBNF non valida nella specifica autonoma.");
            string documentedGrammar = block.Substring(grammarStart, fenceEnd - grammarStart);
            string executableGrammar = File.ReadAllText(
                Path.Combine(projectRoot, "language-packages", "ddf", "grammar.ebnf"), Encoding.UTF8);
            Equal(NormalizeSpecificationWhitespace(executableGrammar),
                NormalizeSpecificationWhitespace(documentedGrammar));

            JsonObject lexicon = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "language-packages", "ddf", "lexicon.json"), Encoding.UTF8)).AsObject();
            JsonObject types = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "language-packages", "ddf", "types.json"), Encoding.UTF8)).AsObject();
            JsonObject standard = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "language-packages", "ddf", "standard-library.json"), Encoding.UTF8)).AsObject();
            IEnumerable<string> terms = lexicon["keywords"].AsArray()
                .Select(item => item.AsObject()["text"].GetValue<string>())
                .Concat(lexicon["booleanLiterals"].AsArray().Select(item => item.GetValue<string>()))
                .Concat(lexicon["operators"].AsArray()
                    .Select(item => item.AsObject()["text"].GetValue<string>()))
                .Concat(types["primitives"].AsArray()
                    .Select(item => item.AsObject()["name"].GetValue<string>()))
                .Concat(standard["functions"].AsArray()
                    .Select(item => item.AsObject()["name"].GetValue<string>()))
                .Distinct(StringComparer.Ordinal);
            string[] missing = terms.Where(term =>
                specification.IndexOf("`" + term + "`", StringComparison.Ordinal) < 0 &&
                specification.IndexOf("`" + term + "(", StringComparison.Ordinal) < 0).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("Termini canonici assenti dalla specifica: " +
                    string.Join(", ", missing));
            if (!specification.Contains("Stato: **normativa**", StringComparison.Ordinal) ||
                !specification.Contains("Appendice A — Esempio completo non normativo", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "La specifica non distingue esplicitamente contratto normativo ed esempi.");

            string example = ExtractSpecificationFence(specification,
                "<!-- ddf-complete-example:start -->",
                "<!-- ddf-complete-example:end -->", "ddf");
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            DdfParseResult parse = DdfParser.Parse(example, package.Language);
            Equal(0, parse.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(example, parse.Root, null,
                package.Language.Types, package.Language.StandardLibrary, null,
                package.Language.Runtime).Diagnostics.Count);
            DdfExecutionResult execution = DdfInterpreter.Execute(example);
            Equal(true, execution.Succeeded);
            Equal(4, execution.ReturnValue);
        }

        private static string NormalizeSpecificationWhitespace(string value) =>
            Regex.Replace(value ?? string.Empty, "\\s+", " ").Trim();

        private static string ExtractSpecificationFence(string specification,
            string startMarker, string endMarker, string language)
        {
            int start = specification.IndexOf(startMarker, StringComparison.Ordinal);
            int end = specification.IndexOf(endMarker, StringComparison.Ordinal);
            if (start < 0 || end <= start)
                throw new InvalidOperationException("Blocco marcato assente dalla specifica autonoma.");
            string block = specification.Substring(start + startMarker.Length,
                end - start - startMarker.Length);
            string fence = "```" + language;
            int fenceStart = block.IndexOf(fence, StringComparison.Ordinal);
            int contentStart = block.IndexOf('\n', fenceStart) + 1;
            int fenceEnd = block.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (fenceStart < 0 || contentStart <= 0 || fenceEnd < contentStart)
                throw new InvalidOperationException("Fence " + language + " non valida nella specifica autonoma.");
            return block.Substring(contentStart, fenceEnd - contentStart).Trim();
        }

        private static void KeepsDdfVersionPolicyAligned()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-version-policy.json"), Encoding.UTF8)).AsObject();
            JsonObject package = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "language-packages", "ddf", "manifest.json"), Encoding.UTF8)).AsObject();
            JsonObject corpus = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "samples", "language-corpus", "corpus.json"), Encoding.UTF8)).AsObject();

            Equal("1.0", policy["schemaVersion"].GetValue<string>());
            Equal("ddf", policy["languageId"].GetValue<string>());
            Equal("beta", policy["stability"].GetValue<string>());
            Equal(package["id"].GetValue<string>(), policy["languageId"].GetValue<string>());
            Equal(package["languageVersion"].GetValue<string>(), policy["languageVersion"].GetValue<string>());
            Equal(package["packageVersion"].GetValue<string>(), policy["packageVersion"].GetValue<string>());
            Equal(corpus["languageVersion"].GetValue<string>(), policy["languageVersion"].GetValue<string>());
            Equal(corpus["languagePackageVersion"].GetValue<string>(), policy["packageVersion"].GetValue<string>());

            JsonArray domains = policy["versionDomains"].AsArray();
            string[] domainIds = domains.Select(item => item.AsObject()["id"].GetValue<string>()).ToArray();
            Equal(domainIds.Length, domainIds.Distinct(StringComparer.Ordinal).Count());
            SequenceEqual(new[]
            {
                "engineApi", "languageVersion", "moduleVersion", "packageVersion",
                "productVersion", "schemaVersion", "standardLibraryVersion"
            }, domainIds.OrderBy(id => id, StringComparer.Ordinal));

            JsonArray releases = policy["releaseRules"].AsArray();
            string[] changes = releases.Select(item => item.AsObject()["change"].GetValue<string>()).ToArray();
            Equal(changes.Length, changes.Distinct(StringComparer.Ordinal).Count());
            Equal(true, releases.Any(item => item.AsObject()["change"].GetValue<string>() ==
                "breakingLanguageContractAfterOne" && item.AsObject()["languageBump"].GetValue<string>() == "major"));

            JsonArray migrations = policy["migrations"].AsArray();
            string[] migrationIds = migrations.Select(item => item.AsObject()["id"].GetValue<string>()).ToArray();
            Equal(migrationIds.Length, migrationIds.Distinct(StringComparer.Ordinal).Count());
            Equal(true, migrations.Any(item => item.AsObject()["automation"].GetValue<string>() == "candidate"));
            Equal(true, migrations.Any(item => item.AsObject()["automation"].GetValue<string>() == "manual"));
            foreach (JsonNode node in migrations)
            {
                JsonObject migration = node.AsObject();
                Equal("specified-not-implemented", migration["availability"].GetValue<string>());
                Equal(true, migration["requiresPreview"].GetValue<bool>());
                Equal(true, migration["requiresBackup"].GetValue<bool>());
                Equal(migration["automation"].GetValue<string>() == "candidate"
                        ? "syntax-preserving" : "semantic-review",
                    migration["classification"].GetValue<string>());
            }

            JsonArray deprecations = policy["deprecations"].AsArray();
            Equal(true, deprecations.Any(item => item.AsObject()["id"].GetValue<string>() ==
                "legacy-library-directive" && !item.AsObject()["automaticMigration"].GetValue<bool>()));

            string migrationGuide = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_MIGRATION_0_12.md"), Encoding.UTF8);
            foreach (string migrationId in migrationIds)
                Equal(true, migrationGuide.Contains("`" + migrationId + "`", StringComparison.Ordinal));
            Equal(true, migrationGuide.Contains("## Percorso consigliato", StringComparison.Ordinal));
            Equal(true, migrationGuide.Contains("## Criteri di completamento", StringComparison.Ordinal));
        }

        private static void KeepsDdfCandidateBaselineFrozen()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            JsonObject baseline = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-candidate-baseline.json"), Encoding.UTF8)).AsObject();

            Equal("1.0", baseline["schemaVersion"].GetValue<string>());
            Equal("ddf-0.12-candidate-cycle-1", baseline["candidateId"].GetValue<string>());
            Equal("ddf", baseline["languageId"].GetValue<string>());
            Equal("0.12", baseline["languageVersion"].GetValue<string>());
            Equal("0.12.3", baseline["packageVersion"].GetValue<string>());
            Equal("frozen-evaluation", baseline["status"].GetValue<string>());
            Equal("sha256-utf8-lf", baseline["hashAlgorithm"].GetValue<string>());

            JsonArray artifacts = baseline["artifacts"].AsArray();
            Equal(17, artifacts.Count);
            string[] paths = artifacts.Select(item => item.AsObject()["path"].GetValue<string>()).ToArray();
            Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
            foreach (JsonNode node in artifacts)
            {
                JsonObject artifact = node.AsObject();
                string relativePath = artifact["path"].GetValue<string>();
                Equal(false, Path.IsPathRooted(relativePath));
                string fullPath = Path.GetFullPath(Path.Combine(projectRoot,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
                Equal(true, fullPath.StartsWith(projectRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
                Equal(true, File.Exists(fullPath));

                string normalized = File.ReadAllText(fullPath, Encoding.UTF8)
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace("\r", "\n", StringComparison.Ordinal);
                string actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
                    .ToLowerInvariant();
                Equal(artifact["sha256"].GetValue<string>(), actual);
            }

            string candidateGuide = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_CANDIDATE.md"), Encoding.UTF8);
            Equal(true, candidateGuide.Contains("## Regola di modifica", StringComparison.Ordinal));
            Equal(true, candidateGuide.Contains("## Registro dei problemi del candidato", StringComparison.Ordinal));
            Equal(true, candidateGuide.Contains("## Gate del ciclo", StringComparison.Ordinal));
            Equal(true, candidateGuide.Contains("stato pubblico `beta`", StringComparison.Ordinal));
        }

        private static void VerifiesDdfCandidateToolchainAgainstNormativeCorpus()
        {
            string corpus = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Corpus", "Language");
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(
                Path.Combine(corpus, "corpus.json"), Encoding.UTF8)).AsObject();
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;

            foreach (JsonNode node in manifest["valid"].AsArray())
            {
                JsonObject testCase = node.AsObject();
                string id = testCase["id"].GetValue<string>();
                string source = ReadCorpusSource(corpus, "valid", testCase);
                string expectedReturn = testCase["returnValue"].GetValue<string>();
                string[] expectedOutput = testCase["output"].AsArray()
                    .Select(item => item.GetValue<string>()).ToArray();
                int mainOffset = source.IndexOf("main()", StringComparison.Ordinal);
                Equal(true, mainOffset >= 0);
                int returnOffset = source.IndexOf("ret ", mainOffset, StringComparison.Ordinal);
                Equal(true, returnOffset >= 0);

                using (var analysis = new DdfEditorAnalysisService())
                {
                    var request = new DdfEditorAnalysisRequest(source, returnOffset,
                        1, package, 1, id, id + ".ddf", includeAllCompletions: true);
                    DdfEditorAnalysisSnapshot snapshot = analysis.Analyze(request);
                    Equal(0, snapshot.Diagnostics.Count);
                    Equal(true, snapshot.CompletionsComputed);
                    Equal(true, snapshot.Completions.Items.Count > 0);
                    Equal(true, snapshot.Symbols.Any(symbol => symbol.Name == "main"));
                }

                string formatted = DdfFormatter.Format(source, package.Language).Text;
                Equal(formatted, DdfFormatter.Format(formatted, package.Language).Text);
                DdfParseResult formattedParse = DdfParser.Parse(formatted, package.Language);
                Equal(0, formattedParse.ValidationDiagnostics.Count);
                Equal(0, DdfTypeChecker.Check(formatted, formattedParse.Root, null,
                    package.Language.Types, package.Language.StandardLibrary, null,
                    package.Language.Runtime).Diagnostics.Count);
                var formattedOutput = new List<string>();
                DdfExecutionResult formattedResult = DdfInterpreter.Execute(formatted,
                    new DdfExecutionOptions { Output = formattedOutput.Add });
                Equal(true, formattedResult.Succeeded);
                Equal(expectedReturn, Convert.ToString(formattedResult.ReturnValue,
                    CultureInfo.InvariantCulture));
                SequenceEqual(expectedOutput, formattedOutput);

                string root = CreateTemporaryDirectory();
                try
                {
                    string path = Path.Combine(root, "main.ddf");
                    File.WriteAllText(path, source, new UTF8Encoding(false));
                    DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                        "DDF candidate " + id, package, "main.ddf");
                    var build = new DdfBuildService();
                    DdfBuildResult first = build.Validate(root, project, package);
                    DdfBuildResult second = build.Validate(root, project, package);
                    Equal(true, first.Success);
                    Equal(DdfVmBuildBackend.BackendId, first.Artifact.Backend);
                    Equal(first.Artifact.Fingerprint, second.Artifact.Fingerprint);
                }
                finally
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }

                var documents = new OpenDocumentCollection();
                IDdfEditorDocument document = documents.CreateVirtual(id + ".ddf", source);
                using var debugger = new DdfEditorDebugService(documents,
                    new DdfLanguagePackageSession());
                using var pauseReached = new ManualResetEventSlim(false);
                int returnLine = source.Take(returnOffset).Count(character => character == '\n') + 1;
                debugger.SetBreakpoints(new[] { new DdfEditorBreakpoint(document.Id, returnLine) });
                debugger.Paused += (sender, args) => pauseReached.Set();
                Task<DdfEditorExecutionCompletion> execution = debugger.RunAsync(
                    new DdfEditorExecutionRequest(entryDocumentId: document.Id));
                Equal(true, pauseReached.Wait(TimeSpan.FromSeconds(5)));
                Equal(DdfEditorDebugState.Paused, debugger.State);
                Equal(document.Id, debugger.CurrentLocation.DocumentId);
                Equal(returnLine, debugger.CurrentLocation.Line);
                Equal(true, debugger.Continue());
                DdfEditorExecutionCompletion completion = execution.GetAwaiter().GetResult();
                Equal(true, completion.Succeeded);
                Equal(expectedReturn, Convert.ToString(completion.ExecutionResult.ReturnValue,
                    CultureInfo.InvariantCulture));
            }

            foreach (JsonNode node in manifest["invalid"].AsArray())
            {
                JsonObject testCase = node.AsObject();
                string source = ReadCorpusSource(corpus, "invalid", testCase);
                string stage = testCase["stage"].GetValue<string>();
                using var analysis = new DdfEditorAnalysisService();
                DdfEditorAnalysisSnapshot snapshot = analysis.Analyze(
                    new DdfEditorAnalysisRequest(source, source.Length, 1, package, 1,
                        computeCompletions: false));
                if (stage == "runtime")
                    Equal(0, snapshot.Diagnostics.Count);
                else
                    Equal(true, testCase["diagnostics"].AsArray()
                        .Select(item => item.GetValue<string>())
                        .All(code => snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == code)));
            }

            foreach (JsonNode node in manifest["incomplete"].AsArray())
            {
                JsonObject testCase = node.AsObject();
                string source = ReadCorpusSource(corpus, "incomplete", testCase);
                using var analysis = new DdfEditorAnalysisService();
                DdfEditorAnalysisSnapshot snapshot = analysis.Analyze(
                    new DdfEditorAnalysisRequest(source, source.Length, 1, package, 1,
                        includeAllCompletions: true));
                Equal(true, snapshot.Diagnostics.Count > 0);
                Equal(true, snapshot.CompletionsComputed);
                string formatted = DdfFormatter.Format(source, package.Language).Text;
                DdfParseResult recovered = DdfParser.Parse(formatted, package.Language);
                Equal(true, recovered.Root.Start >= 0 && recovered.Root.End <= formatted.Length);
            }
        }

        private static void RejectsUnsupportedVmAndHostOperationsAtomically()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-invalid-runtime-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string runtimePath = Path.Combine(root, "runtime.json");
                JsonObject runtime = JsonNode.Parse(File.ReadAllText(runtimePath, Encoding.UTF8)).AsObject();
                runtime["expressionOperations"].AsObject()["binary"].AsArray()[0].AsObject()["operation"] = "reflection.invoke";
                File.WriteAllText(runtimePath, runtime.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
                DdfLanguagePackageLoadResult invalidVm = DdfLanguagePackageLoader.Load(root);
                Equal(false, invalidVm.Success);
                Equal(true, invalidVm.Diagnostics.Any(item => item.Code == "DDFL122" &&
                    item.Domain == DdfDiagnosticDomain.EngineCapability));

                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string libraryPath = Path.Combine(root, "standard-library.json");
                JsonObject library = JsonNode.Parse(File.ReadAllText(libraryPath, Encoding.UTF8)).AsObject();
                library["functions"].AsArray()[0].AsObject()["binding"] = "host.reflection";
                File.WriteAllText(libraryPath, library.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
                DdfLanguagePackageLoadResult invalidHost = DdfLanguagePackageLoader.Load(root);
                Equal(false, invalidHost.Success);
                Equal(true, invalidHost.Diagnostics.Any(item => item.Code == "DDFL132" &&
                    item.Domain == DdfDiagnosticDomain.EngineCapability));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void LoadsProjectManifestIndependentlyFromLanguagePackage()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
                DdfProjectManifest manifest = DdfProjectManifest.CreateImplicit("Manifest test", package, "main.ddf");
                manifest.Module.Id = "example.application";
                manifest.Module.Version = "1.2.3";
                manifest.Module.Exports.Add("main");
                manifest.Dependencies.Add(new DdfProjectDependency { Id = "example.math", Version = "1.2.0" });
                File.WriteAllText(Path.Combine(root, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
                Equal(true, loaded.Success);
                Equal("Manifest test", loaded.Manifest.Name);
                Equal("example.application", loaded.Manifest.Module.Id);
                Equal("1.2.3", loaded.Manifest.Module.Version);
                SequenceEqual(new[] { "main" }, loaded.Manifest.Module.Exports);
                Equal(package.Id, loaded.Manifest.LanguageId);
                Equal(package.PackageVersion, loaded.Manifest.LanguagePackageVersion);
                Equal("main.ddf", loaded.Manifest.EntrySource);
                Equal(DdfVmBuildBackend.BackendId, loaded.Manifest.Backend);
                Equal("example.math", loaded.Manifest.Dependencies.Single().Id);
                Equal(true, typeof(IDdfBuildBackend).IsAssignableFrom(typeof(DdfVmBuildBackend)));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void BuildsReproducibleVmArtifactsFromUnsavedBuffers()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
                string path = Path.Combine(root, "main.ddf");
                const string diskSource = "main() out int { ret 1; }";
                const string bufferSource = "main() out int { ret 2; }";
                File.WriteAllText(path, diskSource, new UTF8Encoding(false));
                DdfProjectManifest manifest = DdfProjectManifest.CreateImplicit("Reproducible", package, "main.ddf");
                manifest.Module.Id = "example.reproducible";
                manifest.Module.Version = "2.1.0";
                manifest.Module.Exports.Add("main");
                var buffers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [path] = bufferSource };
                var service = new DdfBuildService();

                DdfBuildResult validation = service.Validate(root, manifest, package, buffers);
                Equal(true, validation.Success);
                Equal("example.reproducible", validation.Artifact.ModuleId);
                Equal("2.1.0", validation.Artifact.ModuleVersion);
                SequenceEqual(new[] { "main" }, validation.Artifact.Exports);
                Equal(false, Directory.Exists(Path.Combine(root, manifest.Output.Directory)));
                Equal(true, validation.Artifact.Sources.Single().FromUnsavedBuffer);
                DdfExecutionResult execution = DdfVirtualMachine.Execute(bufferSource, validation.Artifact.EntryProgram,
                    package.Language.Runtime, package.Language.Types, package.Language.StandardLibrary,
                    new DdfExecutionOptions { EntryPoint = validation.Artifact.EntryPoint, MaxInstructions = validation.Artifact.MaxInstructions });
                Equal(2L, Convert.ToInt64(execution.ReturnValue));

                DdfBuildResult first = service.Build(root, manifest, package, buffers);
                string firstReceipt = File.ReadAllText(first.ArtifactPath, Encoding.UTF8);
                DdfBuildResult second = service.Build(root, manifest, package, buffers);
                Equal(first.Artifact.Fingerprint, second.Artifact.Fingerprint);
                Equal(firstReceipt, File.ReadAllText(second.ArtifactPath, Encoding.UTF8));
                Equal(diskSource, File.ReadAllText(path, Encoding.UTF8));

                manifest.Module.Version = "2.1.1";
                DdfBuildResult changedModule = service.Validate(root, manifest, package, buffers);
                Equal(false, first.Artifact.Fingerprint == changedModule.Artifact.Fingerprint);
                manifest.Module.Version = "2.1.0";

                manifest.Options.UseUnsavedBuffers = false;
                DdfBuildResult diskBuild = service.Validate(root, manifest, package, buffers);
                Equal(false, diskBuild.Artifact.Sources.Single().FromUnsavedBuffer);
                Equal(false, first.Artifact.Fingerprint == diskBuild.Artifact.Fingerprint);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RejectsUnsafeOrMismatchedProjectConfigurations()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
                File.WriteAllText(Path.Combine(root, "main.ddf"), "main() out int { ret 0; }", new UTF8Encoding(false));
                DdfProjectManifest mismatch = DdfProjectManifest.CreateImplicit("Mismatch", package, "main.ddf");
                mismatch.LanguagePackageVersion = "0.0.0";
                DdfBuildResult result = new DdfBuildService().Validate(root, mismatch, package);
                Equal(false, result.Success);
                Equal(true, result.Issues.Any(item => item.Code == "DDFB003" &&
                    item.Domain == DdfDiagnosticDomain.LanguageDefinition));

                string duplicatePath = Path.Combine(root, "duplicate.ddf");
                File.WriteAllText(duplicatePath, "main() out int { ret 1; }", new UTF8Encoding(false));
                DdfProjectManifest duplicateEntry = DdfProjectManifest.CreateImplicit("Duplicate", package, null);
                DdfBuildResult duplicateResult = new DdfBuildService().Validate(root, duplicateEntry, package);
                Equal(false, duplicateResult.Success);
                Equal(true, duplicateResult.Issues.Any(item =>
                    item.Code == "DDF311" || item.Code == "DDFB009"));
                File.Delete(duplicatePath);

                string duplicateFunctionA = Path.Combine(root, "a.ddf");
                string duplicateFunctionB = Path.Combine(root, "b.ddf");
                File.WriteAllText(duplicateFunctionA, "helper() out int { ret 1; }", new UTF8Encoding(false));
                File.WriteAllText(duplicateFunctionB, "helper() out int { ret 2; }", new UTF8Encoding(false));
                DdfBuildResult duplicateFunctionResult = new DdfBuildService().Validate(root,
                    DdfProjectManifest.CreateImplicit("Duplicate function", package, "main.ddf"), package);
                Equal(false, duplicateFunctionResult.Success);
                Equal(true, duplicateFunctionResult.Issues.Any(item =>
                    item.Code == "DDF311" || item.Code == "DDFB010"));
                File.Delete(duplicateFunctionA);
                File.Delete(duplicateFunctionB);

                string earlyGlobal = Path.Combine(root, "a-global.ddf");
                string lateGlobal = Path.Combine(root, "z-global.ddf");
                File.WriteAllText(earlyGlobal, "int early << late;", new UTF8Encoding(false));
                File.WriteAllText(lateGlobal, "int late << 2;", new UTF8Encoding(false));
                DdfBuildResult globalOrderResult = new DdfBuildService().Validate(root,
                    DdfProjectManifest.CreateImplicit("Global order", package, "main.ddf"), package);
                Equal(false, globalOrderResult.Success);
                Equal(true, globalOrderResult.Issues.Any(item => item.Code == "DDFB011"));
                File.Delete(earlyGlobal);
                File.Delete(lateGlobal);

                DdfProjectManifest missingExport = DdfProjectManifest.CreateImplicit("Missing export", package, "main.ddf");
                missingExport.Module.Exports.Add("notDeclared");
                DdfBuildResult missingExportResult = new DdfBuildService().Validate(root, missingExport, package);
                Equal(false, missingExportResult.Success);
                Equal(true, missingExportResult.Issues.Any(item => item.Code == "DDFB012"));

                DdfProjectManifest invalidModule = DdfProjectManifest.CreateImplicit("Invalid module", package, "main.ddf");
                invalidModule.Module.Exports.Add("main");
                invalidModule.Module.Exports.Add("main");
                invalidModule.Dependencies.Add(new DdfProjectDependency
                {
                    Id = invalidModule.Module.Id,
                    Version = invalidModule.Module.Version
                });
                File.WriteAllText(Path.Combine(root, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(invalidModule), new UTF8Encoding(false));
                DdfProjectManifestLoadResult invalidModuleLoad = DdfProjectManifestLoader.Load(root);
                Equal(false, invalidModuleLoad.Success);
                Equal(true, invalidModuleLoad.Issues.Any(item => item.Code == "DDFP016"));
                Equal(true, invalidModuleLoad.Issues.Any(item => item.Code == "DDFP017"));

                invalidModule.Module = null;
                invalidModule.Dependencies.Clear();
                File.WriteAllText(Path.Combine(root, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(invalidModule), new UTF8Encoding(false));
                DdfProjectManifestLoadResult missingModuleLoad = DdfProjectManifestLoader.Load(root);
                Equal(false, missingModuleLoad.Success);
                Equal(true, missingModuleLoad.Issues.Any(item => item.Code == "DDFP015"));

                DdfProjectManifest unsafeManifest = DdfProjectManifest.CreateImplicit("Unsafe", package, "main.ddf");
                unsafeManifest.Output.Directory = "../outside";
                File.WriteAllText(Path.Combine(root, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(unsafeManifest), new UTF8Encoding(false));
                DdfProjectManifestLoadResult unsafeLoad = DdfProjectManifestLoader.Load(root);
                Equal(false, unsafeLoad.Success);
                Equal(true, unsafeLoad.Issues.Any(item => item.Code == "DDFP013"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void ResolvesVersionedModulesWithExportVisibilityAndCycleChecks()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
                string mainPath = Path.Combine(root, "main.ddf");
                File.WriteAllText(mainPath,
                    "main() out int { ret add(4, 5) + scale(3); }", new UTF8Encoding(false));
                DdfProjectManifest application = DdfProjectManifest.CreateImplicit("Module application", package, "main.ddf");
                application.Module.Id = "example.application";
                application.Module.Version = "1.0.0";
                application.Dependencies.Add(new DdfProjectDependency { Id = "example.math", Version = "1.0.0" });
                application.Dependencies.Add(new DdfProjectDependency { Id = "example.scale", Version = "1.0.0" });

                string mathRoot = Path.Combine(root, ".ddf", "modules", "example.math", "1.0.0");
                Directory.CreateDirectory(mathRoot);
                File.WriteAllText(Path.Combine(mathRoot, "math.ddf"),
                    "struct Box { int value; } int offset << 1; " +
                    "hidden() out int { Box box; box.value << offset; ret box.value; } " +
                    "add(int left, int right) out int { ret hidden() + left + right; }",
                    new UTF8Encoding(false));
                DdfProjectManifest math = DdfProjectManifest.CreateImplicit("Math", package, null);
                math.Module.Id = "example.math";
                math.Module.Version = "1.0.0";
                math.Module.Exports.Add("add");
                math.EntryPoint = "unused";
                File.WriteAllText(Path.Combine(mathRoot, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(math, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                string scaleRoot = Path.Combine(root, ".ddf", "modules", "example.scale", "1.0.0");
                Directory.CreateDirectory(scaleRoot);
                File.WriteAllText(Path.Combine(scaleRoot, "scale.ddf"),
                    "struct Box { int value; } int offset << 2; " +
                    "hidden() out int { Box box; box.value << offset; ret box.value; } " +
                    "scale(int value) out int { ret value * 2 + hidden(); }",
                    new UTF8Encoding(false));
                DdfProjectManifest scale = DdfProjectManifest.CreateImplicit("Scale", package, null);
                scale.Module.Id = "example.scale";
                scale.Module.Version = "1.0.0";
                scale.Module.Exports.Add("scale");
                scale.EntryPoint = "unused";
                File.WriteAllText(Path.Combine(scaleRoot, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(scale, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                var service = new DdfBuildService();
                DdfBuildResult valid = service.Validate(root, application, package);
                if (!valid.Success)
                    throw new InvalidOperationException(string.Join(" | ", valid.Issues.Select(issue => issue.ToString())));
                Equal(true, valid.Success);
                SequenceEqual(new[] { "example.math", "example.scale", "example.application" },
                    valid.Artifact.ResolvedModules.Select(item => item.Id));
                Equal(3, valid.Artifact.Sources.Count);
                Equal(true, valid.Artifact.Sources.Any(item =>
                    item.RelativePath == "@example.math/1.0.0/math.ddf"));
                DdfWorkspaceIndex workspace = DdfWorkspaceIndex.Load(root);
                Equal(1, workspace.Documents.Count);
                Equal("main.ddf", workspace.Documents.Single().RelativePath);
                DdfExecutionResult execution = DdfInterpreter.Execute(valid.Artifact.RuntimeModules,
                    package.Language, new DdfExecutionOptions { EntryPoint = valid.Artifact.EntryPoint });
                Equal(true, execution.Succeeded);
                Equal(18, execution.ReturnValue);

                string mathPath = Path.Combine(mathRoot, "math.ddf");
                File.WriteAllText(mathPath,
                    "struct Box { int value; } int offset << 2; " +
                    "hidden() out int { Box box; box.value << offset; ret box.value; } " +
                    "add(int left, int right) out int { ret hidden() + left + right; }",
                    new UTF8Encoding(false));
                DdfBuildResult changedDependency = service.Validate(root, application, package);
                Equal(true, changedDependency.Success);
                Equal(false, valid.Artifact.Fingerprint == changedDependency.Artifact.Fingerprint);
                File.WriteAllText(mathPath,
                    "struct Box { int value; } int offset << 1; " +
                    "hidden() out int { Box box; box.value << offset; ret box.value; } " +
                    "add(int left, int right) out int { ret hidden() + left + right; }",
                    new UTF8Encoding(false));

                File.WriteAllText(mainPath, "main() out int { ret hidden(); }", new UTF8Encoding(false));
                DdfBuildResult privateReference = service.Validate(root, application, package);
                Equal(false, privateReference.Success);
                Equal(true, privateReference.Issues.Any(item => item.Code == "DDF312"));

                File.WriteAllText(mainPath, "main() out int { ret add(4, 5) + scale(3); }", new UTF8Encoding(false));
                math.Dependencies.Add(new DdfProjectDependency
                {
                    Id = application.Module.Id,
                    Version = application.Module.Version
                });
                File.WriteAllText(Path.Combine(mathRoot, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(math), new UTF8Encoding(false));
                DdfBuildResult cycle = service.Validate(root, application, package);
                Equal(false, cycle.Success);
                Equal(true, cycle.Issues.Any(item => item.Code == "DDFB015"));

                math.Dependencies.Clear();
                File.WriteAllText(Path.Combine(mathRoot, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(math), new UTF8Encoding(false));
                application.Dependencies[0].Version = "9.0.0";
                DdfBuildResult missing = service.Validate(root, application, package);
                Equal(false, missing.Success);
                Equal(true, missing.Issues.Any(item => item.Code == "DDFB013"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void ValidatesShippedSampleProject()
        {
            string root = Path.Combine(Environment.CurrentDirectory, "samples", "project");
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            Equal(true, loaded.Success);
            DdfBuildResult result = new DdfBuildService().Validate(root, loaded.Manifest,
                DdfLanguagePackageLoader.LoadEmbeddedDefault().Package);
            Equal(true, result.Success);
            Equal("ddf.sample.project", result.Artifact.ModuleId);
            Equal("0.1.0", result.Artifact.ModuleVersion);
            Equal("main.ddf", result.Artifact.EntrySource);
            Equal(2, result.Artifact.Sources.Count);
            Equal(false, Directory.Exists(Path.Combine(root, ".ddf-build")));
            DdfExecutionResult execution = DdfInterpreter.Execute(result.Artifact.RuntimeModules,
                DdfLanguagePackageLoader.LoadEmbeddedDefault().Package.Language,
                new DdfExecutionOptions { EntryPoint = result.Artifact.EntryPoint });
            Equal(true, execution.Succeeded);
            Equal(2, execution.ReturnValue);
        }

        private static void ValidatesShippedFlowchartWorkspace()
        {
            string root = Path.Combine(Environment.CurrentDirectory, "samples", "flowchart-workspace");
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            Equal(true, loaded.Success);
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            DdfBuildResult result = new DdfBuildService().Validate(root, loaded.Manifest, package);
            if (!result.Success)
                throw new InvalidOperationException(string.Join(" | ", result.Issues.Select(
                    issue => issue.Code + ": " + issue.Message)));
            Equal(true, result.Success);
            Equal("ddf.sample.flowchart", result.Artifact.ModuleId);
            Equal("0.1.0", result.Artifact.ModuleVersion);
            Equal("main.ddf", result.Artifact.EntrySource);
            Equal(5, result.Artifact.Sources.Count);
            var output = new List<string>();
            DdfExecutionResult execution = DdfInterpreter.Execute(result.Artifact.RuntimeModules,
                package.Language, new DdfExecutionOptions
                {
                    EntryPoint = result.Artifact.EntryPoint,
                    Output = output.Add
                });
            Equal(true, execution.Succeeded);
            Equal(301, execution.ReturnValue);
            SequenceEqual(new[] { "Flowchart workspace completed" }, output);
        }

        private static void ValidatesShippedModuleWorkspace()
        {
            string root = Path.Combine(Environment.CurrentDirectory, "samples", "module-workspace");
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            Equal(true, loaded.Success);
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            DdfBuildResult result = new DdfBuildService().Validate(root, loaded.Manifest, package);
            if (!result.Success)
                throw new InvalidOperationException(string.Join(" | ", result.Issues.Select(
                    issue => issue.Code + ": " + issue.Message)));
            Equal(true, result.Success);
            SequenceEqual(new[] { "ddf.sample.math", "ddf.sample.scale", "ddf.sample.module-app" },
                result.Artifact.ResolvedModules.Select(item => item.Id));
            DdfExecutionResult execution = DdfInterpreter.Execute(result.Artifact.RuntimeModules,
                package.Language, new DdfExecutionOptions { EntryPoint = result.Artifact.EntryPoint });
            Equal(true, execution.Succeeded);
            Equal(42, execution.ReturnValue);
        }

        private static void ExposesModuleExportsToEditorAnalysis()
        {
            string root = Path.Combine(Environment.CurrentDirectory, "samples", "module-workspace");
            string path = Path.Combine(root, "main.ddf");
            string source = File.ReadAllText(path, Encoding.UTF8);
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            DdfModuleEditorIndex modules = DdfModuleEditorIndex.Load(root, package);

            Equal(false, modules.Issues.Any(issue => issue.Severity == DdfDiagnosticSeverity.Error));
            SequenceEqual(new[] { "add", "scale" }, modules.Documents
                .SelectMany(document => document.Symbols).Select(symbol => symbol.Name)
                .OrderBy(name => name, StringComparer.Ordinal));
            Equal(0, modules.FindDefinitions("hiddenOffset").Count);
            Equal(0, modules.FindDefinitions("Box").Count);

            using (var service = new DdfEditorAnalysisService())
            {
                var request = new DdfEditorAnalysisRequest(source,
                    source.IndexOf("add", StringComparison.Ordinal), 1, package, 1,
                    "main", path, true, modules.GetExternalRoots(path),
                    modules.GetExternalSymbols(path), modules.GetCompletionItems(path));
                DdfEditorAnalysisSnapshot snapshot = service.Analyze(request);
                Equal(false, snapshot.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "DDF201" || diagnostic.Code == "DDF312"));
                Equal(true, snapshot.Completions.Items.Any(item => item.DisplayText == "add"));
                Equal(true, snapshot.Completions.Items.Any(item => item.DisplayText == "scale"));
                Equal(false, snapshot.Completions.Items.Any(item => item.DisplayText == "hiddenOffset"));
            }
        }

        private static void ManagesVersionedModulePackagesSafely()
        {
            string moduleRoot = CreateTemporaryDirectory();
            string projectRoot = CreateTemporaryDirectory();
            string packageDirectory = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackage language = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
                File.WriteAllText(Path.Combine(moduleRoot, "math.ddf"),
                    "add(int left, int right) out int { ret left + right; }", new UTF8Encoding(false));
                DdfProjectManifest module = DdfProjectManifest.CreateImplicit("Math module", language, null);
                module.Module.Id = "example.packaged-math";
                module.Module.Version = "1.0.0";
                module.Module.Exports.Add("add");
                module.EntryPoint = "unused";
                File.WriteAllText(Path.Combine(moduleRoot, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(module, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                var store = new DdfModulePackageStore(projectRoot);
                string firstArchive = Path.Combine(packageDirectory, "math" + DdfModulePackageStore.PackageExtension);
                string secondArchive = Path.Combine(packageDirectory, "math-copy" + DdfModulePackageStore.PackageExtension);
                DdfModulePackageResult packed = store.Pack(moduleRoot, firstArchive);
                Equal(true, packed.Success);
                Equal("example.packaged-math", packed.Module.Id);
                Equal(true, File.Exists(firstArchive));
                Equal(true, store.Pack(moduleRoot, secondArchive).Success);
                Equal(true, File.ReadAllBytes(firstArchive).SequenceEqual(File.ReadAllBytes(secondArchive)));
                DdfModulePackageResult preview = store.Inspect(firstArchive);
                Equal(true, preview.Success);
                Equal("example.packaged-math", preview.Module.Id);
                SequenceEqual(new[] { "add" }, preview.Module.Exports);
                Equal(0, preview.Module.Dependencies.Count);
                Equal(0, store.List().Count);

                DdfModulePackageResult installed = store.Install(firstArchive);
                Equal(true, installed.Success);
                Equal(false, installed.Replaced);
                Equal(1, store.List().Count);
                Equal(true, File.Exists(Path.Combine(installed.Module.Path, "math.ddf")));
                Equal(installed.Module.Path, new DdfProjectModuleStoreResolver().ResolveModuleRoot(projectRoot,
                    new DdfProjectDependency { Id = module.Module.Id, Version = module.Module.Version }));

                File.WriteAllText(Path.Combine(projectRoot, "main.ddf"),
                    "main() out int { ret add(4, 5); }", new UTF8Encoding(false));
                DdfProjectManifest application = DdfProjectManifest.CreateImplicit("Package consumer", language, "main.ddf");
                application.Dependencies.Add(new DdfProjectDependency
                {
                    Id = module.Module.Id,
                    Version = module.Module.Version
                });
                DdfBuildResult build = new DdfBuildService().Validate(projectRoot, application, language);
                if (!build.Success)
                    throw new InvalidOperationException(string.Join(" | ", build.Issues.Select(item => item.ToString())));
                Equal(9, DdfInterpreter.Execute(build.Artifact.RuntimeModules, language.Language,
                    new DdfExecutionOptions { EntryPoint = build.Artifact.EntryPoint }).ReturnValue);

                DdfModulePackageResult duplicate = store.Install(firstArchive);
                Equal(false, duplicate.Success);
                Equal("DDFM006", duplicate.Diagnostics.Single().Code);
                DdfModulePackageResult replaced = store.Install(firstArchive, true);
                Equal(true, replaced.Success);
                Equal(true, replaced.Replaced);

                module.Module.Version = "2.0.0";
                File.WriteAllText(Path.Combine(moduleRoot, DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(module, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
                string nextArchive = Path.Combine(packageDirectory, "math-2" + DdfModulePackageStore.PackageExtension);
                Equal(true, store.Pack(moduleRoot, nextArchive).Success);
                Equal(true, store.Install(nextArchive).Success);
                Equal(2, store.List().Count);

                string malicious = Path.Combine(packageDirectory, "unsafe" + DdfModulePackageStore.PackageExtension);
                using (ZipArchive archive = ZipFile.Open(malicious, ZipArchiveMode.Create))
                {
                    using StreamWriter writer = new StreamWriter(archive.CreateEntry("../escape.ddf").Open());
                    writer.Write("unsafe");
                }
                DdfModulePackageResult rejected = store.Install(malicious);
                Equal(false, rejected.Success);
                Equal("DDFM007", rejected.Diagnostics.Single().Code);
                Equal(false, File.Exists(Path.Combine(projectRoot, ".ddf", "modules", "escape.ddf")));

                Equal(true, store.Remove(module.Module.Id, "1.0.0"));
                Equal(true, store.Remove(module.Module.Id, "2.0.0"));
                Equal(0, store.List().Count);
                Equal(false, store.Remove(module.Module.Id, module.Module.Version));
            }
            finally
            {
                if (Directory.Exists(moduleRoot)) Directory.Delete(moduleRoot, true);
                if (Directory.Exists(projectRoot)) Directory.Delete(projectRoot, true);
                if (Directory.Exists(packageDirectory)) Directory.Delete(packageDirectory, true);
            }
        }

        private static void InstallsAndUpdatesVersionedLanguagePackages()
        {
            string source = CreateTemporaryDirectory();
            string storeRoot = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(source);
                var store = new DdfLanguagePackageStore(storeRoot);
                DdfLanguagePackageInstallResult first = store.Install(source);
                Equal(true, first.Success);
                Equal(false, first.Updated);
                Equal(true, Directory.Exists(first.Installed.Path));
                Equal(1, store.List().Count);

                DdfLanguagePackageInstallResult update = store.Install(source);
                Equal(true, update.Success);
                Equal(true, update.Updated);
                Equal(first.Installed.Path, update.Installed.Path);
                Equal(1, store.List().Count);
                Equal(true, DdfLanguagePackageLoader.Load(update.Installed.Path).Success);
            }
            finally
            {
                if (Directory.Exists(source)) Directory.Delete(source, true);
                if (Directory.Exists(storeRoot)) Directory.Delete(storeRoot, true);
            }
        }

        private static void RejectsFailingDeclarativePackageTests()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string testsPath = Path.Combine(root, "tests.json");
                JsonObject tests = JsonNode.Parse(File.ReadAllText(testsPath, Encoding.UTF8)).AsObject();
                tests["cases"].AsArray()[0].AsObject()["expectedReturn"] = "999";
                File.WriteAllText(testsPath, tests.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
                DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.Load(root);
                Equal(false, result.Success);
                Equal(true, result.Diagnostics.Any(item => item.Code == "DDFT001"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void AcceptsExtensibleKeywordRolesAndElseBranches()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            DdfKeywordDefinition elseKeyword = package.Language.Keywords.Single(item => item.Text == "else");
            Equal("Else", elseKeyword.RoleId);
            Equal(DdfKeywordRole.None, elseKeyword.Role);

            const string source = "main() out int { if(false) { ret 1; } else { ret 2; } }";
            DdfParseResult parse = DdfParser.Parse(source, package.Language);
            Equal(0, parse.Diagnostics.Count);
            Equal(0, parse.GrammarDiagnostics.Count);
            var function = (FunctionDeclarationSyntax)parse.Root.Members.Single();
            Equal(true, ((IfStatementSyntax)function.Body.Statements.Single()).ElseBody != null);
            Equal(2L, Convert.ToInt64(DdfInterpreter.Execute(source, parse.Root).ReturnValue));
            string formatted = DdfFormatter.Format(source, package.Language).Text;
            Equal(0, DdfParser.Parse(formatted, package.Language).Diagnostics.Count);
            Equal(0, DdfParser.Parse(formatted, package.Language).GrammarDiagnostics.Count);
        }

        private static void RejectsInvalidEbnfGrammarsAtomically()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-invalid-grammar-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                File.WriteAllText(Path.Combine(root, "grammar.ebnf"),
                    "compilation-unit = compilation-unit, <Identifier> ;", new UTF8Encoding(false));
                DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.Load(root);
                Equal(false, result.Success);
                Equal(true, result.Diagnostics.Any(item => item.Code == "DDFL064" &&
                    item.Message.Contains("left recursion", StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void LoadsDirectoryAndArchiveLanguagePackages()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-language-package-" + Guid.NewGuid().ToString("N"));
            string directory = Path.Combine(root, "ddf");
            string archive = Path.Combine(root, "ddf.ddflang");
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(directory);
                DdfLanguagePackageLoadResult folderResult = DdfLanguagePackageLoader.Load(directory);
                Equal(true, folderResult.Success);
                Equal(DdfLanguagePackageOrigin.Directory, folderResult.Package.Origin);
                Equal(Path.GetFullPath(directory), folderResult.Package.Source);

                ZipFile.CreateFromDirectory(directory, archive);
                DdfLanguagePackageLoadResult archiveResult = DdfLanguagePackageLoader.Load(archive);
                Equal(true, archiveResult.Success);
                Equal(DdfLanguagePackageOrigin.Archive, archiveResult.Package.Origin);
                SequenceEqual(
                    folderResult.Package.Language.Keywords.Select(item => item.Text).OrderBy(item => item),
                    archiveResult.Package.Language.Keywords.Select(item => item.Text).OrderBy(item => item));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void SwitchesActiveLanguagePackagesAtomically()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-active-package-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string manifestPath = Path.Combine(root, "manifest.json");
                JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath, Encoding.UTF8)).AsObject();
                manifest["name"] = "DDF Test Active";
                File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                long initialRevision = DdfLanguageCatalog.Revision;
                DdfLanguagePackageLoadResult activated = DdfLanguageCatalog.TryActivate(root);
                Equal(true, activated.Success);
                Equal("DDF Test Active", DdfLanguageCatalog.DefaultPackage.Name);
                Equal(true, DdfLanguageCatalog.Revision > initialRevision);
                Equal(DdfLanguageCatalog.Revision, DdfLanguageCatalog.Capture().Revision);
                DdfLanguagePackage active = DdfLanguageCatalog.DefaultPackage;
                long activeRevision = DdfLanguageCatalog.Revision;

                DdfLanguagePackageLoadResult rejected = DdfLanguageCatalog.TryActivate(
                    Path.Combine(root, "missing"));
                Equal(false, rejected.Success);
                Equal(true, ReferenceEquals(active, DdfLanguageCatalog.DefaultPackage));
                Equal(activeRevision, DdfLanguageCatalog.Revision);
            }
            finally
            {
                DdfLanguageCatalog.ActivateEmbedded();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void IsolatesLanguagePackageSessionsPerHost()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-host-language-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string manifestPath = Path.Combine(root, "manifest.json");
                JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath, Encoding.UTF8)).AsObject();
                manifest["name"] = "DDF Host Isolato";
                File.WriteAllText(manifestPath,
                    manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                var first = new DdfLanguagePackageSession();
                var second = new DdfLanguagePackageSession();
                int changes = 0;
                first.ActiveLanguageChanged += (sender, args) => changes++;
                DdfLanguagePackage secondInitial = second.ActivePackage;
                long firstRevision = first.Revision;

                DdfLanguagePackageLoadResult activated = first.TryActivate(root);
                Equal(true, activated.Success);
                Equal("DDF Host Isolato", first.ActivePackage.Name);
                Equal(true, first.Revision > firstRevision);
                Equal(1, changes);
                Equal(true, ReferenceEquals(secondInitial, second.ActivePackage));
                Equal(1L, second.Revision);

                DdfLanguagePackage active = first.ActivePackage;
                long activeRevision = first.Revision;
                DdfLanguagePackageLoadResult rejected = first.TryActivate(Path.Combine(root, "missing"));
                Equal(false, rejected.Success);
                Equal(true, ReferenceEquals(active, first.ActivePackage));
                Equal(activeRevision, first.Revision);
                Equal(true, first.Diagnostics.Count > 0);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void DrivesLexicalServicesFromLanguagePackage()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-declarative-lexer-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                KeepOnlyMinimalDeclarativePackageTest(root);
                string lexiconPath = Path.Combine(root, "lexicon.json");
                JsonObject document = JsonNode.Parse(File.ReadAllText(lexiconPath, Encoding.UTF8)).AsObject();
                JsonObject lexical = document["lexical"].AsObject();
                JsonObject identifier = lexical["identifier"].AsObject();
                identifier["start"].AsObject()["characters"] = "_$";
                identifier["part"].AsObject()["characters"] = "_$";
                lexical["number"].AsObject()["decimalSeparator"] = ",";
                foreach (JsonNode node in lexical["delimitedTokens"].AsArray())
                {
                    JsonObject rule = node.AsObject();
                    switch (rule["tokenKind"].GetValue<string>())
                    {
                        case "LineComment": rule["start"] = "#"; break;
                        case "BlockComment": rule["start"] = "##"; rule["end"] = "##"; break;
                        case "LibraryDirective": rule["start"] = "use["; rule["end"] = "]"; break;
                        case "StringLiteral":
                            rule["start"] = "`";
                            rule["end"] = "`";
                            rule["escapeCharacter"] = "~";
                            rule["allowedEscapeCharacters"] = "`~nrt";
                            break;
                    }
                }
                foreach (JsonNode node in document["keywords"].AsArray())
                    if (node.AsObject()["text"].GetValue<string>() == "if") node.AsObject()["text"] = "$if";
                File.WriteAllText(lexiconPath, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(root);
                if (!load.Success)
                    throw new InvalidOperationException(string.Join(" | ", load.Diagnostics.Select(item => item.ToString())));
                DdfLanguageDefinition language = load.Package.Language;
                const string source = "$value 12,5 # note\n##block##\n`hello~`world` use[Console]";
                DdfLexResult lex = DdfLexer.Lex(source, language);
                Equal(0, lex.Diagnostics.Count);
                SequenceEqual(new[]
                {
                    DdfTokenKind.Identifier, DdfTokenKind.NumberLiteral, DdfTokenKind.LineComment,
                    DdfTokenKind.BlockComment, DdfTokenKind.StringLiteral, DdfTokenKind.LibraryDirective
                }, lex.Tokens.Select(item => item.Kind));
                SequenceEqual(new[] { SyntaxKind.Number, SyntaxKind.Comment, SyntaxKind.Comment, SyntaxKind.String, SyntaxKind.Library },
                    DdfSyntaxClassifier.Classify(source, language).Select(item => item.Kind));
                Equal(true, DdfSemanticModel.IsValidIdentifier("$value", language));
                Equal(false, DdfSemanticModel.IsValidIdentifier("value-with-dash", language));

                DdfCompletionResult keywordCompletion = DdfCompletionService.GetCompletions("$i", 2, false, language);
                Equal(true, keywordCompletion.Items.Any(item => item.DisplayText == "$if"));
                const string librarySource = "use[Console]\nuse[Con";
                DdfCompletionResult libraryCompletion = DdfCompletionService.GetCompletions(
                    librarySource, librarySource.Length, false, language);
                SequenceEqual(new[] { "Console" }, libraryCompletion.Items.Select(item => item.DisplayText));

                SequenceEqual(new[] { DdfTokenKind.NumberLiteral, DdfTokenKind.Punctuation, DdfTokenKind.NumberLiteral },
                    DdfLexer.Lex("12.5", language).Tokens.Select(item => item.Kind));
                DdfLexResult unterminated = DdfLexer.Lex("`open", language);
                Equal(DdfTokenKind.StringLiteral, unterminated.Tokens.Single().Kind);
                Equal("DDF001", unterminated.Diagnostics.Single().Code);

                var incremental = new IncrementalDdfLexer(language);
                incremental.Update("$name #");
                DdfLexResult incrementalResult = incremental.Update("$name ##block##").Result;
                DdfLexResult fullResult = DdfLexer.Lex("$name ##block##", language);
                SequenceEqual(fullResult.Tokens.Select(item => item.Kind + ":" + item.Start + ":" + item.Length),
                    incrementalResult.Tokens.Select(item => item.Kind + ":" + item.Start + ":" + item.Length));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void ExecutesSyntaxDistinctDemonstrationLanguage()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                KeepOnlyMinimalDeclarativePackageTest(root);
                string manifestPath = Path.Combine(root, "manifest.json");
                JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath, Encoding.UTF8)).AsObject();
                manifest["id"] = "nova-demo";
                manifest["name"] = "Nova Demo";
                manifest["fileExtensions"] = new JsonArray(".nova");
                File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                string lexiconPath = Path.Combine(root, "lexicon.json");
                JsonObject lexicon = JsonNode.Parse(File.ReadAllText(lexiconPath, Encoding.UTF8)).AsObject();
                foreach (JsonNode node in lexicon["keywords"].AsArray())
                    if (node.AsObject()["text"].GetValue<string>() == "ret") node.AsObject()["text"] = "yield";
                foreach (JsonNode node in lexicon["operators"].AsArray())
                    if (node.AsObject()["text"].GetValue<string>() == "<<") node.AsObject()["text"] = ":=";
                File.WriteAllText(lexiconPath, lexicon.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

                string grammarPath = Path.Combine(root, "grammar.ebnf");
                File.WriteAllText(grammarPath, File.ReadAllText(grammarPath, Encoding.UTF8)
                    .Replace("\"ret\"", "\"yield\"").Replace("\"<<\"", "\":=\""), new UTF8Encoding(false));
                string runtimePath = Path.Combine(root, "runtime.json");
                JsonObject runtime = JsonNode.Parse(File.ReadAllText(runtimePath, Encoding.UTF8)).AsObject();
                foreach (JsonNode node in runtime["expressionOperations"]["binary"].AsArray())
                    if (node.AsObject()["token"].GetValue<string>() == "<<") node.AsObject()["token"] = ":=";
                File.WriteAllText(runtimePath, runtime.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                string testsPath = Path.Combine(root, "tests.json");
                File.WriteAllText(testsPath, File.ReadAllText(testsPath, Encoding.UTF8).Replace("ret 2", "yield 2"), new UTF8Encoding(false));

                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(root);
                if (!load.Success) throw new InvalidOperationException(string.Join(" | ", load.Diagnostics));
                const string source = "main() out int { int value := 4; yield value; }";
                DdfParseResult parse = DdfParser.Parse(source, load.Package.Language);
                Equal(0, parse.Diagnostics.Count);
                DdfIrBuildResult ir = DdfIrLowerer.Lower(source, parse, load.Package.Language);
                if (!ir.Success) throw new InvalidOperationException(string.Join(" | ", ir.Diagnostics));
                DdfExecutionResult execution = DdfInterpreter.Execute(source, ir.Program, load.Package.Language);
                Equal(4L, Convert.ToInt64(execution.ReturnValue));
                Equal(true, DdfFormatter.Format(source, load.Package.Language).Text.Contains("yield value;"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void KeepsShippedSyntaxProfilesSemanticallyEquivalent()
        {
            string profiles = Path.Combine(AppContext.BaseDirectory, "LanguageProfiles");
            var cases = new[]
            {
                new
                {
                    Id = "ddf",
                    Source = "adjust(int value) out int { if(true && value < 5) { ret value + 2; } else { ret value; } } " +
                             "main() out int { int total << hostDouble(adjust(1)); int index << 0; while(index < 3) { total << total + 1; index++; } ret total; }"
                },
                new
                {
                    Id = "ddf-it-didactic",
                    Source = "adjust(int value) restituisce int { se(vero && value < 5) { restituisci value + 2; } altrimenti { restituisci value; } } " +
                             "main() restituisce int { int total << hostDouble(adjust(1)); int index << 0; mentre(index < 3) { total << total + 1; index++; } restituisci total; }"
                },
                new
                {
                    Id = "ddf-c-familiar",
                    Source = "adjust(int value) returns int { if(true && value < 5) { return value + 2; } else { return value; } } " +
                             "main() returns int { int total = hostDouble(adjust(1)); int index = 0; while(index < 3) { total = total + 1; index++; } return total; }"
                }
            };

            var hostCatalog = new DdfEditorHostCatalog();
            hostCatalog.RegisterFunction(new DdfHostFunctionDefinition("hostDouble", "int",
                new[] { new DdfHostParameterDefinition("value", "int") },
                arguments => Convert.ToInt32(arguments[0]) * 2));
            string expectedShape = null;
            string expectedClrMapping = null;
            foreach (var profile in cases)
            {
                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(Path.Combine(profiles, profile.Id));
                if (!load.Success) throw new InvalidOperationException(profile.Id + ": " + string.Join(" | ", load.Diagnostics));
                Equal(profile.Id, load.Package.Id);
                DdfParseResult parse = DdfParser.Parse(profile.Source, load.Package.Language);
                Equal(0, parse.ValidationDiagnostics.Count);
                string formatted = DdfFormatter.Format(profile.Source, load.Package.Language).Text;
                Equal(0, DdfParser.Parse(formatted, load.Package.Language).ValidationDiagnostics.Count);
                Equal(formatted, DdfFormatter.Format(formatted, load.Package.Language).Text);
                Equal(0, DdfTypeChecker.Check(profile.Source, parse.Root, null, load.Package.Language.Types,
                    load.Package.Language.StandardLibrary, hostCatalog.Snapshot, load.Package.Language.Runtime).Diagnostics.Count);
                DdfIrBuildResult ir = DdfIrLowerer.Lower(profile.Source, parse, load.Package.Language);
                if (!ir.Success) throw new InvalidOperationException(profile.Id + ": " + string.Join(" | ", ir.Diagnostics));
                DdfExecutionResult result = DdfInterpreter.Execute(profile.Source, ir.Program, load.Package.Language,
                    new DdfExecutionOptions { HostCatalog = hostCatalog.Snapshot });
                Equal(9L, Convert.ToInt64(result.ReturnValue));
                string shape = SemanticIrShape(ir.Program);
                if (expectedShape == null) expectedShape = shape;
                else Equal(expectedShape, shape);
                string clrMapping = load.Package.Language.Types.ClrMapping.Backend + "|" +
                    load.Package.Language.Types.ClrMapping.MappingVersion + "|" +
                    load.Package.Language.Types.ClrMapping.ArrayRepresentation + "|" +
                    load.Package.Language.Types.ClrMapping.StructureRepresentation + "|" +
                    string.Join(",", load.Package.Language.Types.ClrMapping.Primitives
                        .OrderBy(item => item.TypeName, StringComparer.Ordinal)
                        .Select(item => item.TypeName + "=" + item.ClrTypeName + "/" +
                            item.CSharpTypeName));
                if (expectedClrMapping == null) expectedClrMapping = clrMapping;
                else Equal(expectedClrMapping, clrMapping);
            }
        }

        private static string SemanticIrShape(DdfIrNode node)
        {
            if (node == null) return "null";
            if (node is DdfIrProgram program) return "program(" + string.Join(",", program.Members.Select(SemanticIrShape)) + ")";
            if (node is DdfIrFunction function) return "function:" + function.Name + "(" +
                string.Join(",", function.Parameters.Select(parameter => parameter.Type.Name)) + ")->" +
                function.ReturnType.Name + SemanticIrShape(function.Body);
            if (node is DdfIrBlock block) return "block(" + string.Join(",", block.Statements.Select(SemanticIrShape)) + ")";
            if (node is DdfIrVariableDeclaration variable) return "declare:" + variable.Type.Name + ":" + variable.Name + "=" + SemanticIrShape(variable.Initializer);
            if (node is DdfIrExpressionStatement statement) return "expression(" + SemanticIrShape(statement.Expression) + ")";
            if (node is DdfIrIf conditional) return "if(" + SemanticIrShape(conditional.Condition) + "," +
                SemanticIrShape(conditional.Body) + "," + SemanticIrShape(conditional.ElseBody) + ")";
            if (node is DdfIrWhile loop) return "while(" + SemanticIrShape(loop.Condition) + "," + SemanticIrShape(loop.Body) + ")";
            if (node is DdfIrReturn returned) return "return(" + SemanticIrShape(returned.Expression) + ")";
            if (node is DdfIrLiteral literal) return literal.Kind == DdfLiteralKind.Boolean
                ? "boolean:" + literal.BooleanValue
                : "literal:" + literal.Text;
            if (node is DdfIrName name) return "name:" + name.Name;
            if (node is DdfIrBinary binary) return "binary:" + binary.OperationId + "(" +
                SemanticIrShape(binary.Left) + "," + SemanticIrShape(binary.Right) + ")";
            if (node is DdfIrPostfix postfix) return "postfix:" + postfix.OperationId + "(" + SemanticIrShape(postfix.Operand) + ")";
            if (node is DdfIrCall call) return "call(" + SemanticIrShape(call.Target) + ":" +
                string.Join(",", call.Arguments.Select(SemanticIrShape)) + ")";
            if (node is DdfIrGroup group) return "group(" + SemanticIrShape(group.Expression) + ")";
            if (node is DdfIrUnary unary) return "unary:" + unary.OperationId + "(" + SemanticIrShape(unary.Operand) + ")";
            return node.GetType().Name;
        }

        private static void ConvertsEquivalentSyntaxProfilesSafely()
        {
            string profiles = Path.Combine(AppContext.BaseDirectory, "LanguageProfiles");
            DdfLanguagePackage canonical = DdfLanguagePackageLoader.Load(Path.Combine(profiles, "ddf")).Package;
            DdfLanguagePackage italian = DdfLanguagePackageLoader.Load(Path.Combine(profiles, "ddf-it-didactic")).Package;
            DdfLanguagePackage familiar = DdfLanguagePackageLoader.Load(Path.Combine(profiles, "ddf-c-familiar")).Package;
            const string source = "// if ret true <<\n" +
                "main() out bool { string text << \"if ret true <<\"; if(true) { ret false; } else { ret true; } }";

            DdfSyntaxProfileConversionResult italianResult = DdfSyntaxProfileConverter.Convert(
                source, canonical.Language, italian.Language);
            Equal("// if ret true <<\n" +
                "main() restituisce bool { string text << \"if ret true <<\"; se(vero) { restituisci falso; } altrimenti { restituisci vero; } }",
                italianResult.Text);
            Equal(8, italianResult.ReplacementCount);
            Equal(0, DdfParser.Parse(italianResult.Text, italian.Language).ValidationDiagnostics.Count);

            int identifier = source.IndexOf("text", StringComparison.Ordinal);
            int mappedIdentifier = italianResult.MapPosition(identifier);
            Equal("text", italianResult.Text.Substring(mappedIdentifier, 4));

            DdfSyntaxProfileConversionResult familiarResult = DdfSyntaxProfileConverter.Convert(
                source, canonical.Language, familiar.Language);
            Equal("// if ret true <<\n" +
                "main() returns bool { string text = \"if ret true <<\"; if(true) { return false; } else { return true; } }",
                familiarResult.Text);
            Equal(0, DdfParser.Parse(familiarResult.Text, familiar.Language).ValidationDiagnostics.Count);
            Equal(source, DdfSyntaxProfileConverter.Convert(
                familiarResult.Text, familiar.Language, canonical.Language).Text);
        }

        private static void DrivesPresentationFromLanguagePackages()
        {
            string profiles = Path.Combine(AppContext.BaseDirectory, "LanguageProfiles");
            DdfLanguagePackage italian = DdfLanguagePackageLoader.Load(
                Path.Combine(profiles, "ddf-it-didactic")).Package;
            DdfLanguagePackage familiar = DdfLanguagePackageLoader.Load(
                Path.Combine(profiles, "ddf-c-familiar")).Package;
            var diagnostic = new DdfDiagnostic("DDF201", "Simbolo non risolto: 'missing'.", 0, 7, 1, 1);

            DdfDiagnostic italianDiagnostic = italian.Language.Intellisense.Diagnostics.Apply(diagnostic);
            Equal("Simbolo non risolto: 'missing'.", italianDiagnostic.Message);
            Equal("Questo nome non è stato dichiarato: dichiaralo oppure correggine la grafia.",
                italianDiagnostic.DisplayMessage);
            Equal(true, italianDiagnostic.ToHoverText().Contains(italianDiagnostic.DisplayMessage,
                StringComparison.Ordinal));

            DdfDiagnostic familiarDiagnostic = familiar.Language.Intellisense.Diagnostics.Apply(diagnostic);
            Equal("Unresolved symbol.", familiarDiagnostic.DisplayMessage);
            Equal("Yes", familiar.Language.Intellisense.Flowchart.TrueLabel);
            Equal("No", familiar.Language.Intellisense.Flowchart.FalseLabel);

            const string source = "main() returns int { if(true) { return 1; } else { return 0; } }";
            DdfFlowchartBuildResult result = DdfFlowchartBuilder.Build(source, "main", familiar.Language);
            Equal(true, result.Success);
            Equal("End main", result.Flowchart.FindNode(result.Flowchart.ExitNodeId).Label);
            DdfFlowchartNode decision = result.Flowchart.Nodes.First(item =>
                item.Kind == DdfFlowchartNodeKind.Decision);
            string explanation = familiar.Language.Intellisense.Flowchart.Explain(decision);
            Equal(true, explanation.Contains("Yes", StringComparison.Ordinal));
            Equal(true, explanation.Contains("No", StringComparison.Ordinal));
        }

        private static void ValidatesNormativeSyntaxProfileCorpus()
        {
            string profiles = Path.Combine(AppContext.BaseDirectory, "LanguageProfiles");
            string corpus = Path.Combine(AppContext.BaseDirectory, "ProfileCorpus");
            var cases = new[]
            {
                new { Id = "ddf", Extension = ".ddf", Marker = "out", Legacy = "return" },
                new { Id = "ddf-it-didactic", Extension = ".ddfit", Marker = "restituisce", Legacy = "if" },
                new { Id = "ddf-c-familiar", Extension = ".ddfc", Marker = "returns", Legacy = "ret" }
            };

            foreach (var profile in cases)
            {
                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(Path.Combine(profiles, profile.Id));
                if (!load.Success) throw new InvalidOperationException(profile.Id + ": " + string.Join(" | ", load.Diagnostics));
                DdfLanguageDefinition language = load.Package.Language;
                foreach (DdfSnippetTemplate snippet in language.Intellisense.Snippets.Where(item => item.RequiresKeyword))
                    Equal(true, language.TryGetKeyword(snippet.Prefix, out _));
                Equal(1, language.Operators.Count(item => item.Role == DdfOperatorRole.DeclarationInitializer));
                string directory = Path.Combine(corpus, profile.Id);
                string valid = File.ReadAllText(Path.Combine(directory, "valid-core" + profile.Extension), Encoding.UTF8);
                DdfParseResult parse = DdfParser.Parse(valid, language);
                Equal(0, parse.ValidationDiagnostics.Count);
                Equal(0, DdfTypeChecker.Check(valid, parse.Root, null, language.Types,
                    language.StandardLibrary, null, language.Runtime).Diagnostics.Count);
                string formatted = DdfFormatter.Format(valid, language).Text;
                Equal(formatted, DdfFormatter.Format(formatted, language).Text);
                Equal(0, DdfParser.Parse(formatted, language).ValidationDiagnostics.Count);
                DdfIrBuildResult ir = DdfIrLowerer.Lower(valid, parse, language);
                Equal(true, ir.Success);
                Equal(4L, Convert.ToInt64(DdfInterpreter.Execute(valid, ir.Program, language).ReturnValue));

                foreach (string invalidName in new[] { "invalid-unbraced", "invalid-operator-position" })
                {
                    string invalid = File.ReadAllText(Path.Combine(directory, invalidName + profile.Extension), Encoding.UTF8);
                    Equal(true, DdfParser.Parse(invalid, language).ValidationDiagnostics.Any(item => item.Code == "DDFG101"));
                }

                string missingMarker = profile.Id == "ddf-it-didactic"
                    ? "main() int { restituisci 0; }"
                    : profile.Id == "ddf-c-familiar"
                        ? "main() int { return 0; }"
                        : "main() int { ret 0; }";
                DdfParseResult missingParse = DdfParser.Parse(missingMarker, language);
                DdfDiagnostic markerDiagnostic = missingParse.Diagnostics.First(item =>
                    item.Code == "DDF102" && item.Message.Contains(profile.Marker, StringComparison.Ordinal));
                DdfQuickFix markerFix = DdfQuickFixService.CreateDefault(language.Formatting)
                    .GetFixes(missingMarker, markerDiagnostic)
                    .Single(item => item.Replacement.Trim() == profile.Marker);
                string repaired = Apply(missingMarker, markerFix.ToEditorEdit());
                Equal(0, DdfParser.Parse(repaired, language).ValidationDiagnostics.Count);
                Equal(false, language.TryGetKeyword(profile.Legacy, out _));
            }

            DdfLanguageDefinition canonical = DdfLanguagePackageLoader.Load(Path.Combine(profiles, "ddf")).Package.Language;
            foreach (string removed in new[] { "!!", "&", "|&|", ">><<", "<<>>", "^/" })
                Equal(false, canonical.TryGetOperator(removed, out _));
        }

        private static void RejectsUnreachableLexicalRules()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-lexical-conflict-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string lexiconPath = Path.Combine(root, "lexicon.json");
                JsonObject document = JsonNode.Parse(File.ReadAllText(lexiconPath, Encoding.UTF8)).AsObject();
                JsonObject lexical = document["lexical"].AsObject();
                lexical["longestMatch"] = false;
                lexical["delimitedTokens"].AsArray()[0].AsObject()["start"] = "/";
                File.WriteAllText(lexiconPath, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));

                DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.Load(root);
                Equal(false, result.Success);
                Equal(true, result.Package == null);
                Equal(true, result.Diagnostics.Any(item => item.Code == "DDFL055" &&
                    item.JsonPath.StartsWith("$.lexical.delimitedTokens", StringComparison.Ordinal)));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RejectsInvalidLanguagePackagesAtomically()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-invalid-package-" + Guid.NewGuid().ToString("N"));
            try
            {
                string duplicateDirectory = Path.Combine(root, "duplicate");
                DdfLanguagePackageLoader.ExportEmbeddedDefault(duplicateDirectory);
                string lexiconPath = Path.Combine(duplicateDirectory, "lexicon.json");
                string lexicon = File.ReadAllText(lexiconPath, Encoding.UTF8)
                    .Replace("\"text\": \"float\"", "\"text\": \"int\"");
                File.WriteAllText(lexiconPath, lexicon, new UTF8Encoding(false));

                DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.Load(duplicateDirectory);
                Equal(false, result.Success);
                Equal(true, result.Package == null);
                Equal(true, result.HasErrors);
                DdfLanguagePackageDiagnostic diagnostic = result.Diagnostics.Single(item => item.Code == "DDFL040");
                Equal("lexicon.json", diagnostic.File);
                Equal("$.keywords", diagnostic.JsonPath);
                Equal(DdfDiagnosticDomain.LanguageDefinition, diagnostic.Domain);

                string incompatibleDirectory = Path.Combine(root, "incompatible");
                DdfLanguagePackageLoader.ExportEmbeddedDefault(incompatibleDirectory);
                string manifestPath = Path.Combine(incompatibleDirectory, "manifest.json");
                File.WriteAllText(manifestPath,
                    File.ReadAllText(manifestPath, Encoding.UTF8)
                        .Replace("\"engineApi\": \"1.0\"", "\"engineApi\": \"9.0\""),
                    new UTF8Encoding(false));
                result = DdfLanguagePackageLoader.Load(incompatibleDirectory);
                Equal(false, result.Success);
                Equal(true, result.Package == null);
                Equal(true, result.Diagnostics.Any(item => item.Code == "DDFL021" &&
                    item.Domain == DdfDiagnosticDomain.EngineCapability &&
                    item.JsonPath == "$.engineApi"));

                string unsafeDirectory = Path.Combine(root, "unsafe-reference");
                DdfLanguagePackageLoader.ExportEmbeddedDefault(unsafeDirectory);
                manifestPath = Path.Combine(unsafeDirectory, "manifest.json");
                File.WriteAllText(manifestPath,
                    File.ReadAllText(manifestPath, Encoding.UTF8)
                        .Replace("\"grammar\": \"grammar.json\"", "\"grammar\": \"../grammar.json\""),
                    new UTF8Encoding(false));
                result = DdfLanguagePackageLoader.Load(unsafeDirectory);
                Equal(false, result.Success);
                Equal(true, result.Package == null);
                Equal(true, result.Diagnostics.Any(item => item.Code == "DDFL027" &&
                    item.JsonPath == "$.sections.grammar"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void FallsBackToEmbeddedLanguagePackage()
        {
            string missing = Path.Combine(Path.GetTempPath(), "missing-ddf-package-" + Guid.NewGuid().ToString("N"));
            DdfLanguagePackageLoadResult result = DdfLanguagePackageLoader.LoadWithEmbeddedFallback(missing);
            Equal(true, result.Success);
            Equal(true, result.HasErrors);
            Equal(DdfLanguagePackageOrigin.Embedded, result.Package.Origin);
            Equal(true, result.Diagnostics.Any(item => item.Code == "DDFL002"));
            Equal(true, result.Diagnostics.Any(item => item.Code == "DDFL005" &&
                item.Severity == DdfLanguagePackageDiagnosticSeverity.Warning));
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

        private static void IndexesConfiguredWorkspaceExtensions()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-workspace-extension-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                const string source = "main() out int { ret 0; }";
                File.WriteAllText(Path.Combine(root, "included.demo"), source, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(root, "ignored.ddf"), source, new UTF8Encoding(false));

                DdfWorkspaceIndex index = DdfWorkspaceIndex.Load(
                    root, CancellationToken.None, new[] { ".demo" });
                Equal(1, index.Documents.Count);
                Equal("included.demo", index.Documents.Single().RelativePath);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
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

        private static void UpdatesWorkspaceSourceWithoutParsing()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ddf-workspace-source-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "main.ddf");
                DdfDocumentFile.Save(path, "oldName() out int { ret 0; }");
                DdfWorkspaceIndex original = DdfWorkspaceIndex.Load(directory);
                DdfWorkspaceIndex sourceOnly = original.WithDocumentSource(path, "newName() out int { ret 1; }");
                Equal(true, sourceOnly.Documents.Single().Source.Contains("newName"));
                Equal(1, sourceOnly.FindDefinitions("oldName").Count);
                Equal(0, sourceOnly.FindDefinitions("newName").Count);
                DdfWorkspaceIndex analyzed = sourceOnly.WithDocument(path, sourceOnly.Documents.Single().Source);
                Equal(0, analyzed.FindDefinitions("oldName").Count);
                Equal(1, analyzed.FindDefinitions("newName").Count);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void HonorsWorkspaceIndexingCancellation()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ddf-workspace-cancel-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                DdfDocumentFile.Save(Path.Combine(directory, "main.ddf"), "main() out int { ret 0; }");
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    Throws<OperationCanceledException>(() => DdfWorkspaceIndex.Load(directory, cancellation.Token));
                }
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

        private static void EnforcesDdf012CompoundTypeAndScopeRules()
        {
            DdfTypeSystemDefinition types = DdfTypeSystemDefinition.CreateDdfCompatibleDefault();
            Equal(false, types.IsKnownPrimitive("dict"));
            Equal(false, types.IsNumeric("char"));
            Equal(false, types.CanImplicitlyConvert("char", "float"));
            Equal(false, types.Scopes.AllowShadowing);

            const string arraysAndShadowing =
                "int value << 1; main() out int { int[2][3] matrix; int value << 2; ret value; }";
            DdfTypeCheckResult declarations = DdfTypeChecker.Check(arraysAndShadowing,
                DdfParser.Parse(arraysAndShadowing).Root);
            Equal(true, declarations.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF310"));
            Equal(true, declarations.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF311"));

            const string duplicateDeclarations =
                "int shared; int shared; helper() out int { ret 1; } helper() out int { ret 2; } " +
                "main() out int { int local; int local; ret 0; }";
            DdfTypeCheckResult duplicates = DdfTypeChecker.Check(duplicateDeclarations,
                DdfParser.Parse(duplicateDeclarations).Root);
            Equal(true, duplicates.Diagnostics.Count(diagnostic => diagnostic.Code == "DDF311") >= 5);

            const string recursiveStructures =
                "struct Node { Node child; } struct Left { Right[1] right; } struct Right { Left left; } " +
                "main() out int { ret 0; }";
            DdfTypeCheckResult containment = DdfTypeChecker.Check(recursiveStructures,
                DdfParser.Parse(recursiveStructures).Root);
            Equal(3, containment.Diagnostics.Count(diagnostic => diagnostic.Code == "DDF312"));

            const string invalidOperations =
                "same(int[2] left, int[2] right) out bool { ret left == right; } " +
                "main() out int { string text << \"abc\"; text[0] << \"z\"; while(true) { ret 1; } }";
            DdfTypeCheckResult operations = DdfTypeChecker.Check(invalidOperations,
                DdfParser.Parse(invalidOperations).Root);
            Equal(true, operations.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF302"));
            Equal(true, operations.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF308" &&
                diagnostic.Message.Contains("stringa", StringComparison.Ordinal)));
            Equal(true, operations.Diagnostics.Any(diagnostic => diagnostic.Code == "DDF307"));
        }

        private static void ValidatesNormativeDdfTypeCorpus()
        {
            string corpus = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Corpus", "Types");
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(
                Path.Combine(corpus, "corpus.json"), Encoding.UTF8)).AsObject();
            Equal("1.0", manifest["schemaVersion"].GetValue<string>());
            Equal("0.12", manifest["languageVersion"].GetValue<string>());
            Equal("ddf-vm-ir-v1", manifest["oracleBackend"].GetValue<string>());
            Equal("dotnet-roslyn-v1", manifest["futureBackend"].GetValue<string>());

            JsonArray cases = manifest["cases"].AsArray();
            string[] declaredFiles = cases.Select(item =>
                    item.AsObject()["file"].GetValue<string>())
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Equal(declaredFiles.Length, declaredFiles.Distinct(StringComparer.Ordinal).Count());
            SequenceEqual(Directory.GetFiles(corpus, "*.ddf").Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal), declaredFiles);

            foreach (JsonNode item in cases)
            {
                JsonObject testCase = item.AsObject();
                string fileName = testCase["file"].GetValue<string>();
                string source = File.ReadAllText(Path.Combine(corpus, fileName));
                DdfParseResult parse = DdfParser.Parse(source);
                Equal(0, parse.ValidationDiagnostics.Count);
                string[] expectedDiagnostics = testCase["semanticDiagnostics"].AsArray()
                    .Select(code => code.GetValue<string>())
                    .OrderBy(code => code, StringComparer.Ordinal).ToArray();
                string[] actual = DdfTypeChecker.Check(source, parse.Root).Diagnostics
                    .Select(diagnostic => diagnostic.Code)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToArray();
                SequenceEqual(expectedDiagnostics, actual);

                JsonObject runtime = testCase["runtime"] as JsonObject;
                if (runtime == null) continue;
                Equal(0, actual.Length);
                DdfExecutionResult result = DdfInterpreter.Execute(source);
                string status = runtime["status"].GetValue<string>();
                if (status == "success")
                {
                    Equal(true, result.Succeeded);
                    Equal(runtime["returnClrType"].GetValue<string>(),
                        result.ReturnValue?.GetType().FullName);
                    Equal(runtime["returnValue"].GetValue<string>(),
                        Convert.ToString(result.ReturnValue, CultureInfo.InvariantCulture));
                }
                else
                {
                    Equal(false, result.Succeeded);
                    DdfDiagnostic diagnostic = result.Diagnostics.Single();
                    Equal(runtime["diagnosticCode"].GetValue<string>(), diagnostic.Code);
                    Equal(runtime["line"].GetValue<int>(), diagnostic.Line);
                }
            }
        }

        private static void ValidatesCompleteNormativeDdfLanguageCorpus()
        {
            string corpus = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Corpus", "Language");
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(
                Path.Combine(corpus, "corpus.json"), Encoding.UTF8)).AsObject();
            Equal("1.0", manifest["schemaVersion"].GetValue<string>());
            Equal("ddf", manifest["languageId"].GetValue<string>());
            Equal("0.12", manifest["languageVersion"].GetValue<string>());
            Equal("0.12.3", manifest["languagePackageVersion"].GetValue<string>());
            Equal(DdfVmBuildBackend.BackendId, manifest["oracleBackend"].GetValue<string>());
            Equal("dotnet-roslyn-v1", manifest["futureBackend"].GetValue<string>());

            JsonArray valid = manifest["valid"].AsArray();
            JsonArray invalid = manifest["invalid"].AsArray();
            JsonArray incomplete = manifest["incomplete"].AsArray();
            JsonArray multiFile = manifest["multiFile"].AsArray();
            string[] ids = valid.Concat(invalid).Concat(incomplete).Concat(multiFile)
                .Select(item => item.AsObject()["id"].GetValue<string>()).ToArray();
            Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
            AssertCorpusDirectoryCoverage(corpus, "valid", valid);
            AssertCorpusDirectoryCoverage(corpus, "invalid", invalid);
            AssertCorpusDirectoryCoverage(corpus, "incomplete", incomplete);
            SequenceEqual(Directory.GetDirectories(Path.Combine(corpus, "multi-file"))
                    .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal),
                multiFile.Select(item => item.AsObject()["directory"].GetValue<string>())
                    .OrderBy(name => name, StringComparer.Ordinal));

            JsonArray packageCases = JsonNode.Parse(File.ReadAllText(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "LanguageProfiles", "ddf", "tests.json"),
                Encoding.UTF8)).AsObject()["cases"].AsArray();
            Dictionary<string, JsonObject> declarativeCorpus = packageCases
                .Select(item => item.AsObject())
                .Where(item => item["name"].GetValue<string>().StartsWith("corpus:", StringComparison.Ordinal))
                .ToDictionary(item => item["name"].GetValue<string>().Substring("corpus:".Length),
                    StringComparer.Ordinal);
            Equal(valid.Count, declarativeCorpus.Count);
            foreach (JsonNode node in valid)
            {
                JsonObject testCase = node.AsObject();
                string id = testCase["id"].GetValue<string>();
                Equal(true, declarativeCorpus.TryGetValue(id, out JsonObject declared));
                Equal(NormalizeCorpusSource(ReadCorpusSource(corpus, "valid", testCase)),
                    NormalizeCorpusSource(declared["source"].GetValue<string>()));
                Equal(testCase["returnValue"].GetValue<string>(),
                    declared["expectedReturn"].GetValue<string>());
                Equal(0, declared["expectedDiagnosticCodes"].AsArray().Count);
            }
            DdfLanguagePackageLoadResult packageLoad = DdfLanguagePackageLoader.LoadEmbeddedDefault();
            Equal(true, packageLoad.Success);
            DdfLanguagePackage package = packageLoad.Package;

            foreach (JsonNode node in valid)
            {
                JsonObject testCase = node.AsObject();
                string source = ReadCorpusSource(corpus, "valid", testCase);
                DdfParseResult parse = DdfParser.Parse(source);
                Equal(0, parse.ValidationDiagnostics.Count);
                Equal(0, DdfTypeChecker.Check(source, parse.Root).Diagnostics.Count);
                var output = new List<string>();
                DdfExecutionResult result = DdfInterpreter.Execute(source,
                    new DdfExecutionOptions { Output = output.Add });
                Equal(true, result.Succeeded);
                Equal(testCase["returnClrType"].GetValue<string>(), result.ReturnValue?.GetType().FullName);
                Equal(testCase["returnValue"].GetValue<string>(),
                    Convert.ToString(result.ReturnValue, CultureInfo.InvariantCulture));
                SequenceEqual(testCase["output"].AsArray().Select(item => item.GetValue<string>()), output);
            }

            foreach (JsonNode node in invalid)
            {
                JsonObject testCase = node.AsObject();
                string source = ReadCorpusSource(corpus, "invalid", testCase);
                string stage = testCase["stage"].GetValue<string>();
                DdfParseResult parse = DdfParser.Parse(source);
                IEnumerable<string> actual;
                if (stage == "lexical") actual = parse.LexicalDiagnostics.Select(item => item.Code);
                else if (stage == "syntax")
                {
                    Equal(0, parse.LexicalDiagnostics.Count);
                    actual = parse.SyntaxDiagnostics.Select(item => item.Code);
                }
                else
                {
                    Equal(0, parse.ValidationDiagnostics.Count);
                    DdfTypeCheckResult types = DdfTypeChecker.Check(source, parse.Root);
                    if (stage == "semantic") actual = types.Diagnostics.Select(item => item.Code);
                    else
                    {
                        Equal(0, types.Diagnostics.Count);
                        DdfExecutionResult result = DdfInterpreter.Execute(source);
                        Equal(false, result.Succeeded);
                        actual = result.Diagnostics.Select(item => item.Code);
                    }
                }
                SequenceEqual(testCase["diagnostics"].AsArray().Select(item => item.GetValue<string>())
                        .Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal),
                    actual.Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal));
            }

            foreach (JsonNode node in incomplete)
            {
                JsonObject testCase = node.AsObject();
                string source = ReadCorpusSource(corpus, "incomplete", testCase);
                DdfParseResult parse = DdfParser.Parse(source);
                Equal(true, parse.ValidationDiagnostics.Count >= testCase["minimumDiagnostics"].GetValue<int>());
                Equal(0, parse.Root.Start);
                Equal(true, parse.Root.End >= 0 && parse.Root.End <= source.Length);
                Equal(true, parse.Root.Members.All(member => member.Start >= 0 && member.End <= source.Length));
                DdfIrBuildResult lowered = DdfIrLowerer.Lower(source, parse);
                Equal(true, lowered.Diagnostics.Count > 0 || lowered.Program != null);
            }

            foreach (JsonNode node in multiFile)
            {
                JsonObject testCase = node.AsObject();
                string root = Path.Combine(corpus, "multi-file", testCase["directory"].GetValue<string>());
                Equal(true, File.Exists(Path.Combine(root, testCase["manifest"].GetValue<string>())));
                DdfProjectManifestLoadResult load = DdfProjectManifestLoader.Load(root);
                Equal(true, load.Success);
                DdfBuildResult build = new DdfBuildService().Validate(root, load.Manifest, package);
                if (testCase["status"].GetValue<string>() == "diagnostic")
                {
                    Equal(false, build.Success);
                    SequenceEqual(testCase["diagnostics"].AsArray().Select(item => item.GetValue<string>())
                            .OrderBy(code => code, StringComparer.Ordinal),
                        build.Issues.Where(item => item.Severity == DdfDiagnosticSeverity.Error)
                            .Select(item => item.Code).Distinct(StringComparer.Ordinal)
                            .OrderBy(code => code, StringComparer.Ordinal));
                    continue;
                }

                Equal(true, build.Success);
                var output = new List<string>();
                DdfExecutionResult result = DdfInterpreter.Execute(build.Artifact.RuntimeModules,
                    package.Language, new DdfExecutionOptions
                    {
                        EntryPoint = build.Artifact.EntryPoint,
                        MaxInstructions = build.Artifact.MaxInstructions,
                        Output = output.Add
                    });
                Equal(true, result.Succeeded);
                Equal(testCase["returnClrType"].GetValue<string>(), result.ReturnValue?.GetType().FullName);
                Equal(testCase["returnValue"].GetValue<string>(),
                    Convert.ToString(result.ReturnValue, CultureInfo.InvariantCulture));
                SequenceEqual(testCase["output"].AsArray().Select(item => item.GetValue<string>()), output);
            }
        }

        private static void AssertCorpusDirectoryCoverage(string corpus, string category, JsonArray cases)
        {
            string directory = Path.Combine(corpus, category);
            string[] declared = cases.Select(item => item.AsObject()["file"].GetValue<string>())
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Equal(declared.Length, declared.Distinct(StringComparer.Ordinal).Count());
            SequenceEqual(Directory.GetFiles(directory, "*.ddf").Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal), declared);
        }

        private static string ReadCorpusSource(string corpus, string category, JsonObject testCase) =>
            File.ReadAllText(Path.Combine(corpus, category, testCase["file"].GetValue<string>()), Encoding.UTF8);

        private static string NormalizeCorpusSource(string source) =>
            (source ?? string.Empty).Replace("\r\n", "\n").TrimEnd();

        private static void KeepOnlyMinimalDeclarativePackageTest(string packageRoot)
        {
            string path = Path.Combine(packageRoot, "tests.json");
            JsonObject document = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)).AsObject();
            JsonNode minimal = document["cases"].AsArray()[0].DeepClone();
            document["cases"] = new JsonArray(minimal);
            File.WriteAllText(path,
                document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
        }

        private static void EnforcesDdf012RuntimeNumericContract()
        {
            string corpus = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Corpus", "Types");

            DdfExecutionResult promotions = ExecuteTypeCorpus(corpus,
                "valid-runtime-promotions.ddf");
            Equal(true, promotions.Succeeded);
            Equal(true, promotions.ReturnValue is double);
            Equal(6d, promotions.ReturnValue);

            DdfExecutionResult integerDivision = ExecuteTypeCorpus(corpus,
                "valid-integer-division.ddf");
            Equal(true, integerDivision.Succeeded);
            Equal(3, integerDivision.ReturnValue);

            DdfExecutionResult underflow = DdfInterpreter.Execute(
                "main() out float { ret 0.1 ^ 10000; }");
            Equal(true, underflow.Succeeded);
            Equal(0d, underflow.ReturnValue);

            var failures = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["invalid-runtime-overflow.ddf"] = "DDF417",
                ["invalid-nonfinite-float.ddf"] = "DDF418",
                ["invalid-negative-integer-power.ddf"] = "DDF419"
            };
            foreach (KeyValuePair<string, string> item in failures)
            {
                string source = File.ReadAllText(Path.Combine(corpus, item.Key));
                DdfParseResult parse = DdfParser.Parse(source);
                Equal(0, parse.ValidationDiagnostics.Count);
                Equal(0, DdfTypeChecker.Check(source, parse.Root).Diagnostics.Count);
                DdfExecutionResult result = DdfInterpreter.Execute(source);
                Equal(false, result.Succeeded);
                Equal(item.Value, result.Diagnostics.Single().Code);
            }

            DdfExecutionResult postfixOverflow = DdfInterpreter.Execute(
                "main() out int { int value << 2147483647; value++; ret value; }");
            Equal("DDF417", postfixOverflow.Diagnostics.Single().Code);

            DdfExecutionResult unaryOverflow = DdfInterpreter.Execute(
                "main() out int { int value << -2147483647 - 1; ret -value; }");
            Equal("DDF417", unaryOverflow.Diagnostics.Single().Code);
        }

        private static DdfExecutionResult ExecuteTypeCorpus(string corpus, string fileName)
        {
            string source = File.ReadAllText(Path.Combine(corpus, fileName));
            DdfParseResult parse = DdfParser.Parse(source);
            Equal(0, parse.ValidationDiagnostics.Count);
            Equal(0, DdfTypeChecker.Check(source, parse.Root).Diagnostics.Count);
            return DdfInterpreter.Execute(source);
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

        private static void DisposesPausedDebuggerWithoutRacingWaiter()
        {
            const string source = "main() out int\n{\n    int value << 1;\n    value++;\n    ret value;\n}";
            using var pauseReached = new ManualResetEventSlim(false);
            var debugger = new DdfDebuggerSession();
            debugger.SetBreakpoints(new[] { 4 });
            debugger.Paused = info => pauseReached.Set();
            Task<DdfExecutionResult> execution = Task.Run(() =>
                DdfInterpreter.Execute(source,
                    new DdfExecutionOptions { DebuggerSession = debugger }));
            Equal(true, pauseReached.Wait(2000));
            debugger.Dispose();
            Equal(true, execution.Wait(2000));
            Equal(true, execution.Result.WasCancelled);
            Equal(0, execution.Result.Diagnostics.Count);
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
                Equal("1", pause.State.Variables.Single(item => item.Name == "value").DisplayValue);
                Equal("main", pause.State.CallStack.Single().FunctionName);

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

        private static void StepsThroughFunctionCalls()
        {
            const string source =
                "helper() out int\n{\nret 4;\n}\nmain() out int\n{\nint value << 0;\nvalue << helper();\nvalue++;\nret value;\n}";
            using (var stepIntoReached = new ManualResetEventSlim(false))
            using (var stepIntoDebugger = new DdfDebuggerSession())
            {
                DdfDebugPauseInfo stepIntoPause = null;
                stepIntoDebugger.SetBreakpoints(new[] { 8 });
                stepIntoDebugger.Paused = info => { stepIntoPause = info; stepIntoReached.Set(); };
                Task<DdfExecutionResult> stepIntoExecution = Task.Run(() => DdfInterpreter.Execute(source,
                    new DdfExecutionOptions { DebuggerSession = stepIntoDebugger }));

                Equal(true, stepIntoReached.Wait(2000));
                Equal(8, stepIntoPause.Line);
                stepIntoReached.Reset();
                stepIntoDebugger.StepInto();
                Equal(true, stepIntoReached.Wait(2000));
                Equal(3, stepIntoPause.Line);
                Equal("helper", stepIntoPause.State.CallStack[0].FunctionName);
                Equal("main", stepIntoPause.State.CallStack[1].FunctionName);
                stepIntoDebugger.Continue();
                Equal(true, stepIntoExecution.Wait(2000));
                Equal(5, stepIntoExecution.Result.ReturnValue);
            }

            using var pauseReached = new ManualResetEventSlim(false);
            using var debugger = new DdfDebuggerSession();
            DdfDebugPauseInfo pause = null;
            debugger.SetBreakpoints(new[] { 8 });
            debugger.Paused = info => { pause = info; pauseReached.Set(); };
            Task<DdfExecutionResult> execution = Task.Run(() => DdfInterpreter.Execute(source,
                new DdfExecutionOptions { DebuggerSession = debugger }));

            Equal(true, pauseReached.Wait(2000));
            Equal(8, pause.Line);
            pauseReached.Reset();
            debugger.StepOver();
            Equal(true, pauseReached.Wait(2000));
            Equal(9, pause.Line);
            Equal("4", DdfWatch.Evaluate("value", pause.State).Value.DisplayValue);
            debugger.Continue();
            Equal(true, execution.Wait(2000));
            Equal(5, execution.Result.ReturnValue);

            const string stepOutSource =
                "helper() out int\n{\nint value << 0;\ndo\n{\nvalue++;\n} while(value < 2);\nret value;\n}\nmain() out int\n{\nint result << helper();\nresult++;\nret result;\n}";
            using var stepOutReached = new ManualResetEventSlim(false);
            using var stepOutDebugger = new DdfDebuggerSession();
            DdfDebugPauseInfo stepOutPause = null;
            stepOutDebugger.SetBreakpoints(new[] { 6 });
            stepOutDebugger.Paused = info => { stepOutPause = info; stepOutReached.Set(); };
            Task<DdfExecutionResult> stepOutExecution = Task.Run(() => DdfInterpreter.Execute(stepOutSource,
                new DdfExecutionOptions { DebuggerSession = stepOutDebugger }));

            Equal(true, stepOutReached.Wait(2000));
            Equal(6, stepOutPause.Line);
            stepOutReached.Reset();
            stepOutDebugger.StepOut();
            Equal(true, stepOutReached.Wait(2000));
            Equal(13, stepOutPause.Line);
            Equal("main", stepOutPause.State.CallStack[0].FunctionName);
            stepOutDebugger.Continue();
            Equal(true, stepOutExecution.Wait(2000));
            Equal(3, stepOutExecution.Result.ReturnValue);
        }

        private static void ExecutesAndDebugsProjectFiles()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                string mainPath = Path.Combine(root, "main.ddf");
                string helperPath = Path.Combine(root, "helper.ddf");
                string typesPath = Path.Combine(root, "types.ddf");
                const string mainSource = "main() out int\n{\n    ret helper(4);\n}";
                const string helperDiskSource = "helper(int value) out int\n{\n    ret value;\n}";
                const string helperBufferSource = "helper(int value) out int\n{\n    Point point;\n    point.x << value + offset;\n    ret point.x;\n}";
                const string typesSource = "struct Point\n{\n    int x;\n}\nint offset << 1;";
                File.WriteAllText(mainPath, mainSource, new UTF8Encoding(false));
                File.WriteAllText(helperPath, helperDiskSource, new UTF8Encoding(false));
                File.WriteAllText(typesPath, typesSource, new UTF8Encoding(false));
                DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
                DdfProjectManifest manifest = DdfProjectManifest.CreateImplicit("Multi file", package, "main.ddf");
                var buffers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { [helperPath] = helperBufferSource };
                DdfBuildResult build = new DdfBuildService().Validate(root, manifest, package, buffers);

                Equal(true, build.Success);
                Equal(3, build.Artifact.RuntimeModules.Count);
                Equal(true, build.Artifact.Sources.Single(item => item.RelativePath == "helper.ddf").FromUnsavedBuffer);
                DdfExecutionResult execution = DdfInterpreter.Execute(build.Artifact.RuntimeModules,
                    package.Language, new DdfExecutionOptions { EntryPoint = "main" });
                Equal(true, execution.Succeeded);
                Equal(5, execution.ReturnValue);

                using (var stepReached = new ManualResetEventSlim(false))
                using (var stepDebugger = new DdfDebuggerSession())
                {
                    DdfDebugPauseInfo stepPause = null;
                    stepDebugger.SetBreakpoints(new[] { new DdfSourceBreakpoint(mainPath, 3) });
                    stepDebugger.Paused = info => { stepPause = info; stepReached.Set(); };
                    Task<DdfExecutionResult> stepExecution = Task.Run(() => DdfInterpreter.Execute(
                        build.Artifact.RuntimeModules, package.Language,
                        new DdfExecutionOptions { EntryPoint = "main", DebuggerSession = stepDebugger }));
                    Equal(true, stepReached.Wait(2000));
                    Equal(Path.GetFullPath(mainPath), Path.GetFullPath(stepPause.DocumentPath));
                    Equal(3, stepPause.Line);

                    stepReached.Reset();
                    stepDebugger.StepInto();
                    Equal(true, stepReached.Wait(2000));
                    Equal(Path.GetFullPath(helperPath), Path.GetFullPath(stepPause.DocumentPath));
                    Equal(3, stepPause.Line);

                    stepReached.Reset();
                    stepDebugger.StepOver();
                    Equal(true, stepReached.Wait(2000));
                    Equal(Path.GetFullPath(helperPath), Path.GetFullPath(stepPause.DocumentPath));
                    Equal(4, stepPause.Line);
                    stepDebugger.Continue();
                    Equal(true, stepExecution.Wait(2000));
                    Equal(5, stepExecution.Result.ReturnValue);
                }

                using var pauseReached = new ManualResetEventSlim(false);
                using var debugger = new DdfDebuggerSession();
                DdfDebugPauseInfo pause = null;
                debugger.SetBreakpoints(new[] { new DdfSourceBreakpoint(helperPath, 4) });
                debugger.Paused = info => { pause = info; pauseReached.Set(); };
                Task<DdfExecutionResult> debugExecution = Task.Run(() => DdfInterpreter.Execute(
                    build.Artifact.RuntimeModules, package.Language,
                    new DdfExecutionOptions { EntryPoint = "main", DebuggerSession = debugger }));
                Equal(true, pauseReached.Wait(2000));
                Equal(4, pause.Line);
                Equal(Path.GetFullPath(helperPath), Path.GetFullPath(pause.DocumentPath));
                Equal("helper", pause.State.CallStack[0].FunctionName);
                Equal(Path.GetFullPath(helperPath), Path.GetFullPath(pause.State.CallStack[0].DocumentPath));
                Equal("main", pause.State.CallStack[1].FunctionName);
                Equal(Path.GetFullPath(mainPath), Path.GetFullPath(pause.State.CallStack[1].DocumentPath));
                debugger.Continue();
                Equal(true, debugExecution.Wait(2000));
                Equal(5, debugExecution.Result.ReturnValue);

                buffers[helperPath] = "helper(int value) out int\n{\n    int zero << 0;\n    ret value / zero;\n}";
                DdfBuildResult failingBuild = new DdfBuildService().Validate(root, manifest, package, buffers);
                Equal(true, failingBuild.Success);
                DdfExecutionResult failure = DdfInterpreter.Execute(failingBuild.Artifact.RuntimeModules,
                    package.Language, new DdfExecutionOptions { EntryPoint = "main" });
                Equal(false, failure.Succeeded);
                DdfDiagnostic runtimeDiagnostic = failure.Diagnostics.Single(item => item.Code == "DDF404");
                Equal(Path.GetFullPath(helperPath), Path.GetFullPath(runtimeDiagnostic.DocumentPath));
                Equal(true, failure.StackTrace.Any(frame =>
                    string.Equals(frame.DocumentPath, mainPath, StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void EvaluatesWatches()
        {
            const string source = "struct Point { int x; }\nmain() out int\n{\nint[2] values;\nPoint point;\nvalues[0] << 4;\npoint.x << 7;\nvalues[0] << 9;\nret point.x;\n}";
            var snapshots = new List<DdfRuntimeStateSnapshot>();
            using var debugger = new DdfDebuggerSession();
            debugger.SetBreakpoints(new[] { 8, 9 });
            debugger.Paused = pause => { snapshots.Add(pause.State); debugger.Continue(); };
            var execution = DdfInterpreter.Execute(source, new DdfExecutionOptions { DebuggerSession = debugger });
            Equal(true, execution.Succeeded);
            Equal(2, snapshots.Count);
            Equal("4", DdfWatch.Evaluate("values[0]", snapshots[0]).Value.DisplayValue);
            Equal("9", DdfWatch.Evaluate("values [ 00 ]", snapshots[1]).Value.DisplayValue);
            Equal("7", DdfWatch.Evaluate("point.x", snapshots[0]).Value.DisplayValue);
            Equal("int", DdfWatch.Evaluate("point.x", snapshots[0]).Value.TypeName);
            Equal("5", DdfWatch.Evaluate("values[0] + 1", snapshots[0]).Value.DisplayValue);
            Equal("18", DdfWatch.Evaluate("(point.x * 2) + values[0]", snapshots[0]).Value.DisplayValue);
            Equal("4", DdfWatch.Evaluate("values[values[1]]", snapshots[0]).Value.DisplayValue);
            Equal("true", DdfWatch.Evaluate("point.x > values[0] && true", snapshots[0]).Value.DisplayValue);
            Equal("x7", DdfWatch.Evaluate("\"x\" + point.x", snapshots[0]).Value.DisplayValue);
            Equal("-7", DdfWatch.Evaluate("-point.x", snapshots[0]).Value.DisplayValue);
            DdfRuntimeVariableSnapshot arraySnapshot = snapshots[0].Variables.Single(item => item.Name == "values");
            Equal("int[]", arraySnapshot.TypeName);
            Equal(DdfRuntimeVariableKind.Element, arraySnapshot.Children[0].Kind);
            DdfRuntimeVariableSnapshot structureSnapshot = snapshots[0].Variables.Single(item => item.Name == "point");
            Equal("Point", structureSnapshot.TypeName);
            Equal(DdfRuntimeVariableKind.Field, structureSnapshot.Children[0].Kind);
            foreach (string path in new[] { "values[9]", "point.missing", "missing", "point.x++", "print()", "true || print()", "point.x << 2", "point x", "point.x / 0" })
                Equal(false, DdfWatch.Evaluate(path, snapshots[0]).Available);
            Equal("7", DdfWatch.Evaluate("point.x", snapshots[0]).Value.DisplayValue);
            Equal(false, DdfWatch.Evaluate("point", null).Available);

            const string loop = "main() out int\n{\nint value << 0;\nint[2] values;\nwhile(value < 3)\n{\nvalues[0] << value;\nvalue++;\n}\nret value;\n}";
            var loopValues = new List<string>();
            var loopArrayValues = new List<string>();
            debugger.SetBreakpoints(new[] { 8 });
            debugger.Paused = pause =>
            {
                loopValues.Add(DdfWatch.Evaluate("value", pause.State).Value.DisplayValue);
                loopArrayValues.Add(DdfWatch.Evaluate("values[0]", pause.State).Value.DisplayValue);
                Equal("0", DdfWatch.Evaluate("values[1]", pause.State).Value.DisplayValue);
                debugger.Continue();
            };
            Equal(true, DdfInterpreter.Execute(loop, new DdfExecutionOptions { DebuggerSession = debugger }).Succeeded);
            SequenceEqual(new[] { "0", "1", "2" }, loopValues);
            SequenceEqual(new[] { "0", "1", "2" }, loopArrayValues);

            const string nested = "helper() out int\n{\nint value << 8;\nret value;\n}\nmain() out int\n{\nint callerOnly << 42;\nint value << 3;\nret helper();\n}";
            DdfRuntimeStateSnapshot helperState = null;
            debugger.SetBreakpoints(new[] { 4 });
            debugger.Paused = pause => { helperState = pause.State; debugger.Continue(); };
            const string nestedPath = "C:\\workspace\\nested.ddf";
            Equal(true, DdfInterpreter.Execute(nested, new DdfExecutionOptions
                { DebuggerSession = debugger, SourcePath = nestedPath }).Succeeded);
            Equal("8", DdfWatch.Evaluate("value", helperState).Value.DisplayValue);
            Equal(false, DdfWatch.Evaluate("callerOnly", helperState).Available);
            Equal(2, helperState.CallStack.Count);
            Equal("helper", helperState.CallStack[0].FunctionName);
            Equal("main", helperState.CallStack[1].FunctionName);
            Equal(4, helperState.CallStack[0].Line);
            Equal(10, helperState.CallStack[1].Line);
            Equal(nestedPath, helperState.CallStack[0].DocumentPath);
            Equal("value", helperState.Frames[0].Variables.Single().Name);
            Equal(true, helperState.Frames[1].Variables.Any(item => item.Name == "callerOnly"));
            Equal(false, helperState.Frames[0].Variables.Any(item => item.Name == "callerOnly"));

            const string categorized = "int globalValue << 5;\nhelper(int parameter) out int\n{\nint localValue << 8;\nret localValue + parameter + globalValue;\n}\nmain() out int { ret helper(2); }";
            DdfRuntimeStateSnapshot categorizedState = null;
            debugger.SetBreakpoints(new[] { 5 });
            debugger.Paused = pause => { categorizedState = pause.State; debugger.Continue(); };
            Equal(true, DdfInterpreter.Execute(categorized, new DdfExecutionOptions { DebuggerSession = debugger }).Succeeded);
            Equal(DdfRuntimeVariableKind.Global,
                categorizedState.Variables.Single(item => item.Name == "globalValue").Kind);
            Equal(DdfRuntimeVariableKind.Parameter,
                categorizedState.Variables.Single(item => item.Name == "parameter").Kind);
            Equal(DdfRuntimeVariableKind.Local,
                categorizedState.Variables.Single(item => item.Name == "localValue").Kind);
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

        private sealed class TrackingHostResource : IDisposable
        {
            private readonly string name;
            private readonly ICollection<string> releases;

            public TrackingHostResource(string name, ICollection<string> releases)
            {
                this.name = name;
                this.releases = releases;
            }

            public void Dispose() => releases.Add(name);
        }

        private sealed class TrackingAsyncHostResource : IAsyncDisposable
        {
            private readonly string name;
            private readonly ICollection<string> releases;

            public TrackingAsyncHostResource(string name, ICollection<string> releases)
            {
                this.name = name;
                this.releases = releases;
            }

            public ValueTask DisposeAsync()
            {
                releases.Add(name);
                return ValueTask.CompletedTask;
            }
        }

        private sealed class FakeDatabaseService : IDdfHostDatabaseService
        {
            public List<DdfHostDatabaseRequest> Requests { get; } =
                new List<DdfHostDatabaseRequest>();
            public string CustomerName { get; private set; } = "Ada";

            public async ValueTask<object> ExecuteAsync(
                DdfHostDatabaseRequest request)
            {
                Requests.Add(request);
                if (request.StatementId == "customer.slow")
                {
                    await Task.Delay(500, request.Invocation.CancellationToken);
                    return Customer(7);
                }
                if (request.StatementId == "customer.byId")
                    return Customer(Convert.ToInt32(
                        request.Parameters["customerId"]));
                if (request.StatementId == "customer.updateName")
                {
                    if (request.Invocation.Mode == DdfEditorExecutionMode.HostEffects)
                        CustomerName = Convert.ToString(request.Parameters["name"]);
                    return 1;
                }
                throw new InvalidOperationException(
                    "Statement database non riconosciuto.");
            }

            private Dictionary<string, object> Customer(int id) =>
                new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["name"] = CustomerName
                };
        }

        private sealed class SnapshotEffectTransactionFactory :
            IDdfHostEffectTransactionFactory
        {
            private readonly Func<int> read;
            private readonly Action<int> write;
            private int sequence;

            public SnapshotEffectTransactionFactory(Func<int> read,
                Action<int> write)
            {
                this.read = read;
                this.write = write;
            }

            public SnapshotEffectTransaction LastTransaction { get; private set; }

            public ValueTask<IDdfHostEffectTransaction> BeginAsync(
                DdfHostEffectTransactionContext context)
            {
                LastTransaction = new SnapshotEffectTransaction(
                    "effect-transaction-" + (++sequence), read(), write);
                return ValueTask.FromResult<IDdfHostEffectTransaction>(
                    LastTransaction);
            }
        }

        private sealed class SnapshotEffectTransaction :
            IDdfHostEffectTransaction
        {
            private readonly int initialValue;
            private readonly Action<int> restore;

            public SnapshotEffectTransaction(string id, int initialValue,
                Action<int> restore)
            {
                Id = id;
                this.initialValue = initialValue;
                this.restore = restore;
            }

            public string Id { get; }
            public bool Committed { get; private set; }
            public bool RolledBack { get; private set; }
            public bool Disposed { get; private set; }

            public ValueTask CommitAsync()
            {
                Committed = true;
                return ValueTask.CompletedTask;
            }

            public ValueTask RollbackAsync()
            {
                restore(initialValue);
                RolledBack = true;
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                Disposed = true;
                return ValueTask.CompletedTask;
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
