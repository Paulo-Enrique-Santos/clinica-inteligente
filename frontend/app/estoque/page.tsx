import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type ItemDeEstoque } from "@/lib/api";
import { abrirEmbalagem, movimentarEstoque } from "@/lib/actions/estoque";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, EmptyState } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input, Select } from "@/components/ui/field";

/** "3 ml" sem casas inúteis: 3, não 3,000. */
function numero(v: number) {
  return v.toLocaleString("pt-BR", { maximumFractionDigits: 3 });
}

/**
 * Como o saldo é lido em voz alta.
 *
 * Quem compra pensa em frasco; quem aplica pensa em ml. A tela mostra as duas leituras
 * porque as duas pessoas usam a mesma linha.
 */
function saldoPorExtenso(i: ItemDeEstoque) {
  if (i.contentPerUnit === 1) {
    return `${numero(i.balance)} ${i.unit}`;
  }

  if (i.controlMode === "PorAbertura") {
    return `${numero(i.closedPackages)} ${i.purchaseUnit}${i.closedPackages === 1 ? "" : "s"} fechada${i.closedPackages === 1 ? "" : "s"}`;
  }

  const partes = [`${numero(i.balance)} ${i.unit}`];

  if (i.closedPackages > 0) {
    partes.push(`${i.closedPackages} ${i.purchaseUnit}${i.closedPackages === 1 ? "" : "s"} fechado${i.closedPackages === 1 ? "" : "s"}`);
  }

  if (i.openRemainder > 0) {
    partes.push(`${numero(i.openRemainder)} ${i.unit} em aberto`);
  }

  return partes.join(" · ");
}

export default async function EstoquePage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const itens = await apiFetch<ItemDeEstoque[]>("/stock");
  const podeCadastrar = session.roles.some((r) => r === "OWNER" || r === "FINANCE");
  const emFalta = itens.filter((i) => i.belowMinimum);

  return (
    <AppShell usuario={session.user?.name ?? session.user?.email ?? "—"} papeis={session.roles}>
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
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-ink">{i.name}</p>
                    {i.belowMinimum && <Badge tone="warning">Abaixo do mínimo</Badge>}
                    {i.controlMode === "PorAbertura" && (
                      <Badge tone="neutral">Baixa na abertura</Badge>
                    )}
                  </div>

                  <p className="mt-0.5 text-sm text-ink-muted">{saldoPorExtenso(i)}</p>

                  <p className="mt-0.5 text-xs text-ink-subtle">
                    1 {i.purchaseUnit} = {numero(i.contentPerUnit)} {i.unit} · mínimo{" "}
                    {numero(i.minimumQuantity)} {i.unit}
                  </p>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  {/* Abrir embalagem é o gesto do dia a dia para creme e luva; para os
                      demais, a saída vem do fechamento do atendimento. */}
                  {i.controlMode === "PorAbertura" && (
                    <form action={abrirEmbalagem}>
                      <input type="hidden" name="item" value={i.id} />
                      <Button type="submit" size="sm" variant="secondary">
                        Abrir {i.purchaseUnit}
                      </Button>
                    </form>
                  )}

                  <form action={movimentarEstoque} className="flex items-center gap-2">
                    <input type="hidden" name="item" value={i.id} />
                    <Select name="tipo" defaultValue="Entrada" className="h-8 w-24 py-0 text-sm">
                      <option value="Entrada">Entrada</option>
                      <option value="Saida">Saída</option>
                    </Select>
                    <Input
                      name="quantidade"
                      inputMode="decimal"
                      placeholder="Qtd."
                      required
                      className="h-8 w-20 py-0 text-sm"
                    />
                    <Button type="submit" size="sm" variant="ghost">
                      Registrar
                    </Button>
                  </form>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}

      <p className="mt-6 max-w-2xl text-xs leading-relaxed text-ink-subtle">
        Entrada é contada em embalagens (2 frascos), saída em conteúdo (7 ml). O saldo é
        somado das movimentações a cada consulta — não existe coluna de saldo para ficar
        desatualizada. A divisão entre embalagens fechadas e sobra aberta é aproximação:
        com várias profissionais, a sobra pode estar espalhada em mais de um frasco.
      </p>
    </AppShell>
  );
}
