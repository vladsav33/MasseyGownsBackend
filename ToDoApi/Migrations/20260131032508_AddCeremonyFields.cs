using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GownApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCeremonyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ceremony_date2",
                table: "ceremonies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cerenony_no",
                table: "ceremonies",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ceremony_date2",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "cerenony_no",
                table: "ceremonies");
        }
    }
}
