using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EstornoDeCobranca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A regra anterior era "pago se e somente se tem data de pagamento". Com o
            // estorno ela deixa de valer: a cobranca foi paga de verdade, voltou, e a
            // data de quando entrou continua importando — para conciliar com a
            // maquininha e para explicar a paciente.
            //
            // A regra vira: nao existe pago sem data, e nao existe data em cobranca que
            // nunca foi paga.
            migrationBuilder.Sql("""
                ALTER TABLE payments DROP CONSTRAINT IF EXISTS ck_payments_pago_tem_data;

                ALTER TABLE payments ADD CONSTRAINT ck_payments_pago_tem_data
                    CHECK (
                        (status <> 'Pago' OR paid_at IS NOT NULL)
                        AND (paid_at IS NULL OR status IN ('Pago', 'Estornado'))
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE payments DROP CONSTRAINT IF EXISTS ck_payments_pago_tem_data;

                ALTER TABLE payments ADD CONSTRAINT ck_payments_pago_tem_data
                    CHECK ((status = 'Pago') = (paid_at IS NOT NULL));
                """);
        }
    }
}
