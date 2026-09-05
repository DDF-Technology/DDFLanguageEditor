using System;
using System.Collections.Generic;
using System.Linq;

namespace DDF___Program_Language_Editor
{
    public sealed class EditorPerformanceBudget
    {
        internal EditorPerformanceBudget(string id, string displayName, double targetP95Milliseconds,
            double smokeCeilingP95Milliseconds, string workload)
        {
            Id = id;
            DisplayName = displayName;
            TargetP95Milliseconds = targetP95Milliseconds;
            SmokeCeilingP95Milliseconds = smokeCeilingP95Milliseconds;
            Workload = workload;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public double TargetP95Milliseconds { get; }
        public double SmokeCeilingP95Milliseconds { get; }
        public string Workload { get; }

        public bool IsWithinSmokeCeiling(IEnumerable<double> samples)
        {
            return Percentile95(samples) <= SmokeCeilingP95Milliseconds;
        }

        public static double Percentile95(IEnumerable<double> samples)
        {
            double[] ordered = (samples ?? Enumerable.Empty<double>()).OrderBy(value => value).ToArray();
            if (ordered.Length == 0) throw new ArgumentException("At least one performance sample is required.", nameof(samples));
            int index = Math.Max(0, (int)Math.Ceiling(ordered.Length * 0.95D) - 1);
            return ordered[index];
        }
    }

    public static class EditorPerformanceBudgets
    {
        public const int ReferenceDocumentLines = 500;
        public const int InteractiveSampleCount = 30;
        public const int RealisticDocumentLines = 2000;
        public const int RealisticWorkspaceDocuments = 40;
        public const int RealisticWorkspaceLinesPerDocument = 100;
        public const int RealisticPackageKeywords = 256;
        public const int RealisticPackageSnippets = 96;
        public const int RealisticPackageSwitches = 6;
        public const int EnduranceIterations = 160;
        public const double EnduranceP95CeilingMilliseconds = 750D;
        public const long EnduranceManagedGrowthBytes = 64L * 1024L * 1024L;
        public const int EnduranceHandleGrowth = 24;
        public const int EnduranceGuiResourceGrowth = 16;

        public static readonly EditorPerformanceBudget Input = new EditorPerformanceBudget(
            "input", "Input editor", 16D, 120D,
            "30 inserimenti consecutivi in un documento DDF di 500 righe");

        public static readonly EditorPerformanceBudget Selection = new EditorPerformanceBudget(
            "selection", "Selezione/cursore", 16D, 120D,
            "30 spostamenti di selezione distribuiti in un documento DDF di 500 righe");

        public static readonly EditorPerformanceBudget Scroll = new EditorPerformanceBudget(
            "scroll", "Scroll e gutter", 16D, 180D,
            "30 aggiornamenti di viewport e numeri di riga in un documento DDF di 500 righe");

        public static readonly EditorPerformanceBudget Highlighting = new EditorPerformanceBudget(
            "highlighting", "Colorazione completa", 250D, 5000D,
            "analisi, diagnostiche, outline e colorazione di un documento DDF di 500 righe");

        public static readonly EditorPerformanceBudget Popup = new EditorPerformanceBudget(
            "popup", "Apertura popup", 100D, 1000D,
            "creazione, layout e prima visualizzazione della Command Palette completa");

        public static readonly EditorPerformanceBudget BackgroundAnalysisDispatch = new EditorPerformanceBudget(
            "analysis-dispatch", "Dispatch analisi grande documento", 16D, 120D,
            "avvio cancellabile dell'analisi di un documento DDF di 2000 righe");

        public static readonly EditorPerformanceBudget WorkspaceLoadDispatch = new EditorPerformanceBudget(
            "workspace-dispatch", "Dispatch caricamento workspace", 50D, 250D,
            "avvio e sostituzione del caricamento di 40 file DDF da 100 righe");

        public static readonly EditorPerformanceBudget LargeWorkspaceInput = new EditorPerformanceBudget(
            "large-workspace-input", "Input documento/workspace realistico", 16D, 120D,
            "20 inserimenti in 2000 righe con un workspace indicizzato di 40 file");

        public static readonly EditorPerformanceBudget LanguagePackageSwitch = new EditorPerformanceBudget(
            "language-package-switch", "Cambio pacchetto linguaggio realistico", 500D, 5000D,
            "6 attivazioni con 256 keyword, 96 snippet, documento da 2000 righe e workspace da 40 file");

        public static IReadOnlyList<EditorPerformanceBudget> All { get; } = new[]
        {
            Input,
            Selection,
            Scroll,
            Highlighting,
            Popup,
            BackgroundAnalysisDispatch,
            WorkspaceLoadDispatch,
            LargeWorkspaceInput,
            LanguagePackageSwitch
        };
    }
}
