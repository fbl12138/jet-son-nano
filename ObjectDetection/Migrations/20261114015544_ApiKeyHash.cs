using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObjectDetection.Migrations
{
    /// <inheritdoc />
    public partial class ApiKeyHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 先改名，保住已有数据（EF 默认会删列再建列，会丢数据）
            migrationBuilder.RenameColumn(
                name: "ApiKey",
                table: "Devices",
                newName: "ApiKeyHash");

            // 2) 就地转换：把明文替换成 SHA-256 十六进制小写。
            //    注意 HASHBYTES 对 nvarchar 是按 UTF-16 取字节，而 C# 侧用 UTF-8，
            //    所以先 CONVERT 到 varchar，让 ASCII 部分字节一致。
            migrationBuilder.Sql(
                "UPDATE Devices SET ApiKeyHash = LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varchar(100), ApiKeyHash)), 2))");

            // 3) 转换完再收窄到 64（SHA-256 十六进制固定 64 字符）
            migrationBuilder.AlterColumn<string>(
                name: "ApiKeyHash",
                table: "Devices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 注意：哈希不可逆，回滚只能恢复列结构，无法还原明文 ApiKey。
            migrationBuilder.AlterColumn<string>(
                name: "ApiKeyHash",
                table: "Devices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.RenameColumn(
                name: "ApiKeyHash",
                table: "Devices",
                newName: "ApiKey");
        }
    }
}
