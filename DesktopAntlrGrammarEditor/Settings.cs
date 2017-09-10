using AntlrGrammarEditor;
using Avalonia;
using Avalonia.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;

namespace DesktopAntlrGrammarEditor
{
    public class Settings
    {
        private static JsonConverter[] _converters = new JsonConverter[] { new StringEnumConverter() };
        private static string _settingsFileName;
        private static readonly object _saveLock = new object();

        public static string Directory { get; private set; }

        public double Left { get; set; } = -1;

        public double Top { get; set; } = -1;

        public double Width { get; set; } = -1;

        public double Height { get; set; } = -1;

        public WindowState WindowState { get; set; } = WindowState.Normal;

        // Antlr Grammar Editor FileName
        public string AgeFileName { get; set; }

        public string OpenedGrammarFile { get; set; }

        public string OpenedTextFile { get; set; }

        public bool Autoprocessing { get; set; } = false;

        public string JavaPath { get; set; }

        public bool IsTokensExpanded { get; set; } = false;

        public bool IsParseTreeExpanded { get; set; } = false;

        public Dictionary<Runtime, string> CompilerPaths { get; set; } = new Dictionary<Runtime, string>();

        static Settings()
        {
            Directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DAGE");
            if (!System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.CreateDirectory(Directory);
            }
            _settingsFileName = Path.Combine(Directory, "AntlrGrammarEditor.json");
        }

        public static Settings Load()
        {
            if (File.Exists(_settingsFileName))
            {
                try
                {
                    var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(_settingsFileName), _converters) ?? new Settings();
                    return settings;
                }
                catch
                {
                    return new Settings();
                }
            }
            else
            {
                return new Settings();
            }
        }

        public void Save()
        {
            lock (_saveLock)
            {
                File.WriteAllText(_settingsFileName, JsonConvert.SerializeObject(this, Formatting.Indented, _converters));
            }
        }
    }
}
