using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pulse.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePageView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrowserMajor",
                table: "PageViews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceBrand",
                table: "PageViews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceModel",
                table: "PageViews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSpider",
                table: "PageViews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OsMajor",
                table: "PageViews",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrowserMajor",
                table: "PageViews");

            migrationBuilder.DropColumn(
                name: "DeviceBrand",
                table: "PageViews");

            migrationBuilder.DropColumn(
                name: "DeviceModel",
                table: "PageViews");

            migrationBuilder.DropColumn(
                name: "IsSpider",
                table: "PageViews");

            migrationBuilder.DropColumn(
                name: "OsMajor",
                table: "PageViews");
        }
    }
}
