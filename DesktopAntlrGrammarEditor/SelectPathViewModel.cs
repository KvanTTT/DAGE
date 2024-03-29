﻿using System.Reactive;
using Avalonia.Controls;
using ReactiveUI;

namespace DesktopAntlrGrammarEditor
{
    public class SelectPathViewModel : ReactiveObject
    {
        private string _message;
        private string _path;

        public SelectPathViewModel(Window window, string message, string path = "")
        {
            _message = message;
            _path = path;
            OkCommand = ReactiveCommand.Create(() => window.Close(Path));
            CancelCommand = ReactiveCommand.Create(() => window.Close(null));
        }

        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }

        public string Path
        {
            get => _path;
            set => this.RaiseAndSetIfChanged(ref _path, value);
        }

        public ReactiveCommand<Unit, Unit> OkCommand { get; }

        public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    }
}
