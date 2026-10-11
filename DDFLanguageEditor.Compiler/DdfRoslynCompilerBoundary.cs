using System;
using Microsoft.CodeAnalysis.CSharp;

namespace DDFLanguageEditor.Compiler
{
    /// <summary>
    /// Identifies the standalone-only Roslyn compiler boundary.
    /// Actual DDF-to-C# generation is introduced only after the compiled-unit
    /// and artifact contracts have been fixed.
    /// </summary>
    public static class DdfRoslynCompilerBoundary
    {
        public const string BackendId = "dotnet-roslyn";

        public static Version RoslynAssemblyVersion =>
            typeof(CSharpCompilation).Assembly.GetName().Version ?? new Version(0, 0);
    }
}
