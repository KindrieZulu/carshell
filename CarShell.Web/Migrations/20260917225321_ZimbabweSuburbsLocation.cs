using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CarShell.Web.Migrations
{
    /// <inheritdoc />
    public partial class ZimbabweSuburbsLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostcodeGeocodes");

            migrationBuilder.DropColumn(
                name: "Postcode",
                table: "Listings");

            migrationBuilder.AddColumn<int>(
                name: "SuburbId",
                table: "Listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Suburbs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    City = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: false),
                    Lng = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suburbs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_SuburbId",
                table: "Listings",
                column: "SuburbId");

            migrationBuilder.CreateIndex(
                name: "IX_Suburbs_City_Name",
                table: "Suburbs",
                columns: new[] { "City", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_Suburbs_SuburbId",
                table: "Listings",
                column: "SuburbId",
                principalTable: "Suburbs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Listings_Suburbs_SuburbId",
                table: "Listings");

            migrationBuilder.DropTable(
                name: "Suburbs");

            migrationBuilder.DropIndex(
                name: "IX_Listings_SuburbId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "SuburbId",
                table: "Listings");

            migrationBuilder.AddColumn<string>(
                name: "Postcode",
                table: "Listings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "PostcodeGeocodes",
                columns: table => new
                {
                    Postcode = table.Column<string>(type: "text", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: false),
                    Lng = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostcodeGeocodes", x => x.Postcode);
                });
        }
    }
}
