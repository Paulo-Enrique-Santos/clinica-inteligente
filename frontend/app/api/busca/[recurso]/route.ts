import { apiFetch } from "@/lib/api";

/**
 * Ponte entre o campo de busca (que roda no navegador) e a API .NET.
 *
 * O componente de busca precisa consultar enquanto a pessoa digita, e o access token
 * nunca vai ao navegador — então a consulta passa por aqui, no servidor do Next, que
 * acrescenta o token.
 *
 * A lista de recursos é fechada de propósito: sem ela, esta rota viraria um proxy
 * aberto para qualquer endpoint da API, autenticado com o token de quem estivesse logado.
 */
const RECURSOS: Record<string, string> = {
  pacientes: "/patients",
  procedimentos: "/procedures",
  profissionais: "/professionals",
  estoque: "/stock",
};

export async function GET(
  request: Request,
  ctx: RouteContext<"/api/busca/[recurso]">,
) {
  const { recurso } = await ctx.params;
  const caminho = RECURSOS[recurso];

  if (!caminho) {
    return Response.json({ erro: "Recurso desconhecido." }, { status: 404 });
  }

  const termo = new URL(request.url).searchParams.get("q") ?? "";

  const resultado = await apiFetch<unknown[]>(
    `${caminho}?q=${encodeURIComponent(termo)}`,
  );

  return Response.json(resultado);
}
