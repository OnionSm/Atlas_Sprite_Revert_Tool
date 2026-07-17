namespace AtlasSpriteRevertTool.Models;

/// <summary>
/// Parsed data from a Unity .asset sprite file
/// </summary>
public class SpriteAssetData
{
    public string SpriteName { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;

    // From m_Rect / textureRect
    public double RectX { get; set; }
    public double RectY { get; set; }
    public double RectWidth { get; set; }
    public double RectHeight { get; set; }

    // GUID of the source texture (from m_RD.texture.guid)
    public string TextureGuid { get; set; } = string.Empty;

    // Resolved path after guid lookup
    public string? ResolvedTexturePath { get; set; }

    public bool IsResolved => ResolvedTexturePath != null;
}
