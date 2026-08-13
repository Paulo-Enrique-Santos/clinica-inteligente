import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type MembroDaEquipe } from "@/lib/api";
import { MATRIZ, PAPEIS, ROTULO_DE_ACESSO, TOM_DE_ACESSO, type Papel } from "@/lib/permissoes";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardHeader, CardBody, EmptyState } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

const NOME_DO_PAPEL = Object.fromEntries(PAPEIS.map((p) => [p.chave, p.nome]));

export default async function ConfiguracoesPage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  // Só a dona administra a equipe. A restrição de verdade está no backend; isto aqui
  // evita mostrar uma tela que a pessoa não conseguiria usar.
  if (!session.roles.includes("OWNER")) {
    redirect("/agenda");
  }

  const equipe = await apiFetch<MembroDaEquipe[]>("/team");

  return (
    <AppShell
      atual="/configuracoes"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Configurações"
        descricao="Equipe da clínica e o que cada papel acessa."
        acao={
          <Link href="/configuracoes/nova-conta">
            <Button>Nova conta</Button>
          </Link>
        }
      />

      <Card className="mb-8 overflow-hidden">
        <CardHeader
          title="Equipe"
          description={`${equipe.length} ${equipe.length === 1 ? "pessoa com acesso" : "pessoas com acesso"}`}
        />

        {equipe.length === 0 ? (
          <CardBody>
            <EmptyState
              title="Ninguém além de você"
              description="Crie contas para as doutoras, a recepção e o financeiro."
              action={
                <Link href="/configuracoes/nova-conta">
                  <Button>Criar primeira conta</Button>
                </Link>
              }
            />
          </CardBody>
        ) : (
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-border">
                <th className="px-5 py-3 text-xs font-medium uppercase tracking-wide text-ink-subtle">
                  Pessoa
                </th>
                <th className="px-5 py-3 text-xs font-medium uppercase tracking-wide text-ink-subtle">
                  Usuário
                </th>
                <th className="px-5 py-3 text-xs font-medium uppercase tracking-wide text-ink-subtle">
                  Papel
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {equipe.map((m) => (
                <tr key={m.id} className="transition-colors hover:bg-surface">
                  <td className="px-5 py-3.5 text-ink">
                    {[m.firstName, m.lastName].filter(Boolean).join(" ") || "—"}
                    {!m.enabled && (
                      <Badge tone="danger" className="ml-2">
                        Desativada
                      </Badge>
                    )}
                  </td>
                  <td className="px-5 py-3.5 font-mono text-xs text-ink-muted">
                    {m.username}
                  </td>
                  <td className="px-5 py-3.5">
                    {m.roles.length === 0 ? (
                      <span className="text-ink-subtle">—</span>
                    ) : (
                      m.roles.map((r) => (
                        <Badge key={r} tone="primary" className="mr-1">
                          {NOME_DO_PAPEL[r] ?? r}
                        </Badge>
                      ))
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      <Card className="overflow-hidden">
        <CardHeader
          title="O que cada papel acessa"
          description="Definido no sistema; não é editável por aqui."
        />

        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-border">
                <th className="px-5 py-3 text-xs font-medium uppercase tracking-wide text-ink-subtle">
                  Área
                </th>
                {PAPEIS.map((p) => (
                  <th
                    key={p.chave}
                    className="px-5 py-3 text-xs font-medium uppercase tracking-wide text-ink-subtle"
                  >
                    {p.nome}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {MATRIZ.map((linha) => (
                <tr key={linha.area}>
                  <td className="px-5 py-3.5 text-ink">{linha.area}</td>
                  {PAPEIS.map((p) => {
                    const acesso = linha.acesso[p.chave as Papel];
                    return (
                      <td key={p.chave} className="px-5 py-3.5">
                        <Badge tone={TOM_DE_ACESSO[acesso]}>{ROTULO_DE_ACESSO[acesso]}</Badge>
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <p className="mt-6 text-xs leading-relaxed text-ink-subtle">
        Ao criar uma doutora, o sistema também cria a ficha de profissional dela e a liga
        ao login — é isso que faz a agenda dela funcionar desde o primeiro acesso.
      </p>
    </AppShell>
  );
}
