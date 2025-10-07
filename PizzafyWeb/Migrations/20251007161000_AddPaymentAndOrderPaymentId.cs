using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PizzafyWeb.Migrations
{
    public partial class AddPaymentAndOrderPaymentId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment",
                columns: table => new
                {
                    payment_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payment_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment", x => x.payment_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "payment_id",
                table: "orders",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_orders_payment_id",
                table: "orders",
                column: "payment_id");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_payment_payment_id",
                table: "orders",
                column: "payment_id",
                principalTable: "payment",
                principalColumn: "payment_id",
                onDelete: ReferentialAction.Restrict);

            // Seed data
            migrationBuilder.InsertData(
                table: "payment",
                columns: new[] { "payment_id", "payment_name" },
                values: new object[] { 1, "Cash on Delivery" });

            migrationBuilder.InsertData(
                table: "payment",
                columns: new[] { "payment_id", "payment_name" },
                values: new object[] { 2, "GCash" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_payment_payment_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_payment_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_id",
                table: "orders");

            migrationBuilder.DropTable(
                name: "payment");
        }
    }
}
