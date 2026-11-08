using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ottoman.Domain;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

/// <summary>
/// Reviews move from a text key to typed foreign keys without losing data: migrate to the step before, put reviews of
/// every kind in (and one pointing at nothing), migrate to the end and look.
/// </summary>
[Collection("api")]
public class ReviewTypedTargetsMigrationTests(ApiFixture api)
{
    private const string Before = "20261108164254_OrderCancelReason";

    [Fact]
    public async Task Existing_reviews_keep_their_data_on_typed_keys_and_orphans_are_dropped()
    {
        var name = $"mig_{Guid.NewGuid():N}"[..20];
        var cs = new SqlConnectionStringBuilder(api.ConnectionString) { InitialCatalog = name }.ConnectionString;
        var options = new DbContextOptionsBuilder<OttomanDbContext>().UseSqlServer(cs).Options;

        await using (var db = new OttomanDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(Before);

            await InsertAsync(db, "AspNetUsers", ("Id", "N'u-client'"));
            await InsertAsync(db, "AspNetUsers", ("Id", "N'u-client2'"));
            await InsertAsync(db, "AspNetUsers", ("Id", "N'u-barber'"));
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO EmployeeProfiles (UserId, TitleId, IsActive, PhotoZoom) SELECT TOP 1 N'u-barber', Id, 1, 1.0 FROM EmployeeTitles");
            await InsertAsync(db, "Products", ("Name", "N'Vosak'"), ("Price", "10"));
            await InsertAsync(db, "Services", ("Name", "N'Šišanje'"), ("DurationMinutes", "30"), ("Price", "10"));
            var product = await db.Database.SqlQueryRaw<int>("SELECT Id AS Value FROM Products").SingleAsync();
            var service = await db.Database.SqlQueryRaw<int>("SELECT Id AS Value FROM Services").SingleAsync();

            async Task Review(string client, string type, string key, int rating, string? comment, bool hidden = false, string? reply = null) =>
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO Reviews (ClientId, TargetType, TargetKey, Rating, Comment, CreatedAtUtc, IsHidden, ReplyText) " +
                    "VALUES ({0}, {1}, {2}, {3}, {4}, '2026-10-01', {5}, {6})", client, type, key, rating, comment!, hidden, reply!);

            await Review("u-client", "Product", product.ToString(), 5, "Odličan vosak", reply: "Hvala!");
            await Review("u-client", "Service", service.ToString(), 4, null, hidden: true);
            await Review("u-client", "Employee", "u-barber", 3, "Može bolje");
            await Review("u-client2", "Product", product.ToString(), 2, "Slab");
            await Review("u-client2", "Product", "987654", 1, "Proizvod više ne postoji"); // points at nothing

            await db.GetService<IMigrator>().MigrateAsync();
        }

        await using (var db = new OttomanDbContext(options))
        {
            var rows = await db.Reviews.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
            rows.Should().HaveCount(4, "the review of a product that is gone cannot be kept");
            rows.Select(r => (r.ClientId, r.TargetType, r.Rating, r.Comment, r.IsHidden, r.ReplyText)).Should().Equal(
                ("u-client", ReviewTargetType.Product, 5, "Odličan vosak", false, "Hvala!"),
                ("u-client", ReviewTargetType.Service, 4, null, true, null),
                ("u-client", ReviewTargetType.Employee, 3, "Može bolje", false, null),
                ("u-client2", ReviewTargetType.Product, 2, "Slab", false, null));
            rows[0].ProductId.Should().NotBeNull();
            rows[0].TargetKey.Should().Be(rows[0].ProductId.ToString());
            rows[1].ServiceId.Should().NotBeNull();
            rows[2].EmployeeProfileId.Should().Be("u-barber");
            rows[2].TargetKey.Should().Be("u-barber");

            // The database itself now refuses a second review of the same target, two targets at once, and a missing one.
            var productId = rows[0].ProductId;
            var duplicate = () => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Reviews (ClientId, TargetType, ProductId, Rating, CreatedAtUtc, IsHidden) VALUES (N'u-client', 'Product', {0}, 3, '2026-10-02', 0)", productId!);
            await duplicate.Should().ThrowAsync<SqlException>();
            var twoTargets = () => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Reviews (ClientId, TargetType, ProductId, ServiceId, Rating, CreatedAtUtc, IsHidden) VALUES (N'u-client2', 'Product', {0}, {1}, 3, '2026-10-02', 0)", productId!, rows[1].ServiceId!);
            await twoTargets.Should().ThrowAsync<SqlException>();
            var none = () => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Reviews (ClientId, TargetType, Rating, CreatedAtUtc, IsHidden) VALUES (N'u-client2', 'Service', 3, '2026-10-02', 0)");
            await none.Should().ThrowAsync<SqlException>();
            var dangling = () => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO Reviews (ClientId, TargetType, ProductId, Rating, CreatedAtUtc, IsHidden) VALUES (N'u-client2', 'Product', 424242, 3, '2026-10-02', 0)");
            await dangling.Should().ThrowAsync<SqlException>();

            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Migrating_back_restores_the_text_keys()
    {
        var name = $"mig_{Guid.NewGuid():N}"[..20];
        var cs = new SqlConnectionStringBuilder(api.ConnectionString) { InitialCatalog = name }.ConnectionString;
        var options = new DbContextOptionsBuilder<OttomanDbContext>().UseSqlServer(cs).Options;

        await using var db = new OttomanDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync();
        await InsertAsync(db, "AspNetUsers", ("Id", "N'u-client'"));
        await InsertAsync(db, "Products", ("Name", "N'Vosak'"), ("Price", "10"));
        var product = await db.Database.SqlQueryRaw<int>("SELECT Id AS Value FROM Products").SingleAsync();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Reviews (ClientId, TargetType, ProductId, Rating, CreatedAtUtc, IsHidden) VALUES (N'u-client', 'Product', {0}, 5, '2026-10-02', 0)", product);

        await db.GetService<IMigrator>().MigrateAsync(Before);

        var key = await db.Database.SqlQueryRaw<string>("SELECT TargetKey AS Value FROM Reviews").SingleAsync();
        key.Should().Be(product.ToString());
        await db.Database.EnsureDeletedAsync();
    }

    /// <summary>Inserts a row giving every required column without a default a harmless value, except those overridden.</summary>
    private static async Task InsertAsync(OttomanDbContext db, string table, params (string Column, string Sql)[] overrides)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        var required = new List<(string Name, string Type)>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS " +
                              $"WHERE TABLE_NAME = '{table}' AND IS_NULLABLE = 'NO' AND COLUMN_DEFAULT IS NULL " +
                              $"AND COLUMNPROPERTY(OBJECT_ID('{table}'), COLUMN_NAME, 'IsIdentity') = 0 AND DATA_TYPE <> 'timestamp'";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) required.Add((reader.GetString(0), reader.GetString(1)));
        }

        var columns = new List<string>();
        var values = new List<string>();
        foreach (var (column, type) in required)
        {
            columns.Add($"[{column}]");
            var given = overrides.Where(o => o.Column == column).Select(o => o.Sql).FirstOrDefault();
            values.Add(given ?? (column == "Id" && type is "nvarchar" ? $"N'{Guid.NewGuid():N}'" : type switch
            {
                "bit" or "int" or "bigint" or "smallint" or "tinyint" or "decimal" or "float" => "0",
                "datetime2" or "datetime" or "datetimeoffset" or "date" => "'2026-01-01'",
                "uniqueidentifier" => "NEWID()",
                _ => $"N'{Guid.NewGuid():N}'"[..12] + "'",
            }));
        }
        await using var insert = conn.CreateCommand();
        insert.CommandText = $"INSERT INTO {table} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
        await insert.ExecuteNonQueryAsync();
    }
}
