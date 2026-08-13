"use server";

import { redirect } from "next/navigation";
import { apiSend, mensagemDeErro } from "@/lib/api";
import type { EstadoFormulario } from "@/lib/form";

export async function criarMembroDaEquipe(
  _anterior: EstadoFormulario,
  formulario: FormData,
): Promise<EstadoFormulario> {
  const papel = String(formulario.get("papel") ?? "");

  const resultado = await apiSend("/team", "POST", {
    username: String(formulario.get("usuario") ?? "").trim(),
    email: String(formulario.get("email") ?? "").trim() || null,
    firstName: String(formulario.get("nome") ?? "").trim(),
    lastName: String(formulario.get("sobrenome") ?? "").trim(),
    password: String(formulario.get("senha") ?? ""),
    role: papel,
    // Só usados quando o papel é DOCTOR: nesse caso a API cria também a ficha de
    // profissional e a vincula ao login, para a agenda dela funcionar de imediato.
    displayName: String(formulario.get("nomeExibicao") ?? "").trim() || null,
    specialty: String(formulario.get("especialidade") ?? "").trim() || null,
    // O tenant NÃO vai aqui: ele vem do token de quem está criando.
  });

  if (!resultado.ok) {
    return { erro: mensagemDeErro(resultado.problem) };
  }

  redirect("/configuracoes");
}
