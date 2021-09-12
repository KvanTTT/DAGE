using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            $@"^(?<{TypeMark}>[^\(]+)\(\d+\): (?<{FileMark}>.+?):(?<{LineMark}>\d+):(?<{ColumnMark}>\d+): (?<{MessageMark}>.+)",
            RegexOptions.Compiled);
        private static readonly string FragmentMarkFormat = FragmentMarkWord + "{0:" + new string('0', FragmentMarkDigitsCount) + "}";
        private static readonly int FragmentMarkLength = new OpenCloseMark(string.Format(FragmentMarkFormat, 0),
            RuntimeInfo.Runtimes[Runtime.Java], FragmentMarkSuffix).OpenMark.Length;

        private static readonly Dictionary<Encoding, string> Encodings = new()
        {
            [Encoding.Default] = "default",
            [Encoding.Utf8] = "utf8"
        };

        private readonly Grammar _grammar;
        private readonly List<MappedFragment> _mappedFragments = new();
        private readonly Dictionary<string, Source> _grammarSources = new();
        private readonly ParserGeneratedState _result;

        private string _runtimeDirectoryName = "";

        public Runtime Runtime { get; }

        public string? PackageName { get; }

        public bool GenerateListener { get; }

        public bool GenerateVisitor { get; }

        public string? GeneratorTool { get; set; }

        public Encoding Encoding { get; }

        public ParserGenerator(GrammarCheckedState state, Runtime runtime, string? packageName, bool generateListener, bool generateVisitor,
            Encoding encoding = Encoding.Utf8)
        {
            PackageName = packageName;
            GenerateListener = generateListener;
            GenerateVisitor = generateVisitor;
            _grammar = state.InputState.Grammar;
            _result = new ParserGeneratedState(state, packageName, runtime, generateListener, generateVisitor, _mappedFragments, _grammarSources);
            Runtime = runtime;
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
                _runtimeDirectoryName = Path.Combine(HelperDirectoryName, _grammar.Name, Runtime.ToString());

                if ((Runtime == Runtime.Java || Runtime == Runtime.Go) && !string.IsNullOrWhiteSpace(PackageName))
                    _runtimeDirectoryName = Path.Combine(_runtimeDirectoryName, PackageName);

                if (Directory.Exists(_runtimeDirectoryName))
                    Directory.Delete(_runtimeDirectoryName, true);

                Directory.CreateDirectory(_runtimeDirectoryName);

                cancellationToken.ThrowIfCancellationRequested();

                var runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(Runtime);

                var jarGenerator = GeneratorTool ?? Path.Combine("Generators", runtimeInfo.JarGenerator);
                foreach (string grammarFileName in state.InputState.Grammar.Files)
                {
                    string extension = Path.GetExtension(grammarFileName);
                    if (extension != Grammar.AntlrDotExt)
                        continue;

                    var grammarInfo = state.GrammarInfos[grammarFileName];
                    var (grammarPath, outputDirectory) = InsertFragmentMarks(grammarFileName, grammarInfo);

                    var arguments = new StringBuilder();
                    arguments.Append($@"-jar ""{jarGenerator}"" ""{grammarPath}""");
                    arguments.Append($@" -o ""{outputDirectory}""");
                    arguments.Append($" -Dlanguage={runtimeInfo.DLanguage}");
                    arguments.Append($" -encoding {Encodings[Encoding]}");
                    arguments.Append(GenerateVisitor ? " -visitor" : " -no-visitor");
                    arguments.Append(GenerateListener ? " -listener" : " -no-listener");

                    if (!string.IsNullOrWhiteSpace(PackageName))
                    {
                        arguments.Append(" -package ");
                        arguments.Append(PackageName);
                    }
                    else if (Runtime == Runtime.Go)
                    {
                        arguments.Append(" -package main");
                    }

                    if (grammarFileName.Contains(Grammar.LexerPostfix) && state.LexerSuperClass != null)
                    {
                        arguments.Append(" -DsuperClass=");
                        arguments.Append(state.LexerSuperClass);
                    }

                    if (grammarFileName.Contains(Grammar.ParserPostfix) && state.ParserSuperClass != null)
                    {
                        arguments.Append(" -DsuperClass=");
                        arguments.Append(state.ParserSuperClass);
                    }

                    var argumentsString = arguments.ToString();
                    _result.Command = "java " + argumentsString;
                    processor = new Processor("java", argumentsString, ".");
                    processor.CancellationToken = cancellationToken;
                    processor.ErrorDataReceived += ParserGeneration_ErrorDataReceived;
                    processor.OutputDataReceived += ParserGeneration_OutputDataReceived;

                    processor.Start();

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
                var match = ParserGeneratorMessageRegex.Match(e.Data);
                var groups = match.Groups;
                var grammarFileName = Path.GetFileName(groups[FileMark].Value);
                var line = int.Parse(groups[LineMark].Value);
                var column = int.Parse(groups[ColumnMark].Value) + LineColumnTextSpan.StartColumn;
                var message = groups[MessageMark].Value;
                var diagnosisType = groups[TypeMark].Value == "warning" ? DiagnosisType.Warning : DiagnosisType.Error;

                var textSpan = _result.GetOriginalTextSpanForLineColumn(grammarFileName, line, column);
                var diagnosis = new Diagnosis(textSpan, message, WorkflowStage.ParserGenerated, diagnosisType);
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

        private (string, string) InsertFragmentMarks(string grammarFileName, GrammarInfo grammarInfo)
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

            var fileName = grammarInfo.Source.Name;
            var runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(Runtime.Java);
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

            string grammarPath, outputDirectory;
            Source source;

            if (result != null)
            {
                result.Append(sourceSpan.Slice(previousIndex));
                var newCode = result.ToString();
                source = new SourceWithMarks(fileName, newCode, offsets.ToArray(), FragmentMarkLength,
                    grammarInfo.Source);

                foreach (var rawFragment in rawFragments)
                    _mappedFragments.Add(rawFragment.ToMappedFragment(source));

                grammarPath = Path.Combine(_runtimeDirectoryName, grammarFileName);
                if (!Directory.Exists(_runtimeDirectoryName))
                    Directory.CreateDirectory(_runtimeDirectoryName);
                File.WriteAllText(grammarPath, newCode);
                outputDirectory = ".";
            }
            else
            {
                source = grammarInfo.Source;
                grammarPath = Path.Combine(_grammar.Directory, grammarFileName);
                outputDirectory = _runtimeDirectoryName;
            }

            _grammarSources.Add(fileName, source);

            return (grammarPath, outputDirectory);
        }
    }
}