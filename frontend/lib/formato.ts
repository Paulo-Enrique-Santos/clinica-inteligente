const MOEDA = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

/**
 * Data de hoje no fuso da clínica (-03:00), em yyyy-MM-dd.
 *
 * Vive aqui, e não dentro das telas, porque ler o relógio durante o render torna o
 * componente impuro — e porque a mesma conta era repetida em mais de um lugar.
 */
export function hojeNaClinica(): string {
  return new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString().slice(0, 10);
}

export function reais(valor: number): string {
  return MOEDA.format(valor);
}

export function duracao(minutos: number): string {
  if (minutos < 60) return `${minutos} min`;

  const horas = Math.floor(minutos / 60);
  const resto = minutos % 60;

  return resto === 0 ? `${horas}h` : `${horas}h${String(resto).padStart(2, "0")}`;
}

export function dataHora(iso: string): string {
  return new Date(iso).toLocaleString("pt-BR", {
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function hora(iso: string): string {
  return new Date(iso).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}

export function data(iso: string): string {
  // Data pura (yyyy-MM-dd) não tem fuso: interpretar como UTC e formatar como local
  // adiantaria ou atrasaria um dia dependendo da hora.
  const [ano, mes, dia] = iso.slice(0, 10).split("-");
  return `${dia}/${mes}/${ano}`;
}
