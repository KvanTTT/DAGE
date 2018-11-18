using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AntlrGrammarEditor
{
    public class ParserCompiler : StageProcessor
    {
        public const string PythonHelperFileName = "AntlrPythonCompileTest.py";
        public const string JavaScriptHelperFileName = "AntlrJavaScriptTest.js";
        public const string TemplateGrammarName = "__TemplateGrammarName__";
        public const string TemplateGrammarRoot = "__TemplateGrammarRoot__";
        public const string RuntimesDirName = "AntlrRuntimes";

        private Grammar _grammar;
        private ParserCompiliedState _result;
        private List<string> _buffer;
        private Dictionary<string, List<TextSpanMapping>> _grammarCodeMapping;
        private Runtime _currentRuntime;

        public ParserCompiliedState Compile(ParserGeneratedState state,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _grammar = state.GrammarCheckedState.InputState.Grammar;
            _result = new ParserCompiliedState(state)
            {
                Root = _grammar.Root,
                PreprocessorRoot = _grammar.PreprocessorRoot
            };

            foreach (Runtime runtime in _grammar.Runtimes)
            {
                Compile(state, runtime, cancellationToken);                
            }

            return _result;
        }

        private void Compile(ParserGeneratedState state, Runtime runtime, CancellationToken cancellationToken)
        {
            _currentRuntime = runtime;
            RuntimeInfo runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(runtime);

            Processor processor = null;
            try
            {
                string runtimeSource = runtime == Runtime.CSharpOptimized || runtime == Runtime.CSharpStandard
                    ? "CSharp"
                    : runtime == Runtime.Python2 || runtime == Runtime.Python3
                        ? "Python"
                        : runtime.ToString();
                string runtimeDir = Path.Combine(RuntimesDirName, runtimeSource);
                string runtimeLibraryPath = Path.Combine(runtimeDir, runtimeInfo.RuntimeLibrary);
                string workingDirectory = Path.Combine(ParserGenerator.HelperDirectoryName, _grammar.Name, runtime.ToString());

                var compiliedFiles = new StringBuilder();
                var generatedFiles = new List<string>();
                _grammarCodeMapping = new Dictionary<string, List<TextSpanMapping>>();

                GetGeneratedFileNames(state.GrammarCheckedState, runtimeInfo, workingDirectory, generatedFiles, compiliedFiles,
                    false);
                GetGeneratedFileNames(state.GrammarCheckedState, runtimeInfo, workingDirectory, generatedFiles, compiliedFiles,
                    true);

                string templateName = runtimeInfo.MainFile;
                string arguments = "";

                switch (runtime)
                {
                    case Runtime.CSharpOptimized:
                    case Runtime.CSharpStandard:
                        arguments = PrepareCSharpFiles(workingDirectory, runtime, runtimeDir);
                        break;

                    case Runtime.Java:
                        arguments = PrepareJavaFiles(compiliedFiles, templateName, runtimeDir, workingDirectory,
                            runtimeLibraryPath);
                        break;

                    case Runtime.Python2:
                    case Runtime.Python3:
                        arguments = PreparePythonFiles(generatedFiles, runtime, runtimeDir, workingDirectory);
                        break;

                    case Runtime.JavaScript:
                        arguments = PrepareJavaScriptFiles(generatedFiles, workingDirectory, runtimeDir);
                        break;

                    case Runtime.Go:
                        arguments = PrepareGoFiles(compiliedFiles, templateName, runtimeDir, workingDirectory);
                        break;
                }

                PrepareParserCode(workingDirectory, templateName, runtime, runtimeDir);

                _buffer = new List<string>();

                processor = new Processor(runtimeInfo.RuntimeToolName, arguments, workingDirectory);
                processor.CancellationToken = cancellationToken;
                processor.ErrorDataReceived += ParserCompilation_ErrorDataReceived;
                processor.OutputDataReceived += ParserCompilation_OutputDataReceived;

                processor.Start();

                if (_buffer.Count > 0)
                {
                    if (runtime == Runtime.Python2 || runtime == Runtime.Python3)
                    {
                        AddPythonError();
                    }
                    else if (runtime == Runtime.JavaScript)
                    {
                        AddJavaScriptError();
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                _result.Exception = ex;
                if (!(ex is OperationCanceledException))
                {
                    AddError(new ParsingError(ex, WorkflowStage.ParserCompilied));
                }
            }
            finally
            {
                processor?.Dispose();
            }
        }

        private void GetGeneratedFileNames(GrammarCheckedState grammarCheckedState, RuntimeInfo runtimeInfo, string workingDirectory, List<string> generatedFiles,
            StringBuilder compiliedFiles, bool lexer)
        {
            string grammarNameExt = _grammar.Name + (_grammar.SeparatedLexerAndParser ? (lexer ? "Lexer" : "Parser") : "") + ".g4";
            string shortGeneratedFile = runtimeInfo.GetGeneratedLexerParserName(_grammar, lexer);
            string generatedFile = Path.Combine(workingDirectory, shortGeneratedFile);
            generatedFiles.Add(generatedFile);
            compiliedFiles.Append('"' + shortGeneratedFile + "\" ");
            CodeSource codeSource = new CodeSource(generatedFile, File.ReadAllText(generatedFile));
            _grammarCodeMapping[shortGeneratedFile] = TextHelpers.Map(grammarCheckedState.GrammarActionsTextSpan[grammarNameExt], codeSource, lexer);
        }

        private string PrepareCSharpFiles(string workingDirectory, Runtime runtime, string runtimeDir)
        {
            string antlrCaseInsensitivePath = Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.cs");
            if (_grammar.CaseInsensitive)
            {
                File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.cs"), antlrCaseInsensitivePath);
            }

            File.Copy(Path.Combine(runtimeDir, "Program.cs"), Path.Combine(workingDirectory, "Program.cs"), true);
            File.Copy(Path.Combine(runtimeDir, "AssemblyInfo.cs"), Path.Combine(workingDirectory, "AssemblyInfo.cs"), true);

            var projectContent = File.ReadAllText(Path.Combine(runtimeDir, "Project.csproj"));
            projectContent = projectContent.Replace("<DefineConstants></DefineConstants>",
                $"<DefineConstants>{runtime}</DefineConstants>");
            File.WriteAllText(Path.Combine(workingDirectory, $"{_grammar.Name}.csproj"), projectContent);

            return "build";
        }

        private string PrepareJavaFiles(StringBuilder compiliedFiles, string templateName, string runtimeDir,
            string workingDirectory, string runtimeLibraryPath)
        {
            compiliedFiles.Append('"' + templateName + '"');
            if (_grammar.CaseInsensitive)
            {
                compiliedFiles.Append(" \"AntlrCaseInsensitiveInputStream.java\"");
                File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.java"),
                    Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.java"), true);
            }

            return $@"-cp ""{Path.Combine("..", "..", "..", runtimeLibraryPath)}"" " + compiliedFiles;
        }

        private string PreparePythonFiles(List<string> generatedFiles, Runtime runtime, string runtimeDir, string workingDirectory)
        {
            var stringBuilder = new StringBuilder();

            foreach (string file in generatedFiles)
            {
                var shortFileName = Path.GetFileNameWithoutExtension(file);
                stringBuilder.AppendLine($"from {shortFileName} import {shortFileName}");
            }

            if (_grammar.CaseInsensitive)
            {
                string antlrCaseInsensitiveInputStream =
                    File.ReadAllText(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.py"));
                string superCall, strType, intType;

                if (runtime == Runtime.Python2)
                {
                    superCall = "type(self), self";
                    strType = "";
                    intType = "";
                }
                else
                {
                    superCall = "";
                    strType = ": str";
                    intType = ": int";
                }

                antlrCaseInsensitiveInputStream = antlrCaseInsensitiveInputStream
                    .Replace("'''SuperCall'''", superCall)
                    .Replace("''': str'''", strType)
                    .Replace("''': int'''", intType);

                File.WriteAllText(Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.py"),
                    antlrCaseInsensitiveInputStream);
            }

            File.WriteAllText(Path.Combine(workingDirectory, PythonHelperFileName), stringBuilder.ToString());

            string arguments = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arguments += runtime == Runtime.Python2 ? "-2 " : "-3 ";
            }
            arguments += PythonHelperFileName;

            return arguments;
        }

        private string PrepareJavaScriptFiles(List<string> generatedFiles, string workingDirectory, string runtimeDir)
        {
            var stringBuilder = new StringBuilder();
            foreach (string file in generatedFiles)
            {
                var shortFileName = Path.GetFileNameWithoutExtension(file);
                stringBuilder.AppendLine($"var {shortFileName} = require('./{shortFileName}');");
            }

            File.WriteAllText(Path.Combine(workingDirectory, JavaScriptHelperFileName), stringBuilder.ToString());
            if (_grammar.CaseInsensitive)
            {
                File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.js"),
                    Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.js"), true);
            }

            return JavaScriptHelperFileName;
        }

        private string PrepareGoFiles(StringBuilder compiliedFiles, string templateName, string runtimeDir,
            string workingDirectory)
        {
            compiliedFiles.Insert(0, '"' + templateName + "\" ");
            if (_grammar.CaseInsensitive)
            {
                compiliedFiles.Append(" \"AntlrCaseInsensitiveInputStream.go\"");
                File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.go"),
                    Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.go"), true);
            }

            return "build " + compiliedFiles;
        }

        private void PrepareParserCode(string workingDirectory, string templateName, Runtime runtime, string runtimeDir)
        {
            string templateFile = Path.Combine(workingDirectory, templateName);

            string code = File.ReadAllText(Path.Combine(runtimeDir, templateName));
            code = code.Replace(TemplateGrammarName, _grammar.Name);
            string root = _grammar.Root;
            if (runtime == Runtime.Go)
            {
                root = char.ToUpper(root[0]) + root.Substring(1);
            }

            code = code.Replace(TemplateGrammarRoot, root);

            if (_grammar.CaseInsensitive)
            {
                code = code.Replace("from antlr4.InputStream import InputStream", "");
                code = code.Replace(RuntimeInfo.InitOrGetRuntimeInfo(runtime).AntlrInputStream,
                    (runtime == Runtime.Go ? "New" : "") + "AntlrCaseInsensitiveInputStream");

                if (runtime == Runtime.Python2 || runtime == Runtime.Python3)
                {
                    code = code.Replace("'''AntlrCaseInsensitive'''",
                        "from AntlrCaseInsensitiveInputStream import AntlrCaseInsensitiveInputStream");
                }
                else if (runtime == Runtime.JavaScript)
                {
                    code = code.Replace("/*AntlrCaseInsensitive*/",
                        "var AntlrCaseInsensitiveInputStream = require('./AntlrCaseInsensitiveInputStream').AntlrCaseInsensitiveInputStream;");
                }
            }
            else
            {
                if (runtime == Runtime.Python2 || runtime == Runtime.Python3)
                {
                    code = code.Replace("'''AntlrCaseInsensitive'''", "");
                }
                else if (runtime == Runtime.JavaScript)
                {
                    code = code.Replace("/*AntlrCaseInsensitive*/", "");
                }
            }

            if (runtime == Runtime.Python2)
            {
                code = code.Replace("'''PrintTree'''", "print \"Tree \" + tree.toStringTree(recog=parser);");
            }
            else if (runtime == Runtime.Python3)
            {
                code = code.Replace("'''PrintTree'''", "print(\"Tree \", tree.toStringTree(recog = parser));");
            }

            File.WriteAllText(templateFile, code);
        }

        private void ParserCompilation_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && !e.IsIgnoreError())
            {
                Console.WriteLine(e.Data);
                Runtime runtime = _currentRuntime;
                if (runtime == Runtime.CSharpStandard || runtime == Runtime.CSharpOptimized)
                {
                    AddCSharpError(e.Data);
                }
                else if (runtime == Runtime.Java)
                {
                    AddJavaError(e.Data);
                }
                else if (runtime == Runtime.Python2 || runtime == Runtime.Python3 || runtime == Runtime.JavaScript)
                {
                    lock (_buffer)
                    {
                        _buffer.Add(e.Data);
                    }
                }
                else if (runtime == Runtime.Go)
                {
                    AddGoError(e.Data);
                }
            }
        }

        private void ParserCompilation_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && !e.IsIgnoreError())
            {
                Console.WriteLine(e.Data);
                if (_currentRuntime == Runtime.CSharpOptimized || _currentRuntime == Runtime.CSharpStandard)
                {
                    AddCSharpError(e.Data);
                }
            }
        }

        private void AddCSharpError(string data)
        {
            if (data.Contains(": error CS"))
            {
                var errorString = Helpers.FixEncoding(data);
                ParsingError error;
                CodeSource grammarSource = CodeSource.Empty;
                try
                {
                    // Format:
                    // Lexer.cs(106,11): error CS0103: The name 'a' does not exist in the current context
                    var strs = errorString.Split(':');
                    int leftParenInd = strs[0].IndexOf('(');
                    string codeFileName = strs[0].Remove(leftParenInd);
                    string grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.CSharpStandard], codeFileName);
                    string lineColumnString = strs[0].Substring(leftParenInd);
                    lineColumnString = lineColumnString.Substring(1, lineColumnString.Length - 2); // Remove parenthesis.
                    var strs2 = lineColumnString.Split(',');
                    int line = int.Parse(strs2[0]);
                    int column = int.Parse(strs2[1]);
                    string rest = string.Join(":", strs.Skip(1));
                    var grammarTextSpan = TextHelpers.GetSourceTextSpanForLineColumn(_grammarCodeMapping[codeFileName], line, column);

                    grammarSource = _result.ParserGeneratedState.GrammarCheckedState.GrammarFilesData[grammarFileName];
                    if (!grammarTextSpan.IsEmpty)
                    {
                        error = new ParsingError(grammarTextSpan, $"{grammarFileName}:{grammarTextSpan.StartLineColumn.Line}:{rest}", WorkflowStage.ParserCompilied);
                    }
                    else
                    {
                        error = new ParsingError($"{grammarFileName}:{rest}", grammarSource, WorkflowStage.ParserCompilied);
                    }
                }
                catch
                {
                    error = new ParsingError(errorString, grammarSource, WorkflowStage.ParserCompilied);
                }
                AddError(error);
            }
        }

        private void AddJavaError(string data)
        {
            if (data.Count(c => c == ':') >= 2)
            {
                ParsingError error = null;
                string grammarFileName = "";
                try
                {
                    // Format:
                    // Lexer.java:98: error: cannot find symbol
                    string[] strs = data.Split(':');
                    string codeFileName = strs[0];
                    int codeLine = int.Parse(strs[1]);
                    string rest = string.Join(":", strs.Skip(2));
                    TextSpan grammarTextSpan = TextHelpers.GetSourceTextSpanForLine(_grammarCodeMapping[codeFileName], codeLine);
                    grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.Java], codeFileName);
                    if (grammarTextSpan != null)
                    {
                        error = new ParsingError(grammarTextSpan, $"{grammarFileName}:{grammarTextSpan.StartLineColumn.Line}:{rest}", WorkflowStage.ParserCompilied);
                    }
                    else
                    {
                        // FIXME later (_grammarFilesData) !
                        //error = new ParsingError($"{grammarFileName}:{rest}", _grammarFilesData[grammarFileName], WorkflowStage.ParserCompilied);
                    }
                }
                catch
                {
                    error = new ParsingError(data, CodeSource.Empty, WorkflowStage.ParserCompilied);
                }
                AddError(error);
            }
        }

        private void AddPythonError()
        {
            //Format:
            //Traceback(most recent call last):
            //  File "AntlrPythonCompileTest.py", line 1, in < module >
            //    from NewGrammarLexer import NewGrammarLexer
            //  File "Absolute\Path\To\LexerOrParser.py", line 23
            //    decisionsToDFA = [DFA(ds, i) for i, ds in enumerate(atn.decisionToState) ]
            //    ^
            //IndentationError: unexpected indent
            string message = "";
            string grammarFileName = "";
            TextSpan errorSpan = TextSpan.Empty;
            for (int i = 0; i < _buffer.Count; i++)
            {
                if (_buffer[i].TrimStart().StartsWith("File"))
                {
                    if (grammarFileName != "")
                    {
                        continue;
                    }

                    string codeFileName = _buffer[i];
                    codeFileName = codeFileName.Substring(codeFileName.IndexOf('"') + 1);
                    codeFileName = codeFileName.Remove(codeFileName.IndexOf('"'));
                    codeFileName = Path.GetFileName(codeFileName);

                    List<TextSpanMapping> mapping;
                    if (_grammarCodeMapping.TryGetValue(codeFileName, out mapping))
                    {
                        try
                        {
                            var lineStr = "\", line ";
                            lineStr = _buffer[i].Substring(_buffer[i].IndexOf(lineStr) + lineStr.Length);
                            int commaIndex = lineStr.IndexOf(',');
                            if (commaIndex != -1)
                            {
                                lineStr = lineStr.Remove(commaIndex);
                            }
                            int codeLine = int.Parse(lineStr);
                            errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine);
                            grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.Python2], codeFileName);
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        grammarFileName = "";
                    }
                }
                else if (i == _buffer.Count - 1)
                {
                    message = _buffer[i].Trim();
                }
            }
            string finalMessage = "";
            if (grammarFileName != "")
            {
                finalMessage = grammarFileName + ":";
            }
            if (!errorSpan.IsEmpty)
            {
                finalMessage += errorSpan.StartLineColumn.Line + ":";
            }
            finalMessage += message == "" ? "Unknown Error" : message;
            AddError(new ParsingError(errorSpan, finalMessage, WorkflowStage.ParserCompilied));
           
        }

        private void AddJavaScriptError()
        {
            //Absolute\Path\To\LexerOrParser.js:68
            //                break;
            //                ^^^^^
            //
            //SyntaxError: Unexpected token break
            //    at exports.runInThisContext (vm.js:53:16)
            //    at Module._compile (module.js:373:25)
            //    at Object.Module._extensions..js (module.js:416:10)
            //    at Module.load (module.js:343:32)
            //    at Function.Module._load (module.js:300:12)
            //    at Module.require (module.js:353:17)
            //    at require (internal/module.js:12:17)
            //    at Object.<anonymous> (Absolute\Path\To\AntlrJavaScriptTest.js:1:85)
            //    at Module._compile (module.js:409:26)
            //    at Object.Module._extensions..js (module.js:416:10)
            string message = "";
            string grammarFileName = "";
            TextSpan errorSpan = TextSpan.Empty;
            try
            {
                int semicolonLastIndex = _buffer[0].LastIndexOf(':');
                string codeFileName = Path.GetFileName(_buffer[0].Remove(semicolonLastIndex));
                List<TextSpanMapping> mapping;
                if (_grammarCodeMapping.TryGetValue(codeFileName, out mapping))
                {
                    int codeLine = int.Parse(_buffer[0].Substring(semicolonLastIndex + 1));
                    errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine);
                    grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.JavaScript], codeFileName);
                }
            }
            catch
            {
            }
            if (_buffer.Count > 0)
            {
                message = _buffer.LastOrDefault(line => !line.StartsWith("    at")) ?? "";
            }
            string finalMessage = "";
            if (grammarFileName != "")
            {
                finalMessage = grammarFileName + ":";
            }
            if (!errorSpan.IsEmpty)
            {
                finalMessage += errorSpan.StartLineColumn.Line + ":";
            }
            finalMessage += message == "" ? "Unknown Error" : message;
            AddError(new ParsingError(errorSpan, finalMessage, WorkflowStage.ParserCompilied));
        }

        private void AddGoError(string data)
        {
            if (data.Contains(": syntax error:"))
            {
                // Format:
                // .\newgrammar_parser.go:169: syntax error: unexpected semicolon or newline, expecting expression
                string grammarFileName = "";
                TextSpan errorSpan = TextSpan.Empty;
                string message = "";
                var strs = data.Split(':');
                try
                {
                    string codeFileName = strs[0].Substring(2);
                    List<TextSpanMapping> mapping;
                    if (_grammarCodeMapping.TryGetValue(codeFileName, out mapping))
                    {
                        int codeLine = int.Parse(strs[1]);
                        errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine);
                        grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.Go], codeFileName);
                    }
                    message = strs[3];
                }
                catch
                {
                }
                string finalMessage = "";
                if (grammarFileName != "")
                {
                    finalMessage = grammarFileName + ":";
                }
                if (!errorSpan.IsEmpty)
                {
                    finalMessage += errorSpan.StartLineColumn.Line + ":";
                }
                finalMessage += message == "" ? "Unknown Error" : message;
                AddError(new ParsingError(errorSpan, finalMessage, WorkflowStage.ParserCompilied));
            }
        }

        private string GetGrammarFromCodeFileName(RuntimeInfo runtimeInfo, string codeFileName)
        {
            if (!_grammar.SeparatedLexerAndParser)
            {
                var grammarFileName = Path.GetFileNameWithoutExtension(codeFileName);
                if (grammarFileName.EndsWith(runtimeInfo.LexerPostfix))
                {
                    grammarFileName = grammarFileName.Remove(grammarFileName.Length - runtimeInfo.LexerPostfix.Length);
                }
                else if (grammarFileName.EndsWith(runtimeInfo.ParserPostfix))
                {
                    grammarFileName = grammarFileName.Remove(grammarFileName.Length - runtimeInfo.ParserPostfix.Length);
                }

                return grammarFileName + Grammar.AntlrDotExt;
            }

            return Path.ChangeExtension(codeFileName, Grammar.AntlrDotExt);
        }

        private void AddError(ParsingError error)
        {
            ErrorEvent?.Invoke(this, error);
            _result.Errors.Add(error);
        }
    }
}