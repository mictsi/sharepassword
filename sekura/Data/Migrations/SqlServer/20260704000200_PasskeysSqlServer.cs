using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Sekura.Data.Migrations.SqlServer;

[DbContext(typeof(SqlServerSekuraDbContext))]
[Migration(MigrationId)]
public partial class PasskeysSqlServer : Migration
{
    public const string MigrationId = "20260704000200_PasskeysSqlServer";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LocalUserPasskeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LocalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CredentialId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                PublicKey = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                SignatureCounter = table.Column<long>(type: "bigint", nullable: false),
                Transports = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                AaGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastUsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
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
