using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GownApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCountSku : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "count",
                table: "sku",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "count",
                table: "sku");
        }
    }
}
