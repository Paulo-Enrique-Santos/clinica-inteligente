import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Protocolo } from "@/lib/api";
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

export default async function ProtocolosPage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const protocolos = await apiFetch<Protocolo[]>("/treatment-plans");

  const podePrescrever = session.roles.some((r) => r === "OWNER" || r === "DOCTOR");
  const podeOrcar = session.roles.some(
    (r) => r === "OWNER" || r === "SECRETARY" || r === "FINANCE",
  );

  const aguardando = protocolos.filter((p) => p.status === "Proposto").length;

  return (
    <AppShell
      atual="/protocolos"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Protocolos"
        descricao={
          aguardando > 0
            ? `${aguardando} aguardando orçamento`
            : "Prescrições e tratamentos das pacientes"
        }
        acao={
          podePrescrever ? (
            <Link href="/protocolos/novo">
              <Button>Novo protocolo</Button>
            </Link>
          ) : undefined
        }
      />

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
