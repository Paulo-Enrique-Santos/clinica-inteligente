"use client";

import { useEffect, useId, useRef, useState } from "react";
import { cn } from "@/lib/cn";

type Opcao = { id: string; titulo: string; detalhe?: string };

/**
 * Campo de busca com sugestões.
 *
 * Substitui o <select> nos cadastros que crescem: com trezentas pacientes, rolar uma
 * lista suspensa é pior do que digitar três letras. Procedimento e profissional usam o
 * mesmo componente por consistência — a recepção não deveria ter que aprender dois
 * jeitos de escolher coisas.
 */
export function Busca({
  name,
  recurso,
  label,
  placeholder,
  aoEscolher,
  obrigatorio = true,
}: {
  name: string;
  recurso: "pacientes" | "procedimentos" | "profissionais" | "estoque" | "estoque-mensuravel";
  label: string;
  placeholder?: string;
  aoEscolher?: (opcao: Opcao | null) => void;
  obrigatorio?: boolean;
}) {
  const id = useId();
  const [termo, setTermo] = useState("");
  const [opcoes, setOpcoes] = useState<Opcao[]>([]);
  const [escolhida, setEscolhida] = useState<Opcao | null>(null);
  const [aberto, setAberto] = useState(false);
  const [buscando, setBuscando] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (escolhida) return;

    // Espera a pessoa parar de digitar: uma requisição por tecla castigaria a API
    // e ainda entregaria resultados fora de ordem.
    const t = setTimeout(async () => {
      setBuscando(true);
      try {
        const r = await fetch(
          `/api/busca/${recurso}?q=${encodeURIComponent(termo)}`,
        );
        setOpcoes(r.ok ? normalizar(recurso, await r.json()) : []);
      } catch {
        setOpcoes([]);
      } finally {
        setBuscando(false);
      }
    }, 250);

    return () => clearTimeout(t);
  }, [termo, recurso, escolhida]);

  useEffect(() => {
    function aoClicarFora(e: MouseEvent) {
      if (!containerRef.current?.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener("mousedown", aoClicarFora);
    return () => document.removeEventListener("mousedown", aoClicarFora);
  }, []);

  function escolher(opcao: Opcao) {
    setEscolhida(opcao);
    setTermo(opcao.titulo);
    setAberto(false);
    aoEscolher?.(opcao);
  }

  function limpar() {
    setEscolhida(null);
    setTermo("");
    setOpcoes([]);
    aoEscolher?.(null);
  }

  return (
    <div className="space-y-1.5" ref={containerRef}>
      <label htmlFor={id} className="block text-sm font-medium text-ink">
        {label}
      </label>

      <div className="relative">
        {/* O formulário envia o id; o campo visível carrega o texto. */}
        <input type="hidden" name={name} value={escolhida?.id ?? ""} required={obrigatorio} />

        <input
          id={id}
          value={termo}
          autoComplete="off"
          placeholder={placeholder}
          onChange={(e) => {
            setTermo(e.target.value);
            if (escolhida) limpar();
            setAberto(true);
          }}
          onFocus={() => setAberto(true)}
          className={cn(
            "w-full rounded-control border bg-canvas px-3 py-2 text-sm text-ink",
            "placeholder:text-ink-subtle transition-colors focus:outline-none",
            escolhida ? "border-primary" : "border-border-strong focus:border-primary",
          )}
        />

        {escolhida && (
          <button
            type="button"
            onClick={limpar}
            aria-label="Limpar"
            className="absolute right-2 top-1/2 -translate-y-1/2 rounded px-1.5 text-ink-subtle hover:text-ink"
          >
            ×
          </button>
        )}

        {aberto && !escolhida && (
          <ul className="absolute z-20 mt-1 max-h-64 w-full overflow-auto rounded-card border border-border bg-canvas py-1 shadow-lifted">
            {buscando && opcoes.length === 0 && (
              <li className="px-3 py-2 text-sm text-ink-subtle">Buscando…</li>
            )}

            {!buscando && opcoes.length === 0 && (
              <li className="px-3 py-2 text-sm text-ink-subtle">
                {termo ? "Nada encontrado." : "Digite para buscar."}
              </li>
            )}

            {opcoes.map((o) => (
              <li key={o.id}>
                <button
                  type="button"
                  onClick={() => escolher(o)}
                  className="w-full px-3 py-2 text-left text-sm transition-colors hover:bg-surface-muted"
                >
                  <span className="text-ink">{o.titulo}</span>
                  {o.detalhe && (
                    <span className="ml-2 text-xs text-ink-subtle">{o.detalhe}</span>
                  )}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

const MOEDA = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

function normalizar(recurso: string, dados: unknown[]): Opcao[] {
  if (recurso === "pacientes") {
    return (dados as { id: string; fullName: string; phoneE164: string }[]).map((p) => ({
      id: p.id,
      titulo: p.fullName,
      detalhe: p.phoneE164,
    }));
  }

  if (recurso === "procedimentos") {
    return (dados as { id: string; name: string; durationMinutes: number; price: number }[]).map(
      (p) => ({
        id: p.id,
        titulo: p.name,
        detalhe: `${p.durationMinutes} min · ${MOEDA.format(p.price)}`,
      }),
    );
  }

  if (recurso.startsWith("estoque")) {
    return (dados as { id: string; name: string; unit: string; balance: number }[]).map((i) => ({
      id: i.id,
      titulo: i.name,
      detalhe: `saldo ${i.balance} ${i.unit}`,
    }));
  }

  return (dados as { id: string; displayName: string; specialty: string | null }[]).map((p) => ({
    id: p.id,
    titulo: p.displayName,
    detalhe: p.specialty ?? undefined,
  }));
}
