﻿using AntlrGrammarEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml;

namespace DesktopAntlrGrammarEditor
{
    public class MainWindowViewModel : ReactiveObject
    {
        private readonly Window _window;
        private readonly Settings _settings;
        private Grammar _grammar;
        private readonly Workflow _workflow;
        private string _openedGrammarFile = "";
        private FileName _openedTextFile = FileName.Empty;
        private FileState _grammarFileState, _textFileState;
        private readonly TextEditor _grammarTextBox;
        private readonly TextEditor _textTextBox;
        private readonly TextEditor _tokensTextBox;
        private readonly TextEditor _parseTreeTextBox;
        private readonly ListBox _grammarErrorsListBox;
        private readonly ListBox _textErrorsListBox;
        private bool _autoprocessing;
        private bool _indentedTree;
        private WorkflowStage _endStage = WorkflowStage.TextParsed;

        public MainWindowViewModel(Window window)
        {
            _window = window;
            _grammarTextBox = _window.Find<TextEditor>("GrammarTextBox");
            _textTextBox = _window.Find<TextEditor>("TextTextBox");
            _grammarErrorsListBox = _window.Find<ListBox>("GrammarErrorsListBox");
            _textErrorsListBox = _window.Find<ListBox>("TextErrorsListBox");
            _parseTreeTextBox = _window.Find<TextEditor>("ParseTreeTextBox");
            _tokensTextBox = _window.Find<TextEditor>("TokensTextBox");

            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopAntlrGrammarEditor.Antlr4.xshd"))
            {
                using (XmlTextReader reader = new XmlTextReader(s))
                {
                    _grammarTextBox.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }

            _settings = Settings.Load();

            _window.WindowState = _settings.WindowState;
            if (_settings.Width > 0)
            {
                _window.Width = _settings.Width;
            }
            if (_settings.Height > 0)
            {
                _window.Height = _settings.Height;
            }
            if (_settings.Left != -1 && _settings.Top != -1)
            {
                _window.Position = new Point(_settings.Left, _settings.Top);
            }

            _workflow = new Workflow();

            bool openDefaultGrammar = false;
            if (string.IsNullOrEmpty(_settings.AgeFileName))
            {
                openDefaultGrammar = true;
            }
            else
            {
                try
                {
                    _grammar = Grammar.Load(_settings.AgeFileName);
                }
                catch (Exception ex)
                {
                    ShowOpenFileErrorMessage(_settings.AgeFileName, ex.Message);

                    _settings.OpenedGrammarFile = "";
                    openDefaultGrammar = true;
                }
            }

            if (openDefaultGrammar)
            {
                _grammar = GrammarFactory.CreateDefault();
                GrammarFactory.FillGrammarFiles(_grammar, Settings.Directory, false);
                _settings.AgeFileName = _grammar.AgeFileName;
                _settings.Save();
            }

            _workflow.Grammar = _grammar;
            SelectedRuntime = RuntimeInfo.Runtimes[_grammar.Runtimes.First()];

            InitFiles();
            if (string.IsNullOrEmpty(_settings.OpenedGrammarFile))
            {
                OpenedGrammarFile = GrammarFiles.First();
            }
            else
            {
                OpenedGrammarFile = _settings.OpenedGrammarFile;
            }

            if (string.IsNullOrEmpty(_settings.OpenedTextFile))
            {
                OpenedTextFile = TextFiles.Count > 0 ? TextFiles.First() : null;
            }
            else
            {
                OpenedTextFile = new FileName(_settings.OpenedTextFile);
            }

            SetupWindowSubscriptions();
            SetupWorkflowSubscriptions();
            SetupTextBoxSubscriptions();
            SetupCommandSubscriptions();

            AutoProcessing = _settings.Autoprocessing;
            IndentedTree = _settings.IndentedTree;
        }

        public string OpenedGrammarFile
        {
            get => _openedGrammarFile;
            set
            {
                SaveGrammarFileIfRequired();
                if (value != null && !value.Equals(_openedGrammarFile))
                {
                    try
                    {
                        _grammarTextBox.Text = File.ReadAllText(GetFullGrammarFileName(value));

                        _openedGrammarFile = value;
                        _grammarFileState = FileState.Opened;

                        if (IsParserOpened)
                        {
                            if (_workflow.CurrentState.Stage == WorkflowStage.Input)
                            {
                                _workflow.Grammar = _grammar;
                                _workflow.EndStage = WorkflowStage.GrammarChecked;
                                _workflow.Process();
                                _workflow.EndStage = EndStage;
                            }
                            UpdateRules();
                        }

                        _settings.OpenedGrammarFile = value;
                        _settings.Save();

                        this.RaisePropertyChanged(nameof(IsParserOpened));
                        this.RaisePropertyChanged(nameof(IsPreprocessor));
                        this.RaisePropertyChanged();
                    }
                    catch (Exception ex)
                    {
                        ShowOpenFileErrorMessage(_openedGrammarFile, ex.Message);
                    }
                }
            }
        }

        public bool IsParserOpened => !OpenedGrammarFile.Contains(GrammarFactory.LexerPostfix);

        public bool IsPreprocessor => OpenedGrammarFile.Contains(GrammarFactory.PreprocessorPostfix);

        public ObservableCollection<string> GrammarFiles { get; } = new ObservableCollection<string>();

        public string Root
        {
            get => IsPreprocessor ? _grammar.PreprocessorRoot : _grammar.Root;
            set
            {
                var currentRoot = IsPreprocessor ? _grammar.PreprocessorRoot : _grammar.Root;
                if (value != null && currentRoot != value)
                {
                    if (IsPreprocessor)
                    {
                        _workflow.PreprocessorRoot = value;
                    }
                    else
                    {
                        _workflow.Root = value;
                    }
                    if (AutoProcessing)
                    {
                        Process();
                    }
                    _grammar.Save();
                    this.RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<string> Rules { get; } = new ObservableCollection<string>();

        public RuntimeInfo SelectedRuntime
        {
            get => RuntimeInfo.Runtimes[_grammar.Runtimes.First()];
            set
            {
                if (RuntimeInfo.Runtimes[_grammar.Runtimes.First()] != value)
                {
                    _workflow.Runtime = value.Runtime;
                    _grammar.Runtimes.Clear();
                    _grammar.Runtimes.Add(value.Runtime);
                    _grammar.Save();
                    this.RaisePropertyChanged();
                    if (AutoProcessing)
                    {
                        Process();
                    }
                }
            }
        }

        public string CurrentState => _workflow.CurrentState.Stage.ToString();

        public ObservableCollection<RuntimeInfo> Runtimes { get; } = new ObservableCollection<RuntimeInfo>(RuntimeInfo.Runtimes.Select(r => r.Value).ToList());

        public string GrammarErrorsText => $"Grammar Errors ({GrammarErrors.Count})";

        public bool GrammarErrorsExpanded => GrammarErrors.Count > 0;

        public bool TextBoxEnabled => !string.IsNullOrEmpty(_openedTextFile?.FullFileName);

        public ObservableCollection<object> GrammarErrors { get; } = new ObservableCollection<object>();

        public ObservableCollection<FileName> TextFiles { get; } = new ObservableCollection<FileName>();

        public FileName OpenedTextFile
        {
            get => _openedTextFile;
            set
            {
                SaveTextFileIfRequired();

                if (!string.IsNullOrEmpty(value?.FullFileName) && !value.Equals(_openedTextFile))
                {
                    _textTextBox.IsEnabled = true;
                    _openedTextFile = value;
                    try
                    {
                        string fileName = value.FullFileName;
                        if (!File.Exists(value.FullFileName))
                        {
                            fileName = GetFullGrammarFileName(value.FullFileName);
                        }
                        _textTextBox.Text = File.ReadAllText(fileName);
                    }
                    catch (Exception ex)
                    {
                        _textTextBox.Text = "";
                        ShowOpenFileErrorMessage(_openedTextFile.FullFileName, ex.Message);
                    }
                    _workflow.Text = _textTextBox.Text;
                    _textFileState = FileState.Opened;

                    _settings.OpenedTextFile = value.FullFileName;
                    _settings.Save();

                    ClearParseResult();

                    this.RaisePropertyChanged();
                }

                if (string.IsNullOrEmpty(value?.FullFileName))
                {
                    _textTextBox.IsEnabled = false;
                    _openedTextFile = FileName.Empty;
                    _textTextBox.Text = "";
                    _workflow.Text = _textTextBox.Text;
                    _textFileState = FileState.Opened;

                    _settings.OpenedTextFile = _openedTextFile.FullFileName;
                    _settings.Save();

                    ClearParseResult();

                    this.RaisePropertyChanged();
                }
            }
        }

        public string TextErrorsText => $"Text Errors ({TextErrors.Count})";

        public ObservableCollection<object> TextErrors { get; } = new ObservableCollection<object>();

        public bool TextErrorsExpanded => TextErrors.Count > 0;

        public string Tokens
        {
            get => _tokensTextBox.Text;
            set => _tokensTextBox.Text = value;
        }

        public string Tree
        {
            get => _parseTreeTextBox.Text;
            set => _parseTreeTextBox.Text = value;
        }

        public bool IsTokensExpanded
        {
            get => _settings.IsTokensExpanded;
            set
            {
                if (_settings.IsTokensExpanded != value)
                {
                    _settings.IsTokensExpanded = value;
                    _settings.Save();
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool IsParseTreeExpanded
        {
            get => _settings.IsParseTreeExpanded;
            set
            {
                if (_settings.IsParseTreeExpanded != value)
                {
                    _settings.IsParseTreeExpanded = value;
                    _settings.Save();
                    this.RaisePropertyChanged();
                }
            }
        }

        public bool IndentedTree
        {
            get => _indentedTree;
            set
            {
                if (_indentedTree != value)
                {
                    _indentedTree = value;
                    _workflow.IndentedTree = value;
                    _settings.IndentedTree = value;
                    _settings.Save();
                    this.RaisePropertyChanged();
                }
            }
        }

        public ReactiveCommand NewGrammarCommand { get; private set; }

        public ReactiveCommand OpenGrammarCommand { get; private set; }

        public ReactiveCommand ProcessCommand { get; private set; }

        public ReactiveCommand NewTextFile { get; private set; }

        public ReactiveCommand OpenTextFile { get; private set; }

        public ReactiveCommand RemoveTextFile { get; private set; }

        public bool AutoProcessing
        {
            get => _autoprocessing;
            set
            {
                if (_autoprocessing != value)
                {
                    _autoprocessing = value;
                    if (_autoprocessing)
                    {
                        Process();
                    }
                    _settings.Autoprocessing = _autoprocessing;
                    _settings.Save();

                    this.RaisePropertyChanged();
                }
            }
        }

        public WorkflowStage EndStage
        {
            get => _endStage;
            set => this.RaiseAndSetIfChanged(ref _endStage, value);
        }

        private void SetupWindowSubscriptions()
        {
            _window.GetObservable(Window.WidthProperty)
                .Throttle(TimeSpan.FromMilliseconds(250))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(width =>
                {
                    if (_window.WindowState != WindowState.Maximized)
                    {
                        _settings.Width = width;
                    }
                    _settings.WindowState = _window.WindowState;
                    _settings.Save();
                });

            _window.GetObservable(Window.HeightProperty)
                .Throttle(TimeSpan.FromMilliseconds(250))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(height =>
                {
                    if (_window.WindowState != WindowState.Maximized)
                    {
                        _settings.Height = height;
                    }
                    _settings.WindowState = _window.WindowState;
                    _settings.Save();
                });

            Observable.FromEventPattern<CancelEventArgs>(
                ev => _window.Closing += ev, ev => _window.Closing -= ev)
                .Subscribe(ev =>
                {
                    SaveGrammarFileIfRequired();
                    SaveTextFileIfRequired();
                });

            Observable.FromEventPattern<PointEventArgs>(
                ev => _window.PositionChanged += ev, ev => _window.PositionChanged -= ev)
                .Throttle(TimeSpan.FromMilliseconds(250))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ev =>
                {
                    if (_window.WindowState != WindowState.Maximized)
                    {
                        _settings.Left = _window.Position.X;
                        _settings.Top = _window.Position.Y;
                    }
                    _settings.Save();
                });
        }

        private void SetupWorkflowSubscriptions()
        {
            Observable.FromEventPattern<IWorkflowState>(
                ev => _workflow.StateChanged += ev, ev => _workflow.StateChanged -= ev)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ev =>
                {
                    this.RaisePropertyChanged(nameof(CurrentState));
                });

            Observable.FromEventPattern<ParsingError>(
                ev => _workflow.ErrorEvent += ev, ev => _workflow.ErrorEvent -= ev)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ev =>
                {
                    switch (ev.EventArgs.WorkflowStage)
                    {
                        case WorkflowStage.GrammarChecked:
                        case WorkflowStage.ParserGenerated:
                        case WorkflowStage.ParserCompilied:
                            GrammarErrors.Add(ev.EventArgs);
                            this.RaisePropertyChanged(nameof(GrammarErrorsText));
                            this.RaisePropertyChanged(nameof(GrammarErrorsExpanded));
                            break;
                        case WorkflowStage.TextTokenized:
                        case WorkflowStage.TextParsed:
                            TextErrors.Add(ev.EventArgs);
                            this.RaisePropertyChanged(nameof(TextErrorsText));
                            this.RaisePropertyChanged(nameof(TextErrorsExpanded));
                            break;
                    }
                });

            Observable.FromEventPattern<(TextParsedOutput, object)>(
                 ev => _workflow.TextParsedOutputEvent += ev, ev => _workflow.TextParsedOutputEvent -= ev)
                 .ObserveOn(RxApp.MainThreadScheduler)
                 .Subscribe(ev =>
                 {
                     switch (ev.EventArgs.Item1)
                     {
                         case TextParsedOutput.LexerTime:
                             break;
                         case TextParsedOutput.ParserTime:
                             break;
                         case TextParsedOutput.Tokens:
                             Tokens = (string)ev.EventArgs.Item2;
                             break;
                         case TextParsedOutput.Tree:
                             Tree = (string)ev.EventArgs.Item2;
                             break;
                     }
                 });

            _workflow.ClearErrorsEvent += Workflow_ClearErrorsEvent;
        }

        private void SetupTextBoxSubscriptions()
        {
            _grammarErrorsListBox.DoubleTapped += ErrorsListBox_DoubleTapped;
            _textErrorsListBox.DoubleTapped += ErrorsListBox_DoubleTapped;

            var grammarTextBoxObservable = Observable.FromEventPattern<EventHandler, EventArgs>(
                h => _grammarTextBox.TextChanged += h,
                h => _grammarTextBox.TextChanged -= h);
            var textBoxObservable = Observable.FromEventPattern<EventHandler, EventArgs>(
                h => _textTextBox.TextChanged += h,
                h => _textTextBox.TextChanged -= h);

            grammarTextBoxObservable.Subscribe(x =>
            {
                _grammarFileState = FileState.Changed;
            });

            textBoxObservable.Subscribe(x =>
            {
                _textFileState = FileState.Changed;
            });

            grammarTextBoxObservable
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .Subscribe(x => 
                {
                    if (_grammarFileState == FileState.Changed)
                    {
                        _workflow.Grammar = _grammar;
                        if (AutoProcessing)
                        {
                            SaveGrammarFileIfRequired();
                            Process();
                        }
                    }
                });

            textBoxObservable
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x =>
                {
                    if (_textFileState == FileState.Changed)
                    {
                        _workflow.Text = _textTextBox.Text;
                        if (AutoProcessing)
                        {
                            SaveTextFileIfRequired();
                            Process();
                        }
                    }
                });
        }

        private void SetupCommandSubscriptions()
        {
            NewGrammarCommand = ReactiveCommand.Create(async () =>
            {
                var newGrammarWindow = new NewGrammarWindow();
                Grammar grammar = await newGrammarWindow.ShowDialog<Grammar>();
                if (grammar != null)
                {
                    OpenGrammar(grammar);
                }
                _window.Activate();
            });

            OpenGrammarCommand = ReactiveCommand.Create(async () =>
            {
                var openDialog = new OpenFileDialog
                {
                    Title = "Enter file name",
                    Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter
                        {
                            Name = "Antlr Grammar Editor",
                            Extensions = new List<string> { Grammar.ProjectDotExt.Substring(1) }
                        }
                    }
                };

                string[] fileNames = await openDialog.ShowAsync(_window);
                if (fileNames?.Length > 0)
                {
                    try
                    {
                        var grammar = Grammar.Load(fileNames[0]);
                        OpenGrammar(grammar);
                    }
                    catch (Exception ex)
                    {
                        await ShowOpenFileErrorMessage(fileNames[0], ex.Message);
                    }
                }
            });

            ProcessCommand = ReactiveCommand.Create(() =>
            {
                bool changed = false;

                if (_grammarFileState == FileState.Changed)
                {
                    File.WriteAllText(GetFullGrammarFileName(_openedGrammarFile), _grammarTextBox.Text);
                    _workflow.Grammar = _grammar;
                    _grammarFileState = FileState.Saved;
                    changed = true;
                }

                if (_textFileState == FileState.Changed && !string.IsNullOrEmpty(_openedTextFile?.FullFileName))
                {
                    File.WriteAllText(_openedTextFile.FullFileName, _textTextBox.Text);
                    _workflow.Text = _textTextBox.Text;
                    _textFileState = FileState.Saved;
                    changed = true;
                }

                if (changed)
                {
                    _settings.Save();
                }

                Process();
            });

            NewTextFile = ReactiveCommand.Create(async () =>
            {
                var filters = new List<FileDialogFilter>();
                if (!string.IsNullOrEmpty(_grammar.FileExtension))
                {
                    filters.Add(new FileDialogFilter
                    {
                        Name = $"{_grammar.Name} parsing file",
                        Extensions = new List<string>() { _grammar.FileExtension }
                    });
                }

                filters.Add(new FileDialogFilter
                {
                    Name = "All files",
                    Extensions = new List<string> { "*" }
                });

                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Enter file name",
                    DefaultExtension = _grammar.FileExtension,
                    Filters = filters,
                    InitialDirectory = _grammar.GrammarPath,
                    InitialFileName = Path.GetFileName(GrammarFactory.GenerateTextFileName(_grammar))
                };

                string fileName = await saveFileDialog.ShowAsync(_window);
                if (fileName != null)
                {
                    File.WriteAllText(fileName, "");
                    var newFile = new FileName(fileName);
                    if (!TextFiles.Contains(newFile))
                    {
                        TextFiles.Add(newFile);
                        _grammar.TextFiles.Add(newFile.FullFileName);
                        _grammar.Save();
                        OpenedTextFile = TextFiles.Last();
                    }
                }
            });

            OpenTextFile = ReactiveCommand.Create(async () =>
            {
                var openFileDialog = new OpenFileDialog
                {
                    AllowMultiple = true
                };
                var fileNames = await openFileDialog.ShowAsync(_window);
                if (fileNames != null)
                {
                    bool atLeastOneFileHasBeenAdded = false;
                    foreach (var fileName in fileNames)
                    {
                        var openedFile = new FileName(fileName);
                        if (!TextFiles.Contains(openedFile))
                        {
                            atLeastOneFileHasBeenAdded = true;
                            TextFiles.Add(openedFile);
                            _grammar.TextFiles.Add(openedFile.FullFileName);
                        }
                    }
                    if (atLeastOneFileHasBeenAdded)
                    {
                        _grammar.Save();
                        OpenedTextFile = TextFiles.Last();
                    }
                }
            });

            RemoveTextFile = ReactiveCommand.Create(async () =>
            {
                string shortFileName = OpenedTextFile.ShortFileName;
                string fullFileName = OpenedTextFile.FullFileName;
                _grammar.TextFiles.Remove(OpenedTextFile.FullFileName);
                _grammar.Save();
                var index = TextFiles.IndexOf(OpenedTextFile);
                TextFiles.Remove(OpenedTextFile);
                index = Math.Min(index, TextFiles.Count - 1);
                if (await MessageBox.ShowDialog($"Do you want to delete file {shortFileName}?", "", MessageBoxType.YesNo))
                {
                    try
                    {
                        File.Delete(fullFileName);
                    }
                    catch (Exception ex)
                    {
                        await ShowOpenFileErrorMessage(fullFileName, ex.Message);
                    }
                }
                if (index >= 0)
                {
                    OpenedTextFile = TextFiles[index];
                }
            });
        }

        private void OpenGrammar(Grammar grammar)
        {
            _grammar = grammar;
            _workflow.Grammar = grammar;
            _settings.AgeFileName = grammar.AgeFileName;
            _settings.Save();
            _openedGrammarFile = "";
            _openedTextFile = FileName.Empty;
            Rules.Clear();
            InitFiles();
            OpenedGrammarFile = GrammarFiles.First();
            OpenedTextFile = TextFiles.Count > 0 ? TextFiles.First() : null;
            this.RaisePropertyChanged(nameof(SelectedRuntime));
        }

        private void Workflow_ClearErrorsEvent(object sender, WorkflowStage e)
        {
            ObservableCollection<object> errorsList = GrammarErrors;
            bool grammarErrors = false;
            switch (e)
            {
                case WorkflowStage.Input:
                case WorkflowStage.GrammarChecked:
                case WorkflowStage.ParserGenerated:
                case WorkflowStage.ParserCompilied:
                    errorsList = GrammarErrors;
                    grammarErrors = true;
                    break;
                case WorkflowStage.TextTokenized:
                case WorkflowStage.TextParsed:
                    errorsList = TextErrors;
                    break;
            }
            if (errorsList.Count > 0)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    int i = 0;
                    while (i < errorsList.Count)
                    {
                        if (errorsList[i] is ParsingError parsingError)
                        {
                            if (parsingError.WorkflowStage == e)
                            {
                                errorsList.RemoveAt(i);
                                continue;
                            }
                        }
                        i++;
                    }
                    if (grammarErrors)
                    {
                        this.RaisePropertyChanged(nameof(GrammarErrorsText));
                        this.RaisePropertyChanged(nameof(GrammarErrorsExpanded));
                    }
                    else
                    {
                        this.RaisePropertyChanged(nameof(TextErrorsText));
                        this.RaisePropertyChanged(nameof(TextErrorsExpanded));
                    }
                });
            }
        }

        private void ErrorsListBox_DoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ListBox listBox = (ListBox)sender;
            listBox.Focus();

            if (listBox.SelectedItem is ParsingError parsingError)
            {
                TextEditor textBox = listBox == _grammarErrorsListBox ? _grammarTextBox : _textTextBox;
                if (textBox == _grammarTextBox)
                {
                    OpenedGrammarFile = parsingError.TextSpan.Source.Name;
                }
                textBox.Select(parsingError.TextSpan.Start, parsingError.TextSpan.Length);
            }
        }

        private void InitFiles()
        {
            GrammarFiles.Clear();
            foreach (var file in _grammar.Files)
            {
                GrammarFiles.Add(file);
            }
            TextFiles.Clear();
            foreach (var file in _grammar.TextFiles)
            {
                TextFiles.Add(new FileName(file));
            }
        }

        private void SaveGrammarFileIfRequired()
        {
            if (_grammarFileState == FileState.Changed)
            {
                File.WriteAllText(GetFullGrammarFileName(_openedGrammarFile), _grammarTextBox.Text);
                _grammarFileState = FileState.Saved;
            }
        }

        private void SaveTextFileIfRequired()
        {
            if (_textFileState == FileState.Changed && !string.IsNullOrEmpty(_openedTextFile?.FullFileName))
            {
                File.WriteAllText(_openedTextFile.FullFileName, _textTextBox.Text);
                _textFileState = FileState.Saved;
            }
        }

        private async void Process()
        {
            _workflow.RollbackToPreviousStageIfErrors();

            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Directory.SetCurrentDirectory(assemblyPath);

            if (EndStage >= WorkflowStage.ParserGenerated && Helpers.JavaVersion == null)
            {
                string message = "Install java";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    message += " and make sure path to Java added to the PATH environment variable";
                }

                await MessageBox.ShowDialog(message);

                return;
            }

            RuntimeInfo runtimeInfo = RuntimeInfo.InitOrGetRuntimeInfo(SelectedRuntime.Runtime);

            if (EndStage >= WorkflowStage.ParserCompilied && runtimeInfo.Version == null)
            {
                string message = $"Install {runtimeInfo.RuntimeToolName}";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    message +=
                        $" and make sure path to {runtimeInfo.RuntimeToolName} added to the PATH environment variable";
                }

                await MessageBox.ShowDialog(message);

                return;
            }

            await _workflow.ProcessAsync();

            UpdateRules();
        }

        private void UpdateRules()
        {
            var grammarCheckedState = _workflow.CurrentState.GetState<GrammarCheckedState>();
            if (grammarCheckedState == null)
            {
                return;
            }
            
            var workflowRules = IsPreprocessor ? grammarCheckedState.PreprocessorRules : grammarCheckedState.Rules;
            if (!Rules.SequenceEqual(workflowRules))
            {
                Rules.Clear();
                foreach (var rule in workflowRules)
                {
                    Rules.Add(rule);
                }
                _grammar.Save();
                this.RaisePropertyChanged(nameof(Root));
            }
        }

        private string GetFullGrammarFileName(string fileName) => Path.Combine(_grammar.GrammarPath, fileName);

        private async Task ShowOpenFileErrorMessage(string fileName, string exceptionMessage)
        {
            var messageBox = new MessageBox($"Error while opening {fileName} file: {exceptionMessage}", "Error");
            await messageBox.ShowDialog();
            _window.Activate();
        }

        private void ClearParseResult()
        {
            Tokens = "";
            Tree = "";
            TextErrors.Clear();
        }
    }
}
