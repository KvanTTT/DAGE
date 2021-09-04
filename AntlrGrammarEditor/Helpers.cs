using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using AntlrGrammarEditor.Sources;

namespace AntlrGrammarEditor
{
    public static class Helpers
    {
        private static bool _javaInitialized;

        private static string? _javaVersion;

        public static string RuntimesPath { get; private set; } = "";

        public const string FileMark = "file";
        public const string LineMark = "line";
        public const string ColumnMark = "column";
        public const string TypeMark = "type";
        public const string MessageMark = "message";

        public static readonly Regex JavaScriptWarningMarker =
            new ($@"^\(node:\d+\) \w*?Warning: (?<{MessageMark}>.+)", RegexOptions.Compiled);

        public const string JavaScriptIgnoreMessage =
            "(Use `node --trace-warnings ...` to show where the warning was created)";

        static Helpers()
        {
            GetRuntimesPath();
        }

        static void GetRuntimesPath([CallerFilePath] string? thisFilePath = null)
        {
            RuntimesPath = Path.Combine(Path.GetDirectoryName(thisFilePath) ?? "", "AntlrRuntimes");
        }

        public static string? JavaVersion
        {
            get
            {
                if (!_javaInitialized)
                {
                    _javaInitialized = true;

                    try
                    {
                        var processor = new Processor("java", "-version");
                        string version = "";
                        processor.ErrorDataReceived += (sender, e) =>
                        {
                            if (!e.IsIgnoredMessage(Runtime.Java))
                                version += e.Data + Environment.NewLine;
                        };
                        processor.Start();
                        _javaVersion = version.Trim();
                    }
                    catch
                    {
                        _javaVersion = null;
                    }
                }

                return _javaVersion;
            }
        }

        public static string FixEncoding(string str)
        {
            string result = str;
            var bytes = Encoding.Default.GetBytes(result);
            using (var stream = new MemoryStream(bytes))
            {
                var charsetDetector = new Ude.CharsetDetector();
                charsetDetector.Feed(stream);
                charsetDetector.DataEnd();
                if (charsetDetector.Charset != null)
                {
                    var detectedEncoding = Encoding.GetEncoding(charsetDetector.Charset);
                    result = detectedEncoding.GetString(bytes);
                }
            }
            return result;
        }

        public static bool IsIgnoredMessage(this DataReceivedEventArgs message, Runtime runtime)
        {
            var data = message.Data;

            if (string.IsNullOrWhiteSpace(data))
            {
                return true;
            }

            if (runtime == Runtime.Java)
            {
                return data.Contains("Picked up JAVA_TOOL_OPTIONS");
            }

            if (runtime == Runtime.JavaScript)
            {
                return JavaScriptWarningMarker.IsMatch(data) || data == JavaScriptIgnoreMessage;
            }

            return false;
        }

        public static string GetGeneralRuntimeName(this Runtime runtime)
        {
            if (runtime.IsCSharpRuntime())
            {
                return "CSharp";
            }

            if (runtime.IsPythonRuntime())
            {
                return "Python";
            }

            return runtime.ToString();
        }

        public static bool IsCSharpRuntime(this Runtime runtime)
        {
            return runtime == Runtime.CSharpOptimized || runtime == Runtime.CSharpStandard;
        }

        public static bool IsPythonRuntime(this Runtime runtime)
        {
            return runtime == Runtime.Python2 || runtime == Runtime.Python3;
        }

        public static string FormatErrorMessage(Source source, int line, int charPositionInLine, string msg, bool isWarning = false)
        {
            return $"{(isWarning ? "Warning" : "Error")}: {Path.GetFileName(source.Name)}:{line}:{charPositionInLine}: {msg}";
        }
    }
}
