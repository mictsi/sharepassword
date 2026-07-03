using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SharePassword.Data.Migrations.Sqlite;

[DbContext(typeof(SqliteSharePasswordDbContext))]
[Migration(MigrationId)]
public partial class InformationRequestsSqlite : Migration
{
    public const string MigrationId = "20260703000100_InformationRequestsSqlite";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InformationRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PartnerEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                RequestInstructions = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: false),
                EncryptedPartnerResponse = table.Column<string>(type: "TEXT", nullable: false),
                ResponseEncryptionMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                AccessCodeHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                AccessToken = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastSubmittedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                RequireOidcLogin = table.Column<bool>(type: "INTEGER", nullable: false),
                FailedAccessAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                AccessPausedUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InformationRequests", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InformationRequests_AccessToken",
            table: "InformationRequests",
            column: "AccessToken",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_InformationRequests_CreatedBy",
            table: "InformationRequests",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_InformationRequests_ExpiresAtUtc",
            table: "InformationRequests",
            column: "ExpiresAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InformationRequests");
    }
}
