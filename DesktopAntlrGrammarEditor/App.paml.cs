using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Themes.Default;
using Avalonia.Markup.Xaml;

namespace DesktopAntlrGrammarEditor
{
    class App : Application
    {

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            base.Initialize();
        }

        static void Main(string[] args)
        {
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .Start<MainWindow>();
        }

        public static void AttachDevTools(Window window)
        {
#if DEBUG
            DevTools.Attach(window);
#endif
        }
    }
}
