import { redirect } from "next/navigation";
import { auth } from "@/auth";
import {
  apiFetch,
  type Paciente,
  type Procedimento,
  type Profissional,
} from "@/lib/api";
import { duracao, reais } from "@/lib/formato";
import { criarAtendimento } from "@/lib/actions/agenda";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardBody, EmptyState } from "@/components/ui/card";
import { Field, Input, Select } from "@/components/ui/field";
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
            <Field label="Paciente" htmlFor="paciente">
              <Select id="paciente" name="paciente" required defaultValue="">
                <option value="" disabled>
                  Selecione
                </option>
                {pacientes.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.fullName}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Procedimento" htmlFor="procedimento">
              <Select id="procedimento" name="procedimento" required defaultValue="">
                <option value="" disabled>
                  Selecione
                </option>
                {procedimentos.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name} — {duracao(p.durationMinutes)} — {reais(p.price)}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Profissional" htmlFor="profissional">
              <Select id="profissional" name="profissional" required defaultValue="">
                <option value="" disabled>
                  Selecione
                </option>
                {profissionais.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.displayName}
                  </option>
                ))}
              </Select>
            </Field>

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
