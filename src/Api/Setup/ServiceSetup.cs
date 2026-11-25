using System.Text.Json;
using System.Text.Json.Serialization;
using B1.Infrastructure.Auth;
using B1.Infrastructure.Content;
using B1.Infrastructure.Dashboard;
using B1.Infrastructure.Groups;
using B1.Infrastructure.Persistence;
using B1.Infrastructure.Review;
using B1.Infrastructure.Seed;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;

namespace B1.Api.Setup;

public static class ServiceSetup
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddAppDatabase(configuration);
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();
        services.AddSingleton(AuthSettings.From(configuration));
        services.AddScoped<UserProvisioner>();
        services.AddScoped<AccountService>();
        services.AddScoped<GroupService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ArkuszImporter>();
        services.AddReviewers(configuration);
        services.AddContentStore(configuration, environment.ContentRootPath);
        services.AddSingleton(TimeProvider.System);

        // Cloud: cookie keys live in the database (the container sleeps and is recreated)
        services.AddDataProtection().PersistKeysToDbContext<AppDbContext>().SetApplicationName("egzamin-b1");

        services.Configure<JsonOptions>(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o => o.SwaggerDoc("v1", new()
        {
            Title = "egzamin-b1 API",
            Version = "v1",
            Description = "Polish B1 exam practice: recurring-error bank, daily review and mock exam parts with AI grading.",
        }));

        var corsOrigins = CorsOrigins(configuration);
        services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));
        return services;
    }

    public static string[] CorsOrigins(IConfiguration configuration) => configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
}
