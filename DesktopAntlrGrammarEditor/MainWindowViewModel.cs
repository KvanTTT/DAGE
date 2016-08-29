using AntlrGrammarEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace DesktopAntlrGrammarEditor
{
    public class MainWindowViewModel : ReactiveObject
    {
        private Window _window;
        private Settings _settings;
        private Grammar _grammar;
        private Workflow _workflow;
        private FileName _openedGrammarFile;
        private bool _grammarFileChanged, _textFileChanged;
        private string _currentState = WorkflowStage.Input.ToString();
        private TextBox _grammarTextBox, _textTextBox;
        private string _grammarErrorsText = "Grammar Errors (0)";
        private string _textErrorsText = "Text Errors (0)";
        private ListBox _grammarErrorsListBox, _sourceCodeErrorsListBox;
        private string _tokens, _tree;
        private bool _autoprocessing;

        public MainWindowViewModel(Window window)
        {
            _window = window;
            _grammarTextBox = _window.Find<TextBox>("GrammarTextBox");
            _textTextBox = _window.Find<TextBox>("TextTextBox");
            _grammarErrorsListBox = _window.Find<ListBox>("GrammarErrorsListBox");
            _sourceCodeErrorsListBox = _window.Find<ListBox>("TextErrorsListBox");
            _grammarErrorsListBox.DoubleTapped += ErrorsListBox_DoubleTapped;
            _sourceCodeErrorsListBox.DoubleTapped += ErrorsListBox_DoubleTapped;

            _settings = Settings.Load();

            _workflow = new Workflow();
            _workflow.JavaPath = _settings.JavaPath;
            _workflow.StateChanged += workflow_StateChanged;
            _workflow.NewErrorEvent += workflow_NewErrorEvent;
            _workflow.ClearErrorsEvent += workflow_ClearErrorsEvent;
            _workflow.TextParsedOutputEvent += workflow_TextParsedOutputEvent;
            _window = window;

            if (string.IsNullOrEmpty(_settings.AgeFileName))
            {
                _grammar = GrammarFactory.CreateOrOpenDefaultGrammar(Settings.DefaultDirectory, "NewGrammar");
                _settings.AgeFileName = Path.Combine(Settings.DefaultDirectory, "NewGrammar.age");
                _grammar.AgeFileName = _settings.AgeFileName;
                _settings.Save();
            }
            else
            {
                _grammar = Grammar.Load(_settings.AgeFileName);
            }

            _workflow.Grammar = _grammar;
            _workflow.Text = _settings.Text;
            _textTextBox.Text = _settings.Text;
            SelectedRuntime = _grammar.Runtimes.First();

            InitFiles();
            if (string.IsNullOrEmpty(_settings.OpenedGrammarFile))
            {
                OpenedGrammarFile = GrammarFiles.First();
            }
            else
            {
                OpenedGrammarFile = new FileName(_settings.OpenedGrammarFile);
            }

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
            _window.GetObservable(Window.WidthProperty)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Subscribe(width =>
                {
                    _settings.Width = width;
                    _settings.WindowState = _window.WindowState;
                    _settings.Save();
                });
            _window.GetObservable(Window.HeightProperty)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Subscribe(height =>
                {
                    _settings.Height = height;
                    _settings.WindowState = _window.WindowState;
                    _settings.Save();
                });
            _window.Closed += window_Closed;

            _grammarTextBox.GetObservable(TextBox.TextProperty)
                .Subscribe(str => _grammarFileChanged = true);
            _grammarTextBox.GetObservable(TextBox.TextProperty)
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .Subscribe(str =>
                {
                    _workflow.Grammar = _grammar;
                    if (AutoProcessing)
                    {
                        SaveGrammarFileIfRequired();
                        _workflow.Process();
                    }
                });

            _textTextBox.GetObservable(TextBox.TextProperty)
                .Subscribe(str => _textFileChanged = true);
            _textTextBox.GetObservable(TextBox.TextProperty)
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .Subscribe(str =>
                {
                    _workflow.Text = str;
                    if (AutoProcessing)
                    {
                        ProcessAndUpdateUI();
                    }
                    if (_textFileChanged)
                    {
                        _workflow.Text = str;
                        _settings.Text = _workflow.Text;
                        _settings.Save();
                    }
                });

            NewGrammarCommand.Subscribe(async _ =>
            {
                var newGrammarWindow = new NewGrammarWindow();
                Grammar grammar = await newGrammarWindow.ShowDialog<Grammar>();
                if (grammar != null)
                {
                    OpenGrammar(grammar);
                }
            });

            OpenGrammarCommand.Subscribe(async _ =>
            {
                var openDialog = new OpenFileDialog();
                openDialog.Filters.Add(new FileDialogFilter() { Name = "Antlr Grammar Editor", Extensions = new List<string>() { "age" } });
                string[] fileNames = await openDialog.ShowAsync();
                if (fileNames != null)
                {
                    var grammar = Grammar.Load(fileNames.First());
                    OpenGrammar(grammar);
                }
            });

            ProcessCommand.Subscribe(_ =>
            {
                SaveGrammarFileIfRequired();
                _workflow.Grammar = _grammar;
                _workflow.Text = _textTextBox.Text;
                _settings.Text = _workflow.Text;
                _settings.Save();
                ProcessAndUpdateUI();
            });
        }

        private void OpenGrammar(Grammar grammar)
        {
            _grammar = grammar;
            _workflow.Grammar = grammar;
            _settings.AgeFileName = grammar.AgeFileName;
            _settings.Save();
            InitFiles();
            OpenedGrammarFile = GrammarFiles.First();
        }

        public FileName OpenedGrammarFile
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
                    _openedGrammarFile = value;
                    _grammarTextBox.Text = File.ReadAllText(_openedGrammarFile.LongFileName);
                    _grammarFileChanged = false;

                    if (IsParserOpened)
                    {
                        _workflow.Grammar = _grammar;
                        _workflow.EndStage = WorkflowStage.GrammarChecked;
                        _workflow.Process();
                        _workflow.EndStage = WorkflowStage.TextParsed;

                        UpdateRules();
                    }

                    _settings.OpenedGrammarFile = value.LongFileName;
                    _settings.Save();

                    this.RaisePropertyChanged(nameof(IsParserOpened));
                    this.RaisePropertyChanged(nameof(OpenedGrammarFile));
                }
            }
        }

        public bool IsParserOpened => !OpenedGrammarFile.ShortFileName.Contains(GrammarFactory.LexerPostfix);

        public bool IsPreprocessor => OpenedGrammarFile.ShortFileName.Contains(GrammarFactory.PreprocessorPostfix);

        public ObservableCollection<FileName> GrammarFiles { get; } = new ObservableCollection<FileName>();

        public string Root
        {
            get
            {
                return IsPreprocessor ? _grammar.PreprocessorRoot : _grammar.Root;
            }
            set
            {
                var currentRoot = IsPreprocessor ? _grammar.PreprocessorRoot : _grammar.Root;
                if (currentRoot != value)
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
                        ProcessAndUpdateUI();
                    }
                    _grammar.Save();
                    this.RaisePropertyChanged(nameof(Root));
                }
            }
        }

        public ObservableCollection<string> Rules { get; } = new ObservableCollection<string>();

        public Runtime SelectedRuntime
        {
            get
            {
                return _grammar.Runtimes.First();
            }
            set
            {
                if (_grammar.Runtimes.First() != value)
                {
                    _workflow.Runtime = value;
                    _grammar.Runtimes.Clear();
                    _grammar.Runtimes.Add(value);
                    _grammar.Save();
                    this.RaisePropertyChanged(nameof(SelectedRuntime));
                    if (AutoProcessing)
                    {
                        ProcessAndUpdateUI();
                    }
                }
            }
        }

        public string CurrentState
        {
            get
            {
                return _currentState;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _currentState, value);
            }
        }

        public ObservableCollection<Runtime> Runtimes { get; } = new ObservableCollection<Runtime>((Runtime[])Enum.GetValues(typeof(Runtime)));

        public string GrammarErrorsText
        {
            get
            {
                return _grammarErrorsText;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _grammarErrorsText, value);
            }
        }

        public ObservableCollection<object> GrammarErrors { get; } = new ObservableCollection<object>();

        public ObservableCollection<object> TextErrors { get; } = new ObservableCollection<object>();

        public string TextErrorsText
        {
            get
            {
                return _textErrorsText;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _textErrorsText, value);
            }
        }

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

        public bool AutoProcessing
        {
            get
            {
                return _autoprocessing;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _autoprocessing, value);
                _settings.Autoprocessing = _autoprocessing;
                _settings.Save();
            }
        }

        private void workflow_StateChanged(object sender, WorkflowState e)
        {
            Dispatcher.UIThread.InvokeAsync(() => CurrentState = e.Stage.ToString());
        }

        private void workflow_NewErrorEvent(object sender, Tuple<WorkflowStage, ParsingError> e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                switch (e.Item1)
                {
                    case WorkflowStage.GrammarChecked:
                    case WorkflowStage.ParserGenerated:
                    case WorkflowStage.ParserCompilied:
                        GrammarErrors.Add(e.Item2);
                        GrammarErrorsText = $"Grammar Errors ({GrammarErrors.Count})";
                        break;
                    case WorkflowStage.TextTokenized:
                    case WorkflowStage.TextParsed:
                        TextErrors.Add(e.Item2);
                        TextErrorsText = $"Text Errors ({TextErrors.Count})";
                        break;
                }
            });
        }

        private void workflow_ClearErrorsEvent(object sender, WorkflowStage e)
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
                        GrammarErrorsText = $"Grammar Errors ({GrammarErrors.Count})";
                    }
                    else
                    {
                        TextErrorsText = $"Text Errors ({TextErrors.Count})";
                    }
                });
            }
        }

        private void workflow_TextParsedOutputEvent(object sender, Tuple<TextParsedOutput, object> e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            { 
                switch (e.Item1)
                {
                    case TextParsedOutput.LexerTime:
                        break;
                    case TextParsedOutput.ParserTime:
                        break;
                    case TextParsedOutput.Tokens:
                        Tokens = (string)e.Item2;
                        break;
                    case TextParsedOutput.Tree:
                        Tree = (string)e.Item2;
                        break;
                }
            });
        }

        private void window_Closed(object sender, EventArgs e)
        {
            SaveGrammarFileIfRequired();
            _settings.Left = _window.Position.X;
            _settings.Top = _window.Position.Y;
            _settings.Save();
        }

        private void ErrorsListBox_DoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ListBox listBox = (ListBox)sender;
            listBox.Focus();

            var parsingError = listBox.SelectedItem as ParsingError;
            if (parsingError != null)
            {
                TextBox textBox = listBox == _grammarErrorsListBox ? _grammarTextBox : _textTextBox;
                textBox.SelectionStart = Utils.LineColumnToLinear(textBox.Text, parsingError.Line, parsingError.Column);
                textBox.SelectionEnd = textBox.SelectionStart + 1;
            }
        }

        private void InitFiles()
        {
            GrammarFiles.Clear();
            foreach (var file in _grammar.Files)
            {
                GrammarFiles.Add(new FileName(file));
            }
        }

        private void SaveGrammarFileIfRequired()
        {
            if (_grammarFileChanged)
            {
                File.WriteAllText(_openedGrammarFile.LongFileName, _grammarTextBox.Text);
                _grammarFileChanged = false;
            }
        }

        private async void ProcessAndUpdateUI()
        {
            _workflow.RollbackToPreviousStageIfErrors();
            var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Directory.SetCurrentDirectory(assemblyPath);

            if (string.IsNullOrEmpty(_settings.JavaPath))
            {
                var javaPath = Helpers.GetJavaInstallationPath();
                if (javaPath != null)
                {
                    javaPath = Path.Combine(javaPath, "bin\\Java.exe");
                    if (!File.Exists(javaPath))
                    {
                        javaPath = null;
                    }
                }
                if (javaPath == null)
                {
                    javaPath = "";
                }
                var window = new SelectPathDialog("Select Java path", javaPath);
                var selectResult = await window.ShowDialog<string>();
                if (selectResult != null)
                {
                    _workflow.JavaPath = selectResult;
                    _settings.JavaPath = selectResult;
                    _settings.Save();
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
            else if (!Rules.Contains(Root))
            {
                _grammar.Save();
                this.RaisePropertyChanged(nameof(Root));
            }
        }
    }
}
