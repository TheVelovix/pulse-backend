using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pulse.Migrations
{
    /// <inheritdoc />
    public partial class AddUtmToPageViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UtmCampaign",
                table: "PageViews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmContent",
                table: "PageViews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmMedium",
                table: "PageViews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmSource",
                table: "PageViews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtmTerm",
                table: "PageViews",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UtmCampaign",
                table: "PageViews");

            migrationBuilder.DropColumn(
                name: "UtmContent",
                table: "PageViews");

            migrationBuilder.DropColumn(
                name: "UtmMedium",
                table: "PageViews");

            migrationBuilder.DropColumn(
                name: "UtmSource",
                table: "PageViews");

            migrationBuilder.DropColumn(
                name: "UtmTerm",
                table: "PageViews");
        }
    }
}
