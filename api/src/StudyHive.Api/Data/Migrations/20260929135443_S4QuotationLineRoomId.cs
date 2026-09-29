using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHive.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class S4QuotationLineRoomId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_line_shape",
                table: "quotation_line_items");

            migrationBuilder.AddColumn<Guid>(
                name: "room_id",
                table: "quotation_line_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotation_line_items_room_id",
                table: "quotation_line_items",
                column: "room_id");

            migrationBuilder.AddCheckConstraint(
                name: "chk_line_shape",
                table: "quotation_line_items",
                sql: "(item_type = 'Room' AND room_id IS NOT NULL AND consumable_id IS NULL) OR (item_type = 'Consumable' AND consumable_id IS NOT NULL AND room_id IS NULL AND room_booking_id IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_quotation_line_items_study_rooms_room_id",
                table: "quotation_line_items",
                column: "room_id",
                principalTable: "study_rooms",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_quotation_line_items_study_rooms_room_id",
                table: "quotation_line_items");

            migrationBuilder.DropIndex(
                name: "ix_quotation_line_items_room_id",
                table: "quotation_line_items");

            migrationBuilder.DropCheckConstraint(
                name: "chk_line_shape",
                table: "quotation_line_items");

            migrationBuilder.DropColumn(
                name: "room_id",
                table: "quotation_line_items");

            migrationBuilder.AddCheckConstraint(
                name: "chk_line_shape",
                table: "quotation_line_items",
                sql: "(item_type = 'Room' AND room_booking_id IS NOT NULL AND consumable_id IS NULL) OR (item_type = 'Consumable' AND consumable_id IS NOT NULL AND room_booking_id IS NULL)");
        }
    }
}
