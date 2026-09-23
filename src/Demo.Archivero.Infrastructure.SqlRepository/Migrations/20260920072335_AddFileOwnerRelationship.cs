using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demo.Archivero.Infrastructure.SqlRepository.Migrations
{
    /// <inheritdoc />
    public partial class AddFileOwnerRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "State",
                schema: "dbo",
                table: "Files",
                newName: "Status");

            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                schema: "dbo",
                table: "Files",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Files_OwnerId",
                schema: "dbo",
                table: "Files",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_AppUsers_OwnerId",
                schema: "dbo",
                table: "Files",
                column: "OwnerId",
                principalSchema: "dbo",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_AppUsers_OwnerId",
                schema: "dbo",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_OwnerId",
                schema: "dbo",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                schema: "dbo",
                table: "Files");

            migrationBuilder.RenameColumn(
                name: "Status",
                schema: "dbo",
                table: "Files",
                newName: "State");
        }
    }
}
