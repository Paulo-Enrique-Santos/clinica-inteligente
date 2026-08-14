import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch } from "@/lib/api";
import { data as fmtData, dataHora, reais } from "@/lib/formato";
import { formatarTelefone } from "@/lib/telefone";
import { gerarLinkDeAnamnese } from "@/lib/actions/anamnese";
import { PERGUNTAS } from "@/lib/anamnese";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, EmptyState } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Alert } from "@/components/ui/alert";
import { cn } from "@/lib/cn";

type Ficha = {
  patient: { id: string; fullName: string; phoneE164: string; birthDate: string | null };
  totals: { appointments: number; plans: number; payments: number };
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
  pageSize: number;
};

const TOM: Record<string, "neutral" | "primary" | "success" | "warning" | "danger"> = {
  Agendado: "neutral",
  Confirmado: "primary",
  Realizado: "success",
  Faltou: "warning",
  Cancelado: "danger",
  Pendente: "warning",
  Pago: "success",
  Estornado: "danger",
  Proposto: "primary",
  Aprovado: "success",
  Recusado: "danger",
};

function Celula({ children, className }: { children: React.ReactNode; className?: string }) {
  return <td className={cn("px-4 py-3 align-top", className)}>{children}</td>;
}

function Cabecalho({ colunas }: { colunas: string[] }) {
  return (
    <thead>
      <tr className="border-b border-border">
        {colunas.map((c) => (
          <th
            key={c}
            className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wide text-ink-subtle"
          >
            {c}
          </th>
        ))}
      </tr>
    </thead>
  );
}

export default async function FichaDaPacientePage(props: PageProps<"/pacientes/[id]">) {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const { id } = await props.params;
  const params = await props.searchParams;

  const aba = typeof params.aba === "string" ? params.aba : "sessoes";
  const pagina = Math.max(1, Number(params.pagina ?? 1) || 1);
  const linkGerado = typeof params.link === "string" ? params.link : null;

  // Uma aba por vez: uma paciente antiga pode ter centenas de sessões, e carregar tudo
  // para mostrar as vinte primeiras é desperdício que cresce com o tempo de casa.
  const ficha = await apiFetch<Ficha>(
    `/patients/${id}/ficha?aba=${aba}&pagina=${pagina}`,
  );

  const abas = [
    { chave: "sessoes", rotulo: "Sessões", total: ficha.totals.appointments },
    { chave: "protocolos", rotulo: "Protocolos", total: ficha.totals.plans },
    ...(ficha.showsFinance
      ? [{ chave: "pagamentos", rotulo: "Pagamentos", total: ficha.totals.payments }]
      : []),
    { chave: "anamnese", rotulo: "Anamnese", total: null as number | null },
  ];

  const totalDaAba =
    aba === "sessoes"
      ? ficha.totals.appointments
      : aba === "protocolos"
        ? ficha.totals.plans
        : aba === "pagamentos"
          ? ficha.totals.payments
          : 0;

  const ultimaPagina = Math.max(1, Math.ceil(totalDaAba / ficha.pageSize));

  const respostas: Record<string, string> = ficha.anamnesis
    ? JSON.parse(ficha.anamnesis.answersJson)
    : {};

  const url = (novaAba: string, novaPagina = 1) =>
    `/pacientes/${id}?aba=${novaAba}&pagina=${novaPagina}`;

  return (
    <AppShell usuario={session.user?.name ?? session.user?.email ?? "—"} papeis={session.roles}>
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

      <div className="mb-5 flex flex-wrap gap-1 border-b border-border">
        {abas.map((a) => (
          <Link
            key={a.chave}
            href={url(a.chave)}
            className={cn(
              "-mb-px border-b-2 px-4 py-2.5 text-sm transition-colors",
              a.chave === aba
                ? "border-primary font-medium text-primary"
                : "border-transparent text-ink-muted hover:text-ink",
            )}
          >
            {a.rotulo}
            {a.total !== null && a.total > 0 && (
              <span className="ml-1.5 text-xs text-ink-subtle">{a.total}</span>
            )}
          </Link>
        ))}
      </div>

      <Card className="overflow-hidden">
        {aba === "sessoes" &&
          (ficha.appointments.length === 0 ? (
            <EmptyState title="Nenhum atendimento" description="Ainda não há sessões registradas." />
          ) : (
            <table className="w-full text-sm">
              <Cabecalho colunas={["Data", "Procedimento", "Profissional", "Valor", "Status"]} />
              <tbody className="divide-y divide-border">
                {ficha.appointments.map((a) => (
                  <tr key={a.id} className="transition-colors hover:bg-surface">
                    <Celula className="whitespace-nowrap text-ink-muted">
                      {dataHora(a.startsAt)}
                    </Celula>
                    <Celula>
                      <span className="text-ink">{a.procedureName}</span>
                      {a.executionNotes && (
                        <p className="mt-0.5 text-xs italic text-ink-subtle">
                          {a.executionNotes}
                        </p>
                      )}
                    </Celula>
                    <Celula className="text-ink-muted">{a.professionalName}</Celula>
                    <Celula className="whitespace-nowrap text-ink">{reais(a.price)}</Celula>
                    <Celula>
                      <Badge tone={TOM[a.status] ?? "neutral"}>{a.status}</Badge>
                    </Celula>
                  </tr>
                ))}
              </tbody>
            </table>
          ))}

        {aba === "protocolos" &&
          (ficha.plans.length === 0 ? (
            <EmptyState title="Nenhum protocolo" description="Nada prescrito até agora." />
          ) : (
            <table className="w-full text-sm">
              <Cabecalho colunas={["Data", "Profissional", "Procedimentos", "Total", "Status"]} />
              <tbody className="divide-y divide-border">
                {ficha.plans.map((p) => (
                  <tr key={p.id} className="transition-colors hover:bg-surface">
                    <Celula className="whitespace-nowrap text-ink-muted">
                      {fmtData(p.createdAt)}
                    </Celula>
                    <Celula className="text-ink-muted">{p.professionalName}</Celula>
                    <Celula>
                      {p.items.map((i, n) => (
                        <p key={n} className="text-ink">
                          {i.sessions}× {i.procedureName}
                        </p>
                      ))}
                    </Celula>
                    <Celula className="whitespace-nowrap text-ink">
                      {reais(p.items.reduce((s, i) => s + i.total, 0))}
                    </Celula>
                    <Celula>
                      <Badge tone={TOM[p.status] ?? "neutral"}>{p.status}</Badge>
                    </Celula>
                  </tr>
                ))}
              </tbody>
            </table>
          ))}

        {aba === "pagamentos" &&
          (ficha.payments.length === 0 ? (
            <EmptyState title="Nenhuma cobrança" description="Nada foi cobrado desta paciente." />
          ) : (
            <table className="w-full text-sm">
              <Cabecalho colunas={["Vencimento", "Valor", "Parcela", "Meio", "Status"]} />
              <tbody className="divide-y divide-border">
                {ficha.payments.map((p) => (
                  <tr key={p.id} className="transition-colors hover:bg-surface">
                    <Celula className="whitespace-nowrap text-ink-muted">
                      {fmtData(p.dueDate)}
                    </Celula>
                    <Celula className="whitespace-nowrap text-ink">{reais(p.amount)}</Celula>
                    <Celula className="text-ink-subtle">
                      {p.installmentCount && p.installmentCount > 1
                        ? `${p.installmentNumber}/${p.installmentCount}`
                        : "—"}
                    </Celula>
                    <Celula className="text-ink-muted">{p.method ?? "—"}</Celula>
                    <Celula>
                      <Badge tone={TOM[p.status] ?? "neutral"}>{p.status}</Badge>
                    </Celula>
                  </tr>
                ))}
              </tbody>
            </table>
          ))}

        {aba === "anamnese" && (
          <div className="p-5">
            {!ficha.anamnesis ? (
              <EmptyState
                title="Ficha não preenchida"
                description="Gere o link acima e envie para a paciente preencher."
              />
            ) : (
              <div className="space-y-4">
                <p className="text-xs text-ink-subtle">
                  Preenchida em {fmtData(ficha.anamnesis.submittedAt)}
                </p>

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
              </div>
            )}
          </div>
        )}
      </Card>

      {ultimaPagina > 1 && (
        <div className="mt-4 flex items-center justify-between">
          <p className="text-xs text-ink-subtle">
            Página {pagina} de {ultimaPagina} · {totalDaAba} no total
          </p>
          <div className="flex gap-2">
            <Link href={url(aba, Math.max(1, pagina - 1))}>
              <Button variant="secondary" size="sm" disabled={pagina === 1}>
                ← Anterior
              </Button>
            </Link>
            <Link href={url(aba, Math.min(ultimaPagina, pagina + 1))}>
              <Button variant="secondary" size="sm" disabled={pagina === ultimaPagina}>
                Próxima →
              </Button>
            </Link>
          </div>
        </div>
      )}

      <div className="mt-6">
        <Link href="/pacientes" className="text-sm text-primary hover:underline">
          ← Voltar para pacientes
        </Link>
      </div>
    </AppShell>
  );
}
