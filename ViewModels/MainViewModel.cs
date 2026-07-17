using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AtlasSpriteRevertTool.Models;

namespace AtlasSpriteRevertTool.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // ── Fields ──────────────────────────────────────────────────────────────
    private string _searchFolder = string.Empty;
    private string _outputFolder = string.Empty;
    private string _outputNameTemplate = "{name}";
    private string _statusText = "Ready";
    private int _progressValue;
    private int _progressMax = 1;
    private bool _isBusy;
    private string _cacheInfo = "No folder indexed yet.";
    private bool _isCacheReady;

    // ── Properties ──────────────────────────────────────────────────────────
    public string SearchFolder
    {
        get => _searchFolder;
        set { _searchFolder = value; OnPropertyChanged(); }
    }

    public string OutputFolder
    {
        get => _outputFolder;
        set { _outputFolder = value; OnPropertyChanged(); }
    }

    public string OutputNameTemplate
    {
        get => _outputNameTemplate;
        set { _outputNameTemplate = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public int ProgressValue
    {
        get => _progressValue;
        set { _progressValue = value; OnPropertyChanged(); }
    }

    public int ProgressMax
    {
        get => _progressMax;
        set { _progressMax = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle => !_isBusy;

    public string CacheInfo
    {
        get => _cacheInfo;
        set { _cacheInfo = value; OnPropertyChanged(); }
    }

    public bool IsCacheReady
    {
        get => _isCacheReady;
        set { _isCacheReady = value; OnPropertyChanged(); }
    }

    // Selected asset files
    public ObservableCollection<string> AssetFiles { get; } = [];

    // Results list
    public ObservableCollection<ExtractionResult> Results { get; } = [];

    // ── INotifyPropertyChanged ───────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    // ── Template resolver ────────────────────────────────────────────────────
    /// <summary>
    /// Resolves the output file name from the template.
    /// Supported tokens: {name}, {index}, {guid}
    /// If the template is empty, falls back to the asset file name (without extension).
    /// </summary>
    public string ResolveOutputName(string spriteName, int index, string guid, string assetFilePath)
    {
        // If template is blank → use the asset file name as output name
        string name = string.IsNullOrWhiteSpace(OutputNameTemplate)
            ? System.IO.Path.GetFileNameWithoutExtension(assetFilePath)
            : OutputNameTemplate
                .Replace("{name}", spriteName, StringComparison.OrdinalIgnoreCase)
                .Replace("{index}", index.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{guid}", guid[..Math.Min(8, guid.Length)], StringComparison.OrdinalIgnoreCase);

        // Strip characters that are invalid in file names
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return string.IsNullOrWhiteSpace(name) ? $"sprite_{index}" : name;
    }
}
