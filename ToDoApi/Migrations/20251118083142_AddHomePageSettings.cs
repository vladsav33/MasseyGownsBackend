using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GownApi.Migrations
{
    /// <inheritdoc />
    public partial class AddHomePageSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
       name: "home_page_settings",
       columns: table => new
       {
           id = table.Column<int>(type: "integer", nullable: false)
               .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
           hero_image_url = table.Column<string>(type: "text", nullable: true),
           update_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
       },
       constraints: table =>
       {
           table.PrimaryKey("pk_home_page_settings", x => x.id);
       });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
       name: "home_page_settings");
        }
    }
}
