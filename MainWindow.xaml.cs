using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AtlasSpriteRevertTool.Models;
using AtlasSpriteRevertTool.Services;
using AtlasSpriteRevertTool.ViewModels;
using Microsoft.Win32;

namespace AtlasSpriteRevertTool;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly GuidCacheService _guidCache = new();
    private SpriteExtractorService? _extractor;
    private CancellationTokenSource? _cts;

    // Track previous window state for fullscreen restore
    private WindowState _prevWindowState = WindowState.Normal;
    private bool _isFullscreen = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // Default output folder
        _vm.OutputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "SpriteOutput");
    }

    // ── Window chrome ─────────────────────────────────────────────────────────
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleFullscreen(sender, e);
        else
            DragMove();
    }

    private void MinimizeWindow(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void CloseWindow(object sender, RoutedEventArgs e) => Close();

    private void ToggleFullscreen(object sender, RoutedEventArgs e)
    {
        if (!_isFullscreen)
        {
            // Enter fullscreen
            _prevWindowState = WindowState;
            _isFullscreen = true;

            WindowStyle   = WindowStyle.None;
            ResizeMode    = ResizeMode.NoResize;
            WindowState   = WindowState.Maximized;

            // Hide shadow margin when maximised
            MainBorder.Margin = new Thickness(0);
            MainBorder.CornerRadius = new System.Windows.CornerRadius(0);

            BtnFullscreen.Content = "❐"; // restore icon
            BtnFullscreen.ToolTip = "Exit Fullscreen";
        }
        else
        {
            // Exit fullscreen → restore window
            _isFullscreen = false;

            WindowStyle = WindowStyle.None;
            ResizeMode  = ResizeMode.CanResizeWithGrip;
            WindowState = _prevWindowState == WindowState.Maximized
                ? WindowState.Normal
                : _prevWindowState;

            MainBorder.Margin = new Thickness(10);
            MainBorder.CornerRadius = new System.Windows.CornerRadius(18);

            BtnFullscreen.Content = "⛶"; // fullscreen icon
            BtnFullscreen.ToolTip = "Toggle Fullscreen";
        }
    }

    // ── Browse buttons ────────────────────────────────────────────────────────
    private void BrowseSearchFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select Atlas Texture Search Folder" };
        if (dlg.ShowDialog() == true)
            _vm.SearchFolder = dlg.FolderName;
    }

    private void BrowseOutputFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select Output Folder" };
        if (dlg.ShowDialog() == true)
            _vm.OutputFolder = dlg.FolderName;
    }

    // ── Asset file list ───────────────────────────────────────────────────────
    private void AddAssetFiles(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Unity .asset Files",
            Filter = "Unity Asset Files (*.asset)|*.asset|All Files (*.*)|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        foreach (var f in dlg.FileNames)
            if (!_vm.AssetFiles.Contains(f))
                _vm.AssetFiles.Add(f);
    }

    private void AddAssetFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select Folder Containing .asset Files"
        };
        if (dlg.ShowDialog() != true) return;

        int added = 0;
        foreach (var asset in Directory.EnumerateFiles(dlg.FolderName, "*.asset",
                     SearchOption.AllDirectories))
        {
            if (!_vm.AssetFiles.Contains(asset))
            {
                _vm.AssetFiles.Add(asset);
                added++;
            }
        }

        if (added == 0)
            MessageBox.Show("No .asset files found in the selected folder.",
                "No Files Found", MessageBoxButton.OK, MessageBoxImage.Information);
        else
            _vm.StatusText = $"Added {added} .asset file(s) from folder.";
    }

    private void ClearAssetFiles(object sender, RoutedEventArgs e) =>
        _vm.AssetFiles.Clear();

    private void AssetFileList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void AssetFileList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var f in files)
        {
            if (Directory.Exists(f))
            {
                foreach (var asset in Directory.EnumerateFiles(f, "*.asset",
                             SearchOption.AllDirectories))
                    if (!_vm.AssetFiles.Contains(asset))
                        _vm.AssetFiles.Add(asset);
            }
            else if (!_vm.AssetFiles.Contains(f) &&
                     f.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                _vm.AssetFiles.Add(f);
            }
        }
    }

    // ── Index Folder ──────────────────────────────────────────────────────────
    private async void IndexFolder(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.SearchFolder))
        {
            MessageBox.Show("Please select a Search Folder first.", "Missing Folder",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _vm.IsBusy = true;
        _vm.IsCacheReady = false;
        _vm.CacheInfo = "Indexing…";
        _vm.StatusText = "Building GUID cache…";

        try
        {
            _cts = new CancellationTokenSource();
            var progress = new Progress<string>(msg => _vm.StatusText = msg);
            int count = await _guidCache.ScanFolderAsync(_vm.SearchFolder, progress, _cts.Token);

            _vm.IsCacheReady = count > 0;
            _vm.CacheInfo = $"✔  {count} assets indexed in:\n{_vm.SearchFolder}";
            _vm.StatusText = $"Index complete — {count} files.";

            _extractor?.ClearImageCache();
            _extractor = new SpriteExtractorService(_guidCache);
        }
        catch (OperationCanceledException)
        {
            _vm.CacheInfo = "Indexing cancelled.";
            _vm.StatusText = "Cancelled.";
        }
        catch (Exception ex)
        {
            _vm.CacheInfo = $"Error: {ex.Message}";
            _vm.StatusText = "Indexing failed.";
        }
        finally
        {
            _vm.IsBusy = false;
        }
    }

    // ── Extract ───────────────────────────────────────────────────────────────
    private async void ExtractSprites(object sender, RoutedEventArgs e)
    {
        if (_vm.AssetFiles.Count == 0)
        {
            MessageBox.Show("Add at least one .asset file.", "No Files",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!_vm.IsCacheReady)
        {
            MessageBox.Show("Please index the Search Folder first so GUIDs can be resolved.",
                "Cache Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_vm.OutputFolder))
        {
            MessageBox.Show("Please select an Output Folder.", "Missing Output",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _extractor ??= new SpriteExtractorService(_guidCache);
        _vm.IsBusy = true;
        _vm.ProgressValue = 0;
        _vm.ProgressMax = _vm.AssetFiles.Count;

        _cts = new CancellationTokenSource();

        try
        {
            var files = _vm.AssetFiles.ToList();
            int firstResultIndex = _vm.Results.Count;

            for (int i = 0; i < files.Count; i++)
            {
                _cts.Token.ThrowIfCancellationRequested();

                var filePath = files[i];
                _vm.StatusText = $"Parsing {Path.GetFileName(filePath)}…";

                var spriteData = await Task.Run(() => SpriteAssetParser.Parse(filePath));

                if (spriteData == null)
                {
                    _vm.Results.Add(new ExtractionResult
                    {
                        AssetFilePath = filePath,
                        SpriteName    = Path.GetFileNameWithoutExtension(filePath),
                        Status        = ExtractionStatus.Error,
                        Message       = "Failed to parse asset file (no GUID or invalid rect)."
                    });
                    _vm.ProgressValue = i + 1;
                    continue;
                }

                var outputName = _vm.ResolveOutputName(spriteData.SpriteName, i + 1,
                    spriteData.TextureGuid, filePath);

                var result = await _extractor.ExtractAsync(spriteData, _vm.OutputFolder,
                    outputName, _cts.Token);

                _vm.Results.Add(result);
                _vm.StatusText    = result.Message;
                _vm.ProgressValue = i + 1;

                if (_vm.Results.Count > 0)
                    ResultsList.ScrollIntoView(_vm.Results[^1]);
            }

            var runResults = _vm.Results.Skip(firstResultIndex).ToList();
            int ok  = runResults.Count(r => r.Status == ExtractionStatus.Success);
            int err = runResults.Count(r => r.Status == ExtractionStatus.Error);
            _vm.StatusText = $"Done — {ok} succeeded, {err} failed.";
        }
        catch (OperationCanceledException)
        {
            _vm.StatusText = "Extraction cancelled.";
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            _vm.IsBusy = false;
            _extractor.ClearImageCache();
        }
    }

    // ── Results log ───────────────────────────────────────────────────────────
    private void ClearResults(object sender, RoutedEventArgs e) =>
        _vm.Results.Clear();

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _extractor?.ClearImageCache();
        base.OnClosed(e);
    }
}