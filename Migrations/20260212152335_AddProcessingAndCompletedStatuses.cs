using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentDispatchService.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingAndCompletedStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old: 0 Pending, 1 Sent, 2 Failed
            // New: 0 Pending, 1 Processing, 2 Completed, 3 Failed

            // Move Failed (2) -> Failed (3)
            migrationBuilder.Sql("""
                UPDATE "DispatchRequests"
                SET "Status" = 3
                WHERE "Status" = 2;
                """);

            // Move Sent (1) -> Completed (2)
            migrationBuilder.Sql("""
                UPDATE "DispatchRequests"
                SET "Status" = 2
                WHERE "Status" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse:
            // Completed (2) -> Sent (1)
            migrationBuilder.Sql("""
                UPDATE "DispatchRequests"
                SET "Status" = 1
                WHERE "Status" = 2;
                """);

            // Failed (3) -> Failed (2)
            migrationBuilder.Sql("""
                UPDATE "DispatchRequests"
                SET "Status" = 2
                WHERE "Status" = 3;
                """);
        }
    }
}
