using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpedienteDasProfissionais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "schedule_exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    closed = table.Column<bool>(type: "boolean", nullable: false),
                    starts_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ends_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    break_starts_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    break_ends_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_schedule_exceptions_professionals_professional_id",
                        column: x => x.professional_id,
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ends_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    break_starts_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    break_ends_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_schedules", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_schedules_professionals_professional_id",
                        column: x => x.professional_id,
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_schedule_exceptions_professional_id",
                table: "schedule_exceptions",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_exceptions_tenant_id",
                table: "schedule_exceptions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_exceptions_tenant_id_professional_id_date",
                table: "schedule_exceptions",
                columns: new[] { "tenant_id", "professional_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_schedules_professional_id",
                table: "work_schedules",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_schedules_tenant_id",
                table: "work_schedules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_schedules_tenant_id_professional_id_day_of_week",
                table: "work_schedules",
                columns: new[] { "tenant_id", "professional_id", "day_of_week" },
                unique: true);

            TenantRls.Enable(migrationBuilder, "work_schedules");
            TenantRls.Enable(migrationBuilder, "schedule_exceptions");

            // FK dentro do mesmo tenant: a verificacao de chave estrangeira do Postgres
            // ignora RLS (ver ADR 0003).
            migrationBuilder.Sql("""
                ALTER TABLE work_schedules ADD CONSTRAINT fk_work_schedules_profissional_mesmo_tenant
                    FOREIGN KEY (tenant_id, professional_id) REFERENCES professionals (tenant_id, id)
                    ON DELETE CASCADE;

                ALTER TABLE schedule_exceptions ADD CONSTRAINT fk_schedule_exceptions_profissional_mesmo_tenant
                    FOREIGN KEY (tenant_id, professional_id) REFERENCES professionals (tenant_id, id)
                    ON DELETE CASCADE;
                """);

            // As mesmas regras que a aplicacao valida, tambem no banco: expediente que
            // termina antes de comecar, e intervalo pela metade ou fora do expediente,
            // sao dados que nao deveriam existir por nenhum caminho.
            migrationBuilder.Sql("""
                ALTER TABLE work_schedules ADD CONSTRAINT ck_work_schedules_janela
                    CHECK (ends_at > starts_at);

                ALTER TABLE work_schedules ADD CONSTRAINT ck_work_schedules_intervalo
                    CHECK (
                        (break_starts_at IS NULL AND break_ends_at IS NULL)
                        OR (
                            break_starts_at IS NOT NULL AND break_ends_at IS NOT NULL
                            AND break_ends_at > break_starts_at
                            AND break_starts_at >= starts_at
                            AND break_ends_at <= ends_at
                        )
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            TenantRls.Disable(migrationBuilder, "work_schedules");
            TenantRls.Disable(migrationBuilder, "schedule_exceptions");

            migrationBuilder.DropTable(
                name: "schedule_exceptions");

            migrationBuilder.DropTable(
                name: "work_schedules");
        }
    }
}
