import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch } from "@/lib/api";
import { data as fmtData, dataHora, reais } from "@/lib/formato";
import { formatarTelefone } from "@/lib/telefone";
import { gerarLinkDeAnamnese } from "@/lib/actions/anamnese";
import { PERGUNTAS } from "@/lib/anamnese";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardHeader, CardBody } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Alert } from "@/components/ui/alert";

type Ficha = {
  patient: { id: string; fullName: string; phoneE164: string; birthDate: string | null };
  appointments: {
    id: string;
    startsAt: string;
    procedureName: string;
    professionalName: string;
    status: string;
    price: number;
    executionNotes: string | null;
  }[];
  plans: {
    id: string;
    status: string;
    createdAt: string;
    professionalName: string;
    items: { procedureName: string; sessions: number; total: number }[];
  }[];
  payments: {
    id: string;
    amount: number;
    dueDate: string;
    status: string;
    method: string | null;
    installmentNumber: number | null;
    installmentCount: number | null;
  }[];
  anamnesis: { submittedAt: string; imageConsent: boolean; answersJson: string } | null;
  showsFinance: boolean;
};

const TOM: Record<string, "neutral" | "primary" | "success" | "warning" | "danger"> = {
  Agendado: "neutral",
  Confirmado: "primary",
  Realizado: "success",
  Faltou: "warning",
  Cancelado: "danger",
  Pendente: "warning",
  Pago: "success",
  Proposto: "primary",
  Aprovado: "success",
  Recusado: "danger",
};

export default async function FichaDaPacientePage(props: PageProps<"/pacientes/[id]">) {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const { id } = await props.params;
  const params = await props.searchParams;
  const linkGerado = typeof params.link === "string" ? params.link : null;

  const ficha = await apiFetch<Ficha>(`/patients/${id}/ficha`);

  const realizados = ficha.appointments.filter((a) => a.status === "Realizado");
  const agendados = ficha.appointments.filter(
    (a) => a.status === "Agendado" || a.status === "Confirmado",
  );

  const respostas: Record<string, string> = ficha.anamnesis
    ? JSON.parse(ficha.anamnesis.answersJson)
    : {};

  return (
    <AppShell
      atual="/pacientes"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo={ficha.patient.fullName}
        descricao={`${formatarTelefone(ficha.patient.phoneE164)}${
          ficha.patient.birthDate ? ` · nascida em ${fmtData(ficha.patient.birthDate)}` : ""
        }`}
        acao={
          <form action={gerarLinkDeAnamnese}>
            <input type="hidden" name="paciente" value={id} />
            <Button type="submit" variant="secondary">
              Gerar link da ficha
            </Button>
          </form>
        }
      />

      {linkGerado && (
        <Alert tone="info" className="mb-6">
          <p className="font-medium">Link da ficha gerado — envie para a paciente:</p>
          <p className="mt-1.5 break-all font-mono text-xs">
            {`${process.env.APP_BASE_URL ?? "http://localhost:3000"}/anamnese/${linkGerado}`}
          </p>
          <p className="mt-1.5 text-xs">Vale por 7 dias e serve uma única vez.</p>
        </Alert>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader
            title="Tratamentos contratados"
            description={`${ficha.plans.length} protocolo${ficha.plans.length === 1 ? "" : "s"}`}
          />
          <CardBody className="space-y-3">
            {ficha.plans.length === 0 ? (
              <p className="text-sm text-ink-subtle">Nenhum protocolo ainda.</p>
            ) : (
              ficha.plans.map((p) => (
                <div key={p.id} className="rounded-control border border-border px-4 py-3">
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-sm text-ink">{p.professionalName}</span>
                    <Badge tone={TOM[p.status] ?? "neutral"}>{p.status}</Badge>
                  </div>
                  <ul className="mt-1.5 space-y-0.5">
                    {p.items.map((i, n) => (
                      <li key={n} className="text-xs text-ink-muted">
                        {i.sessions}× {i.procedureName} — {reais(i.total)}
                      </li>
                    ))}
                  </ul>
                </div>
              ))
            )}
          </CardBody>
        </Card>

        <Card>
          <CardHeader
            title="Sessões"
            description={`${realizados.length} realizada${realizados.length === 1 ? "" : "s"} · ${agendados.length} agendada${agendados.length === 1 ? "" : "s"}`}
          />
          <CardBody className="space-y-2">
            {ficha.appointments.length === 0 ? (
              <p className="text-sm text-ink-subtle">Nenhum atendimento ainda.</p>
            ) : (
              ficha.appointments.slice(0, 12).map((a) => (
                <div key={a.id} className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-sm text-ink">{a.procedureName}</p>
                    <p className="text-xs text-ink-subtle">
                      {dataHora(a.startsAt)} · {a.professionalName}
                    </p>
                    {a.executionNotes && (
                      <p className="mt-0.5 text-xs italic text-ink-muted">
                        {a.executionNotes}
                      </p>
                    )}
                  </div>
                  <Badge tone={TOM[a.status] ?? "neutral"}>{a.status}</Badge>
                </div>
              ))
            )}
          </CardBody>
        </Card>

        {ficha.showsFinance && (
          <Card>
            <CardHeader title="Pagamentos" />
            <CardBody className="space-y-2">
              {ficha.payments.length === 0 ? (
                <p className="text-sm text-ink-subtle">Nenhuma cobrança.</p>
              ) : (
                ficha.payments.map((p) => (
                  <div key={p.id} className="flex items-center justify-between gap-3">
                    <div>
                      <p className="text-sm text-ink">{reais(p.amount)}</p>
                      <p className="text-xs text-ink-subtle">
                        vence {fmtData(p.dueDate)}
                        {p.installmentCount && p.installmentCount > 1
                          ? ` · parcela ${p.installmentNumber}/${p.installmentCount}`
                          : ""}
                        {p.method ? ` · ${p.method}` : ""}
                      </p>
                    </div>
                    <Badge tone={TOM[p.status] ?? "neutral"}>{p.status}</Badge>
                  </div>
                ))
              )}
            </CardBody>
          </Card>
        )}

        <Card>
          <CardHeader
            title="Ficha de anamnese"
            description={
              ficha.anamnesis
                ? `Preenchida em ${fmtData(ficha.anamnesis.submittedAt)}`
                : "Ainda não preenchida"
            }
          />
          <CardBody className="space-y-2">
            {!ficha.anamnesis ? (
              <p className="text-sm text-ink-subtle">
                Gere o link acima e envie para a paciente preencher.
              </p>
            ) : (
              <>
                {PERGUNTAS.filter((q) => respostas[q.chave]).map((q) => (
                  <div key={q.chave}>
                    <p className="text-xs text-ink-subtle">{q.texto}</p>
                    <p className="text-sm text-ink">{respostas[q.chave]}</p>
                  </div>
                ))}
                <Badge tone={ficha.anamnesis.imageConsent ? "success" : "neutral"}>
                  {ficha.anamnesis.imageConsent
                    ? "Autoriza uso de imagem"
                    : "Não autoriza uso de imagem"}
                </Badge>
              </>
            )}
          </CardBody>
        </Card>
      </div>

      <div className="mt-6">
        <Link href="/pacientes" className="text-sm text-primary hover:underline">
          ← Voltar para pacientes
        </Link>
      </div>
    </AppShell>
  );
}
