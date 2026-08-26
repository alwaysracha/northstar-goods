using EcommerceApp.Web.Domain.Entities;

namespace EcommerceApp.Web.Services.Orders;

public static class OrderStatusPolicy
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Allowed =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = [OrderStatus.Processing, OrderStatus.Cancelled],
            [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
            [OrderStatus.Shipped] = [OrderStatus.Delivered],
            [OrderStatus.Delivered] = [],
            [OrderStatus.Cancelled] = []
        };

    public static bool CanTransition(OrderStatus current, OrderStatus next)
        => Allowed.TryGetValue(current, out var nextStatuses) && nextStatuses.Contains(next);

    public static IReadOnlyList<OrderStatus> Next(OrderStatus current)
        => Allowed.TryGetValue(current, out var nextStatuses) ? nextStatuses : [];
}
