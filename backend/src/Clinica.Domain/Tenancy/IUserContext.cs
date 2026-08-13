namespace Clinica.Domain.Tenancy;

/// <summary>
/// Quem é a pessoa da requisição — complementar ao <see cref="ITenantContext"/>, que diz
/// de qual clínica ela é.
///
/// Existe porque algumas regras dependem do indivíduo, não só do papel: a doutora vê a
/// própria agenda, e "própria" só faz sentido sabendo qual usuário está logado.
/// </summary>
public interface IUserContext
{
    /// <summary>Identificador do usuário no Keycloak (claim <c>sub</c>).</summary>
    string? UserId { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool HasRole(string role);
}
