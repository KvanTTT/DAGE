using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DesktopAntlrGrammarEditor
{
    public class MessageBox : Window
    {
        public MessageBox(string message, string title = "")
        {
            this.InitializeComponent();
            DataContext = new MessageBoxViewModel(this, message, title);
            App.AttachDevTools(this);
            Activate();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
