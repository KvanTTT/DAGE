using Avalonia.Controls;
using ReactiveUI;
using System;

namespace DesktopAntlrGrammarEditor
{
    public class MessageBoxViewModel : ReactiveObject
    {
        private Window _window;

        public MessageBoxViewModel(Window window, string messageBoxText, string title = "")
        {
            _window = window;
            Title = title;
            MessageBoxText = messageBoxText;

            OkCommand.Subscribe(_ =>
            {
                _window.Close();
            });
        }

        public string Title { get; set; }

        public string MessageBoxText { get; set; }

        public ReactiveCommand<object> OkCommand { get; } = ReactiveCommand.Create();
    }
}
