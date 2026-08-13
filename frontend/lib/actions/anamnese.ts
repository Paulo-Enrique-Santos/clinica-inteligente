"use server";

import { redirect } from "next/navigation";
import { apiSend, mensagemDeErro, publicSend } from "@/lib/api";
import type { EstadoFormulario } from "@/lib/form";
import { PERGUNTAS } from "@/lib/anamnese";

export async function gerarLinkDeAnamnese(formulario: FormData) {
  const paciente = String(formulario.get("paciente") ?? "");

  const resultado = await apiSend<{ token: string }>(
    `/patients/${paciente}/anamnese/link`,
    "POST",
    {},
  );

  if (!resultado.ok) {
    redirect(`/pacientes/${paciente}`);
  }

  redirect(`/pacientes/${paciente}?link=${resultado.data.token}`);
}

export async function enviarFicha(
  _anterior: EstadoFormulario,
  formulario: FormData,
): Promise<EstadoFormulario> {
  const token = String(formulario.get("token") ?? "");

  const answers: Record<string, string> = {};
  for (const p of PERGUNTAS) {
    answers[p.chave] = String(formulario.get(p.chave) ?? "").trim();
  }

  const resultado = await publicSend(`/public/anamnese/${token}`, {
    answers,
    imageConsent: formulario.get("imagem") === "on",
    dataConsent: formulario.get("dados") === "on",
  });

  if (!resultado.ok) {
    return { erro: mensagemDeErro(resultado.problem ?? {}) };
  }

  redirect(`/anamnese/${token}/enviada`);
}
