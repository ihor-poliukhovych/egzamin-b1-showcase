using B1.Domain.Exam;
using AwesomeAssertions;

namespace B1.Domain.Tests.Exam;

public class AnswerNormalizerTests
{
    [Theory]
    [InlineData("Z kim?", "z kim")]
    [InlineData("  z   kim  ", "z kim")]
    [InlineData("Z KIM", "z kim")]
    [InlineData("- Z kim?", "z kim")]
    [InlineData("– Czyje jest to pióro?", "czyje jest to pióro")]
    [InlineData("Nie pamiętam, jaki jest adres tego hotelu.", "nie pamiętam jaki jest adres tego hotelu")]
    [InlineData("Sąsiad pyta o drogę do dworca!", "sąsiad pyta o drogę do dworca")]
    [InlineData("ubierają się", "ubierają się")]
    public void Normalize_WhenValueHasCasePunctuationAndSpaces_ReturnsCanonicalForm(string value, string expected)
    {
        var actual = AnswerNormalizer.Normalize(value);

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?!.")]
    public void Normalize_WhenValueIsEmptyOrPunctuationOnly_ReturnsEmpty(string? value)
    {
        var actual = AnswerNormalizer.Normalize(value);

        actual.Should().BeEmpty();
    }

    [Fact]
    public void Matches_WhenValueEqualsAnyKeyAfterNormalization_ReturnsTrue()
    {
        var keys = new[] { "Brat dostał od Oli książkę", "Brat dostał książkę od Oli" };

        var actual = AnswerNormalizer.Matches("brat dostał książkę od oli.", keys);

        actual.Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenValueDiffersFromEveryKey_ReturnsFalse()
    {
        var keys = new[] { "Brat dostał od Oli książkę" };

        var actual = AnswerNormalizer.Matches("Brat dostał od Oli książeczkę", keys);

        actual.Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenValueIsEmpty_ReturnsFalse()
    {
        var actual = AnswerNormalizer.Matches("", ["tą"]);

        actual.Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenValueDiffersOnlyByDiacritics_ReturnsFalse()
    {
        var actual = AnswerNormalizer.Matches("ta", ["tą"]);

        actual.Should().BeFalse();
    }
}
