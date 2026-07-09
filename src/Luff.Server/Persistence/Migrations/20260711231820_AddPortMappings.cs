using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luff.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortMappings",
                columns: table => new
                {
                    AppName = table.Column<string>(type: "TEXT", nullable: false),
                    HostPort = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerPort = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortMappings", x => new { x.AppName, x.HostPort });
                    table.ForeignKey(
                        name: "FK_PortMappings_Apps_AppName",
                        column: x => x.AppName,
                        principalTable: "Apps",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortMappings");
        }
    }
}
