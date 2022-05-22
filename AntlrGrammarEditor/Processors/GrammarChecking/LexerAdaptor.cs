using Antlr4.Runtime;

namespace AntlrGrammarEditor
{
    public abstract class LexerAdaptor : Lexer
    {
        private static readonly int PREQUEL_CONSTRUCT = -10;
        private static readonly int OPTIONS_CONSTRUCT = -11;

        protected LexerAdaptor(ICharStream input)
            : base(input)
        {
            CurrentRuleType = TokenConstants.InvalidType;
            _insideOptionsBlock = false;
        }

        /**
         * Track whether we are inside of a rule and whether it is lexical parser. _currentRuleType==TokenConstants.InvalidType
         * means that we are outside of a rule. At the first sign of a rule name reference and _currentRuleType==invalid, we
         * can assume that we are starting a parser rule. Similarly, seeing a token reference when not already in rule means
         * starting a token rule. The terminating ';' of a rule, flips this back to invalid type.
         *
         * This is not perfect logic but works. For example, "grammar T;" means that we start and stop a lexical rule for
         * the "T;". Dangerous but works.
         *
         * The whole point of this state information is to distinguish between [..arg actions..] and [charsets]. Char sets
         * can only occur in lexical rules and arg actions cannot occur.
         */
        private int CurrentRuleType { get; set; } = TokenConstants.InvalidType;

        private bool _insideOptionsBlock;

        protected void handleBeginArgument()
        {
            if (InLexerRule)
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
                Type = ANTLRv4Lexer.ARGUMENT_CONTENT;
            }
        }

        protected void handleEndAction()
        {
            var oldMode = _mode;
            var newMode = PopMode();

            if (_modeStack.Count > 0 && newMode == ANTLRv4Lexer.Argument && oldMode == newMode)
            {
                Type = ANTLRv4Lexer.ACTION_CONTENT;
            }
        }

        private bool InLexerRule
        {
            get { return CurrentRuleType == ANTLRv4Lexer.TOKEN_REF; }
        }

        public override IToken Emit()
        {
            if ((Type == ANTLRv4Lexer.OPTIONS || Type == ANTLRv4Lexer.TOKENS || Type == ANTLRv4Lexer.CHANNELS) &&
                CurrentRuleType == TokenConstants.InvalidType)
            {
                // enter prequel construct ending with an RBRACE
                CurrentRuleType = PREQUEL_CONSTRUCT;
            }
            else if (Type == ANTLRv4Lexer.OPTIONS && CurrentRuleType == ANTLRv4Lexer.TOKEN_REF)
            {
                CurrentRuleType = OPTIONS_CONSTRUCT;
            }
            else if (Type == ANTLRv4Lexer.RBRACE && CurrentRuleType == PREQUEL_CONSTRUCT)
            {
                // exit prequel construct
                CurrentRuleType = TokenConstants.InvalidType;
            }
            else if (Type == ANTLRv4Lexer.RBRACE && CurrentRuleType == OPTIONS_CONSTRUCT)
            {
                // exit options
                CurrentRuleType = ANTLRv4Lexer.TOKEN_REF;
            }
            else if (Type == ANTLRv4Lexer.AT && CurrentRuleType == TokenConstants.InvalidType)
            {
                // enter action
                CurrentRuleType = ANTLRv4Lexer.AT;
            }
            else if (Type == ANTLRv4Lexer.SEMI && CurrentRuleType == OPTIONS_CONSTRUCT)
            {
                // ';' in options { .... }. Don't change anything.
            }
            else if (Type == ANTLRv4Lexer.END_ACTION && CurrentRuleType == ANTLRv4Lexer.AT)
            {
                // exit action
                CurrentRuleType = TokenConstants.InvalidType;
            }
            else if (Type == ANTLRv4Lexer.ID)
            {
                var firstChar =
                    _input.GetText(new Antlr4.Runtime.Misc.Interval(_tokenStartCharIndex, _tokenStartCharIndex))[0];
                if (char.IsUpper(firstChar))
                {
                    Type = ANTLRv4Lexer.TOKEN_REF;
                }

                if (char.IsLower(firstChar))
                {
                    Type = ANTLRv4Lexer.RULE_REF;
                }

                if (CurrentRuleType == TokenConstants.InvalidType)
                {
                    // if outside of rule def
                    CurrentRuleType = Type; // set to inside lexer or parser rule
                }
            }
            else if (Type == ANTLRv4Lexer.SEMI)
            {
                CurrentRuleType = TokenConstants.InvalidType;
            }

            return base.Emit();
        }

        public override void Reset()
        {
            CurrentRuleType = TokenConstants.InvalidType;
            _insideOptionsBlock = false;
            base.Reset();
        }
    }
}
