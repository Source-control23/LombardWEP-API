using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LombardAPI_NEW.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestorToDLDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestorId",
                table: "DLDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestorName",
                table: "DLDetails",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestorId",
                table: "DLDetails");

            migrationBuilder.DropColumn(
                name: "RequestorName",
                table: "DLDetails");
        }
    }
}
