using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SharePassword.Data.Migrations.SqlServer;

[DbContext(typeof(SqlServerSharePasswordDbContext))]
[Migration(MigrationId)]
public partial class TextLimitsSqlServer : Migration
{
    public const string MigrationId = "20260703000500_TextLimitsSqlServer";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Instructions",
            table: "PasswordShares",
            type: "nvarchar(max)",
            maxLength: 10000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(1000)",
            oldMaxLength: 1000);

        migrationBuilder.AlterColumn<string>(
            name: "RequestInstructions",
            table: "InformationRequests",
            type: "nvarchar(max)",
            maxLength: 10000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(1000)",
            oldMaxLength: 1000);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Instructions",
            table: "PasswordShares",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldMaxLength: 10000);

        migrationBuilder.AlterColumn<string>(
            name: "RequestInstructions",
            table: "InformationRequests",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldMaxLength: 10000);
    }
}