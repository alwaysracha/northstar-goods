using EcommerceApp.Web.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class DatabaseSchemaTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Deployment_journal_records_every_migration_and_seed_script()
    {
        var scripts = await ReadAsync(factory.AdminConnectionString, "SELECT ScriptName FROM dbo.SchemaVersions");
        Assert.Equal(["migrations/0001_schemas_and_reference_data.sql", "migrations/0002_core_tables.sql", "seed/0001_catalog_and_demo_accounts.sql", "seed/0002_customers_and_order_history.sql"], scripts.Order());
    }

    [Fact]
    public async Task Integrity_constraints_indexes_views_and_procedures_exist_and_are_trusted()
    {
        var constraints = (await ReadAsync(factory.AdminConnectionString, """
            SELECT name FROM sys.check_constraints UNION ALL SELECT name FROM sys.foreign_keys UNION ALL SELECT name FROM sys.key_constraints
            """)).ToHashSet();
        string[] expected = ["CK_Product_Price", "CK_Product_Stock", "CK_CartItem_Quantity", "CK_OrderItem_Quantity", "CK_Orders_Total", "CK_Orders_FailedPayment", "CK_Orders_ShippedIsPaid",
            "CK_Carts_Owner", "CK_DiscountCode_Normalized", "CK_DiscountCode_Window", "CK_PaymentAttempts_DeclineReason", "CK_Refunds_CapturedOnly", "CK_CardDetails_Type",
            "FK_Products_Categories", "FK_Orders_Users", "FK_OrderItems_Orders", "FK_OrderItems_Products", "FK_OrderStatusHistory_Transitions", "FK_Refunds_CapturedAttempt",
            "FK_OrderReturnItems_OrderItems", "FK_PaymentMethods_BillingAddress", "FK_CardDetails_PaymentMethods", "PK_Orders", "UQ_Orders_CheckoutToken"];
        Assert.All(expected, name => Assert.Contains(name, constraints));
        Assert.Empty(await ReadAsync(factory.AdminConnectionString, "SELECT name FROM sys.foreign_keys WHERE is_not_trusted = 1 OR is_disabled = 1 UNION ALL SELECT name FROM sys.check_constraints WHERE is_not_trusted = 1 OR is_disabled = 1"));

        var indexes = (await ReadAsync(factory.AdminConnectionString, "SELECT name FROM sys.indexes WHERE name IS NOT NULL")).ToHashSet();
        Assert.All(new[] { "IX_Carts_UserId", "IX_Carts_SessionKey", "UQ_PaymentMethods_OneDefaultPerUser", "UQ_PaymentAttempts_OneCapturePerOrder", "IX_Orders_CustomerId_CreatedAt" }, name => Assert.Contains(name, indexes));

        var programmability = (await ReadAsync(factory.AdminConnectionString, "SELECT CONCAT(SCHEMA_NAME(schema_id), '.', name) FROM sys.objects WHERE type IN ('V', 'P')")).ToHashSet();
        Assert.All(new[] { "reporting.vw_OrderSummary", "reporting.vw_FailedPayments", "reporting.vw_OrderTimeline", "reporting.vw_ReturnsAndRefunds", "reporting.vw_CustomerLifetimeValue",
            "reporting.vw_ProductSalesPerformance", "reporting.vw_DailySales", "reporting.vw_CustomerPaymentMethods", "sales.usp_ChangeOrderStatus" }, name => Assert.Contains(name, programmability));
    }

    [Fact]
    public async Task Every_column_the_EF_model_maps_exists_in_the_database()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model;
        var columns = (await ReadAsync(factory.AdminConnectionString, "SELECT CONCAT(TABLE_SCHEMA, '.', TABLE_NAME, '.', COLUMN_NAME) FROM INFORMATION_SCHEMA.COLUMNS")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = model.GetEntityTypes()
            .SelectMany(entity =>
            {
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                return entity.GetProperties().Select(p => $"{entity.GetSchema()}.{entity.GetTableName()}.{p.GetColumnName(table)}");
            })
            .Where(column => !columns.Contains(column))
            .ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public async Task Application_login_is_least_privilege()
    {
        await using var connection = new SqlConnection(factory.AppConnectionString);
        await connection.OpenAsync();
        await AssertDeniedAsync(connection, "SELECT COUNT(*) FROM dbo.SchemaVersions");
        await AssertDeniedAsync(connection, "CREATE TABLE sales.NotAllowed (Id int)");
        await AssertDeniedAsync(connection, "DELETE ref.OrderStatuses WHERE 1 = 0");
        await using var read = new SqlCommand("SELECT COUNT(*) FROM reporting.vw_OrderSummary", connection);
        Assert.True((int)(await read.ExecuteScalarAsync())! > 0);
    }

    private static async Task AssertDeniedAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Contains("permission", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<List<string>> ReadAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values;
    }
}
