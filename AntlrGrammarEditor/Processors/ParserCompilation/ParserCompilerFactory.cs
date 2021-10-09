using System;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public static class ParserCompilerFactory
    {
        public static ParserCompiler Create(ParserGeneratedState state,
            CaseInsensitiveType? caseInsensitiveType,
            string? runtimeLibrary,
            EventHandler<Diagnosis>? diagnosisEvent)
        {
            ParserCompiler result;
            switch (state.Runtime)
            {
                case Runtime.CSharpStandard:
                case Runtime.CSharpOptimized:
                    result = new ParserCompilerCSharp(state, caseInsensitiveType);
                    break;
                case Runtime.Java:
                    result = new ParserCompilerJava(state, caseInsensitiveType);
                    break;
                case Runtime.Python:
                    result = new ParserCompilerPython(state, caseInsensitiveType);
                    break;
                case Runtime.JavaScript:
                    result = new ParserCompilerJavaScript(state, caseInsensitiveType);
                    break;
                case Runtime.Go:
                    result = new ParserCompilerGo(state, caseInsensitiveType);
                    break;
                case Runtime.Php:
                    result = new ParserCompilerPhp(state, caseInsensitiveType);
                    break;
                case Runtime.Dart:
                    result = new ParserCompilerDart(state, caseInsensitiveType);
                    break;
                default:
                    throw new NotImplementedException($"ParserCompiler for {state.Runtime} is not implemented");
            }

            result.RuntimeLibrary = runtimeLibrary;
            result.DiagnosisEvent = diagnosisEvent;
            return result;
        }
    }
}