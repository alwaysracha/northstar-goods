using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class SeedDataTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Seed_has_required_volume_and_mostly_successful_orders()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Users.CountAsync() >= 1000);
        var orders = await db.Orders.CountAsync();
        Assert.True(orders >= 5000);
        var paid = await db.Orders.CountAsync(x => x.PaymentStatus != PaymentStatus.Failed && x.PaymentStatus != PaymentStatus.Pending);
        var failed = await db.Orders.CountAsync(x => x.PaymentStatus == PaymentStatus.Failed);
        Assert.True(paid > orders * 0.8, $"{paid} of {orders} orders were paid");
        Assert.InRange(failed, orders * 0.03, orders * 0.12);
        Assert.True(await db.PaymentAttempts.CountAsync(x => x.Status == PaymentAttemptStatus.Declined) > 0);
        Assert.True(await Scalar("SELECT COUNT(*) FROM payment.PaymentMethods") >= 1000);
        Assert.True(await Scalar("SELECT COUNT(*) FROM sales.OrderReturns") > 0);
        Assert.True(await Scalar("SELECT COUNT(*) FROM payment.Refunds") > 0);
    }

    [Theory]
    [InlineData("failed orders without a documented decline", "SELECT COUNT(*) FROM sales.Orders o WHERE o.PaymentStatusId = 2 AND NOT EXISTS (SELECT 1 FROM payment.PaymentAttempts a WHERE a.OrderId = o.Id AND a.DeclineReasonId IS NOT NULL)")]
    [InlineData("failed orders without a PAYMENT_FAILED history row", "SELECT COUNT(*) FROM sales.Orders o WHERE o.PaymentStatusId = 2 AND NOT EXISTS (SELECT 1 FROM sales.OrderStatusHistory h WHERE h.OrderId = o.Id AND h.StatusChangeReasonId = 3)")]
    [InlineData("paid orders without a captured attempt", "SELECT COUNT(*) FROM sales.Orders o WHERE o.PaymentStatusId IN (1, 3, 4) AND NOT EXISTS (SELECT 1 FROM payment.PaymentAttempts a WHERE a.OrderId = o.Id AND a.PaymentAttemptStatusId = 1)")]
    [InlineData("orders whose subtotal differs from their lines", "SELECT COUNT(*) FROM sales.Orders o WHERE o.Subtotal <> (SELECT SUM(i.LineTotal) FROM sales.OrderItems i WHERE i.OrderId = o.Id)")]
    [InlineData("orders whose latest history row differs from their status", "SELECT COUNT(*) FROM sales.Orders o CROSS APPLY (SELECT TOP (1) h.ToStatusId FROM sales.OrderStatusHistory h WHERE h.OrderId = o.Id ORDER BY h.ChangedAt DESC, h.Id DESC) l WHERE l.ToStatusId <> o.OrderStatusId")]
    [InlineData("captures refunded beyond their amount", "SELECT COUNT(*) FROM payment.PaymentAttempts a WHERE a.Amount < (SELECT ISNULL(SUM(r.Amount), 0) FROM payment.Refunds r WHERE r.PaymentAttemptId = a.Id)")]
    [InlineData("items returned beyond the ordered quantity", "SELECT COUNT(*) FROM sales.OrderItems i WHERE i.Quantity < (SELECT ISNULL(SUM(r.Quantity), 0) FROM sales.OrderReturnItems r WHERE r.OrderItemId = i.Id)")]
    [InlineData("payment methods without exactly one detail row", "SELECT COUNT(*) FROM payment.PaymentMethods pm WHERE (SELECT COUNT(*) FROM payment.CardDetails WHERE PaymentMethodId = pm.Id) + (SELECT COUNT(*) FROM payment.WalletDetails WHERE PaymentMethodId = pm.Id) + (SELECT COUNT(*) FROM payment.BankAccountDetails WHERE PaymentMethodId = pm.Id) <> 1")]
    [InlineData("discount codes used beyond their limit", "SELECT COUNT(*) FROM sales.DiscountCodes d WHERE d.UsageLimit < (SELECT COUNT(*) FROM sales.DiscountRedemptions r WHERE r.DiscountCodeId = d.Id)")]
    public async Task Seed_data_satisfies_cross_table_business_rules(string rule, string violationsQuery)
    {
        Assert.True(await Scalar(violationsQuery) == 0, $"Found {rule}.");
    }

    [Fact]
    public async Task Order_status_procedure_audits_changes_and_rejects_invalid_or_stale_ones()
    {
        var orderId = await Scalar("SELECT TOP (1) Id FROM sales.Orders WHERE OrderStatusId = 2 AND CustomerId IS NOT NULL ORDER BY Id DESC");
        await using var connection = new SqlConnection(factory.AppConnectionString);
        await connection.OpenAsync();
        Assert.Equal(3, await ChangeStatus(connection, orderId, expected: 2, next: 1)); // Shipped -> Processing is not a transition
        Assert.Equal(2, await ChangeStatus(connection, orderId, expected: 1, next: 2)); // stale: order is no longer Processing
        var history = await Scalar($"SELECT COUNT(*) FROM sales.OrderStatusHistory WHERE OrderId = {orderId}");
        Assert.Equal(0, await ChangeStatus(connection, orderId, expected: 2, next: 3));
        Assert.Equal(history + 1, await Scalar($"SELECT COUNT(*) FROM sales.OrderStatusHistory WHERE OrderId = {orderId}"));
        Assert.Equal(3, await Scalar($"SELECT CAST(OrderStatusId AS int) FROM sales.Orders WHERE Id = {orderId}"));
    }

    [Fact]
    public async Task Database_rejects_rows_that_break_integrity_rules()
    {
        // Total must equal Subtotal - DiscountTotal + ShippingTotal.
        await AssertRejected("UPDATE TOP (1) sales.Orders SET Total = Total + 1");
        // A declined attempt must document its reason.
        await AssertRejected("UPDATE TOP (1) payment.PaymentAttempts SET DeclineReasonId = NULL WHERE PaymentAttemptStatusId = 2");
        // Shipped orders must be paid.
        await AssertRejected("UPDATE TOP (1) sales.Orders SET PaymentStatusId = 2 WHERE OrderStatusId = 2");
        // History can't record a transition the policy doesn't allow.
        await AssertRejected("INSERT sales.OrderStatusHistory (OrderId, FromStatusId, ToStatusId) SELECT TOP (1) Id, 3, 0 FROM sales.Orders");
    }

    private async Task AssertRejected(string sql)
    {
        await using var connection = new SqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Contains(error.Number, new[] { 547 }); // constraint violation
    }

    private static async Task<int> ChangeStatus(SqlConnection connection, int orderId, byte expected, byte next)
    {
        await using var command = new SqlCommand("sales.usp_ChangeOrderStatus", connection) { CommandType = System.Data.CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@OrderId", orderId);
        command.Parameters.AddWithValue("@ExpectedStatusId", expected);
        command.Parameters.AddWithValue("@NewStatusId", next);
        var result = command.Parameters.Add("@Result", System.Data.SqlDbType.Int);
        result.Direction = System.Data.ParameterDirection.Output;
        await command.ExecuteNonQueryAsync();
        return (int)result.Value;
    }

    private async Task<int> Scalar(string sql)
    {
        await using var connection = new SqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
