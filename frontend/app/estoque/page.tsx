import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type ItemDeEstoque } from "@/lib/api";
import { movimentarEstoque } from "@/lib/actions/estoque";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, EmptyState } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input, Select } from "@/components/ui/field";

export default async function EstoquePage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const itens = await apiFetch<ItemDeEstoque[]>("/stock");
  const podeCadastrar = session.roles.some((r) => r === "OWNER" || r === "FINANCE");
  const emFalta = itens.filter((i) => i.belowMinimum);

  return (
    <AppShell
      atual="/estoque"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Estoque"
        descricao={
          emFalta.length > 0
            ? `${emFalta.length} ${emFalta.length === 1 ? "item abaixo do mínimo" : "itens abaixo do mínimo"}`
            : "Todos os itens acima do mínimo"
        }
        acao={
          podeCadastrar ? (
            <Link href="/estoque/novo">
              <Button>Novo item</Button>
            </Link>
          ) : undefined
        }
      />

      {itens.length === 0 ? (
        <EmptyState
          title="Nenhum item cadastrado"
          description="Cadastre os insumos que a clínica controla para acompanhar saldo e mínimo."
          action={
            podeCadastrar ? (
              <Link href="/estoque/novo">
                <Button>Cadastrar item</Button>
              </Link>
            ) : undefined
          }
        />
      ) : (
        <div className="space-y-3">
          {itens.map((i) => (
            <Card key={i.id} className="px-5 py-4">
              <div className="flex flex-wrap items-center justify-between gap-4">
                <div>
                  <div className="flex items-center gap-2">
                    <p className="text-ink">{i.name}</p>
                    {i.belowMinimum && <Badge tone="warning">Abaixo do mínimo</Badge>}
                  </div>
                  <p className="mt-0.5 text-sm text-ink-muted">
                    Saldo{" "}
                    <span className={i.belowMinimum ? "text-warning" : "text-ink"}>
                      {i.balance} {i.unit}
                    </span>{" "}
                    · mínimo {i.minimumQuantity} {i.unit}
                  </p>
                </div>

                {/* Movimentar é o gesto mais frequente da tela, então fica na linha do
                    item em vez de escondido atrás de outra navegação. */}
                <form action={movimentarEstoque} className="flex items-center gap-2">
                  <input type="hidden" name="item" value={i.id} />
                  <Select name="tipo" defaultValue="Entrada" className="h-8 w-28 py-0 text-sm">
                    <option value="Entrada">Entrada</option>
                    <option value="Saida">Saída</option>
                  </Select>
                  <Input
                    name="quantidade"
                    inputMode="decimal"
                    placeholder="Qtd."
                    required
                    className="h-8 w-24 py-0 text-sm"
                  />
                  <Button type="submit" size="sm" variant="secondary">
                    Registrar
                  </Button>
                </form>
              </div>
            </Card>
          ))}
        </div>
      )}

      <p className="mt-6 text-xs text-ink-subtle">
        O saldo é somado das movimentações a cada consulta. Não existe coluna de saldo para
        ficar desatualizada.
      </p>
    </AppShell>
  );
}
