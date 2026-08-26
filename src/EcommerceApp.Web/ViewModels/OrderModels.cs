using EcommerceApp.Web.Domain.Entities;

namespace EcommerceApp.Web.ViewModels;

public sealed record OrderListItemModel(string OrderNumber, DateTimeOffset CreatedAt, OrderStatus Status, decimal Total, int ItemCount);
public sealed record OrderItemModel(string Sku, string ProductName, decimal UnitPrice, int Quantity);
public sealed record OrderDetailModel(string OrderNumber, DateTimeOffset CreatedAt, OrderStatus Status, PaymentStatus PaymentStatus,
    string ContactEmail, string RecipientName, string ShippingAddress, decimal Subtotal, decimal DiscountTotal,
    decimal ShippingTotal, decimal Total, IReadOnlyList<OrderItemModel> Items);
