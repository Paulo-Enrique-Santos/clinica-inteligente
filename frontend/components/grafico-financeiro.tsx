import { reais } from "@/lib/formato";

type Ponto = { mes: string; recebido: number; aReceber: number };

const MESES = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

/**
 * Recebido × a receber nos últimos seis meses.
 *
 * SVG puro: são doze barras. Uma biblioteca de gráficos custaria centenas de kilobytes
 * para desenhar retângulos — e ainda traria a tentação de usar tipos de gráfico que não
 * respondem pergunta nenhuma.
 *
 * As duas séries medem coisas diferentes de propósito: recebido pela data do pagamento
 * ("quanto entrou"), a receber pelo vencimento ("quanto deveria entrar").
 */
export function GraficoFinanceiro({ serie }: { serie: Ponto[] }) {
  const maior = Math.max(...serie.flatMap((p) => [p.recebido, p.aReceber]), 1);

  return (
    <div>
      <div className="flex items-center gap-4 text-xs text-ink-muted">
        <span className="flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-sm bg-primary" /> Recebido
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-sm bg-champagne" /> A receber
        </span>
      </div>

      <div className="mt-4 flex h-44 items-end gap-3">
        {serie.map((p) => {
          const [ano, mes] = p.mes.split("-");
          const rotulo = `${MESES[Number(mes) - 1]}/${ano.slice(2)}`;

          return (
            <div key={p.mes} className="flex flex-1 flex-col items-center gap-2">
              <div className="flex h-full w-full items-end justify-center gap-1">
                <div
                  className="w-1/2 rounded-t-sm bg-primary transition-all"
                  style={{ height: `${(p.recebido / maior) * 100}%` }}
                  title={`Recebido em ${rotulo}: ${reais(p.recebido)}`}
                />
                <div
                  className="w-1/2 rounded-t-sm bg-champagne transition-all"
                  style={{ height: `${(p.aReceber / maior) * 100}%` }}
                  title={`A receber em ${rotulo}: ${reais(p.aReceber)}`}
                />
              </div>
              <span className="text-[0.65rem] text-ink-subtle">{rotulo}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}
