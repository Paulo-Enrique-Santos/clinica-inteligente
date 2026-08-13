using Clinica.Domain.Tenancy;

namespace Clinica.Domain.Professionals;

/// <summary>
/// Quem atende: doutora, esteticista, biomédica.
///
/// Deliberadamente separado do usuário do Keycloak. Uma profissional precisa existir na
/// agenda desde o primeiro dia, mesmo sem login — e há casos em que nunca terá um
/// (profissional que só atende às terças e não mexe no sistema). Amarrar agenda a conta
/// de acesso obrigaria a criar login fantasma para conseguir agendar.
/// </summary>
public class Professional : TenantEntity
{
    public required string FullName { get; set; }

    /// <summary>Como aparece para a paciente: "Dra. Carla".</summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Vínculo opcional com o usuário do Keycloak (claim <c>sub</c>). Quando presente,
    /// permite que a profissional veja a própria agenda ao entrar no sistema.
    /// </summary>
    public string? KeycloakUserId { get; set; }

    public string? Specialty { get; set; }

    /// <summary>
    /// Desligamento não apaga: o histórico de atendimentos precisa continuar de pé.
    /// </summary>
    public bool Active { get; set; } = true;
}
