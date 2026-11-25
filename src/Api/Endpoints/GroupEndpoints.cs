using B1.Domain.Auth;
using B1.Domain.Exam;
using B1.Domain.Groups;
using B1.Infrastructure.Groups;
using B1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;

namespace B1.Api.Endpoints;

public record GroupNameRequest(string Name);

public record InviteRequest(int? Days, int? MaxUses);

public record JoinRequest(bool Consent);

public record InviteDto(Guid Id, string Token, DateTimeOffset ExpiresAt, int? MaxUses, int Uses, DateTimeOffset? RevokedAt, InviteState State)
{
    public static InviteDto Create(GroupInvite i, DateTimeOffset now) => new(i.Id, i.Token, i.ExpiresAt, i.MaxUses, i.Uses, i.RevokedAt, i.Evaluate(now));
}

public record GroupDto(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? ArchivedAt)
{
    public static GroupDto Create(Group g) => new(g.Id, g.Name, g.CreatedAt, g.ArchivedAt);
}

public record StudentPartDto(PartKind Kind, PartStatus Status, decimal? Points, decimal MaxPoints, decimal? Percent, bool IsBelowThreshold, DateTimeOffset? SubmittedAt);

public record StudentStatusDto(string StudentId, string DisplayName, string Email, DateTimeOffset JoinedAt, IReadOnlyList<StudentPartDto> Parts,
    IReadOnlyList<PatternBrief> TopPatterns, DateTimeOffset? LastActivityAt)
{
    public static StudentStatusDto Create(StudentStatus s) => new(s.StudentId, s.DisplayName, s.Email, s.JoinedAt,
        s.Parts.Select(p => new StudentPartDto(p.Kind, p.Status, p.Points, p.MaxPoints, p.Percent, p.IsBelowThreshold, p.SubmittedAt)).ToList(),
        s.TopPatterns, s.LastActivityAt);
}

public record JoinPreviewDto(string State, Guid? GroupId, string? GroupName, string? TeacherName);

public static class GroupEndpoints
{
    private const int DefaultDays = 14;

    public static IEndpointRouteBuilder MapGroups(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/groups").WithTags("Groups");

        mine.MapGet("/mine", async (GroupService groups, CurrentUser user, CancellationToken ct) =>
            Results.Ok(await groups.MyGroupsAsync(user.Id, ct)));

        mine.MapDelete("/{id:guid}/membership", async Task<Results<NoContent, NotFound>> (Guid id, GroupService groups, CurrentUser user, CancellationToken ct) =>
            await groups.LeaveAsync(id, user.Id, ct) ? TypedResults.NoContent() : TypedResults.NotFound());

        var teacher = app.MapGroup("/groups").WithTags("Groups (teacher)").RequireAuthorization(nameof(UserRole.Teacher));

        teacher.MapGet("/", async (bool? includeArchived, GroupService groups, CurrentUser user, CancellationToken ct) =>
            Results.Ok((await groups.ListAsync(user.Id, includeArchived ?? false, ct)).Select(GroupDto.Create)));

        teacher.MapPost("/", async Task<Results<Created<GroupDto>, ValidationProblem>> (GroupNameRequest req, GroupService groups, CurrentUser user, CancellationToken ct) =>
        {
            if (ValidateName(req.Name) is { } errors) return TypedResults.ValidationProblem(errors);
            var group = await groups.CreateAsync(user.Id, req.Name, ct);
            return TypedResults.Created($"/groups/{group.Id}", GroupDto.Create(group));
        });

        teacher.MapGet("/{id:guid}", async Task<Results<Ok<GroupDto>, NotFound>> (Guid id, GroupService groups, CurrentUser user, CancellationToken ct) =>
        {
            var group = await groups.GetOwnedAsync(id, user.Id, ct);
            return group is null ? TypedResults.NotFound() : TypedResults.Ok(GroupDto.Create(group));
        });

        teacher.MapPut("/{id:guid}", async Task<Results<Ok<GroupDto>, NotFound, ValidationProblem>> (Guid id, GroupNameRequest req, GroupService groups, CurrentUser user, CancellationToken ct) =>
        {
            if (ValidateName(req.Name) is { } errors) return TypedResults.ValidationProblem(errors);
            var group = await groups.RenameAsync(id, user.Id, req.Name, ct);
            return group is null ? TypedResults.NotFound() : TypedResults.Ok(GroupDto.Create(group));
        });

        teacher.MapPost("/{id:guid}/archive", async Task<Results<NoContent, NotFound>> (Guid id, GroupService groups, CurrentUser user, CancellationToken ct) =>
            await groups.ArchiveAsync(id, user.Id, ct) ? TypedResults.NoContent() : TypedResults.NotFound());

        teacher.MapGet("/{id:guid}/invites", async (Guid id, GroupService groups, CurrentUser user, TimeProvider clock, CancellationToken ct) =>
            Results.Ok((await groups.ListInvitesAsync(id, user.Id, ct)).Select(i => InviteDto.Create(i, clock.GetUtcNow()))));

        teacher.MapPost("/{id:guid}/invites", async Task<Results<Created<InviteDto>, NotFound, ValidationProblem>> (Guid id, InviteRequest? req, GroupService groups, CurrentUser user, TimeProvider clock, CancellationToken ct) =>
        {
            var days = req?.Days ?? DefaultDays;
            var errors = GroupInvite.Validate(days, req?.MaxUses);
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);
            var invite = await groups.IssueInviteAsync(id, user.Id, TimeSpan.FromDays(days), req?.MaxUses, ct);
            return invite is null ? TypedResults.NotFound() : TypedResults.Created($"/groups/{id}/invites/{invite.Id}", InviteDto.Create(invite, clock.GetUtcNow()));
        });

        teacher.MapPost("/{id:guid}/invites/reissue", async Task<Results<Ok<InviteDto>, NotFound, ValidationProblem>> (Guid id, InviteRequest? req, GroupService groups, CurrentUser user, TimeProvider clock, CancellationToken ct) =>
        {
            var days = req?.Days ?? DefaultDays;
            var errors = GroupInvite.Validate(days, req?.MaxUses);
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);
            var invite = await groups.ReissueInviteAsync(id, user.Id, TimeSpan.FromDays(days), req?.MaxUses, ct);
            return invite is null ? TypedResults.NotFound() : TypedResults.Ok(InviteDto.Create(invite, clock.GetUtcNow()));
        });

        teacher.MapDelete("/{id:guid}/invites/{inviteId:guid}", async Task<Results<NoContent, NotFound>> (Guid id, Guid inviteId, GroupService groups, CurrentUser user, CancellationToken ct) =>
            await groups.RevokeInviteAsync(id, inviteId, user.Id, ct) ? TypedResults.NoContent() : TypedResults.NotFound());

        teacher.MapGet("/{id:guid}/students", async Task<Results<Ok<List<StudentStatusDto>>, NotFound>> (Guid id, GroupService groups, CurrentUser user, CancellationToken ct) =>
        {
            var students = await groups.GetStudentsAsync(id, user.Id, ct);
            return students is null ? TypedResults.NotFound() : TypedResults.Ok(students.Select(StudentStatusDto.Create).ToList());
        });

        teacher.MapDelete("/{id:guid}/students/{studentId}", async Task<Results<NoContent, NotFound>> (Guid id, string studentId, GroupService groups, CurrentUser user, CancellationToken ct) =>
            await groups.RemoveStudentAsync(id, studentId, user.Id, ct) ? TypedResults.NoContent() : TypedResults.NotFound());

        var join = app.MapGroup("/join").WithTags("Join");

        join.MapGet("/{token}", async (string token, GroupService groups, CancellationToken ct) =>
        {
            var preview = await groups.PreviewAsync(token, ct);
            var dto = new JoinPreviewDto(preview.State.ToString(), preview.GroupId, preview.GroupName, preview.TeacherName);
            return preview.State == JoinOutcome.NotFound ? Results.NotFound(dto) : Results.Ok(dto);
        });

        join.MapPost("/{token}", async (string token, JoinRequest req, GroupService groups, CurrentUser user, AppDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            var outcome = await groups.JoinAsync(token, user.Id, req.Consent, ct);
            // came through a teacher's link and has not chosen a role yet: a student, no separate question needed
            if (outcome is JoinOutcome.Joined && Guid.TryParse(user.Id, out var uid)
                && await db.Users.FirstOrDefaultAsync(u => u.Id == uid, ct) is { RoleChosen: false } joined
                && joined.ChooseRole(UserRole.Student, time.GetUtcNow()))
                await db.SaveChangesAsync(ct);
            var body = new { outcome = outcome.ToString() };
            return outcome switch
            {
                JoinOutcome.Joined or JoinOutcome.AlreadyMember => Results.Ok(body),
                JoinOutcome.NotFound => Results.NotFound(body),
                JoinOutcome.ConsentRequired => Results.BadRequest(body),
                _ => Results.Json(body, statusCode: StatusCodes.Status410Gone),
            };
        });

        return app;
    }

    public const int MaxNameLength = 200;

    private static Dictionary<string, string[]>? ValidateName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? new() { ["name"] = ["Name is required"] }
        : name.Trim().Length > MaxNameLength ? new() { ["name"] = [$"Name must be at most {MaxNameLength} characters"] }
        : null;
}
