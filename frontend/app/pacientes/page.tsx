import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Paciente } from "@/lib/api";
import { formatarTelefone } from "@/lib/telefone";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, EmptyState } from "@/components/ui/card";
import { Button } from "@/components/ui/button";

function iniciais(nome: string) {
  return nome
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase())
    .join("");
}

export default async function PacientesPage() {
  const session = await auth();

  // A checagem de sessão acontece aqui, no servidor, e não num middleware/proxy.
  // Middleware roda antes e sem acesso a tudo, servindo como otimização — a
  // verificação que vale é a que fica junto do dado.
  if (!session || session.error) {
    redirect("/");
  }

  const pacientes = await apiFetch<Paciente[]>("/patients");
  const podeCadastrar = session.roles.some((r) => r === "OWNER" || r === "SECRETARY");

  return (
    <AppShell
      atual="/pacientes"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Pacientes"
        descricao={
          pacientes.length === 1
            ? "1 paciente cadastrada"
            : `${pacientes.length} pacientes cadastradas`
        }
        acao={
          podeCadastrar ? (
            <Link href="/pacientes/nova">
              <Button>Nova paciente</Button>
            </Link>
          ) : undefined
        }
      />

      {pacientes.length === 0 ? (
        <EmptyState
          title="Nenhuma paciente ainda"
          description="Assim que a recepção cadastrar a primeira ficha, ela aparece aqui."
          action={
            podeCadastrar ? (
              <Link href="/pacientes/nova">
                <Button>Cadastrar paciente</Button>
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
                  Paciente
                </th>
                <th className="px-5 py-3 text-xs font-medium uppercase tracking-wide text-ink-subtle">
                  Telefone
                </th>
                <th className="px-5 py-3 text-xs font-medium uppercase tracking-wide text-ink-subtle">
                  Cadastro
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {pacientes.map((p) => (
                <tr key={p.id} className="transition-colors hover:bg-surface">
                  <td className="px-5 py-3.5">
                    <div className="flex items-center gap-3">
                      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary-soft font-display text-xs text-primary">
                        {iniciais(p.fullName)}
                      </span>
                      <span className="text-ink">{p.fullName}</span>
                    </div>
                  </td>
                  <td className="px-5 py-3.5 text-ink-muted">
                    {formatarTelefone(p.phoneE164)}
                  </td>
                  <td className="px-5 py-3.5 text-ink-subtle">
                    {new Date(p.createdAt).toLocaleDateString("pt-BR")}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </AppShell>
  );
}
