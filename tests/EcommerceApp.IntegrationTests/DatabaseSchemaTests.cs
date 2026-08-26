using EcommerceApp.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EcommerceApp.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class DatabaseSchemaTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Fresh_database_has_all_migrations_foreign_key_indexes_and_constraints()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());

        await using var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync();

        var indexes = await ReadNamesAsync(connection, """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = 'public';
            """);
        Assert.Contains("IX_Addresses_UserId", indexes);
        Assert.Contains("IX_CartItems_ProductId", indexes);
        Assert.Contains("IX_DiscountRedemptions_DiscountCodeId", indexes);
        Assert.Contains("IX_DiscountRedemptions_OrderId", indexes);
        Assert.Contains("IX_OrderItems_OrderId", indexes);
        Assert.Contains("IX_OrderItems_ProductId", indexes);
        Assert.Contains("IX_Products_CategoryId", indexes);

        var constraints = await ReadNamesAsync(connection, """
            SELECT conname
            FROM pg_constraint
            WHERE connamespace = 'public'::regnamespace;
            """);
        Assert.Contains("CK_Product_Price", constraints);
        Assert.Contains("CK_Product_Stock", constraints);
        Assert.Contains("CK_CartItem_Quantity", constraints);
        Assert.Contains("CK_OrderItem_Quantity", constraints);
        Assert.Contains("PK_Products", constraints);
        Assert.Contains("PK_Orders", constraints);
        Assert.Contains("FK_Products_Categories_CategoryId", constraints);
        Assert.Contains("FK_Orders_AspNetUsers_CustomerId", constraints);
        Assert.Contains("FK_OrderItems_Orders_OrderId", constraints);
        Assert.Contains("FK_OrderItems_Products_ProductId", constraints);
        Assert.Contains("CK_DiscountCode_Value", constraints);
        Assert.Contains("CK_DiscountCode_Window", constraints);
        Assert.Contains("CK_DiscountCode_UsageLimit", constraints);
    }

    private static async Task<HashSet<string>> ReadNamesAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
