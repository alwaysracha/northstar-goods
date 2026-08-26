using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EcommerceApp.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260825223000_Milestone3Administration")]
public sealed class Milestone3Administration : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateIndex("IX_Orders_CustomerId_CreatedAt", "Orders", ["CustomerId", "CreatedAt"]);
        m.CreateIndex("IX_Orders_Status_CreatedAt", "Orders", ["Status", "CreatedAt"]);
        m.AddCheckConstraint("CK_DiscountCode_Value", "DiscountCodes", "\"Value\" > 0");
        m.AddCheckConstraint("CK_DiscountCode_Window", "DiscountCodes", "\"EndsAt\" > \"StartsAt\"");
        m.AddCheckConstraint("CK_DiscountCode_UsageLimit", "DiscountCodes", "\"UsageLimit\" IS NULL OR \"UsageLimit\" > 0");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropCheckConstraint("CK_DiscountCode_UsageLimit", "DiscountCodes");
        m.DropCheckConstraint("CK_DiscountCode_Window", "DiscountCodes");
        m.DropCheckConstraint("CK_DiscountCode_Value", "DiscountCodes");
        m.DropIndex("IX_Orders_Status_CreatedAt", "Orders");
        m.DropIndex("IX_Orders_CustomerId_CreatedAt", "Orders");
    }
}
