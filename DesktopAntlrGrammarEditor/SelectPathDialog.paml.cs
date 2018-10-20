using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DesktopAntlrGrammarEditor
{
    public class SelectPathDialog : Window
    {
        public SelectPathDialog(string message, string path)
        {
            InitializeComponent();
            DataContext = new SelectPathViewModel(this, message, path);
            App.AttachDevTools(this);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
