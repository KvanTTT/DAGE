using Antlr4.Runtime;

namespace AntlrGrammarEditor
{
    public abstract class LexerAdaptor : Lexer
    {
        private const int InvalidType = -1;
        private int _currentRuleType = InvalidType;

        public LexerAdaptor(ICharStream charStream)
            : base(charStream)
        {
        }

        protected void handleBeginArgument()
        {
            if (_currentRuleType == ANTLRv4Lexer.TOKEN_REF)
            {
                PushMode(ANTLRv4Lexer.LexerCharSet);
                More();
            }
            else
            {
                PushMode(ANTLRv4Lexer.Argument);
            }
        }

        protected void handleEndArgument()
        {
            PopMode();
            if (_modeStack.Count > 0)
            {
                _type = ANTLRv4Lexer.ARGUMENT_CONTENT;
            }
        }

        protected void handleEndAction()
        {
            PopMode();
            if (_modeStack.Count > 0)
            {
                _type = ANTLRv4Lexer.ACTION_CONTENT;
            }
        }

        public override void Emit(IToken token)
        {
            if (_type == ANTLRv4Lexer.ID)
            {
                var tokenText = _input.GetText(new Antlr4.Runtime.Misc.Interval(_tokenStartCharIndex, _tokenStartCharIndex));
                if (char.IsUpper(tokenText[0]))
                {
                    _type = ANTLRv4Lexer.TOKEN_REF;
                }
                else
                {
                    _type = ANTLRv4Lexer.RULE_REF;
                }

                if (_currentRuleType == InvalidType)
                {
                    _currentRuleType = _type;
                }

                base.Emit(new CommonToken(token) { Type = _type });
            }
            else 
            {
                if (_type == ANTLRv4Lexer.SEMI)
                {
                    _currentRuleType = InvalidType;
                }
                base.Emit(token);
            }
        }
    }
}
