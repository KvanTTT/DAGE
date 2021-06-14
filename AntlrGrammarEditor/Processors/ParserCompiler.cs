using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AntlrGrammarEditor.WorkflowState;

namespace AntlrGrammarEditor.Processors
{
    public class ParserCompiler : StageProcessor
    {
        private const string CompilerHelperFileName = "AntlrCompileTest";
        private const string TemplateGrammarName = "__TemplateGrammarName__";
        private const string CaseInsensitiveBlock = "AntlrCaseInsensitive";
        private const string RuntimesPath = "__RuntimesPath__";

        public const string RuntimesDirName = "AntlrRuntimes";

        private const string PackageNamePart = "/*$PackageName$*/";
        private const string PartDart = @"/*$Part$*/";
        private static readonly Regex ParserPartStart = new Regex(@"/\*\$ParserPart\*/", RegexOptions.Compiled);
        private static readonly Regex ParserPartEnd = new Regex(@"/\*ParserPart\$\*/", RegexOptions.Compiled);
        private static readonly Regex ParserPartStartPython = new Regex(@"'''\$ParserPart'''", RegexOptions.Compiled);
        private static readonly Regex ParserPartEndPython = new Regex(@"'''ParserPart\$'''", RegexOptions.Compiled);
        private static readonly Regex ParserIncludeStartPython = new Regex(@"'''\$ParserInclude'''", RegexOptions.Compiled);
        private static readonly Regex ParserIncludeEndPython = new Regex(@"'''ParserInclude\$'''", RegexOptions.Compiled);
        private static readonly Regex LexerIncludeStartDart = new Regex(@"/\*\$LexerInclude\*/", RegexOptions.Compiled);
        private static readonly Regex LexerIncludeEndDart = new Regex(@"/\*LexerInclude\$\*/", RegexOptions.Compiled);
        private static readonly Regex ParserIncludeStartJavaScriptGoPhpDart = new Regex(@"/\*\$ParserInclude\*/", RegexOptions.Compiled);
        private static readonly Regex ParserIncludeEndJavaScriptGoPhpDart = new Regex(@"/\*ParserInclude\$\*/", RegexOptions.Compiled);

        private static readonly Regex DartErrorMarker = new Regex(@"^([^:]+):(\d+):(\d+): ([^:]+): (.+)", RegexOptions.Compiled);

        private Grammar _grammar;
        private ParserCompiledState _result;
        private List<string> _buffer;
        private Dictionary<string, List<TextSpanMapping>> _grammarCodeMapping;
        private RuntimeInfo _currentRuntimeInfo;
        private HashSet<string> _processedMessages;

        public string RuntimeLibrary { get; set; }

        public ParserCompiler()
        {
        }

        public ParserCompiledState Compile(ParserGeneratedState state,
            CancellationToken cancellationToken = default)
        {
            _grammar = state.GrammarCheckedState.InputState.Grammar;
            Runtime runtime = state.Runtime;

            _result = new ParserCompiledState(state);

            _currentRuntimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(runtime);

            Processor processor = null;
            try
            {
                string runtimeSource = runtime.GetGeneralRuntimeName();
                string runtimeDir = Path.Combine(RuntimesDirName, runtimeSource);
                string runtimeLibraryPath = RuntimeLibrary ?? Path.Combine(runtimeDir, _currentRuntimeInfo.RuntimeLibrary);
                string workingDirectory = Path.Combine(ParserGenerator.HelperDirectoryName, _grammar.Name, runtime.ToString());

                var generatedFiles = new List<string>();
                _grammarCodeMapping = new Dictionary<string, List<TextSpanMapping>>();

                string generatedGrammarName =
                    runtime != Runtime.Go ? _grammar.Name : _grammar.Name.ToLowerInvariant();

                if (_grammar.Type != GrammarType.Lexer)
                {
                    GetGeneratedFileNames(state.GrammarCheckedState, generatedGrammarName, workingDirectory, generatedFiles, false);
                }

                GetGeneratedFileNames(state.GrammarCheckedState, generatedGrammarName, workingDirectory, generatedFiles, true);

                CopyCompiledSources(runtime, workingDirectory);

                if (_grammar.Type != GrammarType.Lexer)
                {
                    if (state.IncludeListener)
                    {
                        GetGeneratedListenerOrVisitorFiles(generatedGrammarName, workingDirectory, generatedFiles, false);
                    }
                    if (state.IncludeVisitor)
                    {
                        GetGeneratedListenerOrVisitorFiles(generatedGrammarName, workingDirectory, generatedFiles, true);
                    }
                }

                string arguments = "";

                switch (runtime)
                {
                    case Runtime.CSharpOptimized:
                    case Runtime.CSharpStandard:
                        arguments = PrepareCSharpFiles(workingDirectory, runtimeDir);
                        break;

                    case Runtime.Java:
                        arguments = PrepareJavaFiles(generatedFiles, runtimeDir, workingDirectory, runtimeLibraryPath);
                        break;

                    case Runtime.Python2:
                    case Runtime.Python3:
                        arguments = PreparePythonFiles(generatedFiles, runtimeDir, workingDirectory);
                        break;

                    case Runtime.JavaScript:
                        arguments = PrepareJavaScriptFiles(generatedFiles, workingDirectory, runtimeDir);
                        break;

                    case Runtime.Go:
                        arguments = PrepareGoFiles(generatedFiles, runtimeDir, workingDirectory);
                        break;

                    case Runtime.Php:
                        arguments = PreparePhpFiles(generatedFiles, runtimeDir, workingDirectory);
                        break;

                    case Runtime.Dart:
                        arguments = PrepareDartFiles(generatedFiles, runtimeDir, workingDirectory);
                        // Get dependencies
                        var dependenciesProcessor = new Processor(_currentRuntimeInfo.RuntimeToolName, "pub get",
                            workingDirectory);
                        // TODO: handle dependencies warnings and errors
                        dependenciesProcessor.Start();
                        break;
                }

                PrepareParserCode(workingDirectory, runtimeDir);

                _buffer = new List<string>();
                _processedMessages = new HashSet<string>();

                _result.Command = _currentRuntimeInfo.RuntimeToolName + " " + arguments;
                processor = new Processor(_currentRuntimeInfo.RuntimeToolName, arguments, workingDirectory);
                processor.CancellationToken = cancellationToken;
                processor.ErrorDataReceived += ParserCompilation_ErrorDataReceived;
                processor.OutputDataReceived += ParserCompilation_OutputDataReceived;

                processor.Start();

                if (_buffer.Count > 0)
                {
                    if (runtime.IsPythonRuntime())
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
                    AddError(new ParsingError(ex, WorkflowStage.ParserCompiled));
                }
            }
            finally
            {
                processor?.Dispose();
            }

            return _result;
        }

        private void GetGeneratedFileNames(GrammarCheckedState grammarCheckedState, string generatedGrammarName, string workingDirectory,
            List<string> generatedFiles, bool lexer)
        {
            string grammarNameExt;

            if (_grammar.Type == GrammarType.Combined)
            {
                grammarNameExt = _grammar.Files.FirstOrDefault(file => Path.GetExtension(file)
                    .Equals(Grammar.AntlrDotExt, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                string postfix = lexer ? Grammar.LexerPostfix : Grammar.ParserPostfix;
                grammarNameExt = _grammar.Files.FirstOrDefault(file => file.Contains(postfix)
                    && Path.GetExtension(file).Equals(Grammar.AntlrDotExt, StringComparison.OrdinalIgnoreCase));
            }

            string shortGeneratedFile = generatedGrammarName +
                                        (lexer ? _currentRuntimeInfo.LexerPostfix : _currentRuntimeInfo.ParserPostfix) +
                                        "." + _currentRuntimeInfo.Extensions[0];

            string generatedFileDir = workingDirectory;
            Runtime runtime = _currentRuntimeInfo.Runtime;
            if ((runtime == Runtime.Java || runtime == Runtime.Go) && !string.IsNullOrWhiteSpace(_result.ParserGeneratedState.PackageName))
            {
                generatedFileDir = Path.Combine(generatedFileDir, _result.ParserGeneratedState.PackageName);
            }
            string generatedFile = Path.Combine(generatedFileDir, shortGeneratedFile);
            generatedFiles.Add(generatedFile);
            CodeSource codeSource = new CodeSource(generatedFile, File.ReadAllText(generatedFile));
            _grammarCodeMapping[shortGeneratedFile] = TextHelpers.Map(grammarCheckedState.GrammarActionsTextSpan[grammarNameExt], codeSource, lexer);
        }

        private void CopyCompiledSources(Runtime runtime, string workingDirectory)
        {
            RuntimeInfo runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(runtime);
            string extension = runtimeInfo.Extensions[0];

            foreach (string fileName in _grammar.Files)
            {
                if (Path.GetExtension(fileName).Equals("." + extension, StringComparison.OrdinalIgnoreCase))
                {
                    string sourceFileName = Path.Combine(_grammar.Directory, fileName);

                    string shortFileName = Path.GetFileName(fileName);

                    if ((runtimeInfo.Runtime == Runtime.Java || runtimeInfo.Runtime == Runtime.Go) &&
                        !string.IsNullOrWhiteSpace(_result.ParserGeneratedState.PackageName))
                    {
                        shortFileName = Path.Combine(_result.ParserGeneratedState.PackageName, shortFileName);
                    }

                    string destFileName = Path.Combine(workingDirectory, shortFileName);

                    File.Copy(sourceFileName, destFileName, true);
                }
            }
        }

        private void GetGeneratedListenerOrVisitorFiles(string generatedGrammarName, string workingDirectory,
            List<string> generatedFiles, bool visitor)
        {
            string postfix = visitor ? _currentRuntimeInfo.VisitorPostfix : _currentRuntimeInfo.ListenerPostfix;
            string basePostfix = visitor ? _currentRuntimeInfo.BaseVisitorPostfix : _currentRuntimeInfo.BaseListenerPostfix;

            generatedFiles.Add(Path.Combine(workingDirectory,
                generatedGrammarName + postfix + "." + _currentRuntimeInfo.Extensions[0]));
            if (basePostfix != null)
            {
                generatedFiles.Add(Path.Combine(workingDirectory,
                    generatedGrammarName + basePostfix + "." + _currentRuntimeInfo.Extensions[0]));
            }
        }

        private string PrepareCSharpFiles(string workingDirectory, string runtimeDir)
        {
            string antlrCaseInsensitivePath = Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.cs");
            if (_grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.cs"), antlrCaseInsensitivePath, true);
            }

            File.Copy(Path.Combine(runtimeDir, "Program.cs"), Path.Combine(workingDirectory, "Program.cs"), true);
            File.Copy(Path.Combine(runtimeDir, "AssemblyInfo.cs"), Path.Combine(workingDirectory, "AssemblyInfo.cs"), true);

            var projectContent = File.ReadAllText(Path.Combine(runtimeDir, "Project.csproj"));
            projectContent = projectContent.Replace("<DefineConstants></DefineConstants>",
                $"<DefineConstants>{_currentRuntimeInfo.Runtime}</DefineConstants>");
            File.WriteAllText(Path.Combine(workingDirectory, $"{_grammar.Name}.csproj"), projectContent);

            return "build";
        }

        private string PrepareJavaFiles(List<string> generatedFiles, string runtimeDir, string workingDirectory,
            string runtimeLibraryPath)
        {
            var compiledFiles = new StringBuilder();

            string packageName = _result.ParserGeneratedState.PackageName ?? "";

            compiledFiles.Append('"' + _currentRuntimeInfo.MainFile + '"');

            if (_grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                compiledFiles.Append(" \"AntlrCaseInsensitiveInputStream.java\"");
                File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.java"),
                    Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.java"), true);
            }

            var filesToCompile =
                Directory.GetFiles(Path.Combine(workingDirectory, packageName), "*.java");

            foreach (string helperFile in filesToCompile)
            {
                compiledFiles.Append(" \"");

                if (!string.IsNullOrEmpty(packageName))
                {
                    compiledFiles.Append(_result.ParserGeneratedState.PackageName);
                    compiledFiles.Append(Path.DirectorySeparatorChar);
                }

                compiledFiles.Append(Path.GetFileName(helperFile));

                compiledFiles.Append("\"");
            }

            return $@"-cp ""{Path.Combine("..", "..", "..", runtimeLibraryPath)}"" -Xlint:deprecation " + compiledFiles;
        }

        private string PreparePythonFiles(List<string> generatedFiles, string runtimeDir, string workingDirectory)
        {
            var stringBuilder = new StringBuilder();

            foreach (string file in generatedFiles)
            {
                var shortFileName = Path.GetFileNameWithoutExtension(file);
                stringBuilder.AppendLine($"from {shortFileName} import {shortFileName}");
            }

            if (_grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                string antlrCaseInsensitiveInputStream =
                    File.ReadAllText(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.py"));
                string superCall, strType, intType, boolType;

                if (_currentRuntimeInfo.Runtime == Runtime.Python2)
                {
                    superCall = "type(self), self";
                    strType = "";
                    intType = "";
                    boolType = "";
                }
                else
                {
                    superCall = "";
                    strType = ": str";
                    intType = ": int";
                    boolType = ": bool";
                }

                antlrCaseInsensitiveInputStream = antlrCaseInsensitiveInputStream
                    .Replace("'''SuperCall'''", superCall)
                    .Replace("''': str'''", strType)
                    .Replace("''': int'''", intType)
                    .Replace("''': bool'''", boolType);

                File.WriteAllText(Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.py"),
                    antlrCaseInsensitiveInputStream);
            }

            var compileTestFileName = CreateHelperFile(workingDirectory, stringBuilder);

            string arguments = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arguments += _currentRuntimeInfo.Runtime == Runtime.Python2 ? "-2 " : "-3 ";
            }
            arguments += compileTestFileName;

            return arguments;
        }

        private string PrepareJavaScriptFiles(List<string> generatedFiles, string workingDirectory, string runtimeDir)
        {
            var stringBuilder = new StringBuilder();
            foreach (string file in generatedFiles)
            {
                var shortFileName = Path.GetFileNameWithoutExtension(file);
                stringBuilder.AppendLine($"import {shortFileName} from './{shortFileName}.js';");
            }

            string compileTestFileName = CreateHelperFile(workingDirectory, stringBuilder);

            File.Copy(Path.Combine(runtimeDir, "package.json"), Path.Combine(workingDirectory, "package.json"), true);
            if (_grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.js"),
                    Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.js"), true);
            }

            return compileTestFileName;
        }

        private string PrepareGoFiles(List<string> generatedFiles, string runtimeDir, string workingDirectory)
        {
            var compiledFiles = new StringBuilder('"' + _currentRuntimeInfo.MainFile + "\"");

            if (string.IsNullOrWhiteSpace(_result.ParserGeneratedState.PackageName))
            {
                foreach (string generatedFile in generatedFiles)
                {
                    compiledFiles.Append($" \"{Path.GetFileName(generatedFile)}\"");
                }
            }

            if (_grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                compiledFiles.Append(" \"AntlrCaseInsensitiveInputStream.go\"");

                string sourceFileName = Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.go");
                string destFileName = Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.go");

                File.Copy(sourceFileName, destFileName, true);
            }

            return "build " + compiledFiles;
        }

        private string PreparePhpFiles(List<string> generatedFiles, string runtimeDir, string workingDirectory)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("<?php");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"require_once '{GetPhpAutoloadPath()}';");

            foreach (string file in generatedFiles)
            {
                var shortFileName = Path.GetFileNameWithoutExtension(file);
                stringBuilder.AppendLine($"require_once '{shortFileName}.php';");
            }

            string compileTestFileName = CreateHelperFile(workingDirectory, stringBuilder);

            if (_grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.php"),
                    Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.php"), true);
            }

            return compileTestFileName;
        }

        private string PrepareDartFiles(List<string> generatedFiles, string runtimeDir, string workingDirectory)
        {
            var stringBuilder = new StringBuilder();

            var packageName = _result.ParserGeneratedState.PackageName;
            var lexerOnlyAndNotEmptyPackageName =
                !string.IsNullOrEmpty(packageName) && _grammar.Type == GrammarType.Lexer;

            if (lexerOnlyAndNotEmptyPackageName)
            {
                stringBuilder.AppendLine($"library {packageName};");
                stringBuilder.AppendLine("import 'package:antlr4/antlr4.dart';");

                var lexerFile = generatedFiles.FirstOrDefault(file =>
                    Path.GetFileNameWithoutExtension(file).EndsWith(_currentRuntimeInfo.LexerPostfix));
                if (lexerFile != null)
                {
                    stringBuilder.AppendLine($"part '{Path.GetFileNameWithoutExtension(lexerFile)}.dart';");
                }
            }
            else
            {
                foreach (string file in generatedFiles)
                {
                    var shortFileName = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(packageName) || shortFileName.EndsWith(_currentRuntimeInfo.ParserPostfix))
                    {
                        stringBuilder.AppendLine($"import '{shortFileName}.dart';");
                    }
                }
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("void main() {}");

            string compileTestFileName = CreateHelperFile(workingDirectory, stringBuilder);

            File.Copy(Path.Combine(runtimeDir, "pubspec.yaml"), Path.Combine(workingDirectory, "pubspec.yaml"), true);
            if (_grammar.CaseInsensitiveType != CaseInsensitiveType.None)
            {
                File.Copy(Path.Combine(runtimeDir, "AntlrCaseInsensitiveInputStream.dart"),
                    Path.Combine(workingDirectory, "AntlrCaseInsensitiveInputStream.dart"), true);
            }

            return compileTestFileName;
        }

        private void PrepareParserCode(string workingDirectory, string runtimeDir)
        {
            string templateFile = Path.Combine(workingDirectory, _currentRuntimeInfo.MainFile);
            Runtime runtime = _currentRuntimeInfo.Runtime;

            string code = File.ReadAllText(Path.Combine(runtimeDir, _currentRuntimeInfo.MainFile));
            string packageName = _result.ParserGeneratedState.PackageName;

            code = code.Replace(TemplateGrammarName, _grammar.Name);

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
                    newPackageValue = $"use {packageName}\\{_grammar.Name}Lexer;";
                    if (_grammar.Type != GrammarType.Lexer)
                    {
                        newPackageValue += $"{Environment.NewLine}use {packageName}\\{_grammar.Name}Parser;";
                    }
                }
                else if (runtime == Runtime.Dart)
                {
                    if (_grammar.Type == GrammarType.Lexer)
                    {
                        newPackageValue = "library " + packageName + ";";
                    }
                }
            }

            code = code.Replace(PackageNamePart, newPackageValue);

            if (runtime == Runtime.Go)
            {
                code = code.Replace("/*$PackageName2$*/", isPackageNameEmpty ? "" : packageName + ".");
            }
            else if (runtime == Runtime.Php)
            {
                code = code.Replace(RuntimesPath, GetPhpAutoloadPath());
            }
            else if (runtime == Runtime.Dart)
            {
                code = code.Replace(PartDart, _grammar.Type == GrammarType.Lexer && !isPackageNameEmpty
                    ? $"part '{_grammar.Name}Lexer.dart';" : "");
            }

            string caseInsensitiveBlockMarker = GenerateCaseInsensitiveBlockMarker();

            if (_grammar.CaseInsensitiveType != CaseInsensitiveType.None)
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
                string isLowerBool = (_grammar.CaseInsensitiveType == CaseInsensitiveType.lower).ToString();
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

            Regex parserPartStart, parserPartEnd;

            if (runtime.IsPythonRuntime())
            {
                string newValue = runtime == Runtime.Python2
                    ? "print \"Tree \" + tree.toStringTree(recog=parser);"
                    : "print(\"Tree \", tree.toStringTree(recog=parser));";
                code = code.Replace("'''PrintTree'''", newValue);

                parserPartStart = ParserPartStartPython;
                parserPartEnd = ParserPartEndPython;

                code = RemoveCodeOrClearMarkers(code, ParserIncludeStartPython, ParserIncludeEndPython);
            }
            else
            {
                parserPartStart = ParserPartStart;
                parserPartEnd = ParserPartEnd;

                if (runtime == Runtime.JavaScript || runtime == Runtime.Go || runtime == Runtime.Php || runtime == Runtime.Dart)
                {
                    code = RemoveCodeOrClearMarkers(code, ParserIncludeStartJavaScriptGoPhpDart, ParserIncludeEndJavaScriptGoPhpDart);
                }

                if (runtime == Runtime.Dart)
                {
                    code = RemoveCodeOrClearMarkers(code, LexerIncludeStartDart, LexerIncludeEndDart,
                        () => !isPackageNameEmpty);
                }
            }

            code = RemoveCodeOrClearMarkers(code, parserPartStart, parserPartEnd);

            File.WriteAllText(templateFile, code);
        }

        private string RemoveCodeOrClearMarkers(string code, Regex startMarker, Regex endMarker,
            Func<bool> condition = null)
        {
            if (condition?.Invoke() ?? _grammar.Type == GrammarType.Lexer)
            {
                int parserStartIndex = startMarker.Match(code).Index;
                Match parserEndMatch = endMarker.Match(code);
                int parserEndIndex = parserEndMatch.Index + parserEndMatch.Length;

                code = code.Remove(parserStartIndex) + code.Substring(parserEndIndex);
            }
            else
            {
                code = startMarker.Replace(code, m => "");
                code = endMarker.Replace(code, m => "");
            }

            return code;
        }


        private string CreateHelperFile(string workingDirectory, StringBuilder stringBuilder)
        {
            string compileTestFileName = _currentRuntimeInfo.Runtime + CompilerHelperFileName + "." +
                                         _currentRuntimeInfo.Extensions[0];
            File.WriteAllText(Path.Combine(workingDirectory, compileTestFileName), stringBuilder.ToString());
            return compileTestFileName;
        }

        private string GenerateCaseInsensitiveBlockMarker() =>
            _currentRuntimeInfo.Runtime.IsPythonRuntime()
                ? $"'''{CaseInsensitiveBlock}'''"
                : $"/*{CaseInsensitiveBlock}*/";

        private string GetPhpAutoloadPath() =>
            Helpers.RuntimesPath.Replace("\\", "/") + "/Php/vendor/autoload.php";

        private void ParserCompilation_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Runtime runtime = _currentRuntimeInfo.Runtime;

                if (runtime.IsCSharpRuntime())
                {
                    AddCSharpError(e.Data);
                }
                else if (runtime == Runtime.Java)
                {
                    AddJavaError(e.Data);
                }
                else if (runtime.IsPythonRuntime())
                {
                    AddToBuffer(e.Data);
                }
                else if (runtime == Runtime.JavaScript)
                {
                    var match = Helpers.JavaScriptWarningMarker.Match(e.Data);
                    if (match.Success)
                    {
                        AddError(new ParsingError(e.Data.Substring(match.Length), CodeSource.Empty,
                            WorkflowStage.ParserCompiled,
                            true));
                    }
                    else
                    {
                        if (e.Data != Helpers.JavaScriptIgnoreMessage)
                        {
                            AddToBuffer(e.Data);
                        }
                    }
                }
                else if (runtime == Runtime.Go)
                {
                    AddGoError(e.Data);
                }
                else if (runtime == Runtime.Php)
                {
                    AddPhpError(e.Data);
                }
                else if (runtime == Runtime.Dart)
                {
                    AddDartError(e.Data);
                }
            }
        }

        private void AddToBuffer(string data)
        {
            lock (_buffer)
            {
                _buffer.Add(data);
            }
        }

        private void ParserCompilation_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                if (_processedMessages.Contains(e.Data))
                {
                    return;
                }

                _processedMessages.Add(e.Data);
                Console.WriteLine(e.Data);

                if (_currentRuntimeInfo.Runtime.IsCSharpRuntime())
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
                    // Lexer.cs(106,11): error CS0103: The  name 'a' does not exist in the current context
                    var parts = errorString.Split(new [] { ':' }, 2);
                    var firstPart = parts[0];
                    int leftParenInd = firstPart.IndexOf('(');
                    string codeFileName = firstPart.Remove(leftParenInd);
                    string grammarFileName = GetGrammarFromCodeFileName(_currentRuntimeInfo, codeFileName);
                    string lineColumnString = firstPart.Substring(leftParenInd);
                    lineColumnString = lineColumnString.Substring(1, lineColumnString.Length - 2); // Remove parenthesis.
                    var lineColumnParts = lineColumnString.Split(',');
                    int line = int.Parse(lineColumnParts[0]);
                    int column = int.Parse(lineColumnParts[1]);

                    error = GenerateError(data, codeFileName, line, column, parts[1]);
                }
                catch
                {
                    error = new ParsingError(errorString, grammarSource, WorkflowStage.ParserCompiled);
                }
                AddError(error);
            }
        }

        private void AddJavaError(string data)
        {
            if (data.Count(c => c == ':') >= 2)
            {
                ParsingError error;
                try
                {
                    // Format:
                    // Lexer.java:98: error: cannot find symbol
                    string[] parts = data.Split(new [] { ':' }, 4);

                    string rest = parts[3];
                    if (rest.StartsWith(" [deprecation] ANTLRInputStream"))
                    {
                        return;
                    }

                    string codeFileName = parts[0];
                    error = int.TryParse(parts[1], out int codeLine)
                        ? GenerateError(data, codeFileName, codeLine, 1, rest, parts[2].Trim() == "warning")
                        : null;
                }
                catch
                {
                    error = null;
                }

                if (error == null)
                {
                    error = new ParsingError(data, CodeSource.Empty, WorkflowStage.ParserCompiled);
                }

                AddError(error);
            }
            else
            {
                //AddError(new ParsingError(TextSpan.Empty, data, WorkflowStage.ParserCompiled));
            }
        }

        private ParsingError GenerateError(string data, string codeFileName, int line, int column, string rest, bool isWarning = false)
        {
            ParsingError error;
            TextSpan textSpan;

            if (_grammarCodeMapping.TryGetValue(codeFileName, out List<TextSpanMapping> textSpanMappings))
            {
                string grammarFileName = GetGrammarFromCodeFileName(_currentRuntimeInfo, codeFileName);
                textSpan = TextHelpers.GetSourceTextSpanForLine(textSpanMappings, line, grammarFileName);
                error = new ParsingError(textSpan, $"{grammarFileName}:{textSpan.GetLineColumn().BeginLine}:{rest}",
                    WorkflowStage.ParserCompiled, isWarning);
            }
            else
            {
                Dictionary<string, CodeSource> grammarFilesData = _result.ParserGeneratedState.GrammarCheckedState.GrammarFilesData;
                CodeSource codeSource =
                    grammarFilesData.FirstOrDefault(file => file.Key.EndsWith(codeFileName, StringComparison.OrdinalIgnoreCase)).Value;

                textSpan = codeSource != null
                    ? new LineColumnTextSpan(line, column, codeSource).GetTextSpan()
                    : TextSpan.Empty;
                error = new ParsingError(textSpan, data, WorkflowStage.ParserCompiled, isWarning);
            }

            return error;
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

                    if (_grammarCodeMapping.TryGetValue(codeFileName, out var mapping))
                    {
                        try
                        {
                            var lineStr = "\", line ";
                            lineStr = _buffer[i].Substring(_buffer[i].IndexOf(lineStr, StringComparison.Ordinal) + lineStr.Length);
                            int commaIndex = lineStr.IndexOf(',');
                            if (commaIndex != -1)
                            {
                                lineStr = lineStr.Remove(commaIndex);
                            }
                            int codeLine = int.Parse(lineStr);
                            grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.Python2], codeFileName);
                            errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine, grammarFileName);
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
                finalMessage += errorSpan.GetLineColumn().BeginLine + ":";
            }
            finalMessage += message == "" ? "Unknown Error" : message;
            AddError(new ParsingError(errorSpan, finalMessage, WorkflowStage.ParserCompiled));
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

            // (node:17616) ExperimentalWarning: The ESM module loader is experimental.
            string message = "";
            string grammarFileName = "";
            TextSpan errorSpan = TextSpan.Empty;
            string firstLine = _buffer[0];
            int semicolonLastIndex = firstLine.LastIndexOf(':');
            try
            {
                if (semicolonLastIndex != -1)
                {
                    string beforeLastPart = firstLine.Remove(semicolonLastIndex);
                    string lastPart = firstLine.Substring(semicolonLastIndex + 1);

                    string codeFileName = Path.GetFileName(beforeLastPart);
                    if (_grammarCodeMapping.TryGetValue(codeFileName, out List<TextSpanMapping> mapping) &&
                        int.TryParse(lastPart, out int codeLine))
                    {
                        grammarFileName =
                            GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.JavaScript], codeFileName);
                        errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine, grammarFileName);
                    }
                }
            }
            catch
            {
                // ignored
            }

            for (int i = 1; i < _buffer.Count; i++)
            {
                if (string.IsNullOrEmpty(_buffer[i]) && i + 1 < _buffer.Count)
                {
                    message = _buffer[i + 1];
                }
            }

            string finalMessage = "";
            if (grammarFileName != "")
            {
                finalMessage = grammarFileName + ":";
            }
            if (!errorSpan.IsEmpty)
            {
                finalMessage += errorSpan.GetLineColumn().BeginLine + ":";
            }
            finalMessage += message == "" ? "Unknown Error" : message;
            AddError(new ParsingError(errorSpan, finalMessage, WorkflowStage.ParserCompiled));
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
                var parts = data.Split(':');
                try
                {
                    string codeFileName = parts[0].Substring(2);
                    if (_grammarCodeMapping.TryGetValue(codeFileName, out var mapping))
                    {
                        int codeLine = int.Parse(parts[1]);
                        grammarFileName = GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.Go], codeFileName);
                        errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine, grammarFileName);
                    }
                    message = parts[3];
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
                    finalMessage += errorSpan.GetLineColumn().BeginLine + ":";
                }
                finalMessage += message == "" ? "Unknown Error" : message;
                AddError(new ParsingError(errorSpan, finalMessage, WorkflowStage.ParserCompiled));
            }
            else
            {
                AddError(new ParsingError(TextSpan.Empty, data, WorkflowStage.ParserCompiled));
            }
        }

        private void AddPhpError(string data)
        {
            // PHP Parse error:  syntax error, unexpected ';' in <file_name.php> on line 145
            var dataSpan = data.AsSpan();
            int messageIndex = data.IndexOf(':') + 1;
            const string inString = "in ";
            int inIndex = data.IndexOf(inString, messageIndex, StringComparison.InvariantCulture);
            string message = dataSpan.Slice(messageIndex, inIndex - messageIndex).Trim().ToString();

            const string onLineString = " on line ";
            int lastOnIndex = data.LastIndexOf(onLineString, StringComparison.InvariantCulture);
            int line = int.Parse(dataSpan.Slice(lastOnIndex + onLineString.Length).ToString());

            int fileNameIndex = inIndex + inString.Length;
            string fileName = dataSpan.Slice(fileNameIndex, lastOnIndex - fileNameIndex).ToString();

            var codeSource = new CodeSource(Path.GetFileNameWithoutExtension(fileName), File.ReadAllText(fileName)); // TODO: reuse existed files
            AddError(new ParsingError(line, LineColumnTextSpan.StartColumn, message, codeSource, WorkflowStage.ParserCompiled));
        }

        private void AddDartError(string data)
        {
            // TestParser.dart:64:9: Error: Expected an identifier, but got ';'.
            var match = DartErrorMarker.Match(data);
            if (match.Success)
            {
                var groups = match.Groups;
                var errorSpan = TextSpan.Empty;
                string codeFileName = Path.GetFileName(groups[1].Value);
                if (_grammarCodeMapping.TryGetValue(codeFileName, out List<TextSpanMapping> mapping) &&
                    int.TryParse(groups[2].Value, out int codeLine))
                {
                    var grammarFileName =
                        GetGrammarFromCodeFileName(RuntimeInfo.Runtimes[Runtime.Dart], codeFileName);
                    errorSpan = TextHelpers.GetSourceTextSpanForLine(mapping, codeLine, grammarFileName);
                }

                var message = groups[5].Value.Trim();
                AddError(new ParsingError(errorSpan, message, WorkflowStage.ParserCompiled, false));
            }
        }

        private string GetGrammarFromCodeFileName(RuntimeInfo runtimeInfo, string codeFileName)
        {
            string result = _grammar.Files.FirstOrDefault(file => file.EndsWith(codeFileName));
            if (result != null)
            {
                return result;
            }

            result = Path.GetFileNameWithoutExtension(codeFileName);

            if (_grammar.Type == GrammarType.Combined)
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

            return _grammar.Files.FirstOrDefault(file => file.EndsWith(result));
        }

        private void AddError(ParsingError error)
        {
            ErrorEvent?.Invoke(this, error);
            _result.Errors.Add(error);
        }
    }
}