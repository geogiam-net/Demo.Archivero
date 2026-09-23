using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Archivero.Infrastructure.SqlRepository.Migrations
{
    /// <inheritdoc />
    public partial class LimitFileTitleAndContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [dbo].[Files] WHERE LEN([Title]) > 128)
                BEGIN
                    THROW 50000, 'Cannot limit Files.Title to 128 characters while longer titles exist.', 1;
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "dbo",
                table: "Files",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AddColumn<string>(
                name: "Content",
                schema: "dbo",
                table: "Files",
                type: "nvarchar(1028)",
                maxLength: 1028,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                schema: "dbo",
                table: "Files");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "dbo",
                table: "Files",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);
        }
    }
}
