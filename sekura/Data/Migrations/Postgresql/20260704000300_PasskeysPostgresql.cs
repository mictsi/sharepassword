using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sekura.Data.Migrations.Postgresql;

[DbContext(typeof(PostgresqlSekuraDbContext))]
[Migration(MigrationId)]
public partial class PasskeysPostgresql : Migration
{
    public const string MigrationId = "20260704000300_PasskeysPostgresql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LocalUserPasskeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                LocalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CredentialId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                PublicKey = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                SignatureCounter = table.Column<long>(type: "bigint", nullable: false),
                Transports = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                AaGuid = table.Column<Guid>(type: "uuid", nullable: false),
                DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
