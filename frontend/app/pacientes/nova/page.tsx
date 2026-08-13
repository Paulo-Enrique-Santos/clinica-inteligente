import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardBody } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { Formulario } from "@/components/ui/form";
import { criarPaciente } from "@/lib/actions/pacientes";

export default async function NovaPacientePage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  if (!session.roles.some((r) => r === "OWNER" || r === "SECRETARY")) {
    redirect("/pacientes");
  }

  return (
    <AppShell
      atual="/pacientes"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Nova paciente"
        descricao="Dados básicos da ficha. O restante entra na anamnese."
      />

      <Card className="max-w-lg">
        <CardBody>
          <Formulario acao={criarPaciente} cancelarHref="/pacientes" rotuloEnviar="Cadastrar">
            <Field label="Nome completo" htmlFor="nome">
              <Input id="nome" name="nome" required autoFocus placeholder="Maria Silva" />
            </Field>

            <Field
              label="Telefone"
              htmlFor="telefone"
              hint="Pode digitar como preferir — o sistema formata. É por aqui que os agentes vão falar com ela."
            >
              <Input
                id="telefone"
                name="telefone"
                required
                inputMode="tel"
                placeholder="(11) 98765-4321"
              />
            </Field>

            <Field label="Data de nascimento" htmlFor="nascimento">
              <Input id="nascimento" name="nascimento" type="date" />
            </Field>

            <Field label="Observações" htmlFor="observacoes">
              <Input id="observacoes" name="observacoes" placeholder="Opcional" />
            </Field>
          </Formulario>
        </CardBody>
      </Card>
    </AppShell>
  );
}
