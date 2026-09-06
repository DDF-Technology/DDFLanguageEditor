using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DDFLanguageEditor.Core
{
    public sealed class DdfWatchResult
    {
        internal DdfWatchResult(DdfRuntimeVariableSnapshot value, string message)
        { Value = value; Message = message; }
        public DdfRuntimeVariableSnapshot Value { get; }
        public string Message { get; }
        public bool Available => Value != null;
    }

    public static class DdfWatch
    {
        public static DdfWatchResult Evaluate(string expression, DdfRuntimeStateSnapshot state) =>
            Evaluate(expression, state, DdfLanguageCatalog.Capture().Language);

        // Parse with the active package, then evaluate only immutable snapshots. Calls,
        // assignments, output and postfix mutations never reach the runtime evaluator.
        public static DdfWatchResult Evaluate(string expression, DdfRuntimeStateSnapshot state,
            DdfLanguageDefinition language)
        {
            string source = (expression ?? string.Empty).Trim();
            if (source.Length == 0 || source.Length > 512)
                return Unavailable("Inserire un'espressione di massimo 512 caratteri.");
            if (state == null) return Unavailable("In attesa di una pausa del debugger.");
            if (language == null) return Unavailable("Definizione del linguaggio non disponibile.");

            DdfParseResult parse = DdfParser.Parse(source + ";", language);
            GlobalStatementSyntax global = parse.Root.Members.Count == 1
                ? parse.Root.Members[0] as GlobalStatementSyntax : null;
            ExpressionSyntax syntax = (global?.Statement as ExpressionStatementSyntax)?.Expression;
            if (parse.Diagnostics.Count > 0 || syntax == null || syntax is MissingExpressionSyntax)
                return Unavailable("Espressione non valida: " +
                    (parse.Diagnostics.FirstOrDefault()?.Message ?? "sintassi non riconosciuta."));

            try
            {
                ValidatePure(syntax, language, 0);
                WatchValue result = EvaluateSyntax(syntax, state, language, 0);
                return new DdfWatchResult(result.Snapshot ?? CreateSnapshot(result.Value, result.TypeName), "Disponibile");
            }
            catch (WatchEvaluationException exception)
            {
                return Unavailable(exception.Message);
            }
            catch (Exception exception) when (exception is FormatException || exception is OverflowException)
            {
                return Unavailable("Valore non rappresentabile nell'espressione.");
            }
        }

        private static void ValidatePure(ExpressionSyntax expression, DdfLanguageDefinition language, int depth)
        {
            if (depth > 64) throw Error("Espressione troppo complessa.");
            if (expression is CallExpressionSyntax)
                throw Error("Le chiamate di funzione non sono consentite nelle Watch.");
            if (expression is PostfixExpressionSyntax)
                throw Error("Gli operatori con effetti collaterali non sono consentiti nelle Watch.");
            if (expression is ParenthesizedExpressionSyntax group)
                ValidatePure(group.Expression, language, depth + 1);
            else if (expression is MemberAccessExpressionSyntax member)
                ValidatePure(member.Target, language, depth + 1);
            else if (expression is IndexExpressionSyntax index)
            {
                ValidatePure(index.Target, language, depth + 1);
                ValidatePure(index.Index, language, depth + 1);
            }
            else if (expression is UnaryExpressionSyntax unary)
                ValidatePure(unary.Operand, language, depth + 1);
            else if (expression is BinaryExpressionSyntax binary)
            {
                if (!language.Runtime.TryGetBinaryOperation(binary.OperatorText, out string operation) ||
                    operation == DdfVmExpressionOperations.Assign || operation == DdfVmExpressionOperations.Output)
                    throw Error("Assegnazioni, output ed effetti collaterali non sono consentiti nelle Watch.");
                ValidatePure(binary.Left, language, depth + 1);
                ValidatePure(binary.Right, language, depth + 1);
            }
        }

        private static WatchValue EvaluateSyntax(ExpressionSyntax expression, DdfRuntimeStateSnapshot state,
            DdfLanguageDefinition language, int depth)
        {
            if (depth > 64) throw Error("Espressione troppo complessa.");
            if (expression is NameExpressionSyntax name)
            {
                DdfRuntimeVariableSnapshot variable = state.Variables.FirstOrDefault(item => item.Name == name.Name);
                if (variable == null) throw Error("Variabile non disponibile nello scope corrente.");
                return FromSnapshot(variable);
            }
            if (expression is LiteralExpressionSyntax literal) return ParseLiteral(literal.Text, language);
            if (expression is ParenthesizedExpressionSyntax group)
                return EvaluateSyntax(group.Expression, state, language, depth + 1);
            if (expression is MemberAccessExpressionSyntax member)
            {
                WatchValue target = EvaluateSyntax(member.Target, state, language, depth + 1);
                DdfRuntimeVariableSnapshot child = target.Snapshot?.Children.FirstOrDefault(item => item.Name == member.Name);
                if (child == null) throw Error(target.Snapshot?.IsTruncated == true
                    ? "Limite dello snapshot raggiunto." : "Campo non disponibile: '" + member.Name + "'.");
                return FromSnapshot(child);
            }
            if (expression is IndexExpressionSyntax index)
            {
                WatchValue target = EvaluateSyntax(index.Target, state, language, depth + 1);
                int offset = ToInteger(EvaluateSyntax(index.Index, state, language, depth + 1));
                DdfRuntimeVariableSnapshot child = target.Snapshot?.Children.FirstOrDefault(item =>
                    item.Name == "[" + offset.ToString(CultureInfo.InvariantCulture) + "]");
                if (child == null) throw Error(target.Snapshot?.IsTruncated == true
                    ? "Limite dello snapshot raggiunto." : "Indice array fuori intervallo: " + offset + ".");
                return FromSnapshot(child);
            }
            if (expression is CallExpressionSyntax)
                throw Error("Le chiamate di funzione non sono consentite nelle Watch.");
            if (expression is PostfixExpressionSyntax)
                throw Error("Gli operatori con effetti collaterali non sono consentiti nelle Watch.");
            if (expression is UnaryExpressionSyntax unary)
            {
                if (!language.Runtime.TryGetUnaryOperation(unary.OperatorText, out string operation))
                    throw Error("Operatore unario non supportato: '" + unary.OperatorText + "'.");
                WatchValue operand = EvaluateSyntax(unary.Operand, state, language, depth + 1);
                if (operation == DdfVmExpressionOperations.LogicalNot)
                    return Scalar(!ToBoolean(operand), "bool");
                if (operation == DdfVmExpressionOperations.Positive)
                    return Scalar(ToNumeric(operand), NumericType(operand));
                if (operation == DdfVmExpressionOperations.Negate)
                {
                    object number = ToNumeric(operand);
                    return Scalar(number is double floating ? (object)-floating : -((int)number), NumericType(operand));
                }
                throw Error("Operatore unario non sicuro nelle Watch.");
            }
            if (expression is BinaryExpressionSyntax binary)
                return EvaluateBinary(binary, state, language, depth + 1);
            throw Error("Costrutto non supportato nelle Watch.");
        }

        private static WatchValue EvaluateBinary(BinaryExpressionSyntax binary, DdfRuntimeStateSnapshot state,
            DdfLanguageDefinition language, int depth)
        {
            if (!language.Runtime.TryGetBinaryOperation(binary.OperatorText, out string operation))
                throw Error("Operatore non supportato: '" + binary.OperatorText + "'.");
            if (operation == DdfVmExpressionOperations.Assign || operation == DdfVmExpressionOperations.Output)
                throw Error("Assegnazioni, output ed effetti collaterali non sono consentiti nelle Watch.");

            WatchValue left = EvaluateSyntax(binary.Left, state, language, depth);
            if (operation == DdfVmExpressionOperations.LogicalOr && ToBoolean(left)) return Scalar(true, "bool");
            if (operation == DdfVmExpressionOperations.LogicalAnd && !ToBoolean(left)) return Scalar(false, "bool");
            WatchValue right = EvaluateSyntax(binary.Right, state, language, depth);
            if (operation == DdfVmExpressionOperations.LogicalOr) return Scalar(ToBoolean(right), "bool");
            if (operation == DdfVmExpressionOperations.LogicalAnd) return Scalar(ToBoolean(right), "bool");
            if (operation == DdfVmExpressionOperations.LogicalXor)
                return Scalar(ToBoolean(left) ^ ToBoolean(right), "bool");
            if (operation == DdfVmExpressionOperations.Equal) return Scalar(AreEqual(left, right), "bool");
            if (operation == DdfVmExpressionOperations.NotEqual) return Scalar(!AreEqual(left, right), "bool");
            if (operation == DdfVmExpressionOperations.Less) return Compare(left, right, (a, b) => a < b);
            if (operation == DdfVmExpressionOperations.LessOrEqual) return Compare(left, right, (a, b) => a <= b);
            if (operation == DdfVmExpressionOperations.Greater) return Compare(left, right, (a, b) => a > b);
            if (operation == DdfVmExpressionOperations.GreaterOrEqual) return Compare(left, right, (a, b) => a >= b);
            if (operation == DdfVmExpressionOperations.BetweenExclusive)
                return Compare(left, right, (a, b) => a > b && a < b);
            if (operation == DdfVmExpressionOperations.OutsideExclusive)
                return Compare(left, right, (a, b) => a < b || a > b);
            if (operation == DdfVmExpressionOperations.Add &&
                (left.TypeName == "string" || right.TypeName == "string"))
                return Scalar(ToDisplay(left) + ToDisplay(right), "string");

            object leftNumber = ToNumeric(left), rightNumber = ToNumeric(right);
            double a = Convert.ToDouble(leftNumber, CultureInfo.InvariantCulture);
            double b = Convert.ToDouble(rightNumber, CultureInfo.InvariantCulture);
            string type = leftNumber is double || rightNumber is double ? "float" : "int";
            if (operation == DdfVmExpressionOperations.Divide && b == 0) throw Error("Divisione per zero.");
            if (operation == DdfVmExpressionOperations.Root && b == 0) throw Error("Indice della radice uguale a zero.");
            double value;
            if (operation == DdfVmExpressionOperations.Add) value = a + b;
            else if (operation == DdfVmExpressionOperations.Subtract) value = a - b;
            else if (operation == DdfVmExpressionOperations.Multiply) value = a * b;
            else if (operation == DdfVmExpressionOperations.Divide) value = a / b;
            else if (operation == DdfVmExpressionOperations.Power) value = Math.Pow(a, b);
            else if (operation == DdfVmExpressionOperations.Root) { value = Math.Pow(a, 1d / b); type = "float"; }
            else throw Error("Operatore non sicuro nelle Watch.");
            return Scalar(type == "float" ? (object)value : checked((int)value), type);
        }

        private static WatchValue Compare(WatchValue left, WatchValue right, Func<double, double, bool> comparison) =>
            Scalar(comparison(ToDouble(left), ToDouble(right)), "bool");

        private static bool AreEqual(WatchValue left, WatchValue right)
        {
            if (IsNumeric(left) && IsNumeric(right)) return ToDouble(left) == ToDouble(right);
            return Equals(left.Value, right.Value);
        }

        private static WatchValue ParseLiteral(string text, DdfLanguageDefinition language)
        {
            if (language.IsBooleanLiteral(text))
                return Scalar(string.Equals(text, "true", StringComparison.OrdinalIgnoreCase), "bool");
            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
                return Scalar(Unescape(text.Substring(1, text.Length - 2)), "string");
            string normalized = language.LexicalRules.Number.DecimalSeparator == "." ? text :
                text.Replace(language.LexicalRules.Number.DecimalSeparator, ".");
            if (normalized.IndexOf('.') >= 0)
                return Scalar(double.Parse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture), "float");
            return Scalar(int.Parse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture), "int");
        }

        private static WatchValue FromSnapshot(DdfRuntimeVariableSnapshot snapshot) =>
            new WatchValue(ParseSnapshotValue(snapshot), snapshot.TypeName, snapshot);

        private static object ParseSnapshotValue(DdfRuntimeVariableSnapshot snapshot)
        {
            string text = snapshot.DisplayValue ?? string.Empty;
            if (snapshot.TypeName == "int") return int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (snapshot.TypeName == "float") return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (snapshot.TypeName == "bool") return bool.Parse(text);
            if (snapshot.TypeName == "char") return text.Length == 0 ? '\0' : text[0];
            if (snapshot.TypeName == "string") return text;
            if (text == "null") return null;
            return snapshot;
        }

        private static WatchValue Scalar(object value, string typeName) => new WatchValue(value, typeName, null);

        private static DdfRuntimeVariableSnapshot CreateSnapshot(object value, string typeName) =>
            new DdfRuntimeVariableSnapshot(string.Empty, FormatValue(value), 0, DdfRuntimeVariableKind.Local)
            { TypeName = typeName };

        private static bool IsNumeric(WatchValue value) => value.Value is int || value.Value is double || value.Value is char;
        private static string NumericType(WatchValue value) => value.Value is double ? "float" : "int";
        private static object ToNumeric(WatchValue value)
        {
            if (value.Value is double || value.Value is int) return value.Value;
            if (value.Value is char character) return (int)character;
            throw Error("È richiesto un valore numerico.");
        }
        private static double ToDouble(WatchValue value) => Convert.ToDouble(ToNumeric(value), CultureInfo.InvariantCulture);
        private static int ToInteger(WatchValue value)
        {
            object number = ToNumeric(value);
            if (number is double floating && floating != Math.Truncate(floating))
                throw Error("L'indice deve essere un intero.");
            return checked(Convert.ToInt32(number, CultureInfo.InvariantCulture));
        }
        private static bool ToBoolean(WatchValue value)
        {
            if (value.Value is bool boolean) return boolean;
            throw Error("È richiesto un valore booleano.");
        }
        private static string ToDisplay(WatchValue value) => FormatValue(value.Value);
        private static string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is bool boolean) return boolean ? "true" : "false";
            if (value is double floating) return floating.ToString(CultureInfo.InvariantCulture);
            if (value is DdfRuntimeVariableSnapshot snapshot) return snapshot.DisplayValue;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string Unescape(string value)
        {
            var result = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] != '\\' || index + 1 >= value.Length) { result.Append(value[index]); continue; }
                char escaped = value[++index];
                result.Append(escaped == 'n' ? '\n' : escaped == 'r' ? '\r' : escaped == 't' ? '\t' : escaped);
            }
            return result.ToString();
        }

        private static DdfWatchResult Unavailable(string message) => new DdfWatchResult(null, message);
        private static WatchEvaluationException Error(string message) => new WatchEvaluationException(message);

        private sealed class WatchValue
        {
            public WatchValue(object value, string typeName, DdfRuntimeVariableSnapshot snapshot)
            { Value = value; TypeName = typeName ?? string.Empty; Snapshot = snapshot; }
            public object Value { get; }
            public string TypeName { get; }
            public DdfRuntimeVariableSnapshot Snapshot { get; }
        }

        private sealed class WatchEvaluationException : Exception
        {
            public WatchEvaluationException(string message) : base(message) { }
        }
    }
}
