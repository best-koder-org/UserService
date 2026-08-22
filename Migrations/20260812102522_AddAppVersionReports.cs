using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class AddAppVersionReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent column-add: some databases already have ReadReceiptsEnabled
            // (updated out-of-band). MySQL 8 lacks ADD COLUMN IF NOT EXISTS, so guard
            // via information_schema. Safe on fresh DBs (column missing → it is added).
            migrationBuilder.Sql(@"
                SET @rr := (SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UserProfiles' AND COLUMN_NAME = 'ReadReceiptsEnabled');
                SET @ddl := IF(@rr = 0,
                    'ALTER TABLE UserProfiles ADD COLUMN ReadReceiptsEnabled TINYINT(1) NOT NULL DEFAULT FALSE',
                    'SELECT 1');
                PREPARE s FROM @ddl; EXECUTE s; DEALLOCATE PREPARE s;");

            migrationBuilder.AddColumn<string>(
                name: "WeakestAxesJson",
                table: "PsykologSessions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AppVersionReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    KeycloakId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VersionName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VersionCode = table.Column<int>(type: "int", nullable: false),
                    Platform = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviceModel = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReportedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppVersionReports", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReflectionVectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    KeycloakId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VectorJson = table.Column<string>(type: "mediumtext", maxLength: 8192, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SessionCount = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<double>(type: "double", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReflectionVectors", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AppVersionReport_KeycloakId",
                table: "AppVersionReports",
                column: "KeycloakId");

            migrationBuilder.CreateIndex(
                name: "IX_ReflectionVector_KeycloakId_Unique",
                table: "ReflectionVectors",
                column: "KeycloakId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppVersionReports");

            migrationBuilder.DropTable(
                name: "ReflectionVectors");

            migrationBuilder.DropColumn(
                name: "ReadReceiptsEnabled",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "WeakestAxesJson",
                table: "PsykologSessions");
        }
    }
}
