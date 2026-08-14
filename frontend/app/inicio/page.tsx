import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Atendimento, type Cobranca, type ItemDeEstoque } from "@/lib/api";
import { hojeNaClinica, hora, reais } from "@/lib/formato";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardHeader, CardBody } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/cn";

/** Uma consulta falhando não deve derrubar o painel inteiro. */
async function tentar<T>(promessa: Promise<T>, vazio: T): Promise<T> {
  try {
    return await promessa;
  } catch {
    return vazio;
  }
}

function Indicador({
  rotulo,
  valor,
  detalhe,
  tom = "neutro",
  href,
}: {
  rotulo: string;
  valor: string;
  detalhe?: string;
  tom?: "neutro" | "primario" | "alerta" | "bom";
  href: string;
}) {
  const fundo = {
    neutro: "bg-canvas",
    primario: "bg-primary text-white",
    alerta: "bg-warning-soft",
    bom: "bg-success-soft",
  }[tom];

  const rotuloCor = tom === "primario" ? "text-white/70" : "text-ink-subtle";
  const detalheCor = tom === "primario" ? "text-white/80" : "text-ink-muted";

  return (
    <Link href={href}>
      <div
        className={cn(
          "h-full rounded-card border border-border p-5 shadow-soft transition-transform hover:-translate-y-0.5",
          fundo,
        )}
      >
        <p className={cn("text-xs font-medium uppercase tracking-wider", rotuloCor)}>
          {rotulo}
        </p>
        <p className="mt-2 font-display text-3xl leading-none">{valor}</p>
        {detalhe && <p className={cn("mt-2 text-xs", detalheCor)}>{detalhe}</p>}
      </div>
    </Link>
  );
}

export default async function InicioPage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const hoje = hojeNaClinica();
  const veFinanceiro = session.roles.some((r) => r === "OWNER" || r === "FINANCE");

  const [agendaHoje, vencidas, aReceber, estoque] = await Promise.all([
    tentar(
      apiFetch<Atendimento[]>(
        `/appointments?de=${encodeURIComponent(`${hoje}T00:00:00-03:00`)}&ate=${encodeURIComponent(`${hoje}T23:59:59-03:00`)}`,
      ),
      [],
    ),
    veFinanceiro
      ? tentar(apiFetch<Cobranca[]>("/payments?somenteVencidos=true"), [])
      : Promise.resolve([]),
    veFinanceiro
      ? tentar(apiFetch<Cobranca[]>("/payments?status=Pendente"), [])
      : Promise.resolve([]),
    tentar(apiFetch<ItemDeEstoque[]>("/stock"), []),
  ]);

  const ativos = agendaHoje.filter((a) => a.status !== "Cancelado");
  const aConfirmar = ativos.filter((a) => a.status === "Agendado");
  const emFalta = estoque.filter((i) => i.belowMinimum);

  const totalAReceber = aReceber.reduce((s, c) => s + c.amount, 0);
  const totalVencido = vencidas.reduce((s, c) => s + c.amount, 0);

  return (
    <AppShell usuario={session.user?.name ?? session.user?.email ?? "—"} papeis={session.roles}>
      <PageHeader
        titulo="Painel"
        descricao="O que precisa da sua atenção hoje."
      />

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <Indicador
          rotulo="Hoje"
          valor={String(ativos.length)}
          detalhe={
            aConfirmar.length > 0
              ? `${aConfirmar.length} sem confirmação`
              : "todos confirmados"
          }
          tom="primario"
          href="/agenda"
        />

        {veFinanceiro && (
          <>
            <Indicador
              rotulo="A receber"
              valor={reais(totalAReceber)}
              detalhe={`${aReceber.length} cobrança${aReceber.length === 1 ? "" : "s"}`}
              href="/financeiro?filtro=pendentes"
            />
            <Indicador
              rotulo="Vencidas"
              valor={reais(totalVencido)}
              detalhe={`${vencidas.length} em atraso`}
              tom={vencidas.length > 0 ? "alerta" : "bom"}
              href="/financeiro?filtro=vencidas"
            />
          </>
        )}

        <Indicador
          rotulo="Estoque"
          valor={String(emFalta.length)}
          detalhe={emFalta.length === 0 ? "tudo acima do mínimo" : "abaixo do mínimo"}
          tom={emFalta.length > 0 ? "alerta" : "bom"}
          href="/estoque"
        />
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader
            title="Agenda de hoje"
            description={ativos.length === 0 ? "Nenhum atendimento" : undefined}
          />
          <CardBody className="space-y-3">
            {ativos.length === 0 ? (
              <p className="text-sm text-ink-subtle">
                Dia livre. Aproveite para colocar a ficha das pacientes em dia.
              </p>
            ) : (
              ativos.slice(0, 8).map((a) => (
                <div key={a.id} className="flex items-center justify-between gap-3">
                  <div className="flex min-w-0 items-center gap-3">
                    <span className="w-12 shrink-0 font-display text-sm text-ink">
                      {hora(a.startsAt)}
                    </span>
                    <div className="min-w-0">
                      <p className="truncate text-sm text-ink">{a.patientName}</p>
                      <p className="truncate text-xs text-ink-subtle">
                        {a.procedureName} · {a.professionalName}
                      </p>
                    </div>
                  </div>
                  <Badge tone={a.status === "Confirmado" ? "primary" : "neutral"}>
                    {a.status}
                  </Badge>
                </div>
              ))
            )}
          </CardBody>
        </Card>

        {veFinanceiro ? (
          <Card>
            <CardHeader
              title="Cobranças vencidas"
              description={vencidas.length === 0 ? "Nada em atraso" : undefined}
            />
            <CardBody className="space-y-3">
              {vencidas.length === 0 ? (
                <p className="text-sm text-ink-subtle">
                  Nenhuma cobrança em atraso. É o estado que a gente quer.
                </p>
              ) : (
                vencidas.slice(0, 8).map((c) => (
                  <div key={c.id} className="flex items-center justify-between gap-3">
                    <div className="min-w-0">
                      <p className="truncate text-sm text-ink">{c.patientName}</p>
                      <p className="truncate text-xs text-ink-subtle">{c.procedureName}</p>
                    </div>
                    <span className="shrink-0 text-sm text-danger">{reais(c.amount)}</span>
                  </div>
                ))
              )}
            </CardBody>
          </Card>
        ) : (
          <Card>
            <CardHeader title="Estoque em falta" />
            <CardBody className="space-y-2">
              {emFalta.length === 0 ? (
                <p className="text-sm text-ink-subtle">Tudo acima do mínimo.</p>
              ) : (
                emFalta.map((i) => (
                  <div key={i.id} className="flex items-center justify-between gap-3">
                    <span className="text-sm text-ink">{i.name}</span>
                    <span className="text-xs text-warning">
                      {i.balance} {i.unit} · mínimo {i.minimumQuantity}
                    </span>
                  </div>
                ))
              )}
            </CardBody>
          </Card>
        )}
      </div>
    </AppShell>
  );
}
