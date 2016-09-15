using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public static class WinHelpers
    {
        public static string TryGetPythonPath()
        {
            var result = ReadRegistryKey(@"HKLM\SOFTWARE\Python\PythonCore\versionnumber\", "InstallPath");
            return result;
        }

        public static string ReadRegistryKey(string path, string key)
        {
            try
            {
                RegistryKey regKey = Registry.LocalMachine;
                regKey = regKey.OpenSubKey(path);

                if (regKey != null)
                {
                    return regKey.GetValue(key).ToString();
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
