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
using AntlrGrammarEditor.Processors;
using AntlrGrammarEditor.WorkflowState;

namespace DesktopAntlrGrammarEditor
{
    public class MainWindowViewModel : ReactiveObject
    {
        private static readonly RuntimeInfoWrapper AutodetectRuntime = new RuntimeInfoWrapper(null);

        private readonly Dictionary<Runtime, RuntimeInfoWrapper> _runtimeInfoWrappers;
        private readonly Window _window;
        private readonly Settings _settings;
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
        private WorkflowStage _endStage = WorkflowStage.TextParsed;
        private CodeSource _grammarCode = CodeSource.Empty;
        private CodeSource _text = CodeSource.Empty;
        private LineColumnTextSpan _grammarLineColumn;
        private LineColumnTextSpan _textLineColumn;

        private Grammar Grammar => _workflow?.Grammar;

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

            Grammar grammar = null;

            bool openDefaultGrammar = false;
            string packageName = null;
            string root = null;
            if (string.IsNullOrEmpty(_settings.GrammarFileOrDirectory))
            {
                openDefaultGrammar = true;
            }
            else
            {
                try
                {
                    grammar = GrammarFactory.Open(_settings.GrammarFileOrDirectory, out packageName, out root);
                }
                catch (Exception ex)
                {
                    ShowOpenFileErrorMessage(_settings.GrammarFileOrDirectory, ex.Message);

                    _settings.OpenedGrammarFile = "";
                    openDefaultGrammar = true;
                }
            }

            if (openDefaultGrammar)
            {
                grammar = GrammarFactory.CreateDefault();
                GrammarFactory.FillGrammarFiles(grammar, Settings.Directory, false);
                _settings.GrammarFileOrDirectory = grammar.Directory;
                _settings.Save();
            }

            _workflow = new Workflow(grammar);

            var availableRuntimes = new[]
            {
                Runtime.Java, Runtime.CSharpStandard, Runtime.CSharpOptimized, Runtime.Python2, Runtime.Python3,
                Runtime.Go, Runtime.Php
            };

            _runtimeInfoWrappers = new Dictionary<Runtime, RuntimeInfoWrapper>();

            foreach (Runtime runtime in availableRuntimes)
            {
                _runtimeInfoWrappers.Add(runtime, new RuntimeInfoWrapper(RuntimeInfo.InitOrGetRuntimeInfo(runtime)));
            }

            var list = new List<RuntimeInfoWrapper> {AutodetectRuntime};
            list.AddRange(_runtimeInfoWrappers.Values);

            Runtimes = new ObservableCollection<RuntimeInfoWrapper>(list);

            SelectedRuntime = GetAutoOrSelectedRuntime();
            PackageName = packageName;
            Root = root;

            InitFiles();
            if (string.IsNullOrEmpty(_settings.OpenedGrammarFile) || !grammar.Files.Contains(_settings.OpenedGrammarFile))
            {
                OpenedGrammarFile = GrammarFiles.FirstOrDefault();
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

                        if (_workflow.CurrentState.Stage == WorkflowStage.Input)
                        {
                            _workflow.EndStage = WorkflowStage.GrammarChecked;
                            _workflow.Process();
                            _workflow.EndStage = EndStage;
                        }
                        UpdateRules();

                        _settings.OpenedGrammarFile = value;
                        _settings.Save();

                        _grammarTextBox.SetupHightlighting(value);
                        _grammarCode = new CodeSource(_openedGrammarFile, _grammarTextBox.Text);

                        this.RaisePropertyChanged();
                    }
                    catch (Exception ex)
                    {
                        ShowOpenFileErrorMessage(_openedGrammarFile, ex.Message);
                    }
                }
            }
        }

        public ObservableCollection<string> GrammarFiles => new ObservableCollection<string>(_workflow.Grammar.Files);

        public string Root
        {
            get => _workflow.Root;
            set
            {
                var currentRoot = _workflow.Root;
                if (currentRoot != value)
                {
                    _workflow.Root = value;

                    if (AutoProcessing)
                    {
                        Process();
                    }

                    this.RaisePropertyChanged();
                }
            }
        }

        public string PackageName
        {
            get => _workflow.PackageName;
            set
            {
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
            get => _workflow.PredictionMode;
            set
            {
                var currentPredictionMode = _workflow.PredictionMode;
                if (currentPredictionMode != value)
                {
                    _workflow.PredictionMode = value;

                    if (AutoProcessing)
                    {
                        Process();
                    }

                    this.RaisePropertyChanged();
                }
            }
        }

        public ObservableCollection<string> Rules { get; } = new ObservableCollection<string>();

        public RuntimeInfoWrapper SelectedRuntime
        {
            get => GetAutoOrSelectedRuntime();
            set
            {
                if (GetAutoOrSelectedRuntime() != value)
                {
                    _workflow.Runtime = value.RuntimeInfo?.Runtime;

                    InitGrammarAndCompiledFiles();

                    if (!GrammarFiles.Contains(OpenedGrammarFile))
                    {
                        OpenedGrammarFile = GrammarFiles.First();
                    }

                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(OpenedGrammarFile));
                    if (AutoProcessing)
                    {
                        Process();
                    }
                }
            }
        }

        public RuntimeInfo DetectedRuntime => _runtimeInfoWrappers[_workflow.DetectedRuntime].RuntimeInfo;

        private RuntimeInfoWrapper GetAutoOrSelectedRuntime() =>
            _workflow.Runtime.HasValue
                ? _runtimeInfoWrappers[_workflow.Runtime.Value]
                : AutodetectRuntime;

        public string CurrentState => _workflow.CurrentState.Stage.ToString();

        public ObservableCollection<RuntimeInfoWrapper> Runtimes { get; }

        public string GrammarErrorsText => $"Grammar Errors ({GrammarErrors.Count})";

        public bool GrammarErrorsExpanded => GrammarErrors.Count > 0;

        public bool TextBoxEnabled => !string.IsNullOrEmpty(_openedTextFile?.FullFileName);

        public ObservableCollection<object> GrammarErrors { get; } = new ObservableCollection<object>();

        public ObservableCollection<FileName> TextFiles { get; } = new ObservableCollection<FileName>();

        public ObservableCollection<PredictionMode> PredictionModes { get; } = new ObservableCollection<PredictionMode>
        {
            AntlrGrammarEditor.Processors.PredictionMode.LL,
            AntlrGrammarEditor.Processors.PredictionMode.SLL,
            AntlrGrammarEditor.Processors.PredictionMode.FullLL
        };

        public FileName OpenedTextFile
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
                            fileName = GetFullGrammarFileName(value.FullFileName);
                        }
                        _textTextBox.Text = File.ReadAllText(fileName);
                    }
                    catch (Exception ex)
                    {
                        _textTextBox.Text = "";
                        ShowOpenFileErrorMessage(_openedTextFile.FullFileName, ex.Message);
                    }
                    _workflow.TextFileName = value.FullFileName;
                    _textFileState = FileState.Opened;

                    _settings.OpenedTextFile = value.FullFileName;
                    _settings.Save();

                    _text = new CodeSource(_openedTextFile.ShortFileName, _textTextBox.Text);
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

        public bool IsParserExists => _workflow.Grammar.Type != GrammarType.Lexer;

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

        private LineColumnTextSpan GetSelectionLineColumn(TextEditor textBox, CodeSource source)
        {
            try
            {
                int start = textBox.SelectionStart;
                int end = textBox.SelectionStart + textBox.SelectionLength;
                if (start > end)
                {
                    int t = start;
                    start = end;
                    end = t;
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

        public ReactiveCommand<Unit, Unit> NewGrammarCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> OpenGrammarCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> ProcessCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> NewTextFile { get; private set; }

        public ReactiveCommand<Unit, Unit> OpenTextFile { get; private set; }

        public ReactiveCommand<Unit, Unit> RemoveTextFile { get; private set; }

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

        private void SetupWorkflowSubscriptions()
        {
            Observable.FromEventPattern<IWorkflowState>(
                ev => _workflow.StateChanged += ev, ev => _workflow.StateChanged -= ev)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ev => this.RaisePropertyChanged(nameof(CurrentState)));

            Observable.FromEventPattern<Runtime>(
                    ev => _workflow.DetectedRuntimeEvent += ev, ev => _workflow.DetectedRuntimeEvent -= ev)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ev => this.RaisePropertyChanged(nameof(DetectedRuntime)));

            Observable.FromEventPattern<ParsingError>(
                ev => _workflow.ErrorEvent += ev, ev => _workflow.ErrorEvent -= ev)
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

            grammarTextBoxObservable.Subscribe(x => _grammarFileState = FileState.Changed);

            textBoxObservable.Subscribe(x => _textFileState = FileState.Changed);

            grammarTextBoxObservable
                .Throttle(TimeSpan.FromMilliseconds(200))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => _grammarCode = new CodeSource(_openedGrammarFile, _grammarTextBox.Text));

            textBoxObservable
                .Throttle(TimeSpan.FromMilliseconds(200))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => _text = new CodeSource(_openedTextFile.ShortFileName, _text.Text));

            Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    GrammarCursorPosition = GetSelectionLineColumn(_grammarTextBox, _grammarCode);
                    TextCursorPosition = GetSelectionLineColumn(_textTextBox, _text);
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
                        _workflow.TextFileName = OpenedTextFile.FullFileName;
                        _workflow.RollbackToStage(WorkflowStage.ParserCompiled);

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
            NewGrammarCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var newGrammarWindow = new NewGrammarWindow();
                Grammar grammar = await newGrammarWindow.ShowDialog<Grammar>(_window);
                if (grammar != null)
                {
                    OpenGrammar(grammar, null, null);
                }
                _window.Activate();
            });

            OpenGrammarCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var openGrammarDialog = new OpenFolderDialog
                {
                    Title = "Enter grammar directory name"
                };

                string folderName = null;
                try
                {
                    folderName = await openGrammarDialog.ShowAsync(_window);

                    if (!string.IsNullOrEmpty(folderName))
                    {
                        var grammar = GrammarFactory.Open(folderName, out string packageName, out string root);
                        OpenGrammar(grammar, packageName, root);
                    }
                }
                catch (Exception ex)
                {
                    await ShowOpenFileErrorMessage(folderName, ex.Message);
                }
            });

            ProcessCommand = ReactiveCommand.Create(() =>
            {
                bool changed = false;

                if (_grammarFileState == FileState.Changed)
                {
                    RollbackGrammarIfRequired();

                    File.WriteAllText(GetFullGrammarFileName(_openedGrammarFile), _grammarTextBox.Text);
                    _grammarFileState = FileState.Saved;
                    changed = true;
                }

                if (_textFileState == FileState.Changed && !string.IsNullOrEmpty(_openedTextFile?.FullFileName))
                {
                    File.WriteAllText(_openedTextFile.FullFileName, _textTextBox.Text);
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
                var filters = new List<FileDialogFilter>();
                if (!string.IsNullOrEmpty(Grammar.FileExtension))
                {
                    filters.Add(new FileDialogFilter
                    {
                        Name = $"{Grammar.Name} parsing file",
                        Extensions = new List<string>() { Grammar.FileExtension }
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
                    DefaultExtension = Grammar.FileExtension,
                    Filters = filters,
                    Directory = Grammar.Directory,
                    InitialFileName = Path.GetFileName(GrammarFactory.GenerateTextFileName(Grammar))
                };

                string fileName = await saveFileDialog.ShowAsync(_window);
                if (fileName != null)
                {
                    File.WriteAllText(fileName, "");
                    var newFile = new FileName(fileName);
                    if (!TextFiles.Contains(newFile))
                    {
                        TextFiles.Add(newFile);
                        Grammar.TextFiles.Add(newFile.FullFileName);
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
                            Grammar.TextFiles.Add(openedFile.FullFileName);
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
                string shortFileName = OpenedTextFile.ShortFileName;
                string fullFileName = OpenedTextFile.FullFileName;
                Grammar.TextFiles.Remove(OpenedTextFile.FullFileName);
                var index = TextFiles.IndexOf(OpenedTextFile);
                TextFiles.Remove(OpenedTextFile);
                index = Math.Min(index, TextFiles.Count - 1);
                if (await MessageBox.ShowDialog(_window, $"Do you want to delete file {shortFileName}?", "", MessageBoxType.YesNo))
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

        private void RollbackGrammarIfRequired()
        {
            WorkflowStage switchStage =
                Path.GetExtension(OpenedGrammarFile).Equals(Grammar.AntlrDotExt, StringComparison.OrdinalIgnoreCase)
                   ? WorkflowStage.Input
                   : WorkflowStage.ParserGenerated;
            _workflow.RollbackToStage(switchStage);
        }

        private void OpenGrammar(Grammar grammar, string packageName, string root)
        {
            _workflow.Grammar = grammar;
            _settings.GrammarFileOrDirectory = grammar.Directory;
            _settings.Save();
            _openedGrammarFile = "";
            _openedTextFile = FileName.Empty;
            Rules.Clear();
            InitFiles();
            OpenedGrammarFile = GrammarFiles.FirstOrDefault();
            OpenedTextFile = TextFiles.FirstOrDefault();
            PackageName = packageName;
            Root = root;
            this.RaisePropertyChanged(nameof(SelectedRuntime));
            this.RaisePropertyChanged(nameof(IsParserExists));
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

        private void ErrorsListBox_DoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ListBox listBox = (ListBox)sender;
            listBox.Focus();

            if (listBox.SelectedItem is ParsingError parsingError)
            {
                TextEditor textBox = ReferenceEquals(listBox, _grammarErrorsListBox) ? _grammarTextBox : _textTextBox;
                if (ReferenceEquals(textBox, _grammarTextBox))
                {
                    OpenedGrammarFile = parsingError.TextSpan.Source.Name;
                }

                TextSpan selectTextSpan;
                if (parsingError.TextSpan.Length != 0)
                {
                    selectTextSpan = parsingError.TextSpan;
                }
                else
                {
                    int beginIndex = parsingError.TextSpan.Start;
                    if (parsingError.TextSpan.End >= textBox.Text.Length)
                    {
                        beginIndex = textBox.Text.Length - 1;
                        if (beginIndex < 0)
                            beginIndex = 0;
                    }

                    selectTextSpan = new TextSpan(beginIndex, 1, parsingError.TextSpan.Source);
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

        private void InitFiles()
        {
            InitGrammarAndCompiledFiles();

            TextFiles.Clear();
            foreach (var file in Grammar.TextFiles)
            {
                TextFiles.Add(new FileName(file));
            }
        }

        private void InitGrammarAndCompiledFiles()
        {
            var value = SelectedRuntime;

            var runtimeInfo = value.RuntimeInfo;
            if (runtimeInfo == null)
            {
                return;
            }

            string runtimeName = runtimeInfo.Runtime.ToString();
            string runtimeFilesPath = Path.Combine(Grammar.Directory, runtimeName);

            if (!Directory.Exists(runtimeFilesPath))
            {
                runtimeName = runtimeInfo.Runtime.GetGeneralRuntimeName();
                runtimeFilesPath = Path.Combine(Grammar.Directory, runtimeName);
            }

            var runtimeGrammarAndCompiledFiles = new List<string>();
            if (Directory.Exists(runtimeFilesPath))
            {
                runtimeGrammarAndCompiledFiles = GetGrammarAndCompiliedFiles(runtimeFilesPath, runtimeName, runtimeInfo.Extensions[0]);
            }

            List<string> grammarAndCompiliedFiles = GetGrammarAndCompiliedFiles(Grammar.Directory, "", runtimeInfo.Extensions[0]);

            Grammar.Files.Clear();

            foreach (string fileName in grammarAndCompiliedFiles)
            {
                string runtimeFile = runtimeGrammarAndCompiledFiles
                    .FirstOrDefault(f => Path.GetFileName(f) == fileName);

                string addedFile;
                if (runtimeFile != null)
                {
                    addedFile = runtimeFile;
                    runtimeGrammarAndCompiledFiles.Remove(runtimeFile);
                }
                else
                {
                    addedFile = fileName;
                }

                Grammar.Files.Add(addedFile);
            }

            foreach (string fileName in runtimeGrammarAndCompiledFiles)
            {
                Grammar.Files.Add(fileName);
            }

            this.RaisePropertyChanged(nameof(GrammarFiles));
        }

        private List<string> GetGrammarAndCompiliedFiles(string path, string runtimeName, string extension)
        {
            return Directory.GetFiles(path, "*.*")
                            .Where(file => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                                        || file.EndsWith(Grammar.AntlrDotExt, StringComparison.OrdinalIgnoreCase))
                            .Select(file => Path.Combine(runtimeName, Path.GetFileName(file)))
                            .ToList();
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

                await MessageBox.ShowDialog(_window, message);

                return;
            }

            RuntimeInfo runtimeInfo = SelectedRuntime.RuntimeInfo != null
                ? RuntimeInfo.InitOrGetRuntimeInfo(SelectedRuntime.RuntimeInfo.Runtime)
                : null;

            if (EndStage >= WorkflowStage.ParserCompiled && runtimeInfo != null && runtimeInfo.Version == null)
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

            UpdateRules();
        }

        private void UpdateRules()
        {
            var grammarCheckedState = _workflow.CurrentState.GetState<GrammarCheckedState>();
            if (grammarCheckedState == null)
            {
                return;
            }

            var workflowRules = grammarCheckedState.Rules;
            if (!Rules.SequenceEqual(workflowRules))
            {
                Rules.Clear();
                foreach (var rule in workflowRules)
                {
                    Rules.Add(rule);
                }

                if (!Rules.Contains(Root))
                {
                    Root = Rules[0];
                }
                else
                {
                    this.RaisePropertyChanged(nameof(Root));
                }
            }
        }

        private string GetFullGrammarFileName(string fileName) => Path.Combine(Grammar.Directory, fileName);

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
