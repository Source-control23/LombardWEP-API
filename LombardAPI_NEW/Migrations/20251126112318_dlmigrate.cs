using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LombardAPI_NEW.Migrations
{
    /// <inheritdoc />
    public partial class dlmigrate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DLDetails",
                columns: table => new
                {
                    DLID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Event = table.Column<string>(type: "text", nullable: false),
                    DLName = table.Column<string>(type: "text", nullable: false),
                    DLOWner = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EmpIds = table.Column<string>(type: "text", nullable: false),
                    Rights = table.Column<string>(type: "text", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "text", nullable: false),
                    ApproverEmail = table.Column<string>(type: "text", nullable: true),
                    ApprovalTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DLDetails", x => x.DLID);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RollID = table.Column<int>(type: "integer", nullable: true),
                    Deptid = table.Column<int>(type: "integer", nullable: true),
                    FullName = table.Column<string>(type: "text", nullable: true),
                    Gender = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Line1 = table.Column<string>(type: "text", nullable: true),
                    Line2 = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<string>(type: "text", nullable: true),
                    Twitter = table.Column<string>(type: "text", nullable: true),
                    MobileNo = table.Column<string>(type: "text", nullable: true),
                    MaritalStatus = table.Column<string>(type: "text", nullable: true),
                    UserName = table.Column<string>(type: "text", nullable: true),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: true),
                    Zipcode = table.Column<string>(type: "text", nullable: true),
                    Active = table.Column<int>(type: "integer", nullable: true),
                    UserActive = table.Column<bool>(type: "boolean", nullable: true),
                    department = table.Column<string>(type: "text", nullable: true),
                    designation = table.Column<string>(type: "text", nullable: true),
                    office = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true),
                    user_type = table.Column<int>(type: "integer", nullable: true),
                    UserToken = table.Column<string>(type: "text", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DLDetails");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
