using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

public partial class AddGlobalTags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsGlobal",
            table: "Tags",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AlterColumn<Guid>(
            name: "OwnerId",
            table: "Tags",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_NormalizedName",
            table: "Tags",
            column: "NormalizedName",
            unique: true,
            filter: "\"IsGlobal\" = TRUE");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Tags_NormalizedName", table: "Tags");
        migrationBuilder.DropColumn(name: "IsGlobal", table: "Tags");
        migrationBuilder.AlterColumn<Guid>(
            name: "OwnerId",
            table: "Tags",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
    }
}
