using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pulse.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_pageviews_project_createdat",
                table: "PageViews",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "idx_sessions_project_createdat",
                table: "Sessions",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "idx_pageviews_sessionid",
                table: "PageViews",
                column: "SessionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "idx_pageviews_project_createdat", table: "PageViews");
            migrationBuilder.DropIndex(name: "idx_sessions_project_createdat", table: "Sessions");
            migrationBuilder.DropIndex(name: "idx_pageviews_sessionid", table: "PageViews");
        }
    }
}
