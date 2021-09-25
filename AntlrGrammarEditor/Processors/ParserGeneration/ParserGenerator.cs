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
using AntlrGrammarEditor.Processors.ParserCompilers;
using AntlrGrammarEditor.Sources;
using AntlrGrammarEditor.WorkflowState;
using static AntlrGrammarEditor.Helpers;

namespace AntlrGrammarEditor.Processors.ParserGeneration
{
    public class ParserGenerator : StageProcessor
    {
        public const string FragmentMarkWord = "fragment_";
        public const string FragmentMarkSuffix = "#";
        public const int FragmentMarkDigitsCount = 3;
        public const string HelperDirectoryName = "DageHelperDirectory";

        // warning(180): .\test.g4:3:20: chars '\'' used multiple times in set ['' ]
        private static readonly Regex ParserGeneratorMessageRegex = new(
            $@"^(?<{TypeMark}>[^\(]+)\(\d+\): (?<{FileMark}>.+?):(?<{LineMark}>\d*):(?<{ColumnMark}>\d*): (?<{MessageMark}>.+)",
            RegexOptions.Compiled);
        private static readonly string FragmentMarkFormat = FragmentMarkWord + "{0:" + new string('0', FragmentMarkDigitsCount) + "}";
        private static readonly int FragmentMarkLength = new OpenCloseMark(string.Format(FragmentMarkFormat, 0),
            RuntimeInfo.Runtimes[Runtime.Java], FragmentMarkSuffix).OpenMark.Length;
        private static readonly Regex PackageRegex = new(@"^([a-zA-Z_][a-zA-Z\d_]*)$", RegexOptions.Compiled);

        private static readonly Dictionary<Encoding, string> Encodings = new()
        {
            [Encoding.Default] = "default",
            [Encoding.Utf8] = "utf8"
        };

        private readonly Grammar _grammar;
        private readonly string _runtimeDirectoryName;
        private readonly List<MappedFragment> _mappedFragments = new();
        private readonly Dictionary<string, Source> _grammarSources = new();
        private readonly Dictionary<string, RuntimeFileInfo> _runtimeFileInfos = new();
        private readonly ParserGeneratedState _result;

        public Runtime Runtime => _result.Runtime;

        public string? PackageName => _result.PackageName;

        public string? GeneratorTool { get; set; }

        public Encoding Encoding { get; }

        public ParserGenerator(GrammarCheckedState state,
            Runtime? runtime,
            string? packageName,
            bool? generateListener,
            bool? generateVisitor,
            Encoding encoding = Encoding.Utf8)
        {
            _grammar = state.InputState.Grammar;
            var definedPackageName = packageName ?? state.Package;
            var definedGenerateListener = generateListener ?? state.GenerateListener ?? false;
            var definedGenerateVisitor = generateVisitor ?? state.GenerateVisitor ?? false;
            var detectedRuntime = runtime ?? state.Runtime ?? Runtime.Java;
            _runtimeDirectoryName = Path.Combine(HelperDirectoryName, _grammar.Name, detectedRuntime.ToString());
            if ((detectedRuntime == Runtime.Java || detectedRuntime == Runtime.Go) && !string.IsNullOrWhiteSpace(definedPackageName))
                _runtimeDirectoryName = Path.Combine(_runtimeDirectoryName, definedPackageName);

            string? lexerName, parserName;
            if (state.GrammarProjectType == GrammarProjectType.Combined)
            {
                var grammarInfo = state.GrammarInfos.First(info => info.Type == GrammarFileType.Combined);
                lexerName = grammarInfo.Name + Grammar.LexerPostfix;
                parserName = grammarInfo.Name + Grammar.ParserPostfix;
            }
            else
            {
                lexerName = state.GrammarInfos.FirstOrDefault(info => info.Type == GrammarFileType.Lexer)?.Name;
                parserName = state.GrammarInfos.FirstOrDefault(info => info.Type == GrammarFileType.Parser)?.Name;
            }

            _result = new ParserGeneratedState(state,
                definedPackageName,
                detectedRuntime,
                definedGenerateListener,
                definedGenerateVisitor,
                lexerName,
                parserName,
                _mappedFragments,
                _grammarSources,
                _runtimeFileInfos);
            Encoding = encoding;
        }

        public ParserGeneratedState Generate(CancellationToken cancellationToken = default)
        {
            var grammarCheckedState = _result.GrammarCheckedState;
            _mappedFragments.Clear();
            _grammarSources.Clear();
            Generate(grammarCheckedState, cancellationToken);
            return _result;
        }

        private void Generate(GrammarCheckedState state, CancellationToken cancellationToken)
        {
            Processor? processor = null;

            try
            {
                if (Directory.Exists(_runtimeDirectoryName))
                    Directory.Delete(_runtimeDirectoryName, true);

                Directory.CreateDirectory(_runtimeDirectoryName);

                cancellationToken.ThrowIfCancellationRequested();

                var runtimeInfo = Runtime.GetRuntimeInfo();

                var jarGenerator = GeneratorTool ?? Path.Combine("Generators", runtimeInfo.JarGenerator);
                foreach (var grammarInfo in state.GrammarInfos)
                {
                    var (grammarFileName, outputDirectory) = InsertFragmentMarks(grammarInfo);

                    var arguments = new StringBuilder();
                    arguments.Append($@"-jar ""{jarGenerator}"" ""{grammarFileName}""");
                    arguments.Append($@" -o ""{outputDirectory}""");
                    arguments.Append($" -Dlanguage={runtimeInfo.DLanguage}");
                    arguments.Append($" -encoding {Encodings[Encoding]}");

                    if (grammarInfo.Type.IsParser())
                    {
                        arguments.Append(_result.IncludeVisitor ? " -visitor" : " -no-visitor");
                        arguments.Append(_result.IncludeListener ? " -listener" : " -no-listener");
                    }

                    if (!string.IsNullOrWhiteSpace(PackageName))
                    {
                        if (!PackageRegex.IsMatch(PackageName))
                        {
                            var invalidPackageNameDiagnosis = new Diagnosis(
                                $"Package name ({PackageName}) should contain only latin letter, digits, and underscore",
                                WorkflowStage.ParserGenerated);

                            _result.AddDiagnosis(invalidPackageNameDiagnosis);
                            DiagnosisEvent?.Invoke(this, invalidPackageNameDiagnosis);
                            return;
                        }
                        arguments.Append(" -package ");
                        arguments.Append(PackageName);
                    }
                    else if (Runtime == Runtime.Go)
                    {
                        arguments.Append(" -package main");
                    }

                    if (grammarInfo.SuperClass != null)
                    {
                        arguments.Append(" -DsuperClass=");
                        arguments.Append(grammarInfo.SuperClass);
                    }

                    var argumentsString = arguments.ToString();
                    _result.Command = "java " + argumentsString;
                    processor = new Processor("java", argumentsString, ".");
                    processor.CancellationToken = cancellationToken;
                    processor.ErrorDataReceived += ParserGeneration_ErrorDataReceived;
                    processor.OutputDataReceived += ParserGeneration_OutputDataReceived;

                    processor.Start();

                    AddGeneratedFiles(grammarInfo);

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                {
                    _result.AddDiagnosis(new Diagnosis(ex, WorkflowStage.ParserGenerated));
                    DiagnosisEvent?.Invoke(this, new Diagnosis(ex, WorkflowStage.ParserGenerated));
                }
            }
            finally
            {
                processor?.Dispose();
            }
        }

        private void ParserGeneration_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!e.IsIgnoredMessage(Runtime.Java))
            {
                Diagnosis diagnosis;
                var match = ParserGeneratorMessageRegex.Match(e.Data);
                if (match.Success)
                {
                    var groups = match.Groups;
                    var grammarFileName = Path.GetFileName(groups[FileMark].Value);
                    if (!int.TryParse(groups[LineMark].Value, out int line))
                        line = LineColumnTextSpan.StartLine;
                    int.TryParse(groups[ColumnMark].Value, out int column);
                    column += LineColumnTextSpan.StartColumn;
                    var message = groups[MessageMark].Value;
                    var diagnosisType = groups[TypeMark].Value == "warning"
                        ? DiagnosisType.Warning
                        : DiagnosisType.Error;

                    var textSpan = _result.GetOriginalTextSpanForLineColumn(grammarFileName, line, column);
                    diagnosis = new Diagnosis(textSpan, message, WorkflowStage.ParserGenerated, diagnosisType);
                }
                else
                {
                    diagnosis = new Diagnosis("Unknown error: " + e.Data, WorkflowStage.ParserGenerated);
                }

                _result.AddDiagnosis(diagnosis);
                DiagnosisEvent?.Invoke(this, diagnosis);
            }
        }

        private void ParserGeneration_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!e.IsIgnoredMessage(Runtime.Java))
            {
            }
        }

        private (string GrammarFileName, string OutputDirectory) InsertFragmentMarks(GrammarInfo grammarInfo)
        {
            var sourceSpan = grammarInfo.Source.Text.AsSpan();
            StringBuilder? result = null;
            int previousIndex = 0;

            var offsets = new List<int>();

            void AppendAndAddOffset(string mark)
            {
                result.Append(mark);
                offsets.Add(result.Length);
            }

            var runtimeInfo = Runtime.Java.GetRuntimeInfo();
            var rawFragments = new List<RawMappedFragment>(grammarInfo.Fragments.Count);
            foreach (var fragment in grammarInfo.Fragments)
            {
                result ??= new StringBuilder(sourceSpan.Length);
                var fragmentMark = new OpenCloseMark( string.Format(FragmentMarkFormat, fragment.Number), runtimeInfo, "#");
                var textSpan = fragment.TextSpan;
                result.Append(sourceSpan.Slice(previousIndex, textSpan.Start - previousIndex));
                AppendAndAddOffset(fragmentMark.OpenMark);
                int mappedFragmentIndex = result.Length;
                result.Append(textSpan.Span);
                AppendAndAddOffset(fragmentMark.CloseMark);

                rawFragments.Add(new RawMappedFragment(mappedFragmentIndex, textSpan.Length, fragment));

                previousIndex = textSpan.End;
            }

            string grammarFileName = Path.GetFileName(grammarInfo.Source.Name);
            string newGrammarFileName, outputDirectory;
            Source source;

            if (result != null)
            {
                result.Append(sourceSpan.Slice(previousIndex));
                var newCode = result.ToString();
                source = new SourceWithMarks(grammarFileName, newCode, offsets.ToArray(), FragmentMarkLength,
                    grammarInfo.Source);

                foreach (var rawFragment in rawFragments)
                    _mappedFragments.Add(rawFragment.ToMappedFragment(source));

                newGrammarFileName = Path.Combine(_runtimeDirectoryName, grammarFileName);
                if (!Directory.Exists(_runtimeDirectoryName))
                    Directory.CreateDirectory(_runtimeDirectoryName);
                File.WriteAllText(newGrammarFileName, newCode);
                outputDirectory = ".";
            }
            else
            {
                source = grammarInfo.Source;
                newGrammarFileName = Path.Combine(_grammar.Directory, grammarFileName);
                outputDirectory = _runtimeDirectoryName;
            }

            _grammarSources.Add(grammarFileName, source);

            return (newGrammarFileName, outputDirectory);
        }

        private void AddGeneratedFiles(GrammarInfo grammarInfo)
        {
            if (_result.HasErrors)
                return;

            var runtimeInfo = Runtime.GetRuntimeInfo();
            var grammarName = grammarInfo.Name ?? "";
            var generatedGrammarName = Runtime != Runtime.Go ? grammarName : grammarName.ToLowerInvariant();
            string? lexerFileName = null, parserFileName = null;

            string NormalizeFileName(string fileName, bool lexer)
            {
                string originalPostfix, resultFilePostfix;
                if (lexer)
                {
                    originalPostfix = Grammar.LexerPostfix;
                    resultFilePostfix = runtimeInfo.LexerFilePostfix;
                }
                else
                {
                    originalPostfix = Grammar.ParserPostfix;
                    resultFilePostfix = runtimeInfo.ParserFilePostfix;
                }
                if (Runtime == Runtime.Go && fileName.EndsWith(originalPostfix, StringComparison.OrdinalIgnoreCase))
                {
                    return fileName.Remove(fileName.Length - originalPostfix.Length) + resultFilePostfix;
                }
                return fileName;
            }

            if (grammarInfo.Type == GrammarFileType.Combined)
            {
                lexerFileName = generatedGrammarName + runtimeInfo.LexerFilePostfix;
                parserFileName = generatedGrammarName + runtimeInfo.ParserFilePostfix;
            }
            else
            {
                if (grammarInfo.Type == GrammarFileType.Lexer)
                {
                    lexerFileName = NormalizeFileName(generatedGrammarName, true);
                }
                else
                {
                    parserFileName = NormalizeFileName(generatedGrammarName, false);
                }
            }

            if (lexerFileName != null)
            {
                _runtimeFileInfos.Add(
                    Path.Combine(_runtimeDirectoryName, lexerFileName + "." + runtimeInfo.MainExtension),
                    new RuntimeFileInfo(RuntimeFileType.Generated, grammarInfo));
            }

            if (parserFileName != null)
            {
                _runtimeFileInfos.Add(
                    Path.Combine(_runtimeDirectoryName, parserFileName + "." + runtimeInfo.MainExtension),
                    new RuntimeFileInfo(RuntimeFileType.Generated, grammarInfo));
            }

            if (grammarInfo.Type.IsParser())
            {
                void GetGeneratedListenerOrVisitorFiles(bool visitor)
                {
                    string postfix = visitor ? runtimeInfo.VisitorPostfix : runtimeInfo.ListenerPostfix;
                    string? basePostfix = visitor ? runtimeInfo.BaseVisitorPostfix : runtimeInfo.BaseListenerPostfix;

                    _runtimeFileInfos.Add(
                        Path.Combine(_runtimeDirectoryName, generatedGrammarName + postfix + "." + runtimeInfo.MainExtension),
                        new RuntimeFileInfo(RuntimeFileType.GeneratedHelper, grammarInfo));
                    if (basePostfix != null)
                    {
                        _runtimeFileInfos.Add(
                            Path.Combine(_runtimeDirectoryName, generatedGrammarName + basePostfix + "." + runtimeInfo.MainExtension),
                            new RuntimeFileInfo(RuntimeFileType.GeneratedHelper, grammarInfo));
                    }
                }

                if (_result.IncludeListener)
                    GetGeneratedListenerOrVisitorFiles(false);

                if (_result.IncludeVisitor)
                    GetGeneratedListenerOrVisitorFiles(true);
            }
        }
    }
}