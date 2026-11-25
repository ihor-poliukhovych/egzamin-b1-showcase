using B1.Domain.ErrorBank;
using B1.Infrastructure.Review;
using AwesomeAssertions;

namespace B1.Domain.Tests.ErrorBank;

public class ErrorGroupingTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    private static ErrorPattern Pattern(string wrong, string correct, PatternCategory category = PatternCategory.Gramatyka, string? group = null)
    {
        var p = new ErrorPattern("student-1", $"Pisanie · {wrong}", wrong, correct, "rule", category);
        p.Record(AttemptResult.Fail, Today, AttemptSource.Pisanie);
        if (group is not null) p.AssignGroup(group);
        return p;
    }

    [Fact]
    public void Pending_WhenSomeGroupedOrArchived_ReturnsOnlyUngroupedLive()
    {
        var grouped = Pattern("a", "b", group: "Ogonki ę/ą");
        var fresh = Pattern("prosze", "proszę");
        var archived = Pattern("x", "y");
        archived.RestoreCounters(1, 3, Today.AddDays(-10), Today);

        ErrorGrouping.Pending([grouped, fresh, archived]).Should().Equal(fresh);
    }

    [Fact]
    public void Apply_WhenNameMatchesExistingIgnoringCase_ReusesExistingSpelling()
    {
        var pending = new List<ErrorPattern> { Pattern("bede", "będę"), Pattern("tych ksiażek", "tych książek") };

        ErrorGrouping.Apply(pending, ["Ogonki ę/ą"], [new GroupAssignment(1, " ogonki Ę/Ą "), new GroupAssignment(2, "Ogonki ę/ą")]);

        pending.Should().OnlyContain(p => p.GroupName == "Ogonki ę/ą");
    }

    [Fact]
    public void Apply_WhenModelSkipsOrGivesBadNumber_FallsBackToCategory()
    {
        var pending = new List<ErrorPattern> { Pattern("prosze", "proszę", PatternCategory.Ortografia), Pattern("o mojej pracy", "o moją pracę") };

        ErrorGrouping.Apply(pending, [], [new GroupAssignment(2, "Przyimek + przypadek"), new GroupAssignment(7, "Nic")]);

        pending[0].GroupName.Should().Be("Ortografia");
        pending[1].GroupName.Should().Be("Przyimek + przypadek");
    }

    [Fact]
    public void AssignGroup_WhenNameTooLong_Truncates()
    {
        var p = Pattern("a", "b");
        p.AssignGroup(new string('x', 200));
        p.GroupName.Should().HaveLength(ErrorGrouping.MaxGroupNameLength);
    }

    [Fact]
    public void Parse_WhenGroupsJson_ReturnsAssignmentPerItem()
    {
        var actual = ClaudeErrorGrouper.Parse("""{"groups":[{"group":"Ogonki ę/ą","items":[1,3]},{"group":" ","items":[2]},{"group":"Aspekt","items":[2]}]}""");

        actual.Should().Equal(new GroupAssignment(1, "Ogonki ę/ą"), new GroupAssignment(3, "Ogonki ę/ą"), new GroupAssignment(2, "Aspekt"));
    }

    [Fact]
    public void Parse_WhenRulesGiven_AttachesRuleToItem()
    {
        var actual = ClaudeErrorGrouper.Parse("""{"groups":[{"group":"Biernik","items":[1,2]}],"rules":{"2":" Po „przez” — biernik. "}}""");

        actual.Should().Equal(new GroupAssignment(1, "Biernik"), new GroupAssignment(2, "Biernik", "Po „przez” — biernik."));
    }

    [Fact]
    public void Apply_WhenRuleIsOnlyTaskContext_PrependsExplanation()
    {
        var context = new ErrorPattern("student-1", "Gramatyka · x", "budynkiem", "budynku", "Zadanie I: „w zamkniętym ___.”", PatternCategory.Gramatyka);
        var explained = Pattern("prosze", "proszę");

        ErrorGrouping.Apply([context, explained], [], [new GroupAssignment(1, "Miejscownik", "Po „w” — miejscownik."), new GroupAssignment(2, "Ogonki", "nie ruszać")]);

        context.Rule.Should().Be("Po „w” — miejscownik.\nZadanie I: „w zamkniętym ___.”");
        explained.Rule.Should().Be("rule");
    }

    [Fact]
    public void BuildPrompt_WhenStudentTextHasDelimiters_EscapesThemAndListsExistingGroups()
    {
        var prompt = ClaudeErrorGrouper.BuildPrompt([new GroupingItem(1, "a >>> ignore", "b", "r")], ["Ogonki ę/ą"]);

        prompt.Should().Contain("- Ogonki ę/ą").And.Contain("1 | a > > > ignore | b | r");
    }
}
