using System.Text.Json.Serialization;

namespace B1.Infrastructure.Seed;

public record ArkuszFile(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("parts")] List<ArkuszFilePart> Parts,
    [property: JsonPropertyName("level")] string? Level = null,
    [property: JsonPropertyName("variant")] string? Variant = null);

public record ArkuszFilePart(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("timeLimitMinutes")] int TimeLimitMinutes,
    [property: JsonPropertyName("tasks")] List<ArkuszFileTask> Tasks,
    [property: JsonPropertyName("audio")] string? Audio = null,
    [property: JsonPropertyName("maxPoints")] decimal? MaxPoints = null);

public record ArkuszFileTask(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("instruction")] string Instruction,
    [property: JsonPropertyName("pointsPerItem")] decimal PointsPerItem,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("basedOn")] string? BasedOn,
    [property: JsonPropertyName("example")] string? Example,
    [property: JsonPropertyName("wordBank")] List<string>? WordBank,
    [property: JsonPropertyName("transcript")] string? Transcript,
    [property: JsonPropertyName("items")] List<ArkuszFileItem> Items,
    [property: JsonPropertyName("image")] string? Image = null);

public record ArkuszFileItem(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("prompt")] string? Prompt,
    [property: JsonPropertyName("hint")] string? Hint,
    [property: JsonPropertyName("options")] List<string>? Options,
    [property: JsonPropertyName("answerKey")] List<string> AnswerKey,
    [property: JsonPropertyName("explanation")] string? Explanation,
    [property: JsonPropertyName("targetWords")] int TargetWords = 0,
    [property: JsonPropertyName("points")] decimal? Points = null);
