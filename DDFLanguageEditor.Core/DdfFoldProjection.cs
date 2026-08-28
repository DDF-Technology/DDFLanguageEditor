using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfFoldMarker
    {
        internal DdfFoldMarker(int sourceRangeStart, int sourceContentStart, int sourceContentLength,
            int projectedStart, int projectedLength, int hiddenLineCount)
        {
            SourceRangeStart = sourceRangeStart;
            SourceContentStart = sourceContentStart;
            SourceContentLength = sourceContentLength;
            ProjectedStart = projectedStart;
            ProjectedLength = projectedLength;
            HiddenLineCount = hiddenLineCount;
        }

        public int SourceRangeStart { get; }
        public int SourceContentStart { get; }
        public int SourceContentLength { get; }
        public int ProjectedStart { get; }
        public int ProjectedLength { get; }
        public int HiddenLineCount { get; }
    }

    public sealed class DdfFoldProjection
    {
        private DdfFoldProjection(string text, int sourceLength, IReadOnlyList<DdfFoldMarker> markers,
            IReadOnlyList<string> lineNumberLabels)
        {
            Text = text;
            SourceLength = sourceLength;
            Markers = markers ?? throw new ArgumentNullException(nameof(markers));
            LineNumberLabels = lineNumberLabels ?? throw new ArgumentNullException(nameof(lineNumberLabels));

            DdfFoldMarker first = markers.FirstOrDefault();
            SourceContentStart = first == null ? 0 : first.SourceContentStart;
            SourceContentLength = first == null ? 0 : first.SourceContentLength;
            MarkerStart = first == null ? 0 : first.ProjectedStart;
            MarkerLength = first == null ? 0 : first.ProjectedLength;
            HiddenLineCount = first == null ? 0 : first.HiddenLineCount;
        }

        public string Text { get; }
        public int SourceLength { get; }
        public int SourceContentStart { get; }
        public int SourceContentLength { get; }
        public int MarkerStart { get; }
        public int MarkerLength { get; }
        public int HiddenLineCount { get; }
        public IReadOnlyList<DdfFoldMarker> Markers { get; }
        public IReadOnlyList<string> LineNumberLabels { get; }

        public static DdfFoldProjection Create(string source, DdfFoldingRange range)
        {
            if (range == null) throw new ArgumentNullException(nameof(range));
            return Create(source, new[] { range });
        }

        public static DdfFoldProjection Create(string source, IReadOnlyList<DdfFoldingRange> ranges)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (ranges == null) throw new ArgumentNullException(nameof(ranges));

            List<DdfFoldingRange> normalized = NormalizeRanges(source, ranges);
            var markers = new List<DdfFoldMarker>();
            var projection = new StringBuilder(source.Length);
            int sourceCursor = 0;
            foreach (DdfFoldingRange range in normalized)
            {
                projection.Append(source, sourceCursor, range.ContentStart - sourceCursor);
                int hiddenLines = CountNewLines(source, range.ContentStart, range.ContentLength);
                string markerText = CreateMarkerText(source, range, hiddenLines);
                int projectedStart = projection.Length;
                projection.Append(markerText);
                markers.Add(new DdfFoldMarker(range.Start, range.ContentStart, range.ContentLength,
                    projectedStart, markerText.Length, hiddenLines));
                sourceCursor = range.ContentStart + range.ContentLength;
            }

            projection.Append(source, sourceCursor, source.Length - sourceCursor);
            string projectedText = projection.ToString();
            IReadOnlyList<DdfFoldMarker> readOnlyMarkers = markers.AsReadOnly();
            return new DdfFoldProjection(projectedText, source.Length, readOnlyMarkers,
                CreateLineNumberLabels(source, projectedText, readOnlyMarkers));
        }

        public bool TryProjectSpan(int sourceStart, int sourceLength, out int projectedStart, out int projectedLength)
        {
            projectedStart = 0;
            projectedLength = 0;
            if (sourceStart < 0 || sourceLength < 0 || sourceStart > SourceLength ||
                sourceLength > SourceLength - sourceStart) return false;

            int sourceEnd = sourceStart + sourceLength;
            int delta = 0;
            foreach (DdfFoldMarker marker in Markers)
            {
                int hiddenEnd = marker.SourceContentStart + marker.SourceContentLength;
                if (sourceEnd <= marker.SourceContentStart) break;
                if (sourceStart >= hiddenEnd)
                {
                    delta += marker.ProjectedLength - marker.SourceContentLength;
                    continue;
                }

                return false;
            }

            projectedStart = sourceStart + delta;
            projectedLength = sourceLength;
            return true;
        }

        public bool TryProjectPosition(int sourcePosition, out int projectedPosition)
        {
            return TryProjectSpan(sourcePosition, 0, out projectedPosition, out _);
        }

        public bool TryMapProjectedPosition(int projectedPosition, out int sourcePosition,
            out int markerSourceRangeStart)
        {
            sourcePosition = 0;
            markerSourceRangeStart = -1;
            if (projectedPosition < 0 || projectedPosition > Text.Length) return false;

            int delta = 0;
            foreach (DdfFoldMarker marker in Markers)
            {
                int markerEnd = marker.ProjectedStart + marker.ProjectedLength;
                if (projectedPosition < marker.ProjectedStart)
                {
                    sourcePosition = projectedPosition - delta;
                    return true;
                }

                if (projectedPosition < markerEnd)
                {
                    sourcePosition = marker.SourceContentStart;
                    markerSourceRangeStart = marker.SourceRangeStart;
                    return true;
                }

                delta += marker.ProjectedLength - marker.SourceContentLength;
            }

            sourcePosition = projectedPosition - delta;
            return sourcePosition >= 0 && sourcePosition <= SourceLength;
        }

        private static List<DdfFoldingRange> NormalizeRanges(string source, IReadOnlyList<DdfFoldingRange> ranges)
        {
            var result = new List<DdfFoldingRange>();
            int hiddenEnd = -1;
            foreach (DdfFoldingRange range in ranges.Where(range => range != null)
                .OrderBy(range => range.ContentStart).ThenByDescending(range => range.ContentLength))
            {
                if (range.ContentStart < 0 || range.ContentLength < 0 ||
                    range.ContentStart + range.ContentLength > source.Length)
                    throw new ArgumentOutOfRangeException(nameof(ranges));
                if (range.ContentStart < hiddenEnd) continue;

                result.Add(range);
                hiddenEnd = range.ContentStart + range.ContentLength;
            }

            return result;
        }

        private static string CreateMarkerText(string source, DdfFoldingRange range, int hiddenLines)
        {
            string indentation = GetLineIndentation(source, range.Start);
            string newLine = source.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            return newLine + indentation + "    ⋯ blocco compresso — " + Math.Max(1, hiddenLines) +
                   " righe ⋯" + newLine + indentation;
        }

        private static IReadOnlyList<string> CreateLineNumberLabels(string source, string projection,
            IReadOnlyList<DdfFoldMarker> markers)
        {
            var labels = new List<string>();
            int projectionLineStart = 0;
            while (true)
            {
                DdfFoldMarker marker = markers.FirstOrDefault(candidate =>
                    projectionLineStart > candidate.ProjectedStart &&
                    projectionLineStart < candidate.ProjectedStart + candidate.ProjectedLength);
                if (marker != null) labels.Add("⋯");
                else
                {
                    int sourcePosition = MapProjectedPosition(projectionLineStart, markers);
                    labels.Add(GetSourceLineNumber(source, Math.Min(sourcePosition, source.Length)).ToString());
                }

                int newLine = projection.IndexOf('\n', projectionLineStart);
                if (newLine < 0) break;
                projectionLineStart = newLine + 1;
            }

            return labels.AsReadOnly();
        }

        private static int MapProjectedPosition(int projectedPosition, IReadOnlyList<DdfFoldMarker> markers)
        {
            int delta = 0;
            foreach (DdfFoldMarker marker in markers)
            {
                if (projectedPosition < marker.ProjectedStart) break;
                if (projectedPosition < marker.ProjectedStart + marker.ProjectedLength)
                    return marker.SourceContentStart;
                delta += marker.ProjectedLength - marker.SourceContentLength;
            }

            return projectedPosition - delta;
        }

        private static int GetSourceLineNumber(string source, int position)
        {
            int line = 1;
            for (int index = 0; index < position; index++) if (source[index] == '\n') line++;
            return line;
        }

        private static int CountNewLines(string text, int start, int length)
        {
            int count = 0;
            int end = Math.Min(text.Length, start + length);
            for (int index = Math.Max(0, start); index < end; index++) if (text[index] == '\n') count++;
            return count;
        }

        private static string GetLineIndentation(string text, int position)
        {
            int lineStart = position;
            while (lineStart > 0 && text[lineStart - 1] != '\n') lineStart--;
            int end = lineStart;
            while (end < text.Length && (text[end] == ' ' || text[end] == '\t')) end++;
            return text.Substring(lineStart, end - lineStart);
        }
    }
}
