using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObjectDetection.Migrations
{
    /// <inheritdoc />
    public partial class AddStatsDaily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Persons = table.Column<int>(type: "int", nullable: false),
                    Violations = table.Column<int>(type: "int", nullable: false),
                    Compliant = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyStats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStats_DeviceId_StatDate",
                table: "DailyStats",
                columns: new[] { "DeviceId", "StatDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyStats");
        }
    }
}
