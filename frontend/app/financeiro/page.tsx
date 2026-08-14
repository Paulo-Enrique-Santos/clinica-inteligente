import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Cobranca } from "@/lib/api";
import { data, reais } from "@/lib/formato";
import { formatarTelefone } from "@/lib/telefone";
import { darBaixa, estornarCobranca } from "@/lib/actions/financeiro";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardHeader, CardBody, EmptyState } from "@/components/ui/card";
import { GraficoFinanceiro } from "@/components/grafico-financeiro";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input, Select } from "@/components/ui/field";
import { cn } from "@/lib/cn";

const FILTROS = [
  { chave: "pendentes", rotulo: "A receber", query: "status=Pendente" },
  { chave: "vencidas", rotulo: "Vencidas", query: "somenteVencidos=true" },
  { chave: "pagas", rotulo: "Recebidas", query: "status=Pago" },
  { chave: "estornadas", rotulo: "Estornadas", query: "status=Estornado" },
] as const;

type Resumo = {
  recebido: number;
  pendente: number;
  vencido: number;
  estornado: number;
  serie: { mes: string; recebido: number; aReceber: number }[];
};

export default async function FinanceiroPage(props: PageProps<"/financeiro">) {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  // Financeiro é o dado mais sensível depois do prontuário: a secretária não entra.
  if (!session.roles.some((r) => r === "OWNER" || r === "FINANCE")) {
    redirect("/inicio");
  }

  const params = await props.searchParams;
  const escolhido = typeof params.filtro === "string" ? params.filtro : "pendentes";
  const filtro = FILTROS.find((f) => f.chave === escolhido) ?? FILTROS[0];

  const [resumo, cobrancas] = await Promise.all([
    apiFetch<Resumo>("/payments/resumo"),
    apiFetch<Cobranca[]>(`/payments?${filtro.query}`),
  ]);

  return (
    <AppShell usuario={session.user?.name ?? session.user?.email ?? "—"} papeis={session.roles}>
      <PageHeader titulo="Financeiro" descricao="Entradas, pendências e histórico." />

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {[
          { rotulo: "Recebido", valor: resumo.recebido, tom: "bg-success-soft text-success" },
          { rotulo: "A receber", valor: resumo.pendente, tom: "bg-canvas text-ink" },
          { rotulo: "Vencido", valor: resumo.vencido, tom: "bg-warning-soft text-warning" },
          { rotulo: "Estornado", valor: resumo.estornado, tom: "bg-danger-soft text-danger" },
        ].map((c) => (
          <div key={c.rotulo} className={cn("rounded-card border border-border p-5 shadow-soft", c.tom)}>
            <p className="text-xs font-medium uppercase tracking-wider opacity-70">{c.rotulo}</p>
            <p className="mt-2 font-display text-2xl leading-none">{reais(c.valor)}</p>
          </div>
        ))}
      </div>

      <Card className="mt-6">
        <CardHeader title="Últimos seis meses" />
        <CardBody>
          <GraficoFinanceiro serie={resumo.serie} />
        </CardBody>
      </Card>

      <div className="mb-5 mt-8 flex flex-wrap gap-1">
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
        <EmptyState title="Nada por aqui" description="Nenhuma cobrança neste filtro." />
      ) : (
        <div className="space-y-3">
          {cobrancas.map((c) => (
            <Card key={c.id} className="px-5 py-4">
              <div className="flex flex-wrap items-center justify-between gap-4">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-ink">{c.patientName}</p>
                    {c.overdue && <Badge tone="danger">Vencida</Badge>}
                    {c.status === "Pago" && <Badge tone="success">Recebida</Badge>}
                    {c.status === "Estornado" && <Badge tone="danger">Estornada</Badge>}
                  </div>
                  <p className="mt-0.5 text-sm text-ink-muted">
                    {c.procedureName} · vence {data(c.dueDate)}
                  </p>
                  <p className="mt-1 text-xs text-ink-subtle">
                    {c.patientPhone ? formatarTelefone(c.patientPhone) : "—"}
                    {c.method && ` · ${c.method}`}
                  </p>
                </div>

                <div className="flex flex-wrap items-center gap-3">
                  <span className="font-display text-lg text-ink">{reais(c.amount)}</span>

                  {c.status === "Pendente" && (
                    <form action={darBaixa} className="flex items-center gap-2">
                      <input type="hidden" name="id" value={c.id} />
                      <input type="hidden" name="filtro" value={filtro.chave} />
                      <Select name="metodo" defaultValue="Pix" className="h-8 w-28 py-0 text-sm">
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

                  {c.status === "Pago" && (
                    <form action={estornarCobranca} className="flex items-center gap-2">
                      <input type="hidden" name="id" value={c.id} />
                      <input type="hidden" name="filtro" value={filtro.chave} />
                      <Input
                        name="motivo"
                        placeholder="Motivo do estorno"
                        className="h-8 w-44 py-0 text-sm"
                      />
                      <Button type="submit" size="sm" variant="ghost">
                        Estornar
                      </Button>
                    </form>
                  )}
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}

      <p className="mt-6 max-w-2xl text-xs leading-relaxed text-ink-subtle">
        Dinheiro e cartão entram como recebidos no fechamento do orçamento — o dinheiro já
        passou. Só o PIX parcelado fica pendente de baixa. Quando algo volta (cartão
        recusado, chargeback), use estornar: a cobrança fica marcada como estornada e a
        data do pagamento é preservada, para conciliar com a maquininha.
      </p>
    </AppShell>
  );
}
