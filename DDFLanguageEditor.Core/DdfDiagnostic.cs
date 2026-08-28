using System;
using System.Globalization;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfDiagnostic
    {
        public DdfDiagnostic(string code, string message, int start, int length, int line, int column)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A diagnostic code is required.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("A diagnostic message is required.", nameof(message));
            }

            if (start < 0 || length <= 0 || line <= 0 || column <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            Code = code;
            Message = message;
            Start = start;
            Length = length;
            Line = line;
            Column = column;
        }

        public string Code { get; }

        public string Message { get; }

        public int Start { get; }

        public int Length { get; }

        public int End => Start + Length;

        public int Line { get; }

        public int Column { get; }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "Riga {0}, colonna {1} — {2}: {3}",
                Line,
                Column,
                Code,
                Message);
        }
    }
}
