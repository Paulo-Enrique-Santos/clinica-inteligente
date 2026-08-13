using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtocolosEOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "appointment_id",
                table: "payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "installment_count",
                table: "payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "installment_number",
                table: "payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "treatment_plan_id",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "treatment_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_appointment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_treatment_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_treatment_plans_appointments_origin_appointment_id",
                        column: x => x.origin_appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_treatment_plans_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_treatment_plans_professionals_professional_id",
                        column: x => x.professional_id,
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    treatment_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    procedure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessions = table.Column<int>(type: "integer", nullable: false),
                    interval_days = table.Column<int>(type: "integer", nullable: true),
                    start_after_days = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_items_procedures_procedure_id",
                        column: x => x.procedure_id,
                        principalTable: "procedures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_items_treatment_plans_treatment_plan_id",
                        column: x => x.treatment_plan_id,
                        principalTable: "treatment_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_plan_items_procedure_id",
                table: "plan_items",
                column: "procedure_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_items_tenant_id",
                table: "plan_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_items_tenant_id_treatment_plan_id",
                table: "plan_items",
                columns: new[] { "tenant_id", "treatment_plan_id" });

            migrationBuilder.CreateIndex(
                name: "ix_plan_items_treatment_plan_id",
                table: "plan_items",
                column: "treatment_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_treatment_plans_origin_appointment_id",
                table: "treatment_plans",
                column: "origin_appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_treatment_plans_patient_id",
                table: "treatment_plans",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_treatment_plans_professional_id",
                table: "treatment_plans",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_treatment_plans_tenant_id",
                table: "treatment_plans",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_treatment_plans_tenant_id_patient_id_status",
                table: "treatment_plans",
                columns: new[] { "tenant_id", "patient_id", "status" });

            TenantRls.Enable(migrationBuilder, "treatment_plans");
            TenantRls.Enable(migrationBuilder, "plan_items");

            migrationBuilder.Sql("""
                ALTER TABLE treatment_plans ADD CONSTRAINT uq_treatment_plans_tenant_id UNIQUE (tenant_id, id);

                ALTER TABLE treatment_plans ADD CONSTRAINT fk_plans_paciente_mesmo_tenant
                    FOREIGN KEY (tenant_id, patient_id) REFERENCES patients (tenant_id, id);

                ALTER TABLE plan_items ADD CONSTRAINT fk_itens_plano_mesmo_tenant
                    FOREIGN KEY (tenant_id, treatment_plan_id) REFERENCES treatment_plans (tenant_id, id)
                    ON DELETE CASCADE;

                ALTER TABLE payments ADD CONSTRAINT fk_payments_plano_mesmo_tenant
                    FOREIGN KEY (tenant_id, treatment_plan_id) REFERENCES treatment_plans (tenant_id, id);
                """);

            // Cobranca precisa pertencer a alguma coisa: ou a um atendimento avulso, ou
            // a um protocolo. Solta, nao teria como a clinica saber do que se trata.
            migrationBuilder.Sql("""
                ALTER TABLE payments ADD CONSTRAINT ck_payments_tem_origem
                    CHECK (appointment_id IS NOT NULL OR treatment_plan_id IS NOT NULL);

                ALTER TABLE plan_items ADD CONSTRAINT ck_plan_items_sessoes
                    CHECK (sessions >= 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_items");

            migrationBuilder.DropTable(
                name: "treatment_plans");

            migrationBuilder.DropColumn(
                name: "installment_count",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "installment_number",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "treatment_plan_id",
                table: "payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "appointment_id",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
