using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlightManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class BookingConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve legacy records. Refuse inconsistent data instead of deleting,
            // renumbering seats, changing prices or assigning owners implicitly.
            migrationBuilder.Sql("""
                LOCK TABLE "Flights", "Tickets" IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Flights" WHERE "TotalSeats" <= 0)
                       OR EXISTS (SELECT 1 FROM "Tickets"
                           WHERE "Price" <= 0 OR btrim("SeatNumber") !~ '^[A-Za-z0-9]{1,8}$')
                       OR EXISTS (SELECT 1 FROM "Tickets"
                           GROUP BY "FlightId", upper(btrim("SeatNumber")) HAVING count(*) > 1)
                       OR EXISTS (SELECT 1 FROM "Flights" f
                           WHERE (SELECT count(*) FROM "Tickets" t WHERE t."FlightId" = f."Id") > f."TotalSeats")
                    THEN
                        RAISE EXCEPTION 'Booking migration blocked: invalid prices/seats, duplicate seats or capacity overflow. Review legacy data explicitly.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Passengers_PassengerId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_FlightId",
                table: "Tickets");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedSeatNumber",
                table: "Tickets",
                type: "text",
                nullable: false,
                computedColumnSql: "upper(btrim(\"SeatNumber\"))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_FlightId_NormalizedSeatNumber",
                table: "Tickets",
                columns: new[] { "FlightId", "NormalizedSeatNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tickets_PositivePrice",
                table: "Tickets",
                sql: "\"Price\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tickets_SeatLabel",
                table: "Tickets",
                sql: "btrim(\"SeatNumber\") ~ '^[A-Za-z0-9]{1,8}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Flights_PositiveSeats",
                table: "Flights",
                sql: "\"TotalSeats\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Passengers_PassengerId",
                table: "Tickets",
                column: "PassengerId",
                principalTable: "Passengers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Passengers_PassengerId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_FlightId_NormalizedSeatNumber",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tickets_PositivePrice",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tickets_SeatLabel",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Flights_PositiveSeats",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "NormalizedSeatNumber",
                table: "Tickets");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_FlightId",
                table: "Tickets",
                column: "FlightId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Passengers_PassengerId",
                table: "Tickets",
                column: "PassengerId",
                principalTable: "Passengers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
