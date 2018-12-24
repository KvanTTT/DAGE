using System;
using System.Collections.Generic;
using System.Text;
using Antlr4.Runtime;

public class TokensJsonSerializer
{
    public const string TypeProperty = "Type";
    public const string TextProperty = "Text";
    public const string IndexProperty = "Index";
    public const string LineProperty = "Line";
    public const string ColumnProperty = "Column";
    public const string LengthProperty = "Length";
    public const string ChannelProperty = "Channel";

    public Lexer Lexer { get; }

    public bool Format { get; set; }

    public bool SymbolicNames { get; set; }

    public bool LineColumn { get; set; }

    public TokensJsonSerializer(Lexer lexer)
    {
        Lexer = lexer ?? throw new NullReferenceException(nameof(lexer));
    }

    public string ToJson(IList<IToken> tokens)
    {
        string nl, indent;

        if (Format)
        {
            nl = "\n";
            indent = "    ";
        }
        else
        {
            nl = "";
            indent = "";
        }

        var result = new StringBuilder();
        result.Append('[');
        result.Append(nl);

        for (int i = 0; i < tokens.Count; i++)
        {
            IToken token = tokens[i];

            result.Append(indent);
            result.Append('{');
            result.Append(nl);

            object typeValue = SymbolicNames ? (object) Lexer.Vocabulary.GetSymbolicName(token.Type) : token.Type;
            AppendProperty(result, TypeProperty, typeValue, indent, nl);

            string tokenText = token.Text ?? "";
            if (!string.IsNullOrEmpty(tokenText))
            {
                AppendProperty(result, TextProperty, tokenText, indent, nl);
            }

            if (token.StartIndex != -1)
            {
                if (LineColumn)
                {
                    AppendProperty(result, LineProperty, token.Line, indent, nl);
                    AppendProperty(result, ColumnProperty, token.Column + 1, indent, nl);
                }
                else
                {
                    AppendProperty(result, IndexProperty, token.StartIndex, indent, nl);
                }

                if (token.StopIndex != -1 && token.StopIndex + 1 - token.StartIndex != tokenText.Length)
                {
                    AppendProperty(result, LengthProperty, token.StopIndex - token.StartIndex, indent, nl);
                }
            }

            if (token.Channel != Lexer.DefaultTokenChannel)
            {
                object channelValue =
#if CSharpOptimized
                    token.Channel; // Channel names are not supported in optimized runtime for now
#else
                    SymbolicNames ? (object) Lexer.ChannelNames[token.Channel] : token.Channel;
#endif
                AppendProperty(result, ChannelProperty, channelValue, indent, nl);
            }

            int lastCommaIndex = Format ? result.Length - 2 : result.Length - 1;
            result.Remove(lastCommaIndex, 1);

            result.Append(indent);
            result.Append('}');
            if (i != tokens.Count - 1)
            {
                result.Append(',');
            }

            result.Append(nl);
        }

        result.Append(']');

        return result.ToString();
    }

    private void AppendProperty(StringBuilder result, string propertyName, object propertyValue, string indent,
        string nl)
    {
        result.Append(indent);
        result.Append(indent);
        result.Append('"');
        result.Append(propertyName);
        result.Append("\":");
        if (Format)
        {
            result.Append(" ");
        }

        EscapeAndAppend(result, propertyValue);

        result.Append(',');
        result.Append(nl);
    }

    public static void EscapeAndAppend(StringBuilder result, object obj)
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
