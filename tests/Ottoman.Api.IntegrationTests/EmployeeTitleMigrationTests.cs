using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

/// <summary>
/// The title moves from a text column to a foreign key without losing anyone's title: run the migrations up to the
/// one before, put barbers with text titles in, migrate to the end and look.
/// </summary>
[Collection("api")]
public class EmployeeTitleMigrationTests(ApiFixture api)
{
    private const string Before = "20261104074522_ReservationConfirmation";

    [Fact]
    public async Task Existing_barbers_keep_their_titles_as_foreign_keys_and_blank_ones_become_barber()
    {
        var name = $"mig_{Guid.NewGuid():N}"[..20];
        var cs = new SqlConnectionStringBuilder(api.ConnectionString) { InitialCatalog = name }.ConnectionString;
        var options = new DbContextOptionsBuilder<OttomanDbContext>().UseSqlServer(cs).Options;

        await using (var db = new OttomanDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(Before);

            // Three barbers: a title already on the list, a new one (in another case than it will be compared in),
            // and a blank one.
            var ids = new[] { "u-master", "u-new", "u-blank" };
            foreach (var id in ids) await InsertUserAsync(db, id);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO EmployeeProfiles (UserId, Title, IsActive, PhotoZoom) VALUES " +
                "(N'u-master', N'Master Barber', 1, 1.0), (N'u-new', N'  Stilist  ', 1, 1.0), (N'u-blank', N'', 1, 1.0)");

            await db.GetService<IMigrator>().MigrateAsync();
        }

        await using (var db = new OttomanDbContext(options))
        {
            var rows = await db.EmployeeProfiles.AsNoTracking()
                .Select(p => new { p.UserId, Title = p.Title.Name }).ToDictionaryAsync(p => p.UserId, p => p.Title);
            rows["u-master"].Should().Be("Master Barber");
            rows["u-new"].Should().Be("Stilist");
            rows["u-blank"].Should().Be("Barber");
            (await db.EmployeeTitles.CountAsync(t => t.Name == "Master Barber")).Should().Be(1, "no duplicate titles");

            // The database itself now refuses to delete a title somebody has.
            var master = await db.EmployeeTitles.Where(t => t.Name == "Master Barber").Select(t => t.Id).SingleAsync();
            var delete = () => db.EmployeeTitles.Where(t => t.Id == master).ExecuteDeleteAsync();
            await delete.Should().ThrowAsync<SqlException>();

            await db.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>A minimal account row: every required column of the users table gets a harmless value.</summary>
    private static async Task InsertUserAsync(OttomanDbContext db, string id)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        var required = new List<(string Name, string Type)>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS " +
                              "WHERE TABLE_NAME = 'AspNetUsers' AND IS_NULLABLE = 'NO' AND COLUMN_DEFAULT IS NULL";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) required.Add((reader.GetString(0), reader.GetString(1)));
        }

        var columns = new List<string>();
        var values = new List<string>();
        foreach (var (column, type) in required)
        {
            columns.Add($"[{column}]");
            values.Add(column == "Id" ? $"N'{id}'" : type switch
            {
                "bit" or "int" or "bigint" or "smallint" or "tinyint" or "decimal" or "float" => "0",
                "datetime2" or "datetime" or "datetimeoffset" or "date" => "'2026-01-01'",
                "uniqueidentifier" => "NEWID()",
                _ => "N'x'",
            });
        }
        await using var insert = conn.CreateCommand();
        insert.CommandText = $"INSERT INTO AspNetUsers ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
        await insert.ExecuteNonQueryAsync();
    }
}
