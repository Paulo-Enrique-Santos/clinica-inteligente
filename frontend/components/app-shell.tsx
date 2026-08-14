import { sair } from "@/lib/actions";
import { Button } from "./ui/button";
import { Sidebar } from "./sidebar";

/** "Teste Doutora 2" -> "Teste". Nome de login vira nome de gente. */
function primeiroNome(usuario: string) {
  const limpo = usuario.includes("@") ? usuario.split("@")[0] : usuario;
  const primeiro = limpo.replace(/[._]/g, " ").trim().split(/\s+/)[0] ?? limpo;
  return primeiro.charAt(0).toUpperCase() + primeiro.slice(1);
}

/**
 * Casca do sistema: navegação lateral fixa e área de conteúdo.
 *
 * A prop `atual` sobrou da navegação horizontal — quem marca o item ativo agora é o
 * próprio caminho da URL, dentro da Sidebar. Mantida para não mexer em dez telas de uma
 * vez; sai na próxima limpeza.
 */
export function AppShell({
  usuario,
  papeis,
  children,
}: {
  atual?: string;
  usuario: string;
  papeis: string[];
  children: React.ReactNode;
}) {
  return (
    <div className="flex min-h-full flex-1 bg-surface">
      <Sidebar papeis={papeis} />

      {/* lg:pl-64 abre espaço para a barra fixa; no mobile ela fica sobreposta. */}
      <div className="flex min-w-0 flex-1 flex-col lg:pl-64">
        <header className="border-b border-border bg-canvas">
          <div className="flex h-14 items-center justify-end gap-4 px-4 sm:px-6">
            <p className="text-sm text-ink-muted">
              Olá, <span className="text-ink">{primeiroNome(usuario)}</span>
            </p>

            <form action={sair}>
              <Button variant="secondary" size="sm" type="submit">
                Sair
              </Button>
            </form>
          </div>
        </header>

        <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6 sm:px-6 sm:py-8">
          {children}
        </main>
      </div>
    </div>
  );
}

export function PageHeader({
  titulo,
  descricao,
  acao,
}: {
  titulo: string;
  descricao?: string;
  acao?: React.ReactNode;
}) {
  return (
    // No celular o botão de ação desce para baixo do título, em vez de espremer os dois.
    <div className="mb-6 flex flex-col gap-3 sm:mb-7 sm:flex-row sm:items-end sm:justify-between sm:gap-6">
      <div>
        <h1 className="text-2xl text-ink">{titulo}</h1>
        {descricao && <p className="mt-1.5 text-sm text-ink-muted">{descricao}</p>}
      </div>
      {acao}
    </div>
  );
}
