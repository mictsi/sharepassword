using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sekura.Data.Migrations.Postgresql;

[DbContext(typeof(PostgresqlSekuraDbContext))]
[Migration(MigrationId)]
public partial class RenameSecureSharesPostgresql : Migration
{
    public const string MigrationId = "20260717000300_RenameSecureSharesPostgresql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(
            name: "PasswordShares",
            newName: "SecureShares");

        migrationBuilder.Sql("ALTER TABLE \"SecureShares\" RENAME CONSTRAINT \"PK_PasswordShares\" TO \"PK_SecureShares\";");

        migrationBuilder.RenameIndex(
            name: "IX_PasswordShares_AccessToken",
            newName: "IX_SecureShares_AccessToken",
            table: "SecureShares");

        migrationBuilder.RenameIndex(
            name: "IX_PasswordShares_CreatedBy",
            newName: "IX_SecureShares_CreatedBy",
            table: "SecureShares");

        migrationBuilder.RenameIndex(
            name: "IX_PasswordShares_ExpiresAtUtc",
            newName: "IX_SecureShares_ExpiresAtUtc",
            table: "SecureShares");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameIndex(
            name: "IX_SecureShares_AccessToken",
            newName: "IX_PasswordShares_AccessToken",
            table: "SecureShares");

        migrationBuilder.RenameIndex(
            name: "IX_SecureShares_CreatedBy",
            newName: "IX_PasswordShares_CreatedBy",
            table: "SecureShares");

        migrationBuilder.RenameIndex(
            name: "IX_SecureShares_ExpiresAtUtc",
            newName: "IX_PasswordShares_ExpiresAtUtc",
            table: "SecureShares");

        migrationBuilder.Sql("ALTER TABLE \"SecureShares\" RENAME CONSTRAINT \"PK_SecureShares\" TO \"PK_PasswordShares\";");

        migrationBuilder.RenameTable(
            name: "SecureShares",
            newName: "PasswordShares");
    }
}
