using AntlrGrammarEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaEdit;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AntlrGrammarEditor.Diagnoses;
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.Sources;
using AntlrGrammarEditor.WorkflowState;
using Avalonia.Layout;
using DynamicData;

namespace DesktopAntlrGrammarEditor
{
    public class MainWindowViewModel : ReactiveObject
    {
        private static readonly RuntimeWrapper AutodetectRuntime = new RuntimeWrapper(null);

        private readonly Window _window;
        private readonly Settings _settings;
        private Workflow? _workflow;
        private FileName _openedGrammarFile = FileName.Empty;
        private FileName _openedTextFile = FileName.Empty;
        private FileState _grammarFileState, _textFileState;
        private readonly TextEditor _grammarTextBox;
        private readonly TextEditor _textTextBox;
        private readonly TextEditor _tokensTextBox;
        private readonly TextEditor _parseTreeTextBox;
        private readonly ListBox _grammarErrorsListBox;
        private readonly ListBox _textErrorsListBox;
        private bool _autoprocessing;
        private WorkflowStage _endStage = WorkflowStage.TextParsed;
        private Source? _grammarCode;
        private Source? _text;
        private LineColumnTextSpan _grammarLineColumn = new LineColumnTextSpan(1, 1, new Source("", ""));
        private LineColumnTextSpan _textLineColumn = new LineColumnTextSpan(1, 1, new Source("", ""));
        private static readonly Dictionary<Runtime, RuntimeWrapper> SupportedRuntimeDictionary;

        private Grammar? Grammar => _workflow?.Grammar;

        static MainWindowViewModel()
        {
            SupportedRuntimeDictionary = ((SupportedRuntime[])Enum.GetValues(typeof(SupportedRuntime))).ToDictionary(
                runtime => (Runtime)runtime,
                runtime => new RuntimeWrapper(runtime));
        }

        public MainWindowViewModel(Window window)
        {
            _window = window;
            _grammarTextBox = _window.Find<TextEditor>("GrammarTextBox");
            _textTextBox = _window.Find<TextEditor>("TextTextBox");
            _grammarErrorsListBox = _window.Find<ListBox>("GrammarErrorsListBox");
            _textErrorsListBox = _window.Find<ListBox>("TextErrorsListBox");
            _parseTreeTextBox = _window.Find<TextEditor>("ParseTreeTextBox");
            _tokensTextBox = _window.Find<TextEditor>("TokensTextBox");

            _grammarTextBox.SetupHightlighting(".g4");

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
            if (_settings.Left > 0 && _settings.Top > 0)
            {
                _window.Position = new PixelPoint(_settings.Left, _settings.Top);
            }

            NewGrammarCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var newGrammarWindow = new NewGrammarWindow();
                Grammar localGrammar = await newGrammarWindow.ShowDialog<Grammar>(_window);
                if (localGrammar != null)
                {
                    OpenGrammar(localGrammar);
                }
                _window.Activate();
            });

            OpenGrammarCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var openGrammarDialog = new OpenFileDialog
                {
                    Title = "Enter grammar file name",
                    AllowMultiple = false
                };

                string? grammarFileName = null;
                try
                {
                    grammarFileName = (await openGrammarDialog.ShowAsync(_window)).FirstOrDefault();

                    if (!string.IsNullOrEmpty(grammarFileName))
                    {
                        var localGrammar = GrammarFactory.Open(grammarFileName);
                        OpenGrammar(localGrammar);
                    }
                }
                catch (Exception ex)
                {
                    await ShowOpenFileErrorMessage(grammarFileName ?? "", ex.Message);
                }
            });

            ProcessCommand = ReactiveCommand.Create(() =>
            {
                bool changed = false;

                if (_grammarFileState == FileState.Changed)
                {
                    RollbackGrammarIfRequired();

                    File.WriteAllText(_openedGrammarFile.FullFileName, _grammarTextBox.Text);
                    _grammarFileState = FileState.Saved;
                    changed = true;
                }

                if (_textFileState == FileState.Changed && !string.IsNullOrEmpty(_openedTextFile.FullFileName))
                {
                    File.WriteAllText(_openedTextFile.FullFileName, _textTextBox.Text);
                    if (_workflow != null)
                        _workflow.TextFileName = _openedTextFile.FullFileName;
                    _textFileState = FileState.Saved;
                    changed = true;
                }

                if (changed)
                {
                    _settings.Save();
                }

                Process();
            });

            NewTextFile = ReactiveCommand.CreateFromTask(async () =>
            {
                var grammar = Grammar;
                if (grammar == null)
                    return;

                var filters = new List<FileDialogFilter>();
                if (!string.IsNullOrEmpty(Grammar?.TextExtension))
                {
                    filters.Add(new FileDialogFilter
                    {
                        Name = $"{Grammar.Name} parsing file",
                        Extensions = new List<string> { Grammar.TextExtension }
                    });
                }

                filters.Add(new FileDialogFilter
                {
                    Name = "All files",
                    Extensions = new List<string> { "*" }
                });

                var defaultExamplesDirectory = grammar.ExamplesDirectory;
                if (!Directory.Exists(defaultExamplesDirectory))
                    Directory.CreateDirectory(defaultExamplesDirectory);

                var defaultFileName = GrammarFilesManager.GetNotConflictingTextFile(grammar);

                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Enter file name",
                    DefaultExtension = grammar.TextExtension,
                    Filters = filters,
                    Directory = defaultExamplesDirectory,
                    InitialFileName = Path.GetFileName(defaultFileName)
                };

                string fileName = await saveFileDialog.ShowAsync(_window);
                if (fileName != null)
                {
                    File.WriteAllText(fileName, "");
                    var newFile = new FileName(fileName);
                    if (!TextFiles.Contains(newFile))
                    {
                        TextFiles.Add(newFile);
                        OpenedTextFile = TextFiles.Last();
                    }
                }
            });

            OpenTextFile = ReactiveCommand.CreateFromTask(async () =>
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
                        }
                    }
                    if (atLeastOneFileHasBeenAdded)
                    {
                        OpenedTextFile = TextFiles.Last();
                    }
                }
            });

            RemoveTextFile = ReactiveCommand.CreateFromTask(async () =>
            {
                var openedTextFile = OpenedTextFile;

                if (openedTextFile != null)
                {
                    string shortFileName = openedTextFile.ShortFileName;
                    string fullFileName = openedTextFile.FullFileName;
                    var index = TextFiles.IndexOf(openedTextFile);
                    TextFiles.Remove(openedTextFile);
                    index = Math.Min(index, TextFiles.Count - 1);
                    if (await MessageBox.ShowDialog(_window, $"Do you want to delete the file {shortFileName}?", "",
                        MessageBoxType.YesNo))
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
                }
            });



            SetupWindowSubscriptions();
        }

        private void SetupWindowSubscriptions()
        {
            Observable.FromEventPattern(
                    ev => _window.Opened += ev, ev => _window.Opened -= ev)
                .Subscribe(ev =>
                {
                    Grammar? grammar;

                    bool openDefaultGrammar = false;
                    if (string.IsNullOrEmpty(_settings.GrammarFileName))
                    {
                        openDefaultGrammar = true;
                        grammar = GrammarFilesManager.GetGrammarWithNotConflictingName(Utils.DefaultGrammarsDirectory);
                    }
                    else
                    {
                        try
                        {
                            grammar = GrammarFactory.Open(_settings.GrammarFileName);
                        }
                        catch (Exception ex)
                        {
                            ShowOpenFileErrorMessage(_settings.GrammarFileName, ex.Message).ConfigureAwait(false);

                            _settings.OpenedGrammarFile = null;
                            openDefaultGrammar = true;
                            grammar = GrammarFilesManager.GetGrammarWithNotConflictingName(
                                Utils.DefaultGrammarsDirectory);
                        }
                    }

                    if (openDefaultGrammar)
                    {
                        new GrammarFilesManager(grammar).CreateFiles();
                        var notConflictingTextFile = GrammarFilesManager.GetNotConflictingTextFile(grammar);
                        TextFiles.Add(new FileName(notConflictingTextFile));
                        _settings.GrammarFileName = grammar.FullFileName;
                        _settings.Save();
                    }

                    _workflow = new Workflow(grammar);

                    OpenGrammar(grammar, _settings.OpenedGrammarFile, _settings.OpenedTextFile);

                    this.RaisePropertyChanged(nameof(SelectedRuntime));
                    AutoProcessing = _settings.Autoprocessing;

                    SetupWorkflowSubscriptions();
                    SetupTextBoxSubscriptions();
                });

            _window.GetObservable(Layoutable.WidthProperty)
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

            _window.GetObservable(Layoutable.HeightProperty)
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

            Observable.FromEventPattern<PixelPointEventArgs>(
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

        public FileName? OpenedGrammarFile
        {
            get => _openedGrammarFile;
            set
            {
                SaveGrammarFileIfRequired();
                if (value != null && !value.Equals(_openedGrammarFile))
                {
                    try
                    {
                        _grammarTextBox.Text = File.ReadAllText(value.FullFileName);

                        _openedGrammarFile = value;
                        _grammarFileState = FileState.Opened;

                        _workflow?.GetGrammarCheckedState();
                        UpdateParserRules();

                        _settings.OpenedGrammarFile = value.FullFileName;
                        _settings.Save();

                        _grammarTextBox.SetupHightlighting(value.FullFileName);
                        _grammarCode = new Source(_openedGrammarFile.FullFileName, _grammarTextBox.Text);

                        this.RaisePropertyChanged();
                    }
                    catch (Exception ex)
                    {
                        ShowOpenFileErrorMessage(_openedGrammarFile.FullFileName, ex.Message).ConfigureAwait(false);
                    }
                }
            }
        }

        public ObservableCollection<FileName> GrammarFiles => new ObservableCollection<FileName>(
            _workflow?.GetGrammarCheckedState().GrammarInfos.Select(info => new FileName(info.Source.Name)) ??
            Enumerable.Empty<FileName>());

        public string? Root
        {
            get => _workflow?.Root;
            set
            {
                var currentRoot = _workflow?.Root;
                if (currentRoot != value)
                {
                    if (_workflow != null)
                        _workflow.Root = value;

                    if (AutoProcessing)
                    {
                        Process();
                    }

                    this.RaisePropertyChanged();
                }
            }
        }

        public string? PackageName
        {
            get => _workflow?.PackageName;
            set
            {
                if (_workflow == null)
                    return;

                var currentPackageName = _workflow.PackageName;
                if (currentPackageName != value)
                {
                    _workflow.PackageName = value;

                    if (AutoProcessing)
                    {
                        Process();
                    }

                    this.RaisePropertyChanged();
                }
            }
        }

        public PredictionMode? PredictionMode
        {
            get => _workflow?.PredictionMode;
            set
            {
                var currentPredictionMode = _workflow?.PredictionMode;
                if (currentPredictionMode != value)
                {
                    if (_workflow != null)
                        _workflow.PredictionMode = value;

                    if (AutoProcessing)
                    {
                        Process();
                    }

                    this.RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<string> ParserRules { get; } = new ObservableCollection<string>();

        public RuntimeWrapper SelectedRuntime
        {
            get => _workflow?.Runtime == null
                ? AutodetectRuntime
                : SupportedRuntimeDictionary[_workflow.Runtime.Value];
            set
            {
                var runtime = value.Runtime.HasValue ? (Runtime?)value.Runtime : null;
                if (runtime != _workflow?.Runtime)
                {
                    if (_workflow != null)
                        _workflow.Runtime = runtime;

                    this.RaisePropertyChanged();
                    if (AutoProcessing)
                        Process();
                }
            }
        }



        public string CurrentState => (_workflow?.CurrentState.Stage ?? WorkflowStage.Input).ToString();

        public ObservableCollection<RuntimeWrapper> SupportedRuntimes { get; } = new ObservableCollection<RuntimeWrapper>(
            new [] { AutodetectRuntime }.Union(SupportedRuntimeDictionary.Values));

        public string GrammarErrorsText => $"Grammar Errors ({GrammarErrors.Count})";

        public bool GrammarErrorsExpanded => GrammarErrors.Count > 0;

        public bool TextBoxEnabled => !string.IsNullOrEmpty(_openedTextFile.FullFileName);

        public ObservableCollection<object> GrammarErrors { get; } = new ObservableCollection<object>();

        public ObservableCollection<FileName> TextFiles { get; } = new ObservableCollection<FileName>();

        public ObservableCollection<PredictionMode> PredictionModes { get; } = new ObservableCollection<PredictionMode>
        {
            AntlrGrammarEditor.Processors.PredictionMode.LL,
            AntlrGrammarEditor.Processors.PredictionMode.SLL,
            AntlrGrammarEditor.Processors.PredictionMode.FullLL
        };

        public FileName? OpenedTextFile
        {
            get => _openedTextFile;
            set
            {
                SaveTextFileIfRequired();

                if (!string.IsNullOrEmpty(value?.FullFileName) && !value.Equals(_openedTextFile))
                {
                    _textTextBox.IsEnabled = true;
                    _tokensTextBox.IsEnabled = true;
                    _parseTreeTextBox.IsEnabled = true;
                    _openedTextFile = value;

                    _textTextBox.SetupHightlighting(value.FullFileName);

                    try
                    {
                        string fileName = value.FullFileName;
                        if (!File.Exists(value.FullFileName))
                        {
                            File.WriteAllText(value.FullFileName, "");
                        }
                        _textTextBox.Text = File.ReadAllText(fileName);
                    }
                    catch (Exception ex)
                    {
                        _textTextBox.Text = "";
                        ShowOpenFileErrorMessage(_openedTextFile.FullFileName, ex.Message).ConfigureAwait(false);
                    }
                    if (_workflow != null)
                        _workflow.TextFileName = value.FullFileName;
                    _textFileState = FileState.Opened;

                    _settings.OpenedTextFile = value.FullFileName;
                    _settings.Save();

                    _text = new Source(_openedTextFile.ShortFileName, _textTextBox.Text);
                    ClearParseResult();
                    this.RaisePropertyChanged();
                }

                if (string.IsNullOrEmpty(value?.FullFileName))
                {
                    _textTextBox.IsEnabled = false;
                    _tokensTextBox.IsEnabled = false;
                    _parseTreeTextBox.IsEnabled = false;
                    _openedTextFile = FileName.Empty;
                    _textTextBox.Text = "";
                    if (_workflow != null)
                        _workflow.TextFileName = null;
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

        public bool IsParserExists => _workflow?.GetGrammarCheckedState().GrammarProjectType != GrammarProjectType.Lexer;

        public LineColumnTextSpan GrammarCursorPosition
        {
            get => _grammarLineColumn;
            set
            {
                if (value != _grammarLineColumn)
                {
                    _grammarLineColumn = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public LineColumnTextSpan TextCursorPosition
        {
            get => _textLineColumn;
            set
            {
                if (value != _textLineColumn)
                {
                    _textLineColumn = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        private LineColumnTextSpan GetSelectionLineColumn(TextEditor textBox, Source source)
        {
            try
            {
                int start = textBox.SelectionStart;
                int end = textBox.SelectionStart + textBox.SelectionLength;
                if (start > end)
                {
                    (start, end) = (end, start);
                }

                source.PositionToLineColumn(start, out int startLine, out int startColumn);
                source.PositionToLineColumn(end, out int endLine, out int endColumn);

                return new LineColumnTextSpan(startLine, startColumn, endLine, endColumn, source);
            }
            catch
            {
                return new LineColumnTextSpan();
            }
        }

        public ReactiveCommand<Unit, Unit> NewGrammarCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenGrammarCommand { get; }

        public ReactiveCommand<Unit, Unit> ProcessCommand { get; }

        public ReactiveCommand<Unit, Unit> NewTextFile { get; }

        public ReactiveCommand<Unit, Unit> OpenTextFile { get; }

        public ReactiveCommand<Unit, Unit> RemoveTextFile { get; }

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

        private void SetupWorkflowSubscriptions()
        {
            Observable.FromEventPattern<WorkflowState>(
                ev => _workflow!.StateChanged += ev, ev => _workflow!.StateChanged -= ev)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ev => this.RaisePropertyChanged(nameof(CurrentState)));

            Observable.FromEventPattern<Diagnosis>(
                ev => _workflow!.DiagnosisEvent += ev, ev => _workflow!.DiagnosisEvent -= ev)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ev =>
                {
                    switch (ev.EventArgs.WorkflowStage)
                    {
                        case WorkflowStage.GrammarChecked:
                        case WorkflowStage.ParserGenerated:
                        case WorkflowStage.ParserCompiled:
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
                 ev => _workflow!.TextParsedOutputEvent += ev,
                 ev => _workflow!.TextParsedOutputEvent -= ev)
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

            _workflow!.ClearErrorsEvent += Workflow_ClearErrorsEvent;
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

            grammarTextBoxObservable.Subscribe(x => _grammarFileState = FileState.Changed);

            textBoxObservable.Subscribe(x => _textFileState = FileState.Changed);

            grammarTextBoxObservable
                .Throttle(TimeSpan.FromMilliseconds(200))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => _grammarCode = new Source(_openedGrammarFile.FullFileName, _grammarTextBox.Text));

            textBoxObservable
                .Throttle(TimeSpan.FromMilliseconds(200))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => _text = new Source(_openedTextFile.FullFileName, _text?.Text ?? ""));

            Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    if (_grammarCode != null)
                    {
                        GrammarCursorPosition = GetSelectionLineColumn(_grammarTextBox, _grammarCode);
                    }

                    if (_text != null)
                    {
                        TextCursorPosition = GetSelectionLineColumn(_textTextBox, _text);
                    }
                });

            grammarTextBoxObservable
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .Subscribe(x =>
                {
                    if (_grammarFileState == FileState.Changed)
                    {
                        RollbackGrammarIfRequired();

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
                        _workflow!.TextFileName = OpenedTextFile?.FullFileName;
                        _workflow.RollbackToStage(WorkflowStage.ParserCompiled);

                        if (AutoProcessing)
                        {
                            SaveTextFileIfRequired();
                            Process();
                        }
                    }
                });
        }

        private void RollbackGrammarIfRequired()
        {
            _workflow?.RollbackToStage(WorkflowStage.Input);
        }

        private void OpenGrammar(Grammar grammar, string? openedGrammarFile = null, string? openedTextFile = null)
        {
            _workflow!.Grammar = grammar;
            _settings.GrammarFileName = grammar.FullFileName;
            _settings.Save();
            _openedGrammarFile = FileName.Empty;
            _openedTextFile = FileName.Empty;
            ParserRules.Clear();
            var grammarCheckedState = _workflow.GetGrammarCheckedState();
            InitFiles();
            OpenedGrammarFile = openedGrammarFile == null ||
                                grammarCheckedState.GrammarInfos.All(info => info.Source.Name != openedGrammarFile)
                ? GrammarFiles.LastOrDefault()
                : new FileName(openedGrammarFile);
            OpenedTextFile = openedTextFile == null ||
                             TextFiles.All(textFile => textFile.FullFileName != openedTextFile)
                ? TextFiles.FirstOrDefault()
                : new FileName(openedTextFile);
            PackageName = grammar.Package;
            Root = grammar.Root;
            this.RaisePropertyChanged(nameof(IsParserExists));
        }

        private void InitFiles()
        {
            this.RaisePropertyChanged(nameof(GrammarFiles));

            TextFiles.Clear();
            var textExtension = Grammar?.TextExtension;
            var examplesDirectory = Grammar?.ExamplesDirectory;

            if (!Directory.Exists(examplesDirectory))
                Directory.CreateDirectory(examplesDirectory);

            IEnumerable<string> textFileNames = Directory.GetFiles(examplesDirectory, "*", SearchOption.AllDirectories);
            if (textExtension != null)
            {
                var dotTextExtension = "." + textExtension;
                textFileNames = textFileNames.Where(fileName => Path.GetExtension(fileName) == dotTextExtension);
            }

            TextFiles.AddRange(textFileNames.Select(fileName => new FileName(fileName)));
        }

        private void Workflow_ClearErrorsEvent(object? sender, WorkflowStage e)
        {
            ObservableCollection<object> errorsList = GrammarErrors;
            bool grammarErrors = false;

            switch (e)
            {
                case WorkflowStage.Input:
                case WorkflowStage.GrammarChecked:
                case WorkflowStage.ParserGenerated:
                case WorkflowStage.ParserCompiled:
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
                        if (errorsList[i] is Diagnosis diagnosis)
                        {
                            if (diagnosis.WorkflowStage == e)
                            {
                                errorsList.RemoveAt(i);
                                continue;
                            }
                        }
                        i++;
                    }

                    string errorsTextPropertyName;
                    string errorsExpandedPropertyName;

                    if (grammarErrors)
                    {
                        errorsTextPropertyName = nameof(GrammarErrorsText);
                        errorsExpandedPropertyName = nameof(GrammarErrorsExpanded);
                    }
                    else
                    {
                        errorsTextPropertyName = nameof(TextErrorsText);
                        errorsExpandedPropertyName = nameof(TextErrorsExpanded);
                    }

                    this.RaisePropertyChanged(errorsTextPropertyName);
                    this.RaisePropertyChanged(errorsExpandedPropertyName);
                });
            }
        }

        private void ErrorsListBox_DoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender == null)
                return;

            ListBox listBox = (ListBox)sender;
            listBox.Focus();

            if (listBox.SelectedItem is Diagnosis diagnosis)
            {
                TextEditor textBox = ReferenceEquals(listBox, _grammarErrorsListBox) ? _grammarTextBox : _textTextBox;
                var textSpan = diagnosis.TextSpan;
                if (textSpan == null)
                {
                    return;
                }

                var textSpanValue = textSpan.Value;
                string diagnosisFileName = textSpanValue.Source.Name;
                if (string.IsNullOrEmpty(diagnosisFileName))
                {
                    return;
                }

                if (ReferenceEquals(textBox, _grammarTextBox))
                {
                    OpenedGrammarFile = new FileName(diagnosisFileName);
                }

                TextSpan selectTextSpan;
                if (textSpan.Value.Length != 0)
                {
                    selectTextSpan = textSpanValue;
                }
                else
                {
                    int beginIndex = textSpanValue.Start;
                    if (textSpanValue.End >= textBox.Text.Length)
                    {
                        beginIndex = textBox.Text.Length - 1;
                        if (beginIndex < 0)
                            beginIndex = 0;
                    }

                    selectTextSpan = new TextSpan(beginIndex, 1, textSpanValue.Source);
                }

                int length = textBox.Text.Length;
                if (selectTextSpan.Start >= 0 && selectTextSpan.Start <= length &&
                    selectTextSpan.Length >= 0 && selectTextSpan.End <= length)
                {
                    textBox.Focus();
                    textBox.Select(selectTextSpan.Start, selectTextSpan.Length);
                }
            }
        }

        private void SaveGrammarFileIfRequired()
        {
            if (_grammarFileState == FileState.Changed)
            {
                File.WriteAllText(_openedGrammarFile.FullFileName, _grammarTextBox.Text);
                _grammarFileState = FileState.Saved;
            }
        }

        private void SaveTextFileIfRequired()
        {
            if (_textFileState == FileState.Changed && !string.IsNullOrEmpty(_openedTextFile.FullFileName))
            {
                File.WriteAllText(_openedTextFile.FullFileName, _textTextBox.Text);
                _textFileState = FileState.Saved;
            }
        }

        private async void Process()
        {
            if (_workflow == null)
            {
                await Task.Delay(0);
                return;
            }

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

                await MessageBox.ShowDialog(_window, message);

                return;
            }

            RuntimeInfo? runtimeInfo = SelectedRuntime.Runtime != null
                ? ((Runtime)SelectedRuntime.Runtime.Value).GetRuntimeInfo()
                : null;

            if (EndStage >= WorkflowStage.ParserCompiled && runtimeInfo is { Version: null })
            {
                string message = $"Install {runtimeInfo.RuntimeToolName}";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    message +=
                        $" and make sure path to {runtimeInfo.RuntimeToolName} added to the PATH environment variable";
                }

                await MessageBox.ShowDialog(_window, message);

                return;
            }

            await _workflow.ProcessAsync();

            UpdateParserRules();
        }

        private void UpdateParserRules()
        {
            var grammarCheckedState = _workflow?.CurrentState.GetState<GrammarCheckedState>();
            if (grammarCheckedState == null)
            {
                return;
            }

            var workflowRules = grammarCheckedState.GrammarInfos.FirstOrDefault(info => info.Type == GrammarFileType.Parser)?.Rules;

            if (workflowRules == null)
            {
                ParserRules.Clear();
                Root = null;
            }
            else if (!ParserRules.SequenceEqual(workflowRules))
            {
                ParserRules.Clear();
                foreach (var rule in workflowRules)
                {
                    ParserRules.Add(rule);
                }

                if (Root == null || !ParserRules.Contains(Root))
                {
                    Root = ParserRules[0];
                }
                else
                {
                    this.RaisePropertyChanged(nameof(Root));
                }
            }
        }

        private async Task ShowOpenFileErrorMessage(string fileName, string exceptionMessage)
        {
            var messageBox = new MessageBox($"Error while opening {fileName} file: {exceptionMessage}", "Error");
            await messageBox.ShowDialog(_window);
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
