import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { sair } from "@/lib/actions";
import { apiFetch, type Paciente } from "@/lib/api";

function formatarTelefone(e164: string) {
  // +5511987654321 -> (11) 98765-4321
  const m = e164.match(/^\+55(\d{2})(\d{4,5})(\d{4})$/);
  return m ? `(${m[1]}) ${m[2]}-${m[3]}` : e164;
}

export default async function PacientesPage() {
  const session = await auth();

  // A checagem de sessao acontece aqui, no servidor, e nao num middleware/proxy.
  // Middleware roda antes e sem acesso a tudo, servindo como otimizacao — a verificacao
  // que vale e a que fica junto do dado.
  if (!session || session.error) {
    redirect("/");
  }

  const pacientes = await apiFetch<Paciente[]>("/patients");

  return (
    <div className="flex flex-1 flex-col bg-zinc-50 dark:bg-zinc-950">
      <header className="border-b border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto flex max-w-4xl items-center justify-between px-6 py-4">
          <div>
            <h1 className="font-semibold text-zinc-900 dark:text-zinc-50">Pacientes</h1>
            <p className="mt-0.5 text-xs text-zinc-500">
              {session.user?.name ?? session.user?.email}
              {session.roles.length > 0 && ` · ${session.roles.join(", ")}`}
              {session.tenantId && ` · clínica ${session.tenantId.slice(0, 8)}`}
            </p>
          </div>

          <form action={sair}>
            <button
              type="submit"
              className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm text-zinc-700 transition-colors hover:bg-zinc-100 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              Sair
            </button>
          </form>
        </div>
      </header>

      <main className="mx-auto w-full max-w-4xl flex-1 px-6 py-8">
        {pacientes.length === 0 ? (
          <p className="rounded-lg border border-dashed border-zinc-300 p-8 text-center text-sm text-zinc-500 dark:border-zinc-700">
            Nenhum paciente cadastrado nesta clínica.
          </p>
        ) : (
          <div className="overflow-x-auto rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-zinc-200 text-xs uppercase tracking-wide text-zinc-500 dark:border-zinc-800">
                <tr>
                  <th className="px-4 py-3 font-medium">Nome</th>
                  <th className="px-4 py-3 font-medium">Telefone</th>
                  <th className="px-4 py-3 font-medium">Cadastro</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {pacientes.map((p) => (
                  <tr key={p.id}>
                    <td className="px-4 py-3 text-zinc-900 dark:text-zinc-100">
                      {p.fullName}
                    </td>
                    <td className="px-4 py-3 text-zinc-600 dark:text-zinc-400">
                      {formatarTelefone(p.phoneE164)}
                    </td>
                    <td className="px-4 py-3 text-zinc-500">
                      {new Date(p.createdAt).toLocaleDateString("pt-BR")}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <p className="mt-6 text-xs text-zinc-500">
          Esta lista vem da API com o filtro por clínica aplicado no servidor e no banco.
          Saia e entre como outro usuário para ver outro conjunto de pacientes.
        </p>
      </main>
    </div>
  );
}
