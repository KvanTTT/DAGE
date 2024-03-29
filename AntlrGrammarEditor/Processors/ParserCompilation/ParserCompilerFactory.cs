using System;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public static class ParserCompilerFactory
    {
        public static ParserCompiler Create(ParserGeneratedState state, string? runtimeLibrary, EventHandler<Diagnosis>? diagnosisEvent)
        {
            ParserCompiler result;
            switch (state.Runtime)
            {
                case Runtime.CSharp:
                    result = new ParserCompilerCSharp(state);
                    break;
                case Runtime.Java:
                    result = new ParserCompilerJava(state);
                    break;
                case Runtime.Python:
                    result = new ParserCompilerPython(state);
                    break;
                case Runtime.JavaScript:
                    result = new ParserCompilerJavaScript(state);
                    break;
                case Runtime.Go:
                    result = new ParserCompilerGo(state);
                    break;
                case Runtime.Php:
                    result = new ParserCompilerPhp(state);
                    break;
                case Runtime.Dart:
                    result = new ParserCompilerDart(state);
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