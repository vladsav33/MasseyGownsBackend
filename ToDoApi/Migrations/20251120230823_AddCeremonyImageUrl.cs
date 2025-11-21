using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GownApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCeremonyImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ceremony_text",
                table: "home_page_settings",
                type: "text",
                nullable: true);



            migrationBuilder.AddColumn<string>(
                name: "ceremony_image_url",
                table: "home_page_settings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.DropColumn(
                name: "ceremony_text",
                table: "home_page_settings");



            migrationBuilder.DropColumn(
                name: "ceremony_image_url",
                table: "home_page_settings");


        }
    }
}
