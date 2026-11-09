using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatabaseBroker.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "auth",
                table: "permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "module_name",
                schema: "auth",
                table: "permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_name",
                schema: "auth",
                table: "permissions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                schema: "auth",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "module_name",
                schema: "auth",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "service_name",
                schema: "auth",
                table: "permissions");
        }
    }
}
