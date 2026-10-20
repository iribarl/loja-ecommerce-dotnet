using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loja.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_reservations",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_reservations_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_ExpiresAt",
                schema: "catalog",
                table: "stock_reservations",
                column: "ExpiresAt",
                filter: "\"Status\" = 'Reserved'");

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_OrderId_VariantId",
                schema: "catalog",
                table: "stock_reservations",
                columns: new[] { "OrderId", "VariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservations_VariantId",
                schema: "catalog",
                table: "stock_reservations",
                column: "VariantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_reservations",
                schema: "catalog");
        }
    }
}
