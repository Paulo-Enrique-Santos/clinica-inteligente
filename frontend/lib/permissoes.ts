/**
 * Matriz de permissões — a fonte de verdade para EXIBIÇÃO.
 *
 * Ela descreve o que o backend já impõe; não é ela que decide nada. Existe para a dona
 * da clínica conseguir responder "o que a secretária enxerga?" sem precisar testar
 * logando como cada pessoa.
 *
 * Se a regra do backend mudar e esta tabela não, ela passa a mentir — por isso cada
 * linha aponta para onde a regra vive de verdade.
 */
export const PAPEIS = [
  {
    chave: "OWNER",
    nome: "Dona da clínica",
    descricao: "Acesso total, inclusive financeiro e cadastro de equipe.",
  },
  {
    chave: "DOCTOR",
    nome: "Doutora",
    descricao: "Atende. Vê apenas a própria agenda.",
  },
  {
    chave: "SECRETARY",
    nome: "Secretária",
    descricao: "Organiza a agenda da clínica e cadastra pacientes.",
  },
  {
    chave: "FINANCE",
    nome: "Financeiro",
    descricao: "Cobranças, recebimentos e estoque.",
  },
] as const;

export type Papel = (typeof PAPEIS)[number]["chave"];

type Acesso = "total" | "propria" | "leitura" | "nenhum";

export const MATRIZ: Array<{
  area: string;
  onde: string;
  acesso: Record<Papel, Acesso>;
}> = [
  {
    area: "Agenda",
    onde: "AppointmentEndpoints",
    acesso: { OWNER: "total", DOCTOR: "propria", SECRETARY: "total", FINANCE: "nenhum" },
  },
  {
    area: "Pacientes",
    onde: "PatientEndpoints",
    acesso: { OWNER: "total", DOCTOR: "leitura", SECRETARY: "total", FINANCE: "leitura" },
  },
  {
    area: "Procedimentos e preços",
    onde: "ProcedureEndpoints",
    acesso: { OWNER: "total", DOCTOR: "leitura", SECRETARY: "leitura", FINANCE: "leitura" },
  },
  {
    area: "Financeiro",
    onde: "PaymentEndpoints",
    acesso: { OWNER: "total", DOCTOR: "nenhum", SECRETARY: "nenhum", FINANCE: "total" },
  },
  {
    area: "Estoque",
    onde: "StockEndpoints",
    acesso: { OWNER: "total", DOCTOR: "leitura", SECRETARY: "leitura", FINANCE: "total" },
  },
  {
    area: "Equipe e configurações",
    onde: "TeamEndpoints",
    acesso: { OWNER: "total", DOCTOR: "nenhum", SECRETARY: "nenhum", FINANCE: "nenhum" },
  },
];

export const ROTULO_DE_ACESSO: Record<Acesso, string> = {
  total: "Total",
  propria: "Só a própria",
  leitura: "Consulta",
  nenhum: "Sem acesso",
};

export const TOM_DE_ACESSO: Record<Acesso, "success" | "primary" | "neutral" | "danger"> = {
  total: "success",
  propria: "primary",
  leitura: "neutral",
  nenhum: "danger",
};
