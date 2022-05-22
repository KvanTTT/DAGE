using Antlr4.Runtime;

class AntlrCaseInsensitiveInputStream : AntlrInputStream
{
    private string lookaheadData;

    public AntlrCaseInsensitiveInputStream(string input, bool lowerCase = false)
        : base(input)
    {
        lookaheadData = lowerCase ? input.ToLowerInvariant() : input.ToUpperInvariant();
    }

    public override int LA(int i)
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
                return IntStreamConstants.EOF; // invalid; no char before first char
            }
        }

        if (p + i - 1 >= n)
        {
            return Eof;
        }

        return lookaheadData[p + i - 1];
    }
}
