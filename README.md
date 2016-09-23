# Desktop Antlr Grammar Editor (DAGE)

## Features

* Error checking on all stages of grammar development.
* Official and unofficial runtimes support.
* Case insensitive input stream support (for Pascal, SQL, Visual Basic languages and etc.).
* Mapping between generated code errors and grammar code actions.
* Crossplaform working (based on [Avalonia](https://github.com/AvaloniaUI/Avalonia) multi-platform .NET UI framework).

## Grammar processing stages

1. Grammar syntax checks, parser name and rules collecting.
2. Grammar semantics checks and parser generation for certain runtime.
3. Generated code semantics checks and parser compilation (Interpretation).
4. Text parsing and text syntax errors collecting.

## Error types

1. ANTLR grammar syntax errors.
2. ANTLR tool grammar semantics errors.
3. Generated code semantics errors.
4. Parsing text syntax errors.

## Supported runtimes

* C# Sharwell ([antlr4cs](https://github.com/tunnelvisionlabs/antlr4cs)).
* C#.
* Java.
* Python2.
* Python3.
* JavaScript (teting with nodejs).
* Go ([ANTLR4 Language Target, Runtime for Go](https://github.com/pboyer/antlr4)).

## Tests
* AppVeyor (Windows):
 [![Build status](https://ci.appveyor.com/api/projects/status/afkuyda7k1hr6uw4?svg=true)](https://ci.appveyor.com/project/KvanTTT/dage)
* Travis CI (Linux): [![Build status](https://travis-ci.org/KvanTTT/DAGE.svg)](https://travis-ci.org/KvanTTT/DAGE)