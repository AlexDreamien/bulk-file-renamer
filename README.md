# bulk-file-renamer

[![CI](https://github.com/AlexDreamien/bulk-file-renamer/actions/workflows/ci.yml/badge.svg)](https://github.com/AlexDreamien/bulk-file-renamer/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)

Windows desktop app for batch-renaming files. Pipeline of composable rules
with a live preview, conflict detection, and undo. WPF + MVVM front-end on
top of a pure rule-engine library that's covered by xUnit.

> _Application window screenshot lands here once captured._
>
> ![Screenshot placeholder](docs/screenshot.png)

## Features

- **Add files** via dialog (`Add files...`), an entire folder
  (`Add folder...`), or drag-and-drop straight onto the preview grid.
- **Six composable rules**, applied in a fixed, easy-to-reason order:
  1. Search and replace (case sensitive by default)
  2. Remove characters from a deny-list
  3. Change case (lower / UPPER / Title)
  4. Prefix
  5. Suffix
  6. Sequential numbering (configurable start / step / padding / position / separator)
- **Live preview** — every edit recomputes new names instantly through
  `CommunityToolkit.Mvvm`'s generated `OnXxxChanged` hooks.
- **Conflict detection** — rows whose target paths collide are highlighted
  in red; the Apply button stays disabled until conflicts are resolved.
- **Undo** the last batch. Renames are replayed in opposite order so chained
  moves restore cleanly.

## Architecture

Three projects in one solution, all targeting `net8.0`:

```
src/BulkFileRenamer.Core      Pure logic — rules, pipeline, planner,
                              ConflictDetector, RenameExecutor.
                              No WPF, no Windows-specific types.
src/BulkFileRenamer.App       WPF UI. Single MainWindow + MainViewModel,
                              uses CommunityToolkit.Mvvm.
tests/BulkFileRenamer.Tests   xUnit suite over Core (28 tests).
```

Splitting Core out is the load-bearing design choice: the rules and
planner can be tested in isolation without spinning up a window, and the
ViewModel composes them as a stack of `IRenameRule` instances. The UI is
thin glue.

## Building

Requires .NET 8 SDK and Windows (WPF target framework is `net8.0-windows`).

```bash
dotnet restore
dotnet build --configuration Release
dotnet test  --configuration Release --no-build
```

Run the app:

```bash
dotnet run --project src/BulkFileRenamer.App
```

## Tests

`dotnet test` runs 28 xUnit cases covering:

- **Each rule** — empty-input noop, case sensitivity, padding / step /
  position variants, mode table for case change, character-set semantics
- **Pipeline** — order, identity, full path composition
- **Planner** — order, noop detection
- **Conflict detector** — unique destinations, pairwise / 3-way collisions,
  case-insensitive path comparison
- **Executor** — mover invocation, noop skipping, Undo order
- **Integration** — real filesystem round-trip in a temp directory

CI runs the same on `windows-latest` for every push and pull request — see
[`.github/workflows/ci.yml`](.github/workflows/ci.yml).

## Out of scope

By design: no regex rules (text find/replace only), no cloud features,
no undo history beyond the last operation, no support for other operating
systems (WPF is Windows-only). The Core library itself is OS-agnostic and
could power a console or cross-platform UI separately.

## License

[MIT](LICENSE).
