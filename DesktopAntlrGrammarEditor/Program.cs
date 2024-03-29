﻿using Avalonia;
using Avalonia.ReactiveUI;

namespace DesktopAntlrGrammarEditor
{
    class Program
    {
        public static void Main(string[] args) => BuildAvaloniaApp().Start<MainWindow>();

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .UseReactiveUI()
                .LogToTrace();
    }
}
