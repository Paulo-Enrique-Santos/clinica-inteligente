namespace Clinica.Domain.Appointments;

/// <summary>
/// Ciclo de vida do atendimento.
///
/// <code>
/// Agendado ─┬─► Confirmado ─┬─► Realizado
///           │               ├─► Faltou
///           │               └─► Cancelado
///           ├─► Faltou
///           └─► Cancelado
/// </code>
///
/// Gravado como texto no banco, não como número: quando alguém abrir o Postgres para
/// investigar um problema, "faltou" explica e "3" não.
/// </summary>
public enum AppointmentStatus
{
    Agendado,
    Confirmado,
    Realizado,
    Faltou,
    Cancelado,
}
