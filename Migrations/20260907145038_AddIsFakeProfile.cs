using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFakeProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL 8 lacks ALTER TABLE ADD COLUMN IF NOT EXISTS. Guard with
            // information_schema so re-applying this migration on an already-migrated
            // database is a no-op (otherwise the app crashes on startup).
            migrationBuilder.Sql(@"
                SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UserProfiles' AND COLUMN_NAME = 'IsFakeProfile');
                SET @ddl := IF(@col = 0,
                    'ALTER TABLE UserProfiles ADD COLUMN IsFakeProfile TINYINT(1) NOT NULL DEFAULT 0',
                    'SELECT 1');
                PREPARE s FROM @ddl; EXECUTE s; DEALLOCATE PREPARE s;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFakeProfile",
                table: "UserProfiles");
        }
    }
}
