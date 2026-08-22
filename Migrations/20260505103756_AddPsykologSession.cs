using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// NEUTRALIZED 2026-08-20: this migration was a backdated duplicate. Its changes
    /// (IsBot on UserProfiles, PsykologSessions + PsykologMessages tables) are already
    /// created by 20260323120537_AddIsBotColumn and 20260623150843_AddPsykologEntities.
    /// Because its timestamp (2026-05-05) is older than the applied 2026-06-23 migration,
    /// EF tried to run it on every startup and crashed with "Duplicate column name 'IsBot'".
    /// It is recorded as applied in __EFMigrationsHistory but performs no work.
    /// </remarks>
    public partial class AddPsykologSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op — see class remarks.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op — see class remarks.
        }
    }
}
