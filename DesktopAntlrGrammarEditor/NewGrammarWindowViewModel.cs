using AntlrGrammarEditor;
using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;

namespace DesktopAntlrGrammarEditor
{
    public class NewGrammarWindowViewModel : ReactiveObject
    {
        private readonly Grammar _grammar = GrammarFactory.CreateDefault();
        private Runtime _runtime;
        private string _grammarDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DAGE Grammars");

        public NewGrammarWindowViewModel(Window window)
        {
            OkCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                string grammarFileName = Path.Combine(GrammarDirectory, _grammar.Name);
                bool success = false;

                if (Directory.Exists(grammarFileName))
                {
                    if (await MessageBox.ShowDialog(window, $"Do you want to replace existed grammar {_grammar.Name}?", "", MessageBoxType.YesNo))
                    {
                        success = true;
                    }
                }
                else
                {
                    success = true;
                }

                if (success)
                {
                    GrammarFactory.FillGrammarFiles(_grammar, grammarFileName, true);
                    window.Close(_grammar);
                }
            });

            CancelCommand = ReactiveCommand.Create(() => window.Close(null));

            SelectGrammarDirectory = ReactiveCommand.CreateFromTask(async () =>
            {
                var openFolderDialog = new OpenFolderDialog
                {
                    Directory = GrammarDirectory
                };
                var folderName = await openFolderDialog.ShowAsync(window);
                if (!string.IsNullOrEmpty(folderName))
                {
                    GrammarDirectory = folderName;
                }
            });
        }

        public ReactiveCommand<Unit, Unit> OkCommand { get; }

        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectGrammarDirectory { get; }

        public string GrammarName
        {
            get => _grammar.Name;
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
            get => _grammarDirectory;
            set
            {
                this.RaiseAndSetIfChanged(ref _grammarDirectory, value);
            }
        }

        public string FileExtension
        {
            get => _grammar.FileExtension;
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
            get => RuntimeInfo.InitOrGetRuntimeInfo(_runtime);
            set
            {
                if (_runtime != value.Runtime)
                {
                    _runtime = value.Runtime;
                    this.RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<RuntimeInfo> Runtimes { get; } = new ObservableCollection<RuntimeInfo>(RuntimeInfo.Runtimes.Select(r => r.Value).ToList());

        public GrammarType GrammarType
        {
            get => _grammar.Type;
            set
            {
                if (_grammar.Type != value)
                {
                    _grammar.Type = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public CaseInsensitiveType CaseInsensitiveType
        {
            get => _grammar.CaseInsensitiveType;
            set
            {
                if (_grammar.CaseInsensitiveType != value)
                {
                    _grammar.CaseInsensitiveType = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<CaseInsensitiveType> CaseInsensitiveTypes { get; } =
            new ObservableCollection<CaseInsensitiveType>(new List<CaseInsensitiveType>
        {
            CaseInsensitiveType.None,
            CaseInsensitiveType.Lower,
            CaseInsensitiveType.Upper
        });

        public ObservableCollection<GrammarType> GrammarTypes { get; } =
            new ObservableCollection<GrammarType>(new List<GrammarType>
        {
            GrammarType.Combined,
            GrammarType.Separated,
            GrammarType.Lexer
        });
    }
}
