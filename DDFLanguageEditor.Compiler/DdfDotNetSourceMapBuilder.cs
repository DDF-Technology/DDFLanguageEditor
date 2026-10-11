using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DDFLanguageEditor.Core;
using Microsoft.CodeAnalysis;

namespace DDFLanguageEditor.Compiler
{
    internal static class DdfDotNetSourceMapBuilder
    {
        internal static DdfDotNetSourceMap Create(
            DdfDotNetCompilationWorkItem workItem,
            IEnumerable<SyntaxTree> syntaxTrees)
        {
            if (workItem == null) throw new ArgumentNullException(nameof(workItem));
            var trees = (syntaxTrees ?? throw new ArgumentNullException(
                    nameof(syntaxTrees))).ToList();
            if (trees.Count != workItem.Unit.Sources.Count)
                throw new ArgumentException(
                    "Gli alberi generati non corrispondono ai sorgenti DDF.",
                    nameof(syntaxTrees));
            var map = new DdfDotNetSourceMap
            {
                CompilationFingerprint = workItem.Unit.Fingerprint,
                LanguageProfileId = workItem.Unit.LanguageId,
                LanguageProfileVersion = workItem.Unit.LanguagePackageVersion
            };
            for (int index = 0; index < trees.Count; index++)
            {
                DdfCompilationSourceSnapshot source = workItem.Unit.Sources[index];
                SyntaxTree tree = trees[index];
                var anchors = new List<Anchor>();
                foreach (SyntaxNode node in tree.GetRoot().GetAnnotatedNodes(
                             DdfDotNetSyntaxTreeGenerator.SourceSpanAnnotationKind))
                {
                    SyntaxAnnotation annotation = node.GetAnnotations(
                            DdfDotNetSyntaxTreeGenerator.SourceSpanAnnotationKind)
                        .FirstOrDefault();
                    if (!TryParse(annotation?.Data, out int start, out int length) ||
                        start < 0 || start >= source.SourceText.Length)
                        continue;
                    FileLinePositionSpan line = node.GetLocation().GetLineSpan();
                    anchors.Add(new Anchor(start, length,
                        line.StartLinePosition.Line + 1,
                        line.StartLinePosition.Character + 1,
                        line.EndLinePosition.Line + 1,
                        line.EndLinePosition.Character + 1));
                }
                List<Anchor> ordered = anchors
                    .OrderBy(item => item.Start)
                    .ThenBy(item => item.GeneratedStartLine)
                    .ThenBy(item => item.GeneratedStartColumn)
                    .GroupBy(item => item.Start)
                    .Select(group => group.First())
                    .ToList();
                var points = new List<DdfDotNetSequencePoint>();
                for (int pointIndex = 0; pointIndex < ordered.Count; pointIndex++)
                {
                    Anchor anchor = ordered[pointIndex];
                    int available = source.SourceText.Length - anchor.Start;
                    if (pointIndex + 1 < ordered.Count)
                        available = Math.Min(available,
                            ordered[pointIndex + 1].Start - anchor.Start);
                    int length = Math.Max(1, Math.Min(anchor.Length, available));
                    points.Add(new DdfDotNetSequencePoint
                    {
                        DdfStart = anchor.Start,
                        DdfLength = length,
                        GeneratedStartLine = anchor.GeneratedStartLine,
                        GeneratedStartColumn = anchor.GeneratedStartColumn,
                        GeneratedEndLine = anchor.GeneratedEndLine,
                        GeneratedEndColumn = anchor.GeneratedEndColumn
                    });
                }
                map.Documents.Add(new DdfDotNetSourceMapDocument
                {
                    SourcePath = source.RelativePath,
                    SourceSha256 = source.Sha256,
                    GeneratedPath = tree.FilePath.Replace('\\', '/'),
                    SequencePoints = points
                });
            }
            ISet<string> sourcePaths = workItem.Unit.Sources.Select(item =>
                item.RelativePath).ToHashSet(StringComparer.Ordinal);
            foreach (DdfBoundSymbol symbol in workItem.BoundUnit.Symbols.Where(item =>
                         item.ModuleIdentity == workItem.Identity &&
                         sourcePaths.Contains(item.RelativePath) &&
                         item.Length > 0))
            {
                map.Symbols.Add(new DdfDotNetDebugSymbol
                {
                    SymbolId = symbol.Id,
                    Name = symbol.Name,
                    Kind = symbol.Kind.ToString(),
                    Type = symbol.Type.DisplayName,
                    SourcePath = symbol.RelativePath,
                    DdfStart = symbol.Start,
                    DdfLength = symbol.Length,
                    StorageName = workItem.GeneratedNames.GetIdentifier(symbol.Id),
                    IsWritable = symbol.IsWritable
                });
            }
            return map;
        }

        private static bool TryParse(string value, out int start, out int length)
        {
            start = 0;
            length = 0;
            string[] parts = (value ?? string.Empty).Split(':');
            return parts.Length == 2 && int.TryParse(parts[0],
                       NumberStyles.None, CultureInfo.InvariantCulture, out start) &&
                   int.TryParse(parts[1], NumberStyles.None,
                       CultureInfo.InvariantCulture, out length) && length > 0;
        }

        private sealed class Anchor
        {
            internal Anchor(int start, int length, int generatedStartLine,
                int generatedStartColumn, int generatedEndLine,
                int generatedEndColumn)
            {
                Start = start;
                Length = length;
                GeneratedStartLine = generatedStartLine;
                GeneratedStartColumn = generatedStartColumn;
                GeneratedEndLine = generatedEndLine;
                GeneratedEndColumn = generatedEndColumn;
            }

            internal int Start { get; }
            internal int Length { get; }
            internal int GeneratedStartLine { get; }
            internal int GeneratedStartColumn { get; }
            internal int GeneratedEndLine { get; }
            internal int GeneratedEndColumn { get; }
        }
    }
}
