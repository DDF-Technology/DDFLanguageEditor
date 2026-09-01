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

        public static IReadOnlyList<EditorPerformanceBudget> All { get; } = new[]
        {
            Input,
            Selection,
            Scroll,
            Highlighting,
            Popup
        };
    }
}
