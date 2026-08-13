/**
 * Perguntas da ficha de anamnese.
 *
 * Fica fora do arquivo de Server Actions porque um módulo "use server" só pode exportar
 * funções async — e porque isto é conteúdo, não comportamento.
 *
 * Mudar as perguntas não exige migration: as respostas são gravadas como JSON.
 */
export const PERGUNTAS = [
  { chave: "alergias", texto: "Você tem alguma alergia? Quais?" },
  { chave: "medicamentos", texto: "Usa algum medicamento contínuo?" },
  { chave: "cirurgias", texto: "Já fez alguma cirurgia ou procedimento estético?" },
  { chave: "gestante", texto: "Está gestante ou amamentando?" },
  { chave: "doencas", texto: "Tem alguma doença de pele ou condição de saúde relevante?" },
  { chave: "queixa", texto: "O que mais te incomoda hoje e o que gostaria de melhorar?" },
];
