using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DesktopAntlrGrammarEditor
{
    public class NewGrammarWindow : Window
    {
        public NewGrammarWindow()
        {
            this.InitializeComponent();
            DataContext = new NewGrammarWindowViewModel(this);
            App.AttachDevTools(this);
            Activate();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
