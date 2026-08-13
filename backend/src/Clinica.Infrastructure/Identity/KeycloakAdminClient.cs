using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Clinica.Infrastructure.Identity;

public class KeycloakAdminOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string Realm { get; set; } = "clinica";
    public string ClientId { get; set; } = "clinica-admin";
    public string ClientSecret { get; set; } = string.Empty;
}

public record ContaDaEquipe(
    string Id,
    string Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    IReadOnlyList<string> Roles);

public record NovaConta(
    string Username,
    string? Email,
    string FirstName,
    string LastName,
    string Password,
    string Role);

/// <summary>
/// Fala com a Admin API do Keycloak para administrar as contas da equipe.
///
/// Regra que não pode ser quebrada: o <c>tenant_id</c> da conta criada vem SEMPRE do
/// token de quem está criando, nunca do formulário. É o mesmo princípio que vale para
/// paciente e atendimento (ADR 0001) — a diferença é que aqui um descuido daria a alguém
/// acesso permanente a outra clínica, em vez de um registro errado.
/// </summary>
public class KeycloakAdminClient(
    HttpClient http,
    IOptions<KeycloakAdminOptions> options)
{
    private readonly KeycloakAdminOptions _opcoes = options.Value;

    private string? _token;
    private DateTimeOffset _expiraEm = DateTimeOffset.MinValue;

    private string Base => $"{_opcoes.BaseUrl}/admin/realms/{_opcoes.Realm}";

    public async Task<IReadOnlyList<ContaDaEquipe>> ListarDaClinicaAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        await AutenticarAsync(ct);

        // O Keycloak filtra por atributo com q=chave:valor. Sem esse filtro, a tela de
        // uma clínica listaria a equipe de todas.
        var usuarios = await http.GetFromJsonAsync<List<UsuarioKeycloak>>(
            $"{Base}/users?q=tenant_id:{tenantId}&max=200", ct) ?? [];

        var contas = new List<ContaDaEquipe>();

        foreach (var u in usuarios)
        {
            // Defesa em profundidade: se o filtro do servidor falhar ou mudar de
            // sintaxe entre versões, conferimos o atributo de novo aqui.
            if (!PertenceA(u, tenantId))
            {
                continue;
            }

            var papeis = await http.GetFromJsonAsync<List<PapelKeycloak>>(
                $"{Base}/users/{u.Id}/role-mappings/realm", ct) ?? [];

            contas.Add(new ContaDaEquipe(
                u.Id,
                u.Username,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Enabled,
                papeis.Select(p => p.Name)
                    .Where(n => n is "OWNER" or "DOCTOR" or "SECRETARY" or "FINANCE")
                    .ToArray()));
        }

        return contas.OrderBy(c => c.FirstName).ToArray();
    }

    /// <summary>Cria a conta já carimbada com a clínica e com o papel escolhido.</summary>
    public async Task<string> CriarAsync(Guid tenantId, NovaConta conta, CancellationToken ct = default)
    {
        await AutenticarAsync(ct);

        var criacao = await http.PostAsJsonAsync($"{Base}/users", new
        {
            username = conta.Username,
            email = conta.Email,
            firstName = conta.FirstName,
            lastName = conta.LastName,
            enabled = true,
            emailVerified = true,
            attributes = new Dictionary<string, string[]>
            {
                // Do token de quem cria. Nunca do formulário.
                ["tenant_id"] = [tenantId.ToString()],
            },
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = conta.Password,
                    // Senha definitiva, não provisória: a troca fica para quando a tela
                    // de "alterar minha senha" existir.
                    temporary = false,
                },
            },
        }, ct);

        if (criacao.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new ContaJaExisteException(conta.Username);
        }

        criacao.EnsureSuccessStatusCode();

        // O Keycloak devolve o id apenas no cabeçalho Location.
        var id = criacao.Headers.Location?.Segments.LastOrDefault()?.Trim('/')
            ?? throw new InvalidOperationException("Keycloak nao devolveu o id da conta criada.");

        await AtribuirPapelAsync(id, conta.Role, ct);

        return id;
    }

    private async Task AtribuirPapelAsync(string usuarioId, string papel, CancellationToken ct)
    {
        var definicao = await http.GetFromJsonAsync<PapelKeycloak>($"{Base}/roles/{papel}", ct)
            ?? throw new InvalidOperationException($"Papel '{papel}' nao existe no realm.");

        var resposta = await http.PostAsJsonAsync(
            $"{Base}/users/{usuarioId}/role-mappings/realm",
            new[] { new { id = definicao.Id, name = definicao.Name } },
            ct);

        resposta.EnsureSuccessStatusCode();
    }

    private static bool PertenceA(UsuarioKeycloak usuario, Guid tenantId) =>
        usuario.Attributes is not null
        && usuario.Attributes.TryGetValue("tenant_id", out var valores)
        && valores.Any(v => Guid.TryParse(v, out var g) && g == tenantId);

    private async Task AutenticarAsync(CancellationToken ct)
    {
        // Margem de 30s: token que expira no meio da chamada seguinte não serve.
        if (_token is not null && DateTimeOffset.UtcNow < _expiraEm.AddSeconds(-30))
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            return;
        }

        var resposta = await http.PostAsync(
            $"{_opcoes.BaseUrl}/realms/{_opcoes.Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _opcoes.ClientId,
                ["client_secret"] = _opcoes.ClientSecret,
            }),
            ct);

        resposta.EnsureSuccessStatusCode();

        var token = await resposta.Content.ReadFromJsonAsync<TokenKeycloak>(ct)
            ?? throw new InvalidOperationException("Resposta de token vazia.");

        _token = token.AccessToken;
        _expiraEm = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    private record UsuarioKeycloak(
        string Id,
        string Username,
        string? Email,
        string? FirstName,
        string? LastName,
        bool Enabled,
        Dictionary<string, string[]>? Attributes);

    private record PapelKeycloak(string Id, string Name);

    private record TokenKeycloak(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

public class ContaJaExisteException(string username)
    : Exception($"Ja existe uma conta com o usuario '{username}'.");
