using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DDFLanguageEditor.Core
{
    public enum DdfEditorBuildConfiguration
    {
        Debug,
        Release
    }

    /// <summary>
    /// Richiesta backend-neutral per la produzione di artefatti compilati.
    /// L'implementazione appartiene esclusivamente allo standalone: Core e gli
    /// host embedded non assumono Roslyn, CLR emit o una specifica toolchain.
    /// </summary>
    public sealed class DdfEditorCompiledBuildRequest
    {
        public DdfEditorCompiledBuildRequest(DdfBuildResult preparation,
            DdfLanguagePackage languagePackage, string projectRoot,
            string outputRoot, DdfEditorBuildConfiguration configuration,
            string target, string cacheRoot = null)
        {
            Preparation = preparation ?? throw new ArgumentNullException(
                nameof(preparation));
            LanguagePackage = languagePackage ?? throw new ArgumentNullException(
                nameof(languagePackage));
            ProjectRoot = projectRoot ?? throw new ArgumentNullException(
                nameof(projectRoot));
            OutputRoot = outputRoot ?? throw new ArgumentNullException(
                nameof(outputRoot));
            Configuration = configuration;
            Target = target ?? string.Empty;
            CacheRoot = string.IsNullOrWhiteSpace(cacheRoot)
                ? Path.Combine(ProjectRoot, "obj", "ddf", "cache",
                    Configuration.ToString(), Target)
                : cacheRoot;
        }

        public DdfBuildResult Preparation { get; }
        public DdfLanguagePackage LanguagePackage { get; }
        public string ProjectRoot { get; }
        public string OutputRoot { get; }
        public string CacheRoot { get; }
        public DdfEditorBuildConfiguration Configuration { get; }
        public string Target { get; }
    }

    public sealed class DdfEditorCompiledBuildIssue
    {
        public DdfEditorCompiledBuildIssue(string code, string message,
            string sourcePath = null, int? line = null, int? column = null)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            Line = line;
            Column = column;
        }

        public string Code { get; }
        public string Message { get; }
        public string SourcePath { get; }
        public int? Line { get; }
        public int? Column { get; }
        public override string ToString() => Code + ": " + Message;
    }

    public sealed class DdfEditorCompiledBuildResult
    {
        public DdfEditorCompiledBuildResult(string outputRoot,
            string artifactDirectory, bool cacheHit,
            IEnumerable<DdfEditorCompiledBuildIssue> issues)
            : this(outputRoot, artifactDirectory, null, null, cacheHit, issues)
        {
        }

        public DdfEditorCompiledBuildResult(string outputRoot,
            string artifactDirectory, string cacheRoot,
            string cacheEntryDirectory, bool cacheHit,
            IEnumerable<DdfEditorCompiledBuildIssue> issues)
        {
            OutputRoot = outputRoot ?? string.Empty;
            ArtifactDirectory = artifactDirectory ?? string.Empty;
            CacheRoot = cacheRoot ?? string.Empty;
            CacheEntryDirectory = cacheEntryDirectory ?? string.Empty;
            CacheHit = cacheHit;
            Issues = (issues ?? Array.Empty<DdfEditorCompiledBuildIssue>())
                .ToList().AsReadOnly();
        }

        public string OutputRoot { get; }
        public string ArtifactDirectory { get; }
        public string CacheRoot { get; }
        public string CacheEntryDirectory { get; }
        public bool CacheHit { get; }
        public IReadOnlyList<DdfEditorCompiledBuildIssue> Issues { get; }
        public bool Success => Issues.Count == 0 &&
            !string.IsNullOrWhiteSpace(ArtifactDirectory);
    }

    public sealed class DdfEditorCompiledRunResult
    {
        public DdfEditorCompiledRunResult(string artifactDirectory,
            string resultJson, long instructions, bool ended,
            IEnumerable<string> output,
            IEnumerable<DdfEditorCompiledBuildIssue> issues)
        {
            ArtifactDirectory = artifactDirectory ?? string.Empty;
            ResultJson = resultJson ?? string.Empty;
            Instructions = instructions;
            Ended = ended;
            Output = (output ?? Array.Empty<string>()).ToList().AsReadOnly();
            Issues = (issues ?? Array.Empty<DdfEditorCompiledBuildIssue>())
                .ToList().AsReadOnly();
        }

        public string ArtifactDirectory { get; }
        public string ResultJson { get; }
        public long Instructions { get; }
        public bool Ended { get; }
        public IReadOnlyList<string> Output { get; }
        public IReadOnlyList<DdfEditorCompiledBuildIssue> Issues { get; }
        public bool Success => Issues.Count == 0;
    }

    public sealed class DdfEditorCompiledPublishRequest
    {
        public DdfEditorCompiledPublishRequest(string destination,
            string executableName, string target, string iconPath = null)
        {
            Destination = destination ?? throw new ArgumentNullException(
                nameof(destination));
            ExecutableName = executableName ?? throw new ArgumentNullException(
                nameof(executableName));
            Target = target ?? string.Empty;
            IconPath = iconPath ?? string.Empty;
        }

        public string Destination { get; }
        public string ExecutableName { get; }
        public string Target { get; }
        public string IconPath { get; }
    }

    public sealed class DdfEditorCompiledPublishResult
    {
        public DdfEditorCompiledPublishResult(string outputRoot,
            string executablePath, IEnumerable<string> publishedFiles,
            IEnumerable<DdfEditorCompiledBuildIssue> issues)
        {
            OutputRoot = outputRoot ?? string.Empty;
            ExecutablePath = executablePath ?? string.Empty;
            PublishedFiles = (publishedFiles ?? Array.Empty<string>())
                .ToList().AsReadOnly();
            Issues = (issues ?? Array.Empty<DdfEditorCompiledBuildIssue>())
                .ToList().AsReadOnly();
        }

        public string OutputRoot { get; }
        public string ExecutablePath { get; }
        public IReadOnlyList<string> PublishedFiles { get; }
        public IReadOnlyList<DdfEditorCompiledBuildIssue> Issues { get; }
        public bool Success => Issues.Count == 0 &&
            !string.IsNullOrWhiteSpace(ExecutablePath);
    }

    public interface IDdfEditorCompiledBuildService
    {
        bool HasRunnableBuild { get; }
        string LastArtifactDirectory { get; }

        DdfEditorCompiledBuildResult Build(
            DdfEditorCompiledBuildRequest request);

        Task<DdfEditorCompiledRunResult> RunLastBuildAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        DdfEditorCompiledPublishResult PublishLastBuild(
            DdfEditorCompiledPublishRequest request);
    }
}
