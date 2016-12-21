using Avalonia.Controls;
using ReactiveUI;
using System;

namespace DesktopAntlrGrammarEditor
{
    public class MessageBoxViewModel: ReactiveObject
    {
        private Window _window;

        public MessageBoxViewModel(Window window, string messageBoxText, string title = "", MessageBoxType messageBoxType = MessageBoxType.Ok)
        {
            _window = window;
            Title = title;
            MessageBoxText = messageBoxText;
            MessageBoxType = messageBoxType;

            if (MessageBoxType == MessageBoxType.Ok)
            {
                OkCommand.Subscribe(_ =>
                {
                    _window.Close(true);
                });
            }
            else if (MessageBoxType == MessageBoxType.YesNo)
            {
                YesCommand.Subscribe(_ =>
                {
                    _window.Close(true);
                });

                NoCommand.Subscribe(_ =>
                {
                    _window.Close(false);
                });
            }
        }

        public string Title { get; set; }

        public string MessageBoxText { get; set; }

        public MessageBoxType MessageBoxType { get; set; }

        public bool OkButtonVisible => MessageBoxType == MessageBoxType.Ok;

        public bool YesNoButtonVisible => MessageBoxType == MessageBoxType.YesNo;

        public ReactiveCommand<object> OkCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> YesCommand { get; } = ReactiveCommand.Create();

        public ReactiveCommand<object> NoCommand { get; } = ReactiveCommand.Create();
    }
}
