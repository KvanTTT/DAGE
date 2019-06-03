using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;

namespace DesktopAntlrGrammarEditor
{
    public class MessageBox : Window
    {
        public MessageBox()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public static async Task<bool> ShowDialog(Window window, string message, string title = "", MessageBoxType messageBoxType = MessageBoxType.Ok)
        {
            var messageBox = new MessageBox(message, title, messageBoxType);
            return await messageBox.ShowDialog<bool>(window);
        }

        public MessageBox(string message, string title = "", MessageBoxType messageBoxType = MessageBoxType.Ok)
        {
            this.InitializeComponent();
            DataContext = new MessageBoxViewModel(this, message, title, messageBoxType);
            Activate();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
