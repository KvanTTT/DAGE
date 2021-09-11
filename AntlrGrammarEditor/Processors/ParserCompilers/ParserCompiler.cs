using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AntlrGrammarEditor.Diagnoses;
using AntlrGrammarEditor.Fragments;
using AntlrGrammarEditor.Sources;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public abstract class ParserCompiler : StageProcessor
    {
        private const string CompilerHelperFileName = "AntlrCompileTest";
        private const string TemplateGrammarName = "__TemplateGrammarName__";
        private const string RuntimesPath = "__RuntimesPath__";
        private const string PackageName = "__PackageName__";
        public const string RuntimesDirName = "AntlrRuntimes";

        private static readonly Regex FragmentMarkRegexBegin;
        private static readonly Regex FragmentMarkRegexEnd;

        private readonly OpenCloseMark _packageNameMark;
        private readonly OpenCloseMark _parserPartMark;
        private readonly OpenCloseMark _lexerIncludeMark;
        private readonly OpenCloseMark _parserIncludeMark;
        private readonly OpenCloseMark _caseInsensitiveMark;

        private readonly string _generatedGrammarName;
        private readonly Dictionary<string, FragmentMapper> _fragmentFinders = new ();

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

        static ParserCompiler()
        {
            var mark = new OpenCloseMark(ParserGenerator.FragmentMarkWord, RuntimeInfo.Runtimes[Runtime.Java],
                ParserGenerator.FragmentMarkSuffix);
            var digitRegex = @"\d";
            int digitsCount = ParserGenerator.FragmentMarkDigitsCount;
            var digitsRegex = new StringBuilder(digitRegex.Length * digitsCount).Insert(0, digitRegex, digitsCount).ToString();
            var digitsGroup = $"({digitsRegex})";
            var fragmentRegexStringBegin = Regex.Escape($"{mark.StartCommentToken}{mark.Suffix}{mark.Name}") +
                                           digitsGroup +
                                           Regex.Escape(mark.EndCommentToken);
            var fragmentRegexStringEnd = Regex.Escape($"{mark.StartCommentToken}{mark.Name}") +
                                         digitsGroup +
                                         Regex.Escape($"{mark.Suffix}{mark.EndCommentToken}");
            FragmentMarkRegexBegin = new Regex(fragmentRegexStringBegin, RegexOptions.Compiled);
            FragmentMarkRegexEnd = new Regex(fragmentRegexStringEnd, RegexOptions.Compiled);
        }

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

            _packageNameMark = new OpenCloseMark("PackageName", CurrentRuntimeInfo);
            _parserPartMark = new OpenCloseMark("ParserPart", CurrentRuntimeInfo);
            _lexerIncludeMark = new OpenCloseMark("LexerInclude", CurrentRuntimeInfo);
            _parserIncludeMark = new OpenCloseMark("ParserInclude", CurrentRuntimeInfo);
            _caseInsensitiveMark = new OpenCloseMark("AntlrCaseInsensitive", CurrentRuntimeInfo);
        }

        public ParserCompiledState Compile(CancellationToken cancellationToken = default)
        {
            var state = Result.ParserGeneratedState;

            Processor? processor = null;
            try
            {
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

                PreprocessGeneratedFiles();

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

        private void PreprocessGeneratedFiles()
        {
            foreach (string generatedFile in GeneratedFiles)
            {
                var generatedRawMappedFragments = new List<RawMappedFragment>();

                var text = File.ReadAllText(generatedFile);
                var textSpan = text.AsSpan();
                StringBuilder? newTextBuilder = null;
                var grammarMappedFragments = Result.ParserGeneratedState.MappedFragments;

                var index = 0;
                var beginMatch = FragmentMarkRegexBegin.Match(text, index);
                while (beginMatch.Success)
                {
                    newTextBuilder ??= new StringBuilder(text.Length);

                    if (!int.TryParse(beginMatch.Groups[1].Value, out var fragmentNumber))
                        throw new FormatException("Incorrect fragment number");

                    newTextBuilder.Append(textSpan.Slice(index, beginMatch.Index - index));
                    var fragmentIndex = newTextBuilder.Length;
                    index = beginMatch.Index + beginMatch.Length;

                    var endMatch = FragmentMarkRegexEnd.Match(text, index);

                    if (!endMatch.Success)
                        throw new FormatException("Every mark should have both begin and end part");

                    newTextBuilder.Append(textSpan.Slice(index, endMatch.Index - index));
                    index = endMatch.Index + endMatch.Length;

                    if (fragmentNumber < 0 || fragmentNumber >= grammarMappedFragments.Count)
                        throw new FormatException($"Fragment number {fragmentIndex} does not map to grammar fragment");

                    generatedRawMappedFragments.Add(new RawMappedFragment(fragmentIndex,
                    newTextBuilder.Length - fragmentIndex, grammarMappedFragments[fragmentNumber]));

                    beginMatch = FragmentMarkRegexBegin.Match(text, index);
                }

                if (newTextBuilder != null)
                {
                    // Grammar contains actions and predicates
                    newTextBuilder.Append(textSpan.Slice(index));
                    var newText = newTextBuilder.ToString();
                    File.WriteAllText(generatedFile, newText);
                    var source = new Source(generatedFile, newText);

                    var generatedMappedFragments = new List<MappedFragment>(generatedRawMappedFragments.Count);
                    foreach (var rawMappedFragment in generatedRawMappedFragments)
                        generatedMappedFragments.Add(rawMappedFragment.ToMappedFragment(source));

                    _fragmentFinders.Add(Path.GetFileName(generatedFile),
                        new FragmentMapper(Result.ParserGeneratedState.Runtime, source, generatedMappedFragments));
                }
            }
        }

        private void PrepareParserCode()
        {
            string templateFile = Path.Combine(WorkingDirectory, CurrentRuntimeInfo.MainFile);
            Runtime runtime = CurrentRuntimeInfo.Runtime;

            string code = File.ReadAllText(Path.Combine(RuntimeDir, CurrentRuntimeInfo.MainFile));
            string? packageName = Result.ParserGeneratedState.PackageName;

            code = code.Replace(TemplateGrammarName, Grammar.Name);

            bool isPackageNameEmpty = string.IsNullOrWhiteSpace(packageName);
            bool removePackageNameCode = isPackageNameEmpty;

            if (!isPackageNameEmpty)
            {
                code = code.Replace(PackageName, packageName);

                if (runtime == Runtime.Dart)
                {
                    removePackageNameCode = Grammar.Type != GrammarType.Lexer;
                }
            }

            if (runtime == Runtime.Php)
            {
                RemoveCodeWithinMarkOrRemoveMark(ref code,
                    new OpenCloseMark("PackageNameParser", CurrentRuntimeInfo),
                    isPackageNameEmpty || Grammar.Type == GrammarType.Lexer);
            }
            RemoveCodeWithinMarkOrRemoveMark(ref code, _packageNameMark, removePackageNameCode);

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
                code = code.Replace(new SingleMark("Part", CurrentRuntimeInfo).ToString(),
                    Grammar.Type == GrammarType.Lexer && !isPackageNameEmpty
                    ? $"part '{Grammar.Name}Lexer.dart';" : "");
            }

            if (Grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                string antlrInputStream = CurrentRuntimeInfo.AntlrInputStream;
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
                if (runtime != Runtime.Python)
                {
                    isLowerBool = isLowerBool.ToLowerInvariant();
                }

                code = antlrInputStreamRegex.Replace(code,
                    m => $"{caseInsensitiveStream}({m.Groups[1].Value}, {isLowerBool})");

                if (runtime == Runtime.Python)
                    code = code.Replace("from antlr4.InputStream import InputStream", "");
            }

            RemoveCodeWithinMarkOrRemoveMark(ref code, _caseInsensitiveMark, Grammar.CaseInsensitiveType == CaseInsensitiveType.None);

            if (runtime == Runtime.Dart)
            {
                RemoveCodeWithinMarkOrRemoveMark(ref code, _lexerIncludeMark, !isPackageNameEmpty);
            }

            RemoveCodeWithinMarkOrRemoveMark(ref code, _parserIncludeMark, Grammar.Type == GrammarType.Lexer);
            RemoveCodeWithinMarkOrRemoveMark(ref code, _parserPartMark, Grammar.Type == GrammarType.Lexer);

            File.WriteAllText(templateFile, code);
        }

        private void RemoveCodeWithinMarkOrRemoveMark(ref string code, OpenCloseMark mark, bool removeCode)
        {
            var openMark = mark.OpenMark;
            var closeMark = mark.CloseMark;

            int parserStartIndex = code.IndexOf(openMark, StringComparison.Ordinal);
            if (parserStartIndex == -1)
                return;

            int parserEndIndex = code.IndexOf(closeMark, parserStartIndex, StringComparison.Ordinal);
            if (parserEndIndex == -1)
                throw new FormatException($"Close mark not found for {openMark}");

            var firstPart = code.Remove(parserStartIndex);
            if (removeCode)
            {
                parserEndIndex += closeMark.Length;
                var lastChar = code.ElementAtOrDefault(parserEndIndex);
                if (lastChar == '\r' || lastChar == '\n')
                    parserEndIndex++;
                if (code.ElementAtOrDefault(parserEndIndex) == '\n')
                    parserEndIndex++;
                code = firstPart + code.Substring(parserEndIndex);
            }
            else
            {
                int parserStartIndexEnd = parserStartIndex + openMark.Length;
                var middlePart = code.Substring(parserStartIndexEnd, parserEndIndex - parserStartIndexEnd);
                code = firstPart + middlePart + code.Substring(parserEndIndex + closeMark.Length);
            }
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
                string message = groups[MessageMark].Value;
                var diagnosisType = groups[TypeMark].Value.Contains("warning", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosisType.Warning
                    : DiagnosisType.Error;

                if (CurrentRuntimeInfo.Runtime == Runtime.Java && message.StartsWith("[deprecation] ANTLRInputStream"))
                    return;

                var diagnosis = CreateMappedGrammarDiagnosis(codeFileName, line, column, message, diagnosisType);
                AddDiagnosis(diagnosis);
            }
        }

        protected Diagnosis CreateMappedGrammarDiagnosis(string? codeFileName, int line, int column,
            string message, DiagnosisType type)
        {
            if (codeFileName == null)
                return new Diagnosis(message, WorkflowStage.ParserCompiled, type);

            if (_fragmentFinders.TryGetValue(codeFileName, out FragmentMapper fragmentMapper))
            {
                var mappedResult = fragmentMapper.Map(line, column);
                return new Diagnosis(mappedResult.TextSpanInGrammar, message, WorkflowStage.ParserCompiled, type);
            }

            var grammarFilesData = Result.ParserGeneratedState.GrammarCheckedState.GrammarInfos;
            Source grammarSource =
                grammarFilesData
                    .FirstOrDefault(file => file.Key.EndsWith(codeFileName, StringComparison.OrdinalIgnoreCase))
                    .Value
                    .Source;

            var textSpan = new LineColumnTextSpan(line, column, grammarSource).ToLinear();
            return new Diagnosis(textSpan, message, WorkflowStage.ParserCompiled, type);
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
    }
}