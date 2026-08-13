"use server";

import { redirect } from "next/navigation";
import { apiSend, mensagemDeErro } from "@/lib/api";
import type { EstadoFormulario } from "@/lib/form";

function paraNumero(valor: FormDataEntryValue | null): number {
  return Number(String(valor ?? "0").replace(",", ".")) || 0;
}

export async function criarItemDeEstoque(
  _anterior: EstadoFormulario,
  formulario: FormData,
): Promise<EstadoFormulario> {
  const resultado = await apiSend("/stock", "POST", {
    name: String(formulario.get("nome") ?? "").trim(),
    unit: String(formulario.get("unidade") ?? "").trim(),
    minimumQuantity: paraNumero(formulario.get("minimo")),
  });

  if (!resultado.ok) {
    return { erro: mensagemDeErro(resultado.problem) };
  }

  redirect("/estoque");
}

export async function movimentarEstoque(formulario: FormData) {
  const item = String(formulario.get("item") ?? "");

  await apiSend(`/stock/${item}/movements`, "POST", {
    type: String(formulario.get("tipo") ?? "Entrada"),
    quantity: paraNumero(formulario.get("quantidade")),
    reason: String(formulario.get("motivo") ?? "").trim() || null,
  });

  redirect("/estoque");
}
