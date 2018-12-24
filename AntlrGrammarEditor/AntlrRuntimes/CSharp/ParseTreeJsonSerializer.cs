using System;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

public class ParseTreeJsonSerializer : JsonSerializerBase
{
    public const string ChildrenProperty = "Children";
    public const string TokenPropertyValue = "Token";

    public Parser Parser { get; }

    public ParseTreeJsonSerializer(Lexer lexer, Parser parser)
        : base(lexer)
    {
        Parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public string ToJson(ParserRuleContext parseTree)
    {
        var result = new StringBuilder();

        result.Append('{');
        ToJson(parseTree, result, 1);
        result.Append('}');

        return result.ToString();
    }

    private void ToJson(IParseTree parseTree, StringBuilder builder, int level)
    {
        string indentString = Format ? string.Empty.PadLeft(level * IndentSize) : "";

        if (Format)
        {
            builder.Append('\n');
        }

        if (parseTree is RuleContext ruleContext)
        {
            object typeValue =
                SymbolicNames ? (object)Parser.RuleNames[ruleContext.RuleIndex] : ruleContext.RuleIndex;
            AppendProperty(builder, TypeProperty, typeValue, indentString);

            builder.Append(indentString);
            builder.Append('"');
            builder.Append(ChildrenProperty);
            builder.Append("\":");
            if (Format)
            {
                builder.Append(" ");
            }

            builder.Append("[{");

            for (int i = 0; i < ruleContext.ChildCount; i++)
            {
                ToJson(ruleContext.GetChild(i), builder, level + 1);
                builder.Append(indentString);
                builder.Append("}");
                if (i != ruleContext.ChildCount - 1)
                {
                    builder.Append(",{");
                    if (Format)
                    {
                        builder.Append(" ");
                    }
                }
            }

            builder.Append("]");
            if (Format)
            {
                builder.Append("\n");
            }
        }
        else
        {
            IToken token = ((ITerminalNode)parseTree).Symbol;

            object typeValue = SymbolicNames ? (object)TokenPropertyValue : -1;
            AppendProperty(builder, TypeProperty, typeValue, indentString);
            AppendProperty(builder, IndexProperty, token.TokenIndex, indentString);

            int lastCommaIndex = Format ? builder.Length - 2 : builder.Length - 1;
            builder.Remove(lastCommaIndex, 1);
        }
    }
}