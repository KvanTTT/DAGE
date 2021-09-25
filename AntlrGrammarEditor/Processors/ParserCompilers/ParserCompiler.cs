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
using AntlrGrammarEditor.Processors.ParserGeneration;
using AntlrGrammarEditor.Sources;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.ParserCompilers
{
    public abstract class ParserCompiler : StageProcessor
    {
        private const string CompilerHelperFileName = "AntlrCompileTest";
        private const string TemplateLexerName = "__TemplateLexerName__";
        private const string TemplateParserName = "__TemplateParserName__";
        private const string RuntimesPath = "__RuntimesPath__";
        private const string PackageName = "__PackageName__";
        public const string RuntimesDirName = "AntlrRuntimes";

        private static readonly Regex FragmentMarkRegexBegin;
        private static readonly Regex FragmentMarkRegexEnd;
        private static readonly Regex CSharpStopStringRegex = new (@"^Build FAILED\.$", RegexOptions.Compiled);

        private readonly object _lockObject = new ();

        private readonly OpenCloseMark _packageNameMark;
        private readonly OpenCloseMark _parserPartMark;
        private readonly OpenCloseMark _lexerIncludeMark;
        private readonly OpenCloseMark _parserIncludeMark;
        private readonly OpenCloseMark _caseInsensitiveMark;

        private readonly Dictionary<string, FragmentMapper> _fragmentFinders = new ();
        private readonly Dictionary<string, Source> _runtimeFileSources = new();

        protected readonly GrammarCheckedState GrammarCheckedState;
        protected readonly RuntimeInfo CurrentRuntimeInfo;
        protected readonly ParserCompiledState Result;
        protected readonly string RuntimeDir;
        protected readonly string WorkingDirectory;
        protected readonly string RuntimeLibraryPath;
        protected readonly List<string> Buffer = new();
        private bool _ignoreDiagnosis;

        public string? RuntimeLibrary { get; set; }

        public string GrammarName => Result.ParserGeneratedState.GrammarCheckedState.MainGrammarName;

        public CaseInsensitiveType CaseInsensitiveType => Result.CaseInsensitiveType;

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

        protected ParserCompiler(ParserGeneratedState state, CaseInsensitiveType? caseInsensitiveType)
        {
            GrammarCheckedState = state.GrammarCheckedState;
            var definedCaseInsensitiveType = caseInsensitiveType ??
                                             state.GrammarCheckedState.CaseInsensitiveType ?? CaseInsensitiveType.None;
            Result = new ParserCompiledState(state, definedCaseInsensitiveType, _runtimeFileSources);
            CurrentRuntimeInfo = Result.ParserGeneratedState.Runtime.GetRuntimeInfo();
            string runtimeSource = state.Runtime.GetGeneralRuntimeName();
            RuntimeDir = Path.Combine(RuntimesDirName, runtimeSource);
            RuntimeLibraryPath = RuntimeLibrary ?? Path.Combine(RuntimeDir, CurrentRuntimeInfo.RuntimeLibrary);
            WorkingDirectory = Path.Combine(ParserGenerator.HelperDirectoryName,
                state.GrammarCheckedState.MainGrammarName, Result.ParserGeneratedState.Runtime.ToString());

            _packageNameMark = new OpenCloseMark("PackageName", CurrentRuntimeInfo);
            _parserPartMark = new OpenCloseMark("ParserPart", CurrentRuntimeInfo);
            _lexerIncludeMark = new OpenCloseMark("LexerInclude", CurrentRuntimeInfo);
            _parserIncludeMark = new OpenCloseMark("ParserInclude", CurrentRuntimeInfo);
            _caseInsensitiveMark = new OpenCloseMark("AntlrCaseInsensitive", CurrentRuntimeInfo);
        }

        public ParserCompiledState Compile(CancellationToken cancellationToken = default)
        {
            Processor? processor = null;
            try
            {
                //CopyCompiledSources();

                _runtimeFileSources.Clear();
                PreprocessGeneratedFiles();

                string arguments = PrepareFilesAndGetArguments();
                PrepareParserCode();

                lock (Buffer)
                {
                    Buffer.Clear();
                }

                Result.Command = CurrentRuntimeInfo.RuntimeCompilerToolToolName + " " + arguments;
                processor = new Processor(CurrentRuntimeInfo.RuntimeCompilerToolToolName, arguments, WorkingDirectory);
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

        private void PreprocessGeneratedFiles()
        {
            var grammarMappedFragments = Result.ParserGeneratedState.MappedFragments;
            foreach (var generatedFileName in Result.ParserGeneratedState.RuntimeFileInfos.Keys)
            {
                var generatedRawMappedFragments = new List<RawMappedFragment>();

                var text = File.ReadAllText(generatedFileName);
                var textSpan = text.AsSpan();
                StringBuilder? newTextBuilder = null;

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

                var shortGeneratedFileName = Path.GetFileName(generatedFileName);
                Source source;

                if (newTextBuilder != null)
                {
                    // Grammar contains actions and predicates
                    newTextBuilder.Append(textSpan.Slice(index));
                    var newText = newTextBuilder.ToString();
                    File.WriteAllText(generatedFileName, newText);
                    source = new Source(generatedFileName, newText);

                    var generatedMappedFragments = new List<MappedFragment>(generatedRawMappedFragments.Count);
                    foreach (var rawMappedFragment in generatedRawMappedFragments)
                        generatedMappedFragments.Add(rawMappedFragment.ToMappedFragment(source));

                    _fragmentFinders.Add(shortGeneratedFileName,
                        new FragmentMapper(Result.ParserGeneratedState.Runtime, source, generatedMappedFragments));
                }
                else
                {
                    source = new Source(shortGeneratedFileName, text);
                }

                _runtimeFileSources.Add(shortGeneratedFileName, source);
            }
        }

        private void PrepareParserCode()
        {
            string templateFile = Path.Combine(WorkingDirectory, CurrentRuntimeInfo.MainFile);
            Runtime runtime = CurrentRuntimeInfo.Runtime;

            string code = File.ReadAllText(Path.Combine(RuntimeDir, CurrentRuntimeInfo.MainFile));
            string? packageName = Result.ParserGeneratedState.PackageName;

            var grammarType = GrammarCheckedState.GrammarProjectType;
            var lexerName = Result.ParserGeneratedState.LexerName;
            if (lexerName != null)
                code = code.Replace(TemplateLexerName, lexerName);

            var parserName = Result.ParserGeneratedState.ParserName;
            if (parserName != null)
                code = code.Replace(TemplateParserName, parserName);

            bool isPackageNameEmpty = string.IsNullOrWhiteSpace(packageName);
            bool removePackageNameCode = isPackageNameEmpty;

            if (!isPackageNameEmpty)
            {
                code = code.Replace(PackageName, packageName);

                if (runtime == Runtime.Dart)
                {
                    removePackageNameCode = grammarType != GrammarProjectType.Lexer;
                }
            }

            if (runtime == Runtime.Php)
            {
                RemoveCodeWithinMarkOrRemoveMark(ref code,
                    new OpenCloseMark("PackageNameParser", CurrentRuntimeInfo),
                    isPackageNameEmpty || grammarType == GrammarProjectType.Lexer);
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
                    grammarType == GrammarProjectType.Lexer && !isPackageNameEmpty
                    ? $"part '{lexerName}.dart';" : "");
            }

            if (CaseInsensitiveType != CaseInsensitiveType.None)
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
                string isLowerBool = (CaseInsensitiveType == CaseInsensitiveType.Lower).ToString();
                if (runtime != Runtime.Python)
                {
                    isLowerBool = isLowerBool.ToLowerInvariant();
                }

                code = antlrInputStreamRegex.Replace(code,
                    m => $"{caseInsensitiveStream}({m.Groups[1].Value}, {isLowerBool})");

                if (runtime == Runtime.Python)
                    code = code.Replace("from antlr4.InputStream import InputStream", "");
            }

            RemoveCodeWithinMarkOrRemoveMark(ref code, _caseInsensitiveMark, CaseInsensitiveType == CaseInsensitiveType.None);

            if (runtime == Runtime.Dart)
            {
                RemoveCodeWithinMarkOrRemoveMark(ref code, _lexerIncludeMark, !isPackageNameEmpty);
            }

            RemoveCodeWithinMarkOrRemoveMark(ref code, _parserIncludeMark, grammarType == GrammarProjectType.Lexer);
            RemoveCodeWithinMarkOrRemoveMark(ref code, _parserPartMark, grammarType == GrammarProjectType.Lexer);

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
            lock (_lockObject)
            {
                if (_ignoreDiagnosis)
                    return;

                if (CurrentRuntimeInfo.Runtime.IsCSharpRuntime() && CSharpStopStringRegex.IsMatch(data))
                    _ignoreDiagnosis = true;
            }

            var match = ParserCompilerMessageRegex.Match(data);
            if (match.Success)
            {
                var groups = match.Groups;
                string codeFileName = Path.GetFileName(groups[FileMark].Value);
                int.TryParse(groups[LineMark].Value, out int line);
                if (!int.TryParse(groups[ColumnMark].Value, out int column))
                    column = LineColumnTextSpan.StartColumn;
                string message = groups[MessageMark].Value;
                var (simplifiedMessage, errorTextSpanLength) = SimplifyMessageAndSpecifyErrorTextSpanLength(message);
                var diagnosisType = groups[TypeMark].Value.Contains("warning", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosisType.Warning
                    : DiagnosisType.Error;

                if (CurrentRuntimeInfo.Runtime == Runtime.Java && message.StartsWith("[deprecation] ANTLRInputStream"))
                    return;

                var diagnosis = CreateMappedGrammarDiagnosis(codeFileName, line, column, simplifiedMessage, diagnosisType);
                AddDiagnosis(diagnosis);
            }
        }

        protected virtual (string NewMessage, int ErrorTextSpanLength) SimplifyMessageAndSpecifyErrorTextSpanLength(string message)
        {
            return (message, 0);
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

            var source = _runtimeFileSources[codeFileName];
            var position = source.LineColumnToPosition(line, column);
            return new Diagnosis(new TextSpan(position, 0, source), message, WorkflowStage.ParserCompiled, type);
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