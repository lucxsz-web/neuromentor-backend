using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NeuroMentor.Api.Data;

#nullable disable

namespace NeuroMentor.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260420000003_AddModuleTextChunk")]
public partial class AddModuleTextChunk : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TextChunk",
            table: "LessonModules",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "TextChunk", table: "LessonModules");
    }
}
