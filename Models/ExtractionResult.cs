namespace AtlasSpriteRevertTool.Models;

public enum ExtractionStatus
{
    Pending,
    Success,
    Warning,
    Error
}

public class ExtractionResult
{
    public string AssetFilePath { get; set; } = string.Empty;
    public string SpriteName { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public ExtractionStatus Status { get; set; } = ExtractionStatus.Pending;
    public string Message { get; set; } = string.Empty;

    // For display binding
    public string StatusIcon => Status switch
    {
        ExtractionStatus.Success => "✔",
        ExtractionStatus.Warning => "⚠",
        ExtractionStatus.Error   => "✖",
        _                        => "…"
    };

    public string StatusColor => Status switch
    {
        ExtractionStatus.Success => "#4ade80",
        ExtractionStatus.Warning => "#fbbf24",
        ExtractionStatus.Error   => "#f87171",
        _                        => "#94a3b8"
    };
}
