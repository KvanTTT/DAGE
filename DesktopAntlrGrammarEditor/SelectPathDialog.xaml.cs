using Avalonia;
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
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
