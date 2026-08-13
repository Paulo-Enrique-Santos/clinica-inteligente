using Clinica.Domain.Tenancy;

namespace Clinica.Domain.Procedures;

/// <summary>
/// Procedimento oferecido pela clínica.
///
/// Guarda preço e custo de insumos separados porque o relatório de gestão (Fase 13) precisa
/// responder "qual procedimento é mais rentável?" — e rentabilidade não é preço.
/// </summary>
public class Procedure : TenantEntity
{
    public required string Name { get; set; }

    /// <summary>
    /// Duração padrão. É o que define o fim do atendimento na agenda e, mais adiante, o que
    /// permite ao otimizador (Fase 12) saber se um procedimento cabe num vão livre.
    /// </summary>
    public int DurationMinutes { get; set; }

    public decimal Price { get; set; }

    /// <summary>Custo médio de insumos. Preço menos isto é a margem bruta.</summary>
    public decimal SuppliesCost { get; set; }

    public string? Description { get; set; }

    public bool Active { get; set; } = true;
}
