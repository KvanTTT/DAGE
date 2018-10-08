using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AntlrGrammarEditor
{
    public static class Helpers
    {
        private static bool _javaInitialized;

        private static string javaVersion;

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

        public static bool IsIgnoreError(this DataReceivedEventArgs message)
        {
            return message.Data?.Contains("Picked up JAVA_TOOL_OPTIONS") == true;
        }
    }
}
