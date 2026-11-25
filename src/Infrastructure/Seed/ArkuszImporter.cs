using System.Text.Json;
using B1.Domain.Exam;
using B1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B1.Infrastructure.Seed;

public class ArkuszImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly AppDbContext _appDbContext;
    private readonly ILogger<ArkuszImporter> _logger;

    public ArkuszImporter(AppDbContext appDbContext, ILogger<ArkuszImporter> logger)
    {
        _appDbContext = appDbContext;
        _logger = logger;
    }

    public record Result(int Inserted, int Updated, List<string> Codes);

    public async Task<Result> ImportDirectoryAsync(string directory, CancellationToken ct = default)
    {
        var files = Directory.GetFiles(directory, "*.json").OrderBy(f => f).ToList();
        if (files.Count == 0) throw new FileNotFoundException($"No *.json arkusze found in {directory}");

        var inserted = 0;
        var updated = 0;
        var codes = new List<string>();
        foreach (var file in files)
        {
            var result = await ImportFileAsync(file, ct);
            inserted += result.Inserted;
            updated += result.Updated;
            codes.AddRange(result.Codes);
        }

        return new Result(inserted, updated, codes);
    }

    public async Task<Result> ImportFileAsync(string jsonPath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(jsonPath);
        var file = await JsonSerializer.DeserializeAsync<ArkuszFile>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException($"Arkusz file {jsonPath} is empty");
        return await ImportAsync(file, ct);
    }

    public async Task<Result> ImportAsync(ArkuszFile file, CancellationToken ct = default)
    {
        _logger.LogInformation("ArkuszImporter.ImportAsync STARTED. code={Code}", file.Code);

        var existing = await _appDbContext.Arkusze
            .Include(a => a.Parts)
            .ThenInclude(p => p.Tasks)
            .ThenInclude(t => t.Items)
            .FirstOrDefaultAsync(a => a.Code == file.Code, ct);

        var source = Enum.Parse<ArkuszSource>(file.Source, ignoreCase: true);
        var inserted = 0;
        var updated = 0;

        if (existing is null)
        {
            var arkusz = Arkusz.Create(file.Code, file.Title, source, [], file.Level ?? "B1", file.Variant ?? "dorosli");
            arkusz.Parts = file.Parts.Select(p => CreatePart(arkusz.Id, p)).ToList();
            _appDbContext.Arkusze.Add(arkusz);
            inserted++;
        }
        else
        {
            UpdateInPlace(existing, file, source);
            updated++;
        }

        await _appDbContext.SaveChangesAsync(ct);
        _logger.LogInformation("ArkuszImporter.ImportAsync FINISHED. code={Code} inserted={Inserted} updated={Updated}", file.Code, inserted, updated);
        return new Result(inserted, updated, [file.Code]);
    }

    private static ExamPart CreatePart(Guid arkuszId, ArkuszFilePart p)
    {
        var part = ExamPart.Create(arkuszId, ParseKind(p.Kind), p.TimeLimitMinutes, []);
        part.AudioKey = p.Audio;
        part.MaxPointsOverride = p.MaxPoints;
        part.Tasks = p.Tasks.Select(t => CreateTask(part.Id, t)).ToList();
        return part;
    }

    private static ExamTask CreateTask(Guid partId, ArkuszFileTask t)
    {
        var task = ExamTask.Create(partId, t.Number, ParseType(t.Type), t.Instruction, t.PointsPerItem,
            t.Text, t.BasedOn, t.Example, t.WordBank ?? [], []);
        task.Transcript = t.Transcript;
        task.Image = t.Image;
        task.Items = t.Items.Select(i => CreateItem(task.Id, i)).ToList();
        return task;
    }

    private static ExamItem CreateItemWithPoints(Guid taskId, ArkuszFileItem i)
    {
        var item = ExamItem.Create(taskId, i.Number, i.Prompt, i.Hint, i.Options ?? [], i.AnswerKey, i.Explanation, i.TargetWords);
        item.PointsOverride = i.Points;
        return item;
    }

    private static ExamItem CreateItem(Guid taskId, ArkuszFileItem i) =>
        CreateItemWithPoints(taskId, i);

    private static void UpdateInPlace(Arkusz arkusz, ArkuszFile file, ArkuszSource source)
    {
        arkusz.Title = file.Title;
        arkusz.Source = source;
        arkusz.Level = file.Level ?? "B1";
        arkusz.Variant = file.Variant ?? "dorosli";

        foreach (var p in file.Parts)
        {
            var kind = ParseKind(p.Kind);
            var part = arkusz.Parts.FirstOrDefault(x => x.Kind == kind);
            if (part is null)
            {
                arkusz.Parts.Add(CreatePart(arkusz.Id, p));
                continue;
            }

            part.TimeLimitMinutes = p.TimeLimitMinutes;
            part.AudioKey = p.Audio;
            part.MaxPointsOverride = p.MaxPoints;
        part.MaxPointsOverride = p.MaxPoints;
            UpdateTasks(part, p.Tasks);
        }
    }

    private static void UpdateTasks(ExamPart part, List<ArkuszFileTask> tasks)
    {
        foreach (var t in tasks)
        {
            var task = part.Tasks.FirstOrDefault(x => x.Number == t.Number);
            if (task is null)
            {
                part.Tasks.Add(CreateTask(part.Id, t));
                continue;
            }

            task.Type = ParseType(t.Type);
            task.Instruction = t.Instruction;
            task.PointsPerItem = t.PointsPerItem;
            task.Text = t.Text;
            task.BasedOn = t.BasedOn;
            task.Example = t.Example;
            task.WordBank = t.WordBank ?? [];
            task.Transcript = t.Transcript;
            task.Image = t.Image;
    
            foreach (var i in t.Items)
            {
                var item = task.Items.FirstOrDefault(x => x.Number == i.Number);
                if (item is null)
                {
                    task.Items.Add(CreateItem(task.Id, i));
                    continue;
                }

                item.Prompt = i.Prompt;
                item.Hint = i.Hint;
                item.Options = i.Options ?? [];
                item.AnswerKey = i.AnswerKey;
                item.Explanation = i.Explanation;
                item.TargetWords = i.TargetWords;
                item.PointsOverride = i.Points;
            }
        }
    }

    private static PartKind ParseKind(string kind) => Enum.Parse<PartKind>(kind, ignoreCase: true);

    private static ExamTaskType ParseType(string type) => Enum.Parse<ExamTaskType>(type, ignoreCase: true);
}
