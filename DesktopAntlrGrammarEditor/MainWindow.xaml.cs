using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DesktopAntlrGrammarEditor
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            DataContext = new MainWindowViewModel(this);
            App.AttachDevTools(this);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
