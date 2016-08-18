using AntlrGrammarEditor;
using Avalonia;
using Avalonia.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.IO;

namespace DesktopAntlrGrammarEditor
{
    public class Settings
    {
        private static string settingsFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AntlrGrammarEditor.json");
        private static readonly object saveLock = new object();

        public GrammarType GrammarType { get; set; } = GrammarType.Single;

        public string GrammarText { get; set; } = "";

        public double Left { get; set; } = -1;

        public double Top { get; set; } = -1;

        public double Width { get; set; } = -1;

        public double Height { get; set; } = -1;

        public WindowState WindowState { get; set; } = WindowState.Normal;

        [JsonConverter(typeof(StringEnumConverter))]
        public Runtime SelectedRuntime { get; set; } = Runtime.Java;

        public string Root { get; set; }

        public string Text { get; set; } = "";

        public static Settings Load()
        {
            if (File.Exists(settingsFileName))
            {
                try
                {
                    var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(settingsFileName)) ?? new Settings();
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
            lock (saveLock)
            {
                File.WriteAllText(settingsFileName, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
        }
    }
}
