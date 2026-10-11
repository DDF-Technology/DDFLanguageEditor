using System;
using System.Globalization;

namespace DDFLanguageEditor.Runtime
{
    public static class DdfRuntimeNumeric
    {
        public static int Negate(int value, DdfRuntimeSourceLocation location) =>
            Checked(() => checked(-value), location);
        public static int Increment(int value, DdfRuntimeSourceLocation location) =>
            Checked(() => checked(value + 1), location);
        public static int Decrement(int value, DdfRuntimeSourceLocation location) =>
            Checked(() => checked(value - 1), location);
        public static int PostIncrement(ref int value,
            DdfRuntimeSourceLocation location)
        {
            int previous = value;
            value = Increment(value, location);
            return previous;
        }
        public static int PostDecrement(ref int value,
            DdfRuntimeSourceLocation location)
        {
            int previous = value;
            value = Decrement(value, location);
            return previous;
        }
        public static int Add(int left, int right, DdfRuntimeSourceLocation location) =>
            Checked(() => checked(left + right), location);
        public static int Subtract(int left, int right, DdfRuntimeSourceLocation location) =>
            Checked(() => checked(left - right), location);
        public static int Multiply(int left, int right, DdfRuntimeSourceLocation location) =>
            Checked(() => checked(left * right), location);

        public static int Divide(int left, int right, DdfRuntimeSourceLocation location)
        {
            if (right == 0) throw DdfRuntimeContext.Error("DDF404",
                "Divisione per zero.", location);
            return Checked(() => checked(left / right), location);
        }

        public static int Power(int value, int exponent,
            DdfRuntimeSourceLocation location)
        {
            if (exponent < 0) throw DdfRuntimeContext.Error("DDF419",
                "Una potenza intera richiede un esponente non negativo.", location);
            return Checked(() =>
            {
                int result = 1;
                int factor = value;
                int remaining = exponent;
                while (remaining > 0)
                {
                    if ((remaining & 1) != 0) result = checked(result * factor);
                    remaining >>= 1;
                    if (remaining > 0) factor = checked(factor * factor);
                }
                return result;
            }, location);
        }

        public static double Negate(double value, DdfRuntimeSourceLocation location) =>
            Finite(-value, location);
        public static double Increment(double value, DdfRuntimeSourceLocation location) =>
            Finite(value + 1d, location);
        public static double Decrement(double value, DdfRuntimeSourceLocation location) =>
            Finite(value - 1d, location);
        public static double PostIncrement(ref double value,
            DdfRuntimeSourceLocation location)
        {
            double previous = value;
            value = Increment(value, location);
            return previous;
        }
        public static double PostDecrement(ref double value,
            DdfRuntimeSourceLocation location)
        {
            double previous = value;
            value = Decrement(value, location);
            return previous;
        }
        public static double Add(double left, double right,
            DdfRuntimeSourceLocation location) => Finite(left + right, location);
        public static double Subtract(double left, double right,
            DdfRuntimeSourceLocation location) => Finite(left - right, location);
        public static double Multiply(double left, double right,
            DdfRuntimeSourceLocation location) => Finite(left * right, location);

        public static double Divide(double left, double right,
            DdfRuntimeSourceLocation location)
        {
            if (right == 0d) throw DdfRuntimeContext.Error("DDF404",
                "Divisione per zero.", location);
            return Finite(left / right, location);
        }

        public static double Power(double value, double exponent,
            DdfRuntimeSourceLocation location) =>
            Finite(Math.Pow(value, exponent), location);

        public static double Root(double value, double index,
            DdfRuntimeSourceLocation location)
        {
            if (index == 0d) throw DdfRuntimeContext.Error("DDF404",
                "Indice della radice uguale a zero.", location);
            return Finite(Math.Pow(value, 1d / index), location);
        }

        public static double Finite(double value, DdfRuntimeSourceLocation location)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw DdfRuntimeContext.Error("DDF418",
                    "Il risultato float non è finito.", location);
            return value;
        }

        public static int ToInt(string value, DdfRuntimeSourceLocation location)
        {
            if (int.TryParse(value?.Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int result)) return result;
            throw DdfRuntimeContext.Error("DDF408",
                "Conversione a int non valida.", location);
        }

        public static double ToFloat(string value, DdfRuntimeSourceLocation location)
        {
            if (double.TryParse(value?.Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double result))
                return Finite(result, location);
            throw DdfRuntimeContext.Error("DDF408",
                "Conversione a float non valida.", location);
        }

        public static int CheckedIndex(int index, int length,
            DdfRuntimeSourceLocation location)
        {
            if (index < 0 || index >= length)
                throw DdfRuntimeContext.Error("DDF405",
                    "Indice array fuori intervallo: " + index + ".", location);
            return index;
        }

        private static int Checked(Func<int> operation,
            DdfRuntimeSourceLocation location)
        {
            try { return operation(); }
            catch (OverflowException exception)
            {
                throw DdfRuntimeContext.Error("DDF417",
                    "Overflow dell'aritmetica int checked.", location, exception);
            }
        }
    }
}
