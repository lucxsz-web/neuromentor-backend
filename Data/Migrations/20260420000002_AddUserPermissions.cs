using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeuroMentor.Api.Data;

#nullable disable

namespace NeuroMentor.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260420000002_AddUserPermissions")]
public partial class AddUserPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsAiEnabled",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsAdmin",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsAiEnabled", table: "Users");
        migrationBuilder.DropColumn(name: "IsAdmin", table: "Users");
    }
}
