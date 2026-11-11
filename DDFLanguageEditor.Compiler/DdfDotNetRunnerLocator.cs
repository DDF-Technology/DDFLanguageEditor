using System;
using System.IO;

namespace DDFLanguageEditor.Compiler
{
    /// <summary>One runner resolution policy for standalone build and debug.</summary>
    internal static class DdfDotNetRunnerLocator
    {
        internal static string Resolve(string configured)
        {
            if (!string.IsNullOrWhiteSpace(configured) &&
                File.Exists(Path.GetFullPath(configured)))
                return Path.GetFullPath(configured);

            string environment = Environment.GetEnvironmentVariable(
                "DDF_RUNNER_PATH");
            if (!string.IsNullOrWhiteSpace(environment) &&
                File.Exists(Path.GetFullPath(environment)))
                return Path.GetFullPath(environment);

            string packaged = Path.Combine(AppContext.BaseDirectory,
                "support", "runner", "DDFLanguageEditor.Runner.exe");
            if (File.Exists(packaged)) return packaged;

            DirectoryInfo directory = new(AppContext.BaseDirectory);
            for (int depth = 0; directory != null && depth < 7;
                 depth++, directory = directory.Parent)
            foreach (string configuration in new[] { "Release", "Debug" })
            {
                string candidate = Path.Combine(directory.FullName,
                    "DDFLanguageEditor.Runner", "bin", configuration,
                    "DDFLanguageEditor.Runner.dll");
                if (File.Exists(candidate)) return candidate;
            }

            string beside = Path.Combine(AppContext.BaseDirectory,
                "DDFLanguageEditor.Runner.dll");
            if (File.Exists(beside)) return beside;
            throw new FileNotFoundException(
                "Worker DDF standalone non trovato.");
        }
    }
}
