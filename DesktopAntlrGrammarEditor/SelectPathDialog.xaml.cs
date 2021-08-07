using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DesktopAntlrGrammarEditor
{
    public class SelectPathDialog : Window
    {
        public SelectPathDialog()
        {
        }

        public SelectPathDialog(string message, string path)
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new SelectPathViewModel(this, message, path);
        }
    }
}
