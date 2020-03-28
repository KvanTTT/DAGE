﻿using Avalonia;
using Avalonia.Logging.Serilog;
using Avalonia.ReactiveUI;

namespace DesktopAntlrGrammarEditor
{
    class Program
    {
        public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .UseReactiveUI()
                .LogToDebug();
    }
}
