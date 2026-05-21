using CommunityToolkit.Mvvm.ComponentModel;

namespace BulkFileRenamer.App.ViewModels;

public partial class FileRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _oldName = string.Empty;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private bool _hasConflict;

    public string OldFullPath { get; init; } = string.Empty;
    public string NewFullPath { get; set; } = string.Empty;
}
