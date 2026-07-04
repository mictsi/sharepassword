using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SharePassword.Data.Migrations.Sqlite;

[DbContext(typeof(SqliteSharePasswordDbContext))]
[Migration(MigrationId)]
public partial class PasskeysSqlite : Migration
{
    public const string MigrationId = "20260704000100_PasskeysSqlite";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LocalUserPasskeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                LocalUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CredentialId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                PublicKey = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                SignatureCounter = table.Column<long>(type: "INTEGER", nullable: false),
                Transports = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                AaGuid = table.Column<Guid>(type: "TEXT", nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastUsedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LocalUserPasskeys", x => x.Id);
                table.ForeignKey(
                    name: "FK_LocalUserPasskeys_LocalUsers_LocalUserId",
                    column: x => x.LocalUserId,
                    principalTable: "LocalUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LocalUserPasskeys_CredentialId",
            table: "LocalUserPasskeys",
            column: "CredentialId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_LocalUserPasskeys_LocalUserId",
            table: "LocalUserPasskeys",
            column: "LocalUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LocalUserPasskeys");
    }
}
