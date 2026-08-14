import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Protocolo } from "@/lib/api";
import { cancelarProtocolo } from "@/lib/actions/protocolos";
import { cn } from "@/lib/cn";
import { reais } from "@/lib/formato";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, EmptyState } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

const TOM: Record<string, "neutral" | "primary" | "success" | "danger"> = {
  Proposto: "primary",
  Aprovado: "success",
  Recusado: "danger",
  Concluido: "neutral",
};

const ABAS = [
  { chave: "pendentes", rotulo: "Aguardando orçamento", status: "Proposto" },
  { chave: "ativos", rotulo: "Em andamento", status: "Aprovado" },
  { chave: "recusados", rotulo: "Recusados", status: "Recusado" },
  { chave: "cancelados", rotulo: "Cancelados", status: "Cancelado" },
] as const;

export default async function ProtocolosPage(props: PageProps<"/protocolos">) {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const params = await props.searchParams;
  const escolhida = typeof params.aba === "string" ? params.aba : "pendentes";
  const abaAtual = ABAS.find((a) => a.chave === escolhida) ?? ABAS[0];

  const protocolos = await apiFetch<Protocolo[]>(
    `/treatment-plans?status=${abaAtual.status}`,
  );

  const podePrescrever = session.roles.some((r) => r === "OWNER" || r === "DOCTOR");
  const podeOrcar = session.roles.some(
    (r) => r === "OWNER" || r === "SECRETARY" || r === "FINANCE",
  );

  return (
    <AppShell
      atual="/protocolos"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Protocolos"
        descricao={`${protocolos.length} ${protocolos.length === 1 ? "protocolo" : "protocolos"} · ${abaAtual.rotulo.toLowerCase()}`}
        acao={
          podePrescrever ? (
            <Link href="/protocolos/novo">
              <Button>Novo protocolo</Button>
            </Link>
          ) : undefined
        }
      />

      <div className="mb-5 flex flex-wrap gap-1 border-b border-border">
        {ABAS.map((a) => (
          <Link
            key={a.chave}
            href={`/protocolos?aba=${a.chave}`}
            className={cn(
              "-mb-px border-b-2 px-4 py-2.5 text-sm transition-colors",
              a.chave === abaAtual.chave
                ? "border-primary font-medium text-primary"
                : "border-transparent text-ink-muted hover:text-ink",
            )}
          >
            {a.rotulo}
          </Link>
        ))}
      </div>

      {protocolos.length === 0 ? (
        <EmptyState
          title="Nenhum protocolo ainda"
          description="Depois da avaliação, a doutora monta aqui o que a paciente precisa e em que ritmo."
          action={
            podePrescrever ? (
              <Link href="/protocolos/novo">
                <Button>Criar protocolo</Button>
              </Link>
            ) : undefined
          }
        />
      ) : (
        <div className="space-y-3">
          {protocolos.map((p) => {
            const aceitos = p.items.filter((i) => i.status !== "Recusado");
            const total = aceitos.reduce((s, i) => s + i.total, 0);

            return (
              <Card key={p.id} className="px-5 py-4">
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <p className="text-ink">{p.patientName}</p>
                      <Badge tone={TOM[p.status] ?? "neutral"}>{p.status}</Badge>
                    </div>

                    <p className="mt-0.5 text-sm text-ink-muted">{p.professionalName}</p>

                    <ul className="mt-2 space-y-0.5">
                      {p.items.map((i) => (
                        <li
                          key={i.id}
                          className={
                            i.status === "Recusado"
                              ? "text-xs text-ink-subtle line-through"
                              : "text-xs text-ink-muted"
                          }
                        >
                          {i.sessions}× {i.procedureName}
                          {i.intervalDays ? ` · a cada ${i.intervalDays} dias` : ""}
                          {i.startAfterDays > 0 ? ` · começa em ${i.startAfterDays} dias` : ""}
                        </li>
                      ))}
                    </ul>

                    {p.notes && (
                      <p className="mt-2 text-xs italic text-ink-subtle">{p.notes}</p>
                    )}
                  </div>

                  <div className="flex items-center gap-3">
                    <span className="font-display text-lg text-ink">{reais(total)}</span>

                    {p.status === "Proposto" && podeOrcar && (
                      <Link href={`/protocolos/${p.id}/orcamento`}>
                        <Button size="sm">Fechar orçamento</Button>
                      </Link>
                    )}

                    {/* Cancelar derruba as cobranças pendentes junto; o que já foi
                        pago fica, porque devolver dinheiro é decisão do financeiro. */}
                    {(p.status === "Proposto" || p.status === "Aprovado") && podeOrcar && (
                      <form action={cancelarProtocolo}>
                        <input type="hidden" name="protocolo" value={p.id} />
                        <Button size="sm" variant="ghost" type="submit">
                          Cancelar
                        </Button>
                      </form>
                    )}
                  </div>
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </AppShell>
  );
}
