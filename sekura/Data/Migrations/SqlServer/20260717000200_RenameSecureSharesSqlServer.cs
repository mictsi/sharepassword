using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sekura.Data.Migrations.SqlServer;

[DbContext(typeof(SqlServerSekuraDbContext))]
[Migration(MigrationId)]
public partial class RenameSecureSharesSqlServer : Migration
{
    public const string MigrationId = "20260717000200_RenameSecureSharesSqlServer";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(
            name: "PasswordShares",
            newName: "SecureShares");

        migrationBuilder.Sql("EXEC sp_rename N'PK_PasswordShares', N'PK_SecureShares', N'OBJECT';");

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

        migrationBuilder.Sql("EXEC sp_rename N'PK_SecureShares', N'PK_PasswordShares', N'OBJECT';");

        migrationBuilder.RenameTable(
            name: "SecureShares",
            newName: "PasswordShares");
    }
}
