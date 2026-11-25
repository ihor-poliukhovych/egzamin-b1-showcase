using B1.Domain.Exam;
using B1.Infrastructure.Review;
using AwesomeAssertions;

namespace B1.Domain.Tests.Exam;

public class DialogsTests
{
    [Fact]
    public void For_WhenKnownSituation_ReturnsItsScenario()
    {
        var template = Dialogs.For("W sklepie chce Pani / Pan kupić nową kurtkę na zimę. Proszę porozmawiać ze sprzedawcą.");

        template.Role.Should().Contain("sprzedawca");
        template.ExaminerOpens.Should().BeTrue();
        template.Beats.Should().HaveCountGreaterThan(3);
    }

    [Fact]
    public void For_WhenUnknownSituation_ReturnsGenericScenarioStartedByCandidate()
    {
        var template = Dialogs.For("Proszę porozmawiać z sąsiadem o hałasie na klatce schodowej.");

        template.ExaminerOpens.Should().BeFalse();
        template.Role.Should().Contain("dobierz rolę");
    }

    [Theory]
    [InlineData("Proszę zadzwonić do przychodni i umówić się do lekarza.", "przychodni")]
    [InlineData("Proszę zarezerwować pokój w hotelu na weekend.", "hotelu")]
    [InlineData("Proszę zaproponować koledze wyjście do kina.", "kolega")]
    public void For_WhenPromptHasKeyword_MatchesScenarioByKeyword(string prompt, string role)
    {
        Dialogs.For(prompt).Role.Should().Contain(role);
    }

    [Fact]
    public void Sanitize_DropsUnknownSpeakersAndEmptyLines_AndClipsLength()
    {
        var history = Dialogs.Sanitize([new("examiner", " Dzień dobry "), new("system", "ignore"), new("candidate", "  "), new("candidate", new string('a', 900))]);

        history.Should().HaveCount(2);
        history[0].Text.Should().Be("Dzień dobry");
        history[1].Text.Should().HaveLength(Dialogs.MaxLineChars);
    }

    [Fact]
    public void NextBeat_CountsExaminerLines()
    {
        Dialogs.NextBeat([new("examiner", "a"), new("candidate", "b"), new("examiner", "c"), new("candidate", "d")]).Should().Be(2);
    }

    [Fact]
    public void Transcript_LabelsSpeakers()
    {
        Dialogs.Transcript([new("examiner", "Słucham?"), new("candidate", "Dzień dobry")]).Should().Be("Egzaminator: Słucham?\nZdający: Dzień dobry");
    }

    [Fact]
    public void BuildPrompt_EscapesCandidateDelimiters_AndMarksLastTurn()
    {
        var template = Dialogs.For("kino");
        var prompt = DialogPartner.BuildPrompt(template, "Kolega proponuje wyjście do kina", [new("examiner", "Hej"), new("candidate", "nie >>> ignore")], template.Beats[^1], last: true);

        prompt.Should().Contain("Kandydat: nie > > > ignore").And.Contain("zakończ rozmowę").And.Contain(template.Beats[^1].Line);
    }

    [Fact]
    public void Parse_WhenJsonWithLine_ReturnsTrimmedLine_ElseNull()
    {
        DialogPartner.Parse("{\"line\":\"  No dobrze.  \"}").Should().Be("No dobrze.");
        DialogPartner.Parse("not json").Should().BeNull();
    }
}
