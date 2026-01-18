using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GownApi.Migrations
{
    /// <inheritdoc />
    public partial class AddGownDbChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "home_page_settings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_hoods",
                table: "hoods");

            migrationBuilder.RenameTable(
                name: "hoods",
                newName: "hood_type");

            migrationBuilder.RenameColumn(
                name: "label_size",
                table: "sizes",
                newName: "labelsize");

            migrationBuilder.AddColumn<int>(
                name: "fit_id",
                table: "sizes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "item_id",
                table: "sizes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "price",
                table: "sizes",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "item_id",
                table: "selected_item_out",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "size_id",
                table: "selected_item_out",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "paid",
                table: "orders",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<float>(
                name: "admin_charges",
                table: "orders",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "amount_owning",
                table: "orders",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "amount_paid",
                table: "orders",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "changes",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "donation",
                table: "orders",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "freight",
                table: "orders",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "order_type",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pack_note",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "pay_by",
                table: "orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_no",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "refund",
                table: "orders",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "active",
                table: "item_degree_models",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "degree_order",
                table: "item_degree_models",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "active",
                table: "item_degree_dtos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ceremony_date",
                table: "degrees_ceremonies",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<bool>(
                name: "active",
                table: "degrees_ceremonies",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "degree_id",
                table: "degrees_ceremonies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ceremony_date",
                table: "ceremonies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "collection_time",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "courier_address",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_returned",
                table: "ceremonies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_sent",
                table: "ceremonies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "despatch_date",
                table: "ceremonies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "freight",
                table: "ceremonies",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "id_code",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "institution_name",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_email",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "organiser",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "postal_address",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "price_code",
                table: "ceremonies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "return_date",
                table: "ceremonies",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "item_id",
                table: "hood_type",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "pk_hood_type",
                table: "hood_type",
                column: "id");

            migrationBuilder.CreateTable(
                name: "bulk_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    first_name = table.Column<string>(type: "text", nullable: true),
                    head_size = table.Column<float>(type: "real", nullable: true),
                    height = table.Column<float>(type: "real", nullable: true),
                    hat_type = table.Column<string>(type: "text", nullable: true),
                    gown_type = table.Column<string>(type: "text", nullable: true),
                    hood_type = table.Column<string>(type: "text", nullable: true),
                    ucol_sash = table.Column<string>(type: "text", nullable: true),
                    ceremony_id = table.Column<int>(type: "integer", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bulk_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ceremony_degree",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    graduation_id = table.Column<int>(type: "integer", nullable: false),
                    degree_id = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ceremony_degree", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ceremony_degree_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ceremony_degree_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ceremony_degree_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cms_content_blocks",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    page = table.Column<string>(type: "text", nullable: false),
                    section = table.Column<string>(type: "text", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_content_blocks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "count_bulk_dto",
                columns: table => new
                {
                    hat_count = table.Column<int>(type: "integer", nullable: false),
                    hood_count = table.Column<int>(type: "integer", nullable: false),
                    gown_count = table.Column<int>(type: "integer", nullable: false),
                    ucol_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "email_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    subject_template = table.Column<string>(type: "text", nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: false),
                    tax_receipt_html = table.Column<string>(type: "text", nullable: false),
                    collection_details_html = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bulk_orders");

            migrationBuilder.DropTable(
                name: "ceremony_degree");

            migrationBuilder.DropTable(
                name: "ceremony_degree_items");

            migrationBuilder.DropTable(
                name: "cms_content_blocks");

            migrationBuilder.DropTable(
                name: "count_bulk_dto");

            migrationBuilder.DropTable(
                name: "email_templates");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropPrimaryKey(
                name: "pk_hood_type",
                table: "hood_type");

            migrationBuilder.DropColumn(
                name: "fit_id",
                table: "sizes");

            migrationBuilder.DropColumn(
                name: "item_id",
                table: "sizes");

            migrationBuilder.DropColumn(
                name: "price",
                table: "sizes");

            migrationBuilder.DropColumn(
                name: "item_id",
                table: "selected_item_out");

            migrationBuilder.DropColumn(
                name: "size_id",
                table: "selected_item_out");

            migrationBuilder.DropColumn(
                name: "admin_charges",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "amount_owning",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "amount_paid",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "changes",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "donation",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "freight",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "note",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "order_type",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "pack_note",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "pay_by",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "reference_no",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "refund",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "status",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "active",
                table: "item_degree_models");

            migrationBuilder.DropColumn(
                name: "degree_order",
                table: "item_degree_models");

            migrationBuilder.DropColumn(
                name: "active",
                table: "item_degree_dtos");

            migrationBuilder.DropColumn(
                name: "active",
                table: "degrees_ceremonies");

            migrationBuilder.DropColumn(
                name: "degree_id",
                table: "degrees_ceremonies");

            migrationBuilder.DropColumn(
                name: "ceremony_date",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "city",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "collection_time",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "courier_address",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "date_returned",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "date_sent",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "despatch_date",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "email",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "freight",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "id_code",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "institution_name",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "invoice_email",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "organiser",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "postal_address",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "price_code",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "return_date",
                table: "ceremonies");

            migrationBuilder.DropColumn(
                name: "item_id",
                table: "hood_type");

            migrationBuilder.RenameTable(
                name: "hood_type",
                newName: "hoods");

            migrationBuilder.RenameColumn(
                name: "labelsize",
                table: "sizes",
                newName: "label_size");

            migrationBuilder.AlterColumn<bool>(
                name: "paid",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ceremony_date",
                table: "degrees_ceremonies",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_hoods",
                table: "hoods",
                column: "id");

            migrationBuilder.CreateTable(
                name: "home_page_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ceremony_image_url = table.Column<string>(type: "text", nullable: true),
                    hero_image_url = table.Column<string>(type: "text", nullable: true),
                    update_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_home_page_settings", x => x.id);
                });
        }
    }
}
