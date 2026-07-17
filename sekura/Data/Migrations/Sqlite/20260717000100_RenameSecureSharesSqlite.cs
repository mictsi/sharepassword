using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sekura.Data.Migrations.Sqlite;

[DbContext(typeof(SqliteSekuraDbContext))]
[Migration(MigrationId)]
public partial class RenameSecureSharesSqlite : Migration
{
    public const string MigrationId = "20260717000100_RenameSecureSharesSqlite";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PasswordShares_AccessToken",
            table: "PasswordShares");

        migrationBuilder.DropIndex(
            name: "IX_PasswordShares_CreatedBy",
            table: "PasswordShares");

        migrationBuilder.DropIndex(
            name: "IX_PasswordShares_ExpiresAtUtc",
            table: "PasswordShares");

        migrationBuilder.RenameTable(
            name: "PasswordShares",
            newName: "SecureShares");

        migrationBuilder.CreateIndex(
            name: "IX_SecureShares_AccessToken",
            table: "SecureShares",
            column: "AccessToken",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SecureShares_CreatedBy",
            table: "SecureShares",
            column: "CreatedBy");

        migrationBuilder.CreateIndex(
            name: "IX_SecureShares_ExpiresAtUtc",
            table: "SecureShares",
            column: "ExpiresAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SecureShares_AccessToken",
            table: "SecureShares");

        migrationBuilder.DropIndex(
            name: "IX_SecureShares_CreatedBy",
            table: "SecureShares");

        migrationBuilder.DropIndex(
            name: "IX_SecureShares_ExpiresAtUtc",
            table: "SecureShares");

        migrationBuilder.RenameTable(
            name: "SecureShares",
            newName: "PasswordShares");

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
}
