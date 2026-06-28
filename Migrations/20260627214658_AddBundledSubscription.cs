using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pulse.Migrations
{
    /// <inheritdoc />
    public partial class AddBundledSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BundledSubscription_ExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BundledSubscription_Plan",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BundledSubscription_ExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BundledSubscription_Plan",
                table: "Users");
        }
    }
}
