using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SharePassword.Data.Migrations.SqlServer;

[DbContext(typeof(SqlServerSharePasswordDbContext))]
[Migration(MigrationId)]
public partial class InitialStorageSqlServer : Migration
{
    public const string MigrationId = "20260419000200_InitialStorageSqlServer";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ActorIdentifier = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Operation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                TargetType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                TargetId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                Success = table.Column<bool>(type: "bit", nullable: false),
                IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                UserAgent = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                Details = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PasswordShares",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                SharedUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                EncryptedPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Instructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                AccessCodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                AccessToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastAccessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                RequireOidcLogin = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PasswordShares", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_Operation",
            table: "AuditLogs",
            column: "Operation");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_TimestampUtc",
            table: "AuditLogs",
            column: "TimestampUtc");

        migrationBuilder.CreateIndex(
            name: "IX_PasswordShares_AccessToken",
            table: "PasswordShares",
            column: "AccessToken",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PasswordShares_CreatedBy",
            table: "PasswordShares",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_PasswordShares_ExpiresAtUtc",
            table: "PasswordShares",
            column: "ExpiresAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuditLogs");

        migrationBuilder.DropTable(
            name: "PasswordShares");
    }
}