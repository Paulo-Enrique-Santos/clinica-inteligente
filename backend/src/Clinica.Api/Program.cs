using System.Security.Claims;
using Clinica.Api.Endpoints;
using Clinica.Api.Tenancy;
using Clinica.Domain.Tenancy;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Tenancy
// ---------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

// ---------------------------------------------------------------------------
// Autenticacao (Keycloak)
// ---------------------------------------------------------------------------
var authority = builder.Configuration["Keycloak:Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority nao configurado.");
var audience = builder.Configuration["Keycloak:Audience"]
    ?? throw new InvalidOperationException("Keycloak:Audience nao configurado.");

builder.Services.AddSingleton<IClaimsTransformation, KeycloakRolesTransformation>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;

        // Em dev o Keycloak roda em HTTP puro. Em producao (Fase 14) isto volta a ser
        // obrigatorio — e o IsProduction() garante que ninguem esqueca.
        options.RequireHttpsMetadata = builder.Environment.IsProduction();

        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// Dados
//
// Conecta como clinica_app (usuario de runtime, sem privilegio de dono), para que as
// policies de RLS efetivamente incidam sobre a API. Ver ADR 0001.
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Clinica")
    ?? throw new InvalidOperationException("ConnectionStrings:Clinica nao configurada.");

builder.Services.AddClinicaPersistence(connectionString);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .AllowAnonymous()
   .WithName("Health");

app.MapPatientEndpoints();

app.Run();

/// <summary>
/// Exposta para os testes de integracao (WebApplicationFactory precisa de um tipo de
/// entrada acessivel).
/// </summary>
public partial class Program;
