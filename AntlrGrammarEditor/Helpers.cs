﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

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

            return runtime.ToString();
        }

        public static bool IsCSharpRuntime(this Runtime runtime)
        {
            return runtime == Runtime.CSharpOptimized || runtime == Runtime.CSharpStandard;
        }
    }
}
