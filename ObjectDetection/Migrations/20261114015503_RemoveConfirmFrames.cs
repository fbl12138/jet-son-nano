using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObjectDetection.Migrations
{
    /// <inheritdoc />
    public partial class RemoveConfirmFrames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmFrames",
                table: "ComplianceRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConfirmFrames",
                table: "ComplianceRules",
                type: "int",
                nullable: false,
                defaultValue: 3);
        }
    }
}
