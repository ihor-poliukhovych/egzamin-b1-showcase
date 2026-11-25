using B1.Domain.Feedback;
using AwesomeAssertions;

namespace B1.Domain.Tests.Feedback;

public class FeedbackMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WhenValid_TrimsAndStartsAsNew()
    {
        var m = FeedbackMessage.Create("u1", FeedbackTopic.Bug, "  Nie działa audio  ", " ja@example.pl ", "RU", Now);

        m.Message.Should().Be("Nie działa audio");
        m.ReplyEmail.Should().Be("ja@example.pl");
        m.Language.Should().Be("ru");
        m.Status.Should().Be(FeedbackStatus.New);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("   ")]
    public void Create_WhenMessageTooShort_Throws(string message)
    {
        var act = () => FeedbackMessage.Create(null, FeedbackTopic.Other, message, null, null, Now);
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("message");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("a b@c.pl")]
    public void Create_WhenEmailInvalid_Throws(string email)
    {
        var act = () => FeedbackMessage.Create(null, FeedbackTopic.Other, "Wiadomość testowa", email, null, Now);
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("replyEmail");
    }

    [Fact]
    public void LimitReached_AnonymousSharesLargerCap()
    {
        FeedbackMessage.LimitReached(false, FeedbackMessage.UserHourlyLimit).Should().BeTrue();
        FeedbackMessage.LimitReached(true, FeedbackMessage.UserHourlyLimit).Should().BeFalse();
    }

    [Fact]
    public void MarkReviewed_StoresNote()
    {
        var m = FeedbackMessage.Create(null, FeedbackTopic.Idea, "Dodajcie B2", null, null, Now);
        m.MarkReviewed("  zapisane w TODO  ");
        m.Status.Should().Be(FeedbackStatus.Reviewed);
        m.ReviewNote.Should().Be("zapisane w TODO");
    }
}
