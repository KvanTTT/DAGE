using AntlrGrammarEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace DesktopAntlrGrammarEditor
{
    public class MainWindowViewModel : ReactiveObject
    {
        private Window _window;
        private Settings _settings;
        private Grammar _grammar;
        private Workflow _workflow;
        private string _openedGrammarFile = "";
        private FileName _openedTextFile = FileName.Empty;
        private FileState _grammarFileState, _textFileState;
        private TextBox _grammarTextBox, _textTextBox;
        private ListBox _grammarErrorsListBox, _textErrorsListBox;
        private string _tokens, _tree;
        private bool _autoprocessing;
        private WorkflowStage _endStage = WorkflowStage.TextParsed;

        public MainWindowViewModel(Window window)
        {
            _window = window;
            _grammarTextBox = _window.Find<TextBox>("GrammarTextBox");
            _textTextBox = _window.Find<TextBox>("TextTextBox");
            _grammarErrorsListBox = _window.Find<ListBox>("GrammarErrorsListBox");
            _textErrorsListBox = _window.Find<ListBox>("TextErrorsListBox");

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
            _workflow.JavaPath = _settings.JavaPath;
            _workflow.CompilerPaths = _settings.CompilerPaths;

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
            SelectedRuntime = _grammar.Runtimes.First().GetRuntimeInfo();

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
        }

        public string OpenedGrammarFile
        {
            get
            {
                return _openedGrammarFile;
            }
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
                        ShowOpenFileErrorMessage(_openedGrammarFile, ex.Message).Wait();
                    }
                }
            }
        }

        public bool IsParserOpened => !OpenedGrammarFile.Contains(GrammarFactory.LexerPostfix);

        public bool IsPreprocessor => OpenedGrammarFile.Contains(GrammarFactory.PreprocessorPostfix);

        public ObservableCollection<string> GrammarFiles { get; } = new ObservableCollection<string>();

        public string Root
        {
            get
            {
                return IsPreprocessor ? _grammar.PreprocessorRoot : _grammar.Root;
            }
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
            get
            {
                return _grammar.Runtimes.First().GetRuntimeInfo();
            }
            set
            {
                if (_grammar.Runtimes.First().GetRuntimeInfo() != value)
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
            get
            {
                return _openedTextFile;
            }
            set
            {
                SaveTextFileIfRequired();
                if (!string.IsNullOrEmpty(value?.FullFileName) && !value.Equals(_openedTextFile))
                {
                    _textTextBox.IsEnabled = true;
                    _openedTextFile = value;
                    try
                    {
                        _textTextBox.Text = File.ReadAllText(value.FullFileName);
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
            get
            {
                return _tokens;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _tokens, value);
            }
        }

        public string Tree
        {
            get
            {
                return _tree;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _tree, value);
            }
        }

        public ReactiveCommand<object> NewGrammarCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> OpenGrammarCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> ProcessCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> NewTextFile { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> OpenTextFile { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> RemoveTextFile { get; } = ReactiveCommand.Create();

        public bool AutoProcessing
        {
            get
            {
                return _autoprocessing;
            }
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
            get
            {
                return _endStage;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _endStage, value);
            }
        }

        private void SetupWindowSubscriptions()
        {
            _window.GetObservable(Window.WidthProperty)
                .Throttle(TimeSpan.FromMilliseconds(250))
                .Subscribe(width =>
                {
                    _settings.Width = width;
                    _settings.WindowState = _window.WindowState;
                    _settings.Save();
                });

            _window.GetObservable(Window.HeightProperty)
                .Throttle(TimeSpan.FromMilliseconds(250))
                .Subscribe(height =>
                {
                    _settings.Height = height;
                    _settings.WindowState = _window.WindowState;
                    _settings.Save();
                });

            Observable.FromEventPattern(
                ev => _window.Closed += ev, ev => _window.Closed -= ev)
                .Subscribe(ev =>
                {
                    SaveGrammarFileIfRequired();
                    SaveTextFileIfRequired();
                    _settings.Left = _window.Position.X;
                    _settings.Top = _window.Position.Y;
                    _settings.Save();
                });
        }

        private void SetupWorkflowSubscriptions()
        {
            Observable.FromEventPattern<WorkflowState>(
                ev => _workflow.StateChanged += ev, ev => _workflow.StateChanged -= ev)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ev =>
                {
                    this.RaisePropertyChanged(nameof(CurrentState));
                });

            Observable.FromEventPattern<ParsingError>(
                ev => _workflow.NewErrorEvent += ev, ev => _workflow.NewErrorEvent -= ev)
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

            Observable.FromEventPattern<Tuple<TextParsedOutput, object>>(
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

            _grammarTextBox.GetObservable(TextBox.TextProperty)
                .Subscribe(str =>
                {
                    if (_grammarFileState == FileState.Opened)
                    {
                        _grammarFileState = FileState.Unchanged;
                    }
                    else
                    {
                        _grammarFileState = FileState.Changed;
                    }
                });

            _grammarTextBox.GetObservable(TextBox.TextProperty)
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .Subscribe(str =>
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

            _textTextBox.GetObservable(TextBox.TextProperty)
                .Subscribe(str => {
                    if (_textFileState == FileState.Opened)
                    {
                        _textFileState = FileState.Unchanged;
                    }
                    else
                    {
                        _textFileState = FileState.Changed;
                    }
                });

            _textTextBox.GetObservable(TextBox.TextProperty)
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .Subscribe(str =>
                {
                    if (_textFileState == FileState.Changed)
                    {
                        _workflow.Text = str;
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
            NewGrammarCommand.Subscribe(async _ =>
            {
                var newGrammarWindow = new NewGrammarWindow();
                Grammar grammar = await newGrammarWindow.ShowDialog<Grammar>();
                if (grammar != null)
                {
                    OpenGrammar(grammar);
                }
                _window.Activate();
            });

            OpenGrammarCommand.Subscribe(async _ =>
            {
                var openDialog = new OpenFileDialog();
                openDialog.Filters.Add(new FileDialogFilter() { Name = "Antlr Grammar Editor", Extensions = new List<string>() { Grammar.ProjectDotExt.Substring(1) } });
                string[] fileNames = await openDialog.ShowAsync(_window);
                if (fileNames != null)
                {
                    try
                    {
                        var grammar = Grammar.Load(fileNames.First());
                        OpenGrammar(grammar);
                    }
                    catch (Exception ex)
                    {
                        await ShowOpenFileErrorMessage(fileNames.First(), ex.Message);
                    }
                }
            });

            ProcessCommand.Subscribe(_ =>
            {
                bool changed = false;
                if (_grammarFileState == FileState.Changed)
                {
                    File.WriteAllText(GetFullGrammarFileName(_openedGrammarFile), _grammarTextBox.Text);
                    _workflow.Grammar = _grammar;
                    _grammarFileState = FileState.Unchanged;
                    changed = true;
                }

                if (_textFileState == FileState.Changed && !string.IsNullOrEmpty(_openedTextFile?.FullFileName))
                {
                    File.WriteAllText(_openedTextFile.FullFileName, _textTextBox.Text);
                    _workflow.Text = _textTextBox.Text;
                    _textFileState = FileState.Unchanged;
                    changed = true;
                }

                if (changed)
                {
                    _settings.Save();
                }
                Process();
            });

            NewTextFile.Subscribe(async _ =>
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
                    Extensions = new List<string>() { "*" }
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

            OpenTextFile.Subscribe(async _ =>
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

            RemoveTextFile.Subscribe(async _ =>
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
                        var parsingError = errorsList[i] as ParsingError;
                        if (parsingError != null)
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

            var parsingError = listBox.SelectedItem as ParsingError;
            if (parsingError != null)
            {
                TextBox textBox = listBox == _grammarErrorsListBox ? _grammarTextBox : _textTextBox;
                if (textBox == _grammarTextBox)
                {
                    OpenedGrammarFile = parsingError.FileName;
                }
                textBox.SelectionStart = parsingError.TextSpan.Start;
                textBox.SelectionEnd = parsingError.TextSpan.Start + parsingError.TextSpan.Length;
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
                _grammarFileState = FileState.Unchanged;
            }
        }

        private void SaveTextFileIfRequired()
        {
            if (_textFileState == FileState.Changed && !string.IsNullOrEmpty(_openedTextFile?.FullFileName))
            {
                File.WriteAllText(_openedTextFile.FullFileName, _textTextBox.Text);
                _textFileState = FileState.Unchanged;
            }
        }

        private async void Process()
        {
            _workflow.RollbackToPreviousStageIfErrors();

            var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Directory.SetCurrentDirectory(assemblyPath);

            if (EndStage >= WorkflowStage.ParserGenerated && string.IsNullOrEmpty(_settings.JavaPath))
            {
                var javaPath = "java";
                bool successExecution = ProcessHelpers.IsProcessCanBeExecuted(javaPath, "-version");
                if (!successExecution)
                {
                    javaPath = Helpers.GetJavaExePath(Path.Combine("bin", "java.exe")) ?? "";
                }

                var window = new SelectPathDialog("Select Java Path (java)", javaPath);
                var selectResult = await window.ShowDialog<string>();
                if (selectResult != null)
                {
                    _workflow.JavaPath = selectResult;
                    _settings.JavaPath = selectResult;
                    _settings.Save();
                }
            }

            if (EndStage >= WorkflowStage.ParserCompilied)
            {
                var selectedRuntime = SelectedRuntime.Runtime;
                if (!_settings.CompilerPaths.ContainsKey(selectedRuntime))
                {
                    var runtimeInfo = selectedRuntime.GetRuntimeInfo();
                    var compilerPath = runtimeInfo.DefaultCompilerPath;
                    var compilerFileName = Path.GetFileNameWithoutExtension(compilerPath);

                    var compilied = !runtimeInfo.Interpreted ? "Compiler " : "";
                    var window = new SelectPathDialog($"Select {runtimeInfo.Name} ({compilerFileName}) {compilied}Path (csc)", compilerPath);
                    var selectResult = await window.ShowDialog<string>();
                    if (selectResult != null)
                    {
                        _workflow.CompilerPaths[selectedRuntime] = selectResult;
                        _settings.CompilerPaths[selectedRuntime] = selectResult;
                        _settings.Save();
                    }
                }
            }

            await _workflow.ProcessAsync();

            if (_workflow.GrammarCheckedState != null)
            {
                UpdateRules();
            }
        }

        private void UpdateRules()
        {
            var workflowRules = IsPreprocessor ? _workflow.GrammarCheckedState.PreprocessorRules : _workflow.GrammarCheckedState.Rules;
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
