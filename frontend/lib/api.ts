import { auth } from "@/auth";

const API_BASE = process.env.API_BASE_URL ?? "http://localhost:5231";

/**
 * Chama a API .NET em nome do usuário logado.
 *
 * Roda SEMPRE no servidor (Server Component ou Server Action). Isso é uma decisão de
 * segurança, não de conveniência: o access token nunca chega ao navegador, então não há
 * como ser roubado por XSS ou por extensão instalada na máquina da clínica. De quebra,
 * some a necessidade de CORS — quem fala com a API é o servidor do Next.
 */
export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const resposta = await requisicao(path, init);

  if (!resposta.ok) {
    throw new Error(`API respondeu ${resposta.status} em ${path}`);
  }

  return resposta.json() as Promise<T>;
}

/**
 * Como <c>apiFetch</c>, mas devolve null em 404 em vez de lançar.
 *
 * Existe para os casos em que "não encontrado" é resposta esperada, não falha — por
 * exemplo, a doutora cujo login ainda não foi vinculado a uma ficha de profissional.
 */
export async function apiFetchOrNull<T>(path: string): Promise<T | null> {
  const resposta = await requisicao(path);

  if (resposta.status === 404) return null;

  if (!resposta.ok) {
    throw new Error(`API respondeu ${resposta.status} em ${path}`);
  }

  return resposta.json() as Promise<T>;
}

/** Erro de rede: a API não respondeu. Distinto de "respondeu com erro". */
export class ApiForaDoArError extends Error {
  constructor(path: string, causa: unknown) {
    super(
      `Nao foi possivel alcancar a API em ${API_BASE}${path}. ` +
        `Ela esta rodando? Detalhe: ${causa instanceof Error ? causa.message : causa}`,
    );
    this.name = "ApiForaDoArError";
  }
}

/** Formato de erro do ASP.NET (RFC 7807). */
export type ProblemDetails = {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};

export type ApiResult<T> =
  | { ok: true; data: T }
  | { ok: false; status: number; problem: ProblemDetails };

/**
 * Para escrita. Ao contrário de <c>apiFetch</c>, não lança em erro: devolve o problema
 * para a tela mostrar no campo certo. Validação e conflito de horário são resultados
 * esperados de um formulário, não exceções.
 */
export async function apiSend<T = unknown>(
  path: string,
  method: "POST" | "PUT" | "PATCH" | "DELETE",
  body?: unknown,
): Promise<ApiResult<T>> {
  const resposta = await requisicao(path, {
    method,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (resposta.ok) {
    const texto = await resposta.text();
    return { ok: true, data: (texto ? JSON.parse(texto) : undefined) as T };
  }

  let problem: ProblemDetails = {};
  try {
    problem = (await resposta.json()) as ProblemDetails;
  } catch {
    problem = { title: `Erro ${resposta.status}` };
  }

  return { ok: false, status: resposta.status, problem };
}

async function requisicao(path: string, init?: RequestInit) {
  const session = await auth();

  if (!session?.accessToken) {
    throw new Error("Sem sessao ativa.");
  }

  try {
    return await fetch(`${API_BASE}${path}`, {
      ...init,
      headers: {
        ...init?.headers,
        Authorization: `Bearer ${session.accessToken}`,
        "Content-Type": "application/json",
      },
      // Dado de clínica muda o tempo todo e é por tenant: cache aqui seria, na melhor
      // hipótese, informação velha; na pior, dado de uma clínica servido para outra.
      cache: "no-store",
    });
  } catch (causa) {
    // "fetch failed" sozinho no log não ajuda ninguém a às 8h da manhã descobrir que
    // a API não subiu. A mensagem diz o endereço tentado e a pergunta certa.
    throw new ApiForaDoArError(path, causa);
  }
}

/** Junta as mensagens de validação num texto só, para exibir acima do formulário. */
export function mensagemDeErro(problem: ProblemDetails): string {
  if (problem.errors) {
    const mensagens = Object.values(problem.errors).flat();
    if (mensagens.length > 0) return mensagens.join(" ");
  }
  return problem.detail ?? problem.title ?? "Nao foi possivel concluir a operacao.";
}

// --- Tipos devolvidos pela API -------------------------------------------

export type Paciente = {
  id: string;
  fullName: string;
  phoneE164: string;
  birthDate: string | null;
  notes: string | null;
  createdAt: string;
};

export type Procedimento = {
  id: string;
  name: string;
  durationMinutes: number;
  price: number;
  /** Nulos para quem não pode ver custo — a API omite, não é a tela que esconde. */
  suppliesCost: number | null;
  margin: number | null;
  description: string | null;
  active: boolean;
};

export type Profissional = {
  id: string;
  fullName: string;
  displayName: string;
  specialty: string | null;
  active: boolean;
  vinculadaAoLogin: boolean;
};

export type Atendimento = {
  id: string;
  patientId: string;
  patientName: string;
  patientPhone: string;
  procedureId: string;
  procedureName: string;
  professionalId: string;
  professionalName: string;
  startsAt: string;
  endsAt: string;
  status: string;
  price: number;
  notes: string | null;
};

export type Cobranca = {
  id: string;
  appointmentId: string | null;
  treatmentPlanId: string | null;
  patientName: string;
  patientPhone: string;
  procedureName: string;
  appointmentAt: string | null;
  amount: number;
  dueDate: string;
  status: string;
  overdue: boolean;
  method: string | null;
  paidAt: string | null;
};

export type ItemDoProtocolo = {
  id: string;
  procedureId: string;
  procedureName: string;
  sessions: number;
  intervalDays: number | null;
  startAfterDays: number;
  unitPrice: number;
  total: number;
  status: string;
  notes: string | null;
};

export type Protocolo = {
  id: string;
  patientId: string;
  patientName: string;
  professionalId: string;
  professionalName: string;
  status: string;
  notes: string | null;
  createdAt: string;
  approvedAt: string | null;
  items: ItemDoProtocolo[];
};

export type MembroDaEquipe = {
  id: string;
  username: string;
  email: string | null;
  firstName: string | null;
  lastName: string | null;
  enabled: boolean;
  roles: string[];
};

export type ItemDeEstoque = {
  id: string;
  name: string;
  unit: string;
  balance: number;
  minimumQuantity: number;
  active: boolean;
  belowMinimum: boolean;
};
