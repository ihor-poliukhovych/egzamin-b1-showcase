using B1.Domain.Exam;
using AwesomeAssertions;

namespace B1.Domain.Tests.Exam;

public class ExamAttemptAdoptTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 10, 19, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SubmittedAt = StartedAt.AddMinutes(25);
    private static readonly Guid ArkuszId = Guid.NewGuid();

    private static ExamSession Session(string userId = "student-1") => new(Guid.NewGuid(), userId, ArkuszId, StartedAt.AddHours(1));

    private static ExamAttempt Attempt(PartKind kind = PartKind.Sluchanie, DateTimeOffset? submittedAt = null, Guid? arkuszId = null, string userId = "student-1") =>
        new(Guid.NewGuid(), userId, arkuszId ?? ArkuszId, kind, StartedAt, submittedAt, 12m, 30m, []);

    [Fact]
    public void AdoptInto_WhenSubmittedStandaloneSameArkuszAndPart_LinksToSession()
    {
        var session = Session();
        var attempt = Attempt(submittedAt: SubmittedAt);

        var actual = attempt.AdoptInto(session, PartKind.Sluchanie);

        actual.Should().BeTrue();
        attempt.SessionId.Should().Be(session.Id);
    }

    [Fact]
    public void AdoptInto_WhenNotSubmitted_Refuses()
    {
        var attempt = Attempt(submittedAt: null);

        attempt.AdoptInto(Session(), PartKind.Sluchanie).Should().BeFalse();
        attempt.SessionId.Should().BeNull();
    }

    [Fact]
    public void AdoptInto_WhenAlreadyInAnotherSession_Refuses()
    {
        var attempt = Attempt(submittedAt: SubmittedAt);
        attempt.AdoptInto(Session(), PartKind.Sluchanie);

        attempt.AdoptInto(Session(), PartKind.Sluchanie).Should().BeFalse();
    }

    [Fact]
    public void AdoptInto_WhenOtherPartArkuszOrUser_Refuses()
    {
        var session = Session();

        Attempt(PartKind.Czytanie, SubmittedAt).AdoptInto(session, PartKind.Sluchanie).Should().BeFalse();
        Attempt(submittedAt: SubmittedAt, arkuszId: Guid.NewGuid()).AdoptInto(session, PartKind.Sluchanie).Should().BeFalse();
        Attempt(submittedAt: SubmittedAt, userId: "someone-else").AdoptInto(session, PartKind.Sluchanie).Should().BeFalse();
    }
}
