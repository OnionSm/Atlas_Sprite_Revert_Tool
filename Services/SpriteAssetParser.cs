using System.IO;
using System.Text.RegularExpressions;
using AtlasSpriteRevertTool.Models;

namespace AtlasSpriteRevertTool.Services;

/// <summary>
/// Parses a Unity sprite .asset file (YAML) exported by AssetRipper
/// and extracts the sprite rectangle + texture GUID.
/// We avoid a full YAML parse because Unity uses non-standard tags.
/// </summary>
public static class SpriteAssetParser
{
    // --- field regexes ---
    private static readonly Regex NameRx =
        new(@"m_Name:\s*(.+)", RegexOptions.Compiled);

    // GUID from m_RD.texture
    // texture: {fileID: 2800000, guid: 885d388d462e9144e8b7a9fdfe055d7e, type: 3}
    private static readonly Regex TextureGuidRx =
        new(@"texture:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})[^}]*\}", RegexOptions.Compiled);

    public static SpriteAssetData? Parse(string filePath)
    {
        string text;
        try { text = File.ReadAllText(filePath); }
        catch { return null; }

        var data = new SpriteAssetData { SourceFilePath = filePath };

        // Name
        var m = NameRx.Match(text);
        data.SpriteName = m.Success ? m.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(filePath);

        // GUID – find the FIRST non-zero guid in a texture: { } block
        bool guidFound = false;
        foreach (Match gm in TextureGuidRx.Matches(text))
        {
            var guid = gm.Groups[1].Value;
            if (guid == "00000000000000000000000000000000") continue;
            data.TextureGuid = guid;
            guidFound = true;
            break;
        }
        if (!guidFound) return null; // can't do anything without a source texture

        // Try textureRect first (pixel coords inside the atlas PNG)
        double x = 0, y = 0, w = 0, h = 0;
        if (!TryParseBlock(text, "textureRect:", out x, out y, out w, out h))
        {
            TryParseBlock(text, "m_Rect:", out x, out y, out w, out h);
        }

        if (w <= 0 || h <= 0) return null;

        data.RectX = x;
        data.RectY = y;
        data.RectWidth = w;
        data.RectHeight = h;

        return data;
    }

    private static bool TryParseBlock(string text, string blockHeader,
        out double x, out double y, out double w, out double h)
    {
        x = y = w = h = 0;
        int idx = text.IndexOf(blockHeader, StringComparison.Ordinal);
        if (idx < 0) return false;

        // Take next ~300 chars after the block header
        int end = Math.Min(idx + 300, text.Length);
        var slice = text[idx..end];

        // Each field appears as "  x: value" etc inside the block
        static double? Field(string s, string key)
        {
            var rx = new Regex($@"^\s+{key}:\s*([\-\d\.]+)", RegexOptions.Multiline);
            var m = rx.Match(s);
            return m.Success && double.TryParse(m.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        var fx = Field(slice, "x");
        var fy = Field(slice, "y");
        var fw = Field(slice, "width");
        var fh = Field(slice, "height");

        if (fx == null || fy == null || fw == null || fh == null) return false;

        x = fx.Value; y = fy.Value; w = fw.Value; h = fh.Value;
        return true;
    }
}
