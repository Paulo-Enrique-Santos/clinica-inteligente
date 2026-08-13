"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui/button";

/**
 * Fronteira de erro do sistema.
 *
 * Sem ela, qualquer falha ao carregar dados vira a tela padrão do Next — que, em
 * produção, é uma página branca dizendo "Application error". Na recepção de uma clínica
 * isso vira ligação para o suporte.
 *
 * Em produção o Next esconde a mensagem original do servidor de propósito (ela pode
 * conter caminho de arquivo, connection string, nome de tabela). Por isso o texto aqui é
 * genérico e o detalhe fica no log do servidor — onde só nós enxergamos.
 */
export default function Erro({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div className="flex flex-1 items-center justify-center bg-surface px-6 py-16">
      <div className="w-full max-w-md rounded-card border border-border bg-canvas p-8 text-center shadow-soft">
        <h1 className="text-xl text-ink">Não conseguimos carregar esta tela</h1>

        <p className="mt-3 text-sm leading-relaxed text-ink-muted">
          Isso costuma ser uma falha momentânea de conexão com o sistema. Tente de novo em
          alguns segundos — nenhum dado foi perdido.
        </p>

        <div className="mt-6 flex justify-center gap-2">
          <Button onClick={reset}>Tentar de novo</Button>
          <a
            href="/agenda"
            className="rounded-control border border-border-strong px-4 py-2 text-sm text-ink transition-colors hover:bg-surface-muted"
          >
            Ir para a agenda
          </a>
        </div>

        {error.digest && (
          // Código curto para a pessoa citar ao pedir ajuda; é o que liga esta tela
          // à linha correspondente no log do servidor.
          <p className="mt-6 font-mono text-xs text-ink-subtle">
            Código: {error.digest}
          </p>
        )}
      </div>
    </div>
  );
}
