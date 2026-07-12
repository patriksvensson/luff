using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Luff.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentLinkAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentLinkAddress",
                table: "ServerSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentLinkAddress",
                table: "ServerSettings");
        }
    }
}
