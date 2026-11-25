using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace B1.Infrastructure.Persistence;

/// <summary>For `dotnet ef migrations add`: no database connection is needed, only the provider.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=b1;Username=b1;Password=b1")
            .Options;
        return new AppDbContext(options);
    }
}
