using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DDFLanguageEditor.Core
{
    public enum DdfLanguagePackageOrigin { Directory, Archive, Embedded }
    public enum DdfLanguagePackageDiagnosticSeverity { Warning, Error }

    public sealed class DdfLanguagePackageDiagnostic
    {
        public DdfLanguagePackageDiagnostic(string code, string message, string file = null,
            string jsonPath = null, DdfLanguagePackageDiagnosticSeverity severity = DdfLanguagePackageDiagnosticSeverity.Error)
        {
            Code = code; Message = message; File = file ?? string.Empty;
            JsonPath = jsonPath ?? string.Empty; Severity = severity;
        }
        public string Code { get; }
        public string Message { get; }
        public string File { get; }
        public string JsonPath { get; }
        public DdfLanguagePackageDiagnosticSeverity Severity { get; }
        public override string ToString() => Code + ": " + Message +
            (string.IsNullOrEmpty(File) ? string.Empty : " [" + File +
             (string.IsNullOrEmpty(JsonPath) ? string.Empty : ":" + JsonPath) + "]");
    }

    public sealed class DdfLanguagePackage
    {
        internal DdfLanguagePackage(string id, string name, string packageVersion, string languageVersion,
            IReadOnlyList<string> extensions, DdfLanguageDefinition language, DdfCompiledGrammar grammar,
            DdfLanguagePackageOrigin origin, string source)
        {
            Id = id; Name = name; PackageVersion = packageVersion; LanguageVersion = languageVersion;
            FileExtensions = extensions; Language = language; Grammar = grammar; Origin = origin; Source = source;
        }
        public string Id { get; }
        public string Name { get; }
        public string PackageVersion { get; }
        public string LanguageVersion { get; }
        public IReadOnlyList<string> FileExtensions { get; }
        public DdfLanguageDefinition Language { get; }
        public DdfCompiledGrammar Grammar { get; }
        public DdfLanguagePackageOrigin Origin { get; }
        public string Source { get; }
    }

    public sealed class DdfLanguagePackageLoadResult
    {
        internal DdfLanguagePackageLoadResult(DdfLanguagePackage package,
            IEnumerable<DdfLanguagePackageDiagnostic> diagnostics)
        {
            Package = package;
            Diagnostics = (diagnostics ?? Array.Empty<DdfLanguagePackageDiagnostic>()).ToList().AsReadOnly();
        }
        public DdfLanguagePackage Package { get; }
        public IReadOnlyList<DdfLanguagePackageDiagnostic> Diagnostics { get; }
        public bool Success => Package != null;
        public bool HasErrors => Diagnostics.Any(item =>
            item.Severity == DdfLanguagePackageDiagnosticSeverity.Error);
    }

    public static class DdfLanguagePackageLoader
    {
        public const string SupportedSchemaVersion = "1.0";
        public const string SupportedEngineApi = "1.0";
        private static readonly string[] RequiredSections =
        {
            "lexicon", "grammar", "types", "semantics", "formatting",
            "intellisense", "runtime", "build", "standardLibrary"
        };
        private static readonly IReadOnlyDictionary<string, string> SupportedAdapters =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["lexicon"] = "ddf-lexicon-v1", ["grammar"] = "ddf-grammar-v2",
                ["types"] = "ddf-types-v1", ["semantics"] = "ddf-semantics-v1",
                ["formatting"] = "ddf-formatter-v1", ["intellisense"] = "ddf-intellisense-v1",
                ["runtime"] = "ddf-runtime-v1", ["build"] = "ddf-build-v1",
                ["standardLibrary"] = "ddf-standard-library-v1"
            };
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        public static DdfLanguagePackageLoadResult Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Failure("DDFL001", "Percorso del pacchetto non specificato.");
            try
            {
                if (Directory.Exists(path))
                    return load(new DirectoryReader(path), DdfLanguagePackageOrigin.Directory, Path.GetFullPath(path));
                if (File.Exists(path))
                    return load(new ArchiveReader(path), DdfLanguagePackageOrigin.Archive, Path.GetFullPath(path));
                return Failure("DDFL002", "Pacchetto non trovato.", path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                               exception is InvalidDataException || exception is JsonException ||
                                               exception is ArgumentException || exception is NotSupportedException)
            {
                return Failure("DDFL003", exception.Message, path);
            }
        }

        public static DdfLanguagePackageLoadResult LoadEmbeddedDefault()
        {
            try { return load(new EmbeddedReader(), DdfLanguagePackageOrigin.Embedded, "embedded://ddf"); }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is JsonException)
            { return Failure("DDFL004", "Fallback DDF incorporato non valido: " + exception.Message, "embedded://ddf"); }
        }

        public static DdfLanguagePackageLoadResult LoadWithEmbeddedFallback(string path)
        {
            DdfLanguagePackageLoadResult external = Load(path);
            if (external.Success) return external;
            DdfLanguagePackageLoadResult embedded = LoadEmbeddedDefault();
            var diagnostics = external.Diagnostics.ToList();
            if (embedded.Success)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL005",
                    "Pacchetto esterno rifiutato; è attivo il fallback DDF incorporato.", path, null,
                    DdfLanguagePackageDiagnosticSeverity.Warning));
                return new DdfLanguagePackageLoadResult(embedded.Package, diagnostics);
            }
            diagnostics.AddRange(embedded.Diagnostics);
            return new DdfLanguagePackageLoadResult(null, diagnostics);
        }

        public static void ExportEmbeddedDefault(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory richiesta.", nameof(directory));
            Directory.CreateDirectory(directory);
            using (var reader = new EmbeddedReader())
            {
                foreach (string file in reader.Files)
                    File.WriteAllText(Path.Combine(directory, file), reader.Read(file), new UTF8Encoding(false));
            }
        }

        private static DdfLanguagePackageLoadResult load(IPackageReader reader,
            DdfLanguagePackageOrigin origin, string source)
        {
            using (reader)
            {
                var diagnostics = new List<DdfLanguagePackageDiagnostic>();
                ManifestDto manifest = read<ManifestDto>(reader, "manifest.json", diagnostics);
                if (manifest == null) return new DdfLanguagePackageLoadResult(null, diagnostics);
                validateManifest(manifest, diagnostics);
                if (hasErrors(diagnostics)) return new DdfLanguagePackageLoadResult(null, diagnostics);

                var sectionDocuments = new Dictionary<string, SectionDto>(StringComparer.Ordinal);
                foreach (string section in RequiredSections)
                {
                    string file = manifest.Sections[section];
                    SectionDto document = section == "lexicon"
                        ? read<LexiconDto>(reader, file, diagnostics)
                        : section == "grammar"
                            ? read<GrammarDto>(reader, file, diagnostics)
                            : section == "intellisense"
                                ? read<IntellisenseDto>(reader, file, diagnostics)
                                : section == "formatting"
                                    ? read<FormattingDto>(reader, file, diagnostics)
                                    : read<SectionDto>(reader, file, diagnostics);
                    if (document != null)
                    {
                        sectionDocuments[section] = document;
                        validateSection(section, file, document, manifest.Sections, diagnostics);
                    }
                }
                if (hasErrors(diagnostics)) return new DdfLanguagePackageLoadResult(null, diagnostics);

                DdfLanguageDefinition language = createLanguage(
                    (LexiconDto)sectionDocuments["lexicon"], manifest.Sections["lexicon"], diagnostics);
                if (hasErrors(diagnostics) || language == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);

                DdfCompiledGrammar grammar = createGrammar(reader,
                    (GrammarDto)sectionDocuments["grammar"], manifest.Sections["grammar"], diagnostics);
                if (hasErrors(diagnostics) || grammar == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);
                language.Grammar = grammar;
                language.Formatting = createFormatting((FormattingDto)sectionDocuments["formatting"], language,
                    manifest.Sections["formatting"], diagnostics);
                if (hasErrors(diagnostics) || language.Formatting == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);
                language.Intellisense = createIntellisense(
                    (IntellisenseDto)sectionDocuments["intellisense"],
                    manifest.Sections["intellisense"], diagnostics);
                if (hasErrors(diagnostics) || language.Intellisense == null)
                    return new DdfLanguagePackageLoadResult(null, diagnostics);

                var package = new DdfLanguagePackage(manifest.Id, manifest.Name,
                    manifest.PackageVersion, manifest.LanguageVersion,
                    manifest.FileExtensions.AsReadOnly(), language, grammar, origin, source);
                return new DdfLanguagePackageLoadResult(package, diagnostics);
            }
        }

        private static T read<T>(IPackageReader reader, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics) where T : class
        {
            if (!reader.Exists(file))
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL010", "File richiesto mancante.", file));
                return null;
            }
            try
            {
                T result = JsonSerializer.Deserialize<T>(reader.Read(file), JsonOptions);
                if (result == null) throw new JsonException("Documento JSON vuoto.");
                return result;
            }
            catch (JsonException exception)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL011", exception.Message, file));
                return null;
            }
        }

        private static void validateManifest(ManifestDto manifest, ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            require(manifest.SchemaVersion == SupportedSchemaVersion, diagnostics, "DDFL020",
                "schemaVersion non supportata: " + manifest.SchemaVersion, "manifest.json", "$.schemaVersion");
            require(manifest.EngineApi == SupportedEngineApi, diagnostics, "DDFL021",
                "engineApi non compatibile: " + manifest.EngineApi, "manifest.json", "$.engineApi");
            require(!string.IsNullOrWhiteSpace(manifest.Id) && Regex.IsMatch(manifest.Id, "^[a-z][a-z0-9.-]*$"),
                diagnostics, "DDFL022", "Identificatore pacchetto non valido.", "manifest.json", "$.id");
            require(!string.IsNullOrWhiteSpace(manifest.Name), diagnostics, "DDFL023",
                "Nome pacchetto richiesto.", "manifest.json", "$.name");
            require(Version.TryParse(manifest.PackageVersion, out _), diagnostics, "DDFL024",
                "Versione pacchetto non valida.", "manifest.json", "$.packageVersion");
            require(Version.TryParse(manifest.LanguageVersion, out _), diagnostics, "DDFL025",
                "Versione linguaggio non valida.", "manifest.json", "$.languageVersion");
            manifest.FileExtensions = manifest.FileExtensions ?? new List<string>();
            require(manifest.FileExtensions.Count > 0 && unique(manifest.FileExtensions) &&
                    manifest.FileExtensions.All(item => Regex.IsMatch(item ?? string.Empty, "^\\.[A-Za-z0-9]+$")),
                diagnostics, "DDFL026", "Estensioni file mancanti, duplicate o non valide.", "manifest.json", "$.fileExtensions");
            manifest.Sections = manifest.Sections ?? new Dictionary<string, string>();
            foreach (string section in manifest.Sections.Keys.Except(RequiredSections, StringComparer.Ordinal))
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL029",
                    "Sezione manifest sconosciuta: " + section + ".", "manifest.json", "$.sections." + section));
            }
            foreach (string section in RequiredSections)
            {
                require(manifest.Sections.TryGetValue(section, out string file) && safeFile(file), diagnostics,
                    "DDFL027", "Riferimento sezione mancante o non sicuro: " + section + ".",
                    "manifest.json", "$.sections." + section);
            }
            require(manifest.Sections.Values.Where(value => value != null).Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                    manifest.Sections.Values.Count, diagnostics, "DDFL028", "Due sezioni riferiscono lo stesso file.",
                "manifest.json", "$.sections");
        }

        private static void validateSection(string name, string file, SectionDto section,
            IReadOnlyDictionary<string, string> sectionFiles, ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            require(section.SchemaVersion == SupportedSchemaVersion, diagnostics, "DDFL030",
                "Versione sezione non supportata.", file, "$.schemaVersion");
            require(SupportedAdapters[name] == section.Adapter, diagnostics, "DDFL031",
                "Adapter non supportato per " + name + ": " + section.Adapter, file, "$.adapter");
            foreach (string dependency in section.DependsOn ?? new List<string>())
                require(sectionFiles.ContainsKey(dependency), diagnostics, "DDFL032",
                    "Dipendenza di sezione sconosciuta: " + dependency, file, "$.dependsOn");
        }

        private static DdfLanguageDefinition createLanguage(LexiconDto lexicon, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            lexicon.Keywords = lexicon.Keywords ?? new List<KeywordDto>();
            lexicon.BooleanLiterals = lexicon.BooleanLiterals ?? new List<string>();
            lexicon.Operators = lexicon.Operators ?? new List<OperatorDto>();
            lexicon.Punctuation = lexicon.Punctuation ?? new List<string>();
            validateUnique(lexicon.Keywords.Select(item => item?.Text), "keyword", file, "$.keywords", diagnostics);
            validateUnique(lexicon.BooleanLiterals, "literal", file, "$.booleanLiterals", diagnostics);
            validateUnique(lexicon.Operators.Select(item => item?.Text), "operatore", file, "$.operators", diagnostics);
            validateUnique(lexicon.Punctuation, "punteggiatura", file, "$.punctuation", diagnostics);
            validateUniqueRoles(lexicon.Keywords.Where(item => item != null).Select(item => item.Role),
                "ruolo keyword", file, "$.keywords", diagnostics);
            validateUniqueRoles(lexicon.Operators.Where(item => item != null).Select(item => item.Role),
                "ruolo operatore", file, "$.operators", diagnostics);
            DdfLexicalDefinition lexicalRules = createLexicalRules(lexicon, file, diagnostics);
            var keywords = new List<DdfKeywordDefinition>();
            var operators = new List<DdfOperatorDefinition>();
            foreach (KeywordDto item in lexicon.Keywords)
            {
                if (item == null || !Enum.TryParse(item.TokenKind, true, out DdfTokenKind tokenKind) ||
                    !Enum.IsDefined(tokenKind) || !validRole(item.Role ?? "None"))
                { diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL041", "Keyword con categoria o ruolo non valido.", file, "$.keywords")); continue; }
                try { keywords.Add(new DdfKeywordDefinition(item.Text, tokenKind, item.Role ?? "None")); }
                catch (ArgumentException exception) { diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL042", exception.Message, file, "$.keywords")); }
            }
            foreach (OperatorDto item in lexicon.Operators)
            {
                if (item == null || !Enum.TryParse(item.Associativity ?? "Left", true, out DdfOperatorAssociativity associativity) ||
                    !Enum.IsDefined(associativity) || !Enum.TryParse(item.Role ?? "None", true, out DdfOperatorRole role) || !Enum.IsDefined(role))
                { diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL043", "Operatore con associatività o ruolo non valido.", file, "$.operators")); continue; }
                try { operators.Add(new DdfOperatorDefinition(item.Text, item.BinaryPrecedence, associativity,
                    item.PrefixPrecedence, item.PostfixPrecedence, role)); }
                catch (ArgumentException exception) { diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL044", exception.Message, file, "$.operators")); }
            }
            char[] punctuation = lexicon.Punctuation.Where(item => item?.Length == 1).Select(item => item[0]).ToArray();
            if (punctuation.Length != lexicon.Punctuation.Count)
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL045", "Ogni punteggiatura deve contenere un solo carattere.", file, "$.punctuation"));
            if (hasErrors(diagnostics)) return null;
            try { return new DdfLanguageDefinition(keywords, lexicon.BooleanLiterals, operators, punctuation, lexicalRules); }
            catch (ArgumentException exception) { diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL046", exception.Message, file)); return null; }
        }

        private static DdfCompiledGrammar createGrammar(IPackageReader reader, GrammarDto grammar, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (grammar == null) return null;
            require(grammar.Notation == "ebnf-1.0", diagnostics, "DDFL060",
                "Notazione grammaticale non supportata: " + grammar.Notation + ".", file, "$.notation");
            require(grammar.ChoiceStrategy == "longest", diagnostics, "DDFL061",
                "Strategia delle alternative non supportata: " + grammar.ChoiceStrategy + ".", file, "$.choiceStrategy");
            require(safeResourceFile(grammar.EbnfFile) && reader.Exists(grammar.EbnfFile), diagnostics, "DDFL062",
                "File EBNF mancante o non sicuro.", file, "$.ebnfFile");
            require(!string.IsNullOrWhiteSpace(grammar.StartRule), diagnostics, "DDFL063",
                "Regola iniziale richiesta.", file, "$.startRule");
            if (hasErrors(diagnostics)) return null;
            try
            {
                var nodes = (grammar.Nodes ?? new List<GrammarNodeDto>()).Select(node =>
                    new DdfGrammarNodeDefinition(node.Rule, node.Kind,
                        (node.Fields ?? new List<GrammarFieldDto>()).Select(field =>
                            new DdfGrammarFieldDefinition(field.Name, field.SourceRule, field.Multiple)),
                        createIdeDefinition(node.Ide)));
                return DdfEbnfCompiler.Compile(reader.Read(grammar.EbnfFile), grammar.StartRule, nodes);
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL064", exception.Message,
                    grammar.EbnfFile, "$"));
                return null;
            }
        }

        private static DdfGrammarIdeDefinition createIdeDefinition(GrammarIdeDto ide)
        {
            if (ide == null) return null;
            return new DdfGrammarIdeDefinition(
                parseOptionalEnum<DdfSymbolKind>(ide.SymbolKind, "symbolKind"),
                ide.NameField, ide.NamePrefix, ide.NameSuffix, ide.Detail,
                parseOptionalEnum<DdfFoldingKind>(ide.FoldingKind, "foldingKind"), ide.FoldingField,
                parseOptionalEnum<DdfBreadcrumbKind>(ide.BreadcrumbKind, "breadcrumbKind"),
                ide.BreadcrumbLabel, ide.BreadcrumbSelectionField, ide.BreadcrumbSelectionLength,
                ide.Selectable, ide.TypeField, ide.ReturnTypeField, ide.ParametersField,
                parseOptionalEnum<DdfSymbolKind>(ide.ChildSymbolKind, "childSymbolKind"));
        }

        private static DdfIntellisenseDefinition createIntellisense(IntellisenseDto value, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (value == null) return null;
            try
            {
                require(value.Completion != null, diagnostics, "DDFL070",
                    "Configurazione completion richiesta.", file, "$.completion");
                require(value.SignatureHelp != null, diagnostics, "DDFL071",
                    "Configurazione Signature Help richiesta.", file, "$.signatureHelp");
                require(value.SymbolKinds != null && value.SymbolKinds.Count > 0, diagnostics, "DDFL072",
                    "Presentazioni dei simboli richieste.", file, "$.symbolKinds");
                require(value.Snippets != null, diagnostics, "DDFL073",
                    "Catalogo snippet richiesto.", file, "$.snippets");
                if (hasErrors(diagnostics)) return null;

                CompletionDto completion = value.Completion;
                SignatureHelpDto signature = value.SignatureHelp;
                require(new[] { completion.KeywordOrigin, completion.BooleanOrigin, completion.SnippetOrigin,
                    completion.StandardLibraryOrigin, completion.CurrentDocumentOrigin, completion.WorkspaceOrigin,
                    completion.TypeDetail, completion.ControlFlowDetail, completion.KeywordDetail,
                    completion.BooleanDetail, completion.StandardFunctionDetailPrefix }.All(item => item != null),
                    diagnostics, "DDFL075", "Configurazione completion incompleta.", file, "$.completion");
                require(new[] { signature.CurrentDocumentOrigin, signature.WorkspaceOrigin,
                    signature.StandardLibraryOrigin }.All(item => item != null), diagnostics, "DDFL076",
                    "Configurazione Signature Help incompleta.", file, "$.signatureHelp");
                if (hasErrors(diagnostics)) return null;

                var presentations = value.SymbolKinds.Select(item => new DdfSymbolPresentation(
                    parseRequiredEnum<DdfSymbolKind>(item.Kind, "symbol kind"), item.Label, item.Documentation));
                var snippets = value.Snippets.Select(item => new DdfSnippetTemplate(
                    item.Prefix, item.DisplayText, item.Description, item.Body, item.RequiresKeyword));
                return new DdfIntellisenseDefinition(snippets, presentations,
                    completion.KeywordOrigin, completion.BooleanOrigin, completion.SnippetOrigin,
                    completion.StandardLibraryOrigin, completion.CurrentDocumentOrigin, completion.WorkspaceOrigin,
                    completion.TypeDetail, completion.ControlFlowDetail, completion.KeywordDetail,
                    completion.BooleanDetail, completion.StandardFunctionDetailPrefix,
                    signature.CurrentDocumentOrigin, signature.WorkspaceOrigin, signature.StandardLibraryOrigin);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL074", exception.Message, file, "$"));
                return null;
            }
        }

        private static DdfFormattingDefinition createFormatting(FormattingDto value,
            DdfLanguageDefinition language, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            if (value == null) return null;
            try
            {
                require(value.Tokens != null && value.AutoPairs != null && value.IndentAfter != null &&
                    value.QuickFixes != null, diagnostics, "DDFL080",
                    "Configurazione formatter incompleta.", file, "$");
                if (hasErrors(diagnostics)) return null;
                string[] structuralTokens = { value.Tokens.BlockOpen, value.Tokens.BlockClose,
                    value.Tokens.GroupOpen, value.Tokens.GroupClose, value.Tokens.IndexOpen,
                    value.Tokens.IndexClose, value.Tokens.StatementTerminator, value.Tokens.Separator };
                require(structuralTokens.All(token => !string.IsNullOrEmpty(token) && token.Length == 1 &&
                    language.Punctuation.Contains(token[0])), diagnostics, "DDFL082",
                    "I delimitatori del formatter devono essere punteggiatura dichiarata nel lessico.",
                    file, "$.tokens");
                if (hasErrors(diagnostics)) return null;
                return new DdfFormattingDefinition(value.Tokens.BlockOpen, value.Tokens.BlockClose,
                    value.Tokens.GroupOpen, value.Tokens.GroupClose, value.Tokens.IndexOpen,
                    value.Tokens.IndexClose, value.Tokens.StatementTerminator, value.Tokens.Separator,
                    value.AutoPairs.Select(item => new KeyValuePair<char, char>(one(item.Open), one(item.Close))),
                    value.IndentAfter, value.BlankLineBetweenStatements,
                    value.QuickFixes.Select(item => new DdfQuickFixRule(item.DiagnosticCode, item.Title, item.Replacement)));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL081", exception.Message, file, "$"));
                return null;
            }
        }

        private static char one(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 1)
                throw new ArgumentException("Le coppie automatiche richiedono caratteri singoli.");
            return value[0];
        }

        private static T? parseOptionalEnum<T>(string value, string property) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(typeof(T), parsed)) return parsed;
            throw new ArgumentException("Valore IDE non supportato per " + property + ": " + value + ".");
        }

        private static T parseRequiredEnum<T>(string value, string property) where T : struct
        {
            T? parsed = parseOptionalEnum<T>(value, property);
            if (parsed.HasValue) return parsed.Value;
            throw new ArgumentException("Valore richiesto per " + property + ".");
        }

        private static DdfLexicalDefinition createLexicalRules(LexiconDto lexicon, string file,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            LexicalDto lexical = lexicon.Lexical;
            if (lexical == null || lexical.Whitespace == null || lexical.Identifier == null ||
                lexical.Identifier.Start == null || lexical.Identifier.Part == null ||
                lexical.Number == null || lexical.Number.Digits == null || lexical.DelimitedTokens == null)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL050",
                    "Definizione delle regole lessicali incompleta.", file, "$.lexical"));
                return null;
            }

            try
            {
                DdfCharacterSetDefinition whitespace = createCharacterSet(lexical.Whitespace);
                DdfCharacterSetDefinition identifierStart = createCharacterSet(lexical.Identifier.Start);
                DdfCharacterSetDefinition identifierPart = createCharacterSet(lexical.Identifier.Part);
                DdfCharacterSetDefinition digits = createCharacterSet(lexical.Number.Digits);
                require(identifierStart.UnicodeLetters ? identifierPart.UnicodeLetters : true, diagnostics, "DDFL051",
                    "Le lettere ammesse all'inizio devono essere ammesse anche nel resto dell'identificatore.", file, "$.lexical.identifier");
                require(identifierStart.UnicodeDigits ? identifierPart.UnicodeDigits : true, diagnostics, "DDFL051",
                    "Le cifre ammesse all'inizio devono essere ammesse anche nel resto dell'identificatore.", file, "$.lexical.identifier");
                require(identifierStart.Characters.All(identifierPart.Contains), diagnostics, "DDFL051",
                    "I caratteri espliciti iniziali devono essere ammessi anche nel resto dell'identificatore.", file, "$.lexical.identifier");
                require(digits.UnicodeDigits || digits.Characters.Length > 0, diagnostics, "DDFL052",
                    "La regola numerica non definisce alcuna cifra.", file, "$.lexical.number.digits");

                var rules = new List<DdfDelimitedTokenRule>();
                for (int index = 0; index < lexical.DelimitedTokens.Count; index++)
                {
                    DelimitedTokenDto item = lexical.DelimitedTokens[index];
                    string path = "$.lexical.delimitedTokens[" + index + "]";
                    if (item == null || !Enum.TryParse(item.TokenKind, true, out DdfTokenKind tokenKind) || !Enum.IsDefined(tokenKind))
                    {
                        diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL053",
                            "Categoria del token delimitato non valida.", file, path + ".tokenKind"));
                        continue;
                    }
                    try
                    {
                        rules.Add(new DdfDelimitedTokenRule(tokenKind, item.Start, item.End,
                            item.EndsAtLineBreak, item.AllowLineBreak, item.EscapeCharacter,
                            item.UnterminatedCode, item.UnterminatedMessage));
                    }
                    catch (ArgumentException exception)
                    {
                        diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL054", exception.Message, file, path));
                    }
                }

                for (int earlier = 0; !lexical.LongestMatch && earlier < rules.Count; earlier++)
                    for (int later = earlier + 1; later < rules.Count; later++)
                        if (rules[later].Start.StartsWith(rules[earlier].Start, StringComparison.Ordinal))
                            diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL055",
                                "Regola irraggiungibile senza longest match: " + rules[later].Start + ".",
                                file, "$.lexical.delimitedTokens[" + later + "]"));
                for (int earlier = 0; !lexical.LongestMatch && earlier < lexicon.Operators.Count; earlier++)
                    for (int later = earlier + 1; later < lexicon.Operators.Count; later++)
                        if (lexicon.Operators[earlier] != null && lexicon.Operators[later] != null &&
                            !string.IsNullOrEmpty(lexicon.Operators[earlier].Text) &&
                            !string.IsNullOrEmpty(lexicon.Operators[later].Text) &&
                            lexicon.Operators[later].Text.StartsWith(lexicon.Operators[earlier].Text, StringComparison.Ordinal))
                            diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL066",
                                "Operatore irraggiungibile senza longest match: " + lexicon.Operators[later].Text + ".",
                                file, "$.operators[" + later + "]"));

                var number = new DdfNumberRuleDefinition(digits, lexical.Number.DecimalSeparator,
                    lexical.Number.RequireFractionDigits);
                if (number.DecimalSeparator.Length == 1)
                    require(!whitespace.Contains(number.DecimalSeparator[0]) &&
                            !digits.Contains(number.DecimalSeparator[0]) &&
                            !identifierPart.Contains(number.DecimalSeparator[0]), diagnostics, "DDFL067",
                        "Il separatore decimale è in conflitto con un'altra classe di caratteri.",
                        file, "$.lexical.number.decimalSeparator");
                var result = new DdfLexicalDefinition(whitespace, identifierStart, identifierPart,
                    number, rules, lexical.LongestMatch);
                require(!characterSetsOverlap(whitespace, identifierPart) && !characterSetsOverlap(whitespace, digits),
                    diagnostics, "DDFL058", "Spazi, identificatori e cifre contengono classi di caratteri in conflitto.",
                    file, "$.lexical");
                require(!characterSetsOverlap(identifierStart, digits), diagnostics, "DDFL059",
                    "L'inizio degli identificatori è in conflitto con la regola numerica.", file, "$.lexical");
                foreach (DdfDelimitedTokenRule rule in result.DelimitedTokens)
                {
                    require(!whitespace.Contains(rule.Start[0]), diagnostics, "DDFL056",
                        "Il marcatore lessicale inizia con uno spazio configurato: " + rule.Start + ".", file, "$.lexical.delimitedTokens");
                    require(!lexicon.Operators.Any(item => item != null && item.Text == rule.Start) &&
                            !lexicon.Punctuation.Any(item => item == rule.Start), diagnostics, "DDFL061",
                        "Il marcatore lessicale duplica un operatore o delimitatore: " + rule.Start + ".", file, "$.lexical.delimitedTokens");
                }
                foreach (KeywordDto keyword in lexicon.Keywords.Where(item => item != null))
                    require(result.IsValidIdentifier(keyword.Text), diagnostics, "DDFL062",
                        "Keyword non raggiungibile con le regole degli identificatori: " + keyword.Text + ".", file, "$.keywords");
                foreach (string literal in lexicon.BooleanLiterals)
                    require(result.IsValidIdentifier(literal), diagnostics, "DDFL062",
                        "Literal booleano non raggiungibile con le regole degli identificatori: " + literal + ".", file, "$.booleanLiterals");
                var keywordTexts = new HashSet<string>(lexicon.Keywords.Where(item => item != null).Select(item => item.Text), StringComparer.Ordinal);
                require(!lexicon.BooleanLiterals.Any(keywordTexts.Contains), diagnostics, "DDFL065",
                    "Keyword e letterali booleani non possono condividere la stessa definizione.", file, "$.booleanLiterals");
                var operatorTexts = new HashSet<string>(lexicon.Operators.Where(item => item != null).Select(item => item.Text), StringComparer.Ordinal);
                require(!lexicon.Punctuation.Any(operatorTexts.Contains), diagnostics, "DDFL065",
                    "Operatori e punteggiatura non possono condividere la stessa definizione.", file, "$.punctuation");
                foreach (OperatorDto item in lexicon.Operators.Where(item => item != null && !string.IsNullOrEmpty(item.Text)))
                    require(!whitespace.Contains(item.Text[0]) && !identifierStart.Contains(item.Text[0]) && !digits.Contains(item.Text[0]),
                        diagnostics, "DDFL063", "Operatore irraggiungibile con le regole lessicali: " + item.Text + ".", file, "$.operators");
                foreach (string item in lexicon.Punctuation.Where(item => !string.IsNullOrEmpty(item)))
                    require(!whitespace.Contains(item[0]) && !identifierStart.Contains(item[0]) && !digits.Contains(item[0]),
                        diagnostics, "DDFL064", "Punteggiatura irraggiungibile con le regole lessicali: " + item + ".", file, "$.punctuation");
                return hasErrors(diagnostics) ? null : result;
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL057", exception.Message, file, "$.lexical"));
                return null;
            }
        }

        private static DdfCharacterSetDefinition createCharacterSet(CharacterSetDto value)
        {
            return new DdfCharacterSetDefinition(value.UnicodeLetters, value.UnicodeDigits,
                value.UnicodeWhitespace, value.Characters);
        }

        private static bool characterSetsOverlap(DdfCharacterSetDefinition first, DdfCharacterSetDefinition second)
        {
            if ((first.UnicodeLetters && second.UnicodeLetters) ||
                (first.UnicodeDigits && second.UnicodeDigits) ||
                (first.UnicodeWhitespace && second.UnicodeWhitespace)) return true;
            return first.Characters.Any(second.Contains) || second.Characters.Any(first.Contains);
        }

        private static void validateUnique(IEnumerable<string> values, string label, string file, string path,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                    diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL040", "Definizione " + label + " vuota o duplicata: " + value, file, path));
        }
        private static void validateUniqueRoles(IEnumerable<string> values, string label, string file, string path,
            ICollection<DdfLanguagePackageDiagnostic> diagnostics)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values.Where(item => !string.IsNullOrWhiteSpace(item) &&
                                                          !string.Equals(item, "None", StringComparison.OrdinalIgnoreCase)))
                if (!seen.Add(value))
                    diagnostics.Add(new DdfLanguagePackageDiagnostic("DDFL047",
                        "Definizione " + label + " duplicata: " + value + ".", file, path));
        }
        private static bool unique(IEnumerable<string> values) => values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Count();
        private static bool validRole(string role) => !string.IsNullOrWhiteSpace(role) && Regex.IsMatch(role, "^[A-Z][A-Za-z0-9.]*$");
        private static bool safeFile(string file) => !string.IsNullOrWhiteSpace(file) && Path.GetFileName(file) == file && file.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        private static bool safeResourceFile(string file) => !string.IsNullOrWhiteSpace(file) && Path.GetFileName(file) == file &&
            (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".ebnf", StringComparison.OrdinalIgnoreCase));
        private static bool hasErrors(IEnumerable<DdfLanguagePackageDiagnostic> diagnostics) => diagnostics.Any(item => item.Severity == DdfLanguagePackageDiagnosticSeverity.Error);
        private static void require(bool condition, ICollection<DdfLanguagePackageDiagnostic> diagnostics,
            string code, string message, string file, string path) { if (!condition) diagnostics.Add(new DdfLanguagePackageDiagnostic(code, message, file, path)); }
        private static DdfLanguagePackageLoadResult Failure(string code, string message, string file = null) =>
            new DdfLanguagePackageLoadResult(null, new[] { new DdfLanguagePackageDiagnostic(code, message, file) });

        private interface IPackageReader : IDisposable
        {
            IEnumerable<string> Files { get; }
            bool Exists(string file);
            string Read(string file);
        }
        private sealed class DirectoryReader : IPackageReader
        {
            private readonly string root;
            public DirectoryReader(string rootPath) { root = Path.GetFullPath(rootPath); }
            public IEnumerable<string> Files => Directory.EnumerateFiles(root).Where(path => safeResourceFile(Path.GetFileName(path))).Select(Path.GetFileName);
            public bool Exists(string file) => safeResourceFile(file) && File.Exists(Path.Combine(root, file));
            public string Read(string file) => File.ReadAllText(Path.Combine(root, file), Encoding.UTF8);
            public void Dispose() { }
        }
        private sealed class ArchiveReader : IPackageReader
        {
            private readonly ZipArchive archive;
            public ArchiveReader(string path) { archive = ZipFile.OpenRead(path); }
            public IEnumerable<string> Files => archive.Entries.Where(entry => safeResourceFile(entry.FullName)).Select(entry => entry.FullName);
            public bool Exists(string file) => safeResourceFile(file) && archive.GetEntry(file) != null;
            public string Read(string file) { using (StreamReader reader = new StreamReader(archive.GetEntry(file).Open(), Encoding.UTF8)) return reader.ReadToEnd(); }
            public void Dispose() => archive.Dispose();
        }
        private sealed class EmbeddedReader : IPackageReader
        {
            private const string Prefix = "DDFLanguageEditor.Core.LanguagePackages.Ddf.";
            private readonly Assembly assembly = typeof(DdfLanguagePackageLoader).Assembly;
            public IEnumerable<string> Files => assembly.GetManifestResourceNames().Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)).Select(name => name.Substring(Prefix.Length));
            public bool Exists(string file) => assembly.GetManifestResourceInfo(Prefix + file) != null;
            public string Read(string file) { using (Stream stream = assembly.GetManifestResourceStream(Prefix + file)) { if (stream == null) throw new FileNotFoundException(file); using (var reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd(); } }
            public void Dispose() { }
        }

        private class SectionDto { public string SchemaVersion { get; set; } public string Adapter { get; set; } public List<string> DependsOn { get; set; } public string Description { get; set; } }
        private sealed class GrammarDto : SectionDto { public string Notation { get; set; } public string EbnfFile { get; set; } public string StartRule { get; set; } public string ChoiceStrategy { get; set; } public List<GrammarNodeDto> Nodes { get; set; } }
        private sealed class GrammarNodeDto { public string Rule { get; set; } public string Kind { get; set; } public List<GrammarFieldDto> Fields { get; set; } public GrammarIdeDto Ide { get; set; } }
        private sealed class GrammarFieldDto { public string Name { get; set; } public string SourceRule { get; set; } public bool Multiple { get; set; } }
        private sealed class GrammarIdeDto { public string SymbolKind { get; set; } public string NameField { get; set; } public string NamePrefix { get; set; } public string NameSuffix { get; set; } public string Detail { get; set; } public string FoldingKind { get; set; } public string FoldingField { get; set; } public string BreadcrumbKind { get; set; } public string BreadcrumbLabel { get; set; } public string BreadcrumbSelectionField { get; set; } public int BreadcrumbSelectionLength { get; set; } public bool Selectable { get; set; } public string TypeField { get; set; } public string ReturnTypeField { get; set; } public string ParametersField { get; set; } public string ChildSymbolKind { get; set; } }
        private sealed class IntellisenseDto : SectionDto { public CompletionDto Completion { get; set; } public SignatureHelpDto SignatureHelp { get; set; } public List<SymbolPresentationDto> SymbolKinds { get; set; } public List<SnippetDto> Snippets { get; set; } }
        private sealed class CompletionDto { public string KeywordOrigin { get; set; } public string BooleanOrigin { get; set; } public string SnippetOrigin { get; set; } public string StandardLibraryOrigin { get; set; } public string CurrentDocumentOrigin { get; set; } public string WorkspaceOrigin { get; set; } public string TypeDetail { get; set; } public string ControlFlowDetail { get; set; } public string KeywordDetail { get; set; } public string BooleanDetail { get; set; } public string StandardFunctionDetailPrefix { get; set; } }
        private sealed class SignatureHelpDto { public string CurrentDocumentOrigin { get; set; } public string WorkspaceOrigin { get; set; } public string StandardLibraryOrigin { get; set; } }
        private sealed class SymbolPresentationDto { public string Kind { get; set; } public string Label { get; set; } public string Documentation { get; set; } }
        private sealed class SnippetDto { public string Prefix { get; set; } public string DisplayText { get; set; } public string Description { get; set; } public string Body { get; set; } public bool RequiresKeyword { get; set; } }
        private sealed class FormattingDto : SectionDto { public FormattingTokensDto Tokens { get; set; } public List<AutoPairDto> AutoPairs { get; set; } public List<string> IndentAfter { get; set; } public bool BlankLineBetweenStatements { get; set; } public List<QuickFixDto> QuickFixes { get; set; } }
        private sealed class FormattingTokensDto { public string BlockOpen { get; set; } public string BlockClose { get; set; } public string GroupOpen { get; set; } public string GroupClose { get; set; } public string IndexOpen { get; set; } public string IndexClose { get; set; } public string StatementTerminator { get; set; } public string Separator { get; set; } }
        private sealed class AutoPairDto { public string Open { get; set; } public string Close { get; set; } }
        private sealed class QuickFixDto { public string DiagnosticCode { get; set; } public string Title { get; set; } public string Replacement { get; set; } }
        private sealed class LexiconDto : SectionDto { public LexicalDto Lexical { get; set; } public List<KeywordDto> Keywords { get; set; } public List<string> BooleanLiterals { get; set; } public List<OperatorDto> Operators { get; set; } public List<string> Punctuation { get; set; } }
        private sealed class LexicalDto { public CharacterSetDto Whitespace { get; set; } public IdentifierDto Identifier { get; set; } public NumberDto Number { get; set; } public List<DelimitedTokenDto> DelimitedTokens { get; set; } public bool LongestMatch { get; set; } }
        private sealed class CharacterSetDto { public bool UnicodeLetters { get; set; } public bool UnicodeDigits { get; set; } public bool UnicodeWhitespace { get; set; } public string Characters { get; set; } }
        private sealed class IdentifierDto { public CharacterSetDto Start { get; set; } public CharacterSetDto Part { get; set; } }
        private sealed class NumberDto { public CharacterSetDto Digits { get; set; } public string DecimalSeparator { get; set; } public bool RequireFractionDigits { get; set; } }
        private sealed class DelimitedTokenDto { public string TokenKind { get; set; } public string Start { get; set; } public string End { get; set; } public bool EndsAtLineBreak { get; set; } public bool AllowLineBreak { get; set; } = true; public string EscapeCharacter { get; set; } public string UnterminatedCode { get; set; } public string UnterminatedMessage { get; set; } }
        private sealed class KeywordDto { public string Text { get; set; } public string TokenKind { get; set; } public string Role { get; set; } }
        private sealed class OperatorDto { public string Text { get; set; } public int BinaryPrecedence { get; set; } public string Associativity { get; set; } public int PrefixPrecedence { get; set; } public int PostfixPrecedence { get; set; } public string Role { get; set; } }
        private sealed class ManifestDto { public string SchemaVersion { get; set; } public string EngineApi { get; set; } public string Id { get; set; } public string Name { get; set; } public string PackageVersion { get; set; } public string LanguageVersion { get; set; } public List<string> FileExtensions { get; set; } public Dictionary<string, string> Sections { get; set; } }
    }
}
