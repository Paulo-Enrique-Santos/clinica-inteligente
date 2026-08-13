using System.Security.Claims;
using Clinica.Domain.Tenancy;

namespace Clinica.Api.Tenancy;

public sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public string? UserId =>
        // "sub" é o nome no padrão OIDC; o ASP.NET pode tê-lo mapeado para
        // NameIdentifier dependendo da configuração de mapeamento de claims.
        accessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public IReadOnlyCollection<string> Roles =>
        accessor.HttpContext?.User
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray()
        ?? [];

    public bool HasRole(string role) =>
        accessor.HttpContext?.User.IsInRole(role) ?? false;
}
