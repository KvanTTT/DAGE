using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace AntlrGrammarEditor
{
    public static class Helpers
    {
        private static bool _javaInitialized;

        private static string javaVersion;

        public static string RuntimesPath { get; private set; }

        static Helpers()
        {
            GetRuntimesPath();
        }

        static void GetRuntimesPath([CallerFilePath] string thisFilePath = null)
        {
            RuntimesPath = Path.Combine(Path.GetDirectoryName(thisFilePath), "AntlrRuntimes");
        }

        public static string JavaVersion
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
                            if (!e.IsIgnoreJavaError())
                                version += e.Data + Environment.NewLine;
                        };
                        processor.Start();
                        javaVersion = version.Trim();
                    }
                    catch
                    {
                        javaVersion = null;
                    }
                }

                return javaVersion;
            }
        }

        public static string FixEncoding(string str)
        {
            string result = str;
            var bytes = Encoding.Default.GetBytes(result);
            using (var stream = new MemoryStream(bytes))
            {
                Ude.CharsetDetector charsetDetector = new Ude.CharsetDetector();
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

        public static bool IsIgnoreJavaError(this DataReceivedEventArgs message)
        {
            return message.Data?.Contains("Picked up JAVA_TOOL_OPTIONS") == true;
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
    }
}
