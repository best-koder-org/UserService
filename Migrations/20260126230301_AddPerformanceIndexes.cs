using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Active_LastActive",
                table: "UserProfiles",
                columns: new[] { "IsActive", "LastActiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_City",
                table: "UserProfiles",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Country",
                table: "UserProfiles",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_DateOfBirth",
                table: "UserProfiles",
                column: "DateOfBirth");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Email",
                table: "UserProfiles",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Gender",
                table: "UserProfiles",
                column: "Gender");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_IsActive",
                table: "UserProfiles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_IsOnline",
                table: "UserProfiles",
                column: "IsOnline");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_IsVerified",
                table: "UserProfiles",
                column: "IsVerified");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_LastActiveAt",
                table: "UserProfiles",
                column: "LastActiveAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Location",
                table: "UserProfiles",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Search_Common",
                table: "UserProfiles",
                columns: new[] { "IsActive", "Gender", "DateOfBirth" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_State",
                table: "UserProfiles",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_UserId",
                table: "UserProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchPreferences_UserId",
                table: "MatchPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfile_Active_LastActive",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_City",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_Country",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_DateOfBirth",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_Email",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_Gender",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_IsActive",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_IsOnline",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_IsVerified",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_LastActiveAt",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_Location",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_Search_Common",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_State",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfile_UserId",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MatchPreferences_UserId",
                table: "MatchPreferences");
        }
    }
}
