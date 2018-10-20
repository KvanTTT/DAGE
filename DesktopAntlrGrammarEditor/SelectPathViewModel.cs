using Avalonia.Controls;
using ReactiveUI;

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

            OkCommand = ReactiveCommand.Create(() =>
            {
                _window.Close(Path);
            });

            CancelCommand = ReactiveCommand.Create(() =>
            {
                _window.Close(null);
            });
        }

        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }

        public string Path
        {
            get => _path;
            set => this.RaiseAndSetIfChanged(ref _path, value);
        }

        public ReactiveCommand OkCommand { get; }

        public ReactiveCommand CancelCommand { get; }
    }
}
