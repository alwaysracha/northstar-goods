using EcommerceApp.Web.Domain.Entities;
using EcommerceApp.Web.Services.Orders;

namespace EcommerceApp.UnitTests;

public sealed class OrderStatusPolicyTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Processing)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Processing, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public void Allows_only_explicit_forward_transitions(OrderStatus current, OrderStatus next)
        => Assert.True(OrderStatusPolicy.CanTransition(current, next));

    [Fact]
    public void Rejects_undefined_status_values_without_throwing()
    {
        Assert.False(OrderStatusPolicy.CanTransition((OrderStatus)999, OrderStatus.Processing));
        Assert.Empty(OrderStatusPolicy.Next((OrderStatus)999));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Processing)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Processing)]
    [InlineData(OrderStatus.Processing, OrderStatus.Processing)]
    public void Rejects_skips_backwards_terminal_and_noop_transitions(OrderStatus current, OrderStatus next)
        => Assert.False(OrderStatusPolicy.CanTransition(current, next));
}
