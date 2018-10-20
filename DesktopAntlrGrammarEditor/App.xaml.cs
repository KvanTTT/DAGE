using Avalonia;
using Avalonia.Markup.Xaml;

namespace DesktopAntlrGrammarEditor
{
    class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
