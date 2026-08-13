import { redirect } from "next/navigation";
import { auth } from "@/auth";
import {
  apiFetch,
  type Paciente,
  type Procedimento,
  type Profissional,
} from "@/lib/api";
import { criarAtendimento } from "@/lib/actions/agenda";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardBody, EmptyState } from "@/components/ui/card";
import { Busca } from "@/components/ui/busca";
import { Field, Input } from "@/components/ui/field";
import { Formulario } from "@/components/ui/form";
import { Button } from "@/components/ui/button";
import Link from "next/link";

export default async function NovoAtendimentoPage(props: PageProps<"/agenda/novo">) {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  if (!session.roles.some((r) => r === "OWNER" || r === "SECRETARY")) {
    redirect("/agenda");
  }

  const params = await props.searchParams;
  const dia = typeof params.dia === "string" ? params.dia : "";

  const [pacientes, procedimentos, profissionais] = await Promise.all([
    apiFetch<Paciente[]>("/patients"),
    apiFetch<Procedimento[]>("/procedures"),
    apiFetch<Profissional[]>("/professionals"),
  ]);

  const cabecalho = (
    <PageHeader titulo="Novo agendamento" descricao="O horário de término vem da duração do procedimento." />
  );

  // Sem procedimento ou sem profissional não há o que agendar. Dizer isso é melhor do
  // que mostrar um formulário com selects vazios.
  const faltando =
    procedimentos.length === 0
      ? { o: "procedimento", href: "/procedimentos/novo", rotulo: "Cadastrar procedimento" }
      : profissionais.length === 0
        ? { o: "profissional", href: "/procedimentos", rotulo: "Voltar" }
        : pacientes.length === 0
          ? { o: "paciente", href: "/pacientes/nova", rotulo: "Cadastrar paciente" }
          : null;

  if (faltando) {
    return (
      <AppShell
        atual="/agenda"
        usuario={session.user?.name ?? session.user?.email ?? "—"}
        papeis={session.roles}
      >
        {cabecalho}
        <EmptyState
          title={`Nenhum ${faltando.o} cadastrado`}
          description={`É preciso ter ao menos um ${faltando.o} para conseguir agendar.`}
          action={
            <Link href={faltando.href}>
              <Button>{faltando.rotulo}</Button>
            </Link>
          }
        />
      </AppShell>
    );
  }

  return (
    <AppShell
      atual="/agenda"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      {cabecalho}

      <Card className="max-w-lg">
        <CardBody>
          <Formulario acao={criarAtendimento} cancelarHref="/agenda" rotuloEnviar="Agendar">
            <Busca
              name="paciente"
              recurso="pacientes"
              label="Paciente"
              placeholder="Nome ou telefone"
            />

            <Busca
              name="procedimento"
              recurso="procedimentos"
              label="Procedimento"
              placeholder="Comece a digitar"
            />

            <Busca
              name="profissional"
              recurso="profissionais"
              label="Profissional"
              placeholder="Comece a digitar"
            />

            <div className="grid grid-cols-2 gap-4">
              <Field label="Data" htmlFor="data">
                <Input id="data" name="data" type="date" required defaultValue={dia} />
              </Field>

              <Field label="Horário" htmlFor="hora">
                <Input id="hora" name="hora" type="time" step={300} required />
              </Field>
            </div>

            <Field label="Observações" htmlFor="observacoes">
              <Input id="observacoes" name="observacoes" placeholder="Opcional" />
            </Field>
          </Formulario>
        </CardBody>
      </Card>
    </AppShell>
  );
}
