using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using B1.Api.Endpoints;
using B1.Api.Exam;
using B1.Infrastructure.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace B1.Api.Tests;

/// <summary>An invented arkusz with all five parts, imported into the test database; no official exam content.</summary>
internal static class TestData
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly SemaphoreSlim Lock = new(1, 1);

    public static async Task<string> SeedArkuszAsync(ApiFactory factory, string code = "test-1")
    {
        await Lock.WaitAsync();
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ArkuszImporter>().ImportAsync(Arkusz(code));
            return code;
        }
        finally
        {
            Lock.Release();
        }
    }

    public static async Task<ExamAttemptDto> StartAttemptAsync(HttpClient client, string code, string part)
    {
        var response = await client.PostAsJsonAsync("/exam-attempts", new { arkuszCode = code, part });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExamAttemptDto>(Json))!;
    }

    public static async Task<MeDto> MeAsync(HttpClient client) => (await client.GetFromJsonAsync<MeDto>("/auth/me"))!;

    public static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();

    public static string? Code(JsonElement problem) => problem.TryGetProperty("code", out var c) ? c.GetString() : null;

    private static ArkuszFile Arkusz(string code) => new(code, "Arkusz testowy", "own",
    [
        new("sluchanie", 30, [Task(1, "trueFalse", "Proszę zaznaczyć TAK albo NIE.", [Item(1, "Ania lubi kawę.", ["TAK"])])]),
        new("czytanie", 60, [Task(1, "trueFalse", "Proszę zaznaczyć TAK albo NIE.", [Item(1, "Sklep jest otwarty w niedzielę.", ["NIE"])])]),
        new("gramatyka", 45,
        [
            Task(1, "chooseInText", "Proszę podkreślić poprawną formę.", [Item(1, null, ["tą"], ["tę", "tą", "ta"])], text: "Rozmawiałam z {1} koleżanką."),
            Task(2, "transform", "Proszę przekształcić zdanie.", [Item(1, "Ola dała bratu książkę.", ["Brat dostał książkę od Oli"], hint: "dostał")]),
        ]),
        new("pisanie", 70,
        [
            new(1, "writing", "Proszę napisać oba teksty.", 10m, null, null, null, null, null,
            [
                new(1, "Proszę napisać ogłoszenie o sprzedaży roweru.", "ogloszenie", null, [], null, 40),
                new(2, "Proszę napisać list do kolegi o nowej pracy.", "list", null, [], null, 150),
            ]),
        ]),
        new("mowienie", 15,
        [
            new(1, "speaking", "Proszę wykonać zadania.", 10m, null, null, null, null, null,
            [
                new(1, "Proszę opisać swoje mieszkanie.", "monolog", null, [], null),
                new(2, "Proszę zarezerwować pokój w hotelu.", "sytuacja", null, [], null),
            ]),
        ]),
    ]);

    private static ArkuszFileTask Task(int number, string type, string instruction, List<ArkuszFileItem> items, string? text = null) =>
        new(number, type, instruction, 1m, text, null, null, null, null, items);

    private static ArkuszFileItem Item(int number, string? prompt, List<string> key, List<string>? options = null, string? hint = null) =>
        new(number, prompt, hint, options ?? [], key, null);
}
