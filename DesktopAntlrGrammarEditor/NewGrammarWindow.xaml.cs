using Avalonia;
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
            Activate();
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
