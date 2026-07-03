using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SharePassword.Data.Migrations.Postgresql;

[DbContext(typeof(PostgresqlSharePasswordDbContext))]
[Migration(MigrationId)]
public partial class InformationRequestsPostgresql : Migration
{
    public const string MigrationId = "20260703000300_InformationRequestsPostgresql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InformationRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PartnerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                RequestInstructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                EncryptedPartnerResponse = table.Column<string>(type: "text", nullable: false),
                ResponseEncryptionMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AccessCodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                AccessToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastSubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                RequireOidcLogin = table.Column<bool>(type: "boolean", nullable: false),
                FailedAccessAttempts = table.Column<int>(type: "integer", nullable: false),
                AccessPausedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
