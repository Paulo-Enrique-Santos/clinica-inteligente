using Clinica.Domain.Patients;
using Clinica.Domain.Tenancy;

namespace Clinica.Domain.Anamnesis;

/// <summary>
/// Convite para a paciente preencher a ficha, entregue como link.
///
/// NÃO herda de <see cref="TenantEntity"/>, e isso é deliberado: a paciente não tem
/// login, então a clínica precisa ser descoberta A PARTIR do link. Se esta tabela
/// estivesse sob o filtro por tenant, a busca pelo token nunca acharia nada — não haveria
/// tenant para filtrar antes de o token ser lido.
///
/// O token é a credencial. Por isso: aleatório de 32 bytes, com validade, uso único e
/// nunca reaproveitado.
/// </summary>
public class AnamnesisLink
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Descoberto pelo token, não filtrado por ele.</summary>
    public Guid TenantId { get; set; }

    public Guid PatientId { get; set; }

    public required string Token { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool Valido(DateTimeOffset agora) => UsedAt is null && ExpiresAt > agora;
}

/// <summary>
/// A ficha preenchida pela paciente.
///
/// As respostas ficam em JSON porque o questionário muda com o tempo e por clínica —
/// transformar cada pergunta em coluna obrigaria uma migration a cada ajuste do formulário,
/// e quebraria fichas antigas. O que é estrutural (consentimentos, data) fica em coluna.
/// </summary>
public class AnamnesisResponse : TenantEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Respostas como objeto JSON: pergunta -> resposta.</summary>
    public required string AnswersJson { get; set; }

    /// <summary>Autoriza uso de imagem em redes sociais e material da clínica.</summary>
    public bool ImageConsent { get; set; }

    /// <summary>
    /// Consentimento LGPD para tratamento de dado de saúde. Sem isto, a clínica não
    /// deveria sequer guardar a ficha — por isso é obrigatório no envio.
    /// </summary>
    public bool DataConsent { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }
}
