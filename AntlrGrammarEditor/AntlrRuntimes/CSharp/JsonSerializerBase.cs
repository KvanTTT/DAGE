using System;
using System.Text;
using Antlr4.Runtime;

public abstract class JsonSerializerBase
{
    public const string TypeProperty = "Type";
    public const string IndexProperty = "Index";

    public Lexer Lexer { get; }

    public bool Format { get; set; }

    public int IndentSize { get; set; } = 2;

    public bool SymbolicNames { get; set; }

    public bool LineColumn { get; set; }

    public JsonSerializerBase(Lexer lexer)
    {
        Lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
    }

    protected void AppendProperty(StringBuilder result, string propertyName, object propertyValue, string indent)
    {
        result.Append(indent);
        result.Append('"');
        result.Append(propertyName);
        result.Append("\":");
        if (Format)
        {
            result.Append(" ");
        }

        AppendValue(result, propertyValue);

        result.Append(',');

        if (Format)
        {
            result.Append('\n');
        }
    }

    public static void AppendValue(StringBuilder result, object obj)
    {
        if (obj is int objInt)
        {
            result.Append(objInt);
            return;
        }

        string str = obj as string ?? obj?.ToString() ?? "";

        result.Append('"');
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            switch (c)
            {
                case '\\':
                case '"':
                    result.Append('\\');
                    result.Append(c);
                    break;
                case '\b':
                    result.Append("\\b");
                    break;
                case '\t':
                    result.Append("\\t");
                    break;
                case '\n':
                    result.Append("\\n");
                    break;
                case '\f':
                    result.Append("\\f");
                    break;
                case '\r':
                    result.Append("\\r");
                    break;
                default:
                    result.Append(c);
                    break;
            }
        }

        result.Append('"');
    }
}
