using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AntlrGrammarEditor.Fragments;
using AntlrGrammarEditor.Processors.ParserCompilation;
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

        private readonly Dictionary<string, FragmentMapper> _fragmentMappers = new ();
        private readonly Dictionary<string, (Source, RuntimeFileInfo)> _runtimeFiles = new();

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
            GrammarCheckedState = state.GrammarCheckedState;
            Result = new ParserCompiledState(state, _runtimeFiles);
            CurrentRuntimeInfo = Result.ParserGeneratedState.Runtime.GetRuntimeInfo();
            string runtimeSource = state.Runtime.ToString();
            RuntimeDir = Path.Combine(RuntimesDirName, runtimeSource);
            RuntimeLibraryPath = RuntimeLibrary ?? Path.Combine(RuntimeDir, CurrentRuntimeInfo.RuntimeLibrary);
            WorkingDirectory = Path.Combine(ParserGenerator.HelperDirectoryName,
                state.GrammarCheckedState.MainGrammarName, Result.ParserGeneratedState.Runtime.ToString());

            _packageNameMark = new OpenCloseMark("PackageName", CurrentRuntimeInfo);
            _parserPartMark = new OpenCloseMark("ParserPart", CurrentRuntimeInfo);
            _lexerIncludeMark = new OpenCloseMark("LexerInclude", CurrentRuntimeInfo);
            _parserIncludeMark = new OpenCloseMark("ParserInclude", CurrentRuntimeInfo);
        }

        public ParserCompiledState Compile(CancellationToken cancellationToken = default)
        {
            Processor? processor = null;
            try
            {
                _runtimeFiles.Clear();
                PreprocessGeneratedFiles();
                CopyHelperFiles();

                string arguments = PrepareFilesAndGetArguments();
                PrepareEntryPointCode();

                lock (Buffer)
                    Buffer.Clear();

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
                Result.AddDiagnosis(new ParserCompilationDiagnosis(ex));
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
            foreach (var (generatedFileName, runtimeFileInfo) in Result.ParserGeneratedState.RuntimeFileInfos)
            {
                var generatedRawMappedFragments = new List<RawMappedFragment>();

                var text = File.ReadAllText(runtimeFileInfo.FullName);
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

                Source source;
                if (newTextBuilder != null)
                {
                    // Grammar contains actions and predicates
                    newTextBuilder.Append(textSpan.Slice(index));
                    var newText = newTextBuilder.ToString();
                    File.WriteAllText(runtimeFileInfo.FullName, newText);
                    source = new Source(runtimeFileInfo.FullName, newText);

                    var generatedMappedFragments = new List<MappedFragment>(generatedRawMappedFragments.Count);
                    foreach (var rawMappedFragment in generatedRawMappedFragments)
                        generatedMappedFragments.Add(rawMappedFragment.ToMappedFragment(source));

                    _fragmentMappers.Add(generatedFileName,
                        new FragmentMapper(Result.ParserGeneratedState.Runtime, source, generatedMappedFragments));
                }
                else
                {
                    source = new Source(runtimeFileInfo.FullName, text);
                }

                _runtimeFiles.Add(generatedFileName, (source, runtimeFileInfo));
            }
        }

        private void CopyHelperFiles()
        {
            foreach (var (generatedFileName, runtimeFileInfo) in Result.ParserGeneratedState.RuntimeFileInfos)
            {
                if (runtimeFileInfo.Type == RuntimeFileType.Helper)
                {
                    var outputFileName = Path.Combine(WorkingDirectory, generatedFileName);
                    File.Copy(runtimeFileInfo.FullName, outputFileName, true);
                }
            }
        }

        private void PrepareEntryPointCode()
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

                if (CurrentRuntimeInfo.Runtime == Runtime.CSharp && CSharpStopStringRegex.IsMatch(data))
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

        protected ParserCompilationDiagnosis CreateMappedGrammarDiagnosis(string? codeFileName, int line, int column,
            string message, DiagnosisType type)
        {
            if (codeFileName == null)
                return new ParserCompilationDiagnosis(message, type);

            if (_fragmentMappers.TryGetValue(codeFileName, out FragmentMapper fragmentMapper))
            {
                var mappedResult = fragmentMapper.Map(line, column);
                return new ParserCompilationDiagnosis(mappedResult.TextSpanInGrammar, mappedResult.TextSpanInGenerated, message, type);
            }

            if (_runtimeFiles.TryGetValue(codeFileName, out var tuple))
            {
                var source = tuple.Item1;
                var position = source.LineColumnToPosition(line, column);
                return new ParserCompilationDiagnosis(null, new TextSpan(position, 0, source), message, type);
            }

            return new ParserCompilationDiagnosis(message);
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