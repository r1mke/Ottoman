using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ottoman.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` create migrations without a running database.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<OttomanDbContext>
{
    public OttomanDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                 ?? "Server=localhost,1433;Database=220048;User Id=sa;Password=Design_Time_1!;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<OttomanDbContext>().UseSqlServer(cs).Options;
        return new OttomanDbContext(options);
    }
}
