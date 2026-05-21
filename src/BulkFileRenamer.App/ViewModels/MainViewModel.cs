using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using BulkFileRenamer.Core;
using BulkFileRenamer.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BulkFileRenamer.App.ViewModels;

/// <summary>
/// The whole window is driven from here. Rule parameters are top-level
/// properties; whenever any of them changes, the preview is recomputed.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<FileRowViewModel> Files { get; } = new();

    // -------- Search and replace --------
    [ObservableProperty] private string _findText = string.Empty;
    [ObservableProperty] private string _replaceText = string.Empty;
    [ObservableProperty] private bool _matchCase = true;

    // -------- Remove characters --------
    [ObservableProperty] private string _removeChars = string.Empty;

    // -------- Case change --------
    [ObservableProperty] private CaseMode _caseMode = CaseMode.Lower;
    [ObservableProperty] private bool _changeCaseEnabled;

    // -------- Prefix / suffix --------
    [ObservableProperty] private string _prefix = string.Empty;
    [ObservableProperty] private string _suffix = string.Empty;

    // -------- Numbering --------
    [ObservableProperty] private bool _numberingEnabled;
    [ObservableProperty] private int _numberingStart = 1;
    [ObservableProperty] private int _numberingStep = 1;
    [ObservableProperty] private int _numberingPadding = 3;
    [ObservableProperty] private NumberingPosition _numberingPosition = NumberingPosition.Prefix;
    [ObservableProperty] private string _numberingSeparator = "_";

    // -------- Status --------
    [ObservableProperty] private string _statusMessage = "Add files to begin.";
    [ObservableProperty] private bool _hasConflicts;
    [ObservableProperty] private bool _canUndo;

    public IReadOnlyList<CaseMode> CaseModes { get; } =
        new[] { CaseMode.Lower, CaseMode.Upper, CaseMode.Title };

    public IReadOnlyList<NumberingPosition> NumberingPositions { get; } =
        new[] { NumberingPosition.Prefix, NumberingPosition.Suffix };

    private IReadOnlyList<ExecutedRename> _lastBatch = Array.Empty<ExecutedRename>();
    private readonly RenameExecutor _executor = new();

    // -------- Commands --------

    [RelayCommand]
    private void AddFiles()
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() != true) return;
        AddPaths(dlg.FileNames);
    }

    [RelayCommand]
    private void AddFolder()
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog() != true) return;
        var paths = Directory.EnumerateFiles(dlg.FolderName, "*", SearchOption.TopDirectoryOnly);
        AddPaths(paths);
    }

    [RelayCommand]
    private void Clear()
    {
        Files.Clear();
        StatusMessage = "Cleared.";
        UpdatePreview();
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        var items = Files.Select(f => new FileItem(f.OldFullPath)).ToList();
        var ops = RenamePlanner.Plan(items, BuildPipeline());
        try
        {
            _lastBatch = _executor.Execute(ops);
        }
        catch (IOException ex)
        {
            StatusMessage = "Error: " + ex.Message;
            return;
        }
        // Replace items with their post-rename paths
        var renamed = new List<FileRowViewModel>();
        foreach (var op in ops)
        {
            renamed.Add(new FileRowViewModel
            {
                OldFullPath = op.NewFullPath,
                OldName = Path.GetFileName(op.NewFullPath),
                NewName = Path.GetFileName(op.NewFullPath),
            });
        }
        Files.Clear();
        foreach (var row in renamed) Files.Add(row);
        CanUndo = _lastBatch.Count > 0;
        StatusMessage = $"Renamed {_lastBatch.Count} file(s).";
        UpdatePreview();
    }

    private bool CanApply() => Files.Count > 0 && !HasConflicts;

    [RelayCommand(CanExecute = nameof(CanUndoExec))]
    private void Undo()
    {
        try
        {
            _executor.Undo(_lastBatch);
        }
        catch (IOException ex)
        {
            StatusMessage = "Undo failed: " + ex.Message;
            return;
        }
        // Restore the file list to old paths
        var restored = _lastBatch.Select(e => new FileRowViewModel
        {
            OldFullPath = e.OldPath,
            OldName = Path.GetFileName(e.OldPath),
            NewName = Path.GetFileName(e.OldPath),
        }).ToList();
        Files.Clear();
        foreach (var row in restored) Files.Add(row);
        _lastBatch = Array.Empty<ExecutedRename>();
        CanUndo = false;
        StatusMessage = "Undone.";
        UpdatePreview();
    }

    private bool CanUndoExec() => CanUndo;

    // -------- Drag and drop --------

    public void HandleDrop(string[] paths)
    {
        var files = new List<string>();
        foreach (var p in paths)
        {
            if (File.Exists(p)) files.Add(p);
            else if (Directory.Exists(p))
            {
                files.AddRange(Directory.EnumerateFiles(p, "*", SearchOption.TopDirectoryOnly));
            }
        }
        AddPaths(files);
    }

    // -------- Internals --------

    private void AddPaths(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(Files.Select(f => f.OldFullPath),
            StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var p in paths)
        {
            if (!existing.Add(p)) continue;
            Files.Add(new FileRowViewModel
            {
                OldFullPath = p,
                OldName = Path.GetFileName(p),
                NewName = Path.GetFileName(p),
            });
            added++;
        }
        if (added > 0) StatusMessage = $"Added {added} file(s). Total: {Files.Count}.";
        UpdatePreview();
    }

    private RenamePipeline BuildPipeline()
    {
        var p = new RenamePipeline();
        if (!string.IsNullOrEmpty(FindText))
        {
            p.Rules.Add(new SearchAndReplaceRule
            {
                Find = FindText,
                Replace = ReplaceText,
                MatchCase = MatchCase,
            });
        }
        if (!string.IsNullOrEmpty(RemoveChars))
        {
            p.Rules.Add(new RemoveCharactersRule { Characters = RemoveChars });
        }
        if (ChangeCaseEnabled)
        {
            p.Rules.Add(new ChangeCaseRule { Mode = CaseMode });
        }
        if (!string.IsNullOrEmpty(Prefix))
        {
            p.Rules.Add(new PrefixRule { Text = Prefix });
        }
        if (!string.IsNullOrEmpty(Suffix))
        {
            p.Rules.Add(new SuffixRule { Text = Suffix });
        }
        if (NumberingEnabled)
        {
            p.Rules.Add(new NumberingRule
            {
                Start = NumberingStart,
                Step = NumberingStep,
                Padding = NumberingPadding,
                Position = NumberingPosition,
                Separator = NumberingSeparator,
            });
        }
        return p;
    }

    public void UpdatePreview()
    {
        if (Files.Count == 0)
        {
            HasConflicts = false;
            ApplyCommand.NotifyCanExecuteChanged();
            return;
        }
        var items = Files.Select(f => new FileItem(f.OldFullPath)).ToList();
        var ops = RenamePlanner.Plan(items, BuildPipeline());
        var conflicts = ConflictDetector.Find(ops);
        var conflictIndices = new HashSet<int>();
        foreach (var c in conflicts)
        {
            conflictIndices.Add(c.IndexA);
            conflictIndices.Add(c.IndexB);
        }
        for (var i = 0; i < Files.Count; i++)
        {
            Files[i].NewFullPath = ops[i].NewFullPath;
            Files[i].NewName = Path.GetFileName(ops[i].NewFullPath);
            Files[i].HasConflict = conflictIndices.Contains(i);
        }
        HasConflicts = conflicts.Count > 0;
        if (HasConflicts)
        {
            StatusMessage = $"{conflicts.Count} conflict(s) — destination paths collide.";
        }
        ApplyCommand.NotifyCanExecuteChanged();
    }

    // Recompute preview whenever any rule parameter changes.
    partial void OnFindTextChanged(string value) => UpdatePreview();
    partial void OnReplaceTextChanged(string value) => UpdatePreview();
    partial void OnMatchCaseChanged(bool value) => UpdatePreview();
    partial void OnRemoveCharsChanged(string value) => UpdatePreview();
    partial void OnCaseModeChanged(CaseMode value) => UpdatePreview();
    partial void OnChangeCaseEnabledChanged(bool value) => UpdatePreview();
    partial void OnPrefixChanged(string value) => UpdatePreview();
    partial void OnSuffixChanged(string value) => UpdatePreview();
    partial void OnNumberingEnabledChanged(bool value) => UpdatePreview();
    partial void OnNumberingStartChanged(int value) => UpdatePreview();
    partial void OnNumberingStepChanged(int value) => UpdatePreview();
    partial void OnNumberingPaddingChanged(int value) => UpdatePreview();
    partial void OnNumberingPositionChanged(NumberingPosition value) => UpdatePreview();
    partial void OnNumberingSeparatorChanged(string value) => UpdatePreview();
}
