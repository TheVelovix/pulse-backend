using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pulse.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaddleSubscriptionId",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaddleSubscriptionId",
                table: "Users");
        }
    }
}
