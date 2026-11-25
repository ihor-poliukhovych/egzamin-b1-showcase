using B1.Domain.Auth;
using B1.Infrastructure.Dashboard;
using Microsoft.AspNetCore.Http.HttpResults;

namespace B1.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboards(this IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/me", async (string? level, string? variant, DashboardService dashboards, CurrentUser user, CancellationToken ct) =>
            Results.Ok(await dashboards.GetStudentAsync(user.Id, level ?? "B1", variant ?? "dorosli", ct))).WithTags("Dashboard");

        var teacher = app.MapGroup("/teacher").WithTags("Dashboard (teacher)").RequireAuthorization(nameof(UserRole.Teacher));

        teacher.MapGet("/summary", async (string? level, string? variant, DashboardService dashboards, CurrentUser user, CancellationToken ct) =>
            Results.Ok(await dashboards.GetTeacherSummaryAsync(user.Id, ct, level ?? "B1", variant ?? "dorosli")));

        teacher.MapGet("/groups/{groupId:guid}/students/{studentId}/dashboard", async Task<Results<Ok<StudentDashboard>, NotFound>> (
            Guid groupId, string studentId, string? level, string? variant, DashboardService dashboards, CurrentUser user, CancellationToken ct) =>
        {
            var dashboard = await dashboards.GetForTeacherAsync(groupId, user.Id, studentId, level ?? "B1", variant ?? "dorosli", ct);
            return dashboard is null ? TypedResults.NotFound() : TypedResults.Ok(dashboard);
        });

        return app;
    }
}
