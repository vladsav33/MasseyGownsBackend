using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GownApi.Migrations
{
    /// <inheritdoc />
    public partial class Add3CeremonyPostalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "postal_address2",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "postal_address3",
                table: "ceremonies",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "postal_address2",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "postal_address3",
                table: "ceremonies");
        }
    }
}
