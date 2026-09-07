using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HostDeck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServerSystemIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hostname",
                table: "servers",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KernelVersion",
                table: "servers",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingSystem",
                table: "servers",
                type: "TEXT",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hostname",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "KernelVersion",
                table: "servers");

            migrationBuilder.DropColumn(
                name: "OperatingSystem",
                table: "servers");
        }
    }
}
