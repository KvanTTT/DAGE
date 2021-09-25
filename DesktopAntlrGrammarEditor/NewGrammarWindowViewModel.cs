using AntlrGrammarEditor;
using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;

namespace DesktopAntlrGrammarEditor
{
    public class NewGrammarWindowViewModel : ReactiveObject
    {
        public string GrammarName { get; set; }

        public string GrammarDirectory { get; set; } = Utils.DefaultGrammarsDirectory;

        public string FileExtension { get; set; } = "txt";

        public GrammarProjectType GrammarProjectType { get; set; } = GrammarProjectType.Combined;

        public CaseInsensitiveType CaseInsensitiveType { get; set; } = CaseInsensitiveType.None;

        public ObservableCollection<CaseInsensitiveType> CaseInsensitiveTypes { get; } =
            new ObservableCollection<CaseInsensitiveType>(new List<CaseInsensitiveType>
        {
            CaseInsensitiveType.None,
            CaseInsensitiveType.Lower,
            CaseInsensitiveType.Upper
        });

        public ObservableCollection<GrammarProjectType> GrammarProjectTypes { get; } =
            new ObservableCollection<GrammarProjectType>(new List<GrammarProjectType>
        {
            GrammarProjectType.Combined,
            GrammarProjectType.Separated,
            GrammarProjectType.Lexer
        });

        public ReactiveCommand<Unit, Unit> OkCommand { get; }

        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectGrammarDirectory { get; }

        public NewGrammarWindowViewModel(Window window)
        {
            var grammarWithNotConflictingName = GrammarFilesManager.GetGrammarWithNotConflictingName(GrammarDirectory,
                grammarProjectType: GrammarProjectType);
            GrammarName = grammarWithNotConflictingName.Name;

            OkCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var grammar = new Grammar(GrammarName, GrammarDirectory, null, null, CaseInsensitiveType);
                var grammarFilesManager = new GrammarFilesManager(grammar, GrammarProjectType);

                bool success = false;
                var existingFile = grammarFilesManager.CheckExistence();
                if (existingFile != null)
                {
                    if (await MessageBox.ShowDialog(window, $"Do you want to replace existing file {existingFile}?", "", MessageBoxType.YesNo))
                    {
                        success = true;
                    }
                }
                else
                {
                    success = true;
                }

                if (success)
                {
                    try
                    {
                        grammarFilesManager.CreateFiles();
                    }
                    catch (Exception ex)
                    {
                        await MessageBox.ShowDialog(window, $"Unable to create grammar: {ex.Message}");
                        grammar = null;
                    }

                    window.Close(grammar);
                }
            });

            CancelCommand = ReactiveCommand.Create(() => window.Close(null));

            SelectGrammarDirectory = ReactiveCommand.CreateFromTask(async () =>
            {
                var openFolderDialog = new OpenFolderDialog
                {
                    Directory = GrammarDirectory
                };
                var folderName = await openFolderDialog.ShowAsync(window);
                if (!string.IsNullOrEmpty(folderName))
                {
                    GrammarDirectory = folderName;
                }
            });
        }
    }
}
