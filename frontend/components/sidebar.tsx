"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { cn } from "@/lib/cn";

type Item = { href: string; label: string; papeis?: string[] };
type Grupo = { titulo: string; icone: string; itens: Item[] };

/**
 * Navegação do sistema.
 *
 * Lateral, e não no topo: a clínica usa iPad e computador, onde sobra largura e falta
 * altura. E o menu horizontal quebrava assim que passou de cinco seções — com onze, não
 * havia arranjo que coubesse.
 *
 * Os ícones são desenhados em SVG inline em vez de virem de uma biblioteca: são nove
 * formas simples, e uma dependência de ícones custaria mais do que resolve.
 */
const GRUPOS: Grupo[] = [
  {
    titulo: "Visão geral",
    icone: "painel",
    itens: [{ href: "/inicio", label: "Painel" }],
  },
  { titulo: "Agenda", icone: "calendario", itens: [{ href: "/agenda", label: "Agenda" }] },
  {
    titulo: "Atendimento",
    icone: "pessoas",
    itens: [
      { href: "/pacientes", label: "Pacientes" },
      { href: "/protocolos", label: "Protocolos" },
    ],
  },
  {
    titulo: "Financeiro",
    icone: "dinheiro",
    itens: [{ href: "/financeiro", label: "Cobranças", papeis: ["OWNER", "FINANCE"] }],
  },
  { titulo: "Estoque", icone: "caixa", itens: [{ href: "/estoque", label: "Estoque" }] },
  {
    titulo: "Cadastros",
    icone: "engrenagem",
    itens: [
      { href: "/procedimentos", label: "Procedimentos" },
      { href: "/expediente", label: "Expediente" },
      { href: "/configuracoes", label: "Equipe e acessos", papeis: ["OWNER"] },
    ],
  },
];

function Icone({ nome, className }: { nome: string; className?: string }) {
  const comum = {
    className,
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 1.6,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    viewBox: "0 0 24 24",
  };

  switch (nome) {
    case "painel":
      return (
        <svg {...comum}>
          <rect x="3" y="3" width="7.5" height="9" rx="1.5" />
          <rect x="13.5" y="3" width="7.5" height="5.5" rx="1.5" />
          <rect x="3" y="15" width="7.5" height="6" rx="1.5" />
          <rect x="13.5" y="11.5" width="7.5" height="9.5" rx="1.5" />
        </svg>
      );
    case "calendario":
      return (
        <svg {...comum}>
          <rect x="3" y="5" width="18" height="16" rx="2" />
          <path d="M3 10h18M8 3v4M16 3v4" />
        </svg>
      );
    case "pessoas":
      return (
        <svg {...comum}>
          <circle cx="9" cy="8" r="3.2" />
          <path d="M3.5 20c0-3 2.5-5 5.5-5s5.5 2 5.5 5M17 11.5a2.6 2.6 0 1 0 0-5.2M18 20c0-2.2-.8-3.6-2-4.4" />
        </svg>
      );
    case "dinheiro":
      return (
        <svg {...comum}>
          <rect x="2.5" y="6" width="19" height="12" rx="2" />
          <circle cx="12" cy="12" r="2.5" />
          <path d="M6 12h.01M18 12h.01" />
        </svg>
      );
    case "caixa":
      return (
        <svg {...comum}>
          <path d="M21 8.5 12 4 3 8.5v7L12 20l9-4.5v-7Z" />
          <path d="m3 8.5 9 4.5 9-4.5M12 13v7" />
        </svg>
      );
    default:
      return (
        <svg {...comum}>
          <circle cx="12" cy="12" r="3" />
          <path d="M19.4 15a1.6 1.6 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.6 1.6 0 0 0-2.7 1.1V21a2 2 0 1 1-4 0v-.1A1.6 1.6 0 0 0 7 19.4a1.6 1.6 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1A1.6 1.6 0 0 0 3 15a1.6 1.6 0 0 0-1.5-1H1a2 2 0 1 1 0-4h.1A1.6 1.6 0 0 0 2.6 9a1.6 1.6 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1A1.6 1.6 0 0 0 7 4.6h.1A1.6 1.6 0 0 0 8.6 3V3a2 2 0 1 1 4 0v.1A1.6 1.6 0 0 0 15 4.6a1.6 1.6 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.6 1.6 0 0 0-.3 1.8v.1a1.6 1.6 0 0 0 1.5 1.5H21a2 2 0 1 1 0 4h-.1a1.6 1.6 0 0 0-1.5 1.5Z" />
        </svg>
      );
  }
}

const NOME_DO_PAPEL: Record<string, string> = {
  OWNER: "Responsável pela clínica",
  DOCTOR: "Profissional",
  SECRETARY: "Recepção",
  FINANCE: "Financeiro",
};

export function Sidebar({ papeis, usuario }: { papeis: string[]; usuario: string }) {
  const caminho = usePathname();
  const [abertoNoMobile, setAbertoNoMobile] = useState(false);

  const visiveis = GRUPOS.map((g) => ({
    ...g,
    itens: g.itens.filter((i) => !i.papeis || i.papeis.some((p) => papeis.includes(p))),
  })).filter((g) => g.itens.length > 0);

  const conteudo = (
    <nav className="flex h-full flex-col gap-6 overflow-y-auto px-3 py-5">
      <Link href="/inicio" className="flex items-center gap-2.5 px-2">
        <Image src="/cliniq-mark.png" alt="" width={140} height={157} priority className="h-7 w-auto" />
        <Image
          src="/cliniq-wordmark.png"
          alt="CLINIQ"
          width={300}
          height={68}
          priority
          className="h-4 w-auto"
        />
      </Link>

      {/* Quem sou eu e o que posso fazer aqui — pergunta que a pessoa faz uma vez e
          depois nunca mais, mas que sem resposta incomoda todo dia. */}
      <div className="flex items-center gap-3 rounded-card border border-border bg-surface px-3 py-2.5">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary-soft font-display text-sm text-primary">
          {usuario.charAt(0).toUpperCase()}
        </span>
        <div className="min-w-0">
          <p className="truncate text-sm text-ink">{usuario}</p>
          <p className="truncate text-xs text-ink-subtle">
            {papeis.map((p) => NOME_DO_PAPEL[p] ?? p).join(" · ") || "Sem papel"}
          </p>
        </div>
      </div>

      <div className="space-y-5">
        {visiveis.map((grupo) => (
          <div key={grupo.titulo}>
            <div className="mb-1 flex items-center gap-2 px-2">
              <Icone nome={grupo.icone} className="h-4 w-4 text-ink-subtle" />
              <span className="text-[0.7rem] font-medium uppercase tracking-wider text-ink-subtle">
                {grupo.titulo}
              </span>
            </div>

            <ul className="space-y-0.5">
              {grupo.itens.map((item) => {
                const ativo = caminho === item.href || caminho.startsWith(`${item.href}/`);

                return (
                  <li key={item.href}>
                    <Link
                      href={item.href}
                      onClick={() => setAbertoNoMobile(false)}
                      className={cn(
                        "block rounded-control py-2 pl-8 pr-3 text-sm transition-colors",
                        ativo
                          ? "bg-primary-soft font-medium text-primary"
                          : "text-ink-muted hover:bg-surface-muted hover:text-ink",
                      )}
                    >
                      {item.label}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </div>
    </nav>
  );

  return (
    <>
      {/* Mobile: botão fixo e menu deslizante. A clínica usa iPad e computador, mas a
          recepção confere agenda no celular a caminho do trabalho. */}
      <button
        type="button"
        onClick={() => setAbertoNoMobile(true)}
        aria-label="Abrir menu"
        className="fixed left-3 top-3 z-30 rounded-control border border-border bg-canvas p-2 shadow-soft lg:hidden"
      >
        <svg
          viewBox="0 0 24 24"
          className="h-5 w-5 text-ink"
          fill="none"
          stroke="currentColor"
          strokeWidth={1.8}
          strokeLinecap="round"
        >
          <path d="M4 7h16M4 12h16M4 17h16" />
        </svg>
      </button>

      {abertoNoMobile && (
        <div
          className="fixed inset-0 z-40 bg-ink/30 lg:hidden"
          onClick={() => setAbertoNoMobile(false)}
        />
      )}

      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-50 w-64 border-r border-border bg-canvas transition-transform lg:translate-x-0",
          abertoNoMobile ? "translate-x-0" : "-translate-x-full",
        )}
      >
        {conteudo}
      </aside>
    </>
  );
}
