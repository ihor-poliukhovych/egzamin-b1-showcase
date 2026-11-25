using B1.Domain.Feedback;
using AwesomeAssertions;

namespace B1.Domain.Tests.Feedback;

public class FeedbackAttachmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    public void DetectType_WhenKnownSignature_ReturnsMimeType(byte[] data, string expected)
    {
        FeedbackAttachment.DetectType(data).Should().Be(expected);
    }

    [Fact]
    public void Create_WhenNotAnImage_Throws()
    {
        var act = () => FeedbackAttachment.Create(Guid.NewGuid(), "<svg/>"u8.ToArray(), Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhenTooLarge_Throws()
    {
        var data = new byte[FeedbackAttachment.MaxBytes + 1];
        data[0] = 0xFF; data[1] = 0xD8; data[2] = 0xFF;

        var act = () => FeedbackAttachment.Create(Guid.NewGuid(), data, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhenJpeg_KeepsTypeAndSize()
    {
        var attachment = FeedbackAttachment.Create(Guid.NewGuid(), [0xFF, 0xD8, 0xFF, 0xE0, 1, 2], Now);

        attachment.ContentType.Should().Be("image/jpeg");
        attachment.Size.Should().Be(6);
    }
}
