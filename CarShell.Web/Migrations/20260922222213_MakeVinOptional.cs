using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarShell.Web.Migrations
{
    /// <inheritdoc />
    public partial class MakeVinOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Vin",
                table: "Listings",
                type: "character varying(17)",
                maxLength: 17,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(17)",
                oldMaxLength: 17);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Vin",
                table: "Listings",
                type: "character varying(17)",
                maxLength: 17,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(17)",
                oldMaxLength: 17,
                oldNullable: true);
        }
    }
}
