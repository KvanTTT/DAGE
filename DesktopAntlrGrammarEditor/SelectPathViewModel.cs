using Avalonia.Controls;
using ReactiveUI;
using System;

namespace DesktopAntlrGrammarEditor
{
    public class SelectPathViewModel : ReactiveObject
    {
        private Window _window;
        private string _message;
        private string _path;

        public SelectPathViewModel(Window window, string message, string path = "")
        {
            _window = window;
            Message = message;
            Path = path;

            OkCommand.Subscribe(_ =>
            {
                _window.Close(Path);
            });

            CancelCommand.Subscribe(_ =>
            {
                _window.Close(null);
            });
        }

        public string Message
        {
            get
            {
                return _message;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _message, value);
            }
        }

        public string Path
        {
            get
            {
                return _path;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _path, value);
            }
        }

        public ReactiveCommand<object> OkCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> CancelCommand { get; } = ReactiveCommand.Create();
    }
}
