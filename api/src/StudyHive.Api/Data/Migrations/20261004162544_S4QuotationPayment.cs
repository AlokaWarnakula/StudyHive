using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHive.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class S4QuotationPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paid_at",
                table: "quotations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "paid_by",
                table: "quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_reference",
                table: "quotations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotations_paid_by",
                table: "quotations",
                column: "paid_by");

            migrationBuilder.AddForeignKey(
                name: "fk_quotations_users_paid_by",
                table: "quotations",
                column: "paid_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_quotations_users_paid_by",
                table: "quotations");

            migrationBuilder.DropIndex(
                name: "ix_quotations_paid_by",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "paid_at",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "paid_by",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "payment_reference",
                table: "quotations");
        }
    }
}
