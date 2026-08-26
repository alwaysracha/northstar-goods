using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceApp.Web.Data.Migrations;

public partial class AlignConstraintNames : Migration
{
    private static readonly (string Table, string PostgreSqlName, string EfName)[] Constraints =
    [
        ("Addresses", "Addresses_pkey", "PK_Addresses"),
        ("AspNetRoleClaims", "AspNetRoleClaims_pkey", "PK_AspNetRoleClaims"),
        ("AspNetRoles", "AspNetRoles_pkey", "PK_AspNetRoles"),
        ("AspNetUserClaims", "AspNetUserClaims_pkey", "PK_AspNetUserClaims"),
        ("AspNetUserLogins", "AspNetUserLogins_pkey", "PK_AspNetUserLogins"),
        ("AspNetUserRoles", "AspNetUserRoles_pkey", "PK_AspNetUserRoles"),
        ("AspNetUsers", "AspNetUsers_pkey", "PK_AspNetUsers"),
        ("AspNetUserTokens", "AspNetUserTokens_pkey", "PK_AspNetUserTokens"),
        ("CartItems", "CartItems_pkey", "PK_CartItems"),
        ("Carts", "Carts_pkey", "PK_Carts"),
        ("Categories", "Categories_pkey", "PK_Categories"),
        ("DiscountCodes", "DiscountCodes_pkey", "PK_DiscountCodes"),
        ("DiscountRedemptions", "DiscountRedemptions_pkey", "PK_DiscountRedemptions"),
        ("OrderItems", "OrderItems_pkey", "PK_OrderItems"),
        ("Orders", "Orders_pkey", "PK_Orders"),
        ("Products", "Products_pkey", "PK_Products"),
        ("Addresses", "Addresses_UserId_fkey", "FK_Addresses_AspNetUsers_UserId"),
        ("AspNetRoleClaims", "AspNetRoleClaims_RoleId_fkey", "FK_AspNetRoleClaims_AspNetRoles_RoleId"),
        ("AspNetUserClaims", "AspNetUserClaims_UserId_fkey", "FK_AspNetUserClaims_AspNetUsers_UserId"),
        ("AspNetUserLogins", "AspNetUserLogins_UserId_fkey", "FK_AspNetUserLogins_AspNetUsers_UserId"),
        ("AspNetUserRoles", "AspNetUserRoles_RoleId_fkey", "FK_AspNetUserRoles_AspNetRoles_RoleId"),
        ("AspNetUserRoles", "AspNetUserRoles_UserId_fkey", "FK_AspNetUserRoles_AspNetUsers_UserId"),
        ("AspNetUserTokens", "AspNetUserTokens_UserId_fkey", "FK_AspNetUserTokens_AspNetUsers_UserId"),
        ("CartItems", "CartItems_CartId_fkey", "FK_CartItems_Carts_CartId"),
        ("CartItems", "CartItems_ProductId_fkey", "FK_CartItems_Products_ProductId"),
        ("Carts", "Carts_UserId_fkey", "FK_Carts_AspNetUsers_UserId"),
        ("DiscountRedemptions", "DiscountRedemptions_DiscountCodeId_fkey", "FK_DiscountRedemptions_DiscountCodes_DiscountCodeId"),
        ("DiscountRedemptions", "DiscountRedemptions_OrderId_fkey", "FK_DiscountRedemptions_Orders_OrderId"),
        ("OrderItems", "OrderItems_OrderId_fkey", "FK_OrderItems_Orders_OrderId"),
        ("OrderItems", "OrderItems_ProductId_fkey", "FK_OrderItems_Products_ProductId"),
        ("Orders", "Orders_CustomerId_fkey", "FK_Orders_AspNetUsers_CustomerId"),
        ("Products", "Products_CategoryId_fkey", "FK_Products_Categories_CategoryId"),
        ("CartItems", "CartItems_Quantity_check", "CK_CartItem_Quantity"),
        ("OrderItems", "OrderItems_Quantity_check", "CK_OrderItem_Quantity"),
        ("Products", "Products_Price_check", "CK_Product_Price"),
        ("Products", "Products_StockQuantity_check", "CK_Product_Stock")
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, postgreSqlName, efName) in Constraints)
        {
            migrationBuilder.Sql($"ALTER TABLE \"{table}\" RENAME CONSTRAINT \"{postgreSqlName}\" TO \"{efName}\";");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var (table, postgreSqlName, efName) in Constraints.Reverse())
        {
            migrationBuilder.Sql($"ALTER TABLE \"{table}\" RENAME CONSTRAINT \"{efName}\" TO \"{postgreSqlName}\";");
        }
    }
}
