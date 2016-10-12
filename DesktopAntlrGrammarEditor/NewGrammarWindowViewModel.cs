using AntlrGrammarEditor;
using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace DesktopAntlrGrammarEditor
{
    public class NewGrammarWindowViewModel : ReactiveObject
    {
        private Window _window;
        private Grammar _grammar = GrammarFactory.CreateDefault();
        private string _grammarDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DAGE Grammars");

        public NewGrammarWindowViewModel(Window window)
        {
            _window = window;

            OkCommand.Subscribe(_ =>
            {
                GrammarFactory.FillGrammarFiles(_grammar, Path.Combine(GrammarDirectory, _grammar.Name), true);
                _window.Close(_grammar);
            });

            CancelCommand.Subscribe(_ =>
            {
                _window.Close(null);
            });

            SelectGrammarDirectory.Subscribe(async _ =>
            {
                var openFolderDialog = new OpenFolderDialog
                {
                    InitialDirectory = GrammarDirectory
                };
                var folderName = await openFolderDialog.ShowAsync(_window);
                if (!string.IsNullOrEmpty(folderName))
                {
                    GrammarDirectory = folderName;
                }
            });
        }

        public ReactiveCommand<object> OkCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> CancelCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> SelectGrammarDirectory { get; } = ReactiveCommand.Create();

        public string GrammarName
        {
            get
            {
                return _grammar.Name;
            }
            set
            {
                if (_grammar.Name != value)
                {
                    _grammar.Name = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public string GrammarDirectory
        {
            get
            {
                return _grammarDirectory;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _grammarDirectory, value);
            }
        }

        public string GrammarRoot
        {
            get
            {
                return _grammar.Root;
            }
            set
            {
                if (_grammar.Root != value)
                {
                    _grammar.Root = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public string FileExtension
        {
            get
            {
                return _grammar.FileExtension;
            }
            set
            {
                if (_grammar.FileExtension != value)
                {
                    _grammar.FileExtension = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public RuntimeInfo Runtime
        {
            get
            {
                return _grammar.Runtimes.First().GetRuntimeInfo();
            }
            set
            {
                if (_grammar.Runtimes.First().GetRuntimeInfo() != value)
                {
                    _grammar.Runtimes.Clear();
                    _grammar.Runtimes.Add(value.Runtime);
                    this.RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<RuntimeInfo> Runtimes { get; } = new ObservableCollection<RuntimeInfo>(RuntimeInfo.Runtimes.Select(r => r.Value).ToList());

        public bool SeparatedLexerAndParser
        {
            get
            {
                return _grammar.SeparatedLexerAndParser;
            }
            set
            {
                if (_grammar.SeparatedLexerAndParser != value)
                {
                    _grammar.SeparatedLexerAndParser = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool CaseInsensitive
        {
            get
            {
                return _grammar.CaseInsensitive;
            }
            set
            {
                if (_grammar.CaseInsensitive != value)
                {
                    _grammar.CaseInsensitive = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool Preprocessor
        {
            get
            {
                return _grammar.Preprocessor;
            }
            set
            {
                if (_grammar.Preprocessor != value)
                {
                    _grammar.Preprocessor = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public string PreprocessorGrammarRoot
        {
            get
            {
                return _grammar.PreprocessorRoot;
            }
            set
            {
                if (_grammar.PreprocessorRoot != value)
                {
                    _grammar.PreprocessorRoot = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool PreprocessorSeparatedLexerAndParser
        {
            get
            {
                return _grammar.PreprocessorSeparatedLexerAndParser;
            }
            set
            {
                if (_grammar.PreprocessorSeparatedLexerAndParser != value)
                {
                    _grammar.PreprocessorSeparatedLexerAndParser = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool PreprocessorCaseInsensitive
        {
            get
            {
                return _grammar.PreprocessorCaseInsensitive;
            }
            set
            {
                if (_grammar.PreprocessorCaseInsensitive != value)
                {
                    _grammar.PreprocessorCaseInsensitive = value;
                    this.RaisePropertyChanged();
                }
            }
        }
    }
}
