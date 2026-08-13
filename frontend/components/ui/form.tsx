"use client";

import Link from "next/link";
import { useActionState } from "react";
import { Alert } from "./alert";
import { Button } from "./button";
import { ESTADO_INICIAL, type EstadoFormulario } from "@/lib/form";

type Acao = (
  estado: EstadoFormulario,
  formulario: FormData,
) => Promise<EstadoFormulario>;

/**
 * Casca comum dos formulários: erro no topo, botões embaixo, estado de envio.
 *
 * É o único componente de cliente do sistema até aqui. Justifica-se porque precisa do
 * estado de "enviando" para desabilitar o botão — sem isso, clicar duas vezes numa
 * conexão lenta cria dois registros, e numa recepção isso acontece toda semana.
 */
export function Formulario({
  acao,
  children,
  rotuloEnviar = "Salvar",
  cancelarHref,
}: {
  acao: Acao;
  children: React.ReactNode;
  rotuloEnviar?: string;
  cancelarHref?: string;
}) {
  const [estado, enviar, enviando] = useActionState(acao, ESTADO_INICIAL);

  return (
    <form action={enviar} className="space-y-4">
      {estado?.erro && <Alert>{estado.erro}</Alert>}

      {children}

      <div className="flex items-center justify-end gap-2 pt-2">
        {cancelarHref && (
          <Link
            href={cancelarHref}
            className="rounded-control border border-border-strong px-4 py-2 text-sm text-ink transition-colors hover:bg-surface-muted"
          >
            Cancelar
          </Link>
        )}
        <Button type="submit" disabled={enviando}>
          {enviando ? "Salvando…" : rotuloEnviar}
        </Button>
      </div>
    </form>
  );
}
