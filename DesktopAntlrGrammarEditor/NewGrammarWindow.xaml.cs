using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DesktopAntlrGrammarEditor
{
    public class NewGrammarWindow : Window
    {
        public NewGrammarWindow()
        {
            AvaloniaXamlLoader.Load(this);
            DataContext = new NewGrammarWindowViewModel(this);
            Activate();
        }
    }
}
