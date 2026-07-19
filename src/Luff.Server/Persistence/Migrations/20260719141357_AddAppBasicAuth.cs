using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luff.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppBasicAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BasicAuthPassword",
                table: "Apps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BasicAuthUsername",
                table: "Apps",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasicAuthPassword",
                table: "Apps");

            migrationBuilder.DropColumn(
                name: "BasicAuthUsername",
                table: "Apps");
        }
    }
}
