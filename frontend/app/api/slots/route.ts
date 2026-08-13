import { apiFetch } from "@/lib/api";

/** Horários livres de uma profissional para um procedimento numa data. */
export async function GET(request: Request) {
  const p = new URL(request.url).searchParams;
  const profissional = p.get("prof");
  const procedimento = p.get("proc");
  const data = p.get("data");

  if (!profissional || !procedimento || !data) {
    // Faltando qualquer um dos três, não há o que calcular — e devolver lista vazia
    // é mais honesto do que inventar horários.
    return Response.json([]);
  }

  const livres = await apiFetch<string[]>(
    `/professionals/${profissional}/slots?data=${data}&procedimentoId=${procedimento}`,
  );

  return Response.json(livres);
}
