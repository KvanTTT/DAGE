using Avalonia;
using Avalonia.Logging.Serilog;

namespace DesktopAntlrGrammarEditor
{
    class Program
    {
        static void Main(string[] args)
        {
            BuildAvaloniaApp().Start<MainWindow>();
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect().UseReactiveUI().LogToDebug();
        }
    }
}
