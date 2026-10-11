using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Compiler
{
    /// <summary>
    /// Compares the observable DDF state of one VM pause with one compiled
    /// pause. Instruction counters, CLR storage and backend implementation
    /// details are deliberately excluded from the contract.
    /// </summary>
    public static class DdfDotNetDifferentialDebugTrace
    {
        public static IReadOnlyList<string> Compare(DdfDebugPauseInfo vm,
            DdfDotNetCompiledPause compiled,
            IEnumerable<DdfDotNetSourceMap> sourceMaps)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            if (compiled == null) throw new ArgumentNullException(nameof(compiled));
            if (sourceMaps == null) throw new ArgumentNullException(nameof(sourceMaps));
            var differences = new List<string>();
            var documents = sourceMaps.Where(map => map != null)
                .SelectMany(map => map.Documents ??
                    new List<DdfDotNetSourceMapDocument>())
                .Select(document => Normalize(document.SourcePath))
                .Distinct(StringComparer.Ordinal).ToList();
            CompareValue("pause.path", ResolveVmPath(vm.DocumentPath, documents),
                Normalize(compiled.Location.DocumentPath), differences);
            CompareValue("pause.start", vm.Start.ToString(),
                compiled.Location.Start.ToString(), differences);
            IReadOnlyList<DdfRuntimeStackFrame> vmFrames = vm.State?.CallStack ??
                Array.Empty<DdfRuntimeStackFrame>();
            CompareValue("stack.count", vmFrames.Count.ToString(),
                compiled.Frames.Count.ToString(), differences);
            int frameCount = Math.Min(vmFrames.Count, compiled.Frames.Count);
            for (int index = 0; index < frameCount; index++)
            {
                DdfRuntimeStackFrame expected = vmFrames[index];
                DdfDotNetCompiledFrame actual = compiled.Frames[index];
                string prefix = "stack[" + index + "]";
                CompareValue(prefix + ".function", expected.FunctionName,
                    actual.Name, differences);
                CompareValue(prefix + ".path",
                    ResolveVmPath(expected.DocumentPath, documents),
                    Normalize(actual.Location.DocumentPath), differences);
                CompareValue(prefix + ".start", expected.Start.ToString(),
                    actual.Location.Start.ToString(), differences);
            }
            IReadOnlyList<DdfRuntimeVariableSnapshot> vmVariables =
                vm.State?.Variables ?? Array.Empty<DdfRuntimeVariableSnapshot>();
            IReadOnlyList<DdfDotNetCompiledVariable> compiledVariables =
                compiled.Frames.FirstOrDefault()?.Variables ??
                Array.Empty<DdfDotNetCompiledVariable>();
            IReadOnlyDictionary<string, string> expectedVariables =
                FlattenVm(vmVariables);
            IReadOnlyDictionary<string, string> actualVariables =
                FlattenCompiled(compiledVariables);
            foreach (string key in expectedVariables.Keys.Union(
                         actualVariables.Keys, StringComparer.Ordinal)
                     .OrderBy(key => key, StringComparer.Ordinal))
            {
                expectedVariables.TryGetValue(key, out string expected);
                actualVariables.TryGetValue(key, out string actual);
                CompareValue("variables." + key, expected, actual, differences);
            }
            return differences.AsReadOnly();
        }

        private static IReadOnlyDictionary<string, string> FlattenVm(
            IEnumerable<DdfRuntimeVariableSnapshot> variables)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DdfRuntimeVariableSnapshot variable in variables)
                FlattenVm(variable, variable.Name, result);
            return result;
        }

        private static void FlattenVm(DdfRuntimeVariableSnapshot variable,
            string path, IDictionary<string, string> result)
        {
            result[path] = Describe(variable.TypeName, variable.Kind.ToString(),
                variable.DisplayValue, variable.Children.Count,
                variable.IsTruncated);
            foreach (DdfRuntimeVariableSnapshot child in variable.Children)
                FlattenVm(child, ChildPath(path, child.Name, child.Kind.ToString()),
                    result);
        }

        private static IReadOnlyDictionary<string, string> FlattenCompiled(
            IEnumerable<DdfDotNetCompiledVariable> variables)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DdfDotNetCompiledVariable variable in variables)
                FlattenCompiled(variable, variable.Name, result);
            return result;
        }

        private static void FlattenCompiled(DdfDotNetCompiledVariable variable,
            string path, IDictionary<string, string> result)
        {
            result[path] = Describe(variable.TypeName, variable.Kind.ToString(),
                variable.DisplayValue, variable.Children.Count,
                variable.IsTruncated);
            foreach (DdfDotNetCompiledVariable child in variable.Children)
                FlattenCompiled(child, ChildPath(path, child.Name,
                    child.Kind.ToString()), result);
        }

        private static string ChildPath(string parent, string name, string kind)
        {
            if (kind == "Element")
            {
                int bracket = (name ?? string.Empty).IndexOf('[',
                    StringComparison.Ordinal);
                return parent + (bracket >= 0 ? name.Substring(bracket) : name);
            }
            return parent + "." + name;
        }

        private static string Describe(string type, string kind, string display,
            int children, bool truncated)
        {
            string normalizedDisplay = children > 0 && display != null &&
                display.StartsWith("{", StringComparison.Ordinal)
                    ? "{}" : display ?? string.Empty;
            return (type ?? string.Empty) + "|" + (kind ?? string.Empty) + "|" +
                normalizedDisplay + "|children=" + children + "|truncated=" +
                (truncated ? "true" : "false");
        }

        private static string ResolveVmPath(string path,
            IReadOnlyList<string> documents)
        {
            string normalized = Normalize(path);
            List<string> matches = documents.Where(document =>
                    normalized == document || normalized.EndsWith("/" + document,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
            return matches.Count == 1 ? matches[0] : normalized;
        }

        private static string Normalize(string path) =>
            (path ?? string.Empty).Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

        private static void CompareValue(string path, string expected,
            string actual, ICollection<string> differences)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                differences.Add(path + ": VM=<" + (expected ?? "missing") +
                    "> Roslyn=<" + (actual ?? "missing") + ">");
        }
    }
}
