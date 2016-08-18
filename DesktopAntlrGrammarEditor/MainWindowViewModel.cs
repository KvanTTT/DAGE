using Antlr4.Runtime;
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
using System.Text;
using System.Threading.Tasks;

namespace DesktopAntlrGrammarEditor
{
    public class MainWindowViewModel : ReactiveObject
    {
        private Window _window;
        private string _root;
        private Runtime _selectedRuntime;
        private TextBox _grammarTextBox, _sourceCodeTextBox;
        private string _grammarErrorsText = "Grammar Errors (0)";
        private string _sourceCodeErrorsText = "Source Code Errors (0)";
        private ListBox _grammarErrorsListBox, _sourceCodeErrorsListBox;

        public MainWindowViewModel(Window window)
        {
            Settings = Settings.Load();

            Workflow = new Workflow();
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
                .Subscribe(str => CheckGrammarCommand.Execute(null));

            _sourceCodeTextBox.GetObservable(TextBox.TextProperty)
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .Subscribe(str => ParseTextCommand.Execute(null));

            _root = Settings.Root;
            _selectedRuntime = Settings.SelectedRuntime;

            CheckGrammarCommand.Subscribe(_ =>
            {
                Workflow.Grammar = _grammarTextBox.Text;
                ProcessAndAddErrors();
                if (Workflow.GrammarCheckedState != null)
                {
                    if (!Rules.SequenceEqual(Workflow.GrammarCheckedState.Rules))
                    {
                        Rules.Clear();
                        foreach (var rule in Workflow.GrammarCheckedState.Rules)
                        {
                            Rules.Add(rule);
                        }
                        if (!Rules.Contains(_root))
                        {
                            _root = Rules.First();
                            this.RaisePropertyChanged(nameof(Root));
                        }
                    }
                }
                Settings.GrammarText = _grammarTextBox.Text;
                Settings.Save();
            });

            GenerateParserCommand.Subscribe(_ =>
            {

            });

            CompileParserCommand.Subscribe(_ =>
            {

            });

            ParseTextCommand.Subscribe(_ =>
            {
                Workflow.Text = _sourceCodeTextBox.Text;
                ProcessAndAddErrors();
                Settings.Text = _sourceCodeTextBox.Text;
                Settings.Save();
            });
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

        private void ProcessAndAddErrors()
        {
            Workflow.Process();
            AddErrors();
        }

        private void AddErrors()
        {
            ObservableCollection<object> errors;
            switch (Workflow.CurrentState.Stage)
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
            errors.Clear();
            if (Workflow.CurrentState.Exception != null)
            {
                errors.Add(Workflow.CurrentState.Exception);
            }

            IReadOnlyList<ParsingError> parsingErrors;
            switch (Workflow.CurrentState.Stage)
            {
                case WorkflowStage.GrammarChecked:
                    parsingErrors = (Workflow.CurrentState as GrammarCheckedState).Errors;
                    break;
                case WorkflowStage.ParserGenerated:
                    parsingErrors = (Workflow.CurrentState as ParserGeneratedState).Errors;
                    break;
                case WorkflowStage.ParserCompilied:
                    parsingErrors = (Workflow.CurrentState as ParserCompiliedState).Errors;
                    break;
                case WorkflowStage.TextParsed:
                    parsingErrors = (Workflow.CurrentState as TextParsedState).TextErrors;
                    break;
                default:
                    parsingErrors = new List<ParsingError>();
                    break;
            }
            foreach (var error in parsingErrors)
            {
                errors.Add(error);
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
                this.RaiseAndSetIfChanged(ref _selectedRuntime, value);
                Workflow.Runtime = _selectedRuntime;
                ProcessAndAddErrors();
                Settings.SelectedRuntime = _selectedRuntime;
                Settings.Save();
            }
        }

        public string Root
        {
            get
            {
                return _root;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _root, value);
                Workflow.Root = _root;
                ProcessAndAddErrors();
                Settings.Root = _root;
                Settings.Save();
            }
        }

        public ObservableCollection<string> Rules { get; } = new ObservableCollection<string>();

        public ObservableCollection<Runtime> Runtimes { get; } = 
            new ObservableCollection<Runtime>((Runtime[])Enum.GetValues(typeof(Runtime)));

        public ReactiveCommand<object> CheckGrammarCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> GenerateParserCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> CompileParserCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> ParseTextCommand { get; } = ReactiveCommand.Create();

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

        protected Workflow Workflow { get; set; }

        protected Settings Settings { get; set; }
    }
}
