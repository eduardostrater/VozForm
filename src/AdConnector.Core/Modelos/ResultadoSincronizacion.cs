namespace AdConnector.Core.Modelos;

public sealed class ResultadoSincronizacion
{
    public Guid EjecucionId { get; init; }
    public DateTimeOffset Inicio { get; init; }
    public DateTimeOffset? Fin { get; set; }
    public string Estado { get; set; } = "EN_PROCESO";
    public bool Simulacion { get; init; }

    public int LeidosDelDirectorio { get; set; }
    public int Validos { get; set; }
    public int DescartadosSinDocumento { get; set; }
    public int DescartadosSinCorreo { get; set; }
    public int DescartadosDocumentoInvalido { get; set; }

    public int Actualizados { get; set; }
    public int SinCoincidencia { get; set; }
    public int Ambiguos { get; set; }
    public int YaTenianCorreo { get; set; }

    public string? Mensaje { get; set; }

    public double DuracionSegundos =>
        Fin is null ? (DateTimeOffset.UtcNow - Inicio).TotalSeconds : (Fin.Value - Inicio).TotalSeconds;
}
