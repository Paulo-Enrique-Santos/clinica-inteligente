import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Cobranca } from "@/lib/api";
import { data, reais } from "@/lib/formato";
import { formatarTelefone } from "@/lib/telefone";
import { darBaixa } from "@/lib/actions/financeiro";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, EmptyState } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Select } from "@/components/ui/field";
import { cn } from "@/lib/cn";

const FILTROS = [
  { chave: "pendentes", rotulo: "Pendentes", query: "status=Pendente" },
  { chave: "vencidas", rotulo: "Vencidas", query: "somenteVencidos=true" },
  { chave: "pagas", rotulo: "Recebidas", query: "status=Pago" },
] as const;

export default async function FinanceiroPage(props: PageProps<"/financeiro">) {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  // Financeiro é o dado mais sensível depois do prontuário: a secretária não entra.
  if (!session.roles.some((r) => r === "OWNER" || r === "FINANCE")) {
    redirect("/agenda");
  }

  const params = await props.searchParams;
  const escolhido = typeof params.filtro === "string" ? params.filtro : "pendentes";
  const filtro = FILTROS.find((f) => f.chave === escolhido) ?? FILTROS[0];

  const cobrancas = await apiFetch<Cobranca[]>(`/payments?${filtro.query}`);
  const total = cobrancas.reduce((soma, c) => soma + c.amount, 0);

  return (
    <AppShell
      atual="/financeiro"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Financeiro"
        descricao={`${cobrancas.length} cobrança${cobrancas.length === 1 ? "" : "s"} · ${reais(total)}`}
      />

      <div className="mb-5 flex gap-1">
        {FILTROS.map((f) => (
          <Link
            key={f.chave}
            href={`/financeiro?filtro=${f.chave}`}
            className={cn(
              "rounded-control px-3 py-1.5 text-sm transition-colors",
              f.chave === filtro.chave
                ? "bg-primary-soft font-medium text-primary"
                : "text-ink-muted hover:bg-surface-muted hover:text-ink",
            )}
          >
            {f.rotulo}
          </Link>
        ))}
      </div>

      {cobrancas.length === 0 ? (
        <EmptyState
          title="Nada por aqui"
          description="Nenhuma cobrança neste filtro."
        />
      ) : (
        <div className="space-y-3">
          {cobrancas.map((c) => (
            <Card key={c.id} className="px-5 py-4">
              <div className="flex flex-wrap items-center justify-between gap-4">
                <div>
                  <div className="flex items-center gap-2">
                    <p className="text-ink">{c.patientName}</p>
                    {c.overdue && <Badge tone="danger">Vencida</Badge>}
                    {c.status === "Pago" && <Badge tone="success">Recebida</Badge>}
                  </div>
                  <p className="mt-0.5 text-sm text-ink-muted">
                    {c.procedureName} · vence {data(c.dueDate)}
                  </p>
                  <p className="mt-1 text-xs text-ink-subtle">
                    {formatarTelefone(c.patientPhone)}
                    {c.method && ` · pago em ${c.method}`}
                  </p>
                </div>

                <div className="flex items-center gap-3">
                  <span className="font-display text-lg text-ink">{reais(c.amount)}</span>

                  {c.status === "Pendente" && (
                    <form action={darBaixa} className="flex items-center gap-2">
                      <input type="hidden" name="id" value={c.id} />
                      <input type="hidden" name="filtro" value={filtro.chave} />
                      <Select name="metodo" defaultValue="Pix" className="h-8 w-32 py-0 text-sm">
                        <option value="Pix">Pix</option>
                        <option value="Dinheiro">Dinheiro</option>
                        <option value="Credito">Crédito</option>
                        <option value="Debito">Débito</option>
                        <option value="Transferencia">Transferência</option>
                      </Select>
                      <Button type="submit" size="sm">
                        Dar baixa
                      </Button>
                    </form>
                  )}
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}

      <p className="mt-6 text-xs text-ink-subtle">
        &quot;Vencida&quot; é calculado na hora a partir da data de vencimento, não gravado
        no banco — nenhuma rotina precisa rodar de madrugada para esta lista estar certa.
      </p>
    </AppShell>
  );
}
