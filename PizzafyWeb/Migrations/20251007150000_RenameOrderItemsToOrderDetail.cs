using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PizzafyWeb.Migrations
{
    public partial class RenameOrderItemsToOrderDetail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename table order_items -> order_detail
            migrationBuilder.RenameTable(
                name: "order_items",
                newName: "order_detail");

            // Rename primary key column
            migrationBuilder.RenameColumn(
                name: "order_item_id",
                table: "order_detail",
                newName: "order_detail_id");

            // Rename subtotal -> line_total
            migrationBuilder.RenameColumn(
                name: "subtotal",
                table: "order_detail",
                newName: "line_total");

            // Rename indexes if any were auto-created
            migrationBuilder.RenameIndex(
                name: "IX_order_items_order_id",
                table: "order_detail",
                newName: "IX_order_detail_order_id");

            migrationBuilder.RenameIndex(
                name: "IX_order_items_price_id",
                table: "order_detail",
                newName: "IX_order_detail_price_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert index names
            migrationBuilder.RenameIndex(
                name: "IX_order_detail_order_id",
                table: "order_detail",
                newName: "IX_order_items_order_id");

            migrationBuilder.RenameIndex(
                name: "IX_order_detail_price_id",
                table: "order_detail",
                newName: "IX_order_items_price_id");

            // Revert column names
            migrationBuilder.RenameColumn(
                name: "order_detail_id",
                table: "order_detail",
                newName: "order_item_id");

            migrationBuilder.RenameColumn(
                name: "line_total",
                table: "order_detail",
                newName: "subtotal");

            // Revert table name
            migrationBuilder.RenameTable(
                name: "order_detail",
                newName: "order_items");
        }
    }
}
