using Antlr4.Runtime;

class AntlrCaseInsensitiveInputStream : AntlrInputStream
{
    private string lookaheadData;

    public readonly bool LowerCase;

    public AntlrCaseInsensitiveInputStream(string input, bool lowerCase = true)
        : base(input)
    {
        LowerCase = lowerCase;
        lookaheadData = lowerCase ? input.ToLower() : input.ToUpper();
    }

    public const int Eof =
#if CSharpOptimized
        IntStreamConstants.Eof;
#else
        IntStreamConstants.EOF;
#endif

#if CSharpOptimized
    public override int La(int i)
#else
    public override int LA(int i)
#endif
    {
        if (i == 0)
        {
            return 0; // undefined
        }
        if (i < 0)
        {
            i++; // e.g., translate LA(-1) to use offset i=0; then data[p+0-1]
            if (p + i - 1 < 0)
            {
                return Eof; // invalid; no char before first char
            }
        }

        if (p + i - 1 >= n)
        {
            return Eof;
        }

        return lookaheadData[p + i - 1];
    }
}
