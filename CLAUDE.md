# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Batch file renamer: WPF MVVM app over a pure rule-engine library. See `README.md` for features.

## Build & test

```bash
dotnet build --configuration Release
dotnet test  --configuration Release --no-build           # xUnit
dotnet test  --configuration Release --filter FullyQualifiedName~TestName   # single test
dotnet run   --project src/BulkFileRenamer.App            # run the app
```

Windows-only — the App targets `net8.0-windows` (WPF). `BulkFileRenamer.Core` is OS-agnostic.

## Architecture invariant

`BulkFileRenamer.Core` is pure logic (rules, pipeline, planner, conflict detector, executor) with **no WPF / Windows-UI types**. New behavior goes into Core behind `IRenameRule` and is covered by xUnit; `MainViewModel` stays thin glue. Don't pull UI types into Core or business logic into the ViewModel.

## Gotchas — do not regress

- **Renames must stay inside the source file's directory.** `ConflictDetector` flags any target whose normalized directory differs from the source as a conflict (path-traversal guard against `..\` / absolute paths typed into Prefix/Suffix/Separator). If you add a rule that can emit path separators, this guard must still catch it.
- **Case-only renames go through a temp name.** `RenameExecutor.MoveFile` detects targets equal under `OrdinalIgnoreCase` but different under `Ordinal` and renames `source → temp → target`. On NTFS a single `File.Move` for a case-only change silently no-ops or throws. Do not "simplify" this into one move, and do not treat case-only renames as no-ops — the user is deliberately changing case.
- **Partial-batch failure must preserve undo.** `RenameExecutor.Execute` throws `RenameExecutionException` carrying the renames that already completed; `MainViewModel.Apply` uses it to set `_lastBatch` / `CanUndo` and to sync the file list to paths that actually moved. Never let a mid-batch failure escape without the completed-renames list, or undo of partial work is lost.
- Path comparisons are case-insensitive (`OrdinalIgnoreCase`) because the target filesystem is Windows/NTFS.
