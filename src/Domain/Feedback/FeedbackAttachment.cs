namespace B1.Domain.Feedback;

/// <summary>
/// An image attached to a "Contact" message (usually an error screenshot). Stored in the database next to the message and deleted with it:
/// at the end of the retention period and when the account is deleted. The type is determined by the file signature, not by what the browser sent.
/// </summary>
public class FeedbackAttachment
{
    public const int MaxPerMessage = 3;

    /// <summary>The browser compresses the image to ~1600 px JPEG before sending; this is the ceiling in case compression failed.</summary>
    public const int MaxBytes = 1_500_000;

    /// <summary>No more than this many images per day across the whole site: protects the database from flooding via the anonymous form.</summary>
    public const int DailySiteLimit = 300;

    protected FeedbackAttachment() { }

    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public int Size { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static FeedbackAttachment Create(Guid messageId, byte[] data, DateTimeOffset now)
    {
        if (data.Length == 0) throw new ArgumentException("Image is empty", nameof(data));
        if (data.Length > MaxBytes) throw new ArgumentException("Image is too large", nameof(data));
        var type = DetectType(data) ?? throw new ArgumentException("Only JPEG, PNG or WebP images", nameof(data));
        return new FeedbackAttachment { Id = Guid.NewGuid(), MessageId = messageId, ContentType = type, Data = data, Size = data.Length, CreatedAt = now };
    }

    /// <summary>JPEG / PNG / WebP by the first bytes; everything else (including SVG with scripts) is rejected.</summary>
    public static string? DetectType(ReadOnlySpan<byte> d)
    {
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "image/jpeg";
        if (d.Length >= 8 && d[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (d.Length >= 12 && d[..4].SequenceEqual("RIFF"u8) && d[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    public static string Extension(string contentType) => contentType switch
    {
        "image/png" => "png",
        "image/webp" => "webp",
        _ => "jpg",
    };
}
