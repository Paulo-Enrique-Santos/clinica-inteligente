"use server";

import { redirect } from "next/navigation";
import { apiSend, mensagemDeErro } from "@/lib/api";
import type { EstadoFormulario } from "@/lib/form";

const DIAS = [0, 1, 2, 3, 4, 5, 6];

function horaOuNulo(valor: FormDataEntryValue | null): string | null {
  const texto = String(valor ?? "").trim();
  return texto === "" ? null : texto;
}

export async function salvarExpediente(
  _anterior: EstadoFormulario,
  formulario: FormData,
): Promise<EstadoFormulario> {
  const profissional = String(formulario.get("profissional") ?? "");

  // Só entram os dias marcados como "atende". Desmarcar um dia é a forma de dizer
  // que a profissional não trabalha nele — por isso o PUT substitui a semana inteira.
  const dias = DIAS.filter((d) => formulario.get(`ativo-${d}`) === "on").map((d) => ({
    dayOfWeek: d,
    startsAt: String(formulario.get(`inicio-${d}`) ?? ""),
    endsAt: String(formulario.get(`fim-${d}`) ?? ""),
    breakStartsAt: horaOuNulo(formulario.get(`almocoInicio-${d}`)),
    breakEndsAt: horaOuNulo(formulario.get(`almocoFim-${d}`)),
  }));

  const resultado = await apiSend(`/professionals/${profissional}/schedule`, "PUT", { dias });

  if (!resultado.ok) {
    return { erro: mensagemDeErro(resultado.problem) };
  }

  redirect(`/expediente?prof=${profissional}`);
}

export async function salvarExcecao(formulario: FormData) {
  const profissional = String(formulario.get("profissional") ?? "");
  const fechado = formulario.get("fechado") === "on";

  await apiSend(`/professionals/${profissional}/schedule/exceptions`, "POST", {
    date: String(formulario.get("data") ?? ""),
    closed: fechado,
    // Dia fechado não tem horário: mandar faixa junto seria contraditório.
    startsAt: fechado ? null : horaOuNulo(formulario.get("inicio")),
    endsAt: fechado ? null : horaOuNulo(formulario.get("fim")),
    breakStartsAt: fechado ? null : horaOuNulo(formulario.get("almocoInicio")),
    breakEndsAt: fechado ? null : horaOuNulo(formulario.get("almocoFim")),
    reason: String(formulario.get("motivo") ?? "").trim() || null,
  });

  redirect(`/expediente?prof=${profissional}`);
}

export async function removerExcecao(formulario: FormData) {
  const profissional = String(formulario.get("profissional") ?? "");
  const id = String(formulario.get("id") ?? "");

  await apiSend(`/professionals/${profissional}/schedule/exceptions/${id}`, "DELETE");

  redirect(`/expediente?prof=${profissional}`);
}
