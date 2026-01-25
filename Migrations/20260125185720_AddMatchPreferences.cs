using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserProfileId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MinAge = table.Column<int>(type: "int", nullable: false),
                    MaxAge = table.Column<int>(type: "int", nullable: false),
                    MaxDistanceKm = table.Column<int>(type: "int", nullable: false),
                    PreferredGender = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelationshipGoals = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DealBreakerSmoking = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DealBreakerDrinking = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DealBreakerHasChildren = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DealBreakerWantsChildren = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequireSameReligion = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PreferSimilarEducation = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ShowMeInDiscovery = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EnableDailyLimit = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MinHeightCm = table.Column<int>(type: "int", nullable: true),
                    MaxHeightCm = table.Column<int>(type: "int", nullable: true),
                    PreferredEthnicities = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MustHaveInterests = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchPreferences_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPreferences_UserProfileId",
                table: "MatchPreferences",
                column: "UserProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchPreferences");
        }
    }
}
