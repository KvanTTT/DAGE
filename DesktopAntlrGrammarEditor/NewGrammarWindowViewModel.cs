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

                var fullGrammarDir = Path.GetFullPath(GrammarDirectory);
                var grammarPath = Path.Combine(fullGrammarDir, GrammarName);
                if (Preprocessor)
                {
                    if (PreprocessorSeparatedLexerAndParser)
                    {
                        grammar.Files.Add(grammarPath + GrammarFactory.PreprocessorPostfix + GrammarFactory.LexerPostfix + GrammarFactory.Extension);
                        grammar.Files.Add(grammarPath + GrammarFactory.PreprocessorPostfix + GrammarFactory.ParserPostfix + GrammarFactory.Extension);
                    }
                    else
                    {
                        grammar.Files.Add(grammarPath + GrammarFactory.PreprocessorPostfix + GrammarFactory.Extension);
                    }
                }
                if (SeparatedLexerAndParser)
                {
                    grammar.Files.Add(grammarPath + GrammarFactory.LexerPostfix + GrammarFactory.Extension);
                    grammar.Files.Add(grammarPath + GrammarFactory.ParserPostfix + GrammarFactory.Extension);
                }
                else
                {
                    grammar.Files.Add(grammarPath + GrammarFactory.Extension);
                }

                if (!Directory.Exists(fullGrammarDir))
                {
                    Directory.CreateDirectory(fullGrammarDir);
                }

                foreach (var file in grammar.Files)
                {
                    var shortFileName = Path.GetFileNameWithoutExtension(file);
                    var text = new StringBuilder();
                    if (shortFileName.Contains(GrammarFactory.LexerPostfix))
                    {
                        text.Append("lexer ");
                    }
                    else if (shortFileName.Contains(GrammarFactory.ParserPostfix))
                    {
                        text.Append("parser ");
                    }
                    text.AppendLine($"grammar {shortFileName};");
                    text.AppendLine();

                    if (shortFileName.Contains(GrammarFactory.ParserPostfix))
                    {
                        text.AppendLine($"options {{ tokenVocab = {shortFileName.Replace(GrammarFactory.ParserPostfix, GrammarFactory.LexerPostfix)}; }}");
                        text.AppendLine();
                    }

                    if (!shortFileName.Contains(GrammarFactory.LexerPostfix) && !string.IsNullOrEmpty(GrammarRoot))
                    {
                        text.AppendLine($"{(shortFileName.Contains(GrammarFactory.PreprocessorPostfix) ? PreprocessorGrammarRoot : GrammarRoot)}");
                        text.AppendLine("    : tokensOrRules* EOF");
                        text.AppendLine("    ;");
                        text.AppendLine();
                        text.AppendLine("tokensOrRules");
                        text.AppendLine("    : TOKEN+");
                        text.AppendLine("    ;");
                        text.AppendLine();
                    }

                    if (!shortFileName.Contains(GrammarFactory.ParserPostfix))
                    {
                        text.AppendLine("TOKEN: [a-z]+;");
                        text.AppendLine();
                    }

                    File.WriteAllText(file, text.ToString());
                }

                grammar.Runtimes = new HashSet<Runtime>() { Runtime };
                grammar.AgeFileName = Path.Combine(GrammarDirectory, grammar.Name) + ".age";
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
