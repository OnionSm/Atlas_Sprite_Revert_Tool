using System.IO;
using AtlasSpriteRevertTool.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AtlasSpriteRevertTool.Services;

/// <summary>
/// Crops a sprite from its source atlas texture and saves to the output folder.
/// Unity stores Y-axis bottom-up, so we flip it.
/// </summary>
public class SpriteExtractorService
{
    private readonly GuidCacheService _guidCache;

    // Cache of already-opened atlas images (path → Image) to avoid re-loading
    private readonly Dictionary<string, Image> _imageCache = new(StringComparer.OrdinalIgnoreCase);

    public SpriteExtractorService(GuidCacheService guidCache) => _guidCache = guidCache;

    public async Task<ExtractionResult> ExtractAsync(
        SpriteAssetData sprite,
        string outputFolder,
        string outputName,
        CancellationToken ct = default)
    {
        var result = new ExtractionResult
        {
            AssetFilePath = sprite.SourceFilePath,
            SpriteName = sprite.SpriteName
        };

        // 1. Resolve GUID → texture path
        if (string.IsNullOrEmpty(sprite.TextureGuid))
        {
            result.Status = ExtractionStatus.Error;
            result.Message = "No texture GUID in asset file.";
            return result;
        }

        var texturePath = _guidCache.Resolve(sprite.TextureGuid);
        if (texturePath == null)
        {
            result.Status = ExtractionStatus.Error;
            result.Message = $"GUID not found in cache: {sprite.TextureGuid}";
            return result;
        }

        sprite.ResolvedTexturePath = texturePath;

        // 2. Load (or reuse) the atlas image
        Image atlasImage;
        try
        {
            if (!_imageCache.TryGetValue(texturePath, out atlasImage!))
            {
                atlasImage = await Image.LoadAsync(texturePath, ct);
                _imageCache[texturePath] = atlasImage;
            }
        }
        catch (Exception ex)
        {
            result.Status = ExtractionStatus.Error;
            result.Message = $"Cannot load texture: {ex.Message}";
            return result;
        }

        // 3. Compute crop rectangle
        // Unity's Y-axis is bottom-up; image Y-axis is top-down → flip
        int imgHeight = atlasImage.Height;
        int x = (int)Math.Round(sprite.RectX);
        int y = (int)Math.Round(sprite.RectY);
        int w = (int)Math.Round(sprite.RectWidth);
        int h = (int)Math.Round(sprite.RectHeight);

        // Clamp to atlas bounds
        int atlasW = atlasImage.Width;
        x = Math.Clamp(x, 0, atlasW - 1);
        y = Math.Clamp(y, 0, imgHeight - 1);
        w = Math.Clamp(w, 1, atlasW - x);
        h = Math.Clamp(h, 1, imgHeight - y);

        // Flip Y: Unity bottom-left → image top-left
        int flippedY = imgHeight - y - h;
        flippedY = Math.Clamp(flippedY, 0, imgHeight - 1);
        h = Math.Clamp(h, 1, imgHeight - flippedY);

        var cropRect = new Rectangle(x, flippedY, w, h);

        // 4. Crop and save
        try
        {
            Directory.CreateDirectory(outputFolder);

            var outPath = Path.Combine(outputFolder, outputName);
            if (!outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                outPath += ".png";

            using var cropped = atlasImage.Clone(ctx => ctx.Crop(cropRect));
            await cropped.SaveAsPngAsync(outPath, ct);

            result.Status = ExtractionStatus.Success;
            result.OutputPath = outPath;
            result.Message = $"Saved → {outPath}";
        }
        catch (Exception ex)
        {
            result.Status = ExtractionStatus.Error;
            result.Message = $"Crop/save failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Clears the cached atlas images to free memory.
    /// </summary>
    public void ClearImageCache()
    {
        foreach (var img in _imageCache.Values)
            img.Dispose();
        _imageCache.Clear();
    }
}
