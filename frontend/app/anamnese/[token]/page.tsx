import Image from "next/image";
import { publicFetch } from "@/lib/api";
import { enviarFicha } from "@/lib/actions/anamnese";
import { PERGUNTAS } from "@/lib/anamnese";
import { Field, Input } from "@/components/ui/field";
import { Formulario } from "@/components/ui/form";

// A ficha depende do token e da API; nada aqui pode ser resolvido em tempo de build.
// Sem isto, o build tenta buscar a página e falha porque a API não está de pé.
export const dynamic = "force-dynamic";

/**
 * Ficha de anamnese preenchida pela paciente.
 *
 * Página pública: sem login, sem menu, sem nada do sistema interno. Quem chega aqui é
 * uma pessoa no celular, na recepção ou em casa — a tela precisa parecer da clínica e
 * não pedir nada além do necessário.
 */
export default async function AnamnesePage(props: PageProps<"/anamnese/[token]">) {
  const { token } = await props.params;

  const ficha = await publicFetch<{ primeiroNome: string }>(`/public/anamnese/${token}`);

  if (!ficha) {
    return (
      <div className="flex flex-1 items-center justify-center bg-surface px-6 py-16">
        <div className="w-full max-w-md rounded-card border border-border bg-canvas p-8 text-center shadow-soft">
          <h1 className="text-xl text-ink">Este link não está mais válido</h1>
          <p className="mt-3 text-sm leading-relaxed text-ink-muted">
            Fichas têm prazo e valem uma única vez, para proteger seus dados. Peça um
            novo link à clínica.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-1 justify-center bg-surface px-4 py-10">
      <div className="w-full max-w-lg">
        <div className="mb-6 flex justify-center">
          <Image
            src="/cliniq-logo.png"
            alt="CLINIQ"
            width={440}
            height={364}
            priority
            className="h-auto w-40"
          />
        </div>

        <div className="rounded-card border border-border bg-canvas p-6 shadow-soft sm:p-8">
          <h1 className="text-xl text-ink">Olá, {ficha.primeiroNome}</h1>
          <p className="mt-2 text-sm leading-relaxed text-ink-muted">
            Antes do seu atendimento, precisamos conhecer um pouco da sua saúde. Leva
            menos de dois minutos, e só a sua profissional terá acesso.
          </p>

          <div className="mt-6">
            <Formulario acao={enviarFicha} rotuloEnviar="Enviar ficha">
              <input type="hidden" name="token" value={token} />

              {PERGUNTAS.map((p) => (
                <Field key={p.chave} label={p.texto} htmlFor={p.chave}>
                  <Input id={p.chave} name={p.chave} placeholder="Escreva aqui" />
                </Field>
              ))}

              <div className="space-y-3 rounded-control border border-border bg-surface p-4">
                <label className="flex items-start gap-2.5 text-sm text-ink">
                  <input
                    type="checkbox"
                    name="dados"
                    required
                    className="mt-0.5 accent-primary"
                  />
                  <span>
                    Autorizo a clínica a guardar e usar estas informações para o meu
                    atendimento. <span className="text-danger">*</span>
                  </span>
                </label>

                <label className="flex items-start gap-2.5 text-sm text-ink">
                  <input type="checkbox" name="imagem" className="mt-0.5 accent-primary" />
                  <span>
                    Autorizo o uso de fotos do meu procedimento em materiais e redes
                    sociais da clínica.{" "}
                    <span className="text-ink-subtle">(opcional)</span>
                  </span>
                </label>
              </div>
            </Formulario>
          </div>
        </div>

        <p className="mt-5 text-center text-xs leading-relaxed text-ink-subtle">
          Seus dados são tratados conforme a LGPD e ficam disponíveis apenas para a equipe
          da clínica.
        </p>
      </div>
    </div>
  );
}
