using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DDFLanguageEditor.Core
{
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method |
        AttributeTargets.Property | AttributeTargets.Event |
        AttributeTargets.Class | AttributeTargets.Interface,
        AllowMultiple = false, Inherited = false)]
    public sealed class DdfEditorDeprecatedApiAttribute : Attribute
    {
        public DdfEditorDeprecatedApiAttribute(string apiId,
            string deprecatedInContractVersion,
            string earliestRemovalContractVersion,
            string replacementApiId, string migrationGuide)
        {
            ApiId = Require(apiId, nameof(apiId));
            DeprecatedInContractVersion = Require(deprecatedInContractVersion,
                nameof(deprecatedInContractVersion));
            EarliestRemovalContractVersion = Require(
                earliestRemovalContractVersion,
                nameof(earliestRemovalContractVersion));
            ReplacementApiId = Require(replacementApiId,
                nameof(replacementApiId));
            MigrationGuide = Require(migrationGuide, nameof(migrationGuide));
        }

        public string ApiId { get; }
        public string DeprecatedInContractVersion { get; }
        public string EarliestRemovalContractVersion { get; }
        public string ReplacementApiId { get; }
        public string MigrationGuide { get; }

        private static string Require(string value, string parameterName) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Il metadato lifecycle è obbligatorio.",
                    parameterName)
                : value;
    }

    public sealed class DdfEditorApiLifecycleEntry
    {
        public DdfEditorApiLifecycleEntry(string apiId,
            string deprecatedInContractVersion,
            string earliestRemovalContractVersion,
            string replacementApiId, string migrationGuide)
        {
            ApiId = apiId ?? throw new ArgumentNullException(nameof(apiId));
            DeprecatedInContractVersion = deprecatedInContractVersion ??
                throw new ArgumentNullException(nameof(deprecatedInContractVersion));
            EarliestRemovalContractVersion = earliestRemovalContractVersion ??
                throw new ArgumentNullException(nameof(earliestRemovalContractVersion));
            ReplacementApiId = replacementApiId ??
                throw new ArgumentNullException(nameof(replacementApiId));
            MigrationGuide = migrationGuide ??
                throw new ArgumentNullException(nameof(migrationGuide));
        }

        public string ApiId { get; }
        public string DeprecatedInContractVersion { get; }
        public string EarliestRemovalContractVersion { get; }
        public string ReplacementApiId { get; }
        public string MigrationGuide { get; }
    }

    /// <summary>
    /// Authoritative SDK beta lifecycle policy. Deprecation is metadata only:
    /// compatibility members remain callable and cannot disappear silently.
    /// </summary>
    public static class DdfEditorApiLifecycleCatalog
    {
        public const string PolicyVersion = "1.0";
        public const string EarliestBreakingContractVersion = "2.0";
        public const int MinimumDocumentedSuccessorContracts = 2;

        public const string ExecutionStopId =
            "DDFLanguageEditor.Core.IDdfEditorExecutionService.Stop()";
        public const string DebugStopId =
            "DDFLanguageEditor.Core.IDdfEditorDebugService.Stop()";
        public const string LegacyAdapterConstructorId =
            "DDFLanguageEditor.Core.DdfHostValueAdapter..ctor(Func<object,object>,Func<object,object>)";
        public const string LegacyPersistedStateConstructorId =
            "DDFLanguageEditor.WinForms.DdfEditorPersistedState..ctor(documents,int,preferences,debugging)";

        private static readonly IReadOnlyList<DdfEditorApiLifecycleEntry> entries =
            new ReadOnlyCollection<DdfEditorApiLifecycleEntry>(new[]
            {
                Entry(ExecutionStopId,
                    "DDFLanguageEditor.Core.IDdfEditorAsyncLifecycleService.StopAsync(TimeSpan,CancellationToken)",
                    "Negoziare editor.execution.async-lifecycle; attendere StopAsync e gestire TimedOut/DDFH005. Se assente, usare Stop come fallback legacy."),
                Entry(DebugStopId,
                    "DDFLanguageEditor.Core.IDdfEditorAsyncLifecycleService.StopAsync(TimeSpan,CancellationToken)",
                    "Negoziare editor.execution.async-lifecycle sul debugger; attendere StopAsync e mantenere Stop soltanto come fallback legacy."),
                Entry(LegacyAdapterConstructorId,
                    "DDFLanguageEditor.Core.DdfHostValueAdapter.Create<THost,TScript>(string,Func<THost,TScript>,Func<TScript,THost>)",
                    "Usare Create per scalari tipizzati oppure Enumeration<TEnum>; dichiarare sempre il tipo DDF nominale."),
                Entry(LegacyPersistedStateConstructorId,
                    "DDFLanguageEditor.WinForms.DdfEditorPersistedState..ctor(string,string,documents,int,preferences,debugging)",
                    "Scrivere CurrentSchemaVersion nel primo argomento e migrare lo snapshot prima di applicarlo alla superficie.")
            });

        public static IReadOnlyList<DdfEditorApiLifecycleEntry> Entries => entries;

        public static DdfEditorApiLifecycleEntry Find(string apiId) =>
            entries.FirstOrDefault(item => string.Equals(item.ApiId, apiId,
                StringComparison.Ordinal));

        private static DdfEditorApiLifecycleEntry Entry(string apiId,
            string replacementApiId, string migrationGuide) =>
            new DdfEditorApiLifecycleEntry(apiId, "0.41",
                EarliestBreakingContractVersion, replacementApiId,
                migrationGuide);
    }
}
