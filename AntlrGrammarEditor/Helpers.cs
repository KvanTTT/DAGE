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

            string result = null;
            var jdkDir = dirs.FirstOrDefault(dir => Path.GetFileName(dir).StartsWith(jdk ? "jdk" : "jre"));
            if (jdkDir != null)
            {
                result = Path.Combine(jdkDir, exeName);
                if (File.Exists(result))
                {
                    return result;
                }
            }

            return result;
        }
    }
}
