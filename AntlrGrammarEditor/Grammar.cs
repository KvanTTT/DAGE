using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrGrammarEditor
{
    public class Grammar
    {
        private static JsonConverter[] _jsonConverter = new JsonConverter[] { new StringEnumConverter() };
        private string _ageFileName;

        public string Name { get; set; }
        
        public string Root { get; set; }

        public HashSet<Runtime> Runtimes = new HashSet<Runtime>();

        public bool SeparatedLexerAndParser { get; set; }

        public bool CaseInsensitive { get; set; }

        public bool Preprocessor { get; set; }

        public bool PreprocessorCaseInsensitive { get; set; }

        public string PreprocessorRoot { get; set; }

        public bool PreprocessorSeparatedLexerAndParser { get; set; }

        public List<string> Files { get; set; } = new List<string>();

        [JsonIgnore]
        public string GrammarPath { get; private set; }

        [JsonIgnore]
        public string AgeFileName
        {
            get
            {
                return _ageFileName;
            }
            set
            {
                _ageFileName = value;
                GrammarPath = Path.GetDirectoryName(_ageFileName);
            }
        }

        public static Grammar Load(string fileName)
        {
            var result = JsonConvert.DeserializeObject<Grammar>(File.ReadAllText(fileName), _jsonConverter);
            result.AgeFileName = fileName;
            return result;
        }

        public void Save()
        {
            File.WriteAllText(AgeFileName, JsonConvert.SerializeObject(this, Formatting.Indented, _jsonConverter));
        }
    }
}
