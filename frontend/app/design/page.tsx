import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardHeader, CardBody, EmptyState } from "@/components/ui/card";
import { Field, Input, Select } from "@/components/ui/field";

/**
 * Guia de estilo. Página de desenvolvimento: serve para revisar o sistema
 * inteiro numa tela só, em vez de descobrir inconsistência depois de 15 telas
 * construídas.
 */

const CORES = [
  { nome: "canvas", valor: "#FFFFFF", classe: "bg-canvas border border-border" },
  { nome: "surface", valor: "#FDFBFA", classe: "bg-surface" },
  { nome: "surface-muted", valor: "#F7F2EE", classe: "bg-surface-muted" },
  { nome: "border", valor: "#ECE4DE", classe: "bg-border" },
  { nome: "primary", valor: "#9C5566", classe: "bg-primary" },
  { nome: "primary-hover", valor: "#854656", classe: "bg-primary-hover" },
  { nome: "primary-soft", valor: "#F6EBEE", classe: "bg-primary-soft" },
  { nome: "champagne", valor: "#C9A96B", classe: "bg-champagne" },
  { nome: "champagne-soft", valor: "#F7EFE2", classe: "bg-champagne-soft" },
  { nome: "ink", valor: "#1B1714", classe: "bg-ink" },
  { nome: "ink-muted", valor: "#7A6E67", classe: "bg-ink-muted" },
  { nome: "success", valor: "#4F7A5E", classe: "bg-success" },
  { nome: "warning", valor: "#A8783C", classe: "bg-warning" },
  { nome: "danger", valor: "#A3493F", classe: "bg-danger" },
];

function Secao({ titulo, children }: { titulo: string; children: React.ReactNode }) {
  return (
    <section className="border-t border-border pt-8">
      <h2 className="mb-5 text-lg text-ink">{titulo}</h2>
      {children}
    </section>
  );
}

export default function DesignPage() {
  return (
    <div className="flex-1 bg-surface">
      <div className="mx-auto max-w-4xl space-y-10 px-6 py-14">
        <header>
          <p className="text-xs uppercase tracking-widest text-ink-subtle">
            Sistema de design
          </p>
          <h1 className="mt-2 text-3xl text-ink">
            Clínica<span className="text-primary">.</span>
          </h1>
          <p className="mt-3 max-w-lg text-sm text-ink-muted">
            Branco dominante, neutros quentes e um acento rosé. Serifa nos títulos
            para a elegância; sans no corpo para leitura em tabela.
          </p>
        </header>

        <Secao titulo="Paleta">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {CORES.map((c) => (
              <div key={c.nome}>
                <div
                  className={`h-14 rounded-control ${c.classe}`}
                  style={{ boxShadow: "inset 0 0 0 1px rgba(27,23,20,.06)" }}
                />
                <p className="mt-1.5 text-xs font-medium text-ink">{c.nome}</p>
                <p className="font-mono text-[11px] text-ink-subtle">{c.valor}</p>
              </div>
            ))}
          </div>
        </Secao>

        <Secao titulo="Tipografia">
          <div className="space-y-4">
            <div>
              <h1 className="text-3xl text-ink">Agenda da semana</h1>
              <p className="mt-1 font-mono text-[11px] text-ink-subtle">
                font-display · 30px · títulos de página
              </p>
            </div>
            <div>
              <h2 className="text-lg text-ink">Procedimentos realizados</h2>
              <p className="mt-1 font-mono text-[11px] text-ink-subtle">
                font-display · 18px · títulos de bloco
              </p>
            </div>
            <div>
              <p className="text-sm text-ink">
                Texto de corpo, usado em tabelas, formulários e descrições.
              </p>
              <p className="mt-1 font-mono text-[11px] text-ink-subtle">
                font-sans · 14px
              </p>
            </div>
            <div>
              <p className="text-sm text-ink-muted">
                Texto secundário, para apoio e contexto.
              </p>
              <p className="mt-1 font-mono text-[11px] text-ink-subtle">
                font-sans · 14px · ink-muted
              </p>
            </div>
          </div>
        </Secao>

        <Secao titulo="Botões">
          <div className="flex flex-wrap items-center gap-3">
            <Button>Salvar</Button>
            <Button variant="secondary">Cancelar</Button>
            <Button variant="ghost">Ver detalhes</Button>
            <Button variant="danger">Desmarcar</Button>
            <Button disabled>Indisponível</Button>
          </div>
          <div className="mt-4 flex flex-wrap items-center gap-3">
            <Button size="sm">Pequeno</Button>
            <Button size="md">Médio</Button>
            <Button size="lg">Grande</Button>
          </div>
        </Secao>

        <Secao titulo="Etiquetas">
          <div className="flex flex-wrap gap-2">
            <Badge>Agendado</Badge>
            <Badge tone="primary">Confirmado</Badge>
            <Badge tone="success">Pago</Badge>
            <Badge tone="warning">Pendente</Badge>
            <Badge tone="danger">Faltou</Badge>
            <Badge tone="champagne">Premium</Badge>
          </div>
        </Secao>

        <Secao titulo="Formulário">
          <Card className="max-w-md">
            <CardHeader title="Nova paciente" description="Dados básicos da ficha." />
            <CardBody className="space-y-4">
              <Field label="Nome completo" htmlFor="nome">
                <Input id="nome" placeholder="Maria Silva" />
              </Field>
              <Field
                label="Telefone"
                htmlFor="tel"
                hint="Usado pelo WhatsApp para confirmar horários."
              >
                <Input id="tel" placeholder="(11) 98765-4321" />
              </Field>
              <Field label="Procedimento" htmlFor="proc">
                <Select id="proc" defaultValue="">
                  <option value="" disabled>
                    Selecione
                  </option>
                  <option>Limpeza de pele</option>
                  <option>Preenchimento labial</option>
                </Select>
              </Field>
              <Field label="E-mail" htmlFor="email" error="Informe um e-mail válido.">
                <Input id="email" defaultValue="maria@" />
              </Field>
              <div className="flex justify-end gap-2 pt-1">
                <Button variant="secondary">Cancelar</Button>
                <Button>Salvar</Button>
              </div>
            </CardBody>
          </Card>
        </Secao>

        <Secao titulo="Estado vazio">
          <EmptyState
            title="Nenhum procedimento cadastrado"
            description="Cadastre os procedimentos da clínica para montar a agenda e calcular a rentabilidade."
            action={<Button>Cadastrar procedimento</Button>}
          />
        </Secao>
      </div>
    </div>
  );
}
