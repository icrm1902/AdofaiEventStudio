using System;
using System.Collections.Generic;
using System.Globalization;

namespace AdofaiEventStudio
{
    internal enum ExpressionTokenType
    {
        Number,
        Identifier,
        Plus,
        Minus,
        Star,
        Slash,
        Percent,
        Caret,
        LeftParen,
        RightParen,
        Comma,
        End
    }

    internal sealed class ExpressionToken
    {
        public ExpressionTokenType Type;
        public string Text;
        public double Number;

        internal ExpressionToken(ExpressionTokenType type, string text)
        {
            Type = type;
            Text = text;
        }
    }

    internal static class NumericExpression
    {
        private static readonly Dictionary<string, Func<double, double>> UnaryFunctions =
            new Dictionary<string, Func<double, double>>(StringComparer.OrdinalIgnoreCase)
            {
                { "sin", x => Math.Sin(x) },
                { "cos", x => Math.Cos(x) },
                { "tan", x => Math.Tan(x) },
                { "asin", x => Math.Asin(x) },
                { "acos", x => Math.Acos(x) },
                { "atan", x => Math.Atan(x) },
                { "abs", x => Math.Abs(x) },
                { "sqrt", x => Math.Sqrt(x) },
                { "exp", x => Math.Exp(x) },
                { "ln", x => Math.Log(x) },
                { "log", x => Math.Log10(x) },
                { "log2", x => Math.Log(x, 2.0) },
                { "floor", x => Math.Floor(x) },
                { "ceil", x => Math.Ceiling(x) },
                { "round", x => Math.Round(x) }
            };

        private static readonly Dictionary<string, Func<double, double, double>> BinaryFunctions =
            new Dictionary<string, Func<double, double, double>>(StringComparer.OrdinalIgnoreCase)
            {
                { "min", (a, b) => Math.Min(a, b) },
                { "max", (a, b) => Math.Max(a, b) },
                { "pow", (a, b) => Math.Pow(a, b) },
                { "mod", (a, b) => a % b },
                { "atan2", (a, b) => Math.Atan2(a, b) }
            };

        public static double Evaluate(
            string expression,
            double index,
            double total,
            double current)
        {
            List<ExpressionToken> tokens = Tokenize(expression);
            int position = 0;
            double result = ParseAddSubtract(tokens, ref position, index, total, current);
            if (tokens[position].Type != ExpressionTokenType.End)
                throw new ArgumentException("表达式存在多余内容: " + expression);
            return result;
        }

        private static List<ExpressionToken> Tokenize(string expression)
        {
            List<ExpressionToken> tokens = new List<ExpressionToken>();
            int position = 0;
            while (position < expression.Length)
            {
                char c = expression[position];
                if (char.IsWhiteSpace(c))
                {
                    position++;
                    continue;
                }

                if (char.IsDigit(c) || c == '.')
                {
                    int start = position;
                    while (position < expression.Length &&
                        (char.IsDigit(expression[position]) ||
                         expression[position] == '.'))
                    {
                        position++;
                    }
                    if (position < expression.Length &&
                        (expression[position] == 'e' || expression[position] == 'E'))
                    {
                        position++;
                        if (position < expression.Length &&
                            (expression[position] == '+' || expression[position] == '-'))
                        {
                            position++;
                        }
                        while (position < expression.Length &&
                            char.IsDigit(expression[position]))
                        {
                            position++;
                        }
                    }

                    string numberText = expression.Substring(start, position - start);
                    double number;
                    if (!double.TryParse(
                        numberText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out number))
                    {
                        throw new ArgumentException("数字格式无效: " + numberText);
                    }
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Number, numberText)
                    {
                        Number = number
                    });
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = position;
                    while (position < expression.Length &&
                        (char.IsLetterOrDigit(expression[position]) ||
                         expression[position] == '_'))
                    {
                        position++;
                    }
                    string identifier = expression.Substring(start, position - start);
                    tokens.Add(new ExpressionToken(ExpressionTokenType.Identifier, identifier));
                    continue;
                }

                switch (c)
                {
                    case '+':
                        tokens.Add(new ExpressionToken(ExpressionTokenType.Plus, "+"));
                        position++;
                        break;
                    case '-':
                        tokens.Add(new ExpressionToken(ExpressionTokenType.Minus, "-"));
                        position++;
                        break;
                    case '*':
                        tokens.Add(new ExpressionToken(ExpressionTokenType.Star, "*"));
                        position++;
                        break;
                    case '/':
                        tokens.Add(new ExpressionToken(ExpressionTokenType.Slash, "/"));
                        position++;
                        break;
                    case '%':
                        tokens.Add(new ExpressionToken(ExpressionTokenType.Percent, "%"));
                        position++;
                        break;
                    case '^':
                        tokens.Add(new ExpressionToken(ExpressionTokenType.Caret, "^"));
                        position++;
                        break;
                    case '(':
                        tokens.Add(new ExpressionToken(ExpressionTokenType.LeftParen, "("));
                        position++;
                        break;
                    case ')':
                        tokens.Add(new ExpressionToken(ExpressionTokenType.RightParen, ")"));
                        position++;
                        break;
                    case ',':
                        tokens.Add(new ExpressionToken(ExpressionTokenType.Comma, ","));
                        position++;
                        break;
                    default:
                        throw new ArgumentException("表达式包含无效字符: " + c);
                }
            }

            tokens.Add(new ExpressionToken(ExpressionTokenType.End, string.Empty));
            return tokens;
        }

        private static double ParseAddSubtract(
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            double value = ParseMultiplyDivide(tokens, ref position, index, total, current);
            while (tokens[position].Type == ExpressionTokenType.Plus ||
                tokens[position].Type == ExpressionTokenType.Minus)
            {
                ExpressionTokenType operation = tokens[position].Type;
                position++;
                double right = ParseMultiplyDivide(tokens, ref position, index, total, current);
                if (operation == ExpressionTokenType.Plus)
                    value += right;
                else
                    value -= right;
            }
            return value;
        }

        private static double ParseMultiplyDivide(
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            double value = ParseUnary(tokens, ref position, index, total, current);
            while (tokens[position].Type == ExpressionTokenType.Star ||
                tokens[position].Type == ExpressionTokenType.Slash ||
                tokens[position].Type == ExpressionTokenType.Percent)
            {
                ExpressionTokenType operation = tokens[position].Type;
                position++;
                double right = ParseUnary(tokens, ref position, index, total, current);
                if (operation == ExpressionTokenType.Star)
                    value *= right;
                else if (operation == ExpressionTokenType.Slash)
                    value /= right;
                else
                    value %= right;
            }
            return value;
        }

        private static double ParseUnary(
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            if (tokens[position].Type == ExpressionTokenType.Minus)
            {
                position++;
                return -ParseUnary(tokens, ref position, index, total, current);
            }
            if (tokens[position].Type == ExpressionTokenType.Plus)
            {
                position++;
                return ParseUnary(tokens, ref position, index, total, current);
            }
            return ParsePower(tokens, ref position, index, total, current);
        }

        private static double ParsePower(
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            double left = ParsePrimary(tokens, ref position, index, total, current);
            if (tokens[position].Type == ExpressionTokenType.Caret)
            {
                position++;
                double right = ParseUnary(tokens, ref position, index, total, current);
                return Math.Pow(left, right);
            }
            return left;
        }

        private static double ParsePrimary(
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            ExpressionToken token = tokens[position];
            if (token.Type == ExpressionTokenType.Number)
            {
                position++;
                return token.Number;
            }

            if (token.Type == ExpressionTokenType.LeftParen)
            {
                position++;
                double value = ParseAddSubtract(tokens, ref position, index, total, current);
                if (tokens[position].Type != ExpressionTokenType.RightParen)
                    throw new ArgumentException("缺少右括号。");
                position++;
                return value;
            }

            if (token.Type == ExpressionTokenType.Identifier)
            {
                position++;
                string name = token.Text;
                if (string.Equals(name, "pi", StringComparison.OrdinalIgnoreCase))
                    return Math.PI;
                if (string.Equals(name, "e", StringComparison.OrdinalIgnoreCase))
                    return Math.E;
                if (string.Equals(name, "i", StringComparison.OrdinalIgnoreCase))
                    return index;
                if (string.Equals(name, "n", StringComparison.OrdinalIgnoreCase))
                    return total;
                if (string.Equals(name, "v", StringComparison.OrdinalIgnoreCase))
                    return current;
                if (string.Equals(name, "random", StringComparison.OrdinalIgnoreCase))
                    return EvaluateRandom(tokens, ref position, index, total, current);
                if (string.Equals(name, "inc", StringComparison.OrdinalIgnoreCase))
                    return EvaluateInc(tokens, ref position, index, total, current);
                if (UnaryFunctions.ContainsKey(name))
                    return EvaluateUnaryFunction(name, tokens, ref position, index, total, current);
                if (BinaryFunctions.ContainsKey(name))
                    return EvaluateBinaryFunction(name, tokens, ref position, index, total, current);

                throw new ArgumentException("未知变量或函数: " + name);
            }

            throw new ArgumentException("表达式缺少数字、变量或函数。");
        }

        private static double EvaluateUnaryFunction(
            string name,
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            double argument = ParseFunctionArgument(tokens, ref position, index, total, current);
            return UnaryFunctions[name](argument);
        }

        private static double EvaluateBinaryFunction(
            string name,
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            double[] arguments = ParseTwoFunctionArguments(
                name,
                tokens,
                ref position,
                index,
                total,
                current);
            return BinaryFunctions[name](arguments[0], arguments[1]);
        }

        private static double EvaluateRandom(
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            double[] arguments = ParseTwoFunctionArguments(
                "random",
                tokens,
                ref position,
                index,
                total,
                current);
            return UnityEngine.Random.Range((float)arguments[0], (float)arguments[1]);
        }

        private static double EvaluateInc(
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            double[] arguments = ParseTwoFunctionArguments(
                "inc",
                tokens,
                ref position,
                index,
                total,
                current);
            return arguments[0] + index * arguments[1];
        }

        private static double[] ParseTwoFunctionArguments(
            string name,
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            if (tokens[position].Type != ExpressionTokenType.LeftParen)
                throw new ArgumentException("函数 " + name + " 后需要左括号。");
            position++;
            double first = ParseAddSubtract(tokens, ref position, index, total, current);
            if (tokens[position].Type != ExpressionTokenType.Comma)
                throw new ArgumentException("函数 " + name + " 需要两个参数。");
            position++;
            double second = ParseAddSubtract(tokens, ref position, index, total, current);
            if (tokens[position].Type != ExpressionTokenType.RightParen)
                throw new ArgumentException("缺少右括号。");
            position++;
            return new double[] { first, second };
        }

        private static double ParseFunctionArgument(
            List<ExpressionToken> tokens,
            ref int position,
            double index,
            double total,
            double current)
        {
            if (tokens[position].Type != ExpressionTokenType.LeftParen)
                throw new ArgumentException("函数后需要左括号。");
            position++;
            double value = ParseAddSubtract(tokens, ref position, index, total, current);
            if (tokens[position].Type != ExpressionTokenType.RightParen)
                throw new ArgumentException("缺少右括号。");
            position++;
            return value;
        }
    }
}
