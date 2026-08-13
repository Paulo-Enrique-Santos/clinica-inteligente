import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { criarProtocolo } from "@/lib/actions/protocolos";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardBody } from "@/components/ui/card";
import { Busca } from "@/components/ui/busca";
import { Field, Input } from "@/components/ui/field";
import { Formulario } from "@/components/ui/form";
import { ItensDoProtocolo } from "./itens";

export default async function NovoProtocoloPage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  // Prescrever é da doutora. A recepção negocia o valor depois — por isso ela não
  // entra aqui.
  if (!session.roles.some((r) => r === "OWNER" || r === "DOCTOR")) {
    redirect("/protocolos");
  }

  return (
    <AppShell
      atual="/protocolos"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Novo protocolo"
        descricao="O que a paciente precisa e em que ritmo. O valor é fechado depois, pela recepção."
      />

      <Card className="max-w-2xl">
        <CardBody>
          <Formulario
            acao={criarProtocolo}
            cancelarHref="/protocolos"
            rotuloEnviar="Enviar para orçamento"
          >
            <Busca
              name="paciente"
              recurso="pacientes"
              label="Paciente"
              placeholder="Nome ou telefone"
            />

            <Busca
              name="profissional"
              recurso="profissionais"
              label="Profissional responsável"
              placeholder="Comece a digitar"
            />

            <ItensDoProtocolo />

            <Field
              label="Orientações"
              htmlFor="observacoes"
              hint="Em linguagem de paciente: é o que a recepção vai repetir para ela."
            >
              <Input
                id="observacoes"
                name="observacoes"
                placeholder="Começar pela limpeza; botox depois de 15 dias."
              />
            </Field>
          </Formulario>
        </CardBody>
      </Card>
    </AppShell>
  );
}
