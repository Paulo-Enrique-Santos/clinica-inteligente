"use server";

import { redirect } from "next/navigation";
import { apiSend, mensagemDeErro } from "@/lib/api";
import type { EstadoFormulario } from "@/lib/form";
import { paraE164 } from "@/lib/telefone";

export async function criarPaciente(
  _anterior: EstadoFormulario,
  formulario: FormData,
): Promise<EstadoFormulario> {
  const nascimento = String(formulario.get("nascimento") ?? "").trim();

  const resultado = await apiSend("/patients", "POST", {
    fullName: String(formulario.get("nome") ?? "").trim(),
    phoneE164: paraE164(String(formulario.get("telefone") ?? "")),
    birthDate: nascimento === "" ? null : nascimento,
    notes: String(formulario.get("observacoes") ?? "").trim() || null,
  });

  if (!resultado.ok) {
    return { erro: mensagemDeErro(resultado.problem) };
  }

  // redirect lança por dentro; precisa ficar fora do try/catch de quem chama.
  redirect("/pacientes");
}
