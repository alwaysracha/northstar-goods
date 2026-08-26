using Microsoft.EntityFrameworkCore.Migrations;

namespace EcommerceApp.Web.Data.Migrations;

public partial class CurrentModelBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex("IX_Addresses_UserId", "Addresses", "UserId");
        migrationBuilder.CreateIndex("IX_CartItems_ProductId", "CartItems", "ProductId");
        migrationBuilder.CreateIndex("IX_DiscountRedemptions_DiscountCodeId", "DiscountRedemptions", "DiscountCodeId");
        migrationBuilder.CreateIndex("IX_DiscountRedemptions_OrderId", "DiscountRedemptions", "OrderId");
        migrationBuilder.CreateIndex("IX_OrderItems_OrderId", "OrderItems", "OrderId");
        migrationBuilder.CreateIndex("IX_OrderItems_ProductId", "OrderItems", "ProductId");
        migrationBuilder.CreateIndex("IX_Products_CategoryId", "Products", "CategoryId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Addresses_UserId", "Addresses");
        migrationBuilder.DropIndex("IX_CartItems_ProductId", "CartItems");
        migrationBuilder.DropIndex("IX_DiscountRedemptions_DiscountCodeId", "DiscountRedemptions");
        migrationBuilder.DropIndex("IX_DiscountRedemptions_OrderId", "DiscountRedemptions");
        migrationBuilder.DropIndex("IX_OrderItems_OrderId", "OrderItems");
        migrationBuilder.DropIndex("IX_OrderItems_ProductId", "OrderItems");
        migrationBuilder.DropIndex("IX_Products_CategoryId", "Products");
    }
}
