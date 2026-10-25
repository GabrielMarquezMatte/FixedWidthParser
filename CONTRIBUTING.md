# Contributing

Thanks for considering a contribution. This project takes small, focused pull requests over large
rewrites — see [ARCHITECTURE.md](ARCHITECTURE.md) for the shape of the codebase before diving in, and
[STYLEGUIDE.md](STYLEGUIDE.md) for the code style, which the analyzers only partly enforce.

## Build expectations

- **Warnings are errors in `src/`.** The library, generator and native projects set `TreatWarningsAsErrors`, under a
  curated `AnalysisMode=All` analyzer set (NetAnalyzers, Meziantou, SonarAnalyzer, Roslynator and
  others, in `Directory.Build.props`). A PR that doesn't build clean locally won't build clean in CI
  either — run a full build before pushing:

  ```bash
  dotnet build FixedWidthParser.slnx --configuration Release
  ```

- **Public API changes require a `PublicAPI.Unshipped.txt` entry.** `Microsoft.CodeAnalysis.PublicApiAnalyzers`
  is active and fails the build on any unrecorded public member. If you add, change, or remove anything
  public, update `src/FixedWidthParser/PublicAPI.Unshipped.txt`; the IDE's code fix writes the line for
  you. At a release, the unshipped entries move to `PublicAPI.Shipped.txt`.

- **Minor versions don't break callers.** The policy is in
  [docs/guide/versioning.md](docs/guide/versioning.md); in practice:
  - `IFixedWidthModel<TSelf>` and `IFixedWidthConverter<T>` are implemented by users, so a minor
    version never adds a member to them. New information reaches them as a new property on
    `FixedWidthContext`.
  - New inputs go on the options objects or a new overload, never as a new optional parameter on an
    existing method.
  - `dotnet pack` runs package validation against `PackageValidationBaselineVersion` in
    `src/FixedWidthParser/FixedWidthParser.csproj`, and CI runs it on every pull request. It is off
    while a major version is in development, since that version breaks the last one on purpose; list
    those breaks in the changelog. After each release, turn validation back on with the baseline set
    to the version just published.

- **Tests are required for behavior changes.** Run the suite before opening a PR:

  ```bash
  dotnet test --solution FixedWidthParser.slnx --configuration Release
  ```

  CI also collects coverage (`-- --coverage --coverage-output-format cobertura`) and uploads it to
  Codecov from the Linux leg.

  A change to one path needs the same change, and a parity test, on its twin: the runtime and
  generated paths, the sync and async readers and writers, the sequential and parallel reads, and the
  aggregator's model and sink loops ([STYLEGUIDE.md § Twins](STYLEGUIDE.md#twins)).

- **The native library, its bindings and NativeAOT have their own checks.** A change under
  `src/FixedWidthParser.Native`, `python/` or `cpp/` needs the Python, C and C++ tests; a change to the
  fluent layout or anything it reaches needs the AOT smoke app to publish without an `IL2xxx`/`IL3xxx`
  warning and to run:

  ```bash
  python python/scripts/build_native.py          # publishes the library into python/src/fixedwidth/_lib
  pip install -e "python[dev,pandas,polars]" && pytest python/tests
  cmake -S cpp -B build/cpp && cmake --build build/cpp && ctest --test-dir build/cpp --output-on-failure
  dotnet publish tests/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcTreatWarningsAsErrors=true
  ```

  On Windows, run the CMake steps from a Visual Studio developer prompt, which puts `lib.exe` on the path. Line-splitting code reads untrusted input; read
  [STYLEGUIDE.md § Untrusted Input](STYLEGUIDE.md#untrusted-input) before touching it.

- **Performance claims come with numbers.** This is a performance-sensitive library: avoid
  allocations on the parse and write hot paths, and if a change could affect speed, run the benchmarks
  and put the result in the PR. Findings that justify a design go in [`docs/`](docs/README.md), not in
  code comments.

  ```bash
  dotnet run --project tests/Benchmarks/Benchmarks.csproj --configuration Release -- --filter '*'
  ```

  Filter with fully-qualified patterns, e.g. `--filter 'Benchmarks.Perf.ParserBenchmarks.*'`. Results
  for `master` are published to [GitHub Pages](https://gabrielmarquezmatte.github.io/FixedWidthParser/dev/bench/),
  and every PR gets a benchmark comparison posted as a comment.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): summary`, imperative
mood, lowercase summary, no trailing period. Common types in this repo: `feat`, `fix`, `refactor`,
`perf`, `test`, `docs`, `chore`, `ci`, `build`. The scope is usually the area touched (`reader`,
`writer`, `parser`, `generator`). Example: `fix(reader): bound an unterminated line by MaxLineLength`.

## Pull requests

- Branch off `develop` (or `master` for a hotfix), and open the PR against the same branch.
- One focused change per PR — don't batch unrelated fixes into one commit or one PR.
- If a change is user-visible (new API, behavior change, performance claim), add it to
  [CHANGELOG.md](CHANGELOG.md) and mention it in the PR description.
- CI builds and tests on Linux, Windows, and macOS: the .NET suite, and the native library with its
  Python, C and C++ bindings. It also validates the package against the last release, audits
  dependencies, publishes and runs the NativeAOT smoke app, and runs CodeQL and the benchmark
  comparison. A change that only builds on one OS isn't ready to merge.

## Releasing

Push a `v*` tag (`v2.0.0`, or `v2.0.0-rc.1` for a prerelease). [release.yml](.github/workflows/release.yml)
takes the version from the tag, tests and packs the NuGet package, builds the native library on Windows x64,
macOS arm64 and x64, Linux glibc and musl x64 and arm64, tests one Python wheel per platform, then publishes
the package to NuGet, the wheels to PyPI as `fixedwidthparser`, and the native libraries and an SBOM
to the GitHub Release. Both registries publish through Trusted Publishing, so no API keys are stored:

- nuget.org: a trusted publishing policy for this repository and `release.yml`, and the repository variable
  `NUGET_USER` set to the nuget.org user.
- PyPI: a trusted publisher for this repository and `release.yml`. Before the first release, add it as a
  pending publisher for the project name `fixedwidthparser`.

## Reporting bugs / requesting features

Use the [issue templates](https://github.com/GabrielMarquezMatte/FixedWidthParser/issues/new/choose).
For suspected security vulnerabilities, do **not** open a public issue — see [SECURITY.md](SECURITY.md)
for the private reporting channel.

## Code of conduct

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md).
