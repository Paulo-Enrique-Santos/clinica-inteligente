import Image from "next/image";
import Link from "next/link";
import { sair } from "@/lib/actions";
import { Button } from "./ui/button";
import { cn } from "@/lib/cn";

/**
 * Casca do sistema: marca, navegação e identificação de quem está logado.
 *
 * As seções ainda não construídas aparecem esmaecidas em vez de escondidas —
 * a equipe da clínica enxerga para onde o sistema vai, e ninguém clica num link
 * que leva a 404.
 */
const SECOES = [
  { href: "/agenda", label: "Agenda", pronto: true, papeis: null },
  { href: "/pacientes", label: "Pacientes", pronto: true, papeis: null },
  { href: "/procedimentos", label: "Procedimentos", pronto: true, papeis: null },
  // Financeiro e Configurações somem para quem não pode entrar: mostrar link que
  // leva a "sem acesso" é convidar a pessoa a bater na porta fechada todo dia.
  { href: "/financeiro", label: "Financeiro", pronto: true, papeis: ["OWNER", "FINANCE"] },
  { href: "/estoque", label: "Estoque", pronto: true, papeis: null },
  { href: "/configuracoes", label: "Configurações", pronto: true, papeis: ["OWNER"] },
];

export function AppShell({
  atual,
  usuario,
  papeis,
  children,
}: {
  atual: string;
  usuario: string;
  papeis: string[];
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-1 flex-col bg-surface">
      <header className="border-b border-border bg-canvas">
        <div className="mx-auto flex h-16 max-w-5xl items-center justify-between gap-6 px-6">
          <div className="flex items-center gap-8">
            <Link href="/pacientes" className="flex items-center gap-2.5">
              {/* Símbolo é decorativo (alt vazio) porque o nome vem na imagem ao
                  lado — leitor de tela anunciando duas vezes é ruído. */}
              <Image
                src="/cliniq-mark.png"
                alt=""
                width={140}
                height={157}
                priority
                className="h-7 w-auto"
              />
              {/* O lettering vem do próprio logo, não da fonte: a estrela dentro
                  do Q e o floreio embaixo fazem parte do desenho da marca. */}
              <Image
                src="/cliniq-wordmark.png"
                alt="CLINIQ"
                width={300}
                height={68}
                priority
                className="h-5 w-auto"
              />
            </Link>

            <nav className="hidden items-center gap-1 sm:flex">
              {SECOES.filter(
                (secao) => secao.papeis === null || secao.papeis.some((p) => papeis.includes(p)),
              ).map((secao) =>
                secao.pronto ? (
                  <Link
                    key={secao.href}
                    href={secao.href}
                    className={cn(
                      "rounded-control px-3 py-1.5 text-sm transition-colors",
                      atual === secao.href
                        ? "bg-primary-soft font-medium text-primary"
                        : "text-ink-muted hover:bg-surface-muted hover:text-ink",
                    )}
                  >
                    {secao.label}
                  </Link>
                ) : (
                  <span
                    key={secao.href}
                    className="cursor-default px-3 py-1.5 text-sm text-ink-subtle"
                    title="Em construção"
                  >
                    {secao.label}
                  </span>
                ),
              )}
            </nav>
          </div>

          <div className="flex items-center gap-4">
            <div className="hidden text-right sm:block">
              <p className="text-sm text-ink">{usuario}</p>
              {papeis.length > 0 && (
                <p className="text-xs text-ink-subtle">{papeis.join(" · ")}</p>
              )}
            </div>
            <form action={sair}>
              <Button variant="secondary" size="sm" type="submit">
                Sair
              </Button>
            </form>
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">{children}</main>
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
    <div className="mb-7 flex items-end justify-between gap-6">
      <div>
        <h1 className="text-2xl text-ink">{titulo}</h1>
        {descricao && <p className="mt-1.5 text-sm text-ink-muted">{descricao}</p>}
      </div>
      {acao}
    </div>
  );
}
