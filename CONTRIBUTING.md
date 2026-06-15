# Contributing to FixedWidthParser

Thanks for taking the time to contribute! This document covers how to build, test,
and submit changes.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`)
- Git

## Building

```bash
dotnet restore FixedWidthParser.slnx
dotnet build FixedWidthParser.slnx --configuration Release
```

## Running tests

```bash
dotnet test FixedWidthParser.slnx --configuration Release
```

To collect coverage locally (same as CI):

```bash
dotnet test FixedWidthParser.slnx --configuration Release --collect:"XPlat Code Coverage"
```

The Cobertura report is written under each test project's `TestResults/` folder.

## Running benchmarks

```bash
dotnet run --project tests/Benchmarks/Benchmarks.csproj --configuration Release -- --filter '*'
```

Filter to a subset with fully-qualified patterns, e.g.
`--filter 'Benchmarks.Perf.ParserBenchmarks.*'`. Results for `master` are published to
[GitHub Pages](https://gabrielmarquezmatte.github.io/FixedWidthParser/dev/bench/); every PR
gets a benchmark comparison posted as a comment automatically.

## Coding standards

- The repo enforces a large analyzer suite (SonarAnalyzer, Roslynator, Meziantou,
  NetAnalyzers, and more) configured in `Directory.Build.props`, plus formatting rules in
  `.editorconfig`. Please make sure the build is warning-clean before opening a PR.
- Match the surrounding style: file-scoped namespaces, `_camelCase` private fields,
  span-based hot paths, and XML docs on public members.
- This is a **performance-sensitive** library. Avoid introducing allocations on parse/write
  hot paths; if a change could affect performance, include benchmark numbers in the PR.

## Pull request flow

1. Branch off `develop` (or `master` for hotfixes).
2. Make your change with tests covering it. New behavior without tests will be asked to add them.
3. Ensure `dotnet build` and `dotnet test` pass locally on Release.
4. Open the PR against `master`/`develop`; fill in the PR template.
5. CI (build/test on Linux, Windows, macOS), CodeQL, and the benchmark comparison must be green.

## Reporting bugs / requesting features

Use the [issue templates](https://github.com/GabrielMarquezMatte/FixedWidthParser/issues/new/choose).
For security issues, **do not** open a public issue — see [SECURITY.md](SECURITY.md).
