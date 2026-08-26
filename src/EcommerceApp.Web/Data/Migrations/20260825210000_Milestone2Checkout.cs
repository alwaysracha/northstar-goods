using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EcommerceApp.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260825210000_Milestone2Checkout")]
public sealed class Milestone2Checkout : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>("DiscountCode", "Carts", type: "character varying(64)", maxLength: 64, nullable: true);
        m.AddColumn<string>("NormalizedCode", "DiscountCodes", type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "");
        m.Sql("UPDATE \"DiscountCodes\" SET \"NormalizedCode\" = upper(trim(\"Code\"));");
        m.DropIndex("IX_DiscountCodes_Code", "DiscountCodes");
        m.CreateIndex("IX_DiscountCodes_NormalizedCode", "DiscountCodes", "NormalizedCode", unique: true);
        m.AddColumn<string>("CheckoutToken", "Orders", type: "text", nullable: false, defaultValue: "");
        m.AddColumn<string>("ConfirmationToken", "Orders", type: "text", nullable: false, defaultValue: "");
        m.Sql("UPDATE \"Orders\" SET \"CheckoutToken\" = 'migrated-checkout-' || \"Id\", \"ConfirmationToken\" = 'migrated-confirmation-' || \"Id\";");
        m.CreateIndex("IX_Orders_CheckoutToken", "Orders", "CheckoutToken", unique: true);
        m.CreateIndex("IX_Orders_ConfirmationToken", "Orders", "ConfirmationToken", unique: true);
        m.CreateIndex("IX_Carts_UserId", "Carts", "UserId", unique: true);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropIndex("IX_Carts_UserId", "Carts"); m.DropIndex("IX_Orders_CheckoutToken", "Orders"); m.DropIndex("IX_Orders_ConfirmationToken", "Orders");
        m.DropColumn("DiscountCode", "Carts"); m.DropColumn("CheckoutToken", "Orders"); m.DropColumn("ConfirmationToken", "Orders");
        m.DropIndex("IX_DiscountCodes_NormalizedCode", "DiscountCodes"); m.DropColumn("NormalizedCode", "DiscountCodes"); m.CreateIndex("IX_DiscountCodes_Code", "DiscountCodes", "Code", unique: true);
    }
}
