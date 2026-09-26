using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfLanguagePackageTestSuite
    {
        public string SchemaVersion { get; set; }
        public string Adapter { get; set; }
        public List<string> DependsOn { get; set; }
        public string Description { get; set; }
        public List<DdfLanguagePackageTestCase> Cases { get; set; }
    }

    public sealed class DdfLanguagePackageTestCase
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public List<string> ExpectedTokenKinds { get; set; }
        public List<string> ExpectedDiagnosticCodes { get; set; }
        public string ExpectedFormatted { get; set; }
        public string ExpectedReturn { get; set; }
    }

    public sealed class DdfLanguagePackageTestResult
    {
        internal DdfLanguagePackageTestResult(string name, bool passed, string message)
        { Name = name ?? string.Empty; Passed = passed; Message = message ?? string.Empty; }
        public string Name { get; }
        public bool Passed { get; }
        public string Message { get; }
    }

    public static class DdfLanguagePackageTestRunner
    {
        public static IReadOnlyList<DdfLanguagePackageTestResult> Run(
            DdfLanguagePackage package, DdfLanguagePackageTestSuite suite)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            var results = new List<DdfLanguagePackageTestResult>();
            var cases = suite?.Cases ?? new List<DdfLanguagePackageTestCase>();
            if (cases.Count == 0)
            {
                results.Add(new DdfLanguagePackageTestResult(string.Empty, false,
                    "Il pacchetto deve dichiarare almeno un test."));
                return results.AsReadOnly();
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (DdfLanguagePackageTestCase test in cases)
            {
                string name = test?.Name ?? string.Empty;
                if (test == null || string.IsNullOrWhiteSpace(name) || !names.Add(name) || test.Source == null)
                {
                    results.Add(new DdfLanguagePackageTestResult(name, false,
                        "Nome univoco e sorgente sono obbligatori."));
                    continue;
                }
                try { results.Add(RunCase(package, test)); }
                catch (Exception exception)
                {
                    results.Add(new DdfLanguagePackageTestResult(name, false,
                        "Eccezione durante il test: " + exception.Message));
                }
            }
            return results.AsReadOnly();
        }

        private static DdfLanguagePackageTestResult RunCase(DdfLanguagePackage package,
            DdfLanguagePackageTestCase test)
        {
            DdfLexResult lex = DdfLexer.Lex(test.Source, package.Language);
            if (test.ExpectedTokenKinds != null)
            {
                string[] actual = lex.Tokens.Select(item => item.Kind.ToString()).ToArray();
                if (!actual.SequenceEqual(test.ExpectedTokenKinds, StringComparer.Ordinal))
                    return Fail(test, "Token attesi: " + string.Join(", ", test.ExpectedTokenKinds) +
                        "; ottenuti: " + string.Join(", ", actual) + ".");
            }

            DdfParseResult parse = DdfParser.Parse(test.Source, package.Language);
            DdfTypeCheckResult types = DdfTypeChecker.Check(test.Source, parse.Root, null,
                package.Language.Types, package.Language.StandardLibrary, null,
                package.Language.Runtime);
            DdfIrBuildResult ir = DdfIrLowerer.Lower(test.Source, parse, package.Language);
            string[] diagnostics = lex.Diagnostics.Concat(parse.Diagnostics).Concat(types.Diagnostics)
                .Concat(ir.Diagnostics).Select(item => item.Code).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            string[] expectedDiagnostics = (test.ExpectedDiagnosticCodes ?? new List<string>())
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            if (!diagnostics.SequenceEqual(expectedDiagnostics, StringComparer.Ordinal))
                return Fail(test, "Diagnostiche attese: " + string.Join(", ", expectedDiagnostics) +
                    "; ottenute: " + string.Join(", ", diagnostics) + ".");

            if (test.ExpectedFormatted != null)
            {
                string actual = DdfFormatter.Format(test.Source, package.Language).Text.Replace("\r\n", "\n");
                if (actual != test.ExpectedFormatted.Replace("\r\n", "\n"))
                    return Fail(test, "La formattazione non coincide con il risultato dichiarato.");
            }

            if (test.ExpectedReturn != null)
            {
                if (!ir.Success) return Fail(test, "Il test runtime non produce una IR valida.");
                DdfExecutionResult execution = DdfVirtualMachine.Execute(test.Source, ir.Program,
                    package.Language.Runtime, package.Language.Types, package.Language.StandardLibrary,
                    new DdfExecutionOptions
                    {
                        EntryPoint = package.Language.Runtime.EntryPoint,
                        MaxInstructions = package.Language.Runtime.DefaultInstructionLimit
                    });
                string actual = Convert.ToString(execution.ReturnValue, CultureInfo.InvariantCulture) ?? string.Empty;
                if (!execution.Succeeded || actual != test.ExpectedReturn)
                    return Fail(test, "Risultato runtime atteso: " + test.ExpectedReturn +
                        "; ottenuto: " + actual + ".");
            }

            return new DdfLanguagePackageTestResult(test.Name, true, "OK");
        }

        private static DdfLanguagePackageTestResult Fail(DdfLanguagePackageTestCase test, string message) =>
            new DdfLanguagePackageTestResult(test.Name, false, message);
    }
}
