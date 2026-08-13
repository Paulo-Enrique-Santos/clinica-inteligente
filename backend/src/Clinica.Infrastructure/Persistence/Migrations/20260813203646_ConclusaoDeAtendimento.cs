using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConclusaoDeAtendimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "execution_notes",
                table: "appointments",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "follow_up_at",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "follow_up_sent_at",
                table: "appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_id_follow_up_at",
                table: "appointments",
                columns: new[] { "tenant_id", "follow_up_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_appointments_tenant_id_follow_up_at",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "execution_notes",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "follow_up_at",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "follow_up_sent_at",
                table: "appointments");
        }
    }
}
