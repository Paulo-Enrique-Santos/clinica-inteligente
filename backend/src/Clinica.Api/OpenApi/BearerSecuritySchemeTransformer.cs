using Microsoft.AspNetCore.OpenApi;
// Microsoft.OpenApi 2.x achatou os namespaces: nao existe mais Microsoft.OpenApi.Models,
// os tipos vivem direto em Microsoft.OpenApi.
using Microsoft.OpenApi;

namespace Clinica.Api.OpenApi;

/// <summary>
/// Declara no documento OpenAPI que a API usa Bearer token (JWT).
///
/// Sem isso o Swagger UI nao mostra o botao "Authorize", e como praticamente todo
/// endpoint daqui exige token, a interface serviria apenas para olhar — nao para testar.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeId = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Cole apenas o access token do Keycloak (sem escrever 'Bearer' na frente). " +
                "Para obter um: veja a secao 'Gerar um token' no infra/README.md.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, document)] = new List<string>(),
        });

        return Task.CompletedTask;
    }
}
