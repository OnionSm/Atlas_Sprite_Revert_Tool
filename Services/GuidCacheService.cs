using System.IO;
using System.Text.RegularExpressions;

namespace AtlasSpriteRevertTool.Services;

/// <summary>
/// Scans a folder tree and builds a GUID → file-path cache
/// by reading the companion .meta files that AssetRipper exports.
/// </summary>
public class GuidCacheService
{
    // guid (lower-case, no dashes) → absolute path of the asset file (not the .meta)
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Cache => _cache;

    public int Count => _cache.Count;

    // Simple regex to pull the guid line from a .meta file
    private static readonly Regex GuidRegex =
        new(@"^guid:\s*([0-9a-fA-F]{32})\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Scans <paramref name="rootFolder"/> recursively, reads every .meta file,
    /// and populates the internal guid→path dictionary.
    /// </summary>
    public async Task<int> ScanFolderAsync(string rootFolder, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        _cache.Clear();

        var metaFiles = Directory.EnumerateFiles(rootFolder, "*.meta",
            SearchOption.AllDirectories);

        int count = 0;
        await Task.Run(() =>
        {
            foreach (var metaPath in metaFiles)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var text = File.ReadAllText(metaPath);
                    var m = GuidRegex.Match(text);
                    if (!m.Success) continue;

                    var guid = m.Groups[1].Value.ToLowerInvariant();

                    // The actual asset is the same path without the .meta extension
                    var assetPath = metaPath[..^5]; // remove ".meta"
                    if (!File.Exists(assetPath)) continue;

                    _cache[guid] = assetPath;
                    count++;
                    progress?.Report($"Indexed: {Path.GetFileName(assetPath)}");
                }
                catch
                {
                    // Skip unreadable files
                }
            }
        }, ct);

        return count;
    }

    /// <summary>
    /// Looks up a GUID and returns the matching file path, or null if not found.
    /// </summary>
    public string? Resolve(string guid) =>
        _cache.TryGetValue(guid.ToLowerInvariant(), out var path) ? path : null;
}
