using AntlrGrammarEditor;
using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace DesktopAntlrGrammarEditor
{
    public class NewGrammarWindowViewModel : ReactiveObject
    {
        private Window _window;

        public NewGrammarWindowViewModel(Window window)
        {
            _window = window;
            
            OkCommand.Subscribe(_ =>
            {
                var grammar = new Grammar
                {
                    Name = GrammarName,
                    CaseInsensitive = CaseInsensitive,
                    PreprocessorCaseInsensitive = PreprocessorCaseInsensitive
                };
                
                if (Preprocessor)
                {
                    if (PreprocessorSeparatedLexerAndParser)
                    {
                        grammar.Files.Add(GrammarName + GrammarFactory.PreprocessorPostfix + GrammarFactory.LexerPostfix + GrammarFactory.Extension);
                        grammar.Files.Add(GrammarName + GrammarFactory.PreprocessorPostfix + GrammarFactory.ParserPostfix + GrammarFactory.Extension);
                    }
                    else
                    {
                        grammar.Files.Add(GrammarName + GrammarFactory.PreprocessorPostfix + GrammarFactory.Extension);
                    }
                }
                if (SeparatedLexerAndParser)
                {
                    grammar.Files.Add(GrammarName + GrammarFactory.LexerPostfix + GrammarFactory.Extension);
                    grammar.Files.Add(GrammarName + GrammarFactory.ParserPostfix + GrammarFactory.Extension);
                }
                else
                {
                    grammar.Files.Add(GrammarName + GrammarFactory.Extension);
                }

                var fullGrammarDir = Path.GetFullPath(GrammarDirectory);
                if (!Directory.Exists(fullGrammarDir))
                {
                    Directory.CreateDirectory(fullGrammarDir);
                }

                foreach (var file in grammar.Files)
                {
                    var fileWithoutExtension = Path.GetFileNameWithoutExtension(file);
                    var text = new StringBuilder();
                    if (fileWithoutExtension.Contains(GrammarFactory.LexerPostfix))
                    {
                        text.Append("lexer ");
                    }
                    else if (fileWithoutExtension.Contains(GrammarFactory.ParserPostfix))
                    {
                        text.Append("parser ");
                    }
                    text.AppendLine($"grammar {fileWithoutExtension};");
                    text.AppendLine();

                    if (fileWithoutExtension.Contains(GrammarFactory.ParserPostfix))
                    {
                        text.AppendLine($"options {{ tokenVocab = {fileWithoutExtension.Replace(GrammarFactory.ParserPostfix, GrammarFactory.LexerPostfix)}; }}");
                        text.AppendLine();
                    }

                    if (!fileWithoutExtension.Contains(GrammarFactory.LexerPostfix) && !string.IsNullOrEmpty(GrammarRoot))
                    {
                        text.AppendLine($"{(fileWithoutExtension.Contains(GrammarFactory.PreprocessorPostfix) ? PreprocessorGrammarRoot : GrammarRoot)}");
                        text.AppendLine("    : tokensOrRules* EOF");
                        text.AppendLine("    ;");
                        text.AppendLine();
                        text.AppendLine("tokensOrRules");
                        text.AppendLine("    : TOKEN+");
                        text.AppendLine("    ;");
                        text.AppendLine();
                    }

                    if (!fileWithoutExtension.Contains(GrammarFactory.ParserPostfix))
                    {
                        text.AppendLine("TOKEN: [a-z]+;");
                        text.AppendLine();
                    }

                    File.WriteAllText(Path.Combine(fullGrammarDir, file), text.ToString());
                }

                grammar.Runtimes = new HashSet<Runtime>() { Runtime };
                grammar.AgeFileName = Path.Combine(fullGrammarDir, grammar.Name) + ".age";
                grammar.Save();

                _window.Close(grammar);
            });

            CancelCommand.Subscribe(_ =>
            {
                _window.Close(null);
            });
        }

        public ReactiveCommand<object> OkCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> CancelCommand { get; } = ReactiveCommand.Create();

        public string GrammarName { get; set; } = GrammarFactory.DefaultGrammarName;

        public string GrammarDirectory { get; set; } = GrammarFactory.DefaultGrammarName;

        public string GrammarRoot { get; set; } = GrammarFactory.DefaultRootRule;

        public Runtime Runtime { get; set; } = Runtime.CSharpSharwell;

        public ObservableCollection<Runtime> Runtimes { get; } = new ObservableCollection<Runtime>((Runtime[])Enum.GetValues(typeof(Runtime)));

        public bool SeparatedLexerAndParser { get; set; }

        public bool CaseInsensitive { get; set; }

        public bool Preprocessor { get; set; }

        public string PreprocessorGrammarRoot { get; set; } = GrammarFactory.DefaultPreprocessorRootRule;

        public bool PreprocessorSeparatedLexerAndParser { get; set; }

        public bool PreprocessorCaseInsensitive { get; set; }
    }
}
