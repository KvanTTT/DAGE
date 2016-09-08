using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public static class Helpers
    {
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

        public static RuntimeInfo GetRuntimeInfo(this Runtime runtime)
        {
            return RuntimeInfo.Runtimes[runtime];
        }

        public static string GetCSharpCompilerPath()
        {
            return Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "csc.exe");
        }

        public static string GetJavaExePath(string exeName)
        {
            string result = CheckFile(Environment.SpecialFolder.ProgramFiles, true, exeName) ??
                            CheckFile(Environment.SpecialFolder.ProgramFiles, false, exeName) ??
                            CheckFile(Environment.SpecialFolder.ProgramFilesX86, true, exeName) ??
                            CheckFile(Environment.SpecialFolder.ProgramFilesX86, false, exeName);

            return result;
        }

        private static string CheckFile(Environment.SpecialFolder specialFolder, bool jdk, string exeName)
        {
            var javaFilesDir = Path.Combine(Environment.GetFolderPath(specialFolder), "java");
            var dirs = Directory.GetDirectories(javaFilesDir);
            
            dirs = dirs.Where(dir => Path.GetFileName(dir).StartsWith(jdk ? "jdk" : "jre")).ToArray();
            foreach (var dir in dirs)
            {
                var result = Path.Combine(dir, exeName);
                if (File.Exists(result))
                {
                    return result;
                }
            }

            return null;
        }
    }
}
