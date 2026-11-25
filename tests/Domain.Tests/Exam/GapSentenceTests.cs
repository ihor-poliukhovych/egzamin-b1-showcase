using B1.Domain.Exam;
using AwesomeAssertions;

namespace B1.Domain.Tests.Exam;

public class GapSentenceTests
{
    private static ExamTask Task(string? text) =>
        ExamTask.Create(Guid.NewGuid(), 1, ExamTaskType.ChooseInText, "Proszę podkreślić.", 0.5m, text, null, null, [], []);

    [Fact]
    public void GapSentence_WhenGapInMiddleSentence_ReturnsOnlyThatSentenceWithBlank()
    {
        var task = Task("Na (wakacjach) [przykład] nad morzem. Przez trzy {1} świeciło słońce. Spaliśmy w {2}.");

        task.GapSentence(1).Should().Be("Przez trzy ___ świeciło słońce.");
        task.GapSentence(2).Should().Be("Spaliśmy w ___.");
    }

    [Fact]
    public void GapSentence_WhenOtherGapsAndSolvedExample_ReplacesThem()
    {
        var task = Task("Zostaliśmy w domu, [[ponieważ]] padał deszcz, {1} dzieci {2} grały w karty.");

        task.GapSentence(2).Should().Be("Zostaliśmy w domu, ponieważ padał deszcz, … dzieci ___ grały w karty.");
    }

    [Fact]
    public void GapSentence_WhenNoTextOrNoGap_ReturnsNull()
    {
        Task(null).GapSentence(1).Should().BeNull();
        Task("Bez luk.").GapSentence(3).Should().BeNull();
    }

    [Fact]
    public void GapSentence_WhenSentenceTooLong_ClipsAroundGap()
    {
        var task = Task(string.Join(' ', Enumerable.Repeat("słowo", 60)) + " {1} " + string.Join(' ', Enumerable.Repeat("koniec", 40)) + ".");

        var actual = task.GapSentence(1)!;

        actual.Should().Contain("___").And.StartWith("…").And.EndWith("…");
        actual.Length.Should().BeLessThanOrEqualTo(222);
    }
}
