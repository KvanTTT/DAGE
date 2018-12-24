using System.Collections.Generic;
using System.Text;
using Antlr4.Runtime;

public class TokensJsonSerializer : JsonSerializerBase
{
    public const string TextProperty = "Text";
    public const string ChannelProperty = "Channel";
    public const string LineProperty = "Line";
    public const string ColumnProperty = "Column";
    public const string LengthProperty = "Length";

    public TokensJsonSerializer(Lexer lexer)
        : base(lexer)
    {
    }

    public string ToJson(IList<IToken> tokens)
    {
        string indent = Format ? string.Empty.PadLeft(IndentSize) : "";

        var result = new StringBuilder();
        result.Append('[');

        for (int i = 0; i < tokens.Count; i++)
        {
            IToken token = tokens[i];

            result.Append('{');
            if (Format)
            {
                result.Append('\n');
            }

            object typeValue = SymbolicNames ? (object)Lexer.Vocabulary.GetSymbolicName(token.Type) : token.Type;
            AppendProperty(result, TypeProperty, typeValue, indent);

            if (token.StartIndex != -1)
            {
                if (LineColumn)
                {
                    AppendProperty(result, LineProperty, token.Line, indent);
                    AppendProperty(result, ColumnProperty, token.Column + 1, indent);
                }
                else
                {
                    AppendProperty(result, IndexProperty, token.StartIndex, indent);
                }

                if (token.StopIndex != -1)
                {
                    AppendProperty(result, LengthProperty, token.StopIndex + 1 - token.StartIndex, indent);
                }
            }

            string tokenText = token.Text ?? "";
            if (!string.IsNullOrEmpty(tokenText) && token.StopIndex + 1 - token.StartIndex != tokenText.Length)
            {
                AppendProperty(result, TextProperty, tokenText, indent);
            }

            if (token.Channel != Lexer.DefaultTokenChannel)
            {
                object channelValue =
#if CSharpOptimized
                    token.Channel; // Channel names are not supported in optimized runtime for now
#else
                    SymbolicNames ? (object)Lexer.ChannelNames[token.Channel] : token.Channel;
#endif
                AppendProperty(result, ChannelProperty, channelValue, indent);
            }

            int lastCommaIndex = Format ? result.Length - 2 : result.Length - 1;
            result.Remove(lastCommaIndex, 1);

            result.Append('}');
            if (i != tokens.Count - 1)
            {
                result.Append(',');
            }
        }

        result.Append(']');

        return result.ToString();
    }
}
