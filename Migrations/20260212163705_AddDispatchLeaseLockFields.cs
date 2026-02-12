using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentDispatchService.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchLeaseLockFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LockOwner",
                table: "DispatchRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntilUtc",
                table: "DispatchRequests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockOwner",
                table: "DispatchRequests");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                table: "DispatchRequests");
        }
    }
}
