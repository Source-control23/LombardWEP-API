using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LombardAPI_NEW.Migrations
{
    /// <inheritdoc />
    public partial class updatemi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalID",
                table: "DLDetails",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalID",
                table: "DLDetails");
        }
    }
}
