using System.Globalization;

namespace KillerTools.Engine.Math;

public static class BoundedMath
{
    private static readonly HashSet<string> Functions = ["sqrt", "abs", "sin", "cos", "tan", "log", "log10", "exp", "floor", "ceil", "round", "min", "max"];

    public static double Evaluate(string expression)
    {
        var parser = new Parser(expression);
        var root = parser.Parse();
        var count = 0;
        Validate(root, 0, ref count);
        var value = EvaluateNode(root);
        if (!double.IsFinite(value) || System.Math.Abs(value) > 1_000_000_000_000)
        {
            throw new FormatException("Result outside numeric limit");
        }
        return value;
    }

    private static void Validate(Node node, int depth, ref int count)
    {
        count++;
        if (count > 64 || depth > 16) throw new FormatException("Expression too complex");
        if (node.Kind == NodeKind.Number && (!double.IsFinite(node.Value) || System.Math.Abs(node.Value) > 1_000_000)) throw new FormatException("Invalid number");
        if (node.Kind == NodeKind.Symbol && node.Name is not ("pi" or "e")) throw new FormatException("Unknown symbol");
        if (node.Kind == NodeKind.Function && (!Functions.Contains(node.Name!) || node.Children.Count > 5)) throw new FormatException("Unsupported function");
        if (node.Kind == NodeKind.Operator && node.Name == "^" && (node.Children[1].Kind != NodeKind.Number || node.Children[1].Value > 12)) throw new FormatException("Exponent too large");
        foreach (var child in node.Children) Validate(child, depth + 1, ref count);
    }

    private static double EvaluateNode(Node node) => node.Kind switch
    {
        NodeKind.Number => node.Value,
        NodeKind.Symbol => node.Name == "pi" ? System.Math.PI : System.Math.E,
        NodeKind.Parenthesis => EvaluateNode(node.Children[0]),
        NodeKind.Operator => EvaluateOperator(node),
        NodeKind.Function => EvaluateFunction(node.Name!, node.Children.Select(EvaluateNode).ToArray()),
        _ => throw new FormatException("Unsupported expression"),
    };

    private static double EvaluateOperator(Node node)
    {
        var left = EvaluateNode(node.Children[0]);
        if (node.Children.Count == 1) return node.Name == "-" ? -left : left;
        var right = EvaluateNode(node.Children[1]);
        return node.Name switch
        {
            "+" => left + right, "-" => left - right, "*" => left * right, "/" => left / right,
            "^" => System.Math.Pow(left, right), _ => throw new FormatException("Unsupported operator"),
        };
    }

    private static double EvaluateFunction(string name, double[] values) => name switch
    {
        "sqrt" when values.Length == 1 => System.Math.Sqrt(values[0]),
        "abs" when values.Length == 1 => System.Math.Abs(values[0]),
        "sin" when values.Length == 1 => System.Math.Sin(values[0]),
        "cos" when values.Length == 1 => System.Math.Cos(values[0]),
        "tan" when values.Length == 1 => System.Math.Tan(values[0]),
        "log" when values.Length == 1 => System.Math.Log(values[0]),
        "log" when values.Length == 2 => System.Math.Log(values[0], values[1]),
        "log10" when values.Length == 1 => System.Math.Log10(values[0]),
        "exp" when values.Length == 1 => System.Math.Exp(values[0]),
        "floor" when values.Length == 1 => System.Math.Floor(values[0]),
        "ceil" when values.Length == 1 => System.Math.Ceiling(values[0]),
        "round" when values.Length == 1 => System.Math.Floor(values[0] + 0.5),
        "min" when values.Length > 0 => values.Min(),
        "max" when values.Length > 0 => values.Max(),
        _ => throw new FormatException("Unsupported function"),
    };

    private enum NodeKind { Number, Symbol, Parenthesis, Operator, Function }
    private sealed record Node(NodeKind Kind, double Value = 0, string? Name = null, IReadOnlyList<Node>? Items = null)
    {
        public IReadOnlyList<Node> Children { get; } = Items ?? [];
    }

    private sealed class Parser(string text)
    {
        private int _index;

        public Node Parse()
        {
            var node = ParseExpression();
            SkipWhitespace();
            if (_index != text.Length) throw new FormatException("Unexpected input");
            return node;
        }

        private Node ParseExpression()
        {
            var node = ParseTerm();
            while (TryRead('+') || TryRead('-'))
            {
                var operation = text[_index - 1].ToString();
                node = new(NodeKind.Operator, Name: operation, Items: [node, ParseTerm()]);
            }
            return node;
        }

        private Node ParseTerm()
        {
            var node = ParseUnary();
            while (true)
            {
                if (TryRead('*') || TryRead('/'))
                {
                    var operation = text[_index - 1].ToString();
                    node = new(NodeKind.Operator, Name: operation, Items: [node, ParseUnary()]);
                    continue;
                }
                SkipWhitespace();
                if (_index < text.Length && (char.IsLetter(text[_index]) || text[_index] == '('))
                {
                    node = new(NodeKind.Operator, Name: "*", Items: [node, ParseUnary()]);
                    continue;
                }
                return node;
            }
        }

        private Node ParseUnary()
        {
            if (TryRead('+')) return new(NodeKind.Operator, Name: "+", Items: [ParseUnary()]);
            if (TryRead('-')) return new(NodeKind.Operator, Name: "-", Items: [ParseUnary()]);
            return ParsePower();
        }

        private Node ParsePower()
        {
            var node = ParsePrimary();
            if (TryRead('^')) node = new(NodeKind.Operator, Name: "^", Items: [node, ParseUnary()]);
            return node;
        }

        private Node ParsePrimary()
        {
            SkipWhitespace();
            if (TryRead('('))
            {
                var node = ParseExpression();
                Require(')');
                return new(NodeKind.Parenthesis, Items: [node]);
            }
            if (_index < text.Length && (char.IsDigit(text[_index]) || text[_index] == '.')) return ParseNumber();
            if (_index < text.Length && char.IsLetter(text[_index])) return ParseName();
            throw new FormatException("Expected value");
        }

        private Node ParseNumber()
        {
            var start = _index;
            while (_index < text.Length && (char.IsDigit(text[_index]) || text[_index] == '.')) _index++;
            if (_index < text.Length && text[_index] is 'e' or 'E')
            {
                _index++;
                if (_index < text.Length && text[_index] is '+' or '-') _index++;
                while (_index < text.Length && char.IsDigit(text[_index])) _index++;
            }
            if (!double.TryParse(text[start.._index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) throw new FormatException("Invalid number");
            return new(NodeKind.Number, value);
        }

        private Node ParseName()
        {
            var start = _index;
            while (_index < text.Length && (char.IsLetterOrDigit(text[_index]) || text[_index] == '_')) _index++;
            var name = text[start.._index];
            if (!TryRead('(')) return new(NodeKind.Symbol, Name: name);
            var arguments = new List<Node>();
            if (!TryRead(')'))
            {
                do { arguments.Add(ParseExpression()); } while (TryRead(','));
                Require(')');
            }
            return new(NodeKind.Function, Name: name, Items: arguments);
        }

        private bool TryRead(char value)
        {
            SkipWhitespace();
            if (_index >= text.Length || text[_index] != value) return false;
            _index++;
            return true;
        }

        private void Require(char value)
        {
            if (!TryRead(value)) throw new FormatException($"Expected {value}");
        }

        private void SkipWhitespace()
        {
            while (_index < text.Length && char.IsWhiteSpace(text[_index])) _index++;
        }
    }
}
