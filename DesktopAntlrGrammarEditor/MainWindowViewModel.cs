using AntlrGrammarEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

namespace DesktopAntlrGrammarEditor
{
    public class MainWindowViewModel : ReactiveObject
    {
        private Window _window;
        private string _root = "";
        private string _currentState = WorkflowStage.Input.ToString();
        private Runtime _selectedRuntime;
        private TextBox _grammarTextBox, _sourceCodeTextBox;
        private string _grammarErrorsText = "Grammar Errors (0)";
        private string _sourceCodeErrorsText = "Source Code Errors (0)";
        private ListBox _grammarErrorsListBox, _sourceCodeErrorsListBox;
        private string _tree;
        private bool _autoprocessing;

        public MainWindowViewModel(Window window)
        {
            Settings = Settings.Load();

            Workflow = new Workflow();
            Workflow.StateChanged += Workflow_StateChanged;
            _window = window;

            _grammarTextBox = _window.Find<TextBox>("GrammarTextBox");
            _grammarTextBox.Text = Settings.GrammarText;
            Workflow.Grammar = Settings.GrammarText;
            _sourceCodeTextBox = _window.Find<TextBox>("SourceCodeTextBox");
            _sourceCodeTextBox.Text = Settings.Text;
            Workflow.Text = Settings.Text;
            _grammarErrorsListBox = _window.Find<ListBox>("GrammarErrorsListBox");
            _sourceCodeErrorsListBox = _window.Find<ListBox>("SourceCodeErrorsListBox");

            _grammarErrorsListBox.DoubleTapped += ErrorsListBox_DoubleTapped;
            _sourceCodeErrorsListBox.DoubleTapped += ErrorsListBox_DoubleTapped;

            _window.WindowState = Settings.WindowState;
            if (Settings.Width > 0)
            {
                _window.Width = Settings.Width;
            }
            if (Settings.Height > 0)
            {
                _window.Height = Settings.Height;
            }
            if (Settings.Left != -1 && Settings.Top != -1)
            {
                _window.Position = new Point(Settings.Left, Settings.Top);
            }
            _window.GetObservable(Window.WidthProperty)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Subscribe(width => { Settings.Width = width; Settings.WindowState = _window.WindowState; Settings.Save(); });
            _window.GetObservable(Window.HeightProperty)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Subscribe(height => { Settings.Height = height; Settings.WindowState = _window.WindowState; Settings.Save(); });
            _window.Closed += _window_Closed;

            _grammarTextBox.GetObservable(TextBox.TextProperty)
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .Subscribe(str =>
                {
                    Settings.GrammarText = str;
                    Settings.Save();
                    Workflow.Grammar = str;
                    Dispatcher.UIThread.InvokeAsync(() => UpdateUI());
                    if (AutoProcessing)
                    {
                        Workflow.Process();
                    }
                });

            _sourceCodeTextBox.GetObservable(TextBox.TextProperty)
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .Subscribe(str =>
                {
                    Settings.Text = str;
                    Settings.Save();
                    Workflow.Text = str;
                    Dispatcher.UIThread.InvokeAsync(() => UpdateUI());
                    if (AutoProcessing)
                    {
                        ProcessAndUpdateUI();
                    }
                } );

            _root = Settings.Root;
            _selectedRuntime = Settings.SelectedRuntime;

            ProcessCommand.Subscribe(_ =>
            {
                Workflow.Grammar = _grammarTextBox.Text;
                Workflow.Text = _sourceCodeTextBox.Text;
                Settings.GrammarText = Workflow.Grammar;
                Settings.Text = Workflow.Text;
                Settings.Save();
                ProcessAndUpdateUI();
            });
        }

        public string Root
        {
            get
            {
                return _root;
            }
            set
            {
                Settings.Root = _root;
                Settings.Save();
                this.RaiseAndSetIfChanged(ref _root, value);
                if (Workflow.Root != _root)
                {
                    Workflow.Root = _root;
                    if (AutoProcessing)
                    {
                        ProcessAndUpdateUI();
                    }
                }
            }
        }

        public Runtime SelectedRuntime
        {
            get
            {
                return _selectedRuntime;
            }
            set
            {
                Settings.SelectedRuntime = _selectedRuntime;
                Settings.Save();
                this.RaiseAndSetIfChanged(ref _selectedRuntime, value);
                Workflow.Runtime = _selectedRuntime;
                if (AutoProcessing)
                {
                    ProcessAndUpdateUI();
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

        public ObservableCollection<string> Rules { get; } = new ObservableCollection<string>();

        public ObservableCollection<Runtime> Runtimes { get; } =
            new ObservableCollection<Runtime>((Runtime[])Enum.GetValues(typeof(Runtime)));

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

        public string SourceCodeErrorsText
        {
            get
            {
                return _sourceCodeErrorsText;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _sourceCodeErrorsText, value);
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
                Settings.Autoprocessing = _autoprocessing;
                Settings.Save();
            }
        }

        protected Workflow Workflow { get; set; }

        protected Settings Settings { get; set; }

        private void Workflow_StateChanged(object sender, WorkflowState e)
        {
            Dispatcher.UIThread.InvokeAsync(() => CurrentState = e.Stage.ToString());
        }

        private void _window_Closed(object sender, EventArgs e)
        {
            Settings.Left = _window.Position.X;
            Settings.Top = _window.Position.Y;
            Settings.Save();
        }

        private void ErrorsListBox_DoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ListBox listBox = (ListBox)sender;
            listBox.Focus();

            var parsingError = listBox.SelectedItem as ParsingError;
            if (parsingError != null)
            {
                TextBox textBox = listBox == _grammarErrorsListBox ? _grammarTextBox : _sourceCodeTextBox;
                textBox.SelectionStart = Utils.LineColumnToLinear(textBox.Text, parsingError.Line, parsingError.Column);
                textBox.SelectionEnd = textBox.SelectionStart + 1;
            }
        }

        private void ProcessAndUpdateUI()
        {
            Workflow.RollbackToPreviousStageIfErrors();
            Workflow.Process();

            if (Workflow.GrammarCheckedState != null)
            {
                if (!Rules.SequenceEqual(Workflow.GrammarCheckedState.Rules))
                {
                    Rules.Clear();
                    foreach (var rule in Workflow.GrammarCheckedState.Rules)
                    {
                        Rules.Add(rule);
                    }
                    _root = "";
                    Root = Workflow.Root;
                }
                else if (!Rules.Contains(_root))
                {
                    _root = "";
                    Root = Workflow.Root;
                }
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            Tree = "";
            GrammarErrors.Clear();
            TextErrors.Clear();
            GrammarErrorsText = $"Grammar Errors (0)";
            SourceCodeErrorsText = "Source Code Errors (0)";

            var currentState = Workflow.CurrentState;
            while (currentState != null)
            {
                ObservableCollection<object> errors;
                switch (currentState.Stage)
                {
                    case WorkflowStage.Input:
                    case WorkflowStage.GrammarChecked:
                    case WorkflowStage.ParserGenerated:
                    case WorkflowStage.ParserCompilied:
                        errors = GrammarErrors;
                        break;
                    case WorkflowStage.TextParsed:
                        errors = TextErrors;
                        break;
                    default:
                        errors = GrammarErrors;
                        break;
                }
                if (currentState.Exception != null)
                {
                    errors.Add(currentState.Exception);
                }

                IReadOnlyList<ParsingError> parsingErrors = null;
                switch (currentState.Stage)
                {
                    case WorkflowStage.GrammarChecked:
                        parsingErrors = (currentState as GrammarCheckedState).Errors;
                        break;
                    case WorkflowStage.ParserGenerated:
                        parsingErrors = (currentState as ParserGeneratedState).Errors;
                        break;
                    case WorkflowStage.ParserCompilied:
                        parsingErrors = (currentState as ParserCompiliedState).Errors;
                        break;
                    case WorkflowStage.TextParsed:
                        var textParsedState = currentState as TextParsedState;
                        parsingErrors = textParsedState.TextErrors;
                        Tree = textParsedState.StringTree;
                        break;
                }
                if (parsingErrors != null)
                {
                    foreach (var error in parsingErrors)
                    {
                        errors.Add(error);
                    }
                }

                switch (currentState.Stage)
                {
                    case WorkflowStage.Input:
                    case WorkflowStage.GrammarChecked:
                    case WorkflowStage.ParserGenerated:
                    case WorkflowStage.ParserCompilied:
                        GrammarErrorsText = $"Grammar Errors ({errors.Count})";
                        break;
                    case WorkflowStage.TextParsed:
                        SourceCodeErrorsText = $"Source Code Errors ({errors.Count})";
                        break;
                    default:
                        break;
                }

                currentState = currentState.PreviousState;
            }
        }
    }
}
