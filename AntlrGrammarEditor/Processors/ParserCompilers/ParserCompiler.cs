using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public abstract class ParserCompiler : StageProcessor
    {
        private const string CompilerHelperFileName = "AntlrCompileTest";
        private const string TemplateGrammarName = "__TemplateGrammarName__";
        private const string RuntimesPath = "__RuntimesPath__";
        public const string RuntimesDirName = "AntlrRuntimes";

        protected const string FileMark = "file";
        protected const string LineMark = "line";
        protected const string ColumnMark = "column";
        protected const string TypeMark = "type";

        private readonly SingleMark _packageNameMark;
        private readonly SingleMark _partMark;
        private readonly OpenCloseMark _parserPartMark;
        private readonly OpenCloseMark _lexerIncludeMark;
        private readonly OpenCloseMark _parserIncludeMark;
        private readonly SingleMark _caseInsensitiveMark;

        private readonly string _generatedGrammarName;
        private readonly Dictionary<string, List<TextSpanMapping>> _grammarCodeMapping = new();

        protected readonly Grammar Grammar;
        protected readonly RuntimeInfo CurrentRuntimeInfo;
        protected readonly ParserCompiledState Result;
        protected readonly string RuntimeDir;
        protected readonly string WorkingDirectory;
        protected readonly string RuntimeLibraryPath;
        protected readonly List<string> GeneratedFiles = new();
        protected readonly List<string> Buffer = new();

        public string? RuntimeLibrary { get; set; }

        protected abstract Regex ParserCompilerMessageRegex { get; }

        protected ParserCompiler(ParserGeneratedState state)
        {
            Grammar = state.GrammarCheckedState.InputState.Grammar;
            Result = new ParserCompiledState(state);
            CurrentRuntimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(Result.ParserGeneratedState.Runtime);
            Runtime runtime = state.Runtime;
            string runtimeSource = runtime.GetGeneralRuntimeName();
            RuntimeDir = Path.Combine(RuntimesDirName, runtimeSource);
            RuntimeLibraryPath = RuntimeLibrary ?? Path.Combine(RuntimeDir, CurrentRuntimeInfo.RuntimeLibrary);
            WorkingDirectory = Path.Combine(ParserGenerator.HelperDirectoryName, Grammar.Name, runtime.ToString());
            _generatedGrammarName =
                runtime != Runtime.Go ? Grammar.Name : Grammar.Name.ToLowerInvariant();

            _packageNameMark = new SingleMark("PackageName", CurrentRuntimeInfo);
            _partMark = new SingleMark("Part", CurrentRuntimeInfo);
            _parserPartMark = new OpenCloseMark("ParserPart", CurrentRuntimeInfo);
            _lexerIncludeMark = new OpenCloseMark("LexerInclude", CurrentRuntimeInfo);
            _parserIncludeMark = new OpenCloseMark("ParserInclude", CurrentRuntimeInfo);
            _caseInsensitiveMark = new SingleMark("AntlrCaseInsensitive", CurrentRuntimeInfo);
        }

        public ParserCompiledState Compile(CancellationToken cancellationToken = default)
        {
            var state = Result.ParserGeneratedState;

            Processor? processor = null;
            try
            {
                _grammarCodeMapping.Clear();

                if (Grammar.Type != GrammarType.Lexer)
                    GetGeneratedFileNames(false);

                GetGeneratedFileNames(true);
                CopyCompiledSources();

                if (Grammar.Type != GrammarType.Lexer)
                {
                    if (state.IncludeListener)
                        GetGeneratedListenerOrVisitorFiles(false);

                    if (state.IncludeVisitor)
                        GetGeneratedListenerOrVisitorFiles(true);
                }

                string arguments = PrepareFilesAndGetArguments();
                PrepareParserCode();

                lock (Buffer)
                {
                    Buffer.Clear();
                }

                Result.Command = CurrentRuntimeInfo.RuntimeToolName + " " + arguments;
                processor = new Processor(CurrentRuntimeInfo.RuntimeToolName, arguments, WorkingDirectory);
                processor.CancellationToken = cancellationToken;
                processor.ErrorDataReceived += ParserCompilation_ErrorDataReceived;
                processor.OutputDataReceived += ParserCompilation_OutputDataReceived;

                processor.Start();

                Postprocess();

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                Result.AddDiagnosis(new Diagnosis(ex, WorkflowStage.ParserCompiled));
            }
            finally
            {
                processor?.Dispose();
            }

            return Result;
        }

        protected abstract string PrepareFilesAndGetArguments();

        protected virtual void Postprocess()
        {
        }

        protected string CreateHelperFile(StringBuilder stringBuilder)
        {
            string compileTestFileName = CurrentRuntimeInfo.Runtime + CompilerHelperFileName + "." +
                                         CurrentRuntimeInfo.Extensions[0];
            File.WriteAllText(Path.Combine(WorkingDirectory, compileTestFileName), stringBuilder.ToString());
            return compileTestFileName;
        }

        protected string GetPhpAutoloadPath() =>
            Helpers.RuntimesPath.Replace("\\", "/") + "/Php/vendor/autoload.php";

        private void GetGeneratedFileNames(bool lexer)
        {
            string? grammarNameExt;

            if (Grammar.Type == GrammarType.Combined)
            {
                grammarNameExt = Grammar.Files.FirstOrDefault(file => Path.GetExtension(file)
                    .Equals(Grammar.AntlrDotExt, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                string postfix = lexer ? Grammar.LexerPostfix : Grammar.ParserPostfix;
                grammarNameExt = Grammar.Files.FirstOrDefault(file => file.Contains(postfix)
                    && Path.GetExtension(file).Equals(Grammar.AntlrDotExt, StringComparison.OrdinalIgnoreCase));
            }

            string shortGeneratedFile = _generatedGrammarName +
                                        (lexer ? CurrentRuntimeInfo.LexerPostfix : CurrentRuntimeInfo.ParserPostfix) +
                                        "." + CurrentRuntimeInfo.Extensions[0];

            string generatedFileDir = WorkingDirectory;
            Runtime runtime = CurrentRuntimeInfo.Runtime;
            if ((runtime == Runtime.Java || runtime == Runtime.Go) && !string.IsNullOrWhiteSpace(Result.ParserGeneratedState.PackageName))
            {
                generatedFileDir = Path.Combine(generatedFileDir, Result.ParserGeneratedState.PackageName);
            }
            string generatedFile = Path.Combine(generatedFileDir, shortGeneratedFile);
            GeneratedFiles.Add(generatedFile);
            var codeSource = new CodeSource(generatedFile, File.ReadAllText(generatedFile));
            var grammarCheckedState = Result.ParserGeneratedState.GrammarCheckedState;
            _grammarCodeMapping[shortGeneratedFile] = TextHelpers.Map(grammarCheckedState.GrammarActionsTextSpan[grammarNameExt], codeSource, lexer);
        }

        private void CopyCompiledSources()
        {
            RuntimeInfo runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(CurrentRuntimeInfo.Runtime);
            string extension = runtimeInfo.Extensions[0];

            foreach (string fileName in Grammar.Files)
            {
                if (Path.GetExtension(fileName).Equals("." + extension, StringComparison.OrdinalIgnoreCase))
                {
                    string sourceFileName = Path.Combine(Grammar.Directory, fileName);

                    string shortFileName = Path.GetFileName(fileName);

                    if ((runtimeInfo.Runtime == Runtime.Java || runtimeInfo.Runtime == Runtime.Go) &&
                        !string.IsNullOrWhiteSpace(Result.ParserGeneratedState.PackageName))
                    {
                        shortFileName = Path.Combine(Result.ParserGeneratedState.PackageName, shortFileName);
                    }

                    string destFileName = Path.Combine(WorkingDirectory, shortFileName);

                    File.Copy(sourceFileName, destFileName, true);
                }
            }
        }

        private void GetGeneratedListenerOrVisitorFiles(bool visitor)
        {
            string postfix = visitor ? CurrentRuntimeInfo.VisitorPostfix : CurrentRuntimeInfo.ListenerPostfix;
            string? basePostfix = visitor ? CurrentRuntimeInfo.BaseVisitorPostfix : CurrentRuntimeInfo.BaseListenerPostfix;

            GeneratedFiles.Add(Path.Combine(WorkingDirectory,
                _generatedGrammarName + postfix + "." + CurrentRuntimeInfo.Extensions[0]));
            if (basePostfix != null)
            {
                GeneratedFiles.Add(Path.Combine(WorkingDirectory,
                    _generatedGrammarName + basePostfix + "." + CurrentRuntimeInfo.Extensions[0]));
            }
        }

        private void PrepareParserCode()
        {
            string templateFile = Path.Combine(WorkingDirectory, CurrentRuntimeInfo.MainFile);
            Runtime runtime = CurrentRuntimeInfo.Runtime;

            string code = File.ReadAllText(Path.Combine(RuntimeDir, CurrentRuntimeInfo.MainFile));
            string? packageName = Result.ParserGeneratedState.PackageName;

            code = code.Replace(TemplateGrammarName, Grammar.Name);

            string newPackageValue = "";

            bool isPackageNameEmpty = string.IsNullOrWhiteSpace(packageName);

            if (!isPackageNameEmpty)
            {
                if (runtime.IsCSharpRuntime())
                {
                    newPackageValue = "using " + packageName + ";";
                }
                else if (runtime == Runtime.Java)
                {
                    newPackageValue = "import " + packageName + ".*;";
                }
                else if (runtime == Runtime.Go)
                {
                    newPackageValue = "\"./" + packageName + "\"";
                }
                else if (runtime == Runtime.Php)
                {
                    newPackageValue = $"use {packageName}\\{Grammar.Name}Lexer;";
                    if (Grammar.Type != GrammarType.Lexer)
                    {
                        newPackageValue += $"{Environment.NewLine}use {packageName}\\{Grammar.Name}Parser;";
                    }
                }
                else if (runtime == Runtime.Dart)
                {
                    if (Grammar.Type == GrammarType.Lexer)
                    {
                        newPackageValue = "library " + packageName + ";";
                    }
                }
            }

            code = code.Replace(_packageNameMark.ToString(), newPackageValue);

            if (runtime == Runtime.Go)
            {
                var packageName2Mark = new SingleMark("PackageName2", CurrentRuntimeInfo);
                code = code.Replace(packageName2Mark.ToString(), isPackageNameEmpty ? "" : packageName + ".");
            }
            else if (runtime == Runtime.Php)
            {
                code = code.Replace(RuntimesPath, GetPhpAutoloadPath());
            }
            else if (runtime == Runtime.Dart)
            {
                code = code.Replace(_partMark.ToString(), Grammar.Type == GrammarType.Lexer && !isPackageNameEmpty
                    ? $"part '{Grammar.Name}Lexer.dart';" : "");
            }

            string caseInsensitiveBlockMarker = _caseInsensitiveMark.ToString();

            if (Grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                string antlrInputStream = RuntimeInfo.InitOrGetRuntimeInfo(runtime).AntlrInputStream;
                string caseInsensitiveStream = "AntlrCaseInsensitiveInputStream";

                if (runtime == Runtime.Java)
                {
                    caseInsensitiveStream = "new " + caseInsensitiveStream;
                }
                else if (runtime == Runtime.Go)
                {
                    caseInsensitiveStream = "New" + caseInsensitiveStream;
                }
                else if (runtime == Runtime.Php)
                {
                    antlrInputStream = antlrInputStream + "::fromPath";
                    caseInsensitiveStream = caseInsensitiveStream + "::fromPath";
                }
                else if (runtime == Runtime.Dart)
                {
                    antlrInputStream = antlrInputStream + ".fromPath";
                    caseInsensitiveStream = caseInsensitiveStream + ".fromPath";
                }

                var antlrInputStreamRegex = new Regex($@"{antlrInputStream}\(([^\)]+)\)");
                string isLowerBool = (Grammar.CaseInsensitiveType == CaseInsensitiveType.lower).ToString();
                if (!runtime.IsPythonRuntime())
                {
                    isLowerBool = isLowerBool.ToLowerInvariant();
                }

                code = antlrInputStreamRegex.Replace(code,
                    m => $"{caseInsensitiveStream}({m.Groups[1].Value}, {isLowerBool})");

                if (runtime.IsPythonRuntime())
                {
                    code = code.Replace("from antlr4.InputStream import InputStream", "")
                        .Replace(caseInsensitiveBlockMarker,
                            "from AntlrCaseInsensitiveInputStream import AntlrCaseInsensitiveInputStream");
                }
                else if (runtime == Runtime.JavaScript)
                {
                    code = code.Replace(caseInsensitiveBlockMarker,
                        "import AntlrCaseInsensitiveInputStream from './AntlrCaseInsensitiveInputStream.js';");
                }
                else if (runtime == Runtime.Php)
                {
                    code = code.Replace(caseInsensitiveBlockMarker, "require_once 'AntlrCaseInsensitiveInputStream.php';");
                }
                else if (runtime == Runtime.Dart)
                {
                    code = code.Replace(caseInsensitiveBlockMarker, "import 'AntlrCaseInsensitiveInputStream.dart';");
                }
            }
            else
            {
                code = code.Replace(caseInsensitiveBlockMarker, "");
            }

            if (runtime.IsPythonRuntime())
            {
                string newValue = runtime == Runtime.Python2
                    ? "print \"Tree \" + tree.toStringTree(recog=parser)"
                    : "print(\"Tree \", tree.toStringTree(recog=parser))";
                code = code.Replace("'''$PrintTree$'''", newValue);
            }
            else if (runtime == Runtime.Dart)
            {
                code = RemoveCodeOrClearMarkers(code, _lexerIncludeMark, () => !isPackageNameEmpty);
            }

            code = RemoveCodeOrClearMarkers(code, _parserIncludeMark, () => Grammar.Type == GrammarType.Lexer);
            code = RemoveCodeOrClearMarkers(code, _parserPartMark, () => Grammar.Type == GrammarType.Lexer);

            File.WriteAllText(templateFile, code);
        }

        private string RemoveCodeOrClearMarkers(string code, OpenCloseMark mark, Func<bool> condition)
        {
            var openMark = mark.OpenMark;
            var closeMark = mark.CloseMark;

            if (condition.Invoke())
            {
                int parserStartIndex = code.IndexOf(openMark, StringComparison.Ordinal);
                if (parserStartIndex == -1)
                    return code;

                int parserEndIndex = code.IndexOf(closeMark, StringComparison.Ordinal) + closeMark.Length;
                if (parserEndIndex == -1)
                    return code;

                code = code.Remove(parserStartIndex) + code.Substring(parserEndIndex);
            }
            else
            {
                code = code.Replace(openMark, "").Replace(closeMark, "");
            }

            return code;
        }

        private void ParserCompilation_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                ProcessReceivedData(e.Data);
        }

        private void ParserCompilation_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                ProcessReceivedData(e.Data);
        }

        protected virtual void ProcessReceivedData(string data)
        {
            var match = ParserCompilerMessageRegex.Match(data);
            if (match.Success)
            {
                var groups = match.Groups;
                string codeFileName = Path.GetFileName(groups[FileMark].Value);
                int.TryParse(groups[LineMark].Value, out int line);
                if (!int.TryParse(groups[ColumnMark].Value, out int column))
                    column = LineColumnTextSpan.StartColumn;
                string message = groups[Helpers.MessageMark].Value;
                var diagnosisType = groups[TypeMark].Value.Contains("warning", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosisType.Warning
                    : DiagnosisType.Error;

                if (CurrentRuntimeInfo.Runtime == Runtime.Java && message.StartsWith("[deprecation] ANTLRInputStream"))
                    return;

                var diagnosis = MapGeneratedToSourceAndCreateDiagnosis(codeFileName, line, column, message, diagnosisType);
                AddDiagnosis(diagnosis);
            }
        }

        protected void AddToBuffer(string data)
        {
            lock (Buffer)
            {
                Buffer.Add(data);
            }
        }

        protected void AddDiagnosis(Diagnosis diagnosis)
        {
            DiagnosisEvent?.Invoke(this, diagnosis);
            Result.AddDiagnosis(diagnosis);
        }

        protected Diagnosis MapGeneratedToSourceAndCreateDiagnosis(string? codeFileName, int line, int column, string message, DiagnosisType type)
        {
            if (codeFileName == null)
            {
                return new Diagnosis(message, WorkflowStage.ParserCompiled, type);
            }

            Diagnosis diagnosis;

            if (_grammarCodeMapping.TryGetValue(codeFileName, out List<TextSpanMapping> textSpanMappings))
            {
                string? grammarFileName = GetGrammarFromCodeFileName(CurrentRuntimeInfo, codeFileName);
                if (TryGetSourceTextSpanForLine(textSpanMappings, line, out TextSpan textSpan))
                {
                    return new Diagnosis(textSpan, $"{grammarFileName}:{textSpan.LineColumn.BeginLine}:{message}",
                        WorkflowStage.ParserCompiled, type);
                }

                return new Diagnosis(message, WorkflowStage.ParserCompiled, type);
            }
            else
            {
                var grammarFilesData = Result.ParserGeneratedState.GrammarCheckedState.GrammarFilesData;
                CodeSource? grammarSource =
                    grammarFilesData.FirstOrDefault(file => file.Key.EndsWith(codeFileName, StringComparison.OrdinalIgnoreCase)).Value;

                TextSpan? textSpan = grammarSource != null
                    ? new LineColumnTextSpan(line, column, grammarSource).GetTextSpan()
                    : null;
                diagnosis = textSpan != null
                    ? new Diagnosis(textSpan.Value, message, WorkflowStage.ParserCompiled, type)
                    : new Diagnosis(message, WorkflowStage.ParserCompiled, type);
            }

            return diagnosis;
        }

        private string? GetGrammarFromCodeFileName(RuntimeInfo runtimeInfo, string codeFileName)
        {
            string? result = Grammar.Files.FirstOrDefault(file => file.EndsWith(codeFileName));
            if (result != null)
            {
                return result;
            }

            result = Path.GetFileNameWithoutExtension(codeFileName);

            if (Grammar.Type == GrammarType.Combined)
            {
                if (result.EndsWith(runtimeInfo.LexerPostfix))
                {
                    result = result.Remove(result.Length - runtimeInfo.LexerPostfix.Length);
                }
                else if (result.EndsWith(runtimeInfo.ParserPostfix))
                {
                    result = result.Remove(result.Length - runtimeInfo.ParserPostfix.Length);
                }
            }

            result = result + Grammar.AntlrDotExt;

            return Grammar.Files.FirstOrDefault(file => file.EndsWith(result));
        }

        private static bool TryGetSourceTextSpanForLine(List<TextSpanMapping> textSpanMappings, int destinationLine,
            out TextSpan textSpan)
        {
            foreach (TextSpanMapping textSpanMapping in textSpanMappings)
            {
                LineColumnTextSpan destLineColumnTextSpan = textSpanMapping.DestTextSpan.LineColumn;
                if (destinationLine >= destLineColumnTextSpan.BeginLine &&
                    destinationLine <= destLineColumnTextSpan.EndLine)
                {
                    textSpan = textSpanMapping.SourceTextSpan;
                    return true;
                }
            }

            if (textSpanMappings.Count > 0)
            {
                textSpan = TextSpan.GetEmpty(textSpanMappings[0].SourceTextSpan.Source);
                return true;
            }

            textSpan = default;
            return false;
        }
    }
}