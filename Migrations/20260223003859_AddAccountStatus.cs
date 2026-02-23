using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountStatus",
                table: "UserProfiles",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PauseDuration",
                table: "UserProfiles",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PauseReason",
                table: "UserProfiles",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "PauseUntil",
                table: "UserProfiles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAt",
                table: "UserProfiles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_AccountStatus",
                table: "UserProfiles",
                column: "AccountStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfile_AccountStatus",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AccountStatus",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PauseDuration",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PauseReason",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PauseUntil",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PausedAt",
                table: "UserProfiles");
        }
    }
}
