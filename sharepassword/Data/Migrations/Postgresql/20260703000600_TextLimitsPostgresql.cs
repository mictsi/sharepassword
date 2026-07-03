using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SharePassword.Data.Migrations.Postgresql;

[DbContext(typeof(PostgresqlSharePasswordDbContext))]
[Migration(MigrationId)]
public partial class TextLimitsPostgresql : Migration
{
    public const string MigrationId = "20260703000600_TextLimitsPostgresql";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Instructions",
            table: "PasswordShares",
            type: "character varying(10000)",
            maxLength: 10000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(1000)",
            oldMaxLength: 1000);

        migrationBuilder.AlterColumn<string>(
            name: "RequestInstructions",
            table: "InformationRequests",
            type: "character varying(10000)",
            maxLength: 10000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(1000)",
            oldMaxLength: 1000);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Instructions",
            table: "PasswordShares",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(10000)",
            oldMaxLength: 10000);

        migrationBuilder.AlterColumn<string>(
            name: "RequestInstructions",
            table: "InformationRequests",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(10000)",
            oldMaxLength: 10000);
    }
}