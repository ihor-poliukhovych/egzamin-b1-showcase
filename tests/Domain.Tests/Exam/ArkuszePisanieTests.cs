using B1.Domain.Exam;
using AwesomeAssertions;

namespace B1.Domain.Tests.Exam;

/// <summary>
/// Shape of a Pisanie part as the reviewer expects it: every zestaw has tasks a and b, each with a known form
/// (a key of <see cref="Checklists.Writing"/>), a whole prompt and a sensible target length.
/// </summary>
public class ArkuszePisanieTests
{
    [Fact]
    public void PisanieItems_WhenFactoryPart_HaveKnownFormAndWholePrompt()
    {
        var part = ArkuszFactory.PisaniePart(Guid.NewGuid());

        part.Tasks.Should().NotBeEmpty();
        foreach (var task in part.Tasks)
        {
            task.Items.Should().HaveCount(2, $"zestaw {task.Number}: zadania a i b");
            foreach (var item in task.Items)
            {
                var label = $"zestaw {task.Number}.{item.Number}";
                Checklists.Writing.Should().ContainKey(item.Hint!, label);
                item.Prompt.Should().Contain("roszę", $"{label}: the prompt is complete");
                item.Prompt.Should().MatchRegex(@"^[„""(A-ZĄĆĘŁŃÓŚŹŻ]", $"{label}: the prompt is not cut off");
                item.TargetWords.Should().BeInRange(20, 300, label);
            }
        }
    }

    [Fact]
    public void PisanieReviewRequest_WhenBuiltFromFactoryPart_UsesFormAndTargetWordsOfEachItem()
    {
        var task = ArkuszFactory.PisaniePart(Guid.NewGuid()).Tasks[0];

        var inputs = task.Items.Select(i => new PisanieTaskInput(((char)('a' + i.Number - 1)).ToString(), i.Prompt!, i.Hint!, i.TargetWords, "Tekst.")).ToList();
        var prompt = B1.Infrastructure.Review.ClaudePisanieReviewer.BuildPrompt(new PisanieReviewRequest(inputs));

        prompt.Should().Contain("=== Zadanie a ===").And.Contain("=== Zadanie b ===")
            .And.Contain("Forma: ogloszenie").And.Contain("Wymagana długość: 150 słów");
    }
}
