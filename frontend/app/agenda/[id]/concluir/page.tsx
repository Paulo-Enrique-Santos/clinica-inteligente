import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Atendimento } from "@/lib/api";
import { hora, reais } from "@/lib/formato";
import { concluirAtendimento } from "@/lib/actions/atendimento";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardBody } from "@/components/ui/card";
import { Field, Input, Select } from "@/components/ui/field";
import { Formulario } from "@/components/ui/form";
import { Insumos } from "./insumos";

/** Quando perguntar como a paciente está. */
const CONTATOS = [
  { horas: 24, rotulo: "Em 24 horas" },
  { horas: 48, rotulo: "Em 2 dias" },
  { horas: 168, rotulo: "Em 7 dias" },
  { horas: 0, rotulo: "Não perguntar" },
];

export default async function ConcluirPage(props: PageProps<"/agenda/[id]/concluir">) {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const { id } = await props.params;
  const params = await props.searchParams;
  const dia = typeof params.dia === "string" ? params.dia : "";

  // A agenda do dia já traz tudo que a tela precisa mostrar, e evita um endpoint só
  // para buscar um atendimento.
  const agenda = await apiFetch<Atendimento[]>(
    `/appointments?de=${encodeURIComponent(`${dia}T00:00:00-03:00`)}&ate=${encodeURIComponent(`${dia}T23:59:59-03:00`)}`,
  );

  const atendimento = agenda.find((a) => a.id === id);

  if (!atendimento) {
    redirect(`/agenda?dia=${dia}`);
  }

  const usuario = session.user?.name ?? session.user?.email ?? "—";

  return (
    <AppShell atual="/agenda" usuario={usuario} papeis={session.roles}>
      <PageHeader
        titulo="Concluir atendimento"
        descricao={`${atendimento.patientName} · ${atendimento.procedureName} · ${hora(atendimento.startsAt)}`}
      />

      <Card className="max-w-2xl">
        <CardBody>
          <div className="mb-5 rounded-control bg-surface px-4 py-3 text-sm text-ink-muted">
            {atendimento.professionalName} · {reais(atendimento.price)}
          </div>

          <Formulario
            acao={concluirAtendimento}
            cancelarHref={`/agenda?dia=${dia}`}
            rotuloEnviar="Concluir atendimento"
          >
            <input type="hidden" name="id" value={id} />
            <input type="hidden" name="dia" value={dia} />

            <Insumos />

            <Field
              label="Observações do atendimento"
              htmlFor="observacoes"
              hint="O que aconteceu, reações, orientações dadas. Fica no prontuário, não na agenda."
            >
              <Input
                id="observacoes"
                name="observacoes"
                placeholder="Paciente tolerou bem, sem intercorrências."
              />
            </Field>

            <Field
              label="Perguntar como ela está"
              htmlFor="followUp"
              hint="Por enquanto só fica agendado; o envio automático entra com o agente de pós-procedimento."
            >
              <Select id="followUp" name="followUp" defaultValue="24">
                {CONTATOS.map((c) => (
                  <option key={c.horas} value={c.horas}>
                    {c.rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </Formulario>
        </CardBody>
      </Card>
    </AppShell>
  );
}
