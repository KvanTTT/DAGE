using Avalonia.Controls;
using ReactiveUI;

namespace DesktopAntlrGrammarEditor
{
    public class MessageBoxViewModel: ReactiveObject
    {
        public MessageBoxViewModel(Window window, string messageBoxText, string title = "", MessageBoxType messageBoxType = MessageBoxType.Ok)
        {
            Title = title;
            MessageBoxText = messageBoxText;
            MessageBoxType = messageBoxType;

            if (MessageBoxType == MessageBoxType.Ok)
            {
                OkCommand = ReactiveCommand.Create(() => window.Close(true));
            }
            else if (MessageBoxType == MessageBoxType.YesNo)
            {
                YesCommand = ReactiveCommand.Create(() => window.Close(true));
                NoCommand = ReactiveCommand.Create(() => window.Close(false));
            }
        }

        public string Title { get; }

        public string MessageBoxText { get; }

        public MessageBoxType MessageBoxType { get; }

        public bool OkButtonVisible => MessageBoxType == MessageBoxType.Ok;

        public bool YesNoButtonVisible => MessageBoxType == MessageBoxType.YesNo;

        public ReactiveCommand OkCommand { get; }

        public ReactiveCommand YesCommand { get; }

        public ReactiveCommand NoCommand { get; }
    }
}
