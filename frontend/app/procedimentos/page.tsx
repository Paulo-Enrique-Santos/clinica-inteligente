import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Procedimento } from "@/lib/api";
import { duracao, reais } from "@/lib/formato";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, EmptyState } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

export default async function ProcedimentosPage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const procedimentos = await apiFetch<Procedimento[]>("/procedures");
  const podeCadastrar = session.roles.includes("OWNER");

  // A API já omite custo e margem para quem não pode vê-los; a tela apenas não
  // desenha colunas vazias.
  const mostraMargem = procedimentos.some((p) => p.margin !== null);

  return (
    <AppShell
      atual="/procedimentos"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Procedimentos"
        descricao="Duração e preço definem a agenda e a margem de cada atendimento."
        acao={
          podeCadastrar ? (
            <Link href="/procedimentos/novo">
              <Button>Novo procedimento</Button>
            </Link>
          ) : undefined
        }
      />

      {procedimentos.length === 0 ? (
        <EmptyState
          title="Nenhum procedimento cadastrado"
          description="Cadastre os procedimentos da clínica para conseguir montar a agenda."
          action={
            podeCadastrar ? (
              <Link href="/procedimentos/novo">
                <Button>Cadastrar procedimento</Button>
              </Link>
            ) : undefined
          }
        />
      ) : (
        <Card className="overflow-hidden">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-border">
                <th className="px-5 py-3 text-xs font-medium uppercase tracking-wide text-ink-subtle">
                  Procedimento
                </th>
                <th className="px-5 py-3 text-xs font-medium uppercase tracking-wide text-ink-subtle">
                  Duração
                </th>
                <th className="px-5 py-3 text-right text-xs font-medium uppercase tracking-wide text-ink-subtle">
                  Preço
                </th>
                {mostraMargem && (
                  <>
                    <th className="px-5 py-3 text-right text-xs font-medium uppercase tracking-wide text-ink-subtle">
                      Insumos
                    </th>
                    <th className="px-5 py-3 text-right text-xs font-medium uppercase tracking-wide text-ink-subtle">
                      Margem
                    </th>
                  </>
                )}
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {procedimentos.map((p) => (
                <tr key={p.id} className="transition-colors hover:bg-surface">
                  <td className="px-5 py-3.5 text-ink">{p.name}</td>
                  <td className="px-5 py-3.5 text-ink-muted">{duracao(p.durationMinutes)}</td>
                  <td className="px-5 py-3.5 text-right text-ink">{reais(p.price)}</td>
                  {mostraMargem && (
                    <>
                      <td className="px-5 py-3.5 text-right text-ink-muted">
                        {reais(p.suppliesCost ?? 0)}
                      </td>
                      <td className="px-5 py-3.5 text-right">
                        {/* Margem é o que a Fase 13 vai usar para responder qual
                            procedimento realmente compensa. */}
                        <Badge tone={(p.margin ?? 0) > 0 ? "success" : "danger"}>
                          {reais(p.margin ?? 0)}
                        </Badge>
                      </td>
                    </>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </AppShell>
  );
}
