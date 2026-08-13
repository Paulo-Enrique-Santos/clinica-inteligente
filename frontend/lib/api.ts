import { auth } from "@/auth";

const API_BASE = process.env.API_BASE_URL ?? "http://localhost:5231";

/**
 * Chama a API .NET em nome do usuario logado.
 *
 * Roda SEMPRE no servidor (Server Component ou Server Action). Isso e uma decisao de
 * seguranca, nao de conveniencia: o access token nunca chega ao navegador, entao nao ha
 * como ser roubado por XSS ou por extensao instalada na maquina da clinica. De quebra,
 * some a necessidade de CORS — quem fala com a API e o servidor do Next.
 */
export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const session = await auth();

  if (!session?.accessToken) {
    throw new Error("Sem sessao ativa.");
  }

  const resposta = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      ...init?.headers,
      Authorization: `Bearer ${session.accessToken}`,
      "Content-Type": "application/json",
    },
    // Dado de paciente muda o tempo todo e e por clinica: cache aqui seria, na melhor
    // hipotese, informacao velha; na pior, dado de uma clinica servido para outra.
    cache: "no-store",
  });

  if (!resposta.ok) {
    throw new Error(`API respondeu ${resposta.status} em ${path}`);
  }

  return resposta.json() as Promise<T>;
}

export type Paciente = {
  id: string;
  fullName: string;
  phoneE164: string;
  birthDate: string | null;
  notes: string | null;
  createdAt: string;
};
