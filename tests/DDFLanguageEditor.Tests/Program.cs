using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using DDFLanguageEditor.Core;

namespace DDFLanguageEditor.Tests
{
    internal static class Program
    {
        private static readonly List<string> Failures = new List<string>();
        private static int passedTests;

        private static int Main(string[] args)
        {
            if (args?.Contains("--product-update-github-only",
                    StringComparer.Ordinal) == true)
                return RunRealGitHubProductUpdateSmoke();

            Run("classifies complete line comments", ClassifiesCompleteLineComments);
            Run("classifies complete numbers", ClassifiesCompleteNumbers);
            Run("protects strings from nested classification", ProtectsStrings);
            Run("classifies unterminated block comments", ClassifiesUnterminatedBlockComments);
            Run("colors function declarations and resolved calls", ColorsFunctionDeclarationsAndCalls);
            Run("keeps shadowed names and protected text uncolored", KeepsShadowedFunctionNamesUncolored);
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
            Run("anchors and repairs incomplete multi-file returns", AnchorsAndRepairsIncompleteMultiFileReturns);
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
            Run("validates extension plugin manifests without loading code", ValidatesExtensionPluginManifests);
            Run("keeps plugin capability catalog and manifest schema aligned", KeepsPluginCapabilityCatalogAligned);
            Run("denies extension plugins without exact consent", DeniesExtensionPluginsWithoutExactConsent);
            Run("verifies extension publisher signatures without trusting declarations", VerifiesExtensionPublisherSignatures);
            Run("limits plugin capabilities to exact host resources", LimitsPluginCapabilitiesToExactHostResources);
            Run("records plugin security decisions without mixing source diagnostics", RecordsPluginSecurityDecisions);
            Run("persists and authenticates plugin security decisions fail closed", PersistsPluginSecurityDecisionsFailClosed);
            Run("rejects rollback against a monotonic plugin audit anchor", RejectsPluginAuditRollback);
            Run("records explicit plugin consent and revocation without replay", RecordsExplicitPluginConsentAndRevocation);
            Run("resolves only signed and exactly pinned plugin dependency graphs", ResolvesExactlyPinnedPluginDependencies);
            Run("requires separate consent for every plugin dependency", RequiresSeparateConsentForEveryPluginDependency);
            Run("separates plugin preflight diagnostics from project and source errors", SeparatesPluginPreflightDiagnostics);
            Run("keeps extension point descriptions versioned and bound to plugin identity", KeepsExtensionPointsBoundToPluginIdentity);
            Run("parses extension point catalogs strictly before loading code", ParsesExtensionPointCatalogsStrictly);
            Run("authenticates extension point catalogs against signed manifests", AuthenticatesExtensionPointCatalogs);
            Run("bounds immutable extension call values without exposing host objects", BoundsImmutableExtensionCallValues);
            Run("round-trips extension calls through a strict versioned wire format", RoundTripsExtensionCallWireFormat);
            Run("requires explicit authorization for native extensions", RequiresExplicitNativeExtensionAuthorization);
            Run("rejects invalid native extension identities atomically", RejectsInvalidNativeExtensionIdentitiesAtomically);
            Run("lowers DDF to a common intermediate representation", LowersDdfToCommonIntermediateRepresentation);
            Run("builds a source-linked flowchart from intermediate representation", BuildsSourceLinkedFlowchart);
            Run("models pre-test post-test and counted loops in flowcharts", ModelsAllFlowchartLoopForms);
            Run("routes active flowchart control transfers to semantic targets",
                RoutesActiveFlowchartControlTransfersToSemanticTargets);
            Run("validates the complex flowchart regression matrix", ValidatesComplexFlowchartRegressionMatrix);
            Run("collapses linear flowchart sequences without losing source links", CollapsesLinearFlowchartSequences);
            Run("keeps only the newest asynchronous flowchart snapshot", KeepsNewestFlowchartSnapshot);
            Run("marks recoverable flowchart snapshots as partial", MarksRecoverableFlowchartSnapshotsAsPartial);
            Run("builds coherent versioned editor analysis snapshots", BuildsCoherentEditorAnalysisSnapshots);
            Run("keeps only the newest asynchronous editor analysis snapshot", KeepsNewestEditorAnalysisSnapshot);
            Run("resolves editor definitions across supplied buffers", ResolvesEditorDefinitionsAcrossBuffers);
            Run("lays out flowcharts deterministically without overlapping ranks", LaysOutFlowchartsDeterministically);
            Run("executes and diagnoses from intermediate representation", ExecutesAndDiagnosesFromIntermediateRepresentation);
            Run("supports nominal enums across parser type checker IR and VM", SupportsNominalEnumsAcrossCorePipeline);
            Run("executes exhaustive multi-way selection without fall-through", ExecutesExhaustiveMultiWaySelection);
            Run("validates the active enum and selection acceptance matrix", ValidatesActiveEnumAndSelectionAcceptanceMatrix);
            Run("runs isolated host execution sessions", RunsIsolatedHostExecutionSessions);
            Run("governs host execution schedulers", GovernsHostExecutionSchedulers);
            Run("routes only UI capabilities through the host dispatcher",
                RoutesOnlyUiCapabilitiesThroughHostDispatcher);
            Run("observes asynchronous stop and disposal lifecycles",
                ObservesAsynchronousStopAndDisposalLifecycles);
            Run("owns transactional state per editor host instance", OwnsTransactionalStatePerEditorHostInstance);
            Run("shares an explicit host catalog across IntelliSense type checking and runtime", SharesHostCatalogAcrossPipelines);
            Run("separates typed recoverable errors from engine faults", SeparatesTypedRecoverableErrorsFromEngineFaults);
            Run("transports typed errors across VM and host boundaries", TransportsTypedErrorsAcrossVmAndHostBoundaries);
            Run("keeps the future error handling design explicit and inactive", KeepsFutureErrorHandlingDesignExplicitAndInactive);
            Run("keeps the future optional value design explicit and inactive", KeepsFutureOptionalValueDesignExplicitAndInactive);
            Run("keeps the future module qualification design explicit and inactive", KeepsFutureModuleQualificationDesignExplicitAndInactive);
            Run("keeps the future typed iteration design explicit and inactive", KeepsFutureTypedIterationDesignExplicitAndInactive);
            Run("keeps the future typed collection design explicit and inactive", KeepsFutureTypedCollectionDesignExplicitAndInactive);
            Run("keeps the future extended numeric design explicit and inactive", KeepsFutureExtendedNumericDesignExplicitAndInactive);
            Run("keeps the future string library design explicit and inactive", KeepsFutureStringLibraryDesignExplicitAndInactive);
            Run("keeps the future math library design explicit and inactive", KeepsFutureMathLibraryDesignExplicitAndInactive);
            Run("keeps the future temporal and random design explicit and inactive", KeepsFutureTemporalRandomDesignExplicitAndInactive);
            Run("keeps the future standard library binding contract explicit and inactive", KeepsFutureStandardLibraryBindingContractExplicitAndInactive);
            Run("keeps the future host dialog contract explicit and inactive", KeepsFutureHostDialogContractExplicitAndInactive);
            Run("keeps the future project testing contract explicit and inactive", KeepsFutureProjectTestingContractExplicitAndInactive);
            Run("keeps the future resource lifetime contract explicit and inactive", KeepsFutureResourceLifetimeContractExplicitAndInactive);
            Run("keeps the future cooperative async contract explicit and inactive", KeepsFutureCooperativeAsyncContractExplicitAndInactive);
            Run("guards cooperative suspension generations and late results", GuardsCooperativeSuspensionGenerationsAndLateResults);
            Run("enforces host value adapters access and lifetimes", EnforcesHostValueAdaptersAccessAndLifetimes);
            Run("enforces strict typed scalar and enum host adapters", EnforcesStrictTypedHostAdapters);
            Run("returns immutable typed host values after execution", ReturnsImmutableTypedHostValuesAfterExecution);
            Run("invokes typed procedures manually and from host events", InvokesTypedProceduresManuallyAndFromHostEvents);
            Run("separates analysis simulation and host effects", SeparatesAnalysisSimulationAndHostEffects);
            Run("bounds asynchronous host operations and releases resources", BoundsAsyncHostOperationsAndReleasesResources);
            Run("authorizes observes and revokes opaque host resource leases", AuthorizesObservesAndRevokesHostResourceLeases);
            Run("suspends host calls without occupying the execution worker", SuspendsHostCallsWithoutOccupyingExecutionWorker);
            Run("keeps embedded debugger snapshots while awaiting capabilities", KeepsEmbeddedDebuggerSnapshotsWhileAwaitingCapabilities);
            Run("executes parameterized typed database services", ExecutesParameterizedTypedDatabaseServices);
            Run("registers typed host function libraries atomically", RegistersTypedHostFunctionLibrariesAtomically);
            Run("reports and rolls back partial host effects", ReportsAndRollsBackPartialHostEffects);
            Run("builds immutable host document snapshots", BuildsImmutableHostDocumentSnapshots);
            Run("prepares immutable host scripts without compilation", PreparesImmutableHostScriptsWithoutCompilation);
            Run("routes host builds through a custom public handler", RoutesHostBuildsThroughCustomHandler);
            Run("routes host execution through a custom public build handler", RoutesHostExecutionThroughCustomBuildHandler);
            Run("debugs host buffers with document breakpoints", DebugsHostBuffersWithDocumentBreakpoints);
            Run("executes package operators through syntax-independent VM operations", ExecutesPackageOperatorsThroughVmOperations);
            Run("loads and executes package-defined standard functions", LoadsAndExecutesPackageDefinedStandardFunctions);
            Run("enforces the versioned minimum standard library contract", EnforcesMinimumStandardLibraryContract);
            Run("keeps the autonomous specification aligned with the canonical package", KeepsAutonomousSpecificationAligned);
            Run("keeps every active language increment declaratively integrated", KeepsActiveLanguageIncrementsDeclarativelyIntegrated);
            Run("keeps backend equivalence evidence explicit and release blocking", KeepsBackendEquivalenceEvidenceExplicitAndReleaseBlocking);
            Run("keeps the DDF version policy aligned", KeepsDdfVersionPolicyAligned);
            Run("keeps the authoritative version matrix aligned", KeepsAuthoritativeVersionMatrixAligned);
            Run("models immutable versioned product update results", ModelsImmutableVersionedProductUpdateResults);
            Run("persists update installation plans and crash journals atomically", PersistsUpdateInstallationPlansAndCrashJournalsAtomically);
            Run("downloads and seals verified update staging safely", DownloadsAndSealsVerifiedUpdateStagingSafely);
            Run("rejects unsafe update archives without touching the target", RejectsUnsafeUpdateArchivesWithoutTouchingTarget);
            Run("swaps staged updates and rolls back corrupted promotions", SwapsStagedUpdatesAndRollsBackCorruptedPromotions);
            Run("completes updates only after a successful health check", CompletesUpdatesOnlyAfterSuccessfulHealthCheck);
            Run("keeps embedded product update checks host controlled", KeepsEmbeddedProductUpdateChecksHostControlled);
            Run("checks the fixed GitHub release feed safely", ChecksFixedGitHubReleaseFeedSafely);
            Run("exercises product updates through an isolated loopback server", ExercisesProductUpdatesThroughIsolatedLoopbackServer);
            Run("loads the official release trust store fail closed", LoadsOfficialReleaseTrustStoreFailClosed);
            Run("evaluates only signed correlated GitHub releases", EvaluatesOnlySignedCorrelatedGitHubReleases);
            Run("signs and verifies canonical release manifests", SignsAndVerifiesCanonicalReleaseManifests);
            Run("inspects and migrates persisted artifacts atomically", InspectsAndMigratesPersistedArtifactsAtomically);
            Run("keeps the DDF candidate baseline frozen", KeepsDdfCandidateBaselineFrozen);
            Run("keeps candidate issues classified without implicit language changes", KeepsCandidateIssuesClassified);
            Run("rejects ungoverned incompatible DDF candidate changes", RejectsUngovernedCandidateChanges);
            Run("keeps DDF candidate readiness aligned with manual validation", KeepsCandidateReadinessAlignedWithManualValidation);
            Run("keeps semantic checkpoint automation and manual approval separate", KeepsSemanticCheckpointAutomationAndManualApprovalSeparate);
            Run("verifies the DDF candidate toolchain against the normative corpus", VerifiesDdfCandidateToolchainAgainstNormativeCorpus);
            Run("isolates Roslyn from Core WinForms and embedded SDK", IsolatesRoslynFromCoreWinFormsAndEmbeddedSdk);
            Run("rejects unsupported VM and host operations atomically", RejectsUnsupportedVmAndHostOperationsAtomically);
            Run("loads a project manifest independently from the language package", LoadsProjectManifestIndependentlyFromLanguagePackage);
            Run("builds reproducible VM artifacts from unsaved buffers", BuildsReproducibleVmArtifactsFromUnsavedBuffers);
            Run("defines immutable backend-neutral compilation units", DefinesImmutableBackendNeutralCompilationUnits);
            Run("rejects unsafe or mismatched project configurations", RejectsUnsafeOrMismatchedProjectConfigurations);
            Run("resolves external references only through an authorized exact catalog", ResolvesExternalReferencesThroughAuthorizedCatalog);
            Run("locates actionable external-reference diagnostics in the manifest", LocatesExternalReferenceDiagnosticsInManifest);
            Run("governs adapter families without promoting planned integrations", GovernsAdapterFamiliesWithoutPromotion);
            Run("projects one external API contract across analysis and runtime", ProjectsExternalApiAcrossAnalysisAndRuntime);
            Run("resolves versioned modules with export visibility and cycle checks", ResolvesVersionedModulesWithExportVisibilityAndCycleChecks);
            Run("validates direct module visibility collisions and deterministic navigation", ValidatesDirectModuleVisibilityCollisionsAndDeterministicNavigation);
            Run("validates the shipped sample project", ValidatesShippedSampleProject);
            Run("validates the shipped multi-file flowchart workspace", ValidatesShippedFlowchartWorkspace);
            Run("validates and debugs the shipped large workspace", ValidatesAndDebugsShippedLargeWorkspace);
            Run("validates the shipped versioned module workspace", ValidatesShippedModuleWorkspace);
            Run("exposes direct module exports to live editor analysis", ExposesModuleExportsToEditorAnalysis);
            Run("packs installs replaces and removes versioned modules safely", ManagesVersionedModulePackagesSafely);
            Run("installs new language package versions without replacement", InstallsAndUpdatesVersionedLanguagePackages);
            Run("models language package authoring independently from the UI", ModelsLanguagePackageAuthoringIndependently);
            Run("evaluates authoring snapshots with failing declared tests", EvaluatesAuthoringSnapshotsWithFailingDeclaredTests);
            Run("previews language package transformations before applying them", PreviewsLanguagePackageTransformations);
            Run("undoes and redoes authoring operations deterministically", UndoesAndRedoesAuthoringOperations);
            Run("creates and clones detached language authoring projects", CreatesAndClonesLanguageAuthoringProjects);
            Run("analyzes EBNF authoring sources with references", AnalyzesEbnfAuthoringSourcesWithReferences);
            Run("rejects failing declarative package tests", RejectsFailingDeclarativePackageTests);
            Run("validates package dependency graph and minimum execution coverage",
                ValidatesPackageDependencyGraphAndCoverage);
            Run("reports language package compatibility without changing sources",
                ReportsLanguagePackageCompatibility);
            Run("generates reproducible language package archives with checksums",
                GeneratesReproducibleLanguagePackageArchives);
            Run("keeps the language publication example executable",
                KeepsLanguagePublicationExampleExecutable);
            Run("accepts extensible keyword roles and else branches", AcceptsExtensibleKeywordRolesAndElseBranches);
            Run("rejects invalid EBNF grammars atomically", RejectsInvalidEbnfGrammarsAtomically);
            Run("loads directory and archive language packages", LoadsDirectoryAndArchiveLanguagePackages);
            Run("switches active language packages atomically", SwitchesActiveLanguagePackagesAtomically);
            Run("isolates language package sessions per host", IsolatesLanguagePackageSessionsPerHost);
            Run("drives lexical services from a language package", DrivesLexicalServicesFromLanguagePackage);
            Run("executes a syntax-distinct demonstration language", ExecutesSyntaxDistinctDemonstrationLanguage);
            Run("keeps the shipped syntax profiles semantically equivalent", KeepsShippedSyntaxProfilesSemanticallyEquivalent);
            Run("converts equivalent syntax profiles without touching protected text", ConvertsEquivalentSyntaxProfilesSafely);
            Run("plans keyword migrations and blocks identifier collisions", PlansKeywordMigrationsAndBlocksIdentifierCollisions);
            Run("drives diagnostics and flowchart presentation from language packages", DrivesPresentationFromLanguagePackages);
            Run("validates the normative syntax profile corpus", ValidatesNormativeSyntaxProfileCorpus);
            Run("validates the language-service interaction matrix", ValidatesLanguageServiceInteractionMatrix);
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
            Run("indexes globals declared with user-defined types", IndexesUserDefinedTypeGlobals);
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
            Run("renames semantic workspace references", RenamesSemanticWorkspaceReferences);
            Run("parses line and column navigation", ParsesLineAndColumnNavigation);
            Run("accepts semantically valid typed code", AcceptsSemanticallyValidTypedCode);
            Run("treats top-level functions as module functions", TreatsTopLevelFunctionsAsModuleFunctions);
            Run("enforces typed immutable fixed values", EnforcesTypedImmutableFixedValues);
            Run("exposes fixed as read-only IDE and debug metadata", ExposesFixedAsReadOnlyMetadata);
            Run("persists explicit isolated state values", PersistsExplicitIsolatedStateValues);
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
            Run("validates the active scalar and conversion culture matrix", ValidatesActiveScalarAndConversionCultureMatrix);
            Run("type-checks the editor smoke sample", TypeChecksEditorSmokeSample);
            Run("executes functions control flow and output", ExecutesFunctionsControlFlowAndOutput);
            Run("continues loops consistently across language services", ContinuesLoopsAcrossLanguageServices);
            Run("resolves nested control transfers to unique IR flow targets", ResolvesNestedControlTransfers);
            Run("executes arrays and structure members", ExecutesArraysAndStructureMembers);
            Run("validates the active fixed array acceptance matrix", ValidatesActiveFixedArrayAcceptanceMatrix);
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

        private static void ColorsFunctionDeclarationsAndCalls()
        {
            const string source = "sum(int value) out int\n{\n ret value;\n}\n" +
                "main() out int\n{\n int result << sum(2);\n" +
                " print(\"sum(9)\");\n // sum(3);\n ret result;\n}";
            DdfLanguageDefinition language = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package.Language;
            DdfLexResult lex = DdfLexer.Lex(source, language);
            DdfParseResult parse = DdfParser.ParseForEditorFeatures(
                source, lex, language);
            DdfSemanticModel semantic = DdfSemanticModel.Create(source, parse.Root);
            IReadOnlyList<ClassifiedSpan> spans = DdfFunctionSyntaxClassifier
                .Classify(source, lex, parse, semantic, language);
            SequenceEqual(new[] { "sum", "main", "sum", "print" },
                spans.Select(span => Slice(source, span)));
            Equal(true, spans.All(span => span.Kind == SyntaxKind.Function));

            const string externalSource = "helper(int value) out int { ret value; }";
            DdfLexResult externalLex = DdfLexer.Lex(externalSource, language);
            CompilationUnitSyntax externalRoot = DdfParser.ParseForEditorFeatures(
                externalSource, externalLex, language).Root;
            const string caller = "main() out int { ret helper(2); }";
            DdfLexResult callerLex = DdfLexer.Lex(caller, language);
            DdfParseResult callerParse = DdfParser.ParseForEditorFeatures(
                caller, callerLex, language);
            spans = DdfFunctionSyntaxClassifier.Classify(caller, callerLex,
                callerParse, DdfSemanticModel.Create(caller, callerParse.Root,
                    new[] { "helper" }), language, new[] { externalRoot });
            SequenceEqual(new[] { "main", "helper" },
                spans.Select(span => Slice(caller, span)));
        }

        private static void KeepsShadowedFunctionNamesUncolored()
        {
            const string source = "main() out int\n{\n int print << 1;\n" +
                " print(2);\n ret print;\n}";
            DdfLanguageDefinition language = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package.Language;
            DdfLexResult lex = DdfLexer.Lex(source, language);
            DdfParseResult parse = DdfParser.ParseForEditorFeatures(
                source, lex, language);
            IReadOnlyList<ClassifiedSpan> spans = DdfFunctionSyntaxClassifier
                .Classify(source, lex, parse,
                    DdfSemanticModel.Create(source, parse.Root), language);
            SequenceEqual(new[] { "main" },
                spans.Select(span => Slice(source, span)));
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
            int expectedInsertion = source.IndexOf('1') + 1;
            Equal(expectedInsertion - 1, diagnostic.Start);
            Equal(3, diagnostic.Line);
            DdfQuickFix fix = DdfQuickFixService.CreateDefault()
                .GetFixesAt(source, new[] { diagnostic }, diagnostic.Start).Single();
            Equal(expectedInsertion, fix.Start);
            Equal(';', ApplyQuickFix(source, fix)[expectedInsertion]);
        }

        private static void AnchorsAndRepairsIncompleteMultiFileReturns()
        {
            const string valid =
                "main() out int\n" +
                "{\n" +
                "    Pair pair;\n" +
                "    pair.left << 19;\n" +
                "    pair.right << 23;\n" +
                "    ret add(pair.left, pair.right);\n" +
                "}";
            string source = valid.Replace("pair.right);", "pair.right)", StringComparison.Ordinal);
            int expectedInsertion = source.LastIndexOf(')') + 1;
            DdfParseResult parse = DdfParser.Parse(source);
            DdfDiagnostic diagnostic = parse.Diagnostics.Single(item =>
                item.Code == "DDF102" && item.Message.Contains("';'", StringComparison.Ordinal));
            Equal(6, diagnostic.Line);
            Equal(expectedInsertion - 1, diagnostic.Start);
            Equal(expectedInsertion, diagnostic.InsertionPosition.Value);

            DdfQuickFix fix = DdfQuickFixService.CreateDefault()
                .GetFixesAt(source, parse.Diagnostics, diagnostic.Start).Single();
            Equal(expectedInsertion, fix.Start);
            Equal(valid, ApplyQuickFix(source, fix));

            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            using (var analysis = new DdfEditorAnalysisService())
            {
                DdfEditorAnalysisSnapshot snapshot = analysis.Analyze(
                    new DdfEditorAnalysisRequest(source, diagnostic.Start, 1, package, 1));
                Equal(true, snapshot.Diagnostics.Any(item => item.Code == "DDF102" && item.Line == 6));
                Equal(false, snapshot.Diagnostics.Any(item => item.Code == "DDFG101"));
            }
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
            Equal("0.13.0", result.Package.PackageVersion);
            Equal("0.13", result.Package.LanguageVersion);
            Equal(23, result.Package.Language.Keywords.Count);
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

            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            DdfLanguagePackageLoadResult external = DdfLanguagePackageLoader.Load(
                Path.Combine(projectRoot, "language-packages", "ddf"));
            Equal(true, external.Success);
            Equal(0, external.Diagnostics.Count);
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
            Equal(19, package.Language.Semantics.Bindings.Count);

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

        private static void ValidatesExtensionPluginManifests()
        {
            string json = """
                {"schemaVersion":"1.0","id":"ddf.demo","publisher":"ddf.technology",
                "version":"1.2.3","minimumSdkContract":"0.42","maximumSdkContract":"0.50",
                "assemblyFile":"Demo.dll","assemblySha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "dependencies":["ddf.math"],"capabilities":["file.read","ui.panel"]}
                """;
            DdfExtensionPluginManifest manifest = DdfExtensionPluginManifestCodec.Parse(json);
            Equal("ddf.demo", manifest.Id);
            Equal(true, manifest.SupportsSdkContract("0.42"));
            Equal(false, manifest.SupportsSdkContract("0.51"));
            SequenceEqual(new[] { "ddf.math" }, manifest.Dependencies);
            Throws<FormatException>(() => DdfExtensionPluginManifestCodec.Parse(
                json.Replace("\"id\":\"ddf.demo\"", "\"id\":\"ddf.demo\",\"id\":\"ddf.demo\"")));
            Throws<FormatException>(() => DdfExtensionPluginManifestCodec.Parse(
                json.Replace("\"id\":\"ddf.demo\"", "\"other\":1,\"id\":\"ddf.demo\"")));
            Throws<ArgumentException>(() => DdfExtensionPluginManifestCodec.Parse(
                json.Replace("Demo.dll", "../Demo.dll")));
            Throws<ArgumentException>(() => DdfExtensionPluginManifestCodec.Parse(
                json.Replace("Demo.dll", "Demo*.dll")));
            Throws<ArgumentException>(() => DdfExtensionPluginManifestCodec.Parse(
                json.Replace("file.read", "unknown.read")));
            Throws<ArgumentException>(() => DdfExtensionPluginManifestCodec.Parse(
                json.Replace("\"file.read\",\"ui.panel\"", "\"file.read\",\"file.read\"")));
            Throws<ArgumentException>(() => DdfExtensionPluginManifestCodec.Parse(
                json.Replace("ddf.math", "ddf.demo")));
        }

        private static void KeepsPluginCapabilityCatalogAligned()
        {
            DdfExtensionPluginCapabilityDefinition[] definitions =
                DdfExtensionPluginCapabilityCatalog.All.ToArray();
            Equal("1.0", DdfExtensionPluginCapabilityCatalog.Version);
            Equal(19, definitions.Length);
            Equal(6, definitions.Select(item => item.Domain).Distinct().Count());
            Equal(definitions.Length,
                definitions.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
            Equal(true, definitions.All(item => !string.IsNullOrWhiteSpace(item.Operation)));
            Equal(true, DdfExtensionPluginCapabilityCatalog.TryGet("native.load",
                out DdfExtensionPluginCapabilityDefinition native) &&
                native.RequiresDedicatedWorker);
            Equal(true, DdfExtensionPluginCapabilityCatalog.TryGet("file.read",
                out DdfExtensionPluginCapabilityDefinition file) &&
                !file.RequiresDedicatedWorker);
            Equal(false, DdfExtensionPluginCapabilityCatalog.TryGet("file.erase", out _));
            string schemaPath = Path.Combine(AppContext.BaseDirectory,
                "ddf-extension-plugin.schema.json");
            using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
            string[] declared = schema.RootElement.GetProperty("properties")
                .GetProperty("capabilities").GetProperty("items").GetProperty("enum")
                .EnumerateArray().Select(item => item.GetString()).ToArray();
            SequenceEqual(definitions.Select(item => item.Name), declared);
            using JsonDocument catalog = JsonDocument.Parse(
                DdfExtensionPluginCapabilityCatalog.ToJson());
            Equal(definitions.Length, catalog.RootElement.GetProperty("capabilities")
                .GetArrayLength());
            Throws<ArgumentException>(() => new DdfExtensionPluginManifest(
                "ddf.demo", "ddf.technology", "1.0.0", "0.42", "0.50",
                "Demo.dll", new string('a', 64), Array.Empty<string>(),
                new[] { "file.erase" }));
            Throws<ArgumentException>(() => new DdfExtensionPluginGrant(
                "ddf.demo", "ddf.technology", "1.0.0", new string('a', 64),
                new[] { "file.erase" }));
        }

        private static void DeniesExtensionPluginsWithoutExactConsent()
        {
            var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology", "1.2.3",
                "0.42", "0.50", "Demo.dll", new string('a', 64),
                Array.Empty<string>(), new[] { "file.read", "ui.panel" });
            Equal(DdfExtensionPluginDecision.Denied,
                DdfExtensionPluginPolicy.Evaluate(manifest, "0.42", null));
            Equal(DdfExtensionPluginDecision.Incompatible,
                DdfExtensionPluginPolicy.Evaluate(manifest, "0.41", null));
            var partial = new DdfExtensionPluginGrant("ddf.demo", "ddf.technology", "1.2.3",
                new string('a', 64), new[] { "file.read" });
            Equal(DdfExtensionPluginDecision.CapabilityDenied,
                DdfExtensionPluginPolicy.Evaluate(manifest, "0.42", partial));
            var wrongHash = new DdfExtensionPluginGrant("ddf.demo", "ddf.technology", "1.2.3",
                new string('b', 64), manifest.Capabilities);
            Equal(DdfExtensionPluginDecision.IdentityMismatch,
                DdfExtensionPluginPolicy.Evaluate(manifest, "0.42", wrongHash));
            var revoked = new DdfExtensionPluginGrant("ddf.demo", "ddf.technology", "1.2.3",
                new string('a', 64), manifest.Capabilities, revoked: true);
            Equal(DdfExtensionPluginDecision.Revoked,
                DdfExtensionPluginPolicy.Evaluate(manifest, "0.42", revoked));
            var approved = new DdfExtensionPluginGrant("ddf.demo", "ddf.technology", "1.2.3",
                new string('a', 64), manifest.Capabilities);
            Equal(DdfExtensionPluginDecision.Authorized,
                DdfExtensionPluginPolicy.Evaluate(manifest, "0.42", approved));
        }

        private static void VerifiesExtensionPublisherSignatures()
        {
            var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology", "1.2.3",
                "0.42", "0.50", "Demo.dll", new string('a', 64),
                new[] { "ddf.math" }, new[] { "file.read", "ui.panel" });
            using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            byte[] spki = publisher.ExportSubjectPublicKeyInfo();
            var key = new DdfExtensionPluginPublisherKey("ddf.technology", "publisher-1", spki);
            spki[0] ^= 0xff;
            var trust = new DdfExtensionPluginPublisherTrust(new[] { key });
            DdfExtensionPluginSignature signed = DdfExtensionPluginSignatureVerifier.Sign(
                manifest, "publisher-1", publisher);
            string serialized = DdfExtensionPluginSignatureCodec.Serialize(signed);
            DdfExtensionPluginSignature parsed = DdfExtensionPluginSignatureCodec.Parse(serialized);
            Equal(DdfExtensionPluginSignatureStatus.Verified,
                DdfExtensionPluginSignatureVerifier.Verify(manifest, parsed, trust));
            byte[] assembly = Encoding.UTF8.GetBytes("synthetic-assembly-bytes");
            string assemblyHash = Convert.ToHexString(SHA256.HashData(assembly)).ToLowerInvariant();
            var matching = new DdfExtensionPluginManifest(manifest.Id, manifest.Publisher,
                manifest.Version, manifest.MinimumSdkContract, manifest.MaximumSdkContract,
                manifest.AssemblyFile, assemblyHash, manifest.Dependencies,
                manifest.Capabilities);
            DdfExtensionPluginSignature matchingSignature = DdfExtensionPluginSignatureVerifier.Sign(
                matching, "publisher-1", publisher);
            var grant = new DdfExtensionPluginGrant(matching.Id, matching.Publisher,
                matching.Version, matching.AssemblySha256, matching.Capabilities);
            Equal(DdfExtensionPluginDecision.Authorized,
                DdfExtensionPluginPolicy.EvaluateAdmission(matching, matchingSignature,
                    trust, assembly, "0.42", grant));
            Equal(DdfExtensionPluginDecision.AssemblyHashMismatch,
                DdfExtensionPluginPolicy.EvaluateAdmission(matching, matchingSignature,
                    trust, Encoding.UTF8.GetBytes("changed"), "0.42", grant));
            Equal(DdfExtensionPluginDecision.SignatureNotVerified,
                DdfExtensionPluginPolicy.EvaluateAdmission(matching, null,
                    trust, assembly, "0.42", grant));
            Equal(DdfExtensionPluginSignatureStatus.Missing,
                DdfExtensionPluginSignatureVerifier.Verify(manifest, null, trust));
            var altered = new DdfExtensionPluginManifest(manifest.Id, manifest.Publisher,
                manifest.Version, manifest.MinimumSdkContract, manifest.MaximumSdkContract,
                manifest.AssemblyFile, new string('b', 64), manifest.Dependencies,
                manifest.Capabilities);
            Equal(DdfExtensionPluginSignatureStatus.InvalidSignature,
                DdfExtensionPluginSignatureVerifier.Verify(altered, parsed, trust));
            var falsePublisher = new DdfExtensionPluginManifest(manifest.Id, "another.publisher",
                manifest.Version, manifest.MinimumSdkContract, manifest.MaximumSdkContract,
                manifest.AssemblyFile, manifest.AssemblySha256, manifest.Dependencies,
                manifest.Capabilities);
            Equal(DdfExtensionPluginSignatureStatus.UntrustedPublisherOrKey,
                DdfExtensionPluginSignatureVerifier.Verify(falsePublisher, parsed, trust));
            var revoked = new DdfExtensionPluginPublisherTrust(new[] {
                new DdfExtensionPluginPublisherKey("ddf.technology", "publisher-1",
                    publisher.ExportSubjectPublicKeyInfo(), revoked: true) });
            Equal(DdfExtensionPluginSignatureStatus.RevokedKey,
                DdfExtensionPluginSignatureVerifier.Verify(manifest, parsed, revoked));
            Throws<FormatException>(() => DdfExtensionPluginSignatureCodec.Parse(
                serialized.Replace("\"keyId\":", "\"keyId\":\"publisher-1\",\"keyId\":")));
        }

        private static void LimitsPluginCapabilitiesToExactHostResources()
        {
            byte[] assembly = Encoding.UTF8.GetBytes("resource-policy-test");
            string hash = Convert.ToHexString(SHA256.HashData(assembly)).ToLowerInvariant();
            var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
                "1.0.0", "0.42", "0.50", "Demo.dll", hash,
                Array.Empty<string>(), new[] { "file.read", "network.connect" });
            using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new DdfExtensionPluginPublisherTrust(new[] {
                new DdfExtensionPluginPublisherKey("ddf.technology", "key-1",
                    publisher.ExportSubjectPublicKeyInfo()) });
            DdfExtensionPluginSignature signature = DdfExtensionPluginSignatureVerifier.Sign(
                manifest, "key-1", publisher);
            var grant = new DdfExtensionPluginGrant(manifest.Id, manifest.Publisher,
                manifest.Version, hash, manifest.Capabilities);
            var source = new DdfExtensionPluginResourceGrant(manifest.Id,
                manifest.Publisher, manifest.Version, hash, "file.read",
                "project.sources");
            DdfExtensionPluginOperationResult Evaluate(
                IEnumerable<DdfExtensionPluginResourceGrant> scopes, string capability,
                string resourceId, DdfExtensionPluginGrant consent = null) =>
                DdfExtensionPluginResourcePolicy.Evaluate(manifest, signature,
                    trust, assembly, "0.42", consent ?? grant, scopes,
                    capability, resourceId);
            Equal(DdfExtensionPluginOperationDecision.Authorized,
                Evaluate(new[] { source }, "file.read", "project.sources").Decision);
            Equal(DdfExtensionPluginOperationDecision.ResourceNotGranted,
                Evaluate(null, "file.read", "project.sources").Decision);
            Equal(DdfExtensionPluginOperationDecision.ResourceNotGranted,
                Evaluate(new[] { source }, "file.read", "project.secrets").Decision);
            Equal(DdfExtensionPluginOperationDecision.ResourceNotGranted,
                Evaluate(new[] { source }, "network.connect", "project.sources").Decision);
            Equal(DdfExtensionPluginOperationDecision.CapabilityNotDeclared,
                Evaluate(new[] { source }, "process.start", "project.sources").Decision);
            var revoked = new DdfExtensionPluginResourceGrant(manifest.Id,
                manifest.Publisher, manifest.Version, hash, "file.read",
                "project.sources", revoked: true);
            Equal(DdfExtensionPluginOperationDecision.ResourceRevoked,
                Evaluate(new[] { revoked }, "file.read", "project.sources").Decision);
            Equal(DdfExtensionPluginOperationDecision.ResourceNotGranted,
                Evaluate(new[] { source, source }, "file.read", "project.sources").Decision);
            var changed = new DdfExtensionPluginGrant(manifest.Id, manifest.Publisher,
                manifest.Version, new string('b', 64), manifest.Capabilities);
            DdfExtensionPluginOperationResult denied = Evaluate(new[] { source },
                "file.read", "project.sources", consent: changed);
            Equal(DdfExtensionPluginOperationDecision.AdmissionDenied, denied.Decision);
            Equal(DdfExtensionPluginDecision.IdentityMismatch, denied.AdmissionDecision);
            Throws<ArgumentException>(() => new DdfExtensionPluginResourceGrant(
                manifest.Id, manifest.Publisher, manifest.Version, hash,
                "file.read", "C:\\secret.txt"));
        }

        private static void RecordsPluginSecurityDecisions()
        {
            byte[] assembly = Encoding.UTF8.GetBytes("security-ledger-test");
            string hash = Convert.ToHexString(SHA256.HashData(assembly)).ToLowerInvariant();
            var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
                "1.0.0", "0.42", "0.50", "Demo.dll", hash,
                Array.Empty<string>(), new[] { "file.read" });
            using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new DdfExtensionPluginPublisherTrust(new[] {
                new DdfExtensionPluginPublisherKey("ddf.technology", "key-1",
                    publisher.ExportSubjectPublicKeyInfo()) });
            DdfExtensionPluginSignature signature = DdfExtensionPluginSignatureVerifier.Sign(
                manifest, "key-1", publisher);
            var grant = new DdfExtensionPluginGrant(manifest.Id, manifest.Publisher,
                manifest.Version, hash, manifest.Capabilities);
            var resource = new DdfExtensionPluginResourceGrant(manifest.Id,
                manifest.Publisher, manifest.Version, hash, "file.read",
                "project.sources");
            var ledger = new DdfExtensionPluginSecurityLedger(capacity: 2);
            DateTimeOffset at = new DateTimeOffset(2026, 10, 31, 12, 0, 0,
                TimeSpan.Zero);
            DdfExtensionPluginOperationResult accepted = ledger.EvaluateAndRecord(
                manifest, signature, trust, assembly, "0.42", grant,
                new[] { resource }, "file.read", "project.sources", at);
            Equal(true, accepted.IsAuthorized);
            DdfExtensionPluginOperationResult denied = ledger.EvaluateAndRecord(
                manifest, signature, trust, assembly, "0.42", grant,
                new[] { resource }, "file.read", "project.secrets", at);
            Equal(DdfExtensionPluginOperationDecision.ResourceNotGranted, denied.Decision);
            var snapshot = ledger.Snapshot();
            Equal(2, snapshot.Count);
            Equal(1L, snapshot[0].Sequence);
            Equal("DDFX000", snapshot[0].Code);
            Equal("DDFX202", snapshot[1].Code);
            Equal(DdfExtensionPluginSecurityDomain.Resource, snapshot[1].Domain);
            Equal(at, snapshot[0].ObservedAtUtc);
            Equal("project.secrets", snapshot[1].ResourceId);
            DdfExtensionPluginOperationResult overflow = ledger.EvaluateAndRecord(
                manifest, signature, trust, assembly, "0.42", grant,
                new[] { resource }, "file.read", "project.sources", at);
            Equal(DdfExtensionPluginOperationDecision.AuditUnavailable, overflow.Decision);
            Equal("DDFX900", overflow.SecurityCode);
            Equal(DdfExtensionPluginSecurityDomain.Audit, overflow.SecurityDomain);
            Equal(false, overflow.IsAuthorized);
            Equal(2, ledger.Snapshot().Count);
            var separate = new DdfExtensionPluginSecurityLedger();
            separate.EvaluateAndRecord(manifest, null, trust, assembly,
                "0.42", grant, new[] { resource }, "file.read",
                "project.sources", at);
            Equal("DDFX101", separate.Snapshot()[0].Code);
            Equal(DdfExtensionPluginSecurityDomain.PublisherTrust,
                separate.Snapshot()[0].Domain);
        }

        private static void PersistsPluginSecurityDecisionsFailClosed()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-plugin-audit-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] assembly = Encoding.UTF8.GetBytes("durable-audit-test");
                string hash = Convert.ToHexString(SHA256.HashData(assembly)).ToLowerInvariant();
                var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
                    "1.0.0", "0.42", "0.50", "Demo.dll", hash,
                    Array.Empty<string>(), new[] { "file.read" });
                using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var trust = new DdfExtensionPluginPublisherTrust(new[] {
                    new DdfExtensionPluginPublisherKey("ddf.technology", "key-1",
                        publisher.ExportSubjectPublicKeyInfo()) });
                DdfExtensionPluginSignature signature = DdfExtensionPluginSignatureVerifier.Sign(
                    manifest, "key-1", publisher);
                var consent = new DdfExtensionPluginGrant(manifest.Id, manifest.Publisher,
                    manifest.Version, hash, manifest.Capabilities);
                var resource = new DdfExtensionPluginResourceGrant(manifest.Id,
                    manifest.Publisher, manifest.Version, hash, "file.read",
                    "project.sources");
                byte[] key = RandomNumberGenerator.GetBytes(32);
                string path = Path.Combine(root, "plugin-audit.log");
                DateTimeOffset at = new DateTimeOffset(2026, 11, 1, 0, 0, 0,
                    TimeSpan.Zero);
                DdfExtensionPluginOperationResult Record(DdfExtensionPluginSecurityLedger ledger) =>
                    ledger.EvaluateAndRecord(manifest, signature, trust, assembly,
                        "0.42", consent, new[] { resource }, "file.read",
                        "project.sources", at);
                using (var store = new DdfExtensionPluginFileAuditStore(path, key,
                    createNew: true))
                {
                    var ledger = new DdfExtensionPluginSecurityLedger(durableStore: store);
                    Equal(true, Record(ledger).IsAuthorized);
                    Equal(1L, ledger.Snapshot()[0].Sequence);
                }
                using (var store = new DdfExtensionPluginFileAuditStore(path, key))
                {
                    var ledger = new DdfExtensionPluginSecurityLedger(durableStore: store);
                    Equal(true, Record(ledger).IsAuthorized);
                    Equal(2L, ledger.Snapshot()[0].Sequence);
                }
                string journal = File.ReadAllText(path);
                Equal(true, journal.Contains("\n", StringComparison.Ordinal));
                using (var wrongKey = new DdfExtensionPluginFileAuditStore(path,
                    RandomNumberGenerator.GetBytes(32)))
                {
                    var ledger = new DdfExtensionPluginSecurityLedger(durableStore: wrongKey);
                    Equal(DdfExtensionPluginOperationDecision.AuditUnavailable,
                        Record(ledger).Decision);
                    Equal(0, ledger.Snapshot().Count);
                }
                char[] altered = journal.ToCharArray();
                altered[0] = altered[0] == 'A' ? 'B' : 'A';
                File.WriteAllText(path, new string(altered));
                using (var store = new DdfExtensionPluginFileAuditStore(path, key))
                {
                    var ledger = new DdfExtensionPluginSecurityLedger(durableStore: store);
                    Equal(DdfExtensionPluginOperationDecision.AuditUnavailable,
                        Record(ledger).Decision);
                    Equal(0, ledger.Snapshot().Count);
                }
                File.Delete(path);
                using (var store = new DdfExtensionPluginFileAuditStore(path, key))
                {
                    var ledger = new DdfExtensionPluginSecurityLedger(durableStore: store);
                    Equal(DdfExtensionPluginOperationDecision.AuditUnavailable,
                        Record(ledger).Decision);
                    Equal(false, File.Exists(path));
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void RejectsPluginAuditRollback()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-plugin-anchor-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] assembly = Encoding.UTF8.GetBytes("audit-anchor-test");
                string hash = Convert.ToHexString(SHA256.HashData(assembly)).ToLowerInvariant();
                var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
                    "1.0.0", "0.42", "0.50", "Demo.dll", hash,
                    Array.Empty<string>(), new[] { "file.read" });
                using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var trust = new DdfExtensionPluginPublisherTrust(new[] {
                    new DdfExtensionPluginPublisherKey("ddf.technology", "key-1",
                        publisher.ExportSubjectPublicKeyInfo()) });
                DdfExtensionPluginSignature signature = DdfExtensionPluginSignatureVerifier.Sign(
                    manifest, "key-1", publisher);
                var consent = new DdfExtensionPluginGrant(manifest.Id, manifest.Publisher,
                    manifest.Version, hash, manifest.Capabilities);
                var resource = new DdfExtensionPluginResourceGrant(manifest.Id,
                    manifest.Publisher, manifest.Version, hash, "file.read",
                    "project.sources");
                byte[] key = RandomNumberGenerator.GetBytes(32);
                string path = Path.Combine(root, "anchored-audit.log");
                var anchor = new MemoryPluginAuditAnchor();
                DateTimeOffset at = new DateTimeOffset(2026, 11, 1, 12, 0, 0,
                    TimeSpan.Zero);
                using var store = new DdfExtensionPluginFileAuditStore(path, key,
                    createNew: true, anchor: anchor);
                var ledger = new DdfExtensionPluginSecurityLedger(durableStore: store);
                DdfExtensionPluginOperationResult Record() => ledger.EvaluateAndRecord(
                    manifest, signature, trust, assembly, "0.42", consent,
                    new[] { resource }, "file.read", "project.sources", at);
                Equal(true, Record().IsAuthorized);
                byte[] older = File.ReadAllBytes(path);
                Equal(true, Record().IsAuthorized);
                byte[] current = File.ReadAllBytes(path);
                Equal(2L, anchor.Read().Sequence);
                File.WriteAllBytes(path, older);
                Equal(DdfExtensionPluginOperationDecision.AuditUnavailable, Record().Decision);
                Equal(2L, anchor.Read().Sequence);
                File.WriteAllBytes(path, current);
                anchor.FailNextAdvance = true;
                Equal(DdfExtensionPluginOperationDecision.AuditUnavailable, Record().Decision);
                Equal(2L, anchor.Read().Sequence);
                Equal(DdfExtensionPluginOperationDecision.AuditUnavailable, Record().Decision);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void RecordsExplicitPluginConsentAndRevocation()
        {
            string root = Path.Combine(Path.GetTempPath(), "ddf-plugin-consent-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] assembly = Encoding.UTF8.GetBytes("consent-lifecycle-test");
                string hash = Convert.ToHexString(SHA256.HashData(assembly)).ToLowerInvariant();
                var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
                    "1.0.0", "0.42", "0.50", "Demo.dll", hash,
                    Array.Empty<string>(), new[] { "file.read" });
                using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var trust = new DdfExtensionPluginPublisherTrust(new[] {
                    new DdfExtensionPluginPublisherKey("ddf.technology", "key-1",
                        publisher.ExportSubjectPublicKeyInfo()) });
                DdfExtensionPluginSignature signature = DdfExtensionPluginSignatureVerifier.Sign(
                    manifest, "key-1", publisher);
                string path = Path.Combine(root, "consent-audit.log");
                byte[] key = RandomNumberGenerator.GetBytes(32);
                DateTimeOffset at = new DateTimeOffset(2026, 11, 1, 12, 0, 0,
                    TimeSpan.Zero);
                using (var audit = new DdfExtensionPluginFileAuditStore(path, key,
                    createNew: true))
                {
                    var untrusted = new DdfExtensionPluginConsentLedger(manifest,
                        null, trust, assembly, "0.42", audit);
                    Equal(DdfExtensionPluginConsentChange.AdmissionDenied,
                        untrusted.Approve("file.read", "project.sources", "host.user", at));
                    Equal(0, untrusted.ActiveScopes().Count);
                    var session = new DdfExtensionPluginConsentLedger(manifest,
                        signature, trust, assembly, "0.42", audit);
                    Equal(false, session.Evaluate("file.read", "project.sources", at)
                        .IsAuthorized);
                    Equal(DdfExtensionPluginConsentChange.CapabilityNotDeclared,
                        session.Approve("process.start", "project.sources", "host.user", at));
                    Equal(DdfExtensionPluginConsentChange.Granted,
                        session.Approve("file.read", "project.sources", "host.user", at));
                    Equal(DdfExtensionPluginConsentChange.AlreadyGranted,
                        session.Approve("file.read", "project.sources", "host.user", at));
                    Equal(true, session.Evaluate("file.read", "project.sources", at)
                        .IsAuthorized);
                    Equal(false, session.Evaluate("file.read", "project.secrets", at)
                        .IsAuthorized);
                    Equal(DdfExtensionPluginConsentChange.Revoked,
                        session.Revoke("file.read", "project.sources", "host.user", at));
                    Equal(false, session.Evaluate("file.read", "project.sources", at)
                        .IsAuthorized);
                    Equal(0, session.ActiveScopes().Count);
                }
                Equal(true, new FileInfo(path).Length > 0);
                using (var audit = new DdfExtensionPluginFileAuditStore(path, key))
                {
                    var restarted = new DdfExtensionPluginConsentLedger(manifest,
                        signature, trust, assembly, "0.42", audit);
                    Equal(0, restarted.ActiveScopes().Count);
                    Equal(false, restarted.Evaluate("file.read", "project.sources", at)
                        .IsAuthorized);
                    Equal(DdfExtensionPluginConsentChange.Granted,
                        restarted.Approve("file.read", "project.sources", "host.user", at));
                    audit.Dispose();
                    Equal(DdfExtensionPluginConsentChange.AuditUnavailable,
                        restarted.Revoke("file.read", "project.sources", "host.user", at));
                    Equal(0, restarted.ActiveScopes().Count);
                    Equal(DdfExtensionPluginConsentChange.AuditUnavailable,
                        restarted.Approve("file.read", "project.sources", "host.user", at));
                    Equal(0, restarted.ActiveScopes().Count);
                    Equal(false, restarted.Evaluate("file.read", "project.sources", at)
                        .IsAuthorized);
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void ResolvesExactlyPinnedPluginDependencies()
        {
            using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new DdfExtensionPluginPublisherTrust(new[] {
                new DdfExtensionPluginPublisherKey("ddf.technology", "key-1",
                    publisher.ExportSubjectPublicKeyInfo()) });
            DdfExtensionPluginArtifact Make(string id, params string[] dependencies)
            {
                byte[] bytes = Encoding.UTF8.GetBytes("assembly:" + id);
                string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var manifest = new DdfExtensionPluginManifest(id, "ddf.technology",
                    "1.0.0", "0.42", "0.50", "Plugin.dll", hash,
                    dependencies, Array.Empty<string>());
                return new DdfExtensionPluginArtifact(manifest,
                    DdfExtensionPluginSignatureVerifier.Sign(manifest, "key-1", publisher),
                    bytes);
            }
            DdfExtensionPluginArtifact root = Make("ddf.root", "ddf.math", "ddf.text");
            DdfExtensionPluginArtifact math = Make("ddf.math", "ddf.common");
            DdfExtensionPluginArtifact text = Make("ddf.text", "ddf.common");
            DdfExtensionPluginArtifact common = Make("ddf.common");
            DdfExtensionPluginArtifact[] artifacts = { root, math, text, common };
            var pins = new DdfExtensionPluginDependencyLockFile(artifacts.Select(item =>
                DdfExtensionPluginLockEntry.FromManifest(item.Manifest)));
            string json = DdfExtensionPluginDependencyLockCodec.Serialize(pins);
            DdfExtensionPluginDependencyLockFile roundTrip =
                DdfExtensionPluginDependencyLockCodec.Parse(json);
            DdfExtensionPluginDependencyResolution resolved =
                DdfExtensionPluginDependencyResolver.Resolve("ddf.root", roundTrip,
                    artifacts, trust, "0.42");
            Equal(true, resolved.Success);
            SequenceEqual(new[] { "ddf.common", "ddf.math", "ddf.text", "ddf.root" },
                resolved.OrderedIds);
            Equal(false, DdfExtensionPluginDependencyResolver.Resolve("ddf.root", roundTrip,
                artifacts.Where(item => item != common), trust, "0.42").Success);
            Equal(false, DdfExtensionPluginDependencyResolver.Resolve("ddf.root", roundTrip,
                artifacts, trust, "0.51").Success);
            var wrongPin = new DdfExtensionPluginDependencyLockFile(roundTrip.Entries
                .Where(item => item.Id != "ddf.math").Concat(new[] {
                    new DdfExtensionPluginLockEntry("ddf.math", "ddf.technology",
                        "1.0.0", new string('b', 64),
                        DdfExtensionPluginLockEntry.FromManifest(math.Manifest).ManifestSha256) }));
            Equal(false, DdfExtensionPluginDependencyResolver.Resolve("ddf.root", wrongPin,
                artifacts, trust, "0.42").Success);
            var wrongManifestPin = new DdfExtensionPluginDependencyLockFile(roundTrip.Entries
                .Where(item => item.Id != "ddf.math").Concat(new[] {
                    new DdfExtensionPluginLockEntry("ddf.math", "ddf.technology",
                        "1.0.0", math.Manifest.AssemblySha256, new string('c', 64)) }));
            Equal(false, DdfExtensionPluginDependencyResolver.Resolve("ddf.root",
                wrongManifestPin, artifacts, trust, "0.42").Success);
            var alteredBytes = artifacts.Select(item => item == math
                ? new DdfExtensionPluginArtifact(item.Manifest, item.Signature,
                    Encoding.UTF8.GetBytes("altered")) : item).ToArray();
            Equal(false, DdfExtensionPluginDependencyResolver.Resolve("ddf.root", roundTrip,
                alteredBytes, trust, "0.42").Success);
            var unsigned = artifacts.Select(item => item == math
                ? new DdfExtensionPluginArtifact(item.Manifest, null, item.AssemblyBytes)
                : item).ToArray();
            Equal(false, DdfExtensionPluginDependencyResolver.Resolve("ddf.root", roundTrip,
                unsigned, trust, "0.42").Success);
            DdfExtensionPluginArtifact cycleRoot = Make("ddf.root", "ddf.math");
            DdfExtensionPluginArtifact cycleMath = Make("ddf.math", "ddf.root");
            var cyclePins = new DdfExtensionPluginDependencyLockFile(new[] {
                DdfExtensionPluginLockEntry.FromManifest(cycleRoot.Manifest),
                DdfExtensionPluginLockEntry.FromManifest(cycleMath.Manifest) });
            Equal(false, DdfExtensionPluginDependencyResolver.Resolve("ddf.root", cyclePins,
                new[] { cycleRoot, cycleMath }, trust, "0.42").Success);
            DdfExtensionPluginArtifact extra = Make("ddf.extra");
            var extraPins = new DdfExtensionPluginDependencyLockFile(roundTrip.Entries
                .Concat(new[] { DdfExtensionPluginLockEntry.FromManifest(extra.Manifest) }));
            Equal(false, DdfExtensionPluginDependencyResolver.Resolve("ddf.root", extraPins,
                artifacts.Append(extra), trust, "0.42").Success);
            Throws<FormatException>(() => DdfExtensionPluginDependencyLockCodec.Parse(
                json.Replace("\"entries\":", "\"entries\":[],\"entries\":")));
        }

        private static void RequiresSeparateConsentForEveryPluginDependency()
        {
            using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new DdfExtensionPluginPublisherTrust(new[] {
                new DdfExtensionPluginPublisherKey("ddf.technology", "key-1",
                    publisher.ExportSubjectPublicKeyInfo()) });
            DdfExtensionPluginArtifact Make(string id, string[] dependencies,
                string[] capabilities)
            {
                byte[] bytes = Encoding.UTF8.GetBytes("assembly:" + id);
                string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var manifest = new DdfExtensionPluginManifest(id, "ddf.technology",
                    "1.0.0", "0.42", "0.50", "Plugin.dll", hash,
                    dependencies, capabilities);
                return new DdfExtensionPluginArtifact(manifest,
                    DdfExtensionPluginSignatureVerifier.Sign(manifest, "key-1", publisher),
                    bytes);
            }
            DdfExtensionPluginArtifact root = Make("ddf.root", new[] { "ddf.helper" },
                new[] { "file.read" });
            DdfExtensionPluginArtifact helper = Make("ddf.helper", Array.Empty<string>(),
                new[] { "network.connect" });
            DdfExtensionPluginArtifact[] artifacts = { root, helper };
            var pins = new DdfExtensionPluginDependencyLockFile(artifacts.Select(item =>
                DdfExtensionPluginLockEntry.FromManifest(item.Manifest)));
            DdfExtensionPluginGrant Grant(DdfExtensionPluginArtifact artifact,
                IEnumerable<string> capabilities, bool revoked = false) =>
                new DdfExtensionPluginGrant(artifact.Manifest.Id,
                    artifact.Manifest.Publisher, artifact.Manifest.Version,
                    artifact.Manifest.AssemblySha256, capabilities, revoked);
            DdfExtensionPluginGrant rootGrant = Grant(root, root.Manifest.Capabilities);
            DdfExtensionPluginGrant helperGrant = Grant(helper, helper.Manifest.Capabilities);
            DdfExtensionPluginGraphAdmissionResult Evaluate(params DdfExtensionPluginGrant[] grants) =>
                DdfExtensionPluginGraphAdmission.Evaluate("ddf.root", pins,
                    artifacts, trust, "0.42", grants);
            DdfExtensionPluginGraphAdmissionResult missing = Evaluate(rootGrant);
            Equal(false, missing.Success);
            Equal(0, missing.OrderedIds.Count);
            Equal(true, missing.Issues.Single().StartsWith("DDFX401", StringComparison.Ordinal));
            Equal(true, Evaluate(rootGrant, helperGrant).Success);
            SequenceEqual(new[] { "ddf.helper", "ddf.root" },
                Evaluate(rootGrant, helperGrant).OrderedIds);
            Equal(false, Evaluate(rootGrant, Grant(helper, new[] { "file.read" })).Success);
            Equal(false, Evaluate(rootGrant, Grant(helper,
                new[] { "network.connect", "file.read" })).Success);
            Throws<ArgumentException>(() => Grant(helper,
                new[] { "network.connect", "network.connect" }));
            Equal(false, Evaluate(rootGrant, Grant(helper, helper.Manifest.Capabilities,
                revoked: true)).Success);
            Equal(false, Evaluate(rootGrant, helperGrant, helperGrant).Success);
            var foreign = new DdfExtensionPluginGrant("ddf.other", "ddf.technology",
                "1.0.0", new string('a', 64), Array.Empty<string>());
            Equal(false, Evaluate(rootGrant, helperGrant, foreign).Success);
            var wrongHash = new DdfExtensionPluginGrant(helper.Manifest.Id,
                helper.Manifest.Publisher, helper.Manifest.Version,
                new string('b', 64), helper.Manifest.Capabilities);
            Equal(false, Evaluate(rootGrant, wrongHash).Success);
        }

        private static void SeparatesPluginPreflightDiagnostics()
        {
            Equal("DDFX001", DdfExtensionPluginPreflight.ParseManifest("{}").Diagnostics[0].Code);
            Equal("DDFX002", DdfExtensionPluginPreflight.ParseSignature("{}").Diagnostics[0].Code);
            Equal("DDFX003", DdfExtensionPluginPreflight.ParseLock("{}").Diagnostics[0].Code);
            byte[] assembly = Encoding.UTF8.GetBytes("diagnostic-preflight-test");
            string hash = Convert.ToHexString(SHA256.HashData(assembly)).ToLowerInvariant();
            var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
                "1.0.0", "0.42", "0.50", "Demo.dll", hash,
                Array.Empty<string>(), new[] { "file.read" });
            string manifestJson = JsonSerializer.Serialize(new {
                schemaVersion = "1.0", id = manifest.Id, publisher = manifest.Publisher,
                version = manifest.Version, minimumSdkContract = manifest.MinimumSdkContract,
                maximumSdkContract = manifest.MaximumSdkContract,
                assemblyFile = manifest.AssemblyFile, assemblySha256 = manifest.AssemblySha256,
                dependencies = manifest.Dependencies, capabilities = manifest.Capabilities });
            using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new DdfExtensionPluginPublisherTrust(new[] {
                new DdfExtensionPluginPublisherKey("ddf.technology", "key-1",
                    publisher.ExportSubjectPublicKeyInfo()) });
            DdfExtensionPluginSignature signature = DdfExtensionPluginSignatureVerifier.Sign(
                manifest, "key-1", publisher);
            string signatureJson = DdfExtensionPluginSignatureCodec.Serialize(signature);
            Equal(true, DdfExtensionPluginPreflight.VerifyArtifact(manifestJson,
                signatureJson, assembly, trust, "0.42").Success);
            Equal("DDFX102", DdfExtensionPluginPreflight.VerifyArtifact(manifestJson,
                signatureJson, Encoding.UTF8.GetBytes("altered"), trust,
                "0.42").Diagnostics[0].Code);
            Equal("DDFX103", DdfExtensionPluginPreflight.VerifyArtifact(manifestJson,
                signatureJson, assembly, trust, "0.51").Diagnostics[0].Code);
            Equal("DDFX101", DdfExtensionPluginPreflight.VerifyArtifact(manifestJson,
                signatureJson, assembly,
                new DdfExtensionPluginPublisherTrust(Array.Empty<DdfExtensionPluginPublisherKey>()),
                "0.42").Diagnostics[0].Code);
            var artifact = new DdfExtensionPluginArtifact(manifest, signature, assembly);
            var pins = new DdfExtensionPluginDependencyLockFile(new[] {
                DdfExtensionPluginLockEntry.FromManifest(manifest) });
            DdfExtensionPluginGraphAdmissionResult denied =
                DdfExtensionPluginGraphAdmission.Evaluate(manifest.Id, pins,
                    new[] { artifact }, trust, "0.42", null);
            DdfExtensionPluginDiagnostic graphIssue =
                DdfExtensionPluginPreflight.FromGraphAdmission(denied).Single();
            Equal("DDFX401", graphIssue.Code);
            Equal(DdfExtensionPluginDiagnosticDomain.Authorization, graphIssue.Domain);
            Equal(false, graphIssue.ToString().Contains("Codice sorgente",
                StringComparison.Ordinal));
            DdfExtensionPluginDependencyResolution missingArtifact =
                DdfExtensionPluginDependencyResolver.Resolve(manifest.Id, pins,
                    Array.Empty<DdfExtensionPluginArtifact>(), trust, "0.42");
            DdfExtensionPluginDiagnostic dependencyIssue =
                DdfExtensionPluginPreflight.FromDependencyResolution(missingArtifact).Single();
            Equal("DDFX302", dependencyIssue.Code);
            Equal(DdfExtensionPluginDiagnosticDomain.Dependency, dependencyIssue.Domain);
            var grant = new DdfExtensionPluginGrant(manifest.Id, manifest.Publisher,
                manifest.Version, hash, manifest.Capabilities);
            DdfExtensionPluginOperationResult operation =
                DdfExtensionPluginResourcePolicy.Evaluate(manifest, signature,
                    trust, assembly, "0.42", grant, null,
                    "file.read", "project.sources");
            DdfExtensionPluginDiagnostic scopeIssue =
                DdfExtensionPluginPreflight.FromOperation(operation).Single();
            Equal("DDFX202", scopeIssue.Code);
            Equal(DdfExtensionPluginDiagnosticDomain.Resource, scopeIssue.Domain);
            Equal(false, scopeIssue.Code.StartsWith("DDFP", StringComparison.Ordinal));
        }

        private static void KeepsExtensionPointsBoundToPluginIdentity()
        {
            var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
                "1.0.0", "0.42", "0.50", "Demo.dll", new string('a', 64),
                Array.Empty<string>(), new[] { "network.connect", "ui.command" });
            DdfExtensionPointDescriptor[] points =
            {
                new DdfExtensionPointDescriptor("ddf.demo.semantic", DdfExtensionPointKind.SemanticOperation,
                    "1.0", Array.Empty<string>()),
                new DdfExtensionPointDescriptor("ddf.demo.runtime", DdfExtensionPointKind.RuntimeOperation,
                    "1.0", new[] { "network.connect" }),
                new DdfExtensionPointDescriptor("ddf.demo.host", DdfExtensionPointKind.HostFunction,
                    "1.0", new[] { "ui.command" }),
                new DdfExtensionPointDescriptor("ddf.demo.build", DdfExtensionPointKind.BuildBackend,
                    "1.0", Array.Empty<string>())
            };
            var catalog = new DdfExtensionPointCatalog(manifest, points);
            Equal(4, catalog.Points.Count);
            Equal(true, catalog.TryGet("ddf.demo.runtime", out DdfExtensionPointDescriptor found));
            Equal(DdfExtensionPointKind.RuntimeOperation, found.Kind);
            Equal(false, catalog.TryGet("ddf.demo.missing", out _));
            using JsonDocument json = JsonDocument.Parse(catalog.ToJson());
            Equal("1.0", json.RootElement.GetProperty("schemaVersion").GetString());
            Equal("ddf.demo", json.RootElement.GetProperty("pluginId").GetString());
            Equal(4, json.RootElement.GetProperty("points").GetArrayLength());
            Throws<ArgumentException>(() => new DdfExtensionPointDescriptor("ddf.demo.bad",
                DdfExtensionPointKind.HostFunction, "2.0", Array.Empty<string>()));
            Throws<ArgumentException>(() => new DdfExtensionPointDescriptor("ddf.demo.bad",
                DdfExtensionPointKind.HostFunction, "1.0", new[] { "invented" }));
            Throws<ArgumentException>(() => new DdfExtensionPointCatalog(manifest, new[] {
                new DdfExtensionPointDescriptor("other.plugin.host", DdfExtensionPointKind.HostFunction,
                    "1.0", Array.Empty<string>()) }));
            Throws<ArgumentException>(() => new DdfExtensionPointCatalog(manifest, new[] {
                new DdfExtensionPointDescriptor("ddf.demo.file", DdfExtensionPointKind.HostFunction,
                    "1.0", new[] { "file.read" }) }));
            Throws<ArgumentException>(() => new DdfExtensionPointCatalog(manifest,
                new[] { points[0], points[0] }));
        }

        private static void ParsesExtensionPointCatalogsStrictly()
        {
            var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
                "1.0.0", "0.42", "0.50", "Demo.dll", new string('a', 64),
                Array.Empty<string>(), new[] { "file.read" });
            var descriptor = new DdfExtensionPointDescriptor("ddf.demo.read",
                DdfExtensionPointKind.HostFunction, "1.0", new[] { "file.read" });
            string valid = new DdfExtensionPointCatalog(manifest, new[] { descriptor }).ToJson();
            DdfExtensionPointCatalog parsed = DdfExtensionPointCatalogCodec.Parse(valid, manifest);
            Equal(valid, parsed.ToJson());
            Equal("ddf.demo.read", parsed.Points.Single().Id);
            Throws<FormatException>(() => DdfExtensionPointCatalogCodec.Parse(
                valid.Replace("\"pluginId\":\"ddf.demo\"", "\"pluginId\":\"other.plugin\""), manifest));
            Throws<FormatException>(() => DdfExtensionPointCatalogCodec.Parse(
                valid.Replace("\"pluginVersion\":\"1.0.0\"", "\"pluginVersion\":\"2.0.0\""), manifest));
            Throws<FormatException>(() => DdfExtensionPointCatalogCodec.Parse(
                valid.Replace("\"schemaVersion\":\"1.0\"", "\"schemaVersion\":\"2.0\""), manifest));
            Throws<FormatException>(() => DdfExtensionPointCatalogCodec.Parse(
                valid.Replace("\"kind\":\"HostFunction\"", "\"kind\":\"Unknown\""), manifest));
            Throws<ArgumentException>(() => DdfExtensionPointCatalogCodec.Parse(
                valid.Replace("\"file.read\"", "\"file.write\""), manifest));
            Throws<FormatException>(() => DdfExtensionPointCatalogCodec.Parse(
                valid.Replace("\"points\":", "\"extra\":0,\"points\":"), manifest));
            Throws<FormatException>(() => DdfExtensionPointCatalogCodec.Parse(
                valid.Replace("\"id\":", "\"id\":\"ddf.demo.duplicate\",\"id\":"), manifest));
            Throws<FormatException>(() => DdfExtensionPointCatalogCodec.Parse(
                valid.Replace("\"apiVersion\":", "\"apiVersion\":\"1.0\",\"apiVersion\":"), manifest));
            Throws<ArgumentException>(() => DdfExtensionPointCatalogCodec.Parse(
                new string(' ', 65537), manifest));
            Throws<JsonException>(() => DdfExtensionPointCatalogCodec.Parse(valid + " // comment", manifest));
        }

        private static void AuthenticatesExtensionPointCatalogs()
        {
            var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
                "1.0.0", "0.42", "0.50", "Demo.dll", new string('a', 64),
                Array.Empty<string>(), new[] { "file.read", "file.write" });
            var catalog = new DdfExtensionPointCatalog(manifest, new[] {
                new DdfExtensionPointDescriptor("ddf.demo.read", DdfExtensionPointKind.HostFunction,
                    "1.0", new[] { "file.read" }) });
            using ECDsa publisher = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new DdfExtensionPluginPublisherTrust(new[] {
                new DdfExtensionPluginPublisherKey(manifest.Publisher, "key-1",
                    publisher.ExportSubjectPublicKeyInfo()) });
            DdfExtensionPluginSignature manifestSignature =
                DdfExtensionPluginSignatureVerifier.Sign(manifest, "key-1", publisher);
            DdfExtensionPluginSignature catalogSignature =
                DdfExtensionPointAttestation.Sign(catalog, "key-1", publisher);
            string catalogJson = catalog.ToJson();
            string signatureJson = DdfExtensionPluginSignatureCodec.Serialize(catalogSignature);
            Equal(DdfExtensionPointAttestationStatus.Verified,
                DdfExtensionPointAttestation.Verify(catalog, catalogSignature,
                    manifestSignature, trust));
            Equal(true, DdfExtensionPluginPreflight.VerifyCatalog(catalogJson,
                signatureJson, manifest, manifestSignature, trust).Success);
            Equal("DDFX104", DdfExtensionPluginPreflight.VerifyCatalog(catalogJson,
                signatureJson, manifest, null, trust).Diagnostics.Single().Code);
            Equal("DDFX005", DdfExtensionPluginPreflight.VerifyCatalog(catalogJson,
                "{}", manifest, manifestSignature, trust).Diagnostics.Single().Code);
            Equal("DDFX104", DdfExtensionPluginPreflight.VerifyCatalog(catalogJson,
                DdfExtensionPluginSignatureCodec.Serialize(manifestSignature),
                manifest, manifestSignature, trust).Diagnostics.Single().Code);
            Equal("DDFX004", DdfExtensionPluginPreflight.VerifyCatalog(
                catalogJson.Replace("\"file.read\"", "\"file.delete\""),
                signatureJson, manifest, manifestSignature, trust).Diagnostics.Single().Code);
            var changed = new DdfExtensionPointCatalog(manifest, new[] {
                new DdfExtensionPointDescriptor("ddf.demo.read", DdfExtensionPointKind.HostFunction,
                    "1.0", new[] { "file.write" }) });
            Equal(DdfExtensionPointAttestationStatus.InvalidSignature,
                DdfExtensionPointAttestation.Verify(changed, catalogSignature,
                    manifestSignature, trust));
            Equal("DDFX104", DdfExtensionPluginPreflight.VerifyCatalog(changed.ToJson(),
                signatureJson, manifest, manifestSignature, trust).Diagnostics.Single().Code);
            var changedManifest = new DdfExtensionPluginManifest(manifest.Id,
                manifest.Publisher, "1.0.1", "0.42", "0.50", "Demo.dll",
                manifest.AssemblySha256, manifest.Dependencies, manifest.Capabilities);
            Equal(DdfExtensionPointAttestationStatus.ManifestNotVerified,
                DdfExtensionPointAttestation.Verify(
                    new DdfExtensionPointCatalog(changedManifest, catalog.Points),
                    catalogSignature, manifestSignature, trust));
            using ECDsa stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Equal(DdfExtensionPointAttestationStatus.InvalidSignature,
                DdfExtensionPointAttestation.Verify(catalog,
                    DdfExtensionPointAttestation.Sign(catalog, "key-1", stranger),
                    manifestSignature, trust));
            var revoked = new DdfExtensionPluginPublisherTrust(new[] {
                new DdfExtensionPluginPublisherKey(manifest.Publisher, "key-1",
                    publisher.ExportSubjectPublicKeyInfo(), revoked: true) });
            Equal(DdfExtensionPointAttestationStatus.ManifestNotVerified,
                DdfExtensionPointAttestation.Verify(catalog, catalogSignature,
                    manifestSignature, revoked));
        }

        private static void BoundsImmutableExtensionCallValues()
        {
            Equal("1.0", DdfExtensionValue.ContractVersion);
            Equal(true, DdfExtensionValue.FromBoolean(true).Boolean);
            Equal(int.MaxValue, DdfExtensionValue.FromInteger(int.MaxValue).Integer);
            Equal(1.5, DdfExtensionValue.FromFloat(1.5).Float);
            Equal("ciao", DdfExtensionValue.FromString("ciao").String);
            Equal(DdfExtensionValueKind.Null, DdfExtensionValue.Null.Kind);
            Throws<ArgumentOutOfRangeException>(() => DdfExtensionValue.FromFloat(double.NaN));
            Throws<ArgumentOutOfRangeException>(() => DdfExtensionValue.FromFloat(double.PositiveInfinity));
            Throws<ArgumentException>(() => DdfExtensionValue.FromString(new string('x', 4097)));
            Throws<InvalidOperationException>(() => { _ = DdfExtensionValue.Null.Integer; });
            DdfExtensionValue[] source = { DdfExtensionValue.FromInteger(3) };
            DdfExtensionValue array = DdfExtensionValue.FromArray(source);
            source[0] = DdfExtensionValue.FromInteger(99);
            Equal(3, array.Elements[0].Integer);
            Throws<ArgumentException>(() => DdfExtensionValue.FromArray(
                new DdfExtensionValue[1]));
            DdfExtensionValue deep = DdfExtensionValue.Null;
            for (int index = 0; index < 7; index++) deep = DdfExtensionValue.FromArray(new[] { deep });
            DdfExtensionValue atLimit = deep;
            Throws<ArgumentException>(() => DdfExtensionValue.FromArray(new[] { atLimit }));
            var child = DdfExtensionValue.FromArray(Enumerable.Repeat(DdfExtensionValue.Null, 4));
            Throws<ArgumentException>(() => DdfExtensionValue.FromArray(Enumerable.Repeat(child, 256)));
            var runtime = new DdfExtensionPointDescriptor("ddf.demo.runtime",
                DdfExtensionPointKind.RuntimeOperation, "1.0", Array.Empty<string>());
            var host = new DdfExtensionPointDescriptor("ddf.demo.host",
                DdfExtensionPointKind.HostFunction, "1.0", Array.Empty<string>());
            var semantic = new DdfExtensionPointDescriptor("ddf.demo.semantic",
                DdfExtensionPointKind.SemanticOperation, "1.0", Array.Empty<string>());
            KeyValuePair<string, DdfExtensionValue>[] inputs = {
                new KeyValuePair<string, DdfExtensionValue>("value", array) };
            var request = new DdfExtensionCallRequest(runtime, inputs);
            inputs[0] = new KeyValuePair<string, DdfExtensionValue>("changed", DdfExtensionValue.Null);
            Equal(3, request.Arguments["value"].Elements[0].Integer);
            Equal("1.0", request.ContractVersion);
            Equal(DdfExtensionPointKind.HostFunction,
                new DdfExtensionCallRequest(host, Array.Empty<KeyValuePair<string,
                    DdfExtensionValue>>()).Point.Kind);
            Throws<ArgumentException>(() => new DdfExtensionCallRequest(semantic,
                Array.Empty<KeyValuePair<string, DdfExtensionValue>>()));
            Throws<ArgumentException>(() => new DdfExtensionCallRequest(runtime, new[] {
                new KeyValuePair<string, DdfExtensionValue>("x", DdfExtensionValue.Null),
                new KeyValuePair<string, DdfExtensionValue>("x", DdfExtensionValue.Null) }));
            var twenty = DdfExtensionValue.FromArray(
                Enumerable.Repeat(DdfExtensionValue.Null, 20));
            Throws<ArgumentException>(() => new DdfExtensionCallRequest(runtime,
                Enumerable.Range(0, 64).Select(index =>
                    new KeyValuePair<string, DdfExtensionValue>("arg" + index, twenty))));
        }

        private static void RoundTripsExtensionCallWireFormat()
        {
            var manifest = new DdfExtensionPluginManifest("ddf.demo", "ddf.technology",
                "1.0.0", "0.42", "0.50", "Demo.dll", new string('a', 64),
                Array.Empty<string>(), Array.Empty<string>());
            var point = new DdfExtensionPointDescriptor("ddf.demo.call",
                DdfExtensionPointKind.RuntimeOperation, "1.0", Array.Empty<string>());
            var catalog = new DdfExtensionPointCatalog(manifest, new[] { point });
            var request = new DdfExtensionCallRequest(point, new[] {
                new KeyValuePair<string, DdfExtensionValue>("text", DdfExtensionValue.FromString("à\"")),
                new KeyValuePair<string, DdfExtensionValue>("items", DdfExtensionValue.FromArray(new[] {
                    DdfExtensionValue.Null, DdfExtensionValue.FromBoolean(true),
                    DdfExtensionValue.FromInteger(-3), DdfExtensionValue.FromFloat(1.25) })) });
            string json = DdfExtensionCallCodec.Serialize(request);
            DdfExtensionCallRequest parsed = DdfExtensionCallCodec.Parse(json, catalog);
            Equal(json, DdfExtensionCallCodec.Serialize(parsed));
            Equal("ddf.demo.call", parsed.Point.Id);
            Equal("à\"", parsed.Arguments["text"].String);
            Equal(4, parsed.Arguments["items"].Elements.Count);
            Equal(1.25, parsed.Arguments["items"].Elements[3].Float);
            Throws<FormatException>(() => DdfExtensionCallCodec.Parse(
                json.Replace("\"schemaVersion\":\"1.0\"", "\"schemaVersion\":\"2.0\""), catalog));
            Throws<FormatException>(() => DdfExtensionCallCodec.Parse(
                json.Replace("\"pointId\":\"ddf.demo.call\"", "\"pointId\":\"ddf.other.call\""), catalog));
            Throws<FormatException>(() => DdfExtensionCallCodec.Parse(
                json.Replace("\"arguments\":", "\"extra\":0,\"arguments\":"), catalog));
            Throws<FormatException>(() => DdfExtensionCallCodec.Parse(
                json.Replace("\"kind\":\"Integer\"", "\"kind\":\"Unknown\""), catalog));
            Throws<FormatException>(() => DdfExtensionCallCodec.Parse(
                json.Replace("\"value\":-3", "\"value\":2147483648"), catalog));
            Throws<FormatException>(() => DdfExtensionCallCodec.Parse(
                json.Replace("\"kind\":\"Null\"", "\"kind\":\"Null\",\"value\":null"), catalog));
            Throws<FormatException>(() => DdfExtensionCallCodec.Parse(
                json.Replace("\"text\":", "\"text\":{\"kind\":\"Null\"},\"text\":"), catalog));
            Throws<JsonException>(() => DdfExtensionCallCodec.Parse(json + " // trailing", catalog));
            Throws<ArgumentException>(() => DdfExtensionCallCodec.Parse(new string(' ', 131073), catalog));
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

        private static void RoutesActiveFlowchartControlTransfersToSemanticTargets()
        {
            const string source =
                "main() out int\n{\n" +
                "    int total << 0;\n" +
                "    int outer << 0;\n" +
                "    while(outer < 2)\n    {\n" +
                "        outer++;\n" +
                "        for(int inner << 0; inner < 4; inner++)\n        {\n" +
                "            select(inner)\n            {\n" +
                "                when 0 { continue; }\n" +
                "                when 1 { brk; }\n" +
                "                otherwise { total++; }\n" +
                "            }\n" +
                "            if(total > 5) { ret total; }\n" +
                "        }\n" +
                "        do\n        {\n" +
                "            total++;\n" +
                "            if(total < 3) { continue; }\n" +
                "            brk;\n" +
                "        } while(total < 10);\n" +
                "    }\n" +
                "    end;\n" +
                "}\n";
            DdfFlowchartBuildResult result = DdfFlowchartBuilder.Build(source,
                "main", documentPath: "main.ddf");
            Equal(true, result.Success);
            DdfFlowchart graph = result.Flowchart;

            DdfFlowchartNode forContinue = graph.Nodes.Single(node =>
                node.Kind == DdfFlowchartNodeKind.Continue &&
                node.Start == source.IndexOf("continue;", StringComparison.Ordinal));
            DdfFlowchartEdge forContinueEdge = graph.Edges.Single(edge =>
                edge.From == forContinue.Id);
            DdfFlowchartNode forIncrement = graph.FindNode(forContinueEdge.To);
            Equal(DdfFlowchartEdgeKind.Continue, forContinueEdge.Kind);
            Equal(true, forContinueEdge.IsBackEdge);
            Equal(true, forIncrement.Label.Contains("inner++"));

            int doContinueStart = source.LastIndexOf("continue;",
                StringComparison.Ordinal);
            DdfFlowchartNode doContinue = graph.Nodes.Single(node =>
                node.Kind == DdfFlowchartNodeKind.Continue &&
                node.Start == doContinueStart);
            DdfFlowchartEdge doContinueEdge = graph.Edges.Single(edge =>
                edge.From == doContinue.Id);
            DdfFlowchartNode doDecision = graph.FindNode(doContinueEdge.To);
            Equal(DdfFlowchartEdgeKind.Continue, doContinueEdge.Kind);
            Equal(true, doContinueEdge.IsBackEdge);
            Equal(true, doDecision.Label.Contains("total < 10"));

            int selectBreakStart = source.IndexOf("brk;", StringComparison.Ordinal);
            DdfFlowchartNode selectBreak = graph.Nodes.Single(node =>
                node.Kind == DdfFlowchartNodeKind.Break &&
                node.Start == selectBreakStart);
            DdfFlowchartEdge selectBreakEdge = graph.Edges.Single(edge =>
                edge.From == selectBreak.Id);
            DdfFlowchartNode afterFor = graph.FindNode(selectBreakEdge.To);
            Equal(DdfFlowchartEdgeKind.Break, selectBreakEdge.Kind);
            Equal(true, afterFor.Label.Contains("total++"));
            Equal(true, afterFor.Start > source.IndexOf("do\n",
                StringComparison.Ordinal));

            int doBreakStart = source.LastIndexOf("brk;",
                StringComparison.Ordinal);
            DdfFlowchartNode doBreak = graph.Nodes.Single(node =>
                node.Kind == DdfFlowchartNodeKind.Break &&
                node.Start == doBreakStart);
            DdfFlowchartEdge doBreakEdge = graph.Edges.Single(edge =>
                edge.From == doBreak.Id);
            DdfFlowchartNode outerDecision = graph.FindNode(doBreakEdge.To);
            Equal(DdfFlowchartEdgeKind.Break, doBreakEdge.Kind);
            Equal(true, outerDecision.Label.Contains("outer < 2"));

            foreach (DdfFlowchartNode terminal in graph.Nodes.Where(node =>
                         node.Kind == DdfFlowchartNodeKind.Return ||
                         node.Kind == DdfFlowchartNodeKind.Terminate))
            {
                DdfFlowchartEdge edge = graph.Edges.Single(candidate =>
                    candidate.From == terminal.Id);
                Equal(graph.ExitNodeId, edge.To);
                Equal(terminal.Kind == DdfFlowchartNodeKind.Return
                    ? DdfFlowchartEdgeKind.Return
                    : DdfFlowchartEdgeKind.Terminate, edge.Kind);
            }
            Equal(2, graph.Nodes.Count(node =>
                node.Kind == DdfFlowchartNodeKind.Continue));
            Equal(2, graph.Nodes.Count(node =>
                node.Kind == DdfFlowchartNodeKind.Break));
            Equal(true, graph.Nodes.All(node => node.Start >= 0 &&
                node.Start + node.Length <= source.Length));
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

        private static void SupportsNominalEnumsAcrossCorePipeline()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            const string valid =
                "enum Color { Red, Green, Blue }\n" +
                "main() out int { Color selected; Color expected << Color.Red; " +
                "if(selected == expected) { ret 42; } ret 0; }";
            DdfParseResult parse = DdfParser.Parse(valid, package.Language);
            Equal(0, parse.Diagnostics.Count);
            EnumDeclarationSyntax declaration = parse.Root.Members
                .OfType<EnumDeclarationSyntax>().Single();
            SequenceEqual(new[] { "Red", "Green", "Blue" },
                declaration.Members.Select(item => item.Name));
            DdfTypeCheckResult types = DdfTypeChecker.Check(valid, parse.Root,
                typeSystem: package.Language.Types,
                standardLibrary: package.Language.StandardLibrary,
                runtime: package.Language.Runtime);
            Equal(0, types.Diagnostics.Count);
            DdfIrProgram ir = DdfIrLowerer.Lower(valid, parse, package.Language).Program;
            Equal(1, ir.Members.OfType<DdfIrEnum>().Count());
            DdfExecutionResult execution = DdfVirtualMachine.Execute(valid, ir,
                package.Language.Runtime, package.Language.Types,
                package.Language.StandardLibrary);
            Equal(0, execution.Diagnostics.Count);
            Equal(42L, Convert.ToInt64(execution.ReturnValue));
            string completionSource =
                "enum Color { Red, Green, Blue } main() out int { Color value << Color.; ret 0; }";
            int completionPosition = completionSource.IndexOf("Color.;",
                StringComparison.Ordinal) + "Color.".Length;
            DdfCompletionResult completion = DdfCompletionService.GetCompletions(
                completionSource, completionPosition, true, package.Language);
            SequenceEqual(new[] { "Blue", "Green", "Red" }, completion.Items
                .Where(item => item.Kind == DdfCompletionKind.Field)
                .Select(item => item.DisplayText).OrderBy(item => item,
                    StringComparer.Ordinal));

            const string invalid =
                "enum Empty { } enum Color { Red, Red } enum State { Red } " +
                "main() out int { Color color << State.Red; " +
                "Color.Red << Color.Red; if(color == State.Red) { ret 1; } ret 0; }";
            DdfParseResult invalidParse = DdfParser.Parse(invalid, package.Language);
            DdfTypeCheckResult invalidTypes = DdfTypeChecker.Check(invalid,
                invalidParse.Root, typeSystem: package.Language.Types,
                standardLibrary: package.Language.StandardLibrary,
                runtime: package.Language.Runtime);
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF324"));
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF325"));
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF301"));
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF302"));
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF308"));
        }

        private static void ExecutesExhaustiveMultiWaySelection()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            const string valid =
                "enum Color { Red, Green, Blue }\n" +
                "main() out int\n{\n    Color selected << Color.Green;\n" +
                "    int result << 0;\n    select(selected)\n    {\n" +
                "        when Color.Red { result << 1; }\n" +
                "        when Color.Green { result << 42; }\n" +
                "        when Color.Blue { result << 3; }\n    }\n" +
                "    ret result;\n}";
            DdfParseResult parse = DdfParser.Parse(valid, package.Language);
            Equal(0, parse.Diagnostics.Count);
            SelectStatementSyntax selection = parse.Root.Members
                .OfType<FunctionDeclarationSyntax>().Single().Body.Statements
                .OfType<SelectStatementSyntax>().Single();
            Equal(3, selection.Clauses.Count);
            Equal(false, selection.Clauses.Any(clause => clause.IsOtherwise));
            DdfTypeCheckResult types = DdfTypeChecker.Check(valid, parse.Root,
                typeSystem: package.Language.Types,
                standardLibrary: package.Language.StandardLibrary,
                runtime: package.Language.Runtime);
            Equal(0, types.Diagnostics.Count);
            DdfIrProgram ir = DdfIrLowerer.Lower(valid, parse, package.Language).Program;
            Equal(1, ir.Members.OfType<DdfIrFunction>().Single().Body.Statements
                .OfType<DdfIrSelect>().Count());
            DdfExecutionResult execution = DdfVirtualMachine.Execute(valid, ir,
                package.Language.Runtime, package.Language.Types,
                package.Language.StandardLibrary);
            Equal(true, execution.Succeeded);
            Equal(42L, Convert.ToInt64(execution.ReturnValue));
            Equal(true, DdfFormatter.Format(valid, package.Language).Text
                .Contains("when Color.Green"));
            DdfFlowchart flowchart = DdfFlowchartBuilder.Build(valid, "main",
                package.Language).Flowchart;
            Equal(3, flowchart.Nodes.Count(node =>
                node.Kind == DdfFlowchartNodeKind.Decision));

            const string invalid =
                "enum Color { Red, Green } main() out int { int value << 1; " +
                "select(value) { when 1 { value << 2; } when 1 { value << 3; } } " +
                "select(value) { otherwise { value << 4; } when 2 { value << 5; } " +
                "otherwise { value << 6; } } select(value) { when \"x\" { value << 7; } " +
                "otherwise { value << 8; } } ret value; }";
            DdfParseResult invalidParse = DdfParser.Parse(invalid, package.Language);
            DdfTypeCheckResult invalidTypes = DdfTypeChecker.Check(invalid,
                invalidParse.Root, typeSystem: package.Language.Types,
                standardLibrary: package.Language.StandardLibrary,
                runtime: package.Language.Runtime);
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF326"));
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF327"));
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF328"));
            Equal(true, invalidTypes.Diagnostics.Count(item => item.Code == "DDF329") >= 2);

            string profiles = Path.Combine(AppContext.BaseDirectory, "LanguageProfiles");
            var profileCases = new[]
            {
                new { Id = "ddf", Select = "select", When = "when", Otherwise = "otherwise", Out = "out", Return = "ret", Initializer = "<<" },
                new { Id = "ddf-c-familiar", Select = "switch", When = "case", Otherwise = "default", Out = "returns", Return = "return", Initializer = "=" },
                new { Id = "ddf-it-didactic", Select = "scegli", When = "caso", Otherwise = "predefinito", Out = "restituisce", Return = "restituisci", Initializer = "<<" }
            };
            foreach (var profile in profileCases)
            {
                DdfLanguagePackageLoadResult loaded = DdfLanguagePackageLoader.Load(
                    Path.Combine(profiles, profile.Id));
                Equal(true, loaded.Success);
                string profileSource = "main() " + profile.Out + " int { int value " +
                    profile.Initializer + " 2; " + profile.Select + "(value) { " +
                    profile.When + " 1 { " + profile.Return + " 1; } " +
                    profile.When + " 2 { " + profile.Return + " 42; } " +
                    profile.Otherwise + " { " + profile.Return + " 0; } } }";
                DdfParseResult profileParse = DdfParser.Parse(profileSource,
                    loaded.Package.Language);
                Equal(0, profileParse.Diagnostics.Count);
                Equal(0, DdfTypeChecker.Check(profileSource, profileParse.Root,
                    typeSystem: loaded.Package.Language.Types,
                    standardLibrary: loaded.Package.Language.StandardLibrary,
                    runtime: loaded.Package.Language.Runtime).Diagnostics.Count);
                DdfIrProgram profileIr = DdfIrLowerer.Lower(profileSource,
                    profileParse, loaded.Package.Language).Program;
                Equal(42L, Convert.ToInt64(DdfInterpreter.Execute(profileSource,
                    profileIr, loaded.Package.Language).ReturnValue));
            }
        }

        private static void ValidatesActiveEnumAndSelectionAcceptanceMatrix()
        {
            DdfLanguagePackage package = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;

            DdfTypeCheckResult Check(string source, out DdfParseResult parse)
            {
                parse = DdfParser.Parse(source, package.Language);
                return DdfTypeChecker.Check(source, parse.Root,
                    typeSystem: package.Language.Types,
                    standardLibrary: package.Language.StandardLibrary,
                    runtime: package.Language.Runtime);
            }

            const string exhaustiveBoolean =
                "main() out int { bool enabled << false; select(enabled) { " +
                "when true { ret 1; } when false { ret 42; } } }";
            DdfTypeCheckResult booleanTypes = Check(exhaustiveBoolean,
                out DdfParseResult booleanParse);
            Equal(0, booleanParse.Diagnostics.Count);
            Equal(0, booleanTypes.Diagnostics.Count);
            DdfIrProgram booleanIr = DdfIrLowerer.Lower(exhaustiveBoolean,
                booleanParse, package.Language).Program;
            Equal(42L, Convert.ToInt64(DdfInterpreter.Execute(exhaustiveBoolean,
                booleanIr, package.Language).ReturnValue));

            const string incompleteEnum =
                "enum Color { Red, Green } main() out int { " +
                "Color selected << Color.Red; select(selected) { " +
                "when Color.Red { ret 1; } } }";
            DdfTypeCheckResult incompleteTypes = Check(incompleteEnum, out _);
            Equal(1, incompleteTypes.Diagnostics.Count(item =>
                item.Code == "DDF328"));

            const string duplicateEnumCase =
                "enum Color { Red, Green } main() out int { " +
                "Color selected << Color.Red; select(selected) { " +
                "when Color.Red { ret 1; } when Color.Red { ret 2; } " +
                "when Color.Green { ret 3; } } }";
            DdfTypeCheckResult duplicateTypes = Check(duplicateEnumCase, out _);
            Equal(1, duplicateTypes.Diagnostics.Count(item =>
                item.Code == "DDF327"));

            const string invalidCaseWithFallback =
                "enum Color { Red } enum State { Red } main() out int { " +
                "Color selected << Color.Red; select(selected) { " +
                "when State.Red { ret 1; } otherwise { ret 42; } } }";
            DdfTypeCheckResult fallbackTypes = Check(invalidCaseWithFallback,
                out _);
            Equal(1, fallbackTypes.Diagnostics.Count(item =>
                item.Code == "DDF326"));
            Equal(0, fallbackTypes.Diagnostics.Count(item =>
                item.Code == "DDF328"));

            const string nonConstantCaseWithFallback =
                "main() out int { int selected << 1; int candidate << 1; " +
                "select(selected) { when candidate { ret 1; } " +
                "otherwise { ret 42; } } }";
            DdfTypeCheckResult nonConstantTypes = Check(
                nonConstantCaseWithFallback, out _);
            Equal(1, nonConstantTypes.Diagnostics.Count(item =>
                item.Code == "DDF326"));

            const string incompleteSyntax =
                "enum Mode { Base, main() out int { select(true) { " +
                "when true { ret 1; }";
            DdfParseResult recoveredParse = DdfParser.Parse(incompleteSyntax,
                package.Language);
            Equal(true, recoveredParse.Diagnostics.Count > 0);
            Equal(true, recoveredParse.Root.Length <= incompleteSyntax.Length);
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

        private static void OwnsTransactionalStatePerEditorHostInstance()
        {
            const string source =
                "state int counter << 0; main() out int { counter++; if(counter == 4) { ret 1 / 0; } ret counter; }";
            var leftDocuments = new OpenDocumentCollection();
            leftDocuments.CreateVirtual("state.ddf", source);
            var rightDocuments = new OpenDocumentCollection();
            rightDocuments.CreateVirtual("state.ddf", source);
            using var left = new DdfEditorExecutionService(leftDocuments,
                new DdfLanguagePackageSession());
            using var right = new DdfEditorExecutionService(rightDocuments,
                new DdfLanguagePackageSession());

            Equal(1, left.RunAsync().GetAwaiter().GetResult().ExecutionResult.ReturnValue);
            Equal(2, left.RunAsync().GetAwaiter().GetResult().ExecutionResult.ReturnValue);
            Equal(1, right.RunAsync().GetAwaiter().GetResult().ExecutionResult.ReturnValue);

            IDdfEditorScriptStateService leftState = left;
            Equal(1, leftState.CaptureScriptState().Count);
            Equal(2, leftState.CaptureScriptState().Single().Value);

            DdfEditorExecutionCompletion simulated = left.RunAsync(
                new DdfEditorExecutionRequest(mode: DdfEditorExecutionMode.Simulation))
                .GetAwaiter().GetResult();
            Equal(true, simulated.Succeeded);
            Equal(3, simulated.ExecutionResult.ReturnValue);
            Equal(2, leftState.CaptureScriptState().Single().Value);

            Equal(3, left.RunAsync().GetAwaiter().GetResult().ExecutionResult.ReturnValue);

            DdfEditorExecutionCompletion failed = left.RunAsync()
                .GetAwaiter().GetResult();
            Equal(false, failed.Succeeded);
            Equal(3, leftState.CaptureScriptState().Single().Value);

            leftState.ResetScriptState();
            Equal(0, leftState.CaptureScriptState().Count);
            Equal(1, left.RunAsync().GetAwaiter().GetResult().ExecutionResult.ReturnValue);

            var persisted = new TestScriptStatePersistenceProvider();
            IDdfEditorScriptStatePersistenceService persistence = left;
            persistence.AttachScriptStatePersistenceProvider(persisted);
            persistence.SavePersistedScriptState();
            Equal(1, persisted.SaveCount);
            Equal(DdfEditorScriptStateSnapshot.CurrentSchemaVersion,
                persisted.State.SchemaVersion);
            Equal("ddf", persisted.State.LanguageId);
            Equal(1, persisted.State.Entries.Count);
            var memoryProvider = new DdfInMemoryEditorScriptStatePersistenceProvider();
            memoryProvider.Save(persisted.State);
            Equal(persisted.State, memoryProvider.Load());
            memoryProvider.Clear();
            Equal(null, memoryProvider.Load());
            Equal(2, left.RunAsync().GetAwaiter().GetResult().ExecutionResult.ReturnValue);
            Equal(1, persisted.SaveCount);
            Equal(true, persistence.RestorePersistedScriptState());
            Equal(1, leftState.CaptureScriptState().Single().Value);
            Equal(2, left.RunAsync().GetAwaiter().GetResult().ExecutionResult.ReturnValue);

            persisted.State = new DdfEditorScriptStateSnapshot("0.9",
                "ddf", "0.12.5", persisted.State.Entries);
            Equal(true, persistence.RestorePersistedScriptState());
            Equal(1, persisted.MigrationCount);
            Equal(1, leftState.CaptureScriptState().Single().Value);
            persistence.ResetPersistedScriptState();
            Equal(1, persisted.ClearCount);
            Equal(0, leftState.CaptureScriptState().Count);
            Equal(1, left.RunAsync().GetAwaiter().GetResult().ExecutionResult.ReturnValue);

            var incompatible = new NonMigratingScriptStatePersistenceProvider(
                new DdfEditorScriptStateSnapshot("0.9", "ddf", "0.12.5",
                    persisted.LastSaved.Entries));
            persistence.AttachScriptStatePersistenceProvider(incompatible);
            Throws<InvalidOperationException>(() =>
                persistence.RestorePersistedScriptState());
            Equal(1, leftState.CaptureScriptState().Single().Value);
            Equal(true, persistence.DetachScriptStatePersistenceProvider());
            Equal(null, persistence.ScriptStatePersistenceProvider);
            Throws<InvalidOperationException>(() =>
                persistence.SavePersistedScriptState());
            Equal(1, leftState.CaptureScriptState().Single().Value);

            var sharedDocuments = new OpenDocumentCollection();
            IDdfEditorDocument sharedDocument = sharedDocuments.CreateVirtual("shared.ddf",
                "state int counter << 0;\nmain() out int\n{\n    counter++;\n    ret counter;\n}");
            var sharedSession = new DdfLanguagePackageSession();
            var sharedStore = new DdfStateStore();
            using var sharedExecution = new DdfEditorExecutionService(
                () => sharedDocuments, () => sharedSession,
                scriptStateStore: sharedStore);
            using var sharedDebug = new DdfEditorDebugService(
                () => sharedDocuments, () => sharedSession,
                scriptStateStore: sharedStore);
            Equal(1, sharedExecution.RunAsync().GetAwaiter().GetResult()
                .ExecutionResult.ReturnValue);
            using var statePause = new SemaphoreSlim(0);
            sharedDebug.SetBreakpoints(new[]
            {
                new DdfEditorBreakpoint(sharedDocument.Id, 4)
            });
            sharedDebug.Paused += (sender, args) => statePause.Release();
            Task<DdfEditorExecutionCompletion> debugExecution = sharedDebug.RunAsync();
            Equal(true, statePause.Wait(TimeSpan.FromSeconds(5)));
            Equal(1, sharedDebug.CurrentSnapshot.States.Count);
            DdfEditorDebugVariableSnapshot debugState = sharedDebug.CurrentSnapshot.States.Single();
            Equal("counter", debugState.Name);
            Equal("1", debugState.DisplayValue);
            Equal(DdfRuntimeVariableKind.State, debugState.Kind);
            Equal(true, debugState.IsTransactionalState);
            DdfEditorWatchResult stateWatch = sharedDebug.EvaluateWatch("counter");
            Equal(true, stateWatch.Available);
            Equal("1", stateWatch.Value.DisplayValue);
            Equal(true, sharedDebug.Continue());
            Equal(2, debugExecution.GetAwaiter().GetResult().ExecutionResult.ReturnValue);
            Equal(2, ((IDdfEditorScriptStateService)sharedExecution)
                .CaptureScriptState().Single().Value);

            var persistedAcrossInstances = new TestScriptStatePersistenceProvider();
            DdfEditorScriptStateSnapshot savedSnapshot;
            var reopenDocuments = new OpenDocumentCollection();
            reopenDocuments.CreateVirtual("reopen.ddf",
                "state int counter << 0; main() out int { counter++; ret counter; }");
            using (var firstInstance = new DdfEditorExecutionService(reopenDocuments,
                       new DdfLanguagePackageSession()))
            {
                Equal(1, firstInstance.RunAsync().GetAwaiter().GetResult()
                    .ExecutionResult.ReturnValue);
                IDdfEditorScriptStatePersistenceService firstPersistence = firstInstance;
                firstPersistence.AttachScriptStatePersistenceProvider(
                    persistedAcrossInstances);
                firstPersistence.SavePersistedScriptState();
                savedSnapshot = persistedAcrossInstances.State;
                Equal(1, savedSnapshot.Entries.Single().Value);
                Equal(2, firstInstance.RunAsync().GetAwaiter().GetResult()
                    .ExecutionResult.ReturnValue);
                Equal(1, savedSnapshot.Entries.Single().Value);
            }

            using (var reopenedInstance = new DdfEditorExecutionService(reopenDocuments,
                       new DdfLanguagePackageSession()))
            {
                IDdfEditorScriptStateService reopenedState = reopenedInstance;
                IDdfEditorScriptStatePersistenceService reopenedPersistence =
                    reopenedInstance;
                Equal(0, reopenedState.CaptureScriptState().Count);
                reopenedPersistence.AttachScriptStatePersistenceProvider(
                    persistedAcrossInstances);
                Equal(true, reopenedPersistence.RestorePersistedScriptState());
                Equal(1, reopenedState.CaptureScriptState().Single().Value);
                Equal(2, reopenedInstance.RunAsync().GetAwaiter().GetResult()
                    .ExecutionResult.ReturnValue);
                Equal(true, reopenedPersistence.DetachScriptStatePersistenceProvider());
                Throws<InvalidOperationException>(() =>
                    reopenedPersistence.RestorePersistedScriptState());
                Equal(2, reopenedState.CaptureScriptState().Single().Value);
            }

            using var parallelEntered = new CountdownEvent(2);
            var parallelRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var parallelCatalog = new DdfEditorHostCatalog();
            parallelCatalog.RegisterFunction(new DdfHostFunctionDefinition(
                "holdParallel", "int", Array.Empty<DdfHostParameterDefinition>(),
                invoke: null, invokeAsync: async (context, arguments) =>
                {
                    parallelEntered.Signal();
                    await parallelRelease.Task.ConfigureAwait(false);
                    return 0;
                }));
            const string parallelSource =
                "state int counter << 0; main() out int { counter++; holdParallel(); ret counter; }";
            var parallelLeftDocuments = new OpenDocumentCollection();
            parallelLeftDocuments.CreateVirtual("parallel-left.ddf", parallelSource);
            var parallelRightDocuments = new OpenDocumentCollection();
            parallelRightDocuments.CreateVirtual("parallel-right.ddf", parallelSource);
            using var parallelLeft = new DdfEditorExecutionService(
                () => parallelLeftDocuments, () => new DdfLanguagePackageSession(),
                () => parallelCatalog.Snapshot);
            using var parallelRight = new DdfEditorExecutionService(
                () => parallelRightDocuments, () => new DdfLanguagePackageSession(),
                () => parallelCatalog.Snapshot);
            Task<DdfEditorExecutionCompletion> parallelLeftRun = parallelLeft.RunAsync();
            Task<DdfEditorExecutionCompletion> parallelRightRun = parallelRight.RunAsync();
            Equal(true, parallelEntered.Wait(TimeSpan.FromSeconds(5)));
            parallelRelease.TrySetResult(null);
            Equal(1, parallelLeftRun.GetAwaiter().GetResult().ExecutionResult.ReturnValue);
            Equal(1, parallelRightRun.GetAwaiter().GetResult().ExecutionResult.ReturnValue);
            Equal(1, ((IDdfEditorScriptStateService)parallelLeft)
                .CaptureScriptState().Single().Value);
            Equal(1, ((IDdfEditorScriptStateService)parallelRight)
                .CaptureScriptState().Single().Value);

            using var contendedEntered = new ManualResetEventSlim();
            var contendedRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var contendedCatalog = new DdfEditorHostCatalog();
            contendedCatalog.RegisterFunction(new DdfHostFunctionDefinition(
                "holdInstance", "int", Array.Empty<DdfHostParameterDefinition>(),
                invoke: null, invokeAsync: async (context, arguments) =>
                {
                    contendedEntered.Set();
                    await contendedRelease.Task.ConfigureAwait(false);
                    return 0;
                }));
            var contendedDocuments = new OpenDocumentCollection();
            contendedDocuments.CreateVirtual("contended.ddf",
                "state int counter << 0; main() out int { counter++; holdInstance(); ret counter; }");
            using var contendedInstance = new DdfEditorExecutionService(
                () => contendedDocuments, () => new DdfLanguagePackageSession(),
                () => contendedCatalog.Snapshot);
            Task<DdfEditorExecutionCompletion> contendedRun = contendedInstance.RunAsync();
            Equal(true, contendedEntered.Wait(TimeSpan.FromSeconds(5)));
            Throws<InvalidOperationException>(() => contendedInstance.RunAsync());
            contendedRelease.TrySetResult(null);
            Equal(1, contendedRun.GetAwaiter().GetResult().ExecutionResult.ReturnValue);
            Equal(1, ((IDdfEditorScriptStateService)contendedInstance)
                .CaptureScriptState().Single().Value);
        }

        private static void GovernsHostExecutionSchedulers()
        {
            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("main.ddf", "main() out int { ret 42; }");
            var language = new DdfLanguagePackageSession();
            int callingThread = Environment.CurrentManagedThreadId;
            var scheduler = new RecordingExecutionScheduler();
            using (var execution = new DdfEditorExecutionService(
                       () => documents, () => language,
                       executionScheduler: scheduler))
            {
                DdfEditorExecutionCompletion completion = execution.RunAsync()
                    .GetAwaiter().GetResult();
                Equal(true, completion.Succeeded);
                Equal(42L, Convert.ToInt64(
                    completion.ExecutionResult.ReturnValue));
                Equal(1, scheduler.ScheduleCount);
                Equal(false, scheduler.ExecutionThreadId == callingThread);
                Equal(DdfEditorExecutionState.Idle, execution.State);
            }

            using (var rejected = new DdfEditorExecutionService(
                       () => documents, () => language,
                       executionScheduler: new InlineExecutionScheduler()))
            {
                DdfEditorExecutionCompletion completion = rejected.RunAsync()
                    .GetAwaiter().GetResult();
                Equal(false, completion.Succeeded);
                Equal(true, completion.InternalError.Contains(
                    "thread chiamante/UI", StringComparison.Ordinal));
                Equal(DdfEditorExecutionState.Idle, rejected.State);
            }
        }

        private static void RoutesOnlyUiCapabilitiesThroughHostDispatcher()
        {
            Throws<ArgumentException>(() => new DdfHostFunctionDefinition(
                "invalidUi", "int", Array.Empty<DdfHostParameterDefinition>(),
                arguments => 0, dispatchTarget: DdfHostFunctionDispatchTarget.Ui));

            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterPermission(new DdfHostPermissionDefinition(
                "ui.test", "Test UI"));
            Equal(true, catalog.SetPermission("ui.test", true));
            int workerFunctionThread = 0;
            int uiFunctionThread = 0;
            using var uiStarted = new ManualResetEventSlim();
            var uiRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "workerValue", "int", Array.Empty<DdfHostParameterDefinition>(),
                arguments =>
                {
                    workerFunctionThread = Environment.CurrentManagedThreadId;
                    return 20;
                }));
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "uiValue", "int", Array.Empty<DdfHostParameterDefinition>(),
                invoke: null, requiredPermissions: new[] { "ui.test" },
                invokeAsync: (context, arguments) =>
                {
                    uiFunctionThread = Environment.CurrentManagedThreadId;
                    uiStarted.Set();
                    return new ValueTask<object>(uiRelease.Task);
                },
                dispatchTarget: DdfHostFunctionDispatchTarget.Ui));

            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("ui.ddf",
                "main() out int { ret workerValue() + uiValue(); }");
            var language = new DdfLanguagePackageSession();
            var scheduler = new RecordingExecutionScheduler();
            using var dispatcher = new DedicatedEventDispatcher();
            using (var execution = new DdfEditorExecutionService(
                       () => documents, () => language, () => catalog.Snapshot,
                       eventDispatcher: dispatcher,
                       executionScheduler: scheduler))
            {
                Task<DdfEditorExecutionCompletion> run = execution.RunAsync();
                Equal(true, uiStarted.Wait(TimeSpan.FromSeconds(5)));
                Equal(true, SpinWait.SpinUntil(() =>
                    execution.CooperativeSnapshot.IsSuspended,
                    TimeSpan.FromSeconds(5)));
                Equal("ui.test", execution.CooperativeSnapshot
                    .PendingSuspension.CapabilityId);
                uiRelease.SetResult(22);
                DdfEditorExecutionCompletion completion = run.GetAwaiter()
                    .GetResult();
                Equal(true, completion.Succeeded);
                Equal(42L, Convert.ToInt64(
                    completion.ExecutionResult.ReturnValue));
                Equal(dispatcher.ThreadId, uiFunctionThread);
                Equal(false, uiFunctionThread == workerFunctionThread);
            }

            uiFunctionThread = 0;
            using (var debugger = new DdfEditorDebugService(
                       () => documents, () => language, () => catalog.Snapshot,
                       eventDispatcher: dispatcher,
                       executionScheduler: scheduler))
            {
                DdfEditorExecutionCompletion completion = debugger.RunAsync()
                    .GetAwaiter().GetResult();
                Equal(true, completion.Succeeded);
                Equal(dispatcher.ThreadId, uiFunctionThread);
            }

            var manualDispatcher = new ManualEventDispatcher();
            using var invoker = new DdfEditorUiCapabilityInvoker(manualDispatcher);
            using var cancellation = new CancellationTokenSource();
            bool presented = false;
            ValueTask<int> pending = invoker.InvokeAsync("ui.cancel", token =>
            {
                presented = true;
                return ValueTask.FromResult(1);
            }, cancellation.Token);
            cancellation.Cancel();
            manualDispatcher.Drain();
            Throws<OperationCanceledException>(() => pending.AsTask()
                .GetAwaiter().GetResult());
            Equal(false, presented);
            Throws<ArgumentException>(() => invoker.InvokeAsync("network.http",
                token => ValueTask.FromResult(1), CancellationToken.None));
        }

        private static void ObservesAsynchronousStopAndDisposalLifecycles()
        {
            var entered = new ManualResetEventSlim();
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "waitForStop", "int", Array.Empty<DdfHostParameterDefinition>(),
                invoke: null, invokeAsync: async (context, arguments) =>
                {
                    entered.Set();
                    await Task.Delay(TimeSpan.FromMinutes(1),
                        context.CancellationToken);
                    return 1;
                }));
            var documents = new OpenDocumentCollection();
            IDdfEditorDocument document = documents.CreateVirtual("lifecycle.ddf",
                "main() out int { ret waitForStop(); }");
            var language = new DdfLanguagePackageSession();
            using (var execution = new DdfEditorExecutionService(
                       () => documents, () => language, () => catalog.Snapshot))
            {
                Task<DdfEditorExecutionCompletion> running = execution.RunAsync();
                Equal(true, entered.Wait(TimeSpan.FromSeconds(5)));
                DdfEditorLifecycleResult stopped = execution.StopAsync(
                    TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                Equal(DdfEditorLifecycleOutcome.Completed, stopped.Outcome);
                Equal(true, stopped.Completion.WasCancelled);
                Equal(DdfEditorExecutionState.Idle, execution.State);
                Equal(stopped, execution.LastLifecycleResult);
                Equal(stopped.Completion, running.GetAwaiter().GetResult());
            }

            entered.Reset();
            using (var debugger = new DdfEditorDebugService(
                       () => documents, () => language, () => catalog.Snapshot))
            {
                Task<DdfEditorExecutionCompletion> running = debugger.RunAsync();
                Equal(true, entered.Wait(TimeSpan.FromSeconds(5)));
                DdfEditorLifecycleResult stopped = debugger.StopAsync(
                    TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                Equal(DdfEditorLifecycleOutcome.Completed, stopped.Outcome);
                Equal(true, stopped.Completion.WasCancelled);
                Equal(DdfEditorDebugState.Idle, debugger.State);
                Equal(stopped.Completion, running.GetAwaiter().GetResult());
            }

            var cleanupStarted = new ManualResetEventSlim();
            var cleanupRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupCatalog = new DdfEditorHostCatalog();
            cleanupCatalog.RegisterFunction(new DdfHostFunctionDefinition(
                "acquireSlowCleanup", "int",
                Array.Empty<DdfHostParameterDefinition>(), invoke: null,
                invokeAsync: (context, arguments) =>
                {
                    context.RegisterAsyncResource(
                        new ControllableAsyncHostResource(cleanupStarted,
                            cleanupRelease.Task));
                    return ValueTask.FromResult<object>(1);
                }));
            documents.UpdateDocument(document.Id,
                "main() out int { ret acquireSlowCleanup(); }");
            using (var execution = new DdfEditorExecutionService(
                       () => documents, () => language,
                       () => cleanupCatalog.Snapshot))
            {
                Task<DdfEditorExecutionCompletion> running = execution.RunAsync();
                Equal(true, cleanupStarted.Wait(TimeSpan.FromSeconds(5)));
                DdfEditorLifecycleResult timedOut = execution.StopAsync(
                    TimeSpan.FromMilliseconds(50)).GetAwaiter().GetResult();
                Equal(DdfEditorLifecycleOutcome.TimedOut, timedOut.Outcome);
                Equal("DDFH005", timedOut.ErrorCode);
                Equal(false, timedOut.Succeeded);
                cleanupRelease.SetResult(null);
                running.GetAwaiter().GetResult();
                Equal(DdfEditorExecutionState.Idle, execution.State);
            }

            var dispatcher = new ManualEventDispatcher();
            int callbacks = 0;
            documents.UpdateDocument(document.Id, "main() out int { ret 1; }");
            var disposableExecution = new DdfEditorExecutionService(
                () => documents, () => language,
                eventDispatcher: dispatcher);
            disposableExecution.StateChanged += (sender, args) => callbacks++;
            disposableExecution.ExecutionCompleted +=
                (sender, args) => callbacks++;
            disposableExecution.RunAsync().GetAwaiter().GetResult();
            DdfEditorLifecycleResult disposed = disposableExecution.DisposeAsync(
                TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Equal(DdfEditorLifecycleOutcome.AlreadyIdle, disposed.Outcome);
            dispatcher.Drain();
            Equal(0, callbacks);
            Throws<ObjectDisposedException>(() => disposableExecution.RunAsync());
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
                new[] { "math.use" })
                .DeclaresRecoverableErrors(DdfErrorCatalog.InvalidInput));

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
                Equal(true, function.Detail.Contains(
                    "operation.invalidInput (DDF408)"));
                Equal(true, snapshot.Completions.Items.Any(item =>
                    item.DisplayText == "threshold" && item.TypeName == "int"));
            }

            int hostArgument = source.IndexOf("threshold",
                StringComparison.Ordinal) + 1;
            DdfSignatureHelpResult hostSignature =
                DdfSignatureHelpService.GetSignatureHelp(source, hostArgument,
                    language: package.Language,
                    hostCatalog: deniedSnapshot);
            Equal("hostScale(int value) out int",
                hostSignature.Signature.Signature);
            Equal("operation.invalidInput (DDF408)",
                hostSignature.Signature.ErrorSummary.Substring(
                    "Errori recuperabili: ".Length));
            DdfHoverInfo hostHover = DdfHoverService.CreateForHostFunction(
                deniedSnapshot.Functions.Single(item =>
                    item.Name == "hostScale"));
            Equal(true, hostHover.Documentation.Contains(
                "operation.invalidInput (DDF408)"));

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

        private static void SeparatesTypedRecoverableErrorsFromEngineFaults()
        {
            Equal(true, DdfErrorCatalog.InvalidInput.IsRecoverable);
            Equal(DdfErrorKind.EngineFault,
                DdfErrorCatalog.InstructionLimit.Kind);
            Equal(DdfErrorKind.EngineFault,
                DdfErrorCatalog.CapabilityUnavailable.Kind);
            Equal(DdfErrorKind.EngineFault,
                DdfErrorCatalog.ExecutionCancelled.Kind);
            Equal(DdfErrorKind.EngineFault,
                DdfErrorCatalog.SecurityViolation.Kind);
            Equal(DdfErrorKind.EngineFault,
                DdfErrorCatalog.InvalidArtifact.Kind);
            Equal(true, DdfErrorCatalog.TryGetByCode("DDF408",
                out DdfErrorDefinition inputError));
            Equal("operation.invalidInput", inputError.TypeId);
            Equal(true, DdfErrorCatalog.TryGetByTypeId(
                "engine.executionCancelled", out DdfErrorDefinition cancelled));
            Equal("DDF424", cancelled.Code);

            string[] recoverableCodes = DdfErrorCatalog.All
                .Where(item => item.IsRecoverable)
                .Select(item => item.Code).OrderBy(item => item,
                    StringComparer.Ordinal).ToArray();
            SequenceEqual(new[]
            {
                "DDF404", "DDF405", "DDF408", "DDF417", "DDF418", "DDF419"
            }, recoverableCodes);
            Equal(25, DdfErrorCatalog.All.Count);
            Equal(25, DdfErrorCatalog.All.Select(item => item.Code)
                .Distinct(StringComparer.Ordinal).Count());
            Equal(25, DdfErrorCatalog.All.Select(item => item.TypeId)
                .Distinct(StringComparer.Ordinal).Count());

            var frame = new DdfErrorStackFrame("parse", "lib.ddf", 12, 3);
            var value = new DdfErrorValue(inputError, "Valore non valido.",
                "main.ddf", 4, 5, new[] { frame });
            Equal("DDF408", value.Code);
            Equal("operation.invalidInput", value.TypeId);
            Equal("parse", value.Stack.Single().FunctionName);
            Equal("Errori recuperabili: operation.invalidInput (DDF408)",
                DdfErrorPresentation.FormatDeclarations(new[] { inputError }));

            DdfHostFunctionDefinition hostFunction =
                new DdfHostFunctionDefinition("parseValue", "int",
                    new[] { new DdfHostParameterDefinition("text", "string") },
                    arguments => 0)
                .DeclaresRecoverableErrors(inputError);
            Equal(inputError, hostFunction.RecoverableErrors.Single());
            Throws<ArgumentException>(() => hostFunction
                .DeclaresRecoverableErrors(DdfErrorCatalog.InstructionLimit));

            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterFunction(hostFunction);
            Equal(inputError, catalog.Snapshot.Functions.Single()
                .RecoverableErrors.Single());

            DdfExternalApiFunctionDefinition externalFunction =
                new DdfExternalApiFunctionDefinition("parseValue",
                    "sample.parse", "int",
                    new[] { new DdfExternalApiParameterDefinition("text", "string") })
                .DeclaresRecoverableErrors(inputError);
            var declared = new DdfExternalApiDefinition("sample.errors",
                "1.0.0", functions: new[] { externalFunction });
            var undeclared = new DdfExternalApiDefinition("sample.errors",
                "1.0.0", functions: new[]
                {
                    new DdfExternalApiFunctionDefinition("parseValue",
                        "sample.parse", "int",
                        new[] { new DdfExternalApiParameterDefinition(
                            "text", "string") })
                });
            Equal("1.1", DdfExternalApiDefinition.FormatVersion);
            False(declared.Fingerprint == undeclared.Fingerprint);
            Equal(inputError, DdfExternalApiCatalogProjector
                .CreateAnalysisCatalog(declared).Functions.Single()
                .RecoverableErrors.Single());

            DdfStandardFunction toInt = DdfRuntimeCatalog.StandardFunctions
                .Single(item => item.Name == "toInt");
            Equal(inputError, toInt.ErrorDefinitions.Single());
        }

        private static void TransportsTypedErrorsAcrossVmAndHostBoundaries()
        {
            const string source =
                "divide() out int\n{\n int zero << 0;\n ret 10 / zero;\n}\n" +
                "main() out int\n{\n ret divide();\n}";
            DdfExecutionResult runtimeFailure = DdfInterpreter.Execute(source);
            Equal(false, runtimeFailure.Succeeded);
            Equal("arithmetic.divisionZero", runtimeFailure.Error.TypeId);
            Equal("DDF404", runtimeFailure.Error.Code);
            Equal(DdfErrorKind.Recoverable, runtimeFailure.Error.Kind);
            Equal(runtimeFailure.Diagnostics.Single().Start,
                runtimeFailure.Error.Start);
            Equal(2, runtimeFailure.Error.Stack.Count);
            Equal("divide", runtimeFailure.Error.Stack[0].FunctionName);
            Equal(4, runtimeFailure.Error.Stack[0].Line);
            Equal("main", runtimeFailure.Error.Stack[1].FunctionName);

            DdfExecutionResult cancelled = DdfInterpreter.Execute(
                "main() out void { while(true) { } }",
                new DdfExecutionOptions { CancellationRequested = () => true });
            Equal(true, cancelled.WasCancelled);
            Equal("DDF424", cancelled.Error.Code);
            Equal(DdfErrorKind.EngineFault, cancelled.Error.Kind);
            Equal(0, cancelled.Diagnostics.Count);

            const string hostSource =
                "main() out int { ret parseHost(\"invalid\"); }";
            DdfHostFunctionDefinition declaredFunction =
                new DdfHostFunctionDefinition("parseHost", "int",
                    new[] { new DdfHostParameterDefinition("text", "string") },
                    arguments => throw new DdfHostErrorException(
                        DdfErrorCatalog.InvalidInput,
                        "Il valore host non è valido."))
                .DeclaresRecoverableErrors(DdfErrorCatalog.InvalidInput);
            var declaredCatalog = new DdfEditorHostCatalog();
            declaredCatalog.RegisterFunction(declaredFunction);
            DdfExecutionResult declaredFailure = DdfInterpreter.Execute(
                hostSource, new DdfExecutionOptions
                { HostCatalog = declaredCatalog.Snapshot });
            Equal("DDF408", declaredFailure.Error.Code);
            Equal("operation.invalidInput", declaredFailure.Error.TypeId);
            Equal("Il valore host non è valido.", declaredFailure.Error.Message);
            Equal("parseHost", declaredFailure.Error.Stack[0].FunctionName);

            var undeclaredCatalog = new DdfEditorHostCatalog();
            undeclaredCatalog.RegisterFunction(new DdfHostFunctionDefinition(
                "parseHost", "int",
                new[] { new DdfHostParameterDefinition("text", "string") },
                arguments => throw new DdfHostErrorException(
                    DdfErrorCatalog.InvalidInput,
                    "Errore non dichiarato.")));
            DdfExecutionResult undeclaredFailure = DdfInterpreter.Execute(
                hostSource, new DdfExecutionOptions
                { HostCatalog = undeclaredCatalog.Snapshot });
            Equal("DDF410", undeclaredFailure.Error.Code);
            Equal("engine.hostAdapterFailure", undeclaredFailure.Error.TypeId);
            Equal(DdfErrorKind.EngineFault, undeclaredFailure.Error.Kind);
        }

        private static void KeepsFutureErrorHandlingDesignExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-error-handling-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-error-handling-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-language-version-policy.json"),
                Encoding.UTF8)).AsObject();
            JsonObject lexicon = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "lexicon.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0029", design["decisionId"].GetValue<string>());
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(policy["languageVersion"]
                .GetValue<string>()));
            Equal("object", schema["type"].GetValue<string>());

            string[] plannedKeywords = design["keywords"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            Equal(6, plannedKeywords.Length);
            Equal(6, plannedKeywords.Distinct(StringComparer.Ordinal).Count());
            string[] activeKeywords = lexicon["keywords"].AsArray()
                .Select(item => item.AsObject()["text"].GetValue<string>())
                .ToArray();
            Equal(false, plannedKeywords.Any(keyword => activeKeywords.Contains(
                keyword, StringComparer.Ordinal)));

            JsonObject rules = design["rules"].AsObject();
            Equal(true, rules.All(item => item.Value.GetValue<bool>()));
            Equal(true, design["grammar"].AsObject()["functionErrors"]
                .GetValue<string>().Contains("raises", StringComparison.Ordinal));
            Equal(true, design["grammar"].AsObject()["raiseStatement"]
                .GetValue<string>().Contains("raise", StringComparison.Ordinal));

            Equal(true, DdfErrorCatalog.All.All(item => Regex.IsMatch(item.TypeId,
                "^[a-z][A-Za-z0-9]*(?:\\.[a-z][A-Za-z0-9]*)+$")));

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_ERROR_HANDLING_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("DDF 0.14", StringComparison.Ordinal));
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, decisions.Contains("DDF-0029", StringComparison.Ordinal));
        }

        private static void KeepsFutureOptionalValueDesignExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-optional-values-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-optional-values-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-language-version-policy.json"),
                Encoding.UTF8)).AsObject();
            JsonObject lexicon = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "lexicon.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0030", design["decisionId"].GetValue<string>());
            Equal("0.15", design["targetLanguageVersion"].GetValue<string>());
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(policy["languageVersion"]
                .GetValue<string>()));
            Equal("object", schema["type"].GetValue<string>());

            string[] plannedKeywords = design["keywords"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            SequenceEqual(new[] { "maybe", "some", "none" }, plannedKeywords);
            string[] activeKeywords = lexicon["keywords"].AsArray()
                .Select(item => item.AsObject()["text"].GetValue<string>())
                .ToArray();
            Equal(false, plannedKeywords.Any(keyword => activeKeywords.Contains(
                keyword, StringComparer.Ordinal)));

            JsonObject rules = design["rules"].AsObject();
            Equal(true, rules.All(item => item.Value.GetValue<bool>()));
            Equal(true, design["grammar"].AsObject()["optionalType"]
                .GetValue<string>().Contains("maybe", StringComparison.Ordinal));
            Equal(true, design["grammar"].AsObject()["whenSomeClause"]
                .GetValue<string>().Contains("some", StringComparison.Ordinal));
            Equal(true, design["grammar"].AsObject()["whenNoneClause"]
                .GetValue<string>().Contains("none", StringComparison.Ordinal));

            const string currentSource =
                "maybe() out int { ret 40; } " +
                "some() out int { ret 2; } " +
                "none() out int { ret maybe() + some(); } " +
                "main() out int { ret none(); }";
            DdfLexResult lexed = DdfLexer.Lex(currentSource);
            Equal(0, lexed.Diagnostics.Count);
            Equal(true, lexed.Tokens.Where(item => plannedKeywords.Contains(
                currentSource.Substring(item.Start, item.Length),
                StringComparer.Ordinal)).All(item =>
                    item.Kind == DdfTokenKind.Identifier));
            DdfParseResult parsed = DdfParser.Parse(currentSource, lexed);
            Equal(0, parsed.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(currentSource, parsed.Root).Diagnostics.Count);
            DdfExecutionResult executed = DdfInterpreter.Execute(currentSource);
            Equal(true, executed.Succeeded);
            Equal(42, executed.ReturnValue);

            const string inactiveCollectionSyntax =
                "main() out int { sequence<int> values; " +
                "map<string, int> scores; view<int> snapshot; ret 0; }";
            DdfParseResult inactiveCollections = DdfParser.Parse(
                inactiveCollectionSyntax);
            Equal(true, inactiveCollections.Diagnostics.Count > 0 ||
                DdfTypeChecker.Check(inactiveCollectionSyntax,
                    inactiveCollections.Root).Diagnostics.Count > 0);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_OPTIONAL_VALUES_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("DDF 0.15", StringComparison.Ordinal));
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, decisions.Contains("DDF-0030", StringComparison.Ordinal));
        }

        private static void KeepsFutureModuleQualificationDesignExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-module-qualification-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-module-qualification-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-language-version-policy.json"),
                Encoding.UTF8)).AsObject();
            JsonObject lexicon = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "lexicon.json"),
                Encoding.UTF8)).AsObject();
            JsonObject projectSchema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-project.schema.json"), Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0031", design["decisionId"].GetValue<string>());
            Equal("0.16", design["targetLanguageVersion"].GetValue<string>());
            Equal("1.3", design["targetProjectManifestVersion"].GetValue<string>());
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(policy["languageVersion"]
                .GetValue<string>()));
            Equal("object", schema["type"].GetValue<string>());

            string token = design["qualificationToken"].GetValue<string>();
            Equal("::", token);
            string[] activeOperators = lexicon["operators"].AsArray()
                .Select(item => item.AsObject()["text"].GetValue<string>())
                .ToArray();
            string[] activePunctuation = lexicon["punctuation"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            Equal(false, activeOperators.Contains(token, StringComparer.Ordinal));
            Equal(false, activePunctuation.Contains(token, StringComparer.Ordinal));

            JsonObject rules = design["rules"].AsObject();
            Equal(true, rules.All(item => item.Value.GetValue<bool>()));
            JsonObject manifestRules = design["manifest"].AsObject();
            Equal("alias", manifestRules["dependencyAliasProperty"]
                .GetValue<string>());
            Equal(true, manifestRules["aliasRequired"].GetValue<bool>());
            Equal(7, design["diagnostics"].AsArray().Count);
            Equal(true, design["grammar"].AsObject().All(item =>
                item.Value.GetValue<string>().Contains("::", StringComparison.Ordinal)));

            Equal("1.2", projectSchema["properties"].AsObject()
                ["schemaVersion"].AsObject()["const"].GetValue<string>());
            JsonObject dependencyProperties = projectSchema["properties"].AsObject()
                ["dependencies"].AsObject()["items"].AsObject()
                ["properties"].AsObject();
            Equal(false, dependencyProperties.ContainsKey("alias"));
            Equal(null, typeof(DdfProjectDependency).GetProperty("Alias"));

            const string inactiveQualifiedSource =
                "main() out int { ret math::add(20, 22); }";
            DdfLexResult inactiveQualifiedLex = DdfLexer.Lex(
                inactiveQualifiedSource);
            Equal(true, inactiveQualifiedLex.Diagnostics.Count > 0);
            DdfParseResult inactiveQualifiedParse = DdfParser.Parse(
                inactiveQualifiedSource, inactiveQualifiedLex);
            Equal(true, inactiveQualifiedParse.Diagnostics.Count > 0 ||
                inactiveQualifiedLex.Diagnostics.Count > 0);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_MODULE_QUALIFICATION_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("DDF 0.16", StringComparison.Ordinal));
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, document.Contains("manifesto progetto `1.3`",
                StringComparison.Ordinal));
            Equal(true, decisions.Contains("DDF-0031", StringComparison.Ordinal));
        }

        private static void KeepsFutureTypedIterationDesignExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-typed-iteration-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-typed-iteration-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-language-version-policy.json"),
                Encoding.UTF8)).AsObject();
            JsonObject lexicon = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "lexicon.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0032", design["decisionId"].GetValue<string>());
            Equal("0.17", design["targetLanguageVersion"].GetValue<string>());
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(policy["languageVersion"]
                .GetValue<string>()));
            Equal("object", schema["type"].GetValue<string>());

            string[] plannedKeywords = design["keywords"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            SequenceEqual(new[] { "each", "in" }, plannedKeywords);
            string[] activeKeywords = lexicon["keywords"].AsArray()
                .Select(item => item.AsObject()["text"].GetValue<string>())
                .ToArray();
            Equal(false, plannedKeywords.Any(keyword => activeKeywords.Contains(
                keyword, StringComparer.Ordinal)));

            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(true, design["integration"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal("array", design["collectionKinds"].AsObject()["initial"]
                .AsArray().Single().GetValue<string>());
            Equal("ascending-index", design["snapshot"].AsObject()["arrayOrder"]
                .GetValue<string>());
            Equal("shallow", design["snapshot"].AsObject()["depth"]
                .GetValue<string>());
            Equal(5, design["diagnostics"].AsArray().Count);

            const string currentSource =
                "each() out int { ret 40; } " +
                "in() out int { ret 2; } " +
                "main() out int { ret each() + in(); }";
            DdfLexResult lexed = DdfLexer.Lex(currentSource);
            Equal(0, lexed.Diagnostics.Count);
            Equal(true, lexed.Tokens.Where(item => plannedKeywords.Contains(
                currentSource.Substring(item.Start, item.Length),
                StringComparer.Ordinal)).All(item =>
                    item.Kind == DdfTokenKind.Identifier));
            DdfParseResult parsed = DdfParser.Parse(currentSource, lexed);
            Equal(0, parsed.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(currentSource, parsed.Root).Diagnostics.Count);
            DdfExecutionResult executed = DdfInterpreter.Execute(currentSource);
            Equal(true, executed.Succeeded);
            Equal(42, executed.ReturnValue);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_TYPED_ITERATION_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("DDF 0.17", StringComparison.Ordinal));
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, decisions.Contains("DDF-0032", StringComparison.Ordinal));
        }

        private static void KeepsFutureTypedCollectionDesignExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-typed-collections-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-typed-collections-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-language-version-policy.json"),
                Encoding.UTF8)).AsObject();
            JsonObject lexicon = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "lexicon.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0033", design["decisionId"].GetValue<string>());
            Equal("0.18", design["targetLanguageVersion"].GetValue<string>());
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(policy["languageVersion"]
                .GetValue<string>()));
            Equal("object", schema["type"].GetValue<string>());

            string[] plannedKeywords = design["keywords"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            SequenceEqual(new[] { "sequence", "map", "view", "entry" },
                plannedKeywords);
            string[] activeKeywords = lexicon["keywords"].AsArray()
                .Select(item => item.AsObject()["text"].GetValue<string>())
                .ToArray();
            Equal(false, plannedKeywords.Any(keyword => activeKeywords.Contains(
                keyword, StringComparer.Ordinal)));

            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(true, design["viewContract"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(true, design["integration"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal("sequence<T>", design["typeForms"].AsObject()["sequence"]
                .GetValue<string>());
            Equal("map<K,V>", design["typeForms"].AsObject()["map"]
                .GetValue<string>());
            SequenceEqual(new[] { "int", "char", "bool", "string", "enum" },
                design["mapKeyKinds"].AsArray().Select(item =>
                    item.GetValue<string>()));
            Equal(6, design["diagnostics"].AsArray().Count);

            const string currentSource =
                "sequence() out int { ret 10; } " +
                "map() out int { ret 11; } " +
                "view() out int { ret 12; } " +
                "entry() out int { ret 9; } " +
                "main() out int { ret sequence() + map() + view() + entry(); }";
            DdfLexResult lexed = DdfLexer.Lex(currentSource);
            Equal(0, lexed.Diagnostics.Count);
            Equal(true, lexed.Tokens.Where(item => plannedKeywords.Contains(
                currentSource.Substring(item.Start, item.Length),
                StringComparer.Ordinal)).All(item =>
                    item.Kind == DdfTokenKind.Identifier));
            DdfParseResult parsed = DdfParser.Parse(currentSource, lexed);
            Equal(0, parsed.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(currentSource, parsed.Root).Diagnostics.Count);
            DdfExecutionResult executed = DdfInterpreter.Execute(currentSource);
            Equal(true, executed.Succeeded);
            Equal(42, executed.ReturnValue);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_TYPED_COLLECTIONS_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("DDF 0.18", StringComparison.Ordinal));
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, decisions.Contains("DDF-0033", StringComparison.Ordinal));
        }

        private static void KeepsFutureExtendedNumericDesignExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-extended-numerics-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-extended-numerics-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-language-version-policy.json"),
                Encoding.UTF8)).AsObject();
            JsonObject lexicon = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "lexicon.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0034", design["decisionId"].GetValue<string>());
            Equal("0.19", design["targetLanguageVersion"].GetValue<string>());
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(policy["languageVersion"]
                .GetValue<string>()));
            Equal("object", schema["type"].GetValue<string>());

            string[] plannedKeywords = design["keywords"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            SequenceEqual(new[] { "long", "decimal" }, plannedKeywords);
            string[] activeKeywords = lexicon["keywords"].AsArray()
                .Select(item => item.AsObject()["text"].GetValue<string>())
                .ToArray();
            Equal(false, plannedKeywords.Any(keyword => activeKeywords.Contains(
                keyword, StringComparer.Ordinal)));

            JsonObject types = design["types"].AsObject();
            Equal("i64", types["long"].AsObject()["literalSuffix"]
                .GetValue<string>());
            Equal("dec", types["decimal"].AsObject()["literalSuffix"]
                .GetValue<string>());
            Equal(96, types["decimal"].AsObject()["coefficientBits"]
                .GetValue<int>());
            Equal(28, types["decimal"].AsObject()["maximumScale"]
                .GetValue<int>());
            Equal("nearest-ties-to-even", types["decimal"].AsObject()["rounding"]
                .GetValue<string>());
            SequenceEqual(new[] { "int->long", "int->decimal", "long->decimal" },
                design["implicitConversions"].AsArray().Select(item =>
                    item.GetValue<string>()));
            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(true, design["integration"].AsObject()["invariantFormatting"]
                .GetValue<bool>());
            Equal(true, design["integration"].AsObject()["vmAndRoslynMustBeEquivalent"]
                .GetValue<bool>());
            Equal(5, design["diagnostics"].AsArray().Count);

            const string currentSource =
                "long() out int { ret 20; } " +
                "decimal() out int { ret 22; } " +
                "main() out int { ret long() + decimal(); }";
            DdfLexResult lexed = DdfLexer.Lex(currentSource);
            Equal(0, lexed.Diagnostics.Count);
            Equal(true, lexed.Tokens.Where(item => plannedKeywords.Contains(
                currentSource.Substring(item.Start, item.Length),
                StringComparer.Ordinal)).All(item =>
                    item.Kind == DdfTokenKind.Identifier));
            DdfParseResult parsed = DdfParser.Parse(currentSource, lexed);
            Equal(0, parsed.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(currentSource, parsed.Root).Diagnostics.Count);
            DdfExecutionResult executed = DdfInterpreter.Execute(currentSource);
            Equal(true, executed.Succeeded);
            Equal(42, executed.ReturnValue);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_EXTENDED_NUMERICS_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("DDF 0.19", StringComparison.Ordinal));
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, decisions.Contains("DDF-0034", StringComparison.Ordinal));
        }

        private static void KeepsFutureStringLibraryDesignExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-string-library-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-string-library-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-language-version-policy.json"),
                Encoding.UTF8)).AsObject();
            JsonObject standardLibrary = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "standard-library.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0035", design["decisionId"].GetValue<string>());
            Equal("0.20", design["targetLanguageVersion"].GetValue<string>());
            Equal("0.20.0", design["targetStandardLibraryVersion"]
                .GetValue<string>());
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(policy["languageVersion"]
                .GetValue<string>()));
            Equal("object", schema["type"].GetValue<string>());
            Equal("+", design["concatenationOperator"].GetValue<string>());

            string[] plannedFunctions = design["functions"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            Equal(19, plannedFunctions.Length);
            Equal(19, plannedFunctions.Distinct(StringComparer.Ordinal).Count());
            string[] activeFunctions = standardLibrary["functions"].AsArray()
                .Select(item => item.AsObject()["name"].GetValue<string>())
                .ToArray();
            Equal(false, plannedFunctions.Any(function => activeFunctions.Contains(
                function, StringComparer.Ordinal)));

            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal("15.1", design["unicode"].AsObject()["version"]
                .GetValue<string>());
            Equal(true, design["unicode"].AsObject()["cultureIndependent"]
                .GetValue<bool>());
            Equal(true, design["format"].AsObject()
                .Where(item => item.Value is JsonValue &&
                    (item.Key.EndsWith("Forbidden", StringComparison.Ordinal) ||
                     item.Key == "reflectionForbidden"))
                .All(item => item.Value.GetValue<bool>()));
            Equal(true, design["integration"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(5, design["diagnostics"].AsArray().Count);

            const string futureConcatenation =
                "main() out string { ret \"DDF\" + \" Editor\"; }";
            DdfParseResult parsed = DdfParser.Parse(futureConcatenation);
            Equal(0, parsed.Diagnostics.Count);
            Equal(true, DdfTypeChecker.Check(futureConcatenation, parsed.Root)
                .Diagnostics.Any());

            const string currentIdentifier =
                "contains() out int { ret 42; } main() out int { ret contains(); }";
            DdfExecutionResult executed = DdfInterpreter.Execute(currentIdentifier);
            Equal(true, executed.Succeeded);
            Equal(42, executed.ReturnValue);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_STRING_LIBRARY_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("DDF 0.20", StringComparison.Ordinal));
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, decisions.Contains("DDF-0035", StringComparison.Ordinal));
        }

        private static void KeepsFutureMathLibraryDesignExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-math-library-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-math-library-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-language-version-policy.json"),
                Encoding.UTF8)).AsObject();
            JsonObject extendedNumerics = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-extended-numerics-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject standardLibrary = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "standard-library.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0036", design["decisionId"].GetValue<string>());
            Equal("0.21", design["targetLanguageVersion"].GetValue<string>());
            Equal("0.21.0", design["targetStandardLibraryVersion"]
                .GetValue<string>());
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(policy["languageVersion"]
                .GetValue<string>()));
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(extendedNumerics[
                "targetLanguageVersion"].GetValue<string>()));
            Equal("object", schema["type"].GetValue<string>());
            Equal("ddf.math.transcendental.v1", design[
                "transcendentalAlgorithm"].GetValue<string>());
            Equal("radian", design["angleUnit"].GetValue<string>());

            string[] plannedFunctions = design["functions"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            Equal(35, plannedFunctions.Length);
            Equal(35, plannedFunctions.Distinct(StringComparer.Ordinal).Count());
            string[] activeFunctions = standardLibrary["functions"].AsArray()
                .Select(item => item.AsObject()["name"].GetValue<string>())
                .ToArray();
            Equal(false, plannedFunctions.Any(function => activeFunctions.Contains(
                function, StringComparer.Ordinal)));

            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal("400921fb54442d18", design["constants"].AsObject()[
                "piBinary64Hex"].GetValue<string>());
            Equal("4005bf0a8b145769", design["constants"].AsObject()[
                "eBinary64Hex"].GetValue<string>());
            Equal(true, design["integration"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(5, design["diagnostics"].AsArray().Count);

            const string currentIdentifier =
                "absInt() out int { ret 42; } main() out int { ret absInt(); }";
            DdfExecutionResult executed = DdfInterpreter.Execute(currentIdentifier);
            Equal(true, executed.Succeeded);
            Equal(42, executed.ReturnValue);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_MATH_LIBRARY_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("DDF 0.21", StringComparison.Ordinal));
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, decisions.Contains("DDF-0036", StringComparison.Ordinal));
        }

        private static void KeepsFutureTemporalRandomDesignExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-temporal-random-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-temporal-random-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-language-version-policy.json"),
                Encoding.UTF8)).AsObject();
            JsonObject stringDesign = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-string-library-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject standardLibrary = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "standard-library.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0037", design["decisionId"].GetValue<string>());
            Equal("0.22", design["targetLanguageVersion"].GetValue<string>());
            Equal("0.22.0", design["targetStandardLibraryVersion"]
                .GetValue<string>());
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(policy["languageVersion"]
                .GetValue<string>()));
            Equal(true, Version.Parse(design["targetLanguageVersion"]
                .GetValue<string>()) > Version.Parse(stringDesign[
                "targetLanguageVersion"].GetValue<string>()));
            Equal("object", schema["type"].GetValue<string>());
            Equal(100, design["resolutionNanoseconds"].GetValue<int>());
            Equal("proleptic-gregorian", design["calendar"].GetValue<string>());

            string[] plannedTypes = design["types"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            Equal(5, plannedTypes.Length);
            Equal(5, plannedTypes.Distinct(StringComparer.Ordinal).Count());
            string[] plannedFunctions = design["functions"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            Equal(40, plannedFunctions.Length);
            Equal(40, plannedFunctions.Distinct(StringComparer.Ordinal).Count());
            string[] activeFunctions = standardLibrary["functions"].AsArray()
                .Select(item => item.AsObject()["name"].GetValue<string>())
                .ToArray();
            Equal(false, plannedFunctions.Any(function => activeFunctions.Contains(
                function, StringComparer.Ordinal)));

            Equal("ddf.clock.v1", design["providers"].AsObject()["clock"]
                .GetValue<string>());
            Equal("ddf.random.v1", design["providers"].AsObject()["random"]
                .GetValue<string>());
            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(true, design["integration"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(7, design["diagnostics"].AsArray().Count);

            const string currentIdentifiers =
                "date() out int { ret 1; } " +
                "time() out int { ret 2; } " +
                "datetime() out int { ret 3; } " +
                "instant() out int { ret 4; } " +
                "duration() out int { ret 5; } " +
                "main() out int { ret date() + time() + datetime() + instant() + duration(); }";
            DdfExecutionResult executed = DdfInterpreter.Execute(currentIdentifiers);
            Equal(true, executed.Succeeded);
            Equal(15, executed.ReturnValue);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_TEMPORAL_RANDOM_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("DDF 0.22", StringComparison.Ordinal));
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, decisions.Contains("DDF-0037", StringComparison.Ordinal));
        }

        private static void KeepsFutureStandardLibraryBindingContractExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-standard-library-binding-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-standard-library-binding-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject current = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "standard-library.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0038", design["decisionId"].GetValue<string>());
            Equal("2.0", design["futureSectionSchemaVersion"].GetValue<string>());
            Equal("ddf-standard-library-v4", design["futureAdapter"]
                .GetValue<string>());
            Equal("0.20", design["activationBeforeLanguageVersion"]
                .GetValue<string>());
            Equal("object", schema["type"].GetValue<string>());

            JsonObject currentContract = design["currentContract"].AsObject();
            Equal(current["schemaVersion"].GetValue<string>(), currentContract[
                "sectionSchemaVersion"].GetValue<string>());
            Equal(current["adapter"].GetValue<string>(), currentContract[
                "adapter"].GetValue<string>());
            Equal(current["version"].GetValue<string>(), currentContract[
                "libraryVersion"].GetValue<string>());
            Equal("1.1", current["schemaVersion"].GetValue<string>());
            Equal("ddf-standard-library-v3", current["adapter"].GetValue<string>());

            Equal(17, design["requiredFunctionFields"].AsArray().Count);
            Equal(5, design["bindingFields"].AsArray().Count);
            Equal(5, design["effectKinds"].AsArray().Count);
            Equal(16, design["consumers"].AsArray().Count);
            Equal(6, design["loaderDiagnostics"].AsArray().Count);
            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));

            var identityPattern = new Regex(design["functionIdentityPattern"]
                .GetValue<string>(), RegexOptions.CultureInvariant);
            Equal(true, identityPattern.IsMatch(
                "ddf.standard/string/contains@1"));
            Equal(false, identityPattern.IsMatch("System.String.Contains"));
            Equal(false, design["portabilityKinds"].AsArray()
                .Select(item => item.GetValue<string>())
                .Contains("platform-bound", StringComparer.Ordinal));

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_STANDARD_LIBRARY_BINDING_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, document.Contains("schema `2.0`", StringComparison.Ordinal));
            Equal(true, decisions.Contains("DDF-0038", StringComparison.Ordinal));
        }

        private static void KeepsFutureHostDialogContractExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-host-dialog-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-host-dialog-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject standardLibrary = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "standard-library.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0039", design["decisionId"].GetValue<string>());
            Equal("0.13", design["minimumLanguageVersion"].GetValue<string>());
            Equal("0.38", design["targetEditorSdkContract"].GetValue<string>());
            Equal("ui.dialog.message", design["capabilityId"].GetValue<string>());
            Equal("ddf.ui.dialog.message.v1", design["providerContract"]
                .GetValue<string>());
            Equal("object", schema["type"].GetValue<string>());

            JsonObject function = design["function"].AsObject();
            Equal("dialogMessage", function["name"].GetValue<string>());
            Equal(4, function["parameters"].AsArray().Count);
            Equal("dialogResult", function["returnType"].GetValue<string>());
            Equal("ui", function["effect"].GetValue<string>());
            Equal(4, design["enums"].AsObject()["dialogButtons"].AsArray().Count);
            Equal(5, design["enums"].AsObject()["dialogIcon"].AsArray().Count);
            Equal(4, design["enums"].AsObject()["dialogResult"].AsArray().Count);
            Equal(1, design["defaultLimits"].AsObject()[
                "maximumPendingPerInstance"].GetValue<int>());
            Equal(5, design["recoverableErrors"].AsArray().Count);
            Equal(11, design["requestFields"].AsArray().Count);
            Equal(3, design["responseFields"].AsArray().Count);
            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(true, design["integration"].AsObject().All(item =>
                item.Value.GetValue<bool>()));

            Equal(false, standardLibrary["functions"].AsArray()
                .Select(item => item.AsObject()["name"].GetValue<string>())
                .Contains("dialogMessage", StringComparer.Ordinal));
            const string currentIdentifier =
                "dialogMessage() out int { ret 42; } " +
                "main() out int { ret dialogMessage(); }";
            DdfExecutionResult executed = DdfInterpreter.Execute(currentIdentifier);
            Equal(true, executed.Succeeded);
            Equal(42, executed.ReturnValue);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_HOST_DIALOG_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, document.Contains("ui.dialog.message",
                StringComparison.Ordinal));
            Equal(true, decisions.Contains("DDF-0039", StringComparison.Ordinal));
        }

        private static void KeepsFutureProjectTestingContractExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-project-testing-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-project-testing-design.schema.json"),
                Encoding.UTF8)).AsObject();
            JsonObject packageTests = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "tests.json"),
                Encoding.UTF8)).AsObject();
            JsonObject standardLibrary = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "language-packages", "ddf", "standard-library.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0040", design["decisionId"].GetValue<string>());
            Equal("0.39", design["targetEditorSdkContract"].GetValue<string>());
            Equal("ddf-test-plan@1.0", design["planContract"].GetValue<string>());
            Equal("ddf-project-tests-v1", design["adapter"].GetValue<string>());
            Equal("ddf-test-report@1.0", design["reportContract"].GetValue<string>());
            Equal("object", schema["type"].GetValue<string>());

            Equal("1.0", packageTests["schemaVersion"].GetValue<string>());
            Equal("ddf-tests-v1", packageTests["adapter"].GetValue<string>());
            Equal(packageTests["adapter"].GetValue<string>(), design[
                "currentLanguagePackageTestAdapter"].GetValue<string>());

            JsonObject testLibrary = design["testLibrary"].AsObject();
            Equal("ddf.testing@1", testLibrary["identity"].GetValue<string>());
            string[] testFunctions = testLibrary["functions"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            Equal(11, testFunctions.Length);
            Equal(11, testFunctions.Distinct(StringComparer.Ordinal).Count());
            Equal(128, testLibrary["maximumFailuresPerCase"].GetValue<int>());
            Equal(false, standardLibrary["functions"].AsArray()
                .Select(item => item.AsObject()["name"].GetValue<string>())
                .Any(name => testFunctions.Contains(name, StringComparer.Ordinal)));

            Equal(3, design["expectedOutcomeKinds"].AsArray().Count);
            Equal(5, design["caseStatusKinds"].AsArray().Count);
            Equal(9, design["fixtureFields"].AsArray().Count);
            Equal(10, design["runnerDiagnostics"].AsArray().Count);
            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(true, design["integration"].AsObject().All(item =>
                item.Value.GetValue<bool>()));

            const string currentIdentifier =
                "assertTrue() out int { ret 42; } " +
                "main() out int { ret assertTrue(); }";
            DdfExecutionResult executed = DdfInterpreter.Execute(currentIdentifier);
            Equal(true, executed.Succeeded);
            Equal(42, executed.ReturnValue);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_PROJECT_TESTING_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("accettato, non ancora implementato",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, document.Contains("ddf-tests-v1",
                StringComparison.Ordinal));
            Equal(true, decisions.Contains("DDF-0040", StringComparison.Ordinal));
        }

        private static void KeepsFutureResourceLifetimeContractExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-resource-lifetime-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-resource-lifetime-design.schema.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0041", design["decisionId"].GetValue<string>());
            Equal("0.23", design["targetLanguageVersion"].GetValue<string>());
            Equal("0.40", design["targetEditorSdkContract"].GetValue<string>());
            Equal("ddf-resource-descriptor@1.0",
                design["resourceDescriptorContract"].GetValue<string>());
            Equal("ddf.resource.provider.v1",
                design["runtimeProviderContract"].GetValue<string>());
            Equal("use", design["syntax"].AsObject()["keyword"]
                .GetValue<string>());
            Equal(false, design["syntax"].AsObject()["genericDeferSupported"]
                .GetValue<bool>());
            Equal(10, design["cleanupTriggers"].AsArray().Count);
            Equal(10, design["diagnostics"].AsArray().Count);
            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(true, design["integration"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal("object", schema["type"].GetValue<string>());

            const string currentIdentifier =
                "use() out int { ret 42; } " +
                "main() out int { ret use(); }";
            DdfExecutionResult executed = DdfInterpreter.Execute(currentIdentifier);
            Equal(true, executed.Succeeded);
            Equal(42, executed.ReturnValue);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_RESOURCE_LIFETIME_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains(
                "fondazione SDK implementata, sintassi inattiva",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, document.Contains("defer", StringComparison.Ordinal));
            Equal(true, decisions.Contains("DDF-0041", StringComparison.Ordinal));
        }

        private static void KeepsFutureCooperativeAsyncContractExplicitAndInactive()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject design = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-cooperative-async-design.json"),
                Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-cooperative-async-design.schema.json"),
                Encoding.UTF8)).AsObject();

            Equal("1.0", design["schemaVersion"].GetValue<string>());
            Equal("accepted-design-not-implemented",
                design["status"].GetValue<string>());
            Equal("DDF-0042", design["decisionId"].GetValue<string>());
            Equal("0.24", design["targetLanguageVersion"].GetValue<string>());
            Equal("0.41", design["targetEditorSdkContract"].GetValue<string>());
            Equal("ddf-cooperative-suspension@1.0",
                design["suspensionDescriptorContract"].GetValue<string>());
            Equal("ddf.async-capability-provider.v1",
                design["runtimeProviderContract"].GetValue<string>());
            Equal(0, design["syntax"].AsObject()["newKeywords"].AsArray().Count);
            Equal(false, design["syntax"].AsObject()["asyncIdentifierReserved"]
                .GetValue<bool>());
            Equal(false, design["syntax"].AsObject()["awaitIdentifierReserved"]
                .GetValue<bool>());
            Equal(5, design["executionStates"].AsArray().Count);
            Equal(4, design["irOperations"].AsArray().Count);
            Equal(10, design["diagnostics"].AsArray().Count);
            Equal(true, design["rules"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal(true, design["integration"].AsObject().All(item =>
                item.Value.GetValue<bool>()));
            Equal("object", schema["type"].GetValue<string>());

            const string currentIdentifiers =
                "async() out int { ret 40; } " +
                "await(int value) out int { ret value + 2; } " +
                "main() out int { ret await(async()); }";
            DdfExecutionResult executed = DdfInterpreter.Execute(currentIdentifiers);
            Equal(true, executed.Succeeded);
            Equal(42, executed.ReturnValue);

            string document = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_COOPERATIVE_ASYNC_DESIGN.md"), Encoding.UTF8);
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);
            Equal(true, document.Contains("infrastruttura in implementazione",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, document.Contains("awaiting-capability",
                StringComparison.Ordinal));
            Equal(true, decisions.Contains("DDF-0042", StringComparison.Ordinal));
        }

        private static void GuardsCooperativeSuspensionGenerationsAndLateResults()
        {
            var coordinator = new DdfCooperativeExecutionCoordinator();
            Guid firstExecution = Guid.NewGuid();
            DdfCooperativeExecutionSnapshot started = coordinator.Begin(firstExecution);
            Equal(1L, started.Generation);
            Equal(firstExecution, started.ExecutionId);
            Equal(DdfCooperativeExecutionState.Running, started.State);

            var mutableMap = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["count"] = 2,
                ["values"] = new List<object> { 3, 4 }
            };
            var mutableArguments = new List<object> { 1, mutableMap };
            DateTimeOffset createdAt = new DateTimeOffset(
                2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
            DateTimeOffset executionDeadline = createdAt.AddSeconds(30);
            DateTimeOffset operationDeadline = createdAt.AddSeconds(5);
            DateTimeOffset hostDeadline = createdAt.AddSeconds(10);
            DateTimeOffset? deadline =
                DdfCooperativeExecutionCoordinator.EarliestDeadline(
                    executionDeadline, operationDeadline, hostDeadline);
            using var cancellation = new CancellationTokenSource();
            DdfCooperativeSuspensionRequest request = coordinator.Suspend(
                "database.query", "customers.count", mutableArguments,
                "main.ddf", 25, 18, "transaction-1", createdAt, deadline,
                cancellation.Token);

            Equal(DdfCooperativeExecutionState.AwaitingCapability,
                coordinator.Snapshot.State);
            Equal(operationDeadline, request.Deadline.Value);
            Equal("database.query", request.CapabilityId);
            Equal("customers.count", request.OperationId);
            Equal(true, request.CancellationToken.CanBeCanceled);
            mutableArguments[0] = 99;
            mutableMap["count"] = 99;
            Equal(1, request.Arguments[0]);
            var frozenMap = (IReadOnlyDictionary<string, object>)request.Arguments[1];
            Equal(2, frozenMap["count"]);

            var foreign = new DdfCooperativeSuspensionResult(firstExecution,
                started.Generation, Guid.NewGuid(),
                DdfCooperativeSuspensionResultKind.Succeeded, 42);
            Equal(false, coordinator.TryResume(foreign, out _));
            Equal(DdfCooperativeExecutionState.AwaitingCapability,
                coordinator.Snapshot.State);

            var accepted = new DdfCooperativeSuspensionResult(firstExecution,
                started.Generation, request.CorrelationId,
                DdfCooperativeSuspensionResultKind.Succeeded, 42);
            int acceptedCount = 0;
            Parallel.For(0, 8, iteration =>
            {
                if (coordinator.TryResume(accepted,
                    out DdfCooperativeExecutionSnapshot resumedSnapshot))
                    Interlocked.Increment(ref acceptedCount);
            });
            Equal(1, acceptedCount);
            Equal(DdfCooperativeExecutionState.Running, coordinator.Snapshot.State);
            Equal(null, coordinator.Snapshot.PendingSuspension);

            DdfCooperativeSuspensionRequest cancelledRequest = coordinator.Suspend(
                "network.send", "http.request", new object[] { "payload" },
                "main.ddf", 50, 12, null, createdAt, executionDeadline,
                cancellation.Token);
            Equal(true, coordinator.RequestCancellation(started.Generation));
            Equal(DdfCooperativeExecutionState.Cancelling,
                coordinator.Snapshot.State);
            var late = new DdfCooperativeSuspensionResult(firstExecution,
                started.Generation, cancelledRequest.CorrelationId,
                DdfCooperativeSuspensionResultKind.Succeeded, "late");
            Equal(false, coordinator.TryResume(late, out _));

            coordinator.BeginCleanup(started.Generation);
            Equal(DdfCooperativeExecutionState.CleaningUp,
                coordinator.Snapshot.State);
            coordinator.Complete(started.Generation);
            Equal(DdfCooperativeExecutionState.Idle, coordinator.Snapshot.State);
            Equal(Guid.Empty, coordinator.Snapshot.ExecutionId);

            DdfCooperativeExecutionSnapshot second = coordinator.Begin(Guid.NewGuid());
            Equal(2L, second.Generation);
            Equal(false, coordinator.RequestCancellation(started.Generation));
            Throws<InvalidOperationException>(() => coordinator.Begin(Guid.NewGuid()));
            Throws<ArgumentException>(() => coordinator.Suspend(
                "host.invalid", "opaque", new object[] { new object() },
                string.Empty, 0, 0, null, createdAt, executionDeadline,
                CancellationToken.None));
            coordinator.BeginCleanup(second.Generation);
            coordinator.Complete(second.Generation);
        }

        private static void EnforcesHostValueAdaptersAccessAndLifetimes()
        {
            var catalog = new DdfEditorHostCatalog();
            int snapshotReads = 0;
            int snapshotWrites = 0;
            string snapshotBacking = "5";
            var integerTextAdapter = DdfHostValueAdapter.Create<string, int>(
                "int", value => int.Parse(value,
                    System.Globalization.CultureInfo.InvariantCulture),
                value => Convert.ToString(value,
                    System.Globalization.CultureInfo.InvariantCulture));
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
                        integerTextAdapter)
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

        private enum HostAdapterMode
        {
            Ready,
            Busy,
            Unmapped
        }

        private static void EnforcesStrictTypedHostAdapters()
        {
            DdfHostValueAdapter scalar = DdfHostValueAdapter.Create<string, int>(
                "int", value => int.Parse(value, CultureInfo.InvariantCulture),
                value => value.ToString(CultureInfo.InvariantCulture));
            Equal(DdfHostValueAdapterKind.Scalar, scalar.Kind);
            Equal("int", scalar.ScriptTypeName);
            Equal(true, scalar.IsTyped);
            Equal(12, scalar.ToScriptValue("12"));
            Equal("7", scalar.FromScriptValue(7));
            Throws<InvalidOperationException>(() => scalar.ToScriptValue(12));
            Throws<InvalidOperationException>(() => scalar.FromScriptValue(7L));
            Throws<InvalidOperationException>(() => scalar.ToScriptValue(null));
            Throws<ArgumentException>(() => new DdfHostParameterDefinition(
                "value", "string", valueAdapter: scalar));
            Throws<ArgumentException>(() => new DdfHostObjectDefinition(
                "value", "string", () => "12", valueAdapter: scalar));
            Throws<ArgumentException>(() => new DdfHostFunctionDefinition(
                "value", "string", Array.Empty<DdfHostParameterDefinition>(),
                _ => "12", returnValueAdapter: scalar));

            var enumMembers = new Dictionary<HostAdapterMode, string>
            {
                [HostAdapterMode.Ready] = "Ready",
                [HostAdapterMode.Busy] = "Busy"
            };
            DdfHostValueAdapter enumeration =
                DdfHostValueAdapter.Enumeration("DeviceMode", enumMembers);
            Equal(DdfHostValueAdapterKind.Enumeration, enumeration.Kind);
            Equal("DeviceMode", enumeration.ScriptTypeName);
            Equal(new DdfEnumValue("DeviceMode", "Ready"),
                enumeration.ToScriptValue(HostAdapterMode.Ready));
            Equal(HostAdapterMode.Busy, enumeration.FromScriptValue(
                new DdfEnumValue("DeviceMode", "Busy")));
            Throws<InvalidOperationException>(() => enumeration.ToScriptValue(
                HostAdapterMode.Unmapped));
            Throws<InvalidOperationException>(() => enumeration.FromScriptValue(
                new DdfEnumValue("OtherMode", "Ready")));
            Throws<InvalidOperationException>(() => enumeration.FromScriptValue(
                new DdfEnumValue("DeviceMode", "Missing")));
            Throws<ArgumentException>(() => DdfHostValueAdapter.Enumeration(
                "DeviceMode", new Dictionary<HostAdapterMode, string>
                {
                    [HostAdapterMode.Ready] = "Same",
                    [HostAdapterMode.Busy] = "Same"
                }));

            Equal(DdfHostValueAdapterKind.Legacy,
                new DdfHostValueAdapter(value => value, value => value).Kind);
            Equal(false, DdfHostValueAdapter.Identity.IsTyped);
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

        private static void AuthorizesObservesAndRevokesHostResourceLeases()
        {
            var descriptor = new DdfHostResourceDescriptor(
                "databaseConnection", "database.connection.open",
                "Connessione database", "Lease opaco di test.");
            var granted = new TestResourceLeaseAuthority(true);
            var denied = new TestResourceLeaseAuthority(false);
            var releases = new List<string>();
            DdfHostResourceLease releasedLease = null;
            DdfHostResourceLease revokedLease = null;
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "acquireReleased", "int",
                Array.Empty<DdfHostParameterDefinition>(), null,
                invokeAsync: async (context, arguments) =>
                {
                    releasedLease = await context.RegisterResourceLeaseAsync(
                        descriptor, new TrackingHostResource("released", releases),
                        granted);
                    releasedLease.EnsureAvailable();
                    return 1;
                }));
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "acquireRevoked", "int",
                Array.Empty<DdfHostParameterDefinition>(), null,
                invokeAsync: async (context, arguments) =>
                {
                    revokedLease = await context.RegisterResourceLeaseAsync(
                        descriptor, new TrackingHostResource("revoked", releases),
                        granted);
                    Equal(true, await revokedLease.RevokeAsync("capability revoked"));
                    Equal(false, await revokedLease.RevokeAsync("duplicate"));
                    return 2;
                }));
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "acquireDenied", "int",
                Array.Empty<DdfHostParameterDefinition>(), null,
                invokeAsync: async (context, arguments) =>
                {
                    await context.RegisterResourceLeaseAsync(descriptor,
                        new TrackingHostResource("denied", releases), denied);
                    return 3;
                }));

            var documents = new OpenDocumentCollection();
            IDdfEditorDocument document = documents.CreateVirtual(
                "resource-lease.ddf", "main() out int { ret acquireReleased(); }");
            using var execution = new DdfEditorExecutionService(() => documents,
                () => new DdfLanguagePackageSession(), () => catalog.Snapshot);

            DdfEditorExecutionCompletion released = execution.RunAsync()
                .GetAwaiter().GetResult();
            Equal(true, released.Succeeded);
            Equal(DdfHostResourceLeaseState.Released,
                releasedLease.Snapshot.State);
            Throws<InvalidOperationException>(releasedLease.EnsureAvailable);
            SequenceEqual(new[]
            {
                DdfHostResourceLeaseState.Active,
                DdfHostResourceLeaseState.Released
            }, granted.Snapshots.Select(item => item.State));

            granted.Snapshots.Clear();
            documents.UpdateDocument(document.Id,
                "main() out int { ret acquireRevoked(); }");
            DdfEditorExecutionCompletion revoked = execution.RunAsync()
                .GetAwaiter().GetResult();
            Equal(true, revoked.Succeeded);
            Equal(DdfHostResourceLeaseState.Revoked,
                revokedLease.Snapshot.State);
            Equal("capability revoked", revokedLease.Snapshot.RevocationReason);
            Throws<InvalidOperationException>(revokedLease.EnsureAvailable);
            SequenceEqual(new[]
            {
                DdfHostResourceLeaseState.Active,
                DdfHostResourceLeaseState.Revoked
            }, granted.Snapshots.Select(item => item.State));

            documents.UpdateDocument(document.Id,
                "main() out int { ret acquireDenied(); }");
            DdfEditorExecutionCompletion rejected = execution.RunAsync()
                .GetAwaiter().GetResult();
            Equal(false, rejected.Succeeded);
            Equal(true, rejected.ExecutionResult.Diagnostics.Any(item =>
                item.Code == "DDF409"));
            SequenceEqual(new[] { "released", "revoked", "denied" }, releases);
            Equal(2, granted.Requests.Count);
            Equal(1, denied.Requests.Count);
            Equal(0, denied.Snapshots.Count);
            Equal(true, granted.Snapshots.All(item =>
                item.Descriptor == descriptor && item.ExecutionId != Guid.Empty));
            Equal(false, typeof(DdfHostResourceLeaseSnapshot).GetProperties()
                .Any(property => property.PropertyType == typeof(object) ||
                    typeof(IDisposable).IsAssignableFrom(property.PropertyType) ||
                    typeof(IAsyncDisposable).IsAssignableFrom(property.PropertyType)));

            Throws<ArgumentException>(() => new DdfHostResourceDescriptor(
                "connection", "invalid capability", "Connection"));
            Throws<ArgumentException>(() =>
                DdfHostResourceLeaseDecision.Deny(" "));
        }

        private static void SuspendsHostCallsWithoutOccupyingExecutionWorker()
        {
            var entered = new ManualResetEventSlim();
            var release = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var providerCancelled = new ManualResetEventSlim();
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "delayedValue", "int", Array.Empty<DdfHostParameterDefinition>(),
                null, invokeAsync: async (context, arguments) =>
                {
                    entered.Set();
                    try
                    {
                        return await release.Task.WaitAsync(context.CancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        providerCancelled.Set();
                        throw;
                    }
                }));

            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("cooperative.ddf",
                "main() out int { int value << delayedValue(); ret value + 1; }");
            using var execution = new DdfEditorExecutionService(() => documents,
                () => new DdfLanguagePackageSession(), () => catalog.Snapshot);
            var states = new List<DdfEditorExecutionState>();
            var cooperativeSnapshots =
                new List<DdfEditorCooperativeExecutionSnapshot>();
            var stateSync = new object();
            execution.StateChanged += (sender, args) =>
            {
                lock (stateSync) states.Add(args.State);
            };
            execution.CooperativeStateChanged += (sender, args) =>
            {
                lock (stateSync) cooperativeSnapshots.Add(args.Snapshot);
            };

            Task<DdfEditorExecutionCompletion> running = execution.RunAsync(
                new DdfEditorExecutionRequest(
                    hostCallTimeout: TimeSpan.FromSeconds(5)));
            if (!entered.Wait(TimeSpan.FromSeconds(2)))
                throw new InvalidOperationException(
                    "Il provider sospensivo non è stato invocato.");
            if (!SpinWait.SpinUntil(() => execution.CooperativeSnapshot.State ==
                DdfEditorCooperativeExecutionState.AwaitingCapability,
                TimeSpan.FromSeconds(2)))
                throw new InvalidOperationException(
                    "Lo stato AwaitingCapability non è stato osservato; stato: " +
                    execution.CooperativeSnapshot.State + ".");
            DdfEditorCooperativeExecutionSnapshot suspended =
                execution.CooperativeSnapshot;
            Equal(true, suspended.IsSuspended);
            Equal("host.function", suspended.PendingSuspension.CapabilityId);
            Equal("delayedValue", suspended.PendingSuspension.OperationId);
            Equal(false, suspended.PendingSuspension.CorrelationId == Guid.Empty);
            Equal(true, suspended.PendingSuspension.Deadline.HasValue);
            Equal(false, typeof(DdfEditorCooperativeExecutionSnapshot)
                .GetProperties().Any(property =>
                    typeof(Task).IsAssignableFrom(property.PropertyType) ||
                    property.PropertyType == typeof(CancellationToken)));
            Equal(false, running.IsCompleted);
            release.SetResult(41);
            DdfEditorExecutionCompletion completed = running.GetAwaiter().GetResult();
            if (!completed.Succeeded)
                throw new InvalidOperationException(
                    "La ripresa cooperativa non è riuscita: " +
                    completed.InternalError + " | " + string.Join(" | ",
                        completed.ExecutionResult?.Diagnostics.Select(item =>
                            item.Code + ": " + item.Message) ??
                        Array.Empty<string>()));
            Equal(42, completed.ExecutionResult.ReturnValue);
            Equal(DdfEditorCooperativeExecutionState.Idle,
                execution.CooperativeSnapshot.State);
            lock (stateSync)
            {
                Equal(DdfEditorExecutionState.Idle, states.Last());
                Equal(DdfEditorCooperativeExecutionState.Idle,
                    cooperativeSnapshots.Last().State);
            }

            entered.Reset();
            release = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Task<DdfEditorExecutionCompletion> cancelledRun = execution.RunAsync(
                new DdfEditorExecutionRequest(
                    hostCallTimeout: TimeSpan.FromSeconds(5)));
            if (!entered.Wait(TimeSpan.FromSeconds(2)))
                throw new InvalidOperationException(
                    "Il provider sospensivo non è stato reinvocato.");
            if (!SpinWait.SpinUntil(() => execution.CooperativeSnapshot.State ==
                DdfEditorCooperativeExecutionState.AwaitingCapability,
                TimeSpan.FromSeconds(2)))
                throw new InvalidOperationException(
                    "La seconda attesa non ha esposto AwaitingCapability; stato: " +
                    execution.CooperativeSnapshot.State + ".");
            Equal(true, execution.Stop());
            Equal(true, providerCancelled.Wait(TimeSpan.FromSeconds(2)));
            DdfEditorExecutionCompletion cancelled = cancelledRun
                .GetAwaiter().GetResult();
            Equal(true, cancelled.WasCancelled);
            Equal(DdfEditorExecutionState.Idle, execution.State);
            release.TrySetResult(99);
        }

        private static void KeepsEmbeddedDebuggerSnapshotsWhileAwaitingCapabilities()
        {
            var entered = new ManualResetEventSlim();
            var release = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var catalog = new DdfEditorHostCatalog();
            catalog.RegisterFunction(new DdfHostFunctionDefinition(
                "delayedValue", "int", Array.Empty<DdfHostParameterDefinition>(),
                null, invokeAsync: async (context, arguments) =>
                {
                    entered.Set();
                    return await release.Task.WaitAsync(context.CancellationToken)
                        .ConfigureAwait(false);
                }));

            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("debug-cooperative.ddf", string.Join("\n", new[]
            {
                "main() out int",
                "{",
                "    int seed << 1;",
                "    int value << delayedValue();",
                "    ret value + seed;",
                "}"
            }));
            using var debugger = new DdfEditorDebugService(() => documents,
                () => new DdfLanguagePackageSession(), () => catalog.Snapshot);
            var cooperativeStates =
                new List<DdfEditorCooperativeExecutionState>();
            debugger.CooperativeStateChanged += (sender, args) =>
                cooperativeStates.Add(args.Snapshot.State);

            Task<DdfEditorExecutionCompletion> running = debugger.RunAsync(
                new DdfEditorExecutionRequest(
                    hostCallTimeout: TimeSpan.FromSeconds(5)));
            if (!entered.Wait(TimeSpan.FromSeconds(2)) ||
                !SpinWait.SpinUntil(() => debugger.State ==
                    DdfEditorDebugState.AwaitingCapability,
                    TimeSpan.FromSeconds(2)))
            {
                release.TrySetResult(41);
                DdfEditorExecutionCompletion failed = running.GetAwaiter().GetResult();
                throw new InvalidOperationException(
                    "Il debugger embedded non ha pubblicato l'attesa capability; " +
                    "stato=" + debugger.State + ", errore=" + failed.InternalError +
                    ", diagnostiche=" + string.Join(" | ",
                        failed.ExecutionResult?.Diagnostics.Select(item =>
                            item.Code + ":" + item.Message) ?? Array.Empty<string>()));
            }

            Equal(false, debugger.IsPaused);
            Equal(4, debugger.CurrentLocation.Line);
            Equal("delayedValue", debugger.CooperativeSnapshot
                .PendingSuspension.OperationId);
            Equal(true, debugger.CurrentSnapshot.CallStack.Count > 0);
            DdfEditorWatchResult seed = debugger.EvaluateWatch("seed");
            Equal(true, seed.Available);
            Equal("1", seed.Value.DisplayValue);
            Equal(false, debugger.StepInto());

            release.SetResult(41);
            DdfEditorExecutionCompletion completion = running.GetAwaiter().GetResult();
            Equal(true, completion.Succeeded);
            Equal(42, completion.ExecutionResult.ReturnValue);
            Equal(DdfEditorDebugState.Idle, debugger.State);
            Equal(null, debugger.CurrentSnapshot);
            Equal(true, cooperativeStates.Contains(
                DdfEditorCooperativeExecutionState.AwaitingCapability));
            Equal(DdfEditorCooperativeExecutionState.Idle,
                cooperativeStates.Last());
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

        private static void PreparesImmutableHostScriptsWithoutCompilation()
        {
            var documents = new OpenDocumentCollection();
            IDdfEditorDocument main = documents.CreateVirtual("main.ddf",
                "main() out int { ret helper(20); }");
            documents.CreateVirtual("helpers.ddf",
                "helper(int value) out int { ret value + 22; }");
            var service = new DdfEditorPreparationService(documents,
                new DdfLanguagePackageSession());
            var states = new List<DdfEditorPreparationState>();
            int completions = 0;
            service.StateChanged += (sender, args) => states.Add(args.State);
            service.PreparationCompleted += (sender, args) => completions++;

            DdfEditorPreparationSnapshot validation = service.Validate(
                new DdfEditorPreparationRequest(entryDocumentId: main.Id));
            Equal(true, validation.Succeeded);
            Equal(DdfEditorPreparationMode.Validate, validation.Mode);
            Equal(2, validation.Documents.Count);
            Equal(true, string.IsNullOrEmpty(validation.Result.ArtifactPath));
            Equal(1, completions);

            DdfEditorPreparationSnapshot prepared = service.PrepareScript(
                new DdfEditorPreparationRequest(entryDocumentId: main.Id));
            Equal(true, prepared.Succeeded);
            Equal(DdfEditorPreparationMode.PrepareScript, prepared.Mode);
            Equal(true, prepared.PreparedArtifact != null);
            Equal(true, string.IsNullOrEmpty(prepared.Result.ArtifactPath));
            Equal(2L, prepared.Generation);
            Equal(true, ReferenceEquals(prepared, service.LastSnapshot));
            Equal(DdfEditorPreparationState.Idle, service.State);
            Equal(true, states.Contains(DdfEditorPreparationState.Preparing));
            Equal(2, completions);
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

        private static void KeepsActiveLanguageIncrementsDeclarativelyIntegrated()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            JsonObject contract = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-integration.json"), Encoding.UTF8)).AsObject();
            JsonObject contractSchema = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-integration.schema.json"), Encoding.UTF8)).AsObject();
            JsonObject matrix = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-version-matrix.json"), Encoding.UTF8)).AsObject();
            JsonObject canonicalManifest = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "language-packages", "ddf", "manifest.json"), Encoding.UTF8)).AsObject();

            Equal("1.0", contract["schemaVersion"].GetValue<string>());
            Equal("https://www.ddf.technology/schemas/language-integration-1.0.json",
                contractSchema["$id"].GetValue<string>());
            Equal(matrix["language"]["integrationContractVersion"].GetValue<string>(),
                contract["schemaVersion"].GetValue<string>());
            Equal("ddf-language-integration.json",
                matrix["language"]["integrationContract"].GetValue<string>());
            Equal(canonicalManifest["id"].GetValue<string>(),
                contract["languageId"].GetValue<string>());
            Equal(canonicalManifest["languageVersion"].GetValue<string>(),
                contract["languageVersion"].GetValue<string>());
            Equal(canonicalManifest["packageVersion"].GetValue<string>(),
                contract["packageVersion"].GetValue<string>());

            JsonArray layers = contract["layers"].AsArray();
            string[] layerIds = layers.Select(node =>
                node.AsObject()["id"].GetValue<string>()).ToArray();
            SequenceEqual(new[]
            {
                "ebnf", "formatting", "grammar", "intellisense", "lexicon", "profiles",
                "runtime", "schemas", "semantics", "specification", "standardLibrary",
                "tests", "types"
            }, layerIds.OrderBy(value => value, StringComparer.Ordinal));
            Equal(layerIds.Length, layerIds.Distinct(StringComparer.Ordinal).Count());
            foreach (JsonNode layerNode in layers)
            {
                string relative = layerNode.AsObject()["path"].GetValue<string>();
                string absolute = Path.Combine(projectRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                Equal(true, File.Exists(absolute) || Directory.Exists(absolute));
            }

            JsonArray increments = contract["activeIncrements"].AsArray();
            string[] incrementIds = increments.Select(node =>
                node.AsObject()["id"].GetValue<string>()).ToArray();
            SequenceEqual(new[] { "continue", "enum", "fixed", "select", "state" },
                incrementIds.OrderBy(value => value, StringComparer.Ordinal));
            Equal(incrementIds.Length,
                incrementIds.Distinct(StringComparer.Ordinal).Count());

            JsonObject packageTests = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "language-packages", "ddf", "tests.json"), Encoding.UTF8)).AsObject();
            var packageTestNames = new HashSet<string>(packageTests["cases"].AsArray()
                .Select(node => node.AsObject()["name"].GetValue<string>()), StringComparer.Ordinal);
            JsonObject corpus = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "samples", "language-corpus", "corpus.json"), Encoding.UTF8)).AsObject();
            var corpusValid = new HashSet<string>(corpus["valid"].AsArray()
                .Select(node => node.AsObject()["id"].GetValue<string>()), StringComparer.Ordinal);
            var corpusInvalid = new HashSet<string>(corpus["invalid"].AsArray()
                .Select(node => node.AsObject()["id"].GetValue<string>()), StringComparer.Ordinal);
            var corpusIncomplete = new HashSet<string>(corpus["incomplete"].AsArray()
                .Select(node => node.AsObject()["id"].GetValue<string>()), StringComparer.Ordinal);
            var corpusMultiFile = new HashSet<string>(corpus["multiFile"].AsArray()
                .Select(node => node.AsObject()["id"].GetValue<string>()), StringComparer.Ordinal);

            JsonArray profiles = contract["profiles"].AsArray();
            Equal(1, profiles.Count(node => node.AsObject()["canonical"].GetValue<bool>()));
            SequenceEqual(new[] { "ddf", "ddf-c-familiar", "ddf-it-didactic" },
                profiles.Select(node => node.AsObject()["id"].GetValue<string>())
                    .OrderBy(value => value, StringComparer.Ordinal));

            foreach (JsonNode incrementNode in increments)
            {
                JsonObject increment = incrementNode.AsObject();
                SequenceEqual(layerIds.OrderBy(value => value, StringComparer.Ordinal),
                    increment["requiredLayers"].AsArray().Select(node => node.GetValue<string>())
                        .OrderBy(value => value, StringComparer.Ordinal));
                foreach (JsonNode test in increment["packageTests"].AsArray())
                    Equal(true, packageTestNames.Contains(test.GetValue<string>()));
                foreach (JsonNode test in increment["corpusValid"].AsArray())
                    Equal(true, corpusValid.Contains(test.GetValue<string>()));
                foreach (JsonNode test in increment["corpusInvalid"].AsArray())
                    Equal(true, corpusInvalid.Contains(test.GetValue<string>()));
                foreach (JsonNode test in increment["corpusIncomplete"].AsArray())
                    Equal(true, corpusIncomplete.Contains(test.GetValue<string>()));
                foreach (JsonNode test in increment["corpusMultiFile"].AsArray())
                    Equal(true, corpusMultiFile.Contains(test.GetValue<string>()));
            }

            string specification = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_SPEC.md"), Encoding.UTF8);
            foreach (JsonNode profileNode in profiles)
            {
                JsonObject profile = profileNode.AsObject();
                string profileRoot = Path.Combine(projectRoot,
                    profile["path"].GetValue<string>().Replace('/', Path.DirectorySeparatorChar));
                DdfLanguagePackageLoadResult load = DdfLanguagePackageLoader.Load(profileRoot);
                if (!load.Success)
                    throw new InvalidOperationException(profile["id"].GetValue<string>() + ": " +
                        string.Join(" | ", load.Diagnostics));

                JsonObject lexicon = JsonNode.Parse(File.ReadAllText(Path.Combine(profileRoot,
                    "lexicon.json"), Encoding.UTF8)).AsObject();
                JsonObject grammar = JsonNode.Parse(File.ReadAllText(Path.Combine(profileRoot,
                    "grammar.json"), Encoding.UTF8)).AsObject();
                string ebnf = File.ReadAllText(Path.Combine(profileRoot, "grammar.ebnf"), Encoding.UTF8);
                JsonObject semantics = JsonNode.Parse(File.ReadAllText(Path.Combine(profileRoot,
                    "semantics.json"), Encoding.UTF8)).AsObject();
                JsonObject runtime = JsonNode.Parse(File.ReadAllText(Path.Combine(profileRoot,
                    "runtime.json"), Encoding.UTF8)).AsObject();
                JsonObject intellisense = JsonNode.Parse(File.ReadAllText(Path.Combine(profileRoot,
                    "intellisense.json"), Encoding.UTF8)).AsObject();

                var keywordsByRole = lexicon["keywords"].AsArray().Select(node => node.AsObject())
                    .Where(node => node["role"] != null &&
                        node["role"].GetValue<string>() != "None")
                    .GroupBy(node => node["role"].GetValue<string>(), StringComparer.Ordinal)
                    .ToDictionary(group => group.Key,
                        group => group.Single()["text"].GetValue<string>(), StringComparer.Ordinal);
                var grammarRules = new HashSet<string>(grammar["nodes"].AsArray()
                    .Select(node => node.AsObject()["rule"].GetValue<string>()), StringComparer.Ordinal);
                var semanticOperations = new HashSet<string>(semantics["bindings"].AsArray()
                    .SelectMany(binding => binding.AsObject()["operations"].AsArray())
                    .Select(operation => operation.AsObject()["id"].GetValue<string>()), StringComparer.Ordinal);
                var runtimeOperations = new HashSet<string>(runtime["vmOperations"].AsArray()
                    .Select(node => node.GetValue<string>()), StringComparer.Ordinal);
                var snippetPrefixes = new HashSet<string>(intellisense["snippets"].AsArray()
                    .Select(node => node.AsObject()["prefix"].GetValue<string>()), StringComparer.Ordinal);
                JsonObject diagnostics = intellisense["diagnostics"].AsObject();

                foreach (JsonNode incrementNode in increments)
                {
                    JsonObject increment = incrementNode.AsObject();
                    foreach (JsonNode roleNode in increment["keywordRoles"].AsArray())
                    {
                        string role = roleNode.GetValue<string>();
                        Equal(true, keywordsByRole.ContainsKey(role));
                        if (profile["canonical"].GetValue<bool>())
                            Equal(true, specification.Contains("`" + keywordsByRole[role] + "`",
                                StringComparison.Ordinal));
                    }
                    foreach (JsonNode ruleNode in increment["grammarRules"].AsArray())
                    {
                        string rule = ruleNode.GetValue<string>();
                        Equal(true, grammarRules.Contains(rule));
                        Equal(true, Regex.IsMatch(ebnf, "(?m)^" + Regex.Escape(rule) + "\\s*="));
                    }
                    foreach (JsonNode operationNode in increment["semanticOperations"].AsArray())
                        Equal(true, semanticOperations.Contains(operationNode.GetValue<string>()));
                    foreach (JsonNode operationNode in increment["runtimeOperations"].AsArray())
                        Equal(true, runtimeOperations.Contains(operationNode.GetValue<string>()));
                    foreach (JsonNode roleNode in increment["snippetRoles"].AsArray())
                        Equal(true, snippetPrefixes.Contains(keywordsByRole[roleNode.GetValue<string>()]));
                    foreach (JsonNode diagnosticNode in increment["diagnosticCodes"].AsArray())
                        Equal(true, diagnostics.ContainsKey(diagnosticNode.GetValue<string>()));
                }
            }

            string guidance = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_INTEGRATION.md"), Encoding.UTF8);
            Equal(true, guidance.Contains("non contiene le grafie concrete delle keyword",
                StringComparison.Ordinal));
            Equal(true, guidance.Contains("Il codice UI non", StringComparison.Ordinal));
        }

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

        private static void KeepsBackendEquivalenceEvidenceExplicitAndReleaseBlocking()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject contract = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-backend-equivalence.json"), Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-backend-equivalence.schema.json"), Encoding.UTF8)).AsObject();
            JsonObject matrix = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-version-matrix.json"), Encoding.UTF8)).AsObject();

            Equal("1.0", contract["schemaVersion"].GetValue<string>());
            Equal("https://ddf.technology/schemas/ddf-backend-equivalence.schema.json",
                schema["$id"].GetValue<string>());
            Equal("^(core|compiler|runtime):[A-Za-z][A-Za-z0-9]*$",
                schema["$defs"]["dimension"]["properties"]["evidence"]
                    ["items"]["pattern"].GetValue<string>());
            Equal(matrix["language"]["languageVersion"].GetValue<string>(),
                contract["languageVersion"].GetValue<string>());
            Equal(matrix["language"]["backendEquivalenceContractVersion"]
                    .GetValue<string>(), contract["schemaVersion"].GetValue<string>());
            Equal("ddf-backend-equivalence.json",
                matrix["language"]["backendEquivalenceContract"].GetValue<string>());
            SequenceEqual(new[] { "ddf-vm-ir-v1", "dotnet-roslyn-v1" },
                contract["backends"].AsArray().Select(node => node.GetValue<string>()));
            Equal("block-on-divergence",
                contract["releasePolicy"]["mode"].GetValue<string>());

            string[] required = contract["releasePolicy"]["requiredDimensions"]
                .AsArray().Select(node => node.GetValue<string>()).ToArray();
            SequenceEqual(new[]
            {
                "aliasing", "cleanup-order", "debug-points", "evaluation-order",
                "host-effects", "output", "persistable-state", "runtime-errors",
                "suspensions", "value"
            }, required.OrderBy(value => value, StringComparer.Ordinal));
            Equal(required.Length, required.Distinct(StringComparer.Ordinal).Count());

            JsonArray dimensions = contract["dimensions"].AsArray();
            string[] ids = dimensions.Select(node =>
                node.AsObject()["id"].GetValue<string>()).ToArray();
            SequenceEqual(required.OrderBy(value => value, StringComparer.Ordinal),
                ids.OrderBy(value => value, StringComparer.Ordinal));
            Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());

            var expectedStatus = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["value"] = "enforced",
                ["output"] = "enforced",
                ["runtime-errors"] = "enforced",
                ["host-effects"] = "enforced",
                ["evaluation-order"] = "enforced",
                ["aliasing"] = "enforced",
                ["cleanup-order"] = "enforced",
                ["persistable-state"] = "enforced",
                ["suspensions"] = "enforced",
                ["debug-points"] = "enforced"
            };
            string compilerTests = File.ReadAllText(Path.Combine(projectRoot,
                "tests", "DDFLanguageEditor.Compiler.Tests", "Program.cs"),
                Encoding.UTF8);
            string coreTests = File.ReadAllText(Path.Combine(projectRoot,
                "tests", "DDFLanguageEditor.Tests", "Program.cs"), Encoding.UTF8);
            string runtimeTests = File.ReadAllText(Path.Combine(projectRoot,
                "tests", "DDFLanguageEditor.Runtime.Tests", "Program.cs"),
                Encoding.UTF8);
            foreach (JsonNode dimensionNode in dimensions)
            {
                JsonObject dimension = dimensionNode.AsObject();
                string id = dimension["id"].GetValue<string>();
                string status = dimension["status"].GetValue<string>();
                Equal(expectedStatus[id], status);
                JsonArray evidence = dimension["evidence"].AsArray();
                Equal(true, evidence.Count > 0);
                foreach (JsonNode evidenceNode in evidence)
                {
                    string reference = evidenceNode.GetValue<string>();
                    string[] parts = reference.Split(':');
                    Equal(2, parts.Length);
                    string source = parts[0] == "compiler" ? compilerTests :
                        parts[0] == "core" ? coreTests :
                        parts[0] == "runtime" ? runtimeTests : string.Empty;
                    Equal(true, source.Contains(" " + parts[1] + "(",
                        StringComparison.Ordinal));
                }
                if (status == "enforced")
                {
                    Equal(null, dimension["remainingGap"]?.GetValue<string>());
                    Equal(true, evidence.Any(item => string.Equals(
                        item.GetValue<string>(),
                        "compiler:EnforcesCompleteBackendEquivalenceGate",
                        StringComparison.Ordinal)));
                }
                else
                    Equal(true, !string.IsNullOrWhiteSpace(
                        dimension["remainingGap"]?.GetValue<string>()));
            }

            string guidance = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_BACKEND_EQUIVALENCE.md"), Encoding.UTF8);
            Equal(true, guidance.Contains("Una dimensione `partial` o `planned` non può",
                StringComparison.Ordinal));
            Equal(true, guidance.Contains("stato persistibile", StringComparison.Ordinal));
            Equal(true, guidance.Contains("ordine di cleanup", StringComparison.Ordinal));
            Equal(true, guidance.Contains("ordine di valutazione", StringComparison.Ordinal));
            Equal(true, guidance.Contains("aliasing", StringComparison.Ordinal));
            Equal(true, guidance.Contains("sospension", StringComparison.Ordinal));
            Equal(true, compilerTests.Contains("--backend-equivalence-only",
                StringComparison.Ordinal));
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

        private static void KeepsAuthoritativeVersionMatrixAligned()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject matrix = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-version-matrix.json"), Encoding.UTF8)).AsObject();
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-version-matrix.schema.json"), Encoding.UTF8)).AsObject();

            Equal("1.0", matrix["schemaVersion"].GetValue<string>());
            Equal("https://www.ddf.technology/schemas/version-matrix-1.0.json",
                schema["$id"].GetValue<string>());

            JsonObject product = matrix["product"].AsObject();
            Equal(product["version"].GetValue<string>() + " " +
                CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                    product["stability"].GetValue<string>()),
                product["displayVersion"].GetValue<string>());
            string productInfoSource = File.ReadAllText(Path.Combine(projectRoot,
                "ProductInfo.cs"), Encoding.UTF8);
            Equal(true, productInfoSource.Contains(
                "DdfProductVersionCatalog.Current.CurrentVersion",
                StringComparison.Ordinal));
            Equal(true, productInfoSource.Contains(
                "DdfProductVersionCatalog.Current.DisplayVersion",
                StringComparison.Ordinal));
            Equal(false, productInfoSource.Contains("\"" +
                product["version"].GetValue<string>() + "\"",
                StringComparison.Ordinal));
            AssertSourceAttribute(projectRoot, "Properties/AssemblyInfo.cs",
                "AssemblyVersion", product["assemblyVersion"].GetValue<string>());
            AssertSourceAttribute(projectRoot, "Properties/AssemblyInfo.cs",
                "AssemblyFileVersion", product["fileVersion"].GetValue<string>());
            AssertSourceAttribute(projectRoot, "Properties/AssemblyInfo.cs",
                "AssemblyInformationalVersion",
                product["informationalVersion"].GetValue<string>());

            JsonArray components = matrix["components"].AsArray();
            string[] componentIds = components.Select(node =>
                node.AsObject()["id"].GetValue<string>()).ToArray();
            Equal(componentIds.Length,
                componentIds.Distinct(StringComparer.Ordinal).Count());
            Equal(true, componentIds.Contains("standalone", StringComparer.Ordinal));
            Equal(true, componentIds.Contains("core-sdk", StringComparer.Ordinal));
            Equal(true, componentIds.Contains("winforms-sdk", StringComparer.Ordinal));
            Equal(true, componentIds.Contains("runtime", StringComparer.Ordinal));

            JsonObject update = matrix["productUpdate"].AsObject();
            Equal("ddf-language-editor", update["productId"].GetValue<string>());
            Equal("DDF-Technology/DDFLanguageEditor",
                update["repository"].GetValue<string>());
            Equal(product["stability"].GetValue<string>(),
                update["defaultChannel"].GetValue<string>());
            Equal(true, componentIds.Contains(
                update["standaloneComponentId"].GetValue<string>(),
                StringComparer.Ordinal));
            string[] embeddedComponentIds = update["embeddedComponentIds"].AsArray()
                .Select(item => item.GetValue<string>()).ToArray();
            SequenceEqual(new[] { "core-sdk", "winforms-sdk" },
                embeddedComponentIds);

            DdfProductVersionIdentity identity = DdfProductVersionCatalog.Current;
            Equal(matrix["schemaVersion"].GetValue<string>(), identity.SchemaVersion);
            Equal(update["productId"].GetValue<string>(), identity.ProductId);
            Equal(update["repository"].GetValue<string>(), identity.Repository);
            Equal(product["version"].GetValue<string>(), identity.CurrentVersion);
            Equal(product["stability"].GetValue<string>(), identity.Stability);
            Equal(product["displayVersion"].GetValue<string>(), identity.DisplayVersion);
            Equal(update["defaultChannel"].GetValue<string>(), identity.DefaultChannel);
            Equal(matrix["editorSdk"]["contractVersion"].GetValue<string>(),
                identity.SdkContractVersion);
            Equal(matrix["editorSdk"]["legacyContractVersion"].GetValue<string>(),
                identity.LegacySdkContractVersion);
            Equal(update["standaloneComponentId"].GetValue<string>(),
                identity.Standalone.Id);
            SequenceEqual(embeddedComponentIds,
                identity.EmbeddedComponents.Select(item => item.Id).ToArray());
            Equal(matrix["persistence"].AsObject().Count,
                identity.PersistenceVersions.Count);

            JsonObject invalidMatrix = JsonNode.Parse(matrix.ToJsonString()).AsObject();
            invalidMatrix["productUpdate"]["standaloneComponentId"] = "missing";
            Throws<InvalidDataException>(() =>
                DdfProductVersionCatalog.Parse(invalidMatrix.ToJsonString()));

            foreach (JsonNode node in components)
            {
                JsonObject component = node.AsObject();
                string project = Path.Combine(projectRoot,
                    component["project"].GetValue<string>().Replace('/',
                        Path.DirectorySeparatorChar));
                Equal(true, File.Exists(project));
                XDocument document = XDocument.Load(project);
                XElement root = document.Root ?? throw new InvalidOperationException(
                    "Progetto XML privo di radice: " + project);
                AssertMsBuildProperty(root, "Version",
                    component["version"].GetValue<string>(), project);
                AssertMsBuildProperty(root, "AssemblyVersion",
                    component["assemblyVersion"].GetValue<string>(), project);
                AssertMsBuildProperty(root, "FileVersion",
                    component["fileVersion"].GetValue<string>(), project);
                AssertMsBuildProperty(root, "InformationalVersion",
                    component["informationalVersion"].GetValue<string>(), project);

                if (component["packageId"] != null)
                {
                    AssertMsBuildProperty(root, "PackageId",
                        component["packageId"].GetValue<string>(), project);
                    AssertMsBuildProperty(root, "PackageVersion",
                        component["packageVersion"].GetValue<string>(), project);
                    Equal(false, component["packageVersion"].GetValue<string>() ==
                        "0.11.2-beta");
                }
            }

            JsonObject language = matrix["language"].AsObject();
            JsonObject manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "language-packages", "ddf", "manifest.json"), Encoding.UTF8)).AsObject();
            Equal(language["id"].GetValue<string>(), manifest["id"].GetValue<string>());
            Equal(language["languageVersion"].GetValue<string>(),
                manifest["languageVersion"].GetValue<string>());
            Equal(language["packageVersion"].GetValue<string>(),
                manifest["packageVersion"].GetValue<string>());
            Equal(language["engineApi"].GetValue<string>(),
                manifest["engineApi"].GetValue<string>());

            AssertSourceConstant(projectRoot, "DdfEditorContracts.cs", "CurrentVersion",
                matrix["editorSdk"]["contractVersion"].GetValue<string>());
            Equal("0.34", matrix["editorSdk"]["legacyContractVersion"].GetValue<string>());
            Equal(false, matrix["editorSdk"]["legacyContractVersion"].GetValue<string>() ==
                matrix["editorSdk"]["contractVersion"].GetValue<string>());
            Equal(40, matrix["editorSdk"]["legacyBaselineCommit"].GetValue<string>().Length);
            Equal(DdfEditorApiLifecycleCatalog.PolicyVersion,
                matrix["editorSdk"]["apiLifecyclePolicyVersion"].GetValue<string>());
            Equal(DdfEditorApiLifecycleCatalog.EarliestBreakingContractVersion,
                matrix["editorSdk"]["earliestBreakingContractVersion"].GetValue<string>());
            Equal(DdfEditorApiLifecycleCatalog.MinimumDocumentedSuccessorContracts,
                matrix["editorSdk"]["minimumDocumentedSuccessorContracts"].GetValue<int>());
            Equal("1.0", matrix["language"]["integrationContractVersion"].GetValue<string>());
            Equal("ddf-language-integration.json",
                matrix["language"]["integrationContract"].GetValue<string>());

            JsonObject hostSupport = matrix["hostSupport"].AsObject();
            Equal("1.0", hostSupport["policyVersion"].GetValue<string>());
            JsonObject referenceHost = hostSupport["reference"].AsObject();
            Equal("C#", referenceHost["language"].GetValue<string>());
            Equal(".NET 10", referenceHost["runtime"].GetValue<string>());
            Equal("WinForms", referenceHost["uiFramework"].GetValue<string>());
            Equal("reference-beta", referenceHost["status"].GetValue<string>());
            string[] referenceProjects = referenceHost["projects"].AsArray()
                .Select(node => node.GetValue<string>()).ToArray();
            Equal(3, referenceProjects.Length);
            foreach (string relativeProject in referenceProjects)
            {
                string hostProject = Path.Combine(projectRoot,
                    relativeProject.Replace('/', Path.DirectorySeparatorChar));
                Equal(true, File.Exists(hostProject));
                XDocument hostDocument = XDocument.Load(hostProject);
                Equal("net10.0-windows", hostDocument.Descendants().Single(element =>
                    element.Name.LocalName == "TargetFramework").Value);
                Equal("true", hostDocument.Descendants().Single(element =>
                    element.Name.LocalName == "UseWindowsForms").Value);
            }

            JsonArray plannedHosts = hostSupport["planned"].AsArray();
            string[] plannedIds = plannedHosts.Select(node =>
                node.AsObject()["id"].GetValue<string>()).ToArray();
            SequenceEqual(new[] { "fsharp", "non-dotnet", "vbnet", "wpf" },
                plannedIds.OrderBy(value => value, StringComparer.Ordinal));
            foreach (JsonNode plannedNode in plannedHosts)
            {
                JsonObject planned = plannedNode.AsObject();
                Equal("post-1.0", planned["availability"].GetValue<string>());
                Equal("reuse-versioned-sdk-contracts",
                    planned["strategy"].GetValue<string>());
            }

            string[] prematureManagedHosts = Directory.EnumerateFiles(projectRoot,
                    "*.*proj", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" +
                        Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    (path.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            Equal(0, prematureManagedHosts.Length);
            foreach (string project in Directory.EnumerateFiles(projectRoot, "*.csproj",
                SearchOption.AllDirectories).Where(path => !path.Contains(
                    Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)))
            {
                XDocument projectDocument = XDocument.Load(project);
                Equal(false, projectDocument.Descendants().Any(element =>
                    element.Name.LocalName == "UseWPF" &&
                    string.Equals(element.Value, "true",
                        StringComparison.OrdinalIgnoreCase)));
            }
            string hostPolicy = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_HOST_SUPPORT_POLICY.md"), Encoding.UTF8);
            foreach (string destination in new[] { "VB.NET", "F#", "WPF", "non-.NET" })
                Equal(true, hostPolicy.Contains(destination, StringComparison.Ordinal));
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Runtime/DdfRuntimeAbi.cs", "Version",
                matrix["runtime"]["abiVersion"].GetValue<string>());

            JsonObject worker = matrix["worker"].AsObject();
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetWorkerProtocol.cs",
                "ProtocolVersionValue", worker["protocolVersion"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetWorkerDuplexProtocol.cs",
                "FrameVersionValue", worker["duplexFrameVersion"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetWorkerProtocol.cs",
                "PolicyVersionValue", worker["accessPolicyVersion"].GetValue<string>());

            JsonObject formats = matrix["formats"].AsObject();
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Core/DdfIntermediateRepresentation.cs", "FormatVersion",
                formats["ir"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Core/DdfBoundIntermediateRepresentation.cs", "FormatVersion",
                formats["boundIr"].GetValue<string>());
            AssertSourceConstant(projectRoot, "DDFLanguageEditor.Core/DdfFlowchart.cs",
                "FormatVersion", formats["flowchart"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetCompilationPlanner.cs", "FormatVersion",
                formats["compilationPlan"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetCompilerProfile.cs", "FormatVersion",
                formats["compilerProfile"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetNameMangler.cs", "FormatVersion",
                formats["nameMap"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetArtifactLinker.cs", "FormatVersion",
                formats["linker"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetLinkedBundle.cs", "FormatVersion",
                formats["linkedBundle"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetArtifactCache.cs", "FormatVersion",
                formats["artifactCache"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Core/DdfAdapterFamilyCatalog.cs", "FormatVersionValue",
                formats["adapterFamilyCatalog"].GetValue<string>());

            JsonObject schemas = matrix["schemas"].AsObject();
            AssertSourceConstant(projectRoot, "DDFLanguageEditor.Core/DdfLanguagePackage.cs",
                "SupportedSchemaVersion", schemas["languagePackage"].GetValue<string>());
            AssertSourceConstant(projectRoot, "DDFLanguageEditor.Core/DdfLanguagePackage.cs",
                "SupportedTypesSchemaVersion", schemas["languageTypes"].GetValue<string>());
            AssertSourceConstant(projectRoot, "DDFLanguageEditor.Core/DdfLanguagePackage.cs",
                "SupportedStandardLibrarySchemaVersion",
                schemas["standardLibrary"].GetValue<string>());
            AssertSourceConstant(projectRoot, "DDFLanguageEditor.Core/DdfBuildSystem.cs",
                "SchemaVersionValue", schemas["project"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Core/DdfExternalReferenceLock.cs", "VersionValue",
                schemas["externalReferencesLock"].GetValue<string>());
            AssertSourceConstant(projectRoot, "DDFLanguageEditor.Core/DdfExternalApi.cs",
                "FormatVersion", schemas["externalApi"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetArtifactContract.cs", "SchemaVersionValue",
                schemas["artifactManifest"].GetValue<string>());
            AssertSourceConstant(projectRoot,
                "DDFLanguageEditor.Compiler/DdfDotNetLinkedPackageContract.cs",
                "SchemaVersionValue", schemas["linkedPackage"].GetValue<string>());
            Equal(schemas["artifactManifest"].GetValue<string>(),
                schemas["sourceMap"].GetValue<string>());
            AssertSourceConstant(projectRoot, "DDFLanguageEditor.Core/DdfEditorExecution.cs",
                "CurrentSchemaVersion",
                matrix["persistence"]["scriptState"].GetValue<string>());
            JsonObject persistence = matrix["persistence"].AsObject();
            var persistenceConstants = new Dictionary<string, string>
            {
                ["editorState"] = "EditorStateVersion",
                ["workspaceSession"] = "WorkspaceSessionVersion",
                ["hotExitSession"] = "HotExitSessionVersion",
                ["crashRecoverySession"] = "CrashRecoverySessionVersion",
                ["breakpoints"] = "BreakpointsVersion",
                ["watchExpressions"] = "WatchExpressionsVersion",
                ["paletteLayout"] = "PaletteLayoutVersion",
                ["toolbarLayout"] = "ToolbarLayoutVersion",
                ["scriptState"] = "ScriptStateVersion",
                ["projectManifest"] = "ProjectManifestVersion"
            };
            foreach (KeyValuePair<string, string> item in persistenceConstants)
                AssertSourceConstant(projectRoot,
                    "DDFLanguageEditor.Core/DdfPersistenceContract.cs", item.Value,
                    persistence[item.Key].GetValue<string>());
        }

        private static void AssertMsBuildProperty(XElement project, string propertyName,
            string expected, string projectPath)
        {
            string[] values = project.Descendants(propertyName)
                .Select(element => element.Value.Trim()).Distinct(StringComparer.Ordinal).ToArray();
            if (values.Length != 1 || values[0] != expected)
            {
                throw new InvalidOperationException(Path.GetFileName(projectPath) + " " +
                    propertyName + " expected <" + expected + "> but was <" +
                    string.Join(", ", values) + ">");
            }
        }

        private static void AssertSourceConstant(string projectRoot, string relativePath,
            string constantName, string expected)
        {
            string source = File.ReadAllText(Path.Combine(projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);
            string pattern = @"\b" + Regex.Escape(constantName) +
                "\\s*=\\s*\"" + Regex.Escape(expected) + "\"";
            Equal(true, Regex.IsMatch(source, pattern, RegexOptions.CultureInvariant));
        }

        private static void AssertSourceAttribute(string projectRoot, string relativePath,
            string attributeName, string expected)
        {
            string source = File.ReadAllText(Path.Combine(projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);
            string expectedText = "[assembly: " + attributeName + "(\"" + expected + "\")]";
            Equal(true, source.Contains(expectedText, StringComparison.Ordinal));
        }

        private static void ModelsImmutableVersionedProductUpdateResults()
        {
            DateTimeOffset checkedAt = new DateTimeOffset(2026, 10, 24,
                12, 0, 0, TimeSpan.Zero);
            DateTimeOffset releasedAt = checkedAt.AddDays(-1);
            DdfProductVersionIdentity identity = DdfProductVersionCatalog.Current;

            var mutableReasons = new List<string> { "SDK contract 0.42 required" };
            DdfProductUpdateResult available = DdfProductUpdateResult.Available(
                DdfProductUpdateComponentKind.Standalone, identity.ProductId,
                identity.CurrentVersion, "0.13.6-beta.1",
                DdfProductUpdateChannel.Beta,
                DdfProductUpdateCompatibility.Compatible, checkedAt,
                releasedAt, "Prima release verificabile.",
                new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/tag/v0.13.6-beta.1"),
                mutableReasons);
            mutableReasons.Add("mutazione successiva");

            Equal(DdfProductUpdateResult.SchemaVersionValue,
                available.SchemaVersion);
            Equal(DdfProductUpdateState.UpdateAvailable, available.State);
            Equal(true, available.IsActionable);
            Equal(1, available.CompatibilityReasons.Count);
            Equal(TimeSpan.Zero, available.CheckedAtUtc.Offset);
            Equal(TimeSpan.Zero, available.ReleasedAtUtc.Value.Offset);

            string json = DdfProductUpdateResultJson.Serialize(available);
            DdfProductUpdateResult restored =
                DdfProductUpdateResultJson.Parse(json);
            Equal(available.ComponentKind, restored.ComponentKind);
            Equal(available.ComponentId, restored.ComponentId);
            Equal(available.CurrentVersion, restored.CurrentVersion);
            Equal(available.AvailableVersion, restored.AvailableVersion);
            Equal(available.Channel, restored.Channel);
            Equal(available.State, restored.State);
            Equal(available.Compatibility, restored.Compatibility);
            Equal(available.CheckedAtUtc, restored.CheckedAtUtc);
            Equal(available.ReleasedAtUtc, restored.ReleasedAtUtc);
            Equal(available.ReleaseNotes, restored.ReleaseNotes);
            Equal(available.VerifiedReleaseUri, restored.VerifiedReleaseUri);
            SequenceEqual(available.CompatibilityReasons,
                restored.CompatibilityReasons);

            DdfProductUpdateResult incompatible = DdfProductUpdateResult.Available(
                DdfProductUpdateComponentKind.EmbeddedSdk, "winforms-sdk",
                identity.EmbeddedComponents.Single(component =>
                    component.Id == "winforms-sdk").Version,
                "2.0.0", DdfProductUpdateChannel.Stable,
                DdfProductUpdateCompatibility.Incompatible, checkedAt,
                releasedAt, "Contratto maggiore.",
                new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/tag/v2.0.0"),
                new[] { "Contratto SDK 2.0 non supportato." });
            Equal(DdfProductUpdateState.Incompatible, incompatible.State);
            Equal(false, incompatible.IsActionable);

            DdfProductUpdateResult upToDate = DdfProductUpdateResult.UpToDate(
                DdfProductUpdateComponentKind.LanguagePackage, "ddf",
                "0.13.0", "0.13.0", DdfProductUpdateChannel.Beta, checkedAt);
            Equal(DdfProductUpdateState.UpToDate, upToDate.State);
            Equal(false, upToDate.IsActionable);

            DdfProductUpdateResult unknown = DdfProductUpdateResult.Unknown(
                DdfProductUpdateComponentKind.Plugin, "sample-plugin", "1.0.0",
                DdfProductUpdateChannel.Stable, checkedAt, "Offline.");
            Equal(DdfProductUpdateState.Unknown, unknown.State);
            Equal(DdfProductUpdateCompatibility.Unknown,
                unknown.Compatibility);

            Throws<ArgumentException>(() => DdfProductUpdateResult.Available(
                DdfProductUpdateComponentKind.Module, "sample-module", "1.0.0",
                "1.1.0", DdfProductUpdateChannel.Stable,
                DdfProductUpdateCompatibility.Compatible, checkedAt,
                releasedAt, string.Empty, new Uri("http://example.test/release")));
            Throws<InvalidDataException>(() =>
                DdfProductUpdateResultJson.Parse(json.Replace(
                    "\"schemaVersion\": \"1.0\"",
                    "\"schemaVersion\": \"2.0\"",
                    StringComparison.Ordinal)));

            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-product-update-result.schema.json"),
                Encoding.UTF8)).AsObject();
            Equal("https://www.ddf.technology/schemas/product-update-result-1.0.json",
                schema["$id"].GetValue<string>());
            JsonObject matrix = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-version-matrix.json"), Encoding.UTF8)).AsObject();
            Equal(DdfProductUpdateResult.SchemaVersionValue,
                matrix["schemas"]["productUpdateResult"].GetValue<string>());
        }

        private static void PersistsUpdateInstallationPlansAndCrashJournalsAtomically()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "ddf-update-journal-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                DateTimeOffset now = new DateTimeOffset(2026, 10, 26, 12, 0, 0,
                    TimeSpan.Zero);
                var plan = new DdfProductUpdateInstallationPlan(
                    "portable-main", "standalone", "0.12.5", "0.13.0-beta.1",
                    "win-x64", "x64", new string('a', 64), "official-2026",
                    new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.0/package.zip"),
                    4096, new string('b', 64), Path.Combine(root, "target"),
                    Path.Combine(root, "staging"), Path.Combine(root, "backup"),
                    "DDFLanguageEditor.exe", 1234, now.AddMinutes(-1),
                    new string('c', 64), true, new[]
                    {
                        new DdfProductUpdateInstallationFile("support/core.dll", 20,
                            new string('d', 64)),
                        new DdfProductUpdateInstallationFile("DDFLanguageEditor.exe",
                            10, new string('e', 64))
                    }, now);

                string json = DdfProductUpdateInstallationJson.SerializePlan(plan);
                DdfProductUpdateInstallationPlan restored =
                    DdfProductUpdateInstallationJson.ParsePlan(json);
                Equal(plan.TargetVersion, restored.TargetVersion);
                Equal("DDFLanguageEditor.exe", restored.Inventory[0].RelativePath);
                Equal(DdfProductUpdateInstallationJson.ComputePlanSha256(plan),
                    DdfProductUpdateInstallationJson.ComputePlanSha256(restored));
                Equal(false, json.Contains("workspace", StringComparison.OrdinalIgnoreCase));

                string planPath = Path.Combine(root, "state", "plan.json");
                DdfProductUpdateInstallationJournalStore.WritePlan(planPath, plan);
                Equal(plan.TargetRoot,
                    DdfProductUpdateInstallationJournalStore.ReadPlan(planPath)
                        .TargetRoot);
                Throws<IOException>(() =>
                    DdfProductUpdateInstallationJournalStore.WritePlan(planPath, plan));

                string planHash = DdfProductUpdateInstallationJson
                    .ComputePlanSha256(plan);
                var store = new DdfProductUpdateInstallationJournalStore(
                    Path.Combine(root, "state", "journal.json"));
                DdfProductUpdateInstallationJournal journal = store.Create(planHash,
                    now);
                Equal(DdfProductUpdateInstallationState.Checked, journal.State);
                Equal(DdfProductUpdateRecoveryAction.DiscardStaging,
                    DdfProductUpdateRecoveryPlanner.Plan(journal, true, true, false));
                Throws<InvalidOperationException>(() => store.Create(planHash, now));

                var states = new[]
                {
                    DdfProductUpdateInstallationState.DownloadAuthorized,
                    DdfProductUpdateInstallationState.Downloading,
                    DdfProductUpdateInstallationState.Staged,
                    DdfProductUpdateInstallationState.InstallAuthorized,
                    DdfProductUpdateInstallationState.WaitingForEditorExit,
                    DdfProductUpdateInstallationState.Applying,
                    DdfProductUpdateInstallationState.Verifying,
                    DdfProductUpdateInstallationState.Completed
                };
                DdfProductUpdateInstallationJournal applying = null;
                foreach (DdfProductUpdateInstallationState state in states)
                {
                    journal = store.Transition(journal.Revision, state,
                        now.AddMinutes(journal.Revision + 1));
                    if (state == DdfProductUpdateInstallationState.Applying)
                        applying = journal;
                }
                Equal(8L, journal.Revision);
                Equal(true, journal.IsTerminal);
                Equal(DdfProductUpdateInstallationState.Completed,
                    store.Read().State);
                Equal(DdfProductUpdateRecoveryAction.RestoreBackup,
                    DdfProductUpdateRecoveryPlanner.Plan(applying, false, false, true));
                Equal(DdfProductUpdateRecoveryAction.VerifyOrRollback,
                    DdfProductUpdateRecoveryPlanner.Plan(applying, true, false, true));
                Equal(DdfProductUpdateRecoveryAction.ManualIntervention,
                    DdfProductUpdateRecoveryPlanner.Plan(applying, false, false, false));
                Equal(DdfProductUpdateRecoveryAction.CleanupDeferred,
                    DdfProductUpdateRecoveryPlanner.Plan(journal, true, false, true));
                Equal(false, DdfProductUpdateInstallationTransitions.CanTransition(
                    DdfProductUpdateInstallationState.Applying,
                    DdfProductUpdateInstallationState.Cancelled));
                Throws<InvalidOperationException>(() => store.Transition(0,
                    DdfProductUpdateInstallationState.Failed, now, "stale"));
                Throws<InvalidDataException>(() =>
                    DdfProductUpdateInstallationJson.ParseJournal(
                        DdfProductUpdateInstallationJson.SerializeJournal(journal)
                            .Replace("\"completed\"", "99", StringComparison.Ordinal)));
                Throws<ArgumentException>(() =>
                    new DdfProductUpdateInstallationFile("../escape.dll", 1,
                        new string('f', 64)));
                Throws<ArgumentException>(() =>
                    new DdfProductUpdateInstallationFile("CON.txt", 1,
                        new string('f', 64)));

                string projectRoot = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                JsonObject matrix = JsonNode.Parse(File.ReadAllText(Path.Combine(
                    projectRoot, "ddf-version-matrix.json"), Encoding.UTF8)).AsObject();
                Equal(DdfProductUpdateInstallationPlan.SchemaVersionValue,
                    matrix["schemas"]["productUpdateInstallationPlan"]
                        .GetValue<string>());
                Equal(DdfProductUpdateInstallationJournal.SchemaVersionValue,
                    matrix["schemas"]["productUpdateInstallationJournal"]
                        .GetValue<string>());
                foreach (string schemaFile in new[]
                {
                    "ddf-product-update-installation-plan.schema.json",
                    "ddf-product-update-installation-journal.schema.json"
                })
                {
                    JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                        projectRoot, schemaFile), Encoding.UTF8)).AsObject();
                    Equal(true, schema["$id"].GetValue<string>()
                        .StartsWith("https://www.ddf.technology/schemas/",
                            StringComparison.Ordinal));
                }
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void DownloadsAndSealsVerifiedUpdateStagingSafely()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "ddf-update-stage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["DDFLanguageEditor.exe"] = Encoding.UTF8.GetBytes("editor"),
                    ["support/core.dll"] = Encoding.UTF8.GetBytes("core")
                };
                byte[] archive = CreateProductUpdateArchive(files);
                DateTimeOffset now = new DateTimeOffset(2026, 10, 26, 15, 0, 0,
                    TimeSpan.Zero);
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                DdfReleaseTrustStore trust = new DdfReleaseTrustStore(
                    new Dictionary<string, byte[]>
                    {
                        ["release-2026"] = key.ExportSubjectPublicKeyInfo()
                    });
                Uri uri = new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.2/editor.zip");
                string version = "0.13.6-beta.2";
                var asset = new DdfReleaseManifestAsset(
                    DdfProductUpdateComponentKind.Standalone,
                    DdfProductVersionCatalog.Current.ProductId, version, "win-x64",
                    "x64", uri, archive.LongLength, HashBytes(archive));
                var compatibility = new DdfReleaseCompatibility("0.34", "0.99",
                    new Dictionary<string, string> { ["editorState"] = "1.0" });
                string manifest = DdfReleaseManifestCodec.Serialize(
                    DdfReleaseManifestSigner.Sign(
                        DdfReleaseManifest.CreateUnsigned(
                            DdfProductVersionCatalog.Current.ProductId, version,
                            DdfProductUpdateChannel.Beta, now, "Staging test.",
                            compatibility, new[] { asset }), "release-2026", key));
                DdfProductUpdateInstallationPlan plan = CreateStagingPlan(root,
                    version, uri, archive, manifest, files, now);
                string planHash = DdfProductUpdateInstallationJson
                    .ComputePlanSha256(plan);
                var store = new DdfProductUpdateInstallationJournalStore(
                    Path.Combine(root, "state", "journal.json"));
                DdfProductUpdateInstallationJournal journal = store.Create(planHash,
                    now);
                store.Transition(journal.Revision,
                    DdfProductUpdateInstallationState.DownloadAuthorized,
                    now.AddSeconds(1));

                var handler = new QueueHttpMessageHandler();
                handler.Enqueue((request, cancellationToken) =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(archive)
                    };
                    return Task.FromResult(response);
                });
                var progress = new List<DdfProductUpdateStagingProgress>();
                using (var stager = new DdfProductUpdateStager(handler,
                    new DdfProductUpdateStagingOptions(TimeSpan.FromSeconds(10),
                        archive.LongLength)))
                {
                    DdfProductUpdateStagingSeal seal = stager.StageAsync(plan,
                        manifest, trust, store, now.AddSeconds(2),
                        progress: new InlineProgress<DdfProductUpdateStagingProgress>(
                            item => progress.Add(item))).GetAwaiter().GetResult();
                    Equal(planHash, seal.PlanSha256);
                    Equal(files.Count, seal.FileCount);
                    Equal(files.Values.Sum(item => (long)item.Length),
                        seal.TotalFileBytes);
                }

                Equal(DdfProductUpdateInstallationState.Staged,
                    store.Read().State);
                Equal(false, Directory.Exists(plan.TargetRoot));
                Equal(true, Directory.Exists(plan.StagingRoot));
                foreach (KeyValuePair<string, byte[]> file in files)
                    SequenceEqual(file.Value, File.ReadAllBytes(Path.Combine(
                        plan.StagingRoot,
                        file.Key.Replace('/', Path.DirectorySeparatorChar))));
                DdfProductUpdateStagingSeal persisted =
                    DdfProductUpdateStagingSealCodec.Read(
                        DdfProductUpdateStagingSealCodec.SealPathFor(
                            plan.StagingRoot));
                Equal(planHash, persisted.PlanSha256);
                DdfProductUpdateStagingSealCodec.ValidateForPlan(persisted, plan);
                string projectRoot = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                JsonObject matrix = JsonNode.Parse(File.ReadAllText(Path.Combine(
                    projectRoot, "ddf-version-matrix.json"), Encoding.UTF8)).AsObject();
                Equal(DdfProductUpdateStagingSeal.SchemaVersionValue,
                    matrix["schemas"]["productUpdateStagingSeal"]
                        .GetValue<string>());
                JsonObject sealSchema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                    projectRoot, "ddf-product-update-staging-seal.schema.json"),
                    Encoding.UTF8)).AsObject();
                Equal("https://www.ddf.technology/schemas/product-update-staging-seal-1.0.json",
                    sealSchema["$id"].GetValue<string>());
                Equal(true, progress.Count > 0);
                Equal(archive.LongLength, progress.Last().BytesReceived);
                Equal(0, Directory.GetFiles(root, "*.partial",
                    SearchOption.TopDirectoryOnly).Length);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void RejectsUnsafeUpdateArchivesWithoutTouchingTarget()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "ddf-update-stage-unsafe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["DDFLanguageEditor.exe"] = Encoding.UTF8.GetBytes("editor")
                };
                byte[] archive = CreateProductUpdateArchive(files,
                    "../escape.txt", Encoding.UTF8.GetBytes("escape"));
                DateTimeOffset now = new DateTimeOffset(2026, 10, 26, 16, 0, 0,
                    TimeSpan.Zero);
                using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                DdfReleaseTrustStore trust = new DdfReleaseTrustStore(
                    new Dictionary<string, byte[]>
                    {
                        ["release-2026"] = key.ExportSubjectPublicKeyInfo()
                    });
                Uri uri = new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.3/editor.zip");
                string version = "0.13.6-beta.3";
                var asset = new DdfReleaseManifestAsset(
                    DdfProductUpdateComponentKind.Standalone,
                    DdfProductVersionCatalog.Current.ProductId, version, "win-x64",
                    "x64", uri, archive.LongLength, HashBytes(archive));
                string manifest = DdfReleaseManifestCodec.Serialize(
                    DdfReleaseManifestSigner.Sign(
                        DdfReleaseManifest.CreateUnsigned(
                            DdfProductVersionCatalog.Current.ProductId, version,
                            DdfProductUpdateChannel.Beta, now, "Unsafe archive.",
                            new DdfReleaseCompatibility("0.34", "0.99",
                                new Dictionary<string, string>
                                {
                                    ["editorState"] = "1.0"
                                }), new[] { asset }), "release-2026", key));
                DdfProductUpdateInstallationPlan plan = CreateStagingPlan(root,
                    version, uri, archive, manifest, files, now);
                var store = new DdfProductUpdateInstallationJournalStore(
                    Path.Combine(root, "state", "journal.json"));
                DdfProductUpdateInstallationJournal journal = store.Create(
                    DdfProductUpdateInstallationJson.ComputePlanSha256(plan), now);
                store.Transition(journal.Revision,
                    DdfProductUpdateInstallationState.DownloadAuthorized,
                    now.AddSeconds(1));
                var handler = new QueueHttpMessageHandler();
                handler.Enqueue((request, cancellationToken) => Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(archive)
                    }));
                using var stager = new DdfProductUpdateStager(handler,
                    new DdfProductUpdateStagingOptions(TimeSpan.FromSeconds(10),
                        archive.LongLength));
                Throws<InvalidDataException>(() => stager.StageAsync(plan,
                    manifest, trust, store, now.AddSeconds(2)).GetAwaiter()
                    .GetResult());
                Equal(DdfProductUpdateInstallationState.Failed,
                    store.Read().State);
                Equal(false, Directory.Exists(plan.TargetRoot));
                Equal(false, Directory.Exists(plan.StagingRoot));
                Equal(false, File.Exists(Path.Combine(root, "escape.txt")));
                Equal(false, File.Exists(
                    DdfProductUpdateStagingSealCodec.SealPathFor(plan.StagingRoot)));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static DdfProductUpdateInstallationPlan CreateStagingPlan(
            string root, string version, Uri uri, byte[] archive, string manifest,
            IReadOnlyDictionary<string, byte[]> files, DateTimeOffset now) =>
            new DdfProductUpdateInstallationPlan("portable-main",
                DdfProductVersionCatalog.Current.ProductId, "0.12.5", version,
                "win-x64", "x64", HashBytes(Encoding.UTF8.GetBytes(manifest)),
                "release-2026", uri, archive.LongLength, HashBytes(archive),
                Path.Combine(root, "target"), Path.Combine(root, "staging"),
                Path.Combine(root, "backup"), "DDFLanguageEditor.exe", 1234,
                now.AddMinutes(-1), new string('a', 64), false,
                files.Select(file => new DdfProductUpdateInstallationFile(file.Key,
                    file.Value.LongLength, HashBytes(file.Value))), now);

        private static byte[] CreateProductUpdateArchive(
            IReadOnlyDictionary<string, byte[]> files, string extraPath = null,
            byte[] extraContent = null)
        {
            using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                foreach (KeyValuePair<string, byte[]> file in files)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(file.Key,
                        CompressionLevel.Optimal);
                    using Stream stream = entry.Open();
                    stream.Write(file.Value, 0, file.Value.Length);
                }
                if (extraPath != null)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(extraPath,
                        CompressionLevel.Optimal);
                    using Stream stream = entry.Open();
                    byte[] content = extraContent ?? Array.Empty<byte>();
                    stream.Write(content, 0, content.Length);
                }
            }
            return output.ToArray();
        }

        private static string HashBytes(byte[] bytes) => Convert.ToHexString(
            SHA256.HashData(bytes)).ToLowerInvariant();

        private static void SwapsStagedUpdatesAndRollsBackCorruptedPromotions()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "ddf-update-worker-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                DateTimeOffset now = new DateTimeOffset(2026, 10, 26, 18, 0, 0,
                    TimeSpan.Zero);
                string nonce = new string('9', 64);
                byte[] newExe = Encoding.UTF8.GetBytes("new-editor");
                byte[] newCore = Encoding.UTF8.GetBytes("new-core");
                var inventory = new[]
                {
                    new DdfProductUpdateInstallationFile(
                        "DDFLanguageEditor.exe", newExe.LongLength,
                        HashBytes(newExe)),
                    new DdfProductUpdateInstallationFile("support/core.dll",
                        newCore.LongLength, HashBytes(newCore))
                };
                var plan = new DdfProductUpdateInstallationPlan("portable-main",
                    DdfProductVersionCatalog.Current.ProductId, "0.12.5",
                    "0.13.6-beta.4", "win-x64", "x64", new string('a', 64),
                    "release-2026", new Uri(
                        "https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.4/editor.zip"),
                    100, new string('b', 64), Path.Combine(root, "target"),
                    Path.Combine(root, "staging"), Path.Combine(root, "backup"),
                    "DDFLanguageEditor.exe", 4242, now.AddMinutes(-1), nonce,
                    false, inventory, now);
                Directory.CreateDirectory(plan.TargetRoot);
                File.WriteAllText(Path.Combine(plan.TargetRoot,
                    "DDFLanguageEditor.exe"), "old-editor", Encoding.UTF8);
                File.WriteAllText(Path.Combine(plan.TargetRoot, "old-only.txt"),
                    "old", Encoding.UTF8);
                Directory.CreateDirectory(Path.Combine(plan.StagingRoot,
                    "support"));
                File.WriteAllBytes(Path.Combine(plan.StagingRoot,
                    "DDFLanguageEditor.exe"), newExe);
                File.WriteAllBytes(Path.Combine(plan.StagingRoot,
                    "support", "core.dll"), newCore);

                string planPath = Path.Combine(root, "state", "plan.json");
                string journalPath = Path.Combine(root, "state", "journal.json");
                DdfProductUpdateInstallationJournalStore.WritePlan(planPath, plan);
                string planHash = DdfProductUpdateInstallationJson
                    .ComputePlanSha256(plan);
                string sealJson = JsonSerializer.Serialize(new
                {
                    schemaVersion = DdfProductUpdateStagingSeal.SchemaVersionValue,
                    planSha256 = planHash,
                    inventorySha256 = DdfProductUpdateStagingSealCodec
                        .ComputeInventorySha256(plan.Inventory),
                    assetSha256 = plan.AssetSha256,
                    fileCount = plan.Inventory.Count,
                    totalFileBytes = plan.Inventory.Sum(file => file.Size),
                    stagedUtc = now.AddSeconds(3)
                });
                File.WriteAllText(DdfProductUpdateStagingSealCodec.SealPathFor(
                    plan.StagingRoot), sealJson, new UTF8Encoding(false));

                var store = new DdfProductUpdateInstallationJournalStore(
                    journalPath);
                DdfProductUpdateInstallationJournal journal = store.Create(planHash,
                    now);
                foreach (DdfProductUpdateInstallationState state in new[]
                {
                    DdfProductUpdateInstallationState.DownloadAuthorized,
                    DdfProductUpdateInstallationState.Downloading,
                    DdfProductUpdateInstallationState.Staged,
                    DdfProductUpdateInstallationState.InstallAuthorized
                })
                    journal = store.Transition(journal.Revision, state,
                        now.AddSeconds(journal.Revision + 1));

                var waiter = new ImmediateProcessWaiter();
                var worker = new DdfProductUpdateWorker(waiter,
                    new DdfProductUpdateWorkerOptions(TimeSpan.FromSeconds(5)));
                Throws<InvalidDataException>(() => worker.ApplyAsync(planPath,
                    journalPath, new string('8', 64)).GetAwaiter().GetResult());
                Equal(DdfProductUpdateInstallationState.InstallAuthorized,
                    store.Read().State);

                DdfProductUpdateWorkerResult applied = worker.ApplyAsync(planPath,
                    journalPath, nonce).GetAwaiter().GetResult();
                Equal(DdfProductUpdateWorkerResultKind.AwaitingHealthCheck,
                    applied.Kind);
                Equal(4242, waiter.ProcessId);
                Equal(now.AddMinutes(-1), waiter.ProcessStartedUtc);
                Equal(DdfProductUpdateInstallationState.Verifying,
                    store.Read().State);
                Equal(true, Directory.Exists(plan.BackupRoot));
                Equal(false, Directory.Exists(plan.StagingRoot));
                SequenceEqual(newExe, File.ReadAllBytes(Path.Combine(
                    plan.TargetRoot, "DDFLanguageEditor.exe")));
                Equal("old-editor", File.ReadAllText(Path.Combine(
                    plan.BackupRoot, "DDFLanguageEditor.exe"), Encoding.UTF8));

                File.WriteAllText(Path.Combine(plan.TargetRoot,
                    "DDFLanguageEditor.exe"), "corrupted", Encoding.UTF8);
                DdfProductUpdateWorkerResult recovered = worker.Recover(planPath,
                    journalPath, nonce);
                Equal(DdfProductUpdateWorkerResultKind.RolledBack, recovered.Kind);
                Equal(DdfProductUpdateInstallationState.RolledBack,
                    store.Read().State);
                Equal("old-editor", File.ReadAllText(Path.Combine(
                    plan.TargetRoot, "DDFLanguageEditor.exe"), Encoding.UTF8));
                Equal(false, Directory.Exists(plan.BackupRoot));
                Equal(true, Directory.Exists(recovered.QuarantineRoot));
                Equal("corrupted", File.ReadAllText(Path.Combine(
                    recovered.QuarantineRoot, "DDFLanguageEditor.exe"),
                    Encoding.UTF8));
                Equal(DdfProductUpdateWorkerResultKind.CleanupCompleted,
                    worker.Cleanup(planPath, journalPath, nonce).Kind);
                Equal(false, Directory.Exists(recovered.QuarantineRoot));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void CompletesUpdatesOnlyAfterSuccessfulHealthCheck()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "ddf-update-health-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                DateTimeOffset now = new DateTimeOffset(2026, 10, 26, 19, 0, 0,
                    TimeSpan.Zero);
                byte[] executable = Encoding.UTF8.GetBytes("healthy-editor");
                var plan = new DdfProductUpdateInstallationPlan("portable-main",
                    DdfProductVersionCatalog.Current.ProductId, "0.12.5",
                    "0.13.6-beta.5", "win-x64", "x64", new string('a', 64),
                    "release-2026", new Uri(
                        "https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.5/editor.zip"),
                    100, new string('b', 64), Path.Combine(root, "target"),
                    Path.Combine(root, "staging"), Path.Combine(root, "backup"),
                    "DDFLanguageEditor.exe", 5252, now.AddMinutes(-1),
                    new string('7', 64), true, new[]
                    {
                        new DdfProductUpdateInstallationFile(
                            "DDFLanguageEditor.exe", executable.LongLength,
                            HashBytes(executable))
                    }, now);
                Directory.CreateDirectory(plan.TargetRoot);
                Directory.CreateDirectory(plan.BackupRoot);
                File.WriteAllBytes(Path.Combine(plan.TargetRoot,
                    "DDFLanguageEditor.exe"), executable);
                File.WriteAllText(Path.Combine(plan.BackupRoot,
                    "DDFLanguageEditor.exe"), "previous", Encoding.UTF8);
                string planPath = Path.Combine(root, "state", "plan.json");
                string journalPath = Path.Combine(root, "state", "journal.json");
                DdfProductUpdateInstallationJournalStore.WritePlan(planPath, plan);
                var store = new DdfProductUpdateInstallationJournalStore(
                    journalPath);
                DdfProductUpdateInstallationJournal journal = store.Create(
                    DdfProductUpdateInstallationJson.ComputePlanSha256(plan), now);
                foreach (DdfProductUpdateInstallationState state in new[]
                {
                    DdfProductUpdateInstallationState.DownloadAuthorized,
                    DdfProductUpdateInstallationState.Downloading,
                    DdfProductUpdateInstallationState.Staged,
                    DdfProductUpdateInstallationState.InstallAuthorized,
                    DdfProductUpdateInstallationState.WaitingForEditorExit,
                    DdfProductUpdateInstallationState.Applying,
                    DdfProductUpdateInstallationState.Verifying
                })
                    journal = store.Transition(journal.Revision, state,
                        now.AddSeconds(journal.Revision + 1));

                var health = new FixedProductUpdateHealthCheck(true);
                var worker = new DdfProductUpdateWorker(
                    new ImmediateProcessWaiter());
                DdfProductUpdateWorkerResult result = worker.FinalizeAsync(
                    planPath, journalPath, plan.Nonce, health).GetAwaiter()
                    .GetResult();
                Equal(DdfProductUpdateWorkerResultKind.Completed, result.Kind);
                Equal(1, health.Calls);
                Equal(DdfProductUpdateInstallationState.Completed,
                    store.Read().State);
                Equal(true, Directory.Exists(plan.BackupRoot));
                SequenceEqual(executable, File.ReadAllBytes(Path.Combine(
                    plan.TargetRoot, "DDFLanguageEditor.exe")));

                DdfProductUpdateStartupRecoveryStatus startup =
                    DdfProductUpdateStartupRecovery.Inspect(planPath,
                        journalPath);
                Equal(true, startup.HasState);
                Equal(DdfProductUpdateInstallationState.Completed,
                    startup.State.Value);
                Equal(DdfProductUpdateRecoveryAction.CleanupDeferred,
                    startup.Action);
                Equal(false, startup.RequiresAttention);
                Equal(DdfProductUpdateWorkerResultKind.CleanupCompleted,
                    worker.Cleanup(planPath, journalPath, plan.Nonce).Kind);
                Equal(false, Directory.Exists(plan.BackupRoot));
                Equal(DdfProductUpdateRecoveryAction.None,
                    DdfProductUpdateStartupRecovery.Inspect(planPath,
                        journalPath).Action);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void KeepsEmbeddedProductUpdateChecksHostControlled()
        {
            DdfProductComponentVersion component = DdfProductVersionCatalog.Current
                .EmbeddedComponents.Single(item => item.Id == "winforms-sdk");
            var provider = new RecordingProductUpdateProvider(request =>
                DdfProductUpdateResult.UpToDate(request.ComponentKind,
                    request.ComponentId, request.CurrentVersion,
                    request.CurrentVersion, request.Channel,
                    new DateTimeOffset(2026, 10, 24, 18, 0, 0,
                        TimeSpan.Zero)));
            DdfEditorProductUpdateService service =
                DdfEditorProductUpdateService.ForEmbeddedComponent(provider,
                    component.Id);

            Equal(0, provider.CheckCount);
            Equal(component.Id, service.ComponentId);
            Equal(component.Version, service.CurrentVersion);
            DdfProductUpdateResult result = service.CheckAsync(
                DdfProductUpdateChannel.Beta).GetAwaiter().GetResult();
            Equal(1, provider.CheckCount);
            Equal(DdfProductUpdateComponentKind.EmbeddedSdk,
                provider.LastRequest.ComponentKind);
            Equal(component.Id, provider.LastRequest.ComponentId);
            Equal(component.Version, provider.LastRequest.CurrentVersion);
            Equal(DdfProductUpdateChannel.Beta, provider.LastRequest.Channel);
            Equal(DdfProductUpdateState.UpToDate, result.State);

            var mismatchedProvider = new RecordingProductUpdateProvider(request =>
                DdfProductUpdateResult.UpToDate(
                    DdfProductUpdateComponentKind.Standalone,
                    DdfProductVersionCatalog.Current.ProductId,
                    request.CurrentVersion, request.CurrentVersion,
                    request.Channel, DateTimeOffset.UtcNow));
            var mismatched = new DdfEditorProductUpdateService(
                mismatchedProvider, component.Id, component.Version);
            Equal(DdfProductUpdateState.Unknown, mismatched.CheckAsync(
                DdfProductUpdateChannel.Stable).GetAwaiter().GetResult().State);
            Throws<ArgumentException>(() =>
                DdfEditorProductUpdateService.ForEmbeddedComponent(provider,
                    "unknown-sdk"));
        }

        private static void ChecksFixedGitHubReleaseFeedSafely()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 10, 24, 13, 0, 0,
                TimeSpan.Zero);
            string json = """
                [
                  {
                    "tag_name": "v0.13.6-beta.1",
                    "name": "DDFLanguageEditor 0.13.6 Beta 1",
                    "body": "Note verificate dal valutatore.",
                    "published_at": "2026-10-23T12:00:00Z",
                    "html_url": "https://github.com/DDF-Technology/DDFLanguageEditor/releases/tag/v0.13.6-beta.1",
                    "draft": false,
                    "prerelease": true,
                    "assets": [
                      {
                        "name": "ddf-release-manifest.json",
                        "browser_download_url": "https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.1/ddf-release-manifest.json",
                        "size": 512,
                        "content_type": "application/json"
                      }
                    ]
                  }
                ]
                """;
            var handler = new QueueHttpMessageHandler();
            handler.Enqueue((request, cancellationToken) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8,
                        "application/json")
                };
                response.Headers.ETag = new EntityTagHeaderValue("\"feed-1\"");
                response.Content.Headers.LastModified = now.AddMinutes(-5);
                return Task.FromResult(response);
            });
            handler.Enqueue((request, cancellationToken) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotModified)));
            var evaluator = new RecordingReleaseEvaluator();
            var options = new DdfGitHubReleaseProviderOptions
            {
                Timeout = TimeSpan.FromSeconds(2),
                MaximumResponseBytes = 4096,
                MaximumReleases = 2,
                MaximumAssetsPerRelease = 2,
                MaximumReleaseNotesCharacters = 1024
            };
            DdfProductUpdateRequest request =
                DdfProductUpdateRequest.ForStandalone();
            Throws<ArgumentException>(() => new DdfProductUpdateRequest(
                DdfProductUpdateComponentKind.Standalone, "invalid id",
                "not-a-version", DdfProductUpdateChannel.Beta));
            using (var provider = new DdfGitHubReleaseProvider(evaluator,
                handler, options, () => now))
            {
                DdfProductUpdateResult first = provider.CheckAsync(request)
                    .GetAwaiter().GetResult();
                DdfProductUpdateResult cached = provider.CheckAsync(request)
                    .GetAwaiter().GetResult();
                Equal(DdfProductUpdateState.UpToDate, first.State);
                Equal(DdfProductUpdateState.UpToDate, cached.State);
            }
            Equal(2, evaluator.CallCount);
            Equal(1, evaluator.LastReleases.Count);
            Equal("v0.13.6-beta.1", evaluator.LastReleases[0].Tag);
            Equal(true, evaluator.LastReleases[0].Prerelease);
            Equal(1, evaluator.LastReleases[0].Assets.Count);
            Equal(512L, evaluator.LastReleases[0].Assets[0].Size);
            Equal(2, handler.Requests.Count);
            Equal(DdfGitHubReleaseProvider.ReleasesEndpoint.AbsolutePath,
                handler.Requests[0].Uri.AbsolutePath);
            Equal("per_page=2", handler.Requests[0].Uri.Query.TrimStart('?'));
            Equal(false, handler.Requests[0].HasContent);
            Equal(false, handler.Requests[0].HasAuthorization);
            Equal("\"feed-1\"", handler.Requests[1].IfNoneMatch);
            Equal(now.AddMinutes(-5), handler.Requests[1].IfModifiedSince);

            DdfProductUpdateResult offline = CheckSingleResponse(request, now,
                new ThrowingHttpMessageHandler(new HttpRequestException("offline")),
                options);
            Equal(DdfProductUpdateState.Unknown, offline.State);
            Equal(true, offline.StatusDetail.Contains("offline",
                StringComparison.OrdinalIgnoreCase));

            DdfProductUpdateResult rateLimited = CheckSingleResponse(request,
                now, new QueueHttpMessageHandler(HttpStatusCode.TooManyRequests),
                options);
            Equal(DdfProductUpdateState.Unknown, rateLimited.State);
            Equal(true, rateLimited.StatusDetail.Contains("Limite richieste",
                StringComparison.Ordinal));

            var malformed = new QueueHttpMessageHandler();
            malformed.Enqueue((message, token) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{not-json", Encoding.UTF8,
                        "application/json")
                }));
            DdfProductUpdateResult invalidPayload = CheckSingleResponse(request,
                now, malformed, options);
            Equal(DdfProductUpdateState.Unknown, invalidPayload.State);
            Equal(true, invalidPayload.StatusDetail.Contains("non valida",
                StringComparison.OrdinalIgnoreCase));

            DdfProductUpdateResult emptyCache = CheckSingleResponse(request, now,
                new QueueHttpMessageHandler(HttpStatusCode.NotModified), options);
            Equal(DdfProductUpdateState.Unknown, emptyCache.State);
            Equal(true, emptyCache.StatusDetail.Contains("304",
                StringComparison.Ordinal));

            var smallOptions = new DdfGitHubReleaseProviderOptions
            {
                Timeout = TimeSpan.FromSeconds(1),
                MaximumResponseBytes = 1024
            };
            var oversized = new QueueHttpMessageHandler();
            oversized.Enqueue((message, token) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(new string('x', 1025))
                }));
            DdfProductUpdateResult tooLarge = CheckSingleResponse(request, now,
                oversized, smallOptions);
            Equal(DdfProductUpdateState.Unknown, tooLarge.State);
            Equal(true, tooLarge.StatusDetail.Contains("limite",
                StringComparison.OrdinalIgnoreCase));

            var timeoutOptions = new DdfGitHubReleaseProviderOptions
            {
                Timeout = TimeSpan.FromMilliseconds(20)
            };
            DdfProductUpdateResult timeout = CheckSingleResponse(request, now,
                new DelayedHttpMessageHandler(), timeoutOptions);
            Equal(DdfProductUpdateState.Unknown, timeout.State);
            Equal(true, timeout.StatusDetail.Contains("Timeout",
                StringComparison.Ordinal));

            using (var cancelledProvider = new DdfGitHubReleaseProvider(
                new RecordingReleaseEvaluator(), new DelayedHttpMessageHandler(),
                options, () => now))
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Throws<OperationCanceledException>(() => cancelledProvider
                    .CheckAsync(request, cancellation.Token).GetAwaiter()
                    .GetResult());
            }
        }

        private static DdfProductUpdateResult CheckSingleResponse(
            DdfProductUpdateRequest request, DateTimeOffset now,
            HttpMessageHandler handler,
            DdfGitHubReleaseProviderOptions options)
        {
            using var provider = new DdfGitHubReleaseProvider(
                new RecordingReleaseEvaluator(), handler, options, () => now);
            return provider.CheckAsync(request).GetAwaiter().GetResult();
        }

        private static void ExercisesProductUpdatesThroughIsolatedLoopbackServer()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 10, 25,
                9, 0, 0, TimeSpan.Zero);
            DdfProductUpdateRequest request =
                DdfProductUpdateRequest.ForStandalone();
            var options = new DdfGitHubReleaseProviderOptions
            {
                Timeout = TimeSpan.FromSeconds(2),
                MaximumResponseBytes = 4096,
                MaximumReleases = 4
            };

            using (var server = new LoopbackHttpServer(
                (headers, token) => Task.FromResult(LoopbackHttpResponse(
                    HttpStatusCode.OK, "[]", new[]
                    {
                        "ETag: \"loopback-1\"",
                        "Last-Modified: Sun, 25 Oct 2026 08:55:00 GMT"
                    })),
                (headers, token) => Task.FromResult(LoopbackHttpResponse(
                    HttpStatusCode.NotModified, string.Empty))))
            using (var provider = CreateLoopbackProvider(server,
                new RecordingReleaseEvaluator(), options, now))
            {
                Equal(DdfProductUpdateState.UpToDate, provider.CheckAsync(request)
                    .GetAwaiter().GetResult().State);
                Equal(DdfProductUpdateState.UpToDate, provider.CheckAsync(request)
                    .GetAwaiter().GetResult().State);
                server.Completion.GetAwaiter().GetResult();
                Equal(2, server.Requests.Count);
                if (!server.Requests[0].StartsWith(
                        "GET /releases?per_page=4 HTTP/1.1",
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Request-target loopback inatteso: " + server.Requests[0]);
                if (!server.Requests[1].Contains(
                        "If-None-Match: \"loopback-1\"",
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "ETag condizionale assente: " + server.Requests[1]);
                if (!server.Requests[1].Contains("If-Modified-Since:",
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Data condizionale assente: " + server.Requests[1]);
            }

            using (var redirectServer = new LoopbackHttpServer(
                (headers, token) => Task.FromResult(LoopbackHttpResponse(
                    HttpStatusCode.Redirect, string.Empty,
                    new[] { "Location: /redirected" }))))
            using (var provider = CreateLoopbackProvider(redirectServer,
                new RecordingReleaseEvaluator(), options, now))
            {
                DdfProductUpdateResult redirect = provider.CheckAsync(request)
                    .GetAwaiter().GetResult();
                redirectServer.Completion.GetAwaiter().GetResult();
                Equal(DdfProductUpdateState.Unknown, redirect.State);
                if (!redirect.StatusDetail.Contains("HTTP 302",
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Redirect non rifiutato esplicitamente: " +
                        redirect.StatusDetail);
                Equal(1, redirectServer.Requests.Count);
            }

            using (var delayedServer = new LoopbackHttpServer(
                async (headers, token) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    return Array.Empty<byte>();
                }))
            using (var provider = CreateLoopbackProvider(delayedServer,
                new RecordingReleaseEvaluator(), options, now))
            using (var cancellation = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(100)))
            {
                Throws<OperationCanceledException>(() => provider.CheckAsync(
                    request, cancellation.Token).GetAwaiter().GetResult());
                Equal(1, delayedServer.Requests.Count);
            }

            using (var sharedServer = new LoopbackHttpServer(
                (headers, token) => Task.FromResult(LoopbackHttpResponse(
                    HttpStatusCode.OK, "[]")),
                (headers, token) => Task.FromResult(LoopbackHttpResponse(
                    HttpStatusCode.OK, "[]"))))
            using (var first = CreateLoopbackProvider(sharedServer,
                new RecordingReleaseEvaluator(), options, now))
            using (var second = CreateLoopbackProvider(sharedServer,
                new RecordingReleaseEvaluator(), options, now))
            {
                Task.WaitAll(first.CheckAsync(request), second.CheckAsync(request));
                sharedServer.Completion.GetAwaiter().GetResult();
                Equal(2, sharedServer.Requests.Count);
                if (!sharedServer.Requests.All(value =>
                        !value.Contains("If-None-Match:",
                            StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(
                        "Cache condivisa tra provider distinti: " +
                        string.Join(" | ", sharedServer.Requests));
            }
        }

        private static int RunRealGitHubProductUpdateSmoke()
        {
            try
            {
                using var provider = new DdfGitHubReleaseProvider(
                    new DdfSignedGitHubReleaseEvaluator(
                        DdfOfficialReleaseTrustStore.Create()),
                    new DdfGitHubReleaseProviderOptions
                    {
                        Timeout = TimeSpan.FromSeconds(15),
                        MaximumResponseBytes = 1024 * 1024,
                        MaximumReleases = 20
                    });
                DdfProductUpdateResult result = provider.CheckAsync(
                    DdfProductUpdateRequest.ForStandalone()).GetAwaiter()
                    .GetResult();
                if (result.State == DdfProductUpdateState.Unknown)
                    throw new InvalidOperationException(result.StatusDetail);
                Console.WriteLine("DDF PRODUCT UPDATE GITHUB SMOKE OK: " +
                    result.CheckedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("DDF PRODUCT UPDATE GITHUB SMOKE FAILED: " +
                    exception.Message);
                return 1;
            }
        }

        private static DdfGitHubReleaseProvider CreateLoopbackProvider(
            LoopbackHttpServer server, IDdfProductReleaseEvaluator evaluator,
            DdfGitHubReleaseProviderOptions options, DateTimeOffset now) =>
            new DdfGitHubReleaseProvider(evaluator,
                new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.None,
                    UseProxy = false
                }, options, () => now, server.Endpoint);

        private static byte[] LoopbackHttpResponse(HttpStatusCode status,
            string body, IEnumerable<string> headers = null)
        {
            byte[] payload = Encoding.UTF8.GetBytes(body ?? string.Empty);
            string reason = status switch
            {
                HttpStatusCode.OK => "OK",
                HttpStatusCode.NotModified => "Not Modified",
                HttpStatusCode.Redirect => "Found",
                _ => status.ToString()
            };
            var builder = new StringBuilder()
                .Append("HTTP/1.1 ").Append((int)status).Append(' ')
                .Append(reason).Append("\r\n")
                .Append("Connection: close\r\n")
                .Append("Content-Type: application/json\r\n")
                .Append("Content-Length: ").Append(payload.Length)
                .Append("\r\n");
            foreach (string header in headers ?? Array.Empty<string>())
                builder.Append(header).Append("\r\n");
            builder.Append("\r\n");
            byte[] prefix = Encoding.ASCII.GetBytes(builder.ToString());
            var response = new byte[prefix.Length + payload.Length];
            Buffer.BlockCopy(prefix, 0, response, 0, prefix.Length);
            Buffer.BlockCopy(payload, 0, response, prefix.Length, payload.Length);
            return response;
        }

        private static void LoadsOfficialReleaseTrustStoreFailClosed()
        {
            DdfReleaseTrustStore trust = DdfOfficialReleaseTrustStore.Create();
            Equal("official-2026", DdfOfficialReleaseTrustStore.ActiveKeyId);
            Equal("cef7cb71ca0c95d06a4cf8b56d1996fe4e2dea0fd94ee490fa7184753465e204",
                DdfOfficialReleaseTrustStore.ActiveKeySha256);

            DdfProductVersionIdentity identity = DdfProductVersionCatalog.Current;
            var compatibility = new DdfReleaseCompatibility("0.34", "0.99",
                identity.PersistenceVersions.ToDictionary(item => item.Key,
                    item => item.Value, StringComparer.Ordinal));
            var asset = new DdfReleaseManifestAsset(
                DdfProductUpdateComponentKind.Standalone, identity.ProductId,
                "0.13.6-beta.1", "win-x64", "x64",
                new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.1/editor.zip"),
                1, new string('a', 64));
            DdfReleaseManifest unsigned = DdfReleaseManifest.CreateUnsigned(
                identity.ProductId, "0.13.6-beta.1",
                DdfProductUpdateChannel.Beta, DateTimeOffset.UtcNow,
                string.Empty, compatibility, new[] { asset });
            using ECDsa foreign = ECDsa.Create();
            foreign.GenerateKey(ECCurve.NamedCurves.nistP256);
            string forged = DdfReleaseManifestCodec.Serialize(
                DdfReleaseManifestSigner.Sign(unsigned,
                    DdfOfficialReleaseTrustStore.ActiveKeyId, foreign));
            Equal(false, DdfReleaseManifestVerifier.Verify(forged, trust).Success);

            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject matrix = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-version-matrix.json"), Encoding.UTF8)).AsObject();
            Equal(DdfOfficialReleaseTrustStore.SchemaVersion,
                matrix["schemas"]["releaseTrustStore"].GetValue<string>());
        }

        private static void EvaluatesOnlySignedCorrelatedGitHubReleases()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 10, 26, 14, 30, 0,
                TimeSpan.Zero);
            DdfProductVersionIdentity identity = DdfProductVersionCatalog.Current;
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var trust = new DdfReleaseTrustStore(
                new Dictionary<string, byte[]>
                {
                    ["test-release"] = key.ExportSubjectPublicKeyInfo()
                });
            string version = "0.13.6-beta.9";
            Uri zipUri = new Uri(
                "https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.9/DDFLanguageEditor-win-x64.zip");
            Uri manifestUri = new Uri(
                "https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.9/ddf-release-manifest.json");
            var compatibility = new DdfReleaseCompatibility("0.34", "0.99",
                identity.PersistenceVersions.ToDictionary(item => item.Key,
                    item => item.Value, StringComparer.Ordinal));
            var signedAsset = new DdfReleaseManifestAsset(
                DdfProductUpdateComponentKind.Standalone, identity.ProductId,
                version, "win-x64", "x64", zipUri, 1234,
                new string('a', 64));
            string manifest = DdfReleaseManifestCodec.Serialize(
                DdfReleaseManifestSigner.Sign(
                    DdfReleaseManifest.CreateUnsigned(identity.ProductId,
                        version, DdfProductUpdateChannel.Beta, now,
                        "Release ufficiale firmata.", compatibility,
                        new[] { signedAsset }), "test-release", key));
            var release = new DdfRemoteRelease("v" + version,
                "Signed release", string.Empty, now,
                new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/tag/v0.13.6-beta.9"),
                true, new[]
                {
                    new DdfRemoteReleaseAsset(
                        DdfSignedGitHubReleaseEvaluator.ManifestAssetName,
                        manifestUri, Encoding.UTF8.GetByteCount(manifest),
                        "application/json"),
                    new DdfRemoteReleaseAsset(
                        "DDFLanguageEditor-win-x64.zip", zipUri, 1234,
                        "application/zip")
                });
            var handler = new QueueHttpMessageHandler();
            handler.Enqueue((request, cancellationToken) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                response.Headers.Location = new Uri(
                    "https://release-assets.githubusercontent.com/manifest-token");
                return Task.FromResult(response);
            });
            handler.Enqueue((request, cancellationToken) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifest, Encoding.UTF8,
                        "application/json")
                }));
            using var evaluator = new DdfSignedGitHubReleaseEvaluator(trust,
                handler, runtimeIdentifier: "win-x64", architecture: "x64");
            DdfProductUpdateResult result = evaluator.EvaluateAsync(
                DdfProductUpdateRequest.ForStandalone(
                    DdfProductUpdateChannel.Beta), new[] { release }, now)
                .GetAwaiter().GetResult();
            Equal(DdfProductUpdateState.UpdateAvailable, result.State);
            Equal(true, result.IsActionable);
            Equal(zipUri, result.VerifiedReleaseUri);
            Equal(2, handler.Requests.Count);
            Equal(manifestUri, handler.Requests[0].Uri);
            Equal("release-assets.githubusercontent.com",
                handler.Requests[1].Uri.Host);
        }

        private static void SignsAndVerifiesCanonicalReleaseManifests()
        {
            DateTimeOffset published = new DateTimeOffset(2026, 10, 24,
                14, 0, 0, TimeSpan.Zero);
            DdfProductVersionIdentity identity = DdfProductVersionCatalog.Current;
            var persistence = identity.PersistenceVersions.ToDictionary(
                item => item.Key, item => item.Value, StringComparer.Ordinal);
            var compatibility = new DdfReleaseCompatibility("0.34", "0.99",
                persistence);
            string version = "0.13.6-beta.1";
            var asset = new DdfReleaseManifestAsset(
                DdfProductUpdateComponentKind.Standalone, identity.ProductId,
                version, "win-x64", "x64",
                new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.1/DDFLanguageEditor.zip"),
                123456, new string('a', 64));
            DdfReleaseManifest unsigned = DdfReleaseManifest.CreateUnsigned(
                identity.ProductId, version, DdfProductUpdateChannel.Beta,
                published, "Release firmata di prova.", compatibility,
                new[] { asset });

            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            byte[] publicKey = key.ExportSubjectPublicKeyInfo();
            DdfReleaseManifest signed = DdfReleaseManifestSigner.Sign(unsigned,
                "release-2026", key);
            string json = DdfReleaseManifestCodec.Serialize(signed);
            try { DdfReleaseManifestCodec.Parse(json); }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Parsing manifesto firmato: " +
                    exception, exception);
            }
            var trust = new DdfReleaseTrustStore(
                new Dictionary<string, byte[]>
                {
                    ["release-2026"] = publicKey
                });
            DdfReleaseManifestVerification verified =
                DdfReleaseManifestVerifier.Verify(json, trust);
            if (!verified.Success)
                throw new InvalidOperationException("Verifica firma iniziale: " +
                    string.Join(" | ", verified.Issues));
            Equal(version, verified.Manifest.Version);
            Equal("Release firmata di prova.", verified.Manifest.ReleaseNotes);
            SequenceEqual(DdfReleaseManifestCodec.CanonicalPayload(signed),
                DdfReleaseManifestCodec.CanonicalPayload(
                    DdfReleaseManifestCodec.Parse(json)));

            var request = DdfProductUpdateRequest.ForStandalone();
            DdfProductUpdateResult result = DdfReleaseUpdatePolicy.Evaluate(
                verified, request, "win-x64", "x64", published.AddHours(1));
            Equal(DdfProductUpdateState.UpdateAvailable, result.State);
            Equal(true, result.IsActionable);
            Equal(asset.Uri, result.VerifiedReleaseUri);

            var stableRequest = new DdfProductUpdateRequest(
                request.ComponentKind, request.ComponentId,
                request.CurrentVersion, DdfProductUpdateChannel.Stable);
            DdfProductUpdateResult prereleaseOnStable =
                DdfReleaseUpdatePolicy.Evaluate(verified, stableRequest,
                    "win-x64", "x64", published.AddHours(1));
            Equal(DdfProductUpdateState.Unknown, prereleaseOnStable.State);
            Equal(false, prereleaseOnStable.IsActionable);

            string tampered = json.Replace("Release firmata di prova.",
                "Release alterata.", StringComparison.Ordinal);
            DdfReleaseManifestVerification invalidSignature =
                DdfReleaseManifestVerifier.Verify(tampered, trust);
            Equal(false, invalidSignature.Success);
            Equal(DdfProductUpdateState.Unknown,
                DdfReleaseUpdatePolicy.Evaluate(invalidSignature, request,
                    "win-x64", "x64", published).State);

            using ECDsa otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var wrongTrust = new DdfReleaseTrustStore(
                new Dictionary<string, byte[]>
                {
                    ["other"] = otherKey.ExportSubjectPublicKeyInfo()
                });
            Equal(false, DdfReleaseManifestVerifier.Verify(json, wrongTrust)
                .Success);

            var foreignAsset = new DdfReleaseManifestAsset(
                DdfProductUpdateComponentKind.Standalone, identity.ProductId,
                version, "win-x64", "x64",
                new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.1/foreign.zip"),
                1, new string('e', 64));
            DdfReleaseManifest foreignUnsigned =
                DdfReleaseManifest.CreateUnsigned("another-product", version,
                    DdfProductUpdateChannel.Beta, published, string.Empty,
                    compatibility, new[] { foreignAsset });
            DdfReleaseManifestVerification foreignVerified =
                DdfReleaseManifestVerifier.Verify(
                    DdfReleaseManifestCodec.Serialize(
                        DdfReleaseManifestSigner.Sign(foreignUnsigned,
                            "release-2026", key)), trust);
            DdfProductUpdateResult foreign = DdfReleaseUpdatePolicy.Evaluate(
                foreignVerified, request, "win-x64", "x64", published);
            Equal(DdfProductUpdateState.Unknown, foreign.State);
            Equal(true, foreign.StatusDetail.Contains("prodotto",
                StringComparison.OrdinalIgnoreCase));

            string olderVersion = "0.12.4";
            var olderAsset = new DdfReleaseManifestAsset(
                DdfProductUpdateComponentKind.Standalone, identity.ProductId,
                olderVersion, "win-x64", "x64",
                new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.12.4/DDFLanguageEditor.zip"),
                1, new string('f', 64));
            DdfReleaseManifest olderUnsigned =
                DdfReleaseManifest.CreateUnsigned(identity.ProductId,
                    olderVersion, DdfProductUpdateChannel.Beta, published,
                    string.Empty, compatibility, new[] { olderAsset });
            DdfReleaseManifestVerification olderVerified =
                DdfReleaseManifestVerifier.Verify(
                    DdfReleaseManifestCodec.Serialize(
                        DdfReleaseManifestSigner.Sign(olderUnsigned,
                            "release-2026", key)), trust);
            DdfProductUpdateResult downgrade = DdfReleaseUpdatePolicy.Evaluate(
                olderVerified, request, "win-x64", "x64", published);
            Equal(DdfProductUpdateState.Unknown, downgrade.State);
            Equal(true, downgrade.StatusDetail.Contains("downgrade",
                StringComparison.OrdinalIgnoreCase));

            DdfReleaseManifest incompatibleUnsigned =
                DdfReleaseManifest.CreateUnsigned(identity.ProductId, version,
                    DdfProductUpdateChannel.Beta, published, string.Empty,
                    new DdfReleaseCompatibility("0.99", "1.0", persistence),
                    new[] { asset });
            DdfReleaseManifestVerification incompatibleVerified =
                DdfReleaseManifestVerifier.Verify(
                    DdfReleaseManifestCodec.Serialize(
                        DdfReleaseManifestSigner.Sign(incompatibleUnsigned,
                            "release-2026", key)), trust);
            DdfProductUpdateResult incompatible = DdfReleaseUpdatePolicy.Evaluate(
                incompatibleVerified, request, "win-x64", "x64", published);
            Equal(DdfProductUpdateState.Incompatible, incompatible.State);
            Equal(false, incompatible.IsActionable);

            var exactAsset = new DdfReleaseManifestAsset(
                DdfProductUpdateComponentKind.Standalone, identity.ProductId,
                version, "win-x64", "x64",
                new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.1/exact.zip"),
                1, new string('b', 64));
            var anyAsset = new DdfReleaseManifestAsset(
                DdfProductUpdateComponentKind.Standalone, identity.ProductId,
                version, "any", "any",
                new Uri("https://github.com/DDF-Technology/DDFLanguageEditor/releases/download/v0.13.6-beta.1/any.zip"),
                1, new string('c', 64));
            DdfReleaseManifest ambiguousUnsigned =
                DdfReleaseManifest.CreateUnsigned(identity.ProductId, version,
                    DdfProductUpdateChannel.Beta, published, string.Empty,
                    compatibility, new[] { exactAsset, anyAsset });
            DdfReleaseManifestVerification ambiguousVerified =
                DdfReleaseManifestVerifier.Verify(
                    DdfReleaseManifestCodec.Serialize(
                        DdfReleaseManifestSigner.Sign(ambiguousUnsigned,
                            "release-2026", key)), trust);
            DdfProductUpdateResult ambiguous = DdfReleaseUpdatePolicy.Evaluate(
                ambiguousVerified, request, "win-x64", "x64", published);
            Equal(DdfProductUpdateState.Unknown, ambiguous.State);
            Equal(true, ambiguous.StatusDetail.Contains("ambiguo",
                StringComparison.OrdinalIgnoreCase));

            Throws<ArgumentException>(() => DdfReleaseManifest.CreateUnsigned(
                identity.ProductId, version, DdfProductUpdateChannel.Beta,
                published, string.Empty, compatibility,
                new[] { exactAsset, exactAsset }));
            Throws<ArgumentException>(() => new DdfReleaseManifestAsset(
                DdfProductUpdateComponentKind.Standalone, identity.ProductId,
                version, "win-x64", "x64", new Uri("http://example.test/a"),
                1, new string('d', 64)));

            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-release-manifest.schema.json"),
                Encoding.UTF8)).AsObject();
            Equal("https://www.ddf.technology/schemas/release-manifest-1.0.json",
                schema["$id"].GetValue<string>());
            JsonObject matrix = JsonNode.Parse(File.ReadAllText(Path.Combine(
                projectRoot, "ddf-version-matrix.json"), Encoding.UTF8)).AsObject();
            Equal(DdfReleaseManifest.SchemaVersionValue,
                matrix["schemas"]["releaseManifest"].GetValue<string>());
        }

        private static void InspectsAndMigratesPersistedArtifactsAtomically()
        {
            DdfPersistenceSchemaContract projectContract =
                DdfPersistenceSchemaCatalog.Get(
                    DdfPersistenceArtifactKind.ProjectManifest);
            Equal("1.2", projectContract.CurrentSchemaVersion);
            Equal("1.1", projectContract.MigratableVersions.Single());

            var current = new DdfPersistenceArtifact(
                DdfPersistenceArtifactKind.ProjectManifest, "1.2",
                "{\"schemaVersion\":\"1.2\"}");
            DdfPersistenceMigrationResult currentResult =
                projectContract.Prepare(current);
            Equal(true, currentResult.Succeeded);
            Equal(current, currentResult.Original);
            Equal(current, currentResult.Prepared);
            Equal(DdfPersistenceInspectionStatus.Current,
                currentResult.Inspection.Status);

            var readable = new DdfPersistenceArtifact(
                DdfPersistenceArtifactKind.ProjectManifest, "1.1",
                "{\"schemaVersion\":\"1.1\",\"name\":\"original\"}");
            DdfPersistenceInspection inspection = projectContract.Inspect(readable);
            Equal(DdfPersistenceInspectionStatus.MigrationRequired,
                inspection.Status);
            Equal(true, inspection.CanMigrate);
            Equal(false, inspection.CanLoad);

            var migrator = new TestPersistenceMigrationProvider((source, target) =>
                new DdfPersistenceArtifact(source.Kind,
                    target.CurrentSchemaVersion,
                    "{\"schemaVersion\":\"1.2\",\"name\":\"migrated\"}"));
            DdfPersistenceMigrationResult migrated = projectContract.Prepare(
                readable, migrator);
            Equal(true, migrated.Succeeded);
            Equal(readable, migrated.Original);
            Equal("1.1", migrated.Original.SchemaVersion);
            Equal("{\"schemaVersion\":\"1.1\",\"name\":\"original\"}",
                migrated.Original.Payload);
            Equal("1.2", migrated.Prepared.SchemaVersion);
            Equal(1, migrator.CallCount);

            var unsupported = new DdfPersistenceArtifact(
                DdfPersistenceArtifactKind.ProjectManifest, "9.0", "future");
            DdfPersistenceMigrationResult rejected = projectContract.Prepare(
                unsupported, migrator);
            Equal(false, rejected.Succeeded);
            Equal(unsupported, rejected.Original);
            Equal(null, rejected.Prepared);
            Equal(DdfPersistenceInspectionStatus.UnsupportedVersion,
                rejected.Inspection.Status);
            Equal(1, migrator.CallCount);

            DdfPersistenceMigrationResult wrongKind = projectContract.Prepare(
                readable, new TestPersistenceMigrationProvider((source, target) =>
                    new DdfPersistenceArtifact(
                        DdfPersistenceArtifactKind.ScriptState,
                        target.CurrentSchemaVersion, "wrong")));
            Equal(false, wrongKind.Succeeded);
            Equal(readable, wrongKind.Original);
            Equal(null, wrongKind.Prepared);

            DdfPersistenceMigrationResult failed = projectContract.Prepare(
                readable, new TestPersistenceMigrationProvider((source, target) =>
                    throw new InvalidOperationException("conversione fallita")));
            Equal(false, failed.Succeeded);
            Equal(readable, failed.Original);
            Equal(true, failed.Failure.Contains("conversione fallita",
                StringComparison.Ordinal));

            Equal(10, DdfPersistenceSchemaCatalog.Contracts.Count);
            Equal("1.0", DdfPersistenceSchemaCatalog.Get(
                DdfPersistenceArtifactKind.ScriptState).CurrentSchemaVersion);
            Equal("1.0", DdfPersistenceSchemaCatalog.Get(
                DdfPersistenceArtifactKind.PaletteLayout).CurrentSchemaVersion);
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
            Equal(40, baseline["frozenCommit"].GetValue<string>().Length);

            JsonArray artifacts = baseline["artifacts"].AsArray();
            Equal(17, artifacts.Count);
            string[] paths = artifacts.Select(item => item.AsObject()["path"].GetValue<string>()).ToArray();
            Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
            JsonObject currentPackage = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "language-packages", "ddf", "manifest.json"), Encoding.UTF8)).AsObject();
            bool isCurrentCandidate = currentPackage["languageVersion"].GetValue<string>() ==
                baseline["languageVersion"].GetValue<string>();
            Equal(true, isCurrentCandidate ||
                Version.Parse(currentPackage["languageVersion"].GetValue<string>()) >
                Version.Parse(baseline["languageVersion"].GetValue<string>()));

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

                if (isCurrentCandidate)
                {
                    string normalized = File.ReadAllText(fullPath, Encoding.UTF8)
                        .Replace("\r\n", "\n", StringComparison.Ordinal)
                        .Replace("\r", "\n", StringComparison.Ordinal);
                    string actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
                        .ToLowerInvariant();
                    Equal(artifact["sha256"].GetValue<string>(), actual);
                }
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

        private static void KeepsCandidateIssuesClassified()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            JsonObject baseline = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-candidate-baseline.json"), Encoding.UTF8)).AsObject();
            JsonObject register = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-candidate-issues.json"), Encoding.UTF8)).AsObject();

            Equal("1.0", register["schemaVersion"].GetValue<string>());
            Equal(baseline["candidateId"].GetValue<string>(), register["candidateId"].GetValue<string>());
            Equal(true, DateOnly.TryParseExact(register["reviewedAt"].GetValue<string>(),
                "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));

            JsonArray issues = register["issues"].AsArray();
            string[] ids = issues.Select(item => item.AsObject()["id"].GetValue<string>()).ToArray();
            Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
            Equal(true, ids.All(id => Regex.IsMatch(id, "^CAND-[0-9]{4}$")));

            string[] areas = { "specification", "package", "analysis", "intellisense", "formatter",
                "debugger", "runtime", "build", "test-infrastructure", "documentation", "usability",
                "performance" };
            string[] severities = { "note", "minor", "major", "blocking" };
            string[] classifications = { "test-infrastructure", "implementation-conformance",
                "editorial", "compatible-addition", "breaking-contract" };
            string[] statuses = { "open", "accepted", "resolved", "deferred", "rejected" };
            foreach (JsonNode node in issues)
            {
                JsonObject issue = node.AsObject();
                Equal(true, !string.IsNullOrWhiteSpace(issue["title"].GetValue<string>()));
                Equal(true, areas.Contains(issue["area"].GetValue<string>(), StringComparer.Ordinal));
                Equal(true, severities.Contains(issue["severity"].GetValue<string>(), StringComparer.Ordinal));
                Equal(true, classifications.Contains(issue["classification"].GetValue<string>(), StringComparer.Ordinal));
                Equal(true, statuses.Contains(issue["status"].GetValue<string>(), StringComparer.Ordinal));
                Equal(true, issue["evidence"].AsArray().Count > 0);
                Equal(true, !string.IsNullOrWhiteSpace(issue["resolution"].GetValue<string>()));

                JsonObject impact = issue["normativeImpact"].AsObject();
                bool changesContract = impact["changesContract"].GetValue<bool>();
                if (changesContract)
                {
                    Equal(true, Regex.IsMatch(impact["decisionId"].GetValue<string>(), "^DDF-[0-9]{4}$"));
                    Equal(true, Regex.IsMatch(impact["targetLanguageVersion"].GetValue<string>(),
                        "^[0-9]+\\.[0-9]+$"));
                }
                else
                {
                    Equal(null, impact["decisionId"]?.GetValue<string>());
                    Equal(null, impact["targetLanguageVersion"]?.GetValue<string>());
                }
            }

            JsonObject summary = register["summary"].AsObject();
            Equal(issues.Count, summary["observations"].GetValue<int>());
            Equal(issues.Count(item => item.AsObject()["status"].GetValue<string>() == "open"),
                summary["open"].GetValue<int>());
            Equal(issues.Count(item => item.AsObject()["severity"].GetValue<string>() == "blocking" &&
                item.AsObject()["status"].GetValue<string>() == "open"),
                summary["blocking"].GetValue<int>());
            Equal(issues.Count(item => item.AsObject()["normativeImpact"].AsObject()
                ["changesContract"].GetValue<bool>()), summary["normativeChanges"].GetValue<int>());
        }

        private static void RejectsUngovernedCandidateChanges()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            JsonObject baseline = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-candidate-baseline.json"), Encoding.UTF8)).AsObject();
            JsonObject policy = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-candidate-change-control.json"), Encoding.UTF8)).AsObject();
            JsonObject register = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-candidate-issues.json"), Encoding.UTF8)).AsObject();
            string decisions = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_LANGUAGE_DECISIONS.md"), Encoding.UTF8);

            Equal("1.0", policy["schemaVersion"].GetValue<string>());
            Equal(baseline["candidateId"].GetValue<string>(), policy["candidateId"].GetValue<string>());
            Equal(baseline["languageVersion"].GetValue<string>(),
                policy["currentLanguageVersion"].GetValue<string>());
            JsonObject rule = policy["incompatibleChange"].AsObject();
            Equal("breaking-contract", rule["classification"].GetValue<string>());
            Equal(true, rule["requiresNormativeImpact"].GetValue<bool>());
            Equal(true, rule["requiresDecision"].GetValue<bool>());
            Equal("DDF_LANGUAGE_DECISIONS.md", rule["decisionRegistry"].GetValue<string>());
            Equal(true, rule["decisionMustReferenceIssue"].GetValue<bool>());
            Equal(true, rule["requiresNewLanguageVersion"].GetValue<bool>());
            Equal("greater-than-current-major-minor", rule["versionRule"].GetValue<string>());

            string currentVersion = policy["currentLanguageVersion"].GetValue<string>();
            foreach (JsonNode node in register["issues"].AsArray())
                Equal(0, GetCandidateChangeControlErrors(node.AsObject(), currentVersion, decisions).Count);

            JsonObject template = JsonNode.Parse("""
                {
                  "id": "CAND-9000",
                  "title": "Synthetic incompatible change",
                  "area": "specification",
                  "severity": "major",
                  "classification": "breaking-contract",
                  "status": "accepted",
                  "normativeImpact": {
                    "changesContract": true,
                    "decisionId": "DDF-0024",
                    "targetLanguageVersion": "0.13"
                  },
                  "evidence": [ "synthetic" ],
                  "resolution": "synthetic"
                }
                """).AsObject();

            JsonObject hiddenImpact = template.DeepClone().AsObject();
            hiddenImpact["normativeImpact"].AsObject()["changesContract"] = false;
            Equal(true, GetCandidateChangeControlErrors(hiddenImpact, currentVersion, decisions)
                .Contains("DDFCC001"));

            JsonObject missingDecision = template.DeepClone().AsObject();
            missingDecision["normativeImpact"].AsObject()["decisionId"] = null;
            Equal(true, GetCandidateChangeControlErrors(missingDecision, currentVersion, decisions)
                .Contains("DDFCC003"));

            JsonObject unknownDecision = template.DeepClone().AsObject();
            unknownDecision["normativeImpact"].AsObject()["decisionId"] = "DDF-9999";
            Equal(true, GetCandidateChangeControlErrors(unknownDecision, currentVersion, decisions)
                .Contains("DDFCC004"));

            JsonObject unchangedVersion = template.DeepClone().AsObject();
            unchangedVersion["normativeImpact"].AsObject()["targetLanguageVersion"] = currentVersion;
            Equal(true, GetCandidateChangeControlErrors(unchangedVersion, currentVersion, decisions)
                .Contains("DDFCC005"));

            IReadOnlyList<string> unlinked = GetCandidateChangeControlErrors(template,
                currentVersion, decisions);
            Equal(true, unlinked.Contains("DDFCC006"));
        }

        private static IReadOnlyList<string> GetCandidateChangeControlErrors(JsonObject issue,
            string currentVersion, string decisions)
        {
            var errors = new List<string>();
            string classification = issue["classification"].GetValue<string>();
            JsonObject impact = issue["normativeImpact"].AsObject();
            bool changesContract = impact["changesContract"].GetValue<bool>();
            if (classification == "breaking-contract" && !changesContract)
                errors.Add("DDFCC001");
            if (changesContract && classification != "breaking-contract" &&
                classification != "compatible-addition")
                errors.Add("DDFCC002");
            if (!changesContract) return errors;

            string decisionId = impact["decisionId"]?.GetValue<string>();
            string targetVersion = impact["targetLanguageVersion"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(decisionId)) errors.Add("DDFCC003");
            int decisionStart = string.IsNullOrWhiteSpace(decisionId) ? -1 :
                decisions.IndexOf("## " + decisionId + " ", StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(decisionId) && decisionStart < 0)
                errors.Add("DDFCC004");

            if (!Version.TryParse(currentVersion, out Version current) ||
                !Version.TryParse(targetVersion, out Version target) || target <= current)
                errors.Add("DDFCC005");

            if (decisionStart >= 0)
            {
                int decisionEnd = decisions.IndexOf("\n## ", decisionStart + 3,
                    StringComparison.Ordinal);
                string section = decisionEnd < 0 ? decisions.Substring(decisionStart) :
                    decisions.Substring(decisionStart, decisionEnd - decisionStart);
                string issueId = issue["id"].GetValue<string>();
                if (!section.Contains(issueId, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(targetVersion) ||
                    !section.Contains(targetVersion, StringComparison.Ordinal))
                    errors.Add("DDFCC006");
            }
            return errors;
        }

        private static void KeepsCandidateReadinessAlignedWithManualValidation()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            JsonObject baseline = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-candidate-baseline.json"), Encoding.UTF8)).AsObject();
            JsonObject evaluation = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-candidate-evaluation.json"), Encoding.UTF8)).AsObject();
            JsonObject issues = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-language-candidate-issues.json"), Encoding.UTF8)).AsObject();

            Equal("1.0", evaluation["schemaVersion"].GetValue<string>());
            Equal(baseline["candidateId"].GetValue<string>(),
                evaluation["candidateId"].GetValue<string>());
            JsonObject automated = evaluation["automated"].AsObject();
            Equal("passed", automated["status"].GetValue<string>());
            Equal(true, automated["coreTestsPassed"].GetValue<int>() >= 224);
            Equal(76, automated["menuCommandsCovered"].GetValue<int>());
            Equal(0, automated["releaseWarnings"].GetValue<int>());
            Equal(0, automated["releaseErrors"].GetValue<int>());

            JsonObject manual = evaluation["manual"].AsObject();
            string checklist = manual["checklist"].GetValue<string>();
            Equal(true, File.Exists(Path.Combine(projectRoot, checklist)));
            int open = issues["summary"].AsObject()["open"].GetValue<int>();
            int blocking = issues["summary"].AsObject()["blocking"].GetValue<int>();
            string readiness = evaluation["readiness"].GetValue<string>();
            bool mayBeReady = automated["status"].GetValue<string>() == "passed" &&
                manual["status"].GetValue<string>() == "passed" && open == 0 && blocking == 0;
            Equal(mayBeReady ? "ready" : "not-ready", readiness);
            Equal("passed", manual["status"].GetValue<string>());
            Equal(true, !string.IsNullOrWhiteSpace(manual["completedBy"]?.GetValue<string>()));
        }

        private static void KeepsSemanticCheckpointAutomationAndManualApprovalSeparate()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            string script = File.ReadAllText(Path.Combine(projectRoot, "tools",
                "run-semantic-checkpoint.ps1"), Encoding.UTF8);
            string checklist = File.ReadAllText(Path.Combine(projectRoot,
                "DDF_SEMANTIC_CHECKPOINT_MANUAL_SESSION.md"), Encoding.UTF8);
            JsonObject schema = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-semantic-checkpoint-receipt.schema.json"), Encoding.UTF8)).AsObject();

            string[] requiredStages =
            {
                "release-build",
                "core",
                "backend-equivalence",
                "deterministic-build",
                "winforms-smoke",
                "release-performance"
            };
            foreach (string stage in requiredStages)
                Equal(true, script.Contains("'" + stage + "'", StringComparison.Ordinal));

            Equal(true, script.Contains("--backend-equivalence-only",
                StringComparison.Ordinal));
            Equal(true, script.Contains("--deterministic-build-only",
                StringComparison.Ordinal));
            Equal(true, script.Contains("--realistic-performance-only",
                StringComparison.Ordinal));
            Equal(true, script.Contains("status = 'pending'", StringComparison.Ordinal));
            Equal(true, script.Contains("readiness = 'not-ready'", StringComparison.Ordinal));
            Equal(false, script.Contains("readiness = 'ready'", StringComparison.Ordinal));
            Equal(true, checklist.Contains("La ricevuta non deve mai essere promossa automaticamente",
                StringComparison.Ordinal));
            Equal("1.0", schema["properties"].AsObject()["schemaVersion"].AsObject()["const"]
                .GetValue<string>());
        }

        private static void IsolatesRoslynFromCoreWinFormsAndEmbeddedSdk()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", ".."));
            JsonObject versionMatrix = JsonNode.Parse(File.ReadAllText(Path.Combine(projectRoot,
                "ddf-version-matrix.json"), Encoding.UTF8)).AsObject();
            JsonObject boundary = versionMatrix["embeddedBoundary"].AsObject();
            string coreProject = Path.Combine(projectRoot, "DDFLanguageEditor.Core",
                "DDFLanguageEditor.Core.csproj");
            string winFormsProject = Path.Combine(projectRoot,
                "DDF - Program Language Editor.csproj");
            string compilerProject = Path.Combine(projectRoot, "DDFLanguageEditor.Compiler",
                "DDFLanguageEditor.Compiler.csproj");
            string appProject = Path.Combine(projectRoot, "DDFLanguageEditor.App",
                "DDFLanguageEditor.App.csproj");
            string runnerProject = Path.Combine(projectRoot,
                "DDFLanguageEditor.Runner", "DDFLanguageEditor.Runner.csproj");
            string cliProject = Path.Combine(projectRoot,
                "DDFLanguageEditor.Cli", "DDFLanguageEditor.Cli.csproj");
            string compilerTestsProject = Path.Combine(projectRoot, "tests",
                "DDFLanguageEditor.Compiler.Tests",
                "DDFLanguageEditor.Compiler.Tests.csproj");
            string cliTestsProject = Path.Combine(projectRoot, "tests",
                "DDFLanguageEditor.Cli.Tests",
                "DDFLanguageEditor.Cli.Tests.csproj");
            string editorSmokeTestsProject = Path.Combine(projectRoot, "tests",
                "DDFLanguageEditor.EditorSmokeTests",
                "DDFLanguageEditor.EditorSmokeTests.csproj");

            string[] boundaryProjects = boundary["projects"].AsArray().Select(node =>
                Path.GetFullPath(Path.Combine(projectRoot, node.GetValue<string>().Replace('/',
                    Path.DirectorySeparatorChar)))).ToArray();
            SequenceEqual(new[] { Path.GetFullPath(coreProject), Path.GetFullPath(winFormsProject) },
                boundaryProjects);
            string[] forbiddenProjectSuffixes = boundary["forbiddenProjectSuffixes"].AsArray()
                .Select(node => node.GetValue<string>()).ToArray();
            string[] forbiddenPackagePrefixes = boundary["forbiddenPackagePrefixes"].AsArray()
                .Select(node => node.GetValue<string>()).ToArray();
            foreach (string forbiddenProject in boundaryProjects)
            {
                XDocument document = XDocument.Load(forbiddenProject);
                Equal(false, ProjectItems(document, "PackageReference").Any(item =>
                    forbiddenPackagePrefixes.Any(prefix => ItemIdentity(item).StartsWith(prefix,
                        StringComparison.OrdinalIgnoreCase))));
                Equal(false, ProjectItems(document, "ProjectReference").Any(item =>
                    forbiddenProjectSuffixes.Any(suffix => ItemIdentity(item).EndsWith(suffix,
                        StringComparison.OrdinalIgnoreCase))));
            }

            Regex[] forbiddenSourcePatterns = boundary["forbiddenSourcePatterns"].AsArray()
                .Select(node => new Regex(node.GetValue<string>(),
                    RegexOptions.CultureInvariant)).ToArray();
            foreach (JsonNode node in boundary["sourceScopes"].AsArray())
            {
                JsonObject scope = node.AsObject();
                string sourceRoot = Path.GetFullPath(Path.Combine(projectRoot,
                    scope["path"].GetValue<string>().Replace('/', Path.DirectorySeparatorChar)));
                SearchOption search = scope["recursive"].GetValue<bool>()
                    ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var excluded = new HashSet<string>(scope["exclude"].AsArray()
                    .Select(item => item.GetValue<string>()), StringComparer.OrdinalIgnoreCase);
                foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*.cs", search)
                    .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" +
                            Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                        !path.Contains(Path.DirectorySeparatorChar + "obj" +
                            Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                        !excluded.Contains(Path.GetFileName(path))))
                {
                    string source = File.ReadAllText(sourcePath, Encoding.UTF8);
                    Equal(false, forbiddenSourcePatterns.Any(pattern => pattern.IsMatch(source)));
                }
            }

            XDocument winForms = XDocument.Load(winFormsProject);
            string defaultExcludes = winForms.Descendants().Single(element =>
                element.Name.LocalName == "DefaultItemExcludes").Value;
            Equal(true, defaultExcludes.Contains("DDFLanguageEditor.Compiler\\**",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, defaultExcludes.Contains("DDFLanguageEditor.Runner\\**",
                StringComparison.OrdinalIgnoreCase));
            Equal(true, defaultExcludes.Contains("DDFLanguageEditor.Cli\\**",
                StringComparison.OrdinalIgnoreCase));

            XDocument compiler = XDocument.Load(compilerProject);
            XElement roslyn = ProjectItems(compiler, "PackageReference").Single(item =>
                string.Equals(ItemIdentity(item), "Microsoft.CodeAnalysis.CSharp",
                    StringComparison.OrdinalIgnoreCase));
            Equal("5.9.0", roslyn.Attribute("Version")?.Value);

            var allowedConsumers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.GetFullPath(appProject),
                Path.GetFullPath(runnerProject),
                Path.GetFullPath(cliProject),
                Path.GetFullPath(compilerTestsProject),
                Path.GetFullPath(cliTestsProject),
                Path.GetFullPath(editorSmokeTestsProject)
            };
            foreach (string project in Directory.EnumerateFiles(projectRoot, "*.csproj",
                SearchOption.AllDirectories).Where(path => !path.Contains(
                    Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)))
            {
                XDocument document = XDocument.Load(project);
                bool consumesCompiler = ProjectItems(document, "ProjectReference").Any(item =>
                    ItemIdentity(item).EndsWith("DDFLanguageEditor.Compiler.csproj",
                        StringComparison.OrdinalIgnoreCase));
                if (consumesCompiler)
                    Equal(true, allowedConsumers.Contains(Path.GetFullPath(project)));
            }

            string solution = File.ReadAllText(Path.Combine(projectRoot,
                "DDF - Program Language Editor.sln"), Encoding.UTF8);
            Equal(true, solution.Contains(
                "DDFLanguageEditor.Compiler\\DDFLanguageEditor.Compiler.csproj",
                StringComparison.Ordinal));
        }

        private static IEnumerable<XElement> ProjectItems(XDocument document, string localName) =>
            document.Descendants().Where(element => element.Name.LocalName == localName);

        private static string ItemIdentity(XElement element) =>
            element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? string.Empty;

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
                manifest.References.Add(new DdfProjectExternalReference
                {
                    Id = "microsoft.data.sqlclient",
                    Kind = DdfExternalReferenceKinds.ManagedDotNet,
                    Version = "6.1.0",
                    Architecture = DdfExternalReferenceArchitectures.Any,
                    Origin = DdfExternalReferenceOrigins.Configured,
                    Capabilities = new List<string> { "database.read" }
                });
                manifest.References.Add(new DdfProjectExternalReference
                {
                    Id = "microsoft.ado",
                    Kind = DdfExternalReferenceKinds.Com,
                    Version = "6.1",
                    Architecture = DdfExternalReferenceArchitectures.X64,
                    Origin = DdfExternalReferenceOrigins.System,
                    Capabilities = new List<string> { "database.legacy" }
                });
                manifest.References.Add(new DdfProjectExternalReference
                {
                    Id = "vendor.math",
                    Kind = DdfExternalReferenceKinds.Native,
                    Version = "1.0.0",
                    Architecture = DdfExternalReferenceArchitectures.X64,
                    Origin = DdfExternalReferenceOrigins.Project
                });
                manifest.References.Add(new DdfProjectExternalReference
                {
                    Id = "host.analytics",
                    Kind = DdfExternalReferenceKinds.HostService,
                    Version = "1.0.0",
                    Architecture = DdfExternalReferenceArchitectures.Any,
                    Origin = DdfExternalReferenceOrigins.Host,
                    Capabilities = new List<string> { "analytics.invoke" }
                });
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
                Equal(4, loaded.Manifest.References.Count);
                Equal(DdfExternalReferenceKinds.ManagedDotNet,
                    loaded.Manifest.References[0].Kind);
                Equal(DdfExternalReferenceKinds.Com,
                    loaded.Manifest.References[1].Kind);
                Equal(DdfExternalReferenceKinds.Native,
                    loaded.Manifest.References[2].Kind);
                Equal(DdfExternalReferenceKinds.HostService,
                    loaded.Manifest.References[3].Kind);
                SequenceEqual(new[] { "database.read" },
                    loaded.Manifest.References[0].Capabilities);
                Equal(true, typeof(IDdfBuildBackend).IsAssignableFrom(typeof(DdfVmBuildBackend)));

                manifest.SchemaVersion = "1.1";
                manifest.References = null;
                File.WriteAllText(Path.Combine(root,
                    DdfProjectManifest.FileName), JsonSerializer.Serialize(
                    manifest), new UTF8Encoding(false));
                DdfProjectManifestLoadResult migrated =
                    DdfProjectManifestLoader.Load(root);
                Equal(true, migrated.Success);
                Equal(DdfProjectManifest.SchemaVersionValue,
                    migrated.Manifest.SchemaVersion);
                Equal(0, migrated.Manifest.References.Count);
                Equal(true, migrated.Issues.Any(item =>
                    item.Code == "DDFP021" && item.Severity ==
                    DdfDiagnosticSeverity.Warning));
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

        private static void DefinesImmutableBackendNeutralCompilationUnits()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
                string declarationsPath = Path.Combine(root, "declarations.ddf");
                string mainPath = Path.Combine(root, "main.ddf");
                const string declarations =
                    "struct Box { int value; }\n" +
                    "int offset << 2;\n" +
                    "add(int left, int right) out int { ret left + right + offset; }";
                const string main = "main() out int { ret add(3, 4); }";
                File.WriteAllText(declarationsPath, declarations, new UTF8Encoding(false));
                File.WriteAllText(mainPath, main, new UTF8Encoding(false));
                DdfProjectManifest manifest = DdfProjectManifest.CreateImplicit(
                    "Compilation unit", package, "main.ddf");
                manifest.Module.Id = "example.contract";
                manifest.Module.Version = "1.2.0";
                manifest.Module.Exports.AddRange(new[] { "main", "add", "Box", "offset" });

                DdfBuildResult first = new DdfBuildService().Validate(root, manifest, package);
                Equal(true, first.Success);
                DdfCompilationUnit unit = first.Artifact.CompilationUnits.Single();
                Equal("1.0", unit.Version);
                Equal("example.contract@1.2.0", unit.Identity);
                Equal(package.Id, unit.LanguageId);
                Equal(package.PackageVersion, unit.LanguagePackageVersion);
                Equal(DdfIrProgram.FormatVersion, unit.IrFormatVersion);
                SequenceEqual(new[] { "declarations.ddf", "main.ddf" },
                    unit.Sources.Select(item => item.RelativePath));
                SequenceEqual(unit.Sources.Select(item => item.RelativePath),
                    unit.IntermediateRepresentation.Select(item => item.RelativePath));
                Equal(declarations, unit.Sources.First().SourceText);
                Equal(64, unit.Sources.First().Sha256.Length);
                Equal(false, unit.Sources.Any(item => item.FromUnsavedBuffer));
                SequenceEqual(new[] { "Box", "add", "main", "offset" },
                    unit.PublicInterface.Exports.Select(item => item.Name));
                Equal("add(int,int)->int", unit.PublicInterface.Exports
                    .Single(item => item.Name == "add").Signature);
                Equal(DdfCompilationExportKind.Structure, unit.PublicInterface.Exports
                    .Single(item => item.Name == "Box").Kind);
                Equal(DdfCompilationExportKind.Global, unit.PublicInterface.Exports
                    .Single(item => item.Name == "offset").Kind);
                Equal(64, unit.Fingerprint.Length);
                Throws<NotSupportedException>(() =>
                    ((IList<DdfCompilationSourceSnapshot>)unit.Sources).Add(null));
                Throws<NotSupportedException>(() =>
                    ((IList<DdfCompilationExport>)unit.PublicInterface.Exports).Add(null));

                DdfBuildResult second = new DdfBuildService().Validate(root, manifest, package);
                Equal(unit.Fingerprint, second.Artifact.CompilationUnits.Single().Fingerprint);
                var buffers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [mainPath] = "main() out int { ret add(4, 4); }"
                };
                DdfBuildResult changed = new DdfBuildService().Validate(root, manifest,
                    package, buffers);
                Equal(true, changed.Success);
                Equal(false, unit.Fingerprint ==
                    changed.Artifact.CompilationUnits.Single().Fingerprint);
                Equal(true, changed.Artifact.CompilationUnits.Single().Sources
                    .Single(item => item.RelativePath == "main.ddf").FromUnsavedBuffer);

                string receipt = JsonSerializer.Serialize(first.Artifact);
                Equal(false, receipt.Contains("SourceText", StringComparison.Ordinal));
                Equal(false, receipt.Contains("IntermediateRepresentation", StringComparison.Ordinal));
                Equal(false, receipt.Contains("CompilationUnits", StringComparison.Ordinal));
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

                DdfProjectManifest invalidReferences =
                    DdfProjectManifest.CreateImplicit("Invalid references",
                        package, "main.ddf");
                invalidReferences.References.Add(new DdfProjectExternalReference
                {
                    Id = "legacy.component",
                    Kind = DdfExternalReferenceKinds.Com,
                    Version = "1.0",
                    Architecture = DdfExternalReferenceArchitectures.Any,
                    Origin = DdfExternalReferenceOrigins.System
                });
                invalidReferences.References.Add(new DdfProjectExternalReference
                {
                    Id = "legacy.component",
                    Kind = DdfExternalReferenceKinds.HostService,
                    Version = "1.0",
                    Architecture = DdfExternalReferenceArchitectures.Any,
                    Origin = DdfExternalReferenceOrigins.Configured
                });
                File.WriteAllText(Path.Combine(root,
                    DdfProjectManifest.FileName), JsonSerializer.Serialize(
                    invalidReferences), new UTF8Encoding(false));
                DdfProjectManifestLoadResult invalidReferenceLoad =
                    DdfProjectManifestLoader.Load(root);
                Equal(false, invalidReferenceLoad.Success);
                Equal(true, invalidReferenceLoad.Issues.Any(item =>
                    item.Code == "DDFP018"));
                Equal(true, invalidReferenceLoad.Issues.Any(item =>
                    item.Code == "DDFP019"));

                invalidReferences.References = null;
                File.WriteAllText(Path.Combine(root,
                    DdfProjectManifest.FileName), JsonSerializer.Serialize(
                    invalidReferences), new UTF8Encoding(false));
                DdfProjectManifestLoadResult missingReferencesLoad =
                    DdfProjectManifestLoader.Load(root);
                Equal(false, missingReferencesLoad.Success);
                Equal(true, missingReferencesLoad.Issues.Any(item =>
                    item.Code == "DDFP020"));

                DdfProjectManifest unresolvedReference =
                    DdfProjectManifest.CreateImplicit("Unresolved reference",
                        package, "main.ddf");
                unresolvedReference.References.Add(
                    new DdfProjectExternalReference
                    {
                        Id = "vendor.analytics",
                        Kind = DdfExternalReferenceKinds.ManagedDotNet,
                        Version = "1.0.0",
                        Architecture = DdfExternalReferenceArchitectures.Any,
                        Origin = DdfExternalReferenceOrigins.Configured
                    });
                DdfBuildResult unresolvedResult = new DdfBuildService()
                    .Validate(root, unresolvedReference, package);
                Equal(false, unresolvedResult.Success);
                Equal(true, unresolvedResult.Issues.Any(item =>
                    item.Code == "DDFB018" && item.Domain ==
                    DdfDiagnosticDomain.EngineCapability));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void ResolvesExternalReferencesThroughAuthorizedCatalog()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                File.WriteAllText(Path.Combine(root, "main.ddf"),
                    "main() out int { ret 9; }", new UTF8Encoding(false));
                DdfProjectManifest plain = DdfProjectManifest.CreateImplicit(
                    "Reference baseline", package, "main.ddf");
                DdfBuildResult baseline = new DdfBuildService().Validate(root,
                    plain, package);
                Equal(true, baseline.Success);

                DdfProjectManifest managed = DdfProjectManifest.CreateImplicit(
                    "Managed reference", package, "main.ddf");
                managed.References.Add(new DdfProjectExternalReference
                {
                    Id = "ddf.core.contract",
                    Kind = DdfExternalReferenceKinds.ManagedDotNet,
                    Version = "1.0.0",
                    Architecture = DdfExternalReferenceArchitectures.Any,
                    Origin = DdfExternalReferenceOrigins.Configured,
                    Capabilities = new List<string> { "library.invoke" }
                });
                int authorizations = 0;
                var managedResolver = new
                    DdfExternalReferenceCatalogResolver(new[]
                    {
                        DdfExternalReferenceCatalogEntry.ManagedAssembly(
                            "ddf.core.contract", "1.0.0", "any",
                            "configured", new[] { "library.invoke" },
                            typeof(DdfBuildService).Assembly.Location,
                            new[]
                            {
                                new DdfExternalReferenceCatalogDependency(
                                    "system.text.json", "10.0.0",
                                    typeof(JsonDocument).Assembly.Location)
                            })
                    }, request =>
                    {
                        authorizations++;
                        return request.Context ==
                                   DdfExternalReferenceContext.Standalone &&
                               request.Reference.Id == "ddf.core.contract";
                    });
                var managedService = new DdfBuildService(
                    externalReferenceResolver: managedResolver,
                    externalReferenceContext:
                    DdfExternalReferenceContext.Standalone);
                DdfBuildResult managedResult = managedService.Validate(root,
                    managed, package);
                if (!managedResult.Success)
                    throw new InvalidOperationException(string.Join(" | ",
                        managedResult.Issues.Select(item => item.ToString())));
                Equal(true, managedResult.Success);
                Equal(1, authorizations);
                Equal(1, managedResult.ResolvedExternalReferences.Count);
                Equal(true, managedResult.ResolvedExternalReferences[0]
                    .ApiDefinition.IsEmpty);
                Equal(false, baseline.Artifact.Fingerprint ==
                    managedResult.Artifact.Fingerprint);

                DdfProjectManifest embedded = DdfProjectManifest
                    .CreateImplicit("Host adapter", package, "main.ddf");
                embedded.References.Add(new DdfProjectExternalReference
                {
                    Id = "host.analytics",
                    Kind = DdfExternalReferenceKinds.HostService,
                    Version = "1.0.0",
                    Architecture = DdfExternalReferenceArchitectures.Any,
                    Origin = DdfExternalReferenceOrigins.Host,
                    Capabilities = new List<string> { "analytics.invoke" }
                });
                var adapterResolver = new
                    DdfExternalReferenceCatalogResolver(new[]
                    {
                        DdfExternalReferenceCatalogEntry.TypedAdapter(
                            "host.analytics", "host-service", "1.0.0",
                            "any", "host", new[] { "analytics.invoke" },
                            "host.analytics.v1")
                    }, _ => true);
                DdfBuildResult embeddedResult = new DdfBuildService(
                    externalReferenceResolver: adapterResolver,
                    externalReferenceContext:
                    DdfExternalReferenceContext.Embedded).Validate(root,
                    embedded, package);
                if (!embeddedResult.Success)
                    throw new InvalidOperationException(string.Join(" | ",
                        embeddedResult.Issues.Select(item => item.ToString())));
                Equal(true, embeddedResult.Success);

                var deniedResolver = new
                    DdfExternalReferenceCatalogResolver(new[]
                    {
                        DdfExternalReferenceCatalogEntry.TypedAdapter(
                            "host.analytics", "host-service", "1.0.0",
                            "any", "host", new[] { "analytics.invoke" },
                            "host.analytics.v1")
                    }, _ => false);
                DdfBuildResult denied = new DdfBuildService(
                    externalReferenceResolver: deniedResolver,
                    externalReferenceContext:
                    DdfExternalReferenceContext.Embedded).Validate(root,
                    embedded, package);
                Equal(false, denied.Success);
                Equal(true, denied.Issues.Any(item => item.Code ==
                    "DDFR002"));

                var contextResolver = new
                    DdfExternalReferenceCatalogResolver(new[]
                    {
                        DdfExternalReferenceCatalogEntry.ManagedAssembly(
                            "ddf.core.contract", "1.0.0", "any",
                            "configured", new[] { "library.invoke" },
                            typeof(DdfBuildService).Assembly.Location)
                    }, _ => true);
                DdfBuildResult wrongContext = new DdfBuildService(
                    externalReferenceResolver: contextResolver,
                    externalReferenceContext:
                    DdfExternalReferenceContext.Embedded).Validate(root,
                    managed, package);
                Equal(false, wrongContext.Success);
                Equal(true, wrongContext.Issues.Any(item => item.Code ==
                    "DDFR003"));

                DdfBuildResult locked = managedService.Build(root, managed,
                    package);
                Equal(true, locked.Success);
                string lockPath = Path.Combine(root,
                    managed.Output.Directory,
                    DdfExternalReferenceLockFile.FileName);
                Equal(true, File.Exists(lockPath));
                using (JsonDocument lockDocument = JsonDocument.Parse(
                           File.ReadAllText(lockPath, Encoding.UTF8)))
                {
                    JsonElement lockRoot = lockDocument.RootElement;
                    Equal(DdfExternalReferenceLockFile.FormatValue,
                        lockRoot.GetProperty("format").GetString());
                    Equal(64, lockRoot.GetProperty("fingerprint")
                        .GetString().Length);
                    JsonElement lockedReference = lockRoot
                        .GetProperty("references")[0];
                    Equal("ddf.core.contract", lockedReference
                        .GetProperty("id").GetString());
                    Equal(64, lockedReference.GetProperty("sha256")
                        .GetString().Length);
                    Equal(64, lockedReference.GetProperty("apiFingerprint")
                        .GetString().Length);
                    Equal("system.text.json", lockedReference
                        .GetProperty("dependencies")[0].GetProperty("id")
                        .GetString());
                }
                Equal(true, managedService.Build(root, managed, package)
                    .Success);

                string lockText = File.ReadAllText(lockPath, Encoding.UTF8);
                using (JsonDocument lockDocument = JsonDocument.Parse(
                           lockText))
                {
                    string fingerprint = lockDocument.RootElement
                        .GetProperty("fingerprint").GetString();
                    File.WriteAllText(lockPath, lockText.Replace(fingerprint,
                        new string('0', 64), StringComparison.Ordinal),
                        new UTF8Encoding(false));
                }
                DdfBuildResult alteredLock = managedService.Validate(root,
                    managed, package);
                Equal(false, alteredLock.Success);
                Equal(true, alteredLock.Issues.Any(item => item.Code ==
                    "DDFR010"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void LocatesExternalReferenceDiagnosticsInManifest()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;
                File.WriteAllText(Path.Combine(root, "main.ddf"),
                    "main() out int { ret 0; }", new UTF8Encoding(false));
                DdfProjectManifest project = DdfProjectManifest.CreateImplicit(
                    "Riferimenti esterni è", package, "main.ddf");
                project.References.Add(new DdfProjectExternalReference
                {
                    Id = "host.analytics",
                    Kind = DdfExternalReferenceKinds.HostService,
                    Version = "2.0.0",
                    Architecture = DdfExternalReferenceArchitectures.X64,
                    Origin = DdfExternalReferenceOrigins.Host,
                    Capabilities = new List<string> { "analytics.invoke" }
                });
                string manifestPath = Path.Combine(root,
                    DdfProjectManifest.FileName);
                string manifestSource = JsonSerializer.Serialize(project,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(manifestPath, manifestSource,
                    new UTF8Encoding(false));

                var wrongVersion = new DdfExternalReferenceCatalogResolver(
                    new[]
                    {
                        DdfExternalReferenceCatalogEntry.TypedAdapter(
                            "host.analytics", DdfExternalReferenceKinds
                                .HostService, "1.0.0",
                            DdfExternalReferenceArchitectures.X86,
                            DdfExternalReferenceOrigins.Host,
                            new[] { "analytics.invoke" },
                            "host.analytics.v1")
                    }, _ => true);
                DdfBuildIssue versionIssue = new DdfBuildService(
                        externalReferenceResolver: wrongVersion,
                        externalReferenceContext:
                        DdfExternalReferenceContext.Embedded)
                    .Validate(root, project, package).Issues.Single(item =>
                        item.Code == "DDFR012");
                Equal(Path.GetFullPath(manifestPath),
                    Path.GetFullPath(versionIssue.DocumentPath));
                Equal("\"2.0.0\"", manifestSource.Substring(
                    versionIssue.Start, versionIssue.Length));
                Equal(true, versionIssue.Line > 1 &&
                    versionIssue.Column > 1);

                var wrongArchitecture = new
                    DdfExternalReferenceCatalogResolver(new[]
                    {
                        DdfExternalReferenceCatalogEntry.TypedAdapter(
                            "host.analytics", DdfExternalReferenceKinds
                                .HostService, "2.0.0",
                            DdfExternalReferenceArchitectures.X86,
                            DdfExternalReferenceOrigins.Host,
                            new[] { "analytics.invoke" },
                            "host.analytics.v2")
                    }, _ => true);
                DdfBuildIssue architectureIssue = new DdfBuildService(
                        externalReferenceResolver: wrongArchitecture,
                        externalReferenceContext:
                        DdfExternalReferenceContext.Embedded)
                    .Validate(root, project, package).Issues.Single(item =>
                        item.Code == "DDFR013");
                Equal("\"x64\"", manifestSource.Substring(
                    architectureIssue.Start, architectureIssue.Length));

                var wrongCapabilities = new
                    DdfExternalReferenceCatalogResolver(new[]
                    {
                        DdfExternalReferenceCatalogEntry.TypedAdapter(
                            "host.analytics", DdfExternalReferenceKinds
                                .HostService, "2.0.0",
                            DdfExternalReferenceArchitectures.X64,
                            DdfExternalReferenceOrigins.Host,
                            new[] { "analytics.read" },
                            "host.analytics.v2")
                    }, _ => true);
                DdfBuildIssue capabilityIssue = new DdfBuildService(
                        externalReferenceResolver: wrongCapabilities,
                        externalReferenceContext:
                        DdfExternalReferenceContext.Embedded)
                    .Validate(root, project, package).Issues.Single(item =>
                        item.Code == "DDFR014");
                Equal(true, manifestSource.Substring(capabilityIssue.Start,
                    capabilityIssue.Length).StartsWith("[",
                    StringComparison.Ordinal));

                var denied = new DdfExternalReferenceCatalogResolver(new[]
                {
                    DdfExternalReferenceCatalogEntry.TypedAdapter(
                        "host.analytics", DdfExternalReferenceKinds
                            .HostService, "2.0.0",
                        DdfExternalReferenceArchitectures.X64,
                        DdfExternalReferenceOrigins.Host,
                        new[] { "analytics.invoke" }, "host.analytics.v2")
                }, _ => false);
                DdfBuildIssue deniedIssue = new DdfBuildService(
                        externalReferenceResolver: denied,
                        externalReferenceContext:
                        DdfExternalReferenceContext.Embedded)
                    .Validate(root, project, package).Issues.Single(item =>
                        item.Code == "DDFR002");
                Equal("\"host.analytics\"", manifestSource.Substring(
                    deniedIssue.Start, deniedIssue.Length));

                DdfBuildIssue unavailableResolver = new DdfBuildService()
                    .Validate(root, project, package).Issues.Single(item =>
                        item.Code == "DDFB018");
                Equal(deniedIssue.Start, unavailableResolver.Start);
                Equal(deniedIssue.DocumentPath,
                    unavailableResolver.DocumentPath);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void GovernsAdapterFamiliesWithoutPromotion()
        {
            string path = Path.Combine(AppContext.BaseDirectory,
                "AdapterFamilies", "catalog.json");
            DdfAdapterFamilyCatalogLoadResult result =
                DdfAdapterFamilyCatalogLoader.Load(path);
            if (!result.Success) throw new InvalidOperationException(
                string.Join(" | ", result.Issues));

            DdfAdapterFamilyCatalog catalog = result.Catalog;
            Equal(DdfAdapterFamilyCatalog.FormatVersionValue,
                catalog.FormatVersion);
            Equal(19, catalog.Families.Count);
            Equal(6, catalog.Families.Count(item => item.IsAvailable));
            Equal(64, catalog.Fingerprint.Length);
            Equal(false, catalog.Families.Any(item => item.StandardLibrary));
            Equal(true, catalog.Families.Where(item => item.IsAvailable)
                .All(item => item.Maintainer.Length != 0 &&
                    item.UseCases.Count != 0 && item.Blockers.Count == 0 &&
                    item.Gates.All.All(gate => gate.IsSatisfied)));
            Equal(DdfAdapterFamilyStage.Deferred,
                catalog.Find("ddf.dotnet.ai.providers").Stage);
            Equal(DdfAdapterPortability.ExternalService,
                catalog.Find("ddf.dotnet.ai.providers").Portability);

            JsonObject document = JsonNode.Parse(File.ReadAllText(path,
                Encoding.UTF8)).AsObject();
            JsonArray sourceFamilies = document["families"].AsArray();
            var reversedFamilies = new JsonArray();
            foreach (JsonNode family in sourceFamilies.Reverse())
                reversedFamilies.Add(JsonNode.Parse(family.ToJsonString()));
            document["families"] = reversedFamilies;
            DdfAdapterFamilyCatalogLoadResult reordered =
                DdfAdapterFamilyCatalogLoader.Parse(document.ToJsonString());
            Equal(true, reordered.Success);
            Equal(catalog.Fingerprint, reordered.Catalog.Fingerprint);

            JsonObject invalidAvailable = JsonNode.Parse(File.ReadAllText(path,
                Encoding.UTF8)).AsObject();
            JsonObject first = invalidAvailable["families"].AsArray()[0]
                .AsObject();
            first["maintainer"] = null;
            first["gates"]["testing"]["status"] = "required";
            DdfAdapterFamilyCatalogLoadResult invalidResult =
                DdfAdapterFamilyCatalogLoader.Parse(
                    invalidAvailable.ToJsonString());
            Equal(false, invalidResult.Success);
            Equal(true, invalidResult.Issues.Any(item => item.Code ==
                "DDFAC010"));
            Equal(true, invalidResult.Issues.Any(item => item.Code ==
                "DDFAC011"));

            JsonObject promoted = JsonNode.Parse(File.ReadAllText(path,
                Encoding.UTF8)).AsObject();
            promoted["families"].AsArray()[0]["standardLibrary"] = true;
            DdfAdapterFamilyCatalogLoadResult promotedResult =
                DdfAdapterFamilyCatalogLoader.Parse(promoted.ToJsonString());
            Equal(true, promotedResult.Issues.Any(item => item.Code ==
                "DDFAC007"));

            JsonObject unknown = JsonNode.Parse(File.ReadAllText(path,
                Encoding.UTF8)).AsObject();
            unknown["implicitActivation"] = true;
            DdfAdapterFamilyCatalogLoadResult unknownResult =
                DdfAdapterFamilyCatalogLoader.Parse(unknown.ToJsonString());
            Equal(false, unknownResult.Success);
            Equal("DDFAC002", unknownResult.Issues.Single().Code);
        }

        private static void ProjectsExternalApiAcrossAnalysisAndRuntime()
        {
            var api = new DdfExternalApiDefinition("host.analytics", "1.0.0",
                new[]
                {
                    new DdfHostPermissionDefinition("analytics.use",
                        "Usa analytics", "Consente calcoli analytics esterni.")
                },
                new[]
                {
                    new DdfHostTypeDefinition("Metric", new[]
                    {
                        new DdfHostFieldDefinition("value", "int",
                            "Valore della metrica.")
                    }, "Metrica descritta dall'adapter.")
                },
                new[]
                {
                    new DdfExternalApiObjectDefinition("externalBase",
                        "analytics.base", "int", "Valore base esterno.",
                        requiredPermissions: new[] { "analytics.use" })
                },
                new[]
                {
                    new DdfExternalApiFunctionDefinition("externalAdd",
                        "analytics.add", "int", new[]
                        {
                            new DdfExternalApiParameterDefinition("left", "int"),
                            new DdfExternalApiParameterDefinition("right", "int")
                        }, "Somma due valori tramite l'adapter.",
                        new[] { "analytics.use" })
                });
            Equal(64, api.Fingerprint.Length);
            Equal(false, api.IsEmpty);

            var resolver = new DdfExternalReferenceCatalogResolver(new[]
            {
                DdfExternalReferenceCatalogEntry.TypedAdapter(
                    "host.analytics", DdfExternalReferenceKinds.HostService,
                    "1.0.0", DdfExternalReferenceArchitectures.Any,
                    DdfExternalReferenceOrigins.Host,
                    new[] { "analytics.invoke" }, "host.analytics.v1", api)
            }, _ => true);
            var declaration = new DdfProjectExternalReference
            {
                Id = "host.analytics",
                Kind = DdfExternalReferenceKinds.HostService,
                Version = "1.0.0",
                Architecture = DdfExternalReferenceArchitectures.Any,
                Origin = DdfExternalReferenceOrigins.Host,
                Capabilities = new List<string> { "analytics.invoke" }
            };
            DdfExternalReferenceResolutionResult resolved = resolver.Resolve(
                new DdfExternalReferenceResolutionRequest(
                    Path.GetTempPath(), declaration,
                    DdfExternalReferenceContext.Embedded));
            Equal(true, resolved.Success);
            Equal(api.Fingerprint,
                resolved.Reference.ApiDefinition.Fingerprint);

            string source = "main() out int { ret externalAdd(externalBase, 2); }";
            DdfLanguagePackage package = new DdfLanguagePackageSession()
                .ActivePackage;
            DdfHostCatalogSnapshot analysisCatalog =
                DdfExternalApiCatalogProjector.CreateAnalysisCatalog(api,
                    new[] { "analytics.use" });
            using (var analysis = new DdfEditorAnalysisService())
            {
                DdfEditorAnalysisSnapshot snapshot = analysis.Analyze(
                    new DdfEditorAnalysisRequest(source,
                        source.IndexOf("externalAdd", StringComparison.Ordinal),
                        1, package, 1, includeAllCompletions: true,
                        hostCatalog: analysisCatalog));
                Equal(false, snapshot.TypeCheckResult.Diagnostics.Any());
                DdfCompletionItem function = snapshot.Completions.Items.Single(
                    item => item.DisplayText == "externalAdd");
                Equal("int", function.TypeName);
                Equal(true, function.Detail.Contains(
                    "Somma due valori tramite l'adapter."));
                Equal(true, snapshot.Completions.Items.Any(item =>
                    item.DisplayText == "externalBase" &&
                    item.TypeName == "int"));
                Equal(true, snapshot.Completions.Items.Any(item =>
                    item.DisplayText == "Metric" &&
                    item.Kind == DdfCompletionKind.Type));

                const string missingSource =
                    "main() out int { ret externalMissing(); }";
                DdfEditorAnalysisSnapshot missing = analysis.Analyze(
                    new DdfEditorAnalysisRequest(missingSource,
                        missingSource.IndexOf("externalMissing",
                            StringComparison.Ordinal), 2, package, 2,
                        hostCatalog: analysisCatalog));
                DdfDiagnostic missingDiagnostic = missing.TypeCheckResult
                    .Diagnostics.Single(item => item.Code == "DDF312");
                Equal(missingSource.IndexOf("externalMissing",
                    StringComparison.Ordinal), missingDiagnostic.Start);
            }

            var adapter = new FakeExternalApiRuntimeAdapter();
            DdfHostCatalogSnapshot runtimeCatalog =
                DdfExternalApiCatalogProjector.CreateRuntimeCatalog(api,
                    "host.analytics.v1", adapter,
                    new[] { "analytics.use" });
            var documents = new OpenDocumentCollection();
            documents.CreateVirtual("external-api.ddf", source);
            var language = new DdfLanguagePackageSession();
            using (var execution = new DdfEditorExecutionService(
                () => documents, () => language, () => runtimeCatalog))
            {
                DdfEditorExecutionCompletion result = execution.RunAsync()
                    .GetAwaiter().GetResult();
                Equal(true, result.Succeeded);
                Equal(42L, Convert.ToInt64(
                    result.ExecutionResult.ReturnValue));
            }
            SequenceEqual(new[] { "analytics.base", "analytics.add" },
                adapter.Calls);

            adapter.Calls.Clear();
            var debugDocuments = new OpenDocumentCollection();
            IDdfEditorDocument debugDocument = debugDocuments.CreateVirtual(
                "external-api-debug.ddf",
                "main() out int\n{\n    int result << " +
                "externalAdd(externalBase, 2);\n    ret result;\n}");
            using (var debugger = new DdfEditorDebugService(
                       () => debugDocuments, () => language,
                       () => runtimeCatalog))
            using (var paused = new SemaphoreSlim(0))
            {
                debugger.SetBreakpoints(new[]
                {
                    new DdfEditorBreakpoint(debugDocument.Id, 3)
                });
                debugger.Paused += (sender, args) => paused.Release();
                Task<DdfEditorExecutionCompletion> debugRun = debugger.RunAsync(
                    new DdfEditorExecutionRequest(
                        entryDocumentId: debugDocument.Id));
                Equal(true, paused.Wait(TimeSpan.FromSeconds(5)));
                Equal(DdfEditorDebugState.Paused, debugger.State);
                Equal(3, debugger.CurrentLocation.Line);
                Equal(true, debugger.Continue());
                DdfEditorExecutionCompletion debugResult = debugRun
                    .GetAwaiter().GetResult();
                Equal(true, debugResult.Succeeded);
                Equal(42L, Convert.ToInt64(
                    debugResult.ExecutionResult.ReturnValue));
            }
            SequenceEqual(new[] { "analytics.base", "analytics.add" },
                adapter.Calls);
            Throws<ArgumentException>(() =>
                DdfExternalApiCatalogProjector.CreateRuntimeCatalog(api,
                    "different.adapter", adapter));

            var mismatched = new DdfExternalApiDefinition("other.api", "1.0.0");
            var mismatchedResolver = new DdfExternalReferenceCatalogResolver(
                new[]
                {
                    DdfExternalReferenceCatalogEntry.TypedAdapter(
                        "host.analytics", DdfExternalReferenceKinds.HostService,
                        "1.0.0", DdfExternalReferenceArchitectures.Any,
                        DdfExternalReferenceOrigins.Host,
                        new[] { "analytics.invoke" }, "host.analytics.v1",
                        mismatched)
                }, _ => true);
            DdfExternalReferenceResolutionResult rejected = mismatchedResolver
                .Resolve(new DdfExternalReferenceResolutionRequest(
                    Path.GetTempPath(), declaration,
                    DdfExternalReferenceContext.Embedded));
            Equal(false, rejected.Success);
            Equal("DDFR008", rejected.Code);
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

        private static void ValidatesDirectModuleVisibilityCollisionsAndDeterministicNavigation()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackage package = DdfLanguagePackageLoader
                    .LoadEmbeddedDefault().Package;

                DdfProjectManifest WriteModule(string id, string source,
                    IEnumerable<string> exports,
                    IEnumerable<DdfProjectDependency> dependencies = null)
                {
                    string moduleRoot = Path.Combine(root, ".ddf", "modules", id,
                        "1.0.0");
                    Directory.CreateDirectory(moduleRoot);
                    File.WriteAllText(Path.Combine(moduleRoot, "module.ddf"), source,
                        new UTF8Encoding(false));
                    DdfProjectManifest manifest = DdfProjectManifest.CreateImplicit(
                        id, package, null);
                    manifest.Module.Id = id;
                    manifest.Module.Version = "1.0.0";
                    manifest.EntryPoint = "unused";
                    manifest.Module.Exports.AddRange(exports);
                    if (dependencies != null)
                        manifest.Dependencies.AddRange(dependencies);
                    File.WriteAllText(Path.Combine(moduleRoot,
                            DdfProjectManifest.FileName),
                        JsonSerializer.Serialize(manifest,
                            new JsonSerializerOptions { WriteIndented = true }),
                        new UTF8Encoding(false));
                    return manifest;
                }

                DdfProjectDependency Dependency(string id) =>
                    new DdfProjectDependency { Id = id, Version = "1.0.0" };

                WriteModule("example.left",
                    "int privateValue << 20; hidden() out int { ret privateValue; } " +
                    "collide() out int { ret 1; } leftOnly() out int { ret 20; }",
                    new[] { "collide", "leftOnly" });
                DdfProjectManifest right = WriteModule("example.right",
                    "int privateValue << 22; hidden() out int { ret privateValue; } " +
                    "collide() out int { ret 2; } rightOnly() out int { ret 22; }",
                    new[] { "rightOnly" });
                WriteModule("example.leaf",
                    "transitiveOnly() out int { ret 40; }",
                    new[] { "transitiveOnly" });
                WriteModule("example.bridge",
                    "publicBridge() out int { ret transitiveOnly() + 2; }",
                    new[] { "publicBridge" },
                    new[] { Dependency("example.leaf") });

                string mainPath = Path.Combine(root, "main.ddf");
                var application = DdfProjectManifest.CreateImplicit(
                    "Module visibility matrix", package, "main.ddf");
                application.Module.Id = "example.application";
                application.Module.Version = "1.0.0";
                application.Dependencies.Add(Dependency("example.left"));
                application.Dependencies.Add(Dependency("example.right"));

                void SaveApplication(string source)
                {
                    File.WriteAllText(mainPath, source, new UTF8Encoding(false));
                    File.WriteAllText(Path.Combine(root,
                            DdfProjectManifest.FileName),
                        JsonSerializer.Serialize(application,
                            new JsonSerializerOptions { WriteIndented = true }),
                        new UTF8Encoding(false));
                }

                var service = new DdfBuildService();
                SaveApplication("main() out int { ret leftOnly() + rightOnly(); }");
                DdfBuildResult first = service.Validate(root, application, package);
                DdfBuildResult second = service.Validate(root, application, package);
                Equal(true, first.Success);
                Equal(true, second.Success);
                Equal(first.Artifact.Fingerprint, second.Artifact.Fingerprint);
                SequenceEqual(first.Artifact.ResolvedModules.Select(item => item.Id),
                    second.Artifact.ResolvedModules.Select(item => item.Id));
                Equal(42L, Convert.ToInt64(DdfInterpreter.Execute(
                    first.Artifact.RuntimeModules, package.Language,
                    new DdfExecutionOptions { EntryPoint = "main" }).ReturnValue));

                right.Module.Exports.Add("collide");
                File.WriteAllText(Path.Combine(root, ".ddf", "modules",
                        "example.right", "1.0.0", DdfProjectManifest.FileName),
                    JsonSerializer.Serialize(right,
                        new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
                SaveApplication("main() out int { ret collide(); }");
                DdfBuildResult collision = service.Validate(root, application,
                    package);
                Equal(false, collision.Success);
                Equal(true, collision.Issues.Any(item => item.Code == "DDF311"));

                SaveApplication(
                    "main() out int { ret hidden() + privateValue; }");
                DdfBuildResult privateSymbols = service.Validate(root, application,
                    package);
                Equal(false, privateSymbols.Success);
                Equal(true, privateSymbols.Issues.Any(item =>
                    item.Code == "DDF312"));
                Equal(true, privateSymbols.Issues.Any(item =>
                    item.Code == "DDF313"));

                application.Dependencies.Clear();
                application.Dependencies.Add(Dependency("example.bridge"));
                SaveApplication("main() out int { ret publicBridge(); }");
                DdfBuildResult direct = service.Validate(root, application, package);
                Equal(true, direct.Success);
                Equal(42L, Convert.ToInt64(DdfInterpreter.Execute(
                    direct.Artifact.RuntimeModules, package.Language,
                    new DdfExecutionOptions { EntryPoint = "main" }).ReturnValue));

                SaveApplication("main() out int { ret transitiveOnly(); }");
                DdfBuildResult transitive = service.Validate(root, application,
                    package);
                Equal(false, transitive.Success);
                Equal(true, transitive.Issues.Any(item =>
                    item.Code == "DDF312"));

                SaveApplication("main() out int { ret absent() + absentValue; }");
                DdfBuildResult absent = service.Validate(root, application, package);
                Equal(false, absent.Success);
                Equal(true, absent.Issues.Any(item => item.Code == "DDF312"));
                Equal(true, absent.Issues.Any(item => item.Code == "DDF313"));

                SaveApplication("main() out int { ret publicBridge(); }");
                DdfModuleEditorIndex editorIndex = DdfModuleEditorIndex.Load(root,
                    package);
                Equal(false, editorIndex.Issues.Any(item =>
                    item.Severity == DdfDiagnosticSeverity.Error));
                SequenceEqual(new[] { "publicBridge" }, editorIndex.Documents
                    .SelectMany(document => document.Symbols)
                    .Select(symbol => symbol.Name));
                Equal(1, editorIndex.FindDefinitions("publicBridge").Count);
                Equal(0, editorIndex.FindDefinitions("transitiveOnly").Count);
                Equal(0, editorIndex.FindDefinitions("privateValue").Count);
                Equal(0, editorIndex.FindDefinitions("hidden").Count);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void ValidatesShippedSampleProject()
        {
            string root = Path.Combine(Environment.CurrentDirectory, "samples", "project");
            string outputRoot = Path.Combine(root, ".ddf-build");
            string[] outputStateBefore = CaptureDirectoryState(outputRoot);
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            Equal(true, loaded.Success);
            DdfBuildResult result = new DdfBuildService().Validate(root, loaded.Manifest,
                DdfLanguagePackageLoader.LoadEmbeddedDefault().Package);
            Equal(true, result.Success);
            Equal("ddf.sample.project", result.Artifact.ModuleId);
            Equal("0.1.0", result.Artifact.ModuleVersion);
            Equal("main.ddf", result.Artifact.EntrySource);
            Equal(2, result.Artifact.Sources.Count);
            SequenceEqual(outputStateBefore, CaptureDirectoryState(outputRoot));
            DdfExecutionResult execution = DdfInterpreter.Execute(result.Artifact.RuntimeModules,
                DdfLanguagePackageLoader.LoadEmbeddedDefault().Package.Language,
                new DdfExecutionOptions { EntryPoint = result.Artifact.EntryPoint });
            Equal(true, execution.Succeeded);
            Equal(2, execution.ReturnValue);
        }

        private static string[] CaptureDirectoryState(string root)
        {
            if (!Directory.Exists(root)) return Array.Empty<string>();
            return Directory.EnumerateFileSystemEntries(root, "*",
                    SearchOption.AllDirectories)
                .Select(path =>
                {
                    string relative = Path.GetRelativePath(root, path);
                    if (Directory.Exists(path)) return "D|" + relative;
                    var info = new FileInfo(path);
                    return "F|" + relative + "|" + info.Length + "|" +
                        info.LastWriteTimeUtc.Ticks;
                })
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
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

        private static void ValidatesAndDebugsShippedLargeWorkspace()
        {
            string root = Path.Combine(Environment.CurrentDirectory, "samples", "debug-large-workspace");
            DdfProjectManifestLoadResult loaded = DdfProjectManifestLoader.Load(root);
            Equal(true, loaded.Success);
            DdfLanguagePackage package = DdfLanguagePackageLoader.LoadEmbeddedDefault().Package;
            DdfBuildResult result = new DdfBuildService().Validate(root, loaded.Manifest, package);
            if (!result.Success)
                throw new InvalidOperationException(string.Join(" | ", result.Issues.Select(
                    issue => issue.Code + ": " + issue.Message)));

            Equal("ddf.sample.debug.large", result.Artifact.ModuleId);
            Equal("main.ddf", result.Artifact.EntrySource);
            Equal(42, result.Artifact.Sources.Count);
            Equal(42, result.Artifact.RuntimeModules.Count);

            string stage10Path = Path.Combine(root, "stage-10.ddf");
            string stage20Path = Path.Combine(root, "stage-20.ddf");
            string stage30Path = Path.Combine(root, "stage-30.ddf");
            string stage40Path = Path.Combine(root, "stage-40.ddf");
            string activeFeaturesPath = Path.Combine(root, "active-features.ddf");
            int pauses = 0;
            int maximumStackDepth = 0;
            var pausesByDocument = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            DdfDebugPauseInfo stage40Pause = null;
            DdfDebugPauseInfo activeFeaturesPause = null;
            using var debugger = new DdfDebuggerSession();
            debugger.SetBreakpoints(new[]
            {
                new DdfSourceBreakpoint(stage10Path, 3),
                new DdfSourceBreakpoint(stage20Path, 3),
                new DdfSourceBreakpoint(stage30Path, 3),
                new DdfSourceBreakpoint(stage40Path, 14),
                new DdfSourceBreakpoint(activeFeaturesPath, 26)
            });
            debugger.Paused = pause =>
            {
                Interlocked.Increment(ref pauses);
                maximumStackDepth = Math.Max(maximumStackDepth,
                    pause.State.CallStack.Count);
                pausesByDocument[pause.DocumentPath] =
                    pausesByDocument.TryGetValue(pause.DocumentPath,
                        out int count) ? count + 1 : 1;
                if (string.Equals(pause.DocumentPath, stage40Path,
                        StringComparison.OrdinalIgnoreCase) && stage40Pause == null)
                    stage40Pause = pause;
                if (string.Equals(pause.DocumentPath, activeFeaturesPath,
                        StringComparison.OrdinalIgnoreCase) && activeFeaturesPause == null)
                    activeFeaturesPause = pause;
                debugger.Continue();
            };

            var output = new List<string>();
            var stateStore = new DdfStateStore();
            DdfExecutionResult execution = DdfInterpreter.Execute(result.Artifact.RuntimeModules,
                package.Language, new DdfExecutionOptions
                {
                    EntryPoint = result.Artifact.EntryPoint,
                    Output = output.Add,
                    DebuggerSession = debugger,
                    StateStore = stateStore
                });

            Equal(true, execution.Succeeded);
            Equal(3387, execution.ReturnValue);
            Equal(14, pauses);
            Equal(41, maximumStackDepth);
            Equal(1, pausesByDocument[stage10Path]);
            Equal(1, pausesByDocument[stage20Path]);
            Equal(1, pausesByDocument[stage30Path]);
            Equal(4, pausesByDocument[stage40Path]);
            Equal(7, pausesByDocument[activeFeaturesPath]);
            Equal(true, stage40Pause != null);
            Equal(14, stage40Pause.Line);
            Equal(41, stage40Pause.State.CallStack.Count);
            Equal("stage40", stage40Pause.State.CallStack[0].FunctionName);
            Equal("main", stage40Pause.State.CallStack[40].FunctionName);
            Equal("821", DdfWatch.Evaluate("baseValue", stage40Pause.State).Value.DisplayValue);
            Equal("821", DdfWatch.Evaluate("values[0]", stage40Pause.State).Value.DisplayValue);
            Equal("851", DdfWatch.Evaluate("values[3]", stage40Pause.State).Value.DisplayValue);
            Equal(true, activeFeaturesPause != null);
            Equal("1", DdfWatch.Evaluate("stressRuns", activeFeaturesPause.State).Value.DisplayValue);
            Equal("8", DdfWatch.Evaluate("StressArrayLength", activeFeaturesPause.State).Value.DisplayValue);
            Equal("StressMode.Active", DdfWatch.Evaluate("mode", activeFeaturesPause.State).Value.DisplayValue);
            Equal("1", DdfWatch.Evaluate("values[0]", activeFeaturesPause.State).Value.DisplayValue);
            SequenceEqual(new[] { "Large debugger workspace completed" }, output);

            DdfExecutionResult secondExecution = DdfInterpreter.Execute(
                result.Artifact.RuntimeModules, package.Language,
                new DdfExecutionOptions
                {
                    EntryPoint = result.Artifact.EntryPoint,
                    StateStore = stateStore
                });
            Equal(true, secondExecution.Succeeded);
            Equal(3388, secondExecution.ReturnValue);
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
                Equal(false, update.Success);
                Equal(true, update.Diagnostics.Any(item => item.Code == "DDFPA040"));
                Equal(1, store.List().Count);
                Equal(true, DdfLanguagePackageLoader.Load(first.Installed.Path).Success);

                string manifestPath = Path.Combine(source, "manifest.json");
                JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath,
                    Encoding.UTF8)).AsObject();
                manifest["packageVersion"] = "0.13.1";
                File.WriteAllText(manifestPath, manifest.ToJsonString(),
                    new UTF8Encoding(false));
                DdfLanguagePackageInstallResult next = store.Install(source);
                Equal(true, next.Success);
                Equal(false, next.Updated);
                Equal(2, store.List().Count);
                Equal(true, Directory.Exists(first.Installed.Path));
            }
            finally
            {
                if (Directory.Exists(source)) Directory.Delete(source, true);
                if (Directory.Exists(storeRoot)) Directory.Delete(storeRoot, true);
            }
        }

        private static void ModelsLanguagePackageAuthoringIndependently()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-authoring-test-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string generated = Path.Combine(root, "generated");
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(source);
                const string originalNotes =
                    "{\n  // commento dell'autore\n  \"customField\": 42\n}\n";
                File.WriteAllText(Path.Combine(source, "author-notes.json"),
                    originalNotes, new UTF8Encoding(false));

                DdfLanguageAuthoringResult loaded =
                    DdfLanguagePackageAuthoring.LoadDirectory(source);
                Equal(true, loaded.Success);
                Equal(0L, loaded.Project.Revision);
                Equal(true, loaded.Project.TryGetDocument("author-notes.json",
                    out DdfLanguageAuthoringDocument notes));
                Equal(originalNotes, notes.Content);

                const string editedNotes =
                    "{\n  // commento conservato\n  \"customField\": 43\n}\n";
                var edit = new DdfLanguageAuthoringChangeSet(0, new[]
                {
                    DdfLanguageAuthoringChange.Upsert("author-notes.json",
                        editedNotes)
                }, "Aggiorna note");
                DdfLanguageAuthoringResult edited =
                    DdfLanguagePackageAuthoring.Apply(loaded.Project, edit);
                Equal(true, edited.Success);
                Equal(1L, edited.Project.Revision);
                Equal(editedNotes, edited.Project.Documents["author-notes.json"].Content);
                Equal(true, edited.Inverse != null);

                DdfLanguageAuthoringResult undone =
                    DdfLanguagePackageAuthoring.Apply(edited.Project,
                        edited.Inverse);
                Equal(true, undone.Success);
                Equal(originalNotes,
                    undone.Project.Documents["author-notes.json"].Content);

                Equal(false, DdfLanguagePackageAuthoring.Apply(edited.Project,
                    edit).Success);
                Equal(false, DdfLanguagePackageAuthoring.Apply(edited.Project,
                    new DdfLanguageAuthoringChangeSet(edited.Project.Revision,
                        new[] { DdfLanguageAuthoringChange.Upsert(
                            "../outside.json", "{}") })).Success);

                DdfLanguagePackageLoadResult validation =
                    DdfLanguagePackageAuthoring.Validate(edited.Project);
                Equal(true, validation.Success);
                DdfLanguageAuthoringGenerationResult generation =
                    DdfLanguagePackageAuthoring.GenerateDirectory(
                        edited.Project, generated);
                Equal(true, generation.Success);
                Equal("ddf", generation.Package.Id);
                Equal(editedNotes, File.ReadAllText(Path.Combine(generated,
                    "author-notes.json"), Encoding.UTF8));
                Equal(false, DdfLanguagePackageAuthoring.GenerateDirectory(
                    edited.Project, generated).Success);

                string originalManifest = loaded.Project.Documents["manifest.json"]
                    .Content;
                string oldManifest = originalManifest.Replace(
                    "\"schemaVersion\": \"1.0\"",
                    "\"schemaVersion\": \"0.9\"", StringComparison.Ordinal);
                DdfLanguageAuthoringResult oldProject =
                    DdfLanguagePackageAuthoring.Apply(loaded.Project,
                        new DdfLanguageAuthoringChangeSet(0, new[]
                        {
                            DdfLanguageAuthoringChange.Upsert("manifest.json",
                                oldManifest)
                        }));
                Equal(true, oldProject.Success);
                var migration = new TestAuthoringMigration(originalManifest);
                DdfLanguageAuthoringMigrationResult migrated =
                    DdfLanguagePackageAuthoring.Migrate(oldProject.Project,
                        "1.0", new[] { migration });
                Equal(true, migrated.Success);
                SequenceEqual(new[] { "test-0.9-to-1.0" },
                    migrated.AppliedMigrationIds);
                Equal(originalManifest,
                    migrated.Project.Documents["manifest.json"].Content);
                Equal(true, DdfLanguagePackageAuthoring.Validate(
                    migrated.Project).Success);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void EvaluatesAuthoringSnapshotsWithFailingDeclaredTests()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-authoring-evaluate-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                DdfLanguageAuthoringProject project =
                    DdfLanguagePackageAuthoring.LoadDirectory(root).Project;
                string tests = project.Documents["tests.json"].Content;
                string failingTests = tests.Replace("\"expectedReturn\": \"2\"",
                    "\"expectedReturn\": \"999\"", StringComparison.Ordinal);
                Equal(false, string.Equals(tests, failingTests,
                    StringComparison.Ordinal));
                DdfLanguageAuthoringResult edited =
                    DdfLanguagePackageAuthoring.Apply(project,
                        new DdfLanguageAuthoringChangeSet(project.Revision,
                            new[]
                            {
                                DdfLanguageAuthoringChange.Upsert("tests.json",
                                    failingTests)
                            }));
                Equal(true, edited.Success);

                DdfLanguagePackageLoadResult evaluated =
                    DdfLanguagePackageAuthoring.Evaluate(edited.Project);
                Equal(true, evaluated.Package != null);
                Equal(true, evaluated.HasErrors);
                Equal(true, evaluated.Diagnostics.Any(item =>
                    item.Code == "DDFT001"));
                Equal(true, DdfLanguagePackageTestRunner.Run(evaluated.Package,
                    evaluated.Package.Tests).Any(item => !item.Passed));

                DdfLanguagePackageLoadResult strict =
                    DdfLanguagePackageAuthoring.Validate(edited.Project);
                Equal(false, strict.Success);
                Equal(true, strict.Package == null);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void PreviewsLanguagePackageTransformations()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-authoring-preview-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                const string oldNotes =
                    "{\n  // mantenere questo commento\n  \"value\": 42\n}\n";
                File.WriteAllText(Path.Combine(root, "author-notes.json"), oldNotes,
                    new UTF8Encoding(false));
                DdfLanguageAuthoringProject project =
                    DdfLanguagePackageAuthoring.LoadDirectory(root).Project;
                var transformation = new TestAuthoringTransformation();

                DdfLanguageAuthoringPreview preview =
                    DdfLanguagePackageAuthoring.PreviewTransformation(project,
                        transformation);
                Equal(true, preview.Success);
                Equal(true, preview.HasChanges);
                Equal(project.Revision, preview.BaseRevision);
                Equal(project.Fingerprint, preview.BaseFingerprint);
                Equal("test.update-notes", preview.TransformationId);
                Equal(3, preview.Documents.Count);
                Equal(oldNotes, project.Documents["author-notes.json"].Content);

                DdfLanguageAuthoringDocumentDiff modified = preview.Documents
                    .Single(item => item.RelativePath == "author-notes.json");
                Equal(DdfLanguageAuthoringDocumentChangeKind.Modified,
                    modified.Kind);
                Equal(true, modified.Lines.Any(item =>
                    item.Kind == DdfLanguageAuthoringDiffLineKind.Removed &&
                    item.Text.Contains("42", StringComparison.Ordinal)));
                Equal(true, modified.Lines.Any(item =>
                    item.Kind == DdfLanguageAuthoringDiffLineKind.Added &&
                    item.Text.Contains("43", StringComparison.Ordinal)));
                Equal(DdfLanguageAuthoringDocumentChangeKind.Added,
                    preview.Documents.Single(item => item.RelativePath ==
                        "preview-only.txt").Kind);
                Equal(DdfLanguageAuthoringDocumentChangeKind.Removed,
                    preview.Documents.Single(item => item.RelativePath ==
                        "grammar.ebnf").Kind);

                DdfLanguageAuthoringResult applied =
                    DdfLanguagePackageAuthoring.ApplyPreview(project, preview);
                Equal(true, applied.Success);
                Equal(true, applied.Project.Documents["author-notes.json"].Content
                    .Contains("43", StringComparison.Ordinal));
                Equal(false, applied.Project.Documents.ContainsKey("grammar.ebnf"));
                Equal(true, applied.Project.Documents.ContainsKey("preview-only.txt"));

                DdfLanguageAuthoringResult concurrent =
                    DdfLanguagePackageAuthoring.Apply(project,
                        new DdfLanguageAuthoringChangeSet(project.Revision, new[]
                        {
                            DdfLanguageAuthoringChange.Upsert("unrelated.txt", "x")
                        }));
                Equal(true, concurrent.Success);
                DdfLanguageAuthoringResult stale =
                    DdfLanguagePackageAuthoring.ApplyPreview(concurrent.Project,
                        preview);
                Equal(false, stale.Success);
                Equal(true, stale.Diagnostics.Any(item => item.Code == "DDFPA024"));

                DdfLanguageAuthoringPreview invalid =
                    DdfLanguagePackageAuthoring.PreviewTransformation(project,
                        new InvalidTestAuthoringTransformation());
                Equal(false, invalid.Success);
                Equal(true, invalid.Diagnostics.Any(item => item.Code ==
                    "DDFPA021"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void UndoesAndRedoesAuthoringOperations()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-authoring-history-test-" + Guid.NewGuid().ToString("N"));
            string generated = Path.Combine(Path.GetTempPath(),
                "ddf-authoring-history-output-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                DdfLanguageAuthoringProject initial =
                    DdfLanguagePackageAuthoring.LoadDirectory(root).Project;
                var session = new DdfLanguageAuthoringSession(initial,
                    historyLimit: 2);

                DdfLanguageAuthoringPreview first = session.Preview(
                    new UpsertAuthoringTransformation("test.first", "first.txt",
                        "prima\n"));
                Equal(true, session.Commit(first).Success);
                string firstFingerprint = session.Project.Fingerprint;
                Equal(true, session.CanUndo);
                Equal(false, session.CanRedo);

                DdfLanguageAuthoringPreview stale = session.Preview(
                    new UpsertAuthoringTransformation("test.stale", "stale.txt",
                        "obsoleto\n"));
                Equal(true, session.Commit(session.Preview(
                    new UpsertAuthoringTransformation("test.second", "second.txt",
                        "seconda\n"))).Success);
                string secondFingerprint = session.Project.Fingerprint;
                int historyBeforeFailure = session.UndoHistory.Count;
                Equal(false, session.Commit(stale).Success);
                Equal(secondFingerprint, session.Project.Fingerprint);
                Equal(historyBeforeFailure, session.UndoHistory.Count);

                Equal(true, session.Undo().Success);
                Equal(firstFingerprint, session.Project.Fingerprint);
                Equal(true, session.CanRedo);
                Equal(true, session.Redo().Success);
                Equal(secondFingerprint, session.Project.Fingerprint);

                Equal(true, session.Undo().Success);
                Equal(true, session.Commit(session.Preview(
                    new UpsertAuthoringTransformation("test.third", "third.txt",
                        "terza\n"))).Success);
                Equal(false, session.CanRedo);
                Equal(2, session.UndoHistory.Count);

                Equal(true, session.Commit(session.Preview(
                    new UpsertAuthoringTransformation("test.fourth", "fourth.txt",
                        "quarta\n"))).Success);
                Equal(2, session.UndoHistory.Count);
                Equal(true, DdfLanguagePackageAuthoring.Validate(
                    session.Project).Success);
                Equal(true, DdfLanguagePackageAuthoring.GenerateDirectory(
                    session.Project, generated).Success);
                Equal(true, File.Exists(Path.Combine(generated, "first.txt")));
                Equal(true, File.Exists(Path.Combine(generated, "third.txt")));
                Equal(true, File.Exists(Path.Combine(generated, "fourth.txt")));
                Equal(false, File.Exists(Path.Combine(generated, "second.txt")));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                if (Directory.Exists(generated)) Directory.Delete(generated, true);
            }
        }

        private static void CreatesAndClonesLanguageAuthoringProjects()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "ddf-authoring-template-test-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string archive = Path.Combine(root, "source.ddflang");
            string storeRoot = Path.Combine(root, "store");
            try
            {
                Directory.CreateDirectory(root);
                DdfLanguagePackageLoader.ExportEmbeddedDefault(source);
                const string notes = "# nota personalizzata\nnon riscrivere\n";
                File.WriteAllText(Path.Combine(source, "author-notes.txt"), notes,
                    new UTF8Encoding(false));
                var identity = new DdfLanguageAuthoringIdentity(
                    "acme.educational", "ACME Educational", "0.1.0", "0.1",
                    new[] { ".acme" });

                DdfLanguageAuthoringTemplateResult blank =
                    DdfLanguageAuthoringProjectFactory.CreateBlank(identity);
                EnsureAuthoringTemplateSuccess(blank, "blank");
                Equal(DdfLanguageAuthoringTemplateKind.Blank,
                    blank.TemplateKind);
                Equal("acme.educational", ReadAuthoringManifestString(
                    blank.Project, "id"));
                using (JsonDocument tests = JsonDocument.Parse(
                    blank.Project.Documents["tests.json"].Content))
                    Equal(1, tests.RootElement.GetProperty("cases")
                        .GetArrayLength());
                Equal(true, DdfLanguagePackageAuthoring.Validate(
                    blank.Project).Success);

                DdfLanguageAuthoringTemplateResult embedded =
                    DdfLanguageAuthoringProjectFactory.CloneEmbeddedDdf(identity);
                EnsureAuthoringTemplateSuccess(embedded, "embedded");
                Equal(DdfLanguageAuthoringTemplateKind.EmbeddedDdf,
                    embedded.TemplateKind);
                Equal("embedded://ddf", embedded.Source);
                Equal("acme.educational", ReadAuthoringManifestString(
                    embedded.Project, "id"));

                DdfLanguageAuthoringTemplateResult directory =
                    DdfLanguageAuthoringProjectFactory.ClonePackage(source,
                        identity);
                EnsureAuthoringTemplateSuccess(directory, "directory");
                Equal(DdfLanguageAuthoringTemplateKind.DirectoryPackage,
                    directory.TemplateKind);
                Equal(notes, directory.Project.Documents["author-notes.txt"].Content);
                Equal("ddf", DdfLanguagePackageLoader.Load(source).Package.Id);

                ZipFile.CreateFromDirectory(source, archive,
                    CompressionLevel.NoCompression, false);
                DdfLanguageAuthoringTemplateResult archived =
                    DdfLanguageAuthoringProjectFactory.ClonePackage(archive,
                        identity);
                EnsureAuthoringTemplateSuccess(archived, "archive");
                Equal(DdfLanguageAuthoringTemplateKind.ArchivePackage,
                    archived.TemplateKind);
                Equal(notes, archived.Project.Documents["author-notes.txt"].Content);

                var store = new DdfLanguagePackageStore(storeRoot);
                Equal(true, store.Install(source).Success);
                DdfInstalledLanguagePackage installed = store.List().Single();
                var installedIdentity = new DdfLanguageAuthoringIdentity(
                    "acme.installed", "ACME Installed", "1.0.0", "1.0",
                    new[] { ".aci" });
                DdfLanguageAuthoringTemplateResult installedClone =
                    DdfLanguageAuthoringProjectFactory.CloneInstalledPackage(
                        installed, installedIdentity);
                EnsureAuthoringTemplateSuccess(installedClone, "installed");
                Equal(DdfLanguageAuthoringTemplateKind.InstalledPackage,
                    installedClone.TemplateKind);
                Equal("acme.installed", ReadAuthoringManifestString(
                    installedClone.Project, "id"));

                Throws<ArgumentException>(() =>
                    new DdfLanguageAuthoringIdentity("Invalid Id", "Invalid",
                        "1.0.0", "1.0", new[] { ".bad" }));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static string ReadAuthoringManifestString(
            DdfLanguageAuthoringProject project, string property)
        {
            using JsonDocument manifest = JsonDocument.Parse(
                project.Documents["manifest.json"].Content);
            return manifest.RootElement.GetProperty(property).GetString();
        }

        private static void EnsureAuthoringTemplateSuccess(
            DdfLanguageAuthoringTemplateResult result, string label)
        {
            if (result.Success) return;
            throw new InvalidOperationException(label + ": " + string.Join(" | ",
                result.Diagnostics.Select(item => item.ToString())));
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

        private static void ValidatesPackageDependencyGraphAndCoverage()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                string runtimePath = Path.Combine(root, "runtime.json");
                string testsPath = Path.Combine(root, "tests.json");
                string runtimeSource = File.ReadAllText(runtimePath, Encoding.UTF8);
                string testsSource = File.ReadAllText(testsPath, Encoding.UTF8);

                JsonObject runtime = JsonNode.Parse(runtimeSource).AsObject();
                runtime["dependsOn"] = new JsonArray("grammar", "grammar", "types", "semantics");
                File.WriteAllText(runtimePath, runtime.ToJsonString(), new UTF8Encoding(false));
                Equal(true, DdfLanguagePackageLoader.Load(root).Diagnostics.Any(item =>
                    item.Code == "DDFL033"));

                runtime["dependsOn"] = new JsonArray("runtime", "grammar", "types", "semantics");
                File.WriteAllText(runtimePath, runtime.ToJsonString(), new UTF8Encoding(false));
                Equal(true, DdfLanguagePackageLoader.Load(root).Diagnostics.Any(item =>
                    item.Code == "DDFL034"));

                runtime["dependsOn"] = new JsonArray("build", "grammar", "types", "semantics");
                File.WriteAllText(runtimePath, runtime.ToJsonString(), new UTF8Encoding(false));
                Equal(true, DdfLanguagePackageLoader.Load(root).Diagnostics.Any(item =>
                    item.Code == "DDFL035"));

                File.WriteAllText(runtimePath, runtimeSource, new UTF8Encoding(false));
                JsonObject tests = JsonNode.Parse(testsSource).AsObject();
                foreach (JsonNode test in tests["cases"].AsArray())
                    test.AsObject().Remove("expectedReturn");
                File.WriteAllText(testsPath, tests.ToJsonString(), new UTF8Encoding(false));
                Equal(true, DdfLanguagePackageLoader.Load(root).Diagnostics.Any(item =>
                    item.Code == "DDFT001" && item.Message.Contains("Copertura minima")));

                File.WriteAllText(testsPath, testsSource, new UTF8Encoding(false));
                Equal(true, DdfLanguagePackageLoader.Load(root).Success);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void ReportsLanguagePackageCompatibility()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                string previousPath = Path.Combine(root, "previous");
                string currentPath = Path.Combine(root, "current");
                DdfLanguagePackageLoader.ExportEmbeddedDefault(previousPath);
                DdfLanguagePackageLoader.ExportEmbeddedDefault(currentPath);
                string manifestPath = Path.Combine(currentPath, "manifest.json");
                JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath,
                    Encoding.UTF8)).AsObject();
                manifest["packageVersion"] = "0.13.1";
                File.WriteAllText(manifestPath, manifest.ToJsonString(),
                    new UTF8Encoding(false));

                DdfLanguageAuthoringProject previous =
                    DdfLanguagePackageAuthoring.LoadDirectory(previousPath).Project;
                DdfLanguageAuthoringProject current =
                    DdfLanguagePackageAuthoring.LoadDirectory(currentPath).Project;
                DdfLanguagePackageCompatibilityReport unchanged =
                    DdfLanguagePackageCompatibility.Compare(previous, current);
                Equal(false, unchanged.HasBreakingChanges);
                Equal(false, unchanged.RequiresReview);

                string libraryPath = Path.Combine(currentPath, "standard-library.json");
                JsonObject library = JsonNode.Parse(File.ReadAllText(libraryPath,
                    Encoding.UTF8)).AsObject();
                JsonObject function = library["functions"].AsArray().Select(item =>
                    item.AsObject()).Single(item =>
                    item["name"].GetValue<string>() == "readLine");
                function["documentation"] = "Documentazione aggiornata.";
                File.WriteAllText(libraryPath, library.ToJsonString(),
                    new UTF8Encoding(false));
                current = DdfLanguagePackageAuthoring.LoadDirectory(currentPath).Project;
                Equal(false, DdfLanguagePackageCompatibility.Compare(previous,
                    current).HasBreakingChanges);

                function["name"] = "readInput";
                File.WriteAllText(libraryPath, library.ToJsonString(),
                    new UTF8Encoding(false));
                current = DdfLanguagePackageAuthoring.LoadDirectory(currentPath).Project;
                DdfLanguagePackageCompatibilityReport breaking =
                    DdfLanguagePackageCompatibility.Compare(previous, current);
                Equal(true, breaking.HasBreakingChanges);
                Equal(true, breaking.Findings.Any(item =>
                    item.Section == "standardLibrary" &&
                    item.Level == DdfLanguageCompatibilityLevel.Breaking));

                string ebnfPath = Path.Combine(currentPath, "grammar.ebnf");
                File.AppendAllText(ebnfPath, Environment.NewLine);
                current = DdfLanguagePackageAuthoring.LoadDirectory(currentPath).Project;
                Equal(true, DdfLanguagePackageCompatibility.Compare(previous,
                    current).RequiresReview);
                Equal(true, DdfLanguagePackageLoader.Load(previousPath).Success);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void GeneratesReproducibleLanguagePackageArchives()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                string source = Path.Combine(root, "source");
                DdfLanguagePackageLoader.ExportEmbeddedDefault(source);
                DdfLanguageAuthoringProject project =
                    DdfLanguagePackageAuthoring.LoadDirectory(source).Project;
                string firstPath = Path.Combine(root, "first.ddflang");
                string secondPath = Path.Combine(root, "second.ddflang");
                DdfLanguagePackageArchiveResult first =
                    DdfLanguagePackageArchiveBuilder.Generate(project, firstPath);
                DdfLanguagePackageArchiveResult second =
                    DdfLanguagePackageArchiveBuilder.Generate(project, secondPath);
                Equal(true, first.Success);
                Equal(true, second.Success);
                Equal(first.Sha256, second.Sha256);
                Equal(true, File.ReadAllBytes(firstPath).SequenceEqual(
                    File.ReadAllBytes(secondPath)));
                Equal(true, DdfLanguagePackageLoader.Load(firstPath).Success);

                using (ZipArchive archive = ZipFile.OpenRead(firstPath))
                {
                    Equal(true, archive.GetEntry("manifest.json") != null);
                    Equal(true, archive.GetEntry(
                        DdfLanguagePackageArchiveBuilder.ChecksumsFile) != null);
                    Equal(true, archive.GetEntry(
                        DdfLanguagePackageArchiveBuilder.ValidationFile) != null);
                    using StreamReader reader = new StreamReader(archive.GetEntry(
                        DdfLanguagePackageArchiveBuilder.ChecksumsFile).Open());
                    JsonObject checksums = JsonNode.Parse(reader.ReadToEnd()).AsObject();
                    Equal("SHA-256", checksums["algorithm"].GetValue<string>());
                    Equal(project.Documents.Count, checksums["files"].AsArray().Count);
                }
                DdfLanguagePackageArchiveResult refused =
                    DdfLanguagePackageArchiveBuilder.Generate(project, firstPath);
                Equal(false, refused.Success);
                Equal(true, refused.Diagnostics.Any(item => item.Code == "DDFPA031"));
                Equal(first.Sha256, Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(firstPath))).ToLowerInvariant());
                Equal(0, DdfLanguagePackageArchiveBuilder.VerifyArchive(firstPath).Count);
                var store = new DdfLanguagePackageStore(Path.Combine(root, "installed"));
                Equal(true, store.Install(firstPath).Success);
                Equal(false, store.Install(firstPath).Success);
                using (ZipArchive archive = ZipFile.Open(secondPath,
                    ZipArchiveMode.Update))
                {
                    ZipArchiveEntry entry = archive.GetEntry("formatting.json");
                    string content;
                    using (var reader = new StreamReader(entry.Open()))
                        content = reader.ReadToEnd();
                    entry.Delete();
                    using var writer = new StreamWriter(archive.CreateEntry(
                        "formatting.json").Open());
                    writer.Write(content + " ");
                }
                Equal(true, DdfLanguagePackageLoader.Load(secondPath).Success);
                DdfLanguagePackageInstallResult tampered = store.Install(secondPath);
                Equal(false, tampered.Success);
                Equal(true, tampered.Diagnostics.Any(item => item.Code == "DDFPA036"));
                Equal(1, store.List().Count);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void KeepsLanguagePublicationExampleExecutable()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            string sampleRoot = Path.Combine(projectRoot, "samples",
                "language-package-publication");
            string source = File.ReadAllText(Path.Combine(sampleRoot, "hello.ddf"),
                Encoding.UTF8);
            JsonObject example = JsonNode.Parse(File.ReadAllText(Path.Combine(
                sampleRoot, "example-test-case.json"), Encoding.UTF8)).AsObject();
            Equal(source.Trim(), example["source"].GetValue<string>().Trim());
            var suite = new DdfLanguagePackageTestSuite
            {
                Cases = new List<DdfLanguagePackageTestCase>
                {
                    new DdfLanguagePackageTestCase
                    {
                        Name = example["name"].GetValue<string>(),
                        Source = source,
                        ExpectedDiagnosticCodes = new List<string>(),
                        ExpectedReturn = example["expectedReturn"].GetValue<string>()
                    }
                }
            };
            Equal(true, DdfLanguagePackageTestRunner.Run(
                DdfLanguagePackageLoader.LoadEmbeddedDefault().Package,
                suite).Single().Passed);
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

        private static void AnalyzesEbnfAuthoringSourcesWithReferences()
        {
            const string source =
                "root = branch ;\n" +
                "branch = <Identifier> | \"x\" ;\n" +
                "unused = \"u\" ;\n";
            DdfEbnfAuthoringAnalysis analysis =
                DdfEbnfAuthoringAnalyzer.Analyze(source, "root");
            Equal(true, analysis.Success);
            Equal(3, analysis.Rules.Count);
            Equal("branch", analysis.Rules.Single(item => item.Name == "root")
                .References.Single().TargetRule);
            Equal("root", analysis.Rules.Single(item => item.Name == "branch")
                .ReferencedBy.Single());
            Equal(true, analysis.Diagnostics.Any(item =>
                item.Code == "DDFEBNF003" && item.Severity ==
                    DdfEbnfAuthoringDiagnosticSeverity.Warning &&
                item.Line == 3));

            DdfEbnfAuthoringAnalysis unknown =
                DdfEbnfAuthoringAnalyzer.Analyze("root = missing ;", "root");
            Equal(false, unknown.Success);
            Equal(true, unknown.Diagnostics.Any(item =>
                item.Code == "DDFEBNF002" && item.Start == 7));

            DdfEbnfAuthoringAnalysis recursive =
                DdfEbnfAuthoringAnalyzer.Analyze("root = root, \"x\" ;", "root");
            Equal(false, recursive.Success);
            Equal(true, recursive.Diagnostics.Any(item =>
                item.Message.Contains("left recursion",
                    StringComparison.OrdinalIgnoreCase)));
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

        private static void PlansKeywordMigrationsAndBlocksIdentifierCollisions()
        {
            string profiles = Path.Combine(AppContext.BaseDirectory,
                "LanguageProfiles");
            DdfLanguagePackage canonical = DdfLanguagePackageLoader.Load(
                Path.Combine(profiles, "ddf")).Package;
            DdfLanguagePackage italian = DdfLanguagePackageLoader.Load(
                Path.Combine(profiles, "ddf-it-didactic")).Package;
            const string safeSource =
                "main() out bool { if(true) { ret false; } ret true; }";
            DdfLanguageMigrationPlan safe = DdfLanguageMigrationAnalyzer.Analyze(
                new[] { new DdfLanguageMigrationDocument("main.ddf", safeSource) },
                canonical, italian);
            Equal(true, safe.CanActivate);
            Equal(false, safe.RequiresReview);
            Equal(true, safe.HasAutomaticConversions);
            Equal(true, safe.Previews.Single().ConvertedText.Contains(
                "se(vero)", StringComparison.Ordinal));

            string root = Path.Combine(Path.GetTempPath(),
                "ddf-keyword-migration-" + Guid.NewGuid().ToString("N"));
            try
            {
                DdfLanguagePackageLoader.ExportEmbeddedDefault(root);
                KeepOnlyMinimalDeclarativePackageTest(root);
                string lexiconPath = Path.Combine(root, "lexicon.json");
                JsonObject lexicon = JsonNode.Parse(File.ReadAllText(
                    lexiconPath, Encoding.UTF8)).AsObject();
                string[] futureKeywords =
                {
                    "legacyName", "raises", "attempt", "recover", "always",
                    "raise", "maybe", "some", "none", "each", "in", "use"
                };
                for (int index = 0; index < futureKeywords.Length; index++)
                    lexicon["keywords"].AsArray().Add(new JsonObject
                    {
                        ["text"] = futureKeywords[index],
                        ["tokenKind"] = "ControlFlowKeyword",
                        ["role"] = "MigrationReserved" + index
                    });
                File.WriteAllText(lexiconPath, lexicon.ToJsonString(
                    new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(false));
                DdfLanguagePackageLoadResult loaded =
                    DdfLanguagePackageLoader.Load(root);
                if (!loaded.Success)
                    throw new InvalidOperationException(string.Join(" | ",
                        loaded.Diagnostics));
                string collisionSource =
                    "main() out int\n{\n" +
                    string.Join("\n", futureKeywords.Select(name =>
                        "    int " + name + " << 1;")) + "\n" +
                    "    string protected << \"" +
                    string.Join(" ", futureKeywords) + "\";\n" +
                    "    // " + string.Join(" ", futureKeywords) + "\n" +
                    "    ret 0;\n}\n";
                DdfLanguageMigrationPlan blocked =
                    DdfLanguageMigrationAnalyzer.Analyze(new[]
                    {
                        new DdfLanguageMigrationDocument("collision.ddf",
                            collisionSource)
                    }, canonical, loaded.Package);
                Equal(false, blocked.CanActivate);
                Equal(true, blocked.RequiresReview);
                Equal(futureKeywords.Length, blocked.Diagnostics.Count(item =>
                    item.Code == "DDFM001"));
                DdfLanguageMigrationDiagnostic first = blocked.Diagnostics
                    .First(item => item.Code == "DDFM001");
                Equal(DdfLanguageMigrationIssueKind.IdentifierCollision,
                    first.Kind);
                Equal("collision.ddf", first.DocumentPath);
                Equal(3, first.Line);
                Equal("legacyName", first.OriginalText);
                Equal("legacyName_value", first.SuggestedText);
                Equal(false, blocked.Diagnostics.Any(item => item.Line ==
                    futureKeywords.Length + 3 || item.Line ==
                    futureKeywords.Length + 4));
                Equal(collisionSource, blocked.Previews.Single().ConvertedText);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
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

        private static void ValidatesLanguageServiceInteractionMatrix()
        {
            string profiles = Path.Combine(AppContext.BaseDirectory,
                "LanguageProfiles");
            DdfLanguagePackage canonical = DdfLanguagePackageLoader.Load(
                Path.Combine(profiles, "ddf")).Package;
            DdfLanguagePackage italian = DdfLanguagePackageLoader.Load(
                Path.Combine(profiles, "ddf-it-didactic")).Package;
            DdfLanguagePackage familiar = DdfLanguagePackageLoader.Load(
                Path.Combine(profiles, "ddf-c-familiar")).Package;
            DdfLanguagePackage[] packages = { canonical, italian, familiar };

            var precedenceCases = new[]
            {
                new
                {
                    Name = "multiplication-before-addition",
                    Source = "main() out int { ret 1 + 2 * 3; }",
                    Expected = 7
                },
                new
                {
                    Name = "explicit-grouping",
                    Source = "main() out int { ret (1 + 2) * 3; }",
                    Expected = 9
                },
                new
                {
                    Name = "right-associative-power",
                    Source = "main() out int { ret 2 ^ 3 ^ 2; }",
                    Expected = 512
                },
                new
                {
                    Name = "logical-precedence",
                    Source = "main() out int { if(false || true ^^ false && false) " +
                        "{ ret 42; } ret 0; }",
                    Expected = 42
                },
                new
                {
                    Name = "relational-and-arithmetic",
                    Source = "main() out int { if(1 + 2 * 3 == 7 && 4 > 3) " +
                        "{ ret 42; } ret 0; }",
                    Expected = 42
                },
                new
                {
                    Name = "right-associative-assignment",
                    Source = "main() out int { int first << 0; int second << 0; " +
                        "first << second << 21; ret first + second; }",
                    Expected = 42
                }
            };

            foreach (var test in precedenceCases)
            {
                DdfParseResult canonicalParse = DdfParser.Parse(test.Source,
                    canonical.Language);
                Equal(0, canonicalParse.ValidationDiagnostics.Count);
                DdfIrBuildResult canonicalIr = DdfIrLowerer.Lower(test.Source,
                    canonicalParse, canonical.Language);
                if (!canonicalIr.Success)
                    throw new InvalidOperationException(test.Name + ": " +
                        string.Join(" | ", canonicalIr.Diagnostics));
                string expectedShape = SemanticIrShape(canonicalIr.Program);

                foreach (DdfLanguagePackage package in packages)
                {
                    DdfSyntaxProfileConversionResult conversion =
                        DdfSyntaxProfileConverter.Convert(test.Source,
                            canonical.Language, package.Language);
                    string converted = conversion.Text;
                    Equal(test.Source, DdfSyntaxProfileConverter.Convert(
                        converted, package.Language, canonical.Language).Text);

                    DdfParseResult parse = DdfParser.Parse(converted,
                        package.Language);
                    Equal(0, parse.ValidationDiagnostics.Count);
                    Equal(0, DdfTypeChecker.Check(converted, parse.Root, null,
                        package.Language.Types,
                        package.Language.StandardLibrary, null,
                        package.Language.Runtime).Diagnostics.Count);
                    DdfIrBuildResult ir = DdfIrLowerer.Lower(converted, parse,
                        package.Language);
                    if (!ir.Success)
                        throw new InvalidOperationException(test.Name + "/" +
                            package.Id + ": " + string.Join(" | ",
                                ir.Diagnostics));
                    Equal(expectedShape, SemanticIrShape(ir.Program));
                    DdfExecutionResult execution = DdfInterpreter.Execute(
                        converted, ir.Program, package.Language);
                    if (!execution.Succeeded)
                        throw new InvalidOperationException(test.Name + "/" +
                            package.Id + " execution: " + string.Join(" | ",
                                execution.Diagnostics.Select(item =>
                                    item.Code + " " + item.Message)));
                    Equal(test.Expected, Convert.ToInt32(execution.ReturnValue,
                        CultureInfo.InvariantCulture));

                    string formatted = DdfFormatter.Format(converted,
                        package.Language).Text;
                    Equal(formatted, DdfFormatter.Format(formatted,
                        package.Language).Text);
                    DdfParseResult formattedParse = DdfParser.Parse(formatted,
                        package.Language);
                    Equal(0, formattedParse.ValidationDiagnostics.Count);
                    DdfIrBuildResult formattedIr = DdfIrLowerer.Lower(formatted,
                        formattedParse, package.Language);
                    if (!formattedIr.Success)
                        throw new InvalidOperationException(test.Name + "/" +
                            package.Id + " formatted IR: " + string.Join(" | ",
                                formattedIr.Diagnostics));
                    Equal(expectedShape, SemanticIrShape(formattedIr.Program));
                }
            }

            var recoveryCases = new[]
            {
                new
                {
                    Source = "main( out int{int value<<\"unfinished",
                    Marker = "\"unfinished"
                },
                new
                {
                    Source = "main() out int { if(true) { ret 1;",
                    Marker = "1;"
                },
                new
                {
                    Source = "main() out int { int value << 1 + ; ret value }",
                    Marker = "value"
                }
            };
            foreach (var test in recoveryCases)
            {
                foreach (DdfLanguagePackage package in packages)
                {
                    string converted = DdfSyntaxProfileConverter.Convert(
                        test.Source, canonical.Language, package.Language).Text;
                    DdfParseResult recovered = DdfParser.Parse(converted,
                        package.Language);
                    if (recovered.Diagnostics.Count == 0 ||
                        recovered.Root.Members.Count == 0 ||
                        recovered.Root.Start < 0 ||
                        recovered.Root.End > converted.Length)
                        throw new InvalidOperationException(package.Id +
                            " recovery before format: " + converted);

                    string formatted = DdfFormatter.Format(converted,
                        package.Language).Text;
                    if (!formatted.Contains(test.Marker,
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(package.Id +
                            " formatter lost marker " + test.Marker + ": " +
                            formatted);
                    Equal(formatted, DdfFormatter.Format(formatted,
                        package.Language).Text);
                    DdfParseResult formattedRecovery = DdfParser.Parse(
                        formatted, package.Language);
                    if (formattedRecovery.Diagnostics.Count == 0 ||
                        formattedRecovery.Root.End > formatted.Length)
                        throw new InvalidOperationException(package.Id +
                            " recovery after format: " + formatted);
                }
            }

            var collisionCases = new[]
            {
                new
                {
                    Target = italian,
                    Names = new[]
                    {
                        "struttura", "enumerazione", "fisso", "stato",
                        "restituisci", "interrompi", "continua", "termina",
                        "restituisce", "se", "altrimenti", "scegli", "caso",
                        "predefinito", "mentre", "fai", "per", "vero", "falso"
                    }
                },
                new
                {
                    Target = familiar,
                    Names = new[]
                    {
                        "const", "return", "break", "exit", "returns",
                        "switch", "case", "default"
                    }
                }
            };
            foreach (var test in collisionCases)
            {
                string source = "main() out int\n{\n" +
                    string.Join("\n", test.Names.Select(name =>
                        "    int " + name + " << 1;")) + "\n" +
                    "    string protected << \"" +
                    string.Join(" ", test.Names) + "\";\n" +
                    "    // " + string.Join(" ", test.Names) + "\n" +
                    "    ret 0;\n}\n";
                DdfParseResult sourceParse = DdfParser.Parse(source,
                    canonical.Language);
                Equal(0, sourceParse.ValidationDiagnostics.Count);
                Equal(0, DdfTypeChecker.Check(source, sourceParse.Root, null,
                    canonical.Language.Types,
                    canonical.Language.StandardLibrary, null,
                    canonical.Language.Runtime).Diagnostics.Count);

                DdfLanguageMigrationPlan migration =
                    DdfLanguageMigrationAnalyzer.Analyze(new[]
                    {
                        new DdfLanguageMigrationDocument("collision.ddf",
                            source)
                    }, canonical, test.Target);
                Equal(false, migration.CanActivate);
                if (!migration.RequiresReview)
                    throw new InvalidOperationException(test.Target.Id +
                        " did not require collision review.");
                DdfLanguageMigrationDiagnostic[] collisions =
                    migration.Diagnostics.Where(item =>
                        item.Code == "DDFM001").ToArray();
                Equal(test.Names.Length, collisions.Length);
                SequenceEqual(test.Names, collisions.Select(item =>
                    item.OriginalText));
                if (!collisions.All(item =>
                        !string.IsNullOrWhiteSpace(item.SuggestedText)))
                    throw new InvalidOperationException(test.Target.Id +
                        " produced an empty collision suggestion.");
                string preview = migration.Previews.Single().ConvertedText;
                Equal(DdfSyntaxProfileConverter.Convert(source,
                    canonical.Language, test.Target.Language).Text, preview);
                Equal(true, preview.Contains("\"" +
                    string.Join(" ", test.Names) + "\"",
                    StringComparison.Ordinal));
                Equal(true, preview.Contains("// " +
                    string.Join(" ", test.Names), StringComparison.Ordinal));
                foreach (string name in test.Names)
                    Equal(true, preview.Contains("int " + name + " ",
                        StringComparison.Ordinal));
            }
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

        private static void IndexesUserDefinedTypeGlobals()
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "ddf-workspace-user-type-global-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                DdfDocumentFile.Save(Path.Combine(directory, "types.ddf"),
                    "enum Priority { Low, High }\n" +
                    "fixed Priority DefaultPriority << Priority.High;\n");
                DdfDocumentFile.Save(Path.Combine(directory, "main.ddf"),
                    "main() out int { int value << weight(DefaultPriority); ret value; }\n" +
                    "weight(Priority priority) out int { ret 1; }\n");

                DdfWorkspaceIndex workspace = DdfWorkspaceIndex.Load(directory);
                IReadOnlyList<DdfWorkspaceSymbol> definitions =
                    workspace.FindDefinitions("DefaultPriority");

                Equal(1, definitions.Count);
                Equal("DefaultPriority", definitions[0].Symbol.Name);
                Equal("Priority", definitions[0].Symbol.Detail);
                Equal(true, definitions[0].Symbol.IsFixed);
                Equal(1, workspace.FindDefinitions("Priority").Count);
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
                string analyzedSource = sourceOnly.Documents.Single().Source;
                DdfParseResult parse = DdfParser.Parse(analyzedSource);
                IReadOnlyList<DdfDocumentSymbol> symbols = DdfSymbolIndex.Create(
                    parse, analyzedSource).Symbols;
                DdfWorkspaceIndex analyzed = sourceOnly.WithDocument(path,
                    analyzedSource, parse.Root, symbols);
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

        private static void RenamesSemanticWorkspaceReferences()
        {
            const string main = "main() out int { completedRuns++; int copy << completedRuns; ret copy; }";
            const string state = "state int completedRuns << 0; // completedRuns ignored";
            var documents = new[]
            {
                new DdfWorkspaceSearchDocument("main", "main.ddf", "main.ddf", main),
                new DdfWorkspaceSearchDocument("state", "state.ddf", "state.ddf", state)
            };

            IReadOnlyList<DdfWorkspaceReplacementChange> changes =
                DdfWorkspaceNavigationService.CreateRenameChanges(documents, "state",
                    state.IndexOf("completedRuns", StringComparison.Ordinal), "runCount");
            Equal(2, changes.Count);
            Equal("main() out int { runCount++; int copy << runCount; ret copy; }",
                changes.Single(change => change.Document.Id == "main").UpdatedSource);
            Equal("state int runCount << 0; // completedRuns ignored",
                changes.Single(change => change.Document.Id == "state").UpdatedSource);
            Equal(3, changes.Sum(change => change.ReplacementCount));
            Throws<ArgumentException>(() => DdfWorkspaceNavigationService.CreateRenameChanges(
                documents, "state", state.IndexOf("completedRuns", StringComparison.Ordinal), "if"));
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

        private static void TreatsTopLevelFunctionsAsModuleFunctions()
        {
            const string source =
                "static() out int { ret 40; } main() out int { ret static() + 2; }";
            DdfLexResult lex = DdfLexer.Lex(source);
            Equal(DdfTokenKind.Identifier, lex.Tokens[0].Kind);

            DdfParseResult parse = DdfParser.Parse(source);
            Equal(0, parse.Diagnostics.Count);
            Equal(2, parse.Root.Members.OfType<FunctionDeclarationSyntax>().Count());
            Equal(0, DdfTypeChecker.Check(source, parse.Root).Diagnostics.Count);
            Equal(42, DdfInterpreter.Execute(source).ReturnValue);

            const string nested =
                "main() out int { helper() out int { ret 1; } ret 0; }";
            Equal(true, DdfParser.Parse(nested).Diagnostics.Count > 0);

            DdfLanguagePackage package = DdfLanguagePackageLoader
                .LoadEmbeddedDefault().Package;
            Equal("funzione di modulo",
                package.Language.Intellisense.GetSymbolLabel(
                    DdfSymbolKind.Function));
            Equal(true, package.Language.Intellisense
                .GetSymbolDocumentation(DdfSymbolKind.Function)
                .Contains("non richiede né ammette il modificatore static",
                    StringComparison.Ordinal));
        }

        private static void EnforcesTypedImmutableFixedValues()
        {
            const string valid =
                "fixed int answer << base + 2; fixed int base << 40; main() out int { fixed int offset << 1 + 1; ret answer + offset - 2; }";
            DdfParseResult parse = DdfParser.Parse(valid);
            Equal(0, parse.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(valid, parse.Root).Diagnostics.Count);

            DdfIrBuildResult lowered = DdfIrLowerer.Lower(valid, parse);
            Equal(true, lowered.Success);
            DdfIrVariableDeclaration[] globals = lowered.Program.Members
                .OfType<DdfIrGlobal>()
                .Select(item => item.Statement)
                .OfType<DdfIrVariableDeclaration>()
                .ToArray();
            Equal(2, globals.Length);
            Equal(true, globals.All(item => item.IsFixed));
            Equal(42, globals.Single(item => item.Name == "answer").ConstantValue.Value);
            DdfIrVariableDeclaration local = lowered.Program.Members
                .OfType<DdfIrFunction>()
                .Single()
                .Body.Statements
                .OfType<DdfIrVariableDeclaration>()
                .First();
            Equal(true, local.IsFixed);
            Equal(2, local.ConstantValue.Value);

            DdfExecutionResult execution = DdfInterpreter.Execute(valid);
            Equal(true, execution.Succeeded);
            Equal(42, execution.ReturnValue);

            const string enumFixed =
                "enum Color { Red, Green, Blue } " +
                "fixed Color preferred << Color.Green; " +
                "main() out int { fixed Color selected << preferred; " +
                "if(selected == Color.Green) { ret 42; } ret 0; }";
            DdfParseResult enumParse = DdfParser.Parse(enumFixed);
            Equal(0, enumParse.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(enumFixed,
                enumParse.Root).Diagnostics.Count);
            DdfIrBuildResult enumIr = DdfIrLowerer.Lower(enumFixed, enumParse);
            Equal(true, enumIr.Success);
            DdfConstantValue enumValue = enumIr.Program.Members
                .OfType<DdfIrGlobal>()
                .Select(item => item.Statement)
                .OfType<DdfIrVariableDeclaration>()
                .Single().ConstantValue;
            Equal(DdfConstantKind.Enumeration, enumValue.Kind);
            Equal(new DdfEnumValue("Color", "Green"), enumValue.Value);
            DdfExecutionResult enumExecution = DdfInterpreter.Execute(enumFixed);
            Equal(true, enumExecution.Succeeded);
            Equal(42, enumExecution.ReturnValue);

            const string invalid =
                "fixed int value; main() out int { fixed int parsed << toInt(\"2\"); value << 1; value++; ret value; }";
            DdfParseResult invalidParse = DdfParser.Parse(invalid);
            Equal(true, invalidParse.Diagnostics.Any(item => item.Code == "DDF106"));
            DdfTypeCheckResult invalidTypes = DdfTypeChecker.Check(invalid, invalidParse.Root);
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF314"));
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF315"));
            Equal(2, invalidTypes.Diagnostics.Count(item => item.Code == "DDF316"));

            const string cyclic =
                "fixed int left << right + 1; fixed int right << left + 1; main() out int { ret 0; }";
            DdfIrBuildResult cyclicIr = DdfIrLowerer.Lower(cyclic, DdfParser.Parse(cyclic));
            Equal(true, cyclicIr.Diagnostics.Any(item => item.Code == "DDF318"));

            const string overflowing =
                "fixed int tooLarge << 2147483647 + 1; main() out int { ret 0; }";
            DdfIrBuildResult overflowIr = DdfIrLowerer.Lower(overflowing,
                DdfParser.Parse(overflowing));
            Equal(true, overflowIr.Diagnostics.Any(item => item.Code == "DDF319"));

            const string firstDocument = "fixed int combined << baseValue + 2;";
            const string secondDocument = "fixed int baseValue << 40;";
            DdfIrBuildResult firstIr = DdfIrLowerer.Lower(firstDocument,
                DdfParser.Parse(firstDocument));
            DdfIrBuildResult secondIr = DdfIrLowerer.Lower(secondDocument,
                DdfParser.Parse(secondDocument));
            DdfIrVariableDeclaration combined = firstIr.Program.Members
                .OfType<DdfIrGlobal>().Select(item => item.Statement)
                .OfType<DdfIrVariableDeclaration>().Single();
            Equal(null, combined.ConstantValue);
            IReadOnlyList<DdfConstantEvaluationIssue> multiFileIssues =
                DdfConstantEvaluator.Evaluate(new[]
                {
                    new DdfIrModule("first.ddf", firstDocument, firstIr.Program,
                        "ddf.application", "1.0.0", Array.Empty<string>(),
                        new[] { new DdfProjectDependency { Id = "ddf.constants", Version = "1.0.0" } }),
                    new DdfIrModule("second.ddf", secondDocument, secondIr.Program,
                        "ddf.constants", "1.0.0", new[] { "baseValue" },
                        Array.Empty<DdfProjectDependency>())
                });
            Equal(0, multiFileIssues.Count);
            Equal(42, combined.ConstantValue.Value);
        }

        private static void ExposesFixedAsReadOnlyMetadata()
        {
            const string source =
                "fixed int globalFixed << 40;\n" +
                "main() out int\n" +
                "{\n" +
                "    fixed int localFixed << 2;\n" +
                "    int mutableValue << 1;\n" +
                "    ret globalFixed + localFixed + mutableValue - 1;\n" +
                "}";
            DdfParseResult parse = DdfParser.Parse(source);
            DdfSymbolIndex index = DdfSymbolIndex.Create(parse, source);
            DdfDocumentSymbol global = index.Symbols.Single(item =>
                item.Name == "globalFixed");
            DdfDocumentSymbol function = index.Symbols.Single(item =>
                item.Name == "main");
            DdfDocumentSymbol local = function.Children.Single(item =>
                item.Name == "localFixed");
            DdfDocumentSymbol mutable = function.Children.Single(item =>
                item.Name == "mutableValue");
            Equal(true, global.IsReadOnly);
            Equal(true, global.IsFixed);
            Equal(true, local.IsReadOnly);
            Equal(true, local.IsFixed);
            Equal(false, mutable.IsReadOnly);
            Equal(false, mutable.IsFixed);

            int completionPosition = source.IndexOf("localFixed + mutableValue",
                StringComparison.Ordinal) + "localF".Length;
            DdfCompletionItem completion = DdfCompletionService.GetCompletions(
                    source, completionPosition)
                .Items.Single(item => item.DisplayText == "localFixed");
            Equal(true, completion.IsReadOnly);
            Equal(true, completion.IsFixed);
            Equal("fixed · sola lettura", completion.CategoryLabel);

            DdfRuntimeStateSnapshot snapshot = null;
            using var debugger = new DdfDebuggerSession();
            debugger.SetBreakpoints(new[] { 6 });
            debugger.Paused = pause =>
            {
                snapshot = pause.State;
                debugger.Continue();
            };
            DdfExecutionResult execution = DdfInterpreter.Execute(source,
                new DdfExecutionOptions { DebuggerSession = debugger });
            Equal(true, execution.Succeeded);
            DdfRuntimeVariableSnapshot runtimeGlobal = snapshot.Variables
                .Single(item => item.Name == "globalFixed");
            DdfRuntimeVariableSnapshot runtimeLocal = snapshot.Variables
                .Single(item => item.Name == "localFixed");
            DdfRuntimeVariableSnapshot runtimeMutable = snapshot.Variables
                .Single(item => item.Name == "mutableValue");
            Equal(true, runtimeGlobal.IsReadOnly);
            Equal(true, runtimeGlobal.IsFixed);
            Equal(true, runtimeLocal.IsReadOnly);
            Equal(true, runtimeLocal.IsFixed);
            Equal(false, runtimeMutable.IsReadOnly);
            Equal(false, runtimeMutable.IsFixed);
        }

        private static void PersistsExplicitIsolatedStateValues()
        {
            const string source =
                "state int counter << 0; main() out int { counter++; ret counter; }";
            DdfParseResult parse = DdfParser.Parse(source);
            Equal(0, parse.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(source, parse.Root).Diagnostics.Count);

            DdfIrBuildResult lowered = DdfIrLowerer.Lower(source, parse);
            Equal(true, lowered.Success);
            DdfIrVariableDeclaration state = lowered.Program.Members
                .OfType<DdfIrGlobal>().Select(item => item.Statement)
                .OfType<DdfIrVariableDeclaration>().Single();
            Equal(true, state.IsState);
            Equal(false, state.IsFixed);

            var store = new DdfStateStore();
            var options = new DdfExecutionOptions { StateStore = store };
            Equal(1, DdfInterpreter.Execute(source, options).ReturnValue);
            Equal(2, DdfInterpreter.Execute(source, options).ReturnValue);
            Equal(1, store.Count);
            Equal(2, store.Snapshot().Single().Value);

            Equal(1, DdfInterpreter.Execute(source).ReturnValue);
            Equal(1, DdfInterpreter.Execute(source).ReturnValue);
            store.Reset();
            Equal(1, DdfInterpreter.Execute(source, options).ReturnValue);

            const string busySource =
                "state int counter << 0; main() out int { while(true) { counter++; } ret counter; }";
            var busyStore = new DdfStateStore();
            var cancellation = new CancellationTokenSource();
            Task<DdfExecutionResult> active = Task.Run(() => DdfInterpreter.Execute(busySource,
                new DdfExecutionOptions
                {
                    StateStore = busyStore,
                    MaxInstructions = int.MaxValue,
                    CancellationToken = cancellation.Token
                }));
            Equal(true, SpinWait.SpinUntil(() => busyStore.Count == 1 && !active.IsCompleted, 5000));
            DdfExecutionResult overlapping = DdfInterpreter.Execute(source,
                new DdfExecutionOptions { StateStore = busyStore });
            Equal(true, overlapping.Diagnostics.Any(item => item.Code == "DDF423"));
            cancellation.Cancel();
            Equal(true, active.Wait(5000));
            Equal(true, active.Result.WasCancelled);

            const string invalid =
                "state int missing; struct Box { state int field << 0; } main() out int { state int local << 0; ret local; }";
            DdfParseResult invalidParse = DdfParser.Parse(invalid);
            Equal(true, invalidParse.Diagnostics.Any(item => item.Code == "DDF107"));
            DdfTypeCheckResult invalidTypes = DdfTypeChecker.Check(invalid, invalidParse.Root);
            Equal(true, invalidTypes.Diagnostics.Any(item => item.Code == "DDF321"));
            Equal(2, invalidTypes.Diagnostics.Count(item => item.Code == "DDF320"));

            const string compound =
                "state int[2] values << 0; main() out int { ret 0; }";
            DdfParseResult compoundParse = DdfParser.Parse(compound);
            Equal(true, DdfTypeChecker.Check(compound, compoundParse.Root).Diagnostics
                .Any(item => item.Code == "DDF322"));
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
            Equal("0.13", manifest["languageVersion"].GetValue<string>());
            Equal("0.13.0", manifest["languagePackageVersion"].GetValue<string>());
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

        private static void ValidatesActiveScalarAndConversionCultureMatrix()
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                foreach (string cultureName in new[] { "it-IT", "en-US", "tr-TR" })
                {
                    CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = culture;

                    DdfExecutionResult integerLimits = DdfInterpreter.Execute(
                        "main() out int { int minimum << toInt(\"-2147483648\"); " +
                        "int maximum << toInt(\"  +2147483647  \"); " +
                        "if(minimum < 0 && maximum > 0) { ret 42; } ret 0; }");
                    if (!integerLimits.Succeeded)
                        throw new InvalidOperationException("integer limits: " +
                            string.Join("; ", integerLimits.Diagnostics.Select(
                                item => item.Code + " " + item.Message)));
                    Equal(42, integerLimits.ReturnValue);

                    var output = new List<string>();
                    DdfExecutionResult floating = DdfInterpreter.Execute(
                        "main() out float { float value << toFloat(\"1.5e1\") + 0.1 + 0.2; " +
                        "value >> Console; ret value; }", options: new DdfExecutionOptions
                        {
                            Output = output.Add
                        });
                    if (!floating.Succeeded)
                        throw new InvalidOperationException("floating conversions: " +
                            string.Join("; ", floating.Diagnostics.Select(
                                item => item.Code + " " + item.Message)));
                    Equal(15d + 0.1d + 0.2d, floating.ReturnValue);
                    Equal("15.299999999999999", output.Single());

                    string unicodeSource = "main() out int { ret length(\"A" +
                        char.ConvertFromUtf32(0x1F600) + "\"); }";
                    DdfExecutionResult utf16 = DdfInterpreter.Execute(unicodeSource);
                    if (!utf16.Succeeded)
                        throw new InvalidOperationException("UTF-16 length: " +
                            string.Join("; ", utf16.Diagnostics.Select(
                                item => item.Code + " " + item.Message)));
                    Equal(3, utf16.ReturnValue);

                    DdfExecutionResult characterAndBoolean = DdfInterpreter.Execute(
                        "main() out int { char letter << \"A\"; string text << \"AZ\"; " +
                        "bool matches << letter == text[0]; " +
                        "if(matches) { ret 42; } ret 0; }");
                    if (!characterAndBoolean.Succeeded)
                        throw new InvalidOperationException("char/bool: " +
                            string.Join("; ", characterAndBoolean.Diagnostics.Select(
                                item => item.Code + " " + item.Message)));
                    Equal(42, characterAndBoolean.ReturnValue);

                    foreach (string source in new[]
                    {
                        "main() out char { string text << \"A\"; ret text[-1]; }",
                        "main() out char { string text << \"A\"; ret text[1]; }"
                    })
                    {
                        DdfExecutionResult invalidIndex =
                            DdfInterpreter.Execute(source);
                        Equal(false, invalidIndex.Succeeded);
                        Equal("DDF405", invalidIndex.Diagnostics.Single().Code);
                    }

                    var invalidConversions = new[]
                    {
                        "main() out int { ret toInt(\"2147483648\"); }",
                        "main() out int { ret toInt(\"-2147483649\"); }",
                        "main() out float { ret toFloat(\"1,5\"); }",
                        "main() out float { ret toFloat(\"1e309\"); }",
                        "main() out float { ret toFloat(\"NaN\"); }"
                    };
                    foreach (string source in invalidConversions)
                    {
                        DdfExecutionResult invalid = DdfInterpreter.Execute(source);
                        Equal(false, invalid.Succeeded);
                        Equal("DDF408", invalid.Diagnostics.Single().Code);
                    }
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
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
            SequenceEqual(new[] { "when", "while" }, result.Items
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

            const string conversion =
                "main() out int { ret toInt(\"abc\"); }";
            DdfSignatureHelpResult conversionResult =
                DdfSignatureHelpService.GetSignatureHelp(conversion,
                    conversion.IndexOf("abc", StringComparison.Ordinal) + 1);
            Equal("Errori recuperabili: operation.invalidInput (DDF408)",
                conversionResult.Signature.ErrorSummary);
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
            DdfHoverInfo conversion = DdfHoverService.CreateForStandardFunction(
                DdfRuntimeCatalog.StandardFunctions.Single(item =>
                    item.Name == "toInt"));
            Equal(true, conversion.Documentation.Contains(
                "operation.invalidInput (DDF408)"));
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

        private static void ContinuesLoopsAcrossLanguageServices()
        {
            const string source = @"main() out int
{
    int total << 0;
    for(int index << 0; index < 6; index++)
    {
        if(index == 2)
        {
            continue;
        }
        total << total + index;
    }
    int probe << 0;
    while(probe < 3)
    {
        probe++;
        if(probe < 3) { continue; }
        total << total + 10;
    }
    do
    {
        probe--;
        if(probe == 1) { continue; }
        total << total + 100;
    } while(probe > 0);
    ret total;
}";
            DdfParseResult parse = DdfParser.Parse(source);
            if (parse.Diagnostics.Count != 0)
                throw new InvalidOperationException(string.Join(" | ", parse.Diagnostics));
            Equal(3, parse.Root.Members.OfType<FunctionDeclarationSyntax>()
                .Sum(function => CountContinueStatements(function.Body)));
            DdfTypeCheckResult checkedSource = DdfTypeChecker.Check(source, parse.Root);
            if (checkedSource.Diagnostics.Count != 0)
                throw new InvalidOperationException(string.Join(" | ", checkedSource.Diagnostics));
            Equal(223, DdfInterpreter.Execute(source).ReturnValue);

            string formatted = DdfFormatter.Format(source).Text;
            Equal(true, formatted.Contains("continue;"));
            Equal(0, DdfParser.Parse(formatted).Diagnostics.Count);

            int continuePauses = 0;
            using (var debugger = new DdfDebuggerSession())
            {
                debugger.SetBreakpoints(new[] { 8 });
                debugger.Paused = pause =>
                {
                    Equal("continue;", source.Substring(pause.Start, pause.Length));
                    Interlocked.Increment(ref continuePauses);
                    debugger.Continue();
                };
                DdfExecutionResult debugResult = DdfInterpreter.Execute(source,
                    new DdfExecutionOptions { DebuggerSession = debugger });
                Equal(true, debugResult.Succeeded);
                Equal(223, debugResult.ReturnValue);
                Equal(1, continuePauses);
            }

            DdfFlowchart flowchart = DdfFlowchartBuilder.Build(source, "main").Flowchart;
            Equal(3, flowchart.Nodes.Count(node => node.Kind == DdfFlowchartNodeKind.Continue));
            Equal(3, flowchart.Edges.Count(edge => edge.Kind == DdfFlowchartEdgeKind.Continue));
            DdfFlowchartNode increment = flowchart.Nodes.Single(node =>
                node.Kind == DdfFlowchartNodeKind.Process && node.Label.Contains("index++"));
            Equal(true, flowchart.Edges.Any(edge => edge.Kind ==
                DdfFlowchartEdgeKind.Continue && edge.To == increment.Id));

            const string invalid = "main() out int { continue; ret 0; }";
            DdfParseResult invalidParse = DdfParser.Parse(invalid);
            Equal("DDF323", DdfTypeChecker.Check(invalid, invalidParse.Root)
                .Diagnostics.Single().Code);

            string profiles = Path.Combine(AppContext.BaseDirectory, "LanguageProfiles");
            var profileCases = new[]
            {
                new { Id = "ddf", Keyword = "continue", If = "if", For = "for", Out = "out", Return = "ret", Initializer = "<<" },
                new { Id = "ddf-c-familiar", Keyword = "continue", If = "if", For = "for", Out = "returns", Return = "return", Initializer = "=" },
                new { Id = "ddf-it-didactic", Keyword = "continua", If = "se", For = "per", Out = "restituisce", Return = "restituisci", Initializer = "<<" }
            };
            foreach (var profile in profileCases)
            {
                DdfLanguagePackageLoadResult loaded = DdfLanguagePackageLoader.Load(
                    Path.Combine(profiles, profile.Id));
                Equal(true, loaded.Success);
                string profileSource = "main() " + profile.Out + " int { int total " +
                    profile.Initializer + " 0; " + profile.For + "(int index " + profile.Initializer +
                    " 0; index < 4; index++) { " + profile.If + "(index == 1) { " + profile.Keyword +
                    "; } total " + profile.Initializer + " total + index; } " +
                    profile.Return + " total; }";
                DdfParseResult profileParse = DdfParser.Parse(profileSource,
                    loaded.Package.Language);
                Equal(0, profileParse.Diagnostics.Count);
                DdfIrBuildResult profileIr = DdfIrLowerer.Lower(profileSource,
                    profileParse, loaded.Package.Language);
                Equal(5L, Convert.ToInt64(DdfInterpreter.Execute(profileSource,
                    profileIr.Program, loaded.Package.Language).ReturnValue));
            }
        }

        private static int CountContinueStatements(StatementSyntax statement)
        {
            if (statement == null) return 0;
            if (statement is ContinueStatementSyntax) return 1;
            if (statement is BlockStatementSyntax block)
                return block.Statements.Sum(CountContinueStatements);
            if (statement is IfStatementSyntax conditional)
                return CountContinueStatements(conditional.Body) +
                    CountContinueStatements(conditional.ElseBody);
            if (statement is WhileStatementSyntax loop)
                return CountContinueStatements(loop.Body);
            if (statement is DoWhileStatementSyntax postLoop)
                return CountContinueStatements(postLoop.Body);
            if (statement is ForStatementSyntax counted)
                return CountContinueStatements(counted.Body);
            return 0;
        }

        private static void ResolvesNestedControlTransfers()
        {
            const string source = @"main() out int
{
    int total << 0;
    for(int outer << 0; outer < 3; outer++)
    {
        int inner << 0;
        while(inner < 4)
        {
            inner++;
            if(inner == 1) { continue; }
            if(outer == 1 && inner == 2) { brk; }
            total++;
        }

        int probe << 0;
        do
        {
            probe++;
            if(probe == 1) { continue; }
            if(probe == 2) { brk; }
        } while(probe < 3);

        if(outer == 0) { continue; }
        select(outer)
        {
            when 2
            {
                if(total > 0) { ret total + 35; }
            }
            otherwise { total++; }
        }
    }
    ret 4000;
}";
            DdfParseResult parse = DdfParser.Parse(source);
            Equal(0, parse.ValidationDiagnostics.Count);
            Equal(0, DdfTypeChecker.Check(source, parse.Root).Diagnostics.Count);
            Equal(42, DdfInterpreter.Execute(source).ReturnValue);

            DdfIrBuildResult lowered = DdfIrLowerer.Lower(source, parse);
            Equal(0, lowered.Diagnostics.Count);
            DdfFlowchart flow = DdfFlowchartBuilder.Build(source, "main").Flowchart;
            var transfers = flow.Nodes.Where(node =>
                node.Kind == DdfFlowchartNodeKind.Continue ||
                node.Kind == DdfFlowchartNodeKind.Break ||
                node.Kind == DdfFlowchartNodeKind.Return).ToArray();
            Equal(7, transfers.Length);
            Equal(transfers.Length, transfers.Select(node => node.Start).Distinct().Count());
            Equal(true, transfers.All(node => node.Start >= 0 && node.Length > 0 &&
                node.Start + node.Length <= source.Length));

            foreach (DdfFlowchartNode transfer in transfers)
            {
                DdfFlowchartEdgeKind expected = transfer.Kind == DdfFlowchartNodeKind.Continue
                    ? DdfFlowchartEdgeKind.Continue
                    : transfer.Kind == DdfFlowchartNodeKind.Break
                        ? DdfFlowchartEdgeKind.Break
                        : DdfFlowchartEdgeKind.Return;
                DdfFlowchartEdge edge = flow.Edges.Single(item => item.From == transfer.Id);
                Equal(expected, edge.Kind);
                Equal(true, flow.FindNode(edge.To) != null);
            }

            string[] continueTargets = transfers
                .Where(node => node.Kind == DdfFlowchartNodeKind.Continue)
                .Select(node => flow.Edges.Single(edge => edge.From == node.Id).To)
                .Distinct(StringComparer.Ordinal).ToArray();
            string[] breakTargets = transfers
                .Where(node => node.Kind == DdfFlowchartNodeKind.Break)
                .Select(node => flow.Edges.Single(edge => edge.From == node.Id).To)
                .Distinct(StringComparer.Ordinal).ToArray();
            Equal(3, continueTargets.Length);
            Equal(2, breakTargets.Length);
            Equal(true, transfers.Where(node => node.Kind == DdfFlowchartNodeKind.Return)
                .All(node => flow.Edges.Single(edge => edge.From == node.Id).To ==
                    flow.ExitNodeId));

            const string nestedEnd = @"main() out int
{
    for(int outer << 0; outer < 2; outer++)
    {
        int inner << 0;
        while(inner < 2)
        {
            inner++;
            select(inner)
            {
                when 1 { if(outer == 1) { end; } }
                otherwise { }
            }
        }
    }
    ret 0;
}";
            DdfExecutionResult ended = DdfInterpreter.Execute(nestedEnd);
            Equal(true, ended.Succeeded);
            Equal(true, ended.WasTerminated);
            DdfFlowchart endFlow = DdfFlowchartBuilder.Build(nestedEnd, "main").Flowchart;
            DdfFlowchartNode terminate = endFlow.Nodes.Single(node =>
                node.Kind == DdfFlowchartNodeKind.Terminate);
            DdfFlowchartEdge terminateEdge = endFlow.Edges.Single(edge =>
                edge.From == terminate.Id);
            Equal(DdfFlowchartEdgeKind.Terminate, terminateEdge.Kind);
            Equal(endFlow.ExitNodeId, terminateEdge.To);

            const string invalid = "main() out int { brk; continue; ret 0; }";
            DdfParseResult invalidParse = DdfParser.Parse(invalid);
            Equal(2, DdfTypeChecker.Check(invalid, invalidParse.Root).Diagnostics
                .Count(diagnostic => diagnostic.Code == "DDF323"));
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

        private static void ValidatesActiveFixedArrayAcceptanceMatrix()
        {
            const string identityAndSingleEvaluation = @"int lengthCalls << 0;
size() out int
{
    lengthCalls++;
    ret 3;
}
mutate(int[3] values) out void
{
    values[1] << 20;
}
replaceLocally(int[3] values) out void
{
    int[3] replacement;
    values << replacement;
    values[0] << 99;
}
main() out int
{
    int[size()] values;
    int[3] alias << values;
    values[0] << 20;
    mutate(alias);
    values[2] << 1;
    replaceLocally(alias);
    ret lengthCalls + values[0] + alias[1] + values[2];
}";
            DdfParseResult identityParse = DdfParser.Parse(
                identityAndSingleEvaluation);
            Equal(0, identityParse.Diagnostics.Count);
            Equal(0, DdfTypeChecker.Check(identityAndSingleEvaluation,
                identityParse.Root).Diagnostics.Count);
            DdfExecutionResult identity = DdfInterpreter.Execute(
                identityAndSingleEvaluation);
            Equal(true, identity.Succeeded);
            Equal(42L, Convert.ToInt64(identity.ReturnValue));

            const string empty =
                "main() out int { int[0] values; ret 42; }";
            DdfExecutionResult emptyResult = DdfInterpreter.Execute(empty);
            Equal(true, emptyResult.Succeeded);
            Equal(42L, Convert.ToInt64(emptyResult.ReturnValue));

            const string minimum =
                "main() out int { int[1] values; values[0] << 42; ret values[0]; }";
            Equal(42L, Convert.ToInt64(
                DdfInterpreter.Execute(minimum).ReturnValue));

            const string large = @"main() out int
{
    int[4096] values;
    for(int index << 0; index < 4096; index++)
    {
        values[index] << index;
    }
    ret values[4095] - 4053;
}";
            DdfExecutionResult largeResult = DdfInterpreter.Execute(large,
                new DdfExecutionOptions { MaxInstructions = 200000 });
            Equal(true, largeResult.Succeeded);
            Equal(42L, Convert.ToInt64(largeResult.ReturnValue));

            const string invalidTypes =
                "main() out int { int[1] ints; float[1] floats << ints; " +
                "ints[\"0\"] << 1; ret 0; }";
            DdfTypeCheckResult invalidTypeResult = DdfTypeChecker.Check(
                invalidTypes, DdfParser.Parse(invalidTypes).Root);
            Equal(true, invalidTypeResult.Diagnostics.Any(item =>
                item.Code == "DDF301"));
            Equal(true, invalidTypeResult.Diagnostics.Any(item =>
                item.Code == "DDF305"));

            foreach (string invalidRuntime in new[]
            {
                "main() out int { int[0] values; ret values[0]; }",
                "main() out int { int[1] values; ret values[-1]; }",
                "main() out int { int[-1] values; ret 0; }"
            })
            {
                DdfExecutionResult failed = DdfInterpreter.Execute(invalidRuntime);
                Equal(false, failed.Succeeded);
                Equal("DDF405", failed.Diagnostics.Single().Code);
            }
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
            Equal("DDF424", result.Error.Code);
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
                Equal("arithmetic.divisionZero", failure.Error.TypeId);
                Equal(Path.GetFullPath(helperPath),
                    Path.GetFullPath(failure.Error.DocumentPath));
                Equal(true, failure.Error.Stack.Any(frame =>
                    string.Equals(Path.GetFullPath(frame.DocumentPath),
                        Path.GetFullPath(mainPath),
                        StringComparison.OrdinalIgnoreCase)));
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
            Equal(4, result.StackTrace[0].Line);
            Equal("10 / zero", source.Substring(result.StackTrace[0].Start,
                result.StackTrace[0].Length));
            Equal("fail", source.Substring(result.StackTrace[1].Start,
                result.StackTrace[1].Length));

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

        private sealed class TestScriptStatePersistenceProvider :
            IDdfEditorScriptStatePersistenceProvider,
            IDdfEditorScriptStateMigrationProvider
        {
            public DdfEditorScriptStateSnapshot State { get; set; }
            public DdfEditorScriptStateSnapshot LastSaved { get; private set; }
            public int SaveCount { get; private set; }
            public int LoadCount { get; private set; }
            public int ClearCount { get; private set; }
            public int MigrationCount { get; private set; }

            public DdfEditorScriptStateSnapshot Load()
            {
                LoadCount++;
                return State;
            }

            public void Save(DdfEditorScriptStateSnapshot snapshot)
            {
                State = snapshot;
                LastSaved = snapshot;
                SaveCount++;
            }

            public void Clear()
            {
                State = null;
                ClearCount++;
            }

            public DdfEditorScriptStateSnapshot Migrate(
                DdfEditorScriptStateSnapshot snapshot, string targetSchemaVersion,
                string targetLanguageId, string targetLanguagePackageVersion)
            {
                MigrationCount++;
                return new DdfEditorScriptStateSnapshot(targetSchemaVersion,
                    targetLanguageId, targetLanguagePackageVersion, snapshot.Entries);
            }
        }

        private sealed class NonMigratingScriptStatePersistenceProvider :
            IDdfEditorScriptStatePersistenceProvider
        {
            public NonMigratingScriptStatePersistenceProvider(
                DdfEditorScriptStateSnapshot state) { State = state; }
            public DdfEditorScriptStateSnapshot State { get; private set; }
            public DdfEditorScriptStateSnapshot Load() => State;
            public void Save(DdfEditorScriptStateSnapshot snapshot) => State = snapshot;
            public void Clear() => State = null;
        }

        private sealed class TestPersistenceMigrationProvider :
            IDdfPersistenceMigrationProvider
        {
            private readonly Func<DdfPersistenceArtifact,
                DdfPersistenceSchemaContract, DdfPersistenceArtifact> migrate;

            public TestPersistenceMigrationProvider(Func<DdfPersistenceArtifact,
                DdfPersistenceSchemaContract, DdfPersistenceArtifact> migrate)
            {
                this.migrate = migrate;
            }

            public int CallCount { get; private set; }

            public DdfPersistenceArtifact Migrate(DdfPersistenceArtifact source,
                DdfPersistenceSchemaContract targetContract)
            {
                CallCount++;
                return migrate(source, targetContract);
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

        private sealed class TestResourceLeaseAuthority :
            IDdfHostResourceLeaseAuthority
        {
            private readonly bool grant;

            public TestResourceLeaseAuthority(bool grant)
            {
                this.grant = grant;
            }

            public List<DdfHostResourceLeaseRequest> Requests { get; } = new();
            public List<DdfHostResourceLeaseSnapshot> Snapshots { get; } = new();

            public ValueTask<DdfHostResourceLeaseDecision> AuthorizeAsync(
                DdfHostResourceLeaseRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                return ValueTask.FromResult(grant
                    ? DdfHostResourceLeaseDecision.Grant()
                    : DdfHostResourceLeaseDecision.Deny("policy"));
            }

            public void Observe(DdfHostResourceLeaseSnapshot snapshot) =>
                Snapshots.Add(snapshot);
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

        private sealed class ControllableAsyncHostResource : IAsyncDisposable
        {
            private readonly ManualResetEventSlim started;
            private readonly Task completion;

            public ControllableAsyncHostResource(ManualResetEventSlim started,
                Task completion)
            {
                this.started = started;
                this.completion = completion;
            }

            public ValueTask DisposeAsync()
            {
                started.Set();
                return new ValueTask(completion);
            }
        }

        private sealed class FakeExternalApiRuntimeAdapter :
            IDdfExternalApiRuntimeAdapter
        {
            public string AdapterId => "host.analytics.v1";
            public List<string> Calls { get; } = new List<string>();

            public object ReadObject(string bindingId)
            {
                Calls.Add(bindingId);
                if (bindingId == "analytics.base") return 40;
                throw new InvalidOperationException(
                    "Oggetto esterno non riconosciuto: " + bindingId + ".");
            }

            public void WriteObject(string bindingId, object value) => throw new
                InvalidOperationException("Oggetto esterno in sola lettura: " +
                    bindingId + ".");

            public ValueTask<object> InvokeAsync(
                DdfHostInvocationContext context, string bindingId,
                IReadOnlyList<object> arguments)
            {
                Calls.Add(bindingId);
                if (bindingId != "analytics.add") throw new
                    InvalidOperationException(
                        "Funzione esterna non riconosciuta: " + bindingId + ".");
                return ValueTask.FromResult<object>(
                    Convert.ToInt32(arguments[0]) +
                    Convert.ToInt32(arguments[1]));
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

        private sealed class RecordingExecutionScheduler :
            IDdfEditorExecutionScheduler
        {
            public int ScheduleCount { get; private set; }
            public int ExecutionThreadId { get; private set; }

            public Task<TResult> ScheduleAsync<TResult>(
                Func<Task<TResult>> operation)
            {
                ScheduleCount++;
                return Task.Run(async () =>
                {
                    ExecutionThreadId = Environment.CurrentManagedThreadId;
                    return await operation().ConfigureAwait(false);
                });
            }
        }

        private sealed class InlineExecutionScheduler :
            IDdfEditorExecutionScheduler
        {
            public Task<TResult> ScheduleAsync<TResult>(
                Func<Task<TResult>> operation) => operation();
        }

        private sealed class ManualEventDispatcher : IDdfEditorEventDispatcher
        {
            private readonly Queue<Action> callbacks = new();
            public bool HasAccess => false;
            public void Post(Action callback) => callbacks.Enqueue(callback);
            public void Drain()
            {
                while (callbacks.Count > 0) callbacks.Dequeue()();
            }
        }

        private sealed class DedicatedEventDispatcher :
            IDdfEditorEventDispatcher, IDisposable
        {
            private readonly BlockingCollection<Action> callbacks = new();
            private readonly Thread thread;

            public DedicatedEventDispatcher()
            {
                using var started = new ManualResetEventSlim();
                thread = new Thread(() =>
                {
                    ThreadId = Environment.CurrentManagedThreadId;
                    started.Set();
                    foreach (Action callback in callbacks.GetConsumingEnumerable())
                        callback();
                }) { IsBackground = true, Name = "DDF test UI dispatcher" };
                thread.Start();
                started.Wait();
            }

            public int ThreadId { get; private set; }
            public bool HasAccess =>
                Environment.CurrentManagedThreadId == ThreadId;
            public void Post(Action callback) => callbacks.Add(callback);
            public void Dispose()
            {
                callbacks.CompleteAdding();
                thread.Join(TimeSpan.FromSeconds(5));
                callbacks.Dispose();
            }
        }

        private sealed class RecordingReleaseEvaluator :
            IDdfProductReleaseEvaluator
        {
            public int CallCount { get; private set; }
            public IReadOnlyList<DdfRemoteRelease> LastReleases { get; private set; } =
                Array.Empty<DdfRemoteRelease>();

            public DdfProductUpdateResult Evaluate(
                DdfProductUpdateRequest request,
                IReadOnlyList<DdfRemoteRelease> releases,
                DateTimeOffset checkedAtUtc)
            {
                CallCount++;
                LastReleases = releases;
                return DdfProductUpdateResult.UpToDate(request.ComponentKind,
                    request.ComponentId, request.CurrentVersion,
                    request.CurrentVersion, request.Channel, checkedAtUtc);
            }
        }

        private sealed class RecordingProductUpdateProvider :
            IDdfProductUpdateProvider
        {
            private readonly Func<DdfProductUpdateRequest,
                DdfProductUpdateResult> resultFactory;

            public RecordingProductUpdateProvider(
                Func<DdfProductUpdateRequest, DdfProductUpdateResult> resultFactory)
            {
                this.resultFactory = resultFactory ??
                    throw new ArgumentNullException(nameof(resultFactory));
            }

            public int CheckCount { get; private set; }
            public DdfProductUpdateRequest LastRequest { get; private set; }

            public Task<DdfProductUpdateResult> CheckAsync(
                DdfProductUpdateRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckCount++;
                LastRequest = request;
                return Task.FromResult(resultFactory(request));
            }
        }

        private sealed class LoopbackHttpServer : IDisposable
        {
            private readonly TcpListener listener;
            private readonly CancellationTokenSource stop = new();
            private readonly IReadOnlyList<Func<string, CancellationToken,
                Task<byte[]>>> responses;

            public LoopbackHttpServer(params Func<string, CancellationToken,
                Task<byte[]>>[] responses)
            {
                if (responses == null || responses.Length == 0 ||
                    responses.Any(item => item == null))
                    throw new ArgumentException(
                        "Il server loopback richiede almeno una risposta.",
                        nameof(responses));
                this.responses = responses;
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Endpoint = new Uri("http://127.0.0.1:" + port + "/releases");
                Completion = ServeAsync();
            }

            public Uri Endpoint { get; }
            public List<string> Requests { get; } = new();
            public Task Completion { get; }

            public void Dispose()
            {
                stop.Cancel();
                listener.Stop();
                try { Completion.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                stop.Dispose();
            }

            private async Task ServeAsync()
            {
                try
                {
                    foreach (Func<string, CancellationToken, Task<byte[]>> response
                        in responses)
                    {
                        using TcpClient client = await listener.AcceptTcpClientAsync(
                            stop.Token).ConfigureAwait(false);
                        using NetworkStream stream = client.GetStream();
                        string headers = await ReadHeadersAsync(stream, stop.Token)
                            .ConfigureAwait(false);
                        Requests.Add(headers);
                        byte[] payload = await response(headers, stop.Token)
                            .ConfigureAwait(false);
                        if (payload.Length != 0)
                        {
                            await stream.WriteAsync(payload, stop.Token)
                                .ConfigureAwait(false);
                            await stream.FlushAsync(stop.Token)
                                .ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                }
                catch (SocketException) when (stop.IsCancellationRequested)
                {
                }
            }

            private static async Task<string> ReadHeadersAsync(NetworkStream stream,
                CancellationToken cancellationToken)
            {
                var builder = new StringBuilder();
                var buffer = new byte[1];
                while (builder.Length < 32 * 1024)
                {
                    int read = await stream.ReadAsync(buffer, cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0) break;
                    builder.Append((char)buffer[0]);
                    if (builder.Length >= 4 &&
                        builder[^4] == '\r' && builder[^3] == '\n' &&
                        builder[^2] == '\r' && builder[^1] == '\n')
                        return builder.ToString();
                }
                throw new InvalidDataException(
                    "Richiesta HTTP loopback incompleta o oltre quota.");
            }
        }

        private sealed class TestAuthoringMigration : IDdfLanguageAuthoringMigration
        {
            private readonly string manifest;

            public TestAuthoringMigration(string manifest) =>
                this.manifest = manifest;

            public string Id => "test-0.9-to-1.0";
            public string SourceSchemaVersion => "0.9";
            public string TargetSchemaVersion => "1.0";

            public DdfLanguageAuthoringChangeSet CreateChangeSet(
                DdfLanguageAuthoringProject project) =>
                new DdfLanguageAuthoringChangeSet(project.Revision, new[]
                {
                    DdfLanguageAuthoringChange.Upsert("manifest.json", manifest)
                }, "Migrazione test 0.9 → 1.0");
        }

        private sealed class TestAuthoringTransformation :
            IDdfLanguageAuthoringTransformation
        {
            public string Id => "test.update-notes";
            public string DisplayName => "Aggiorna note di test";

            public DdfLanguageAuthoringChangeSet CreateChangeSet(
                DdfLanguageAuthoringProject project) =>
                new DdfLanguageAuthoringChangeSet(project.Revision, new[]
                {
                    DdfLanguageAuthoringChange.Upsert("author-notes.json",
                        "{\n  // mantenere questo commento\n  \"value\": 43\n}\n"),
                    DdfLanguageAuthoringChange.Upsert("preview-only.txt",
                        "anteprima\n"),
                    DdfLanguageAuthoringChange.Remove("grammar.ebnf")
                }, DisplayName);
        }

        private sealed class InvalidTestAuthoringTransformation :
            IDdfLanguageAuthoringTransformation
        {
            public string Id => string.Empty;
            public string DisplayName => "Trasformazione non valida";
            public DdfLanguageAuthoringChangeSet CreateChangeSet(
                DdfLanguageAuthoringProject project) => null;
        }

        private sealed class UpsertAuthoringTransformation :
            IDdfLanguageAuthoringTransformation
        {
            private readonly string path;
            private readonly string content;

            public UpsertAuthoringTransformation(string id, string path,
                string content)
            {
                Id = id;
                this.path = path;
                this.content = content;
            }

            public string Id { get; }
            public string DisplayName => Id;

            public DdfLanguageAuthoringChangeSet CreateChangeSet(
                DdfLanguageAuthoringProject project) =>
                new DdfLanguageAuthoringChangeSet(project.Revision, new[]
                {
                    DdfLanguageAuthoringChange.Upsert(path, content)
                }, DisplayName);
        }

        private sealed class InlineProgress<T> : IProgress<T>
        {
            private readonly Action<T> report;

            public InlineProgress(Action<T> report) => this.report = report;

            public void Report(T value) => report(value);
        }

        private sealed class ImmediateProcessWaiter :
            IDdfProductUpdateProcessWaiter
        {
            public int ProcessId { get; private set; }
            public DateTimeOffset ProcessStartedUtc { get; private set; }

            public Task WaitForExitAsync(int processId,
                DateTimeOffset processStartedUtc, TimeSpan timeout,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProcessId = processId;
                ProcessStartedUtc = processStartedUtc;
                return Task.CompletedTask;
            }
        }

        private sealed class FixedProductUpdateHealthCheck :
            IDdfProductUpdateHealthCheck
        {
            private readonly bool success;

            public FixedProductUpdateHealthCheck(bool success) =>
                this.success = success;

            public int Calls { get; private set; }

            public Task<DdfProductUpdateHealthCheckResult> CheckAsync(
                DdfProductUpdateInstallationPlan plan,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Calls++;
                return Task.FromResult(success
                    ? DdfProductUpdateHealthCheckResult.Passed()
                    : DdfProductUpdateHealthCheckResult.Failed("Test failure."));
            }
        }

        private sealed class RecordedHttpRequest
        {
            public RecordedHttpRequest(HttpRequestMessage request)
            {
                Uri = request.RequestUri;
                HasContent = request.Content != null;
                HasAuthorization = request.Headers.Authorization != null;
                IfNoneMatch = request.Headers.IfNoneMatch.FirstOrDefault()
                    ?.ToString() ?? string.Empty;
                IfModifiedSince = request.Headers.IfModifiedSince;
            }

            public Uri Uri { get; }
            public bool HasContent { get; }
            public bool HasAuthorization { get; }
            public string IfNoneMatch { get; }
            public DateTimeOffset? IfModifiedSince { get; }
        }

        private sealed class QueueHttpMessageHandler : HttpMessageHandler
        {
            private readonly Queue<Func<HttpRequestMessage, CancellationToken,
                Task<HttpResponseMessage>>> responses = new();

            public QueueHttpMessageHandler() { }

            public QueueHttpMessageHandler(HttpStatusCode statusCode)
            {
                Enqueue((request, cancellationToken) => Task.FromResult(
                    new HttpResponseMessage(statusCode)));
            }

            public List<RecordedHttpRequest> Requests { get; } = new();

            public void Enqueue(Func<HttpRequestMessage, CancellationToken,
                Task<HttpResponseMessage>> response) => responses.Enqueue(response);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(new RecordedHttpRequest(request));
                if (responses.Count == 0)
                    throw new InvalidOperationException("Risposta HTTP test mancante.");
                HttpResponseMessage response = await responses.Dequeue()(
                    request, cancellationToken);
                response.RequestMessage = request;
                return response;
            }
        }

        private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
        {
            private readonly Exception exception;
            public ThrowingHttpMessageHandler(Exception exception) =>
                this.exception = exception;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromException<HttpResponseMessage>(exception);
        }

        private sealed class DelayedHttpMessageHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request
                };
            }
        }

        // Test double only: production hosts must use independently protected durable storage.
        private sealed class MemoryPluginAuditAnchor : IDdfExtensionPluginAuditAnchor
        {
            private readonly object gate = new object();
            private DdfExtensionPluginAuditCheckpoint current;

            public bool FailNextAdvance { get; set; }

            public bool TryInitialize(DdfExtensionPluginAuditCheckpoint initial)
            {
                lock (gate)
                {
                    if (current != null || initial == null || initial.Sequence != 0)
                        return false;
                    current = initial;
                    return true;
                }
            }

            public DdfExtensionPluginAuditCheckpoint Read()
            {
                lock (gate) return current;
            }

            public bool TryAdvance(DdfExtensionPluginAuditCheckpoint expected,
                DdfExtensionPluginAuditCheckpoint next)
            {
                lock (gate)
                {
                    if (FailNextAdvance)
                    {
                        FailNextAdvance = false;
                        return false;
                    }
                    if (current == null || !current.Matches(expected) ||
                        next == null || next.Sequence != expected.Sequence + 1)
                        return false;
                    current = next;
                    return true;
                }
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
