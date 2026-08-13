using Clinica.Domain.Tenancy;

namespace Clinica.Domain.Patients;

/// <summary>
/// Paciente da clinica.
/// </summary>
public class Patient : TenantEntity
{
    public required string FullName { get; set; }

    /// <summary>
    /// Telefone em E.164 (ex.: +5511987654321), sempre.
    ///
    /// Parece detalhe, mas nao e: o WhatsApp identifica conversa por numero, e a partir da
    /// Fase 3 uma thread pode existir antes do paciente (numero desconhecido). Guardar em
    /// formato livre agora significa reconciliacao manual depois.
    /// </summary>
    public required string PhoneE164 { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? Notes { get; set; }
}
