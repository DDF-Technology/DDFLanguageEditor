using System;

namespace DDFLanguageEditor.Core
{
    // Compatibility facade: parsing/lowering live here; the VM only consumes common IR.
    public static class DdfInterpreter
    {
        public static DdfExecutionResult Execute(string source, DdfExecutionOptions options = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            DdfLanguageDefinition language = DdfLanguageCatalog.Default;
            DdfParseResult parse = DdfParser.Parse(source, language);
            if (parse.Diagnostics.Count > 0)
                return Failed(parse.Diagnostics);
            DdfIrBuildResult lowered = DdfIrLowerer.Lower(source, parse, language);
            if (!lowered.Success) return Failed(lowered.Diagnostics);
            return Execute(source, lowered.Program, language, options);
        }

        public static DdfExecutionResult Execute(string source, CompilationUnitSyntax root,
            DdfExecutionOptions options = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (root == null) throw new ArgumentNullException(nameof(root));
            DdfLanguageDefinition language = DdfLanguageCatalog.Default;
            DdfIrBuildResult lowered = DdfIrLowerer.Lower(source, root, runtime: language.Runtime);
            if (!lowered.Success) return Failed(lowered.Diagnostics);
            return Execute(source, lowered.Program, language, options);
        }

        public static DdfExecutionResult Execute(string source, DdfIrProgram root,
            DdfExecutionOptions options = null) =>
            Execute(source, root, DdfLanguageCatalog.Default, options);

        public static DdfExecutionResult Execute(string source, DdfIrProgram root,
            DdfLanguageDefinition language, DdfExecutionOptions options = null)
        {
            if (language == null) throw new ArgumentNullException(nameof(language));
            options = options ?? new DdfExecutionOptions();
            if (string.IsNullOrWhiteSpace(options.EntryPoint)) options.EntryPoint = language.Runtime.EntryPoint;
            if (options.MaxInstructions <= 0) options.MaxInstructions = language.Runtime.DefaultInstructionLimit;
            return DdfVirtualMachine.Execute(source, root, language.Runtime, language.Types,
                language.StandardLibrary, options);
        }

        public static DdfExecutionResult Execute(System.Collections.Generic.IReadOnlyList<DdfIrModule> modules,
            DdfLanguageDefinition language, DdfExecutionOptions options = null)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            if (language == null) throw new ArgumentNullException(nameof(language));
            options = options ?? new DdfExecutionOptions();
            if (string.IsNullOrWhiteSpace(options.EntryPoint)) options.EntryPoint = language.Runtime.EntryPoint;
            if (options.MaxInstructions <= 0) options.MaxInstructions = language.Runtime.DefaultInstructionLimit;
            return DdfVirtualMachine.Execute(modules, language.Runtime, language.Types,
                language.StandardLibrary, options);
        }

        private static DdfExecutionResult Failed(System.Collections.Generic.IReadOnlyList<DdfDiagnostic> diagnostics) =>
            new DdfExecutionResult(null, 0, false, false, diagnostics, Array.Empty<DdfRuntimeStackFrame>());
    }
}
