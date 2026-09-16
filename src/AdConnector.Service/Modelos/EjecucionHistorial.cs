namespace AdConnector.Service.Modelos;

/// <summary>Fila del historial de ejecuciones que consume el tablero de monitoreo.</summary>
public sealed record EjecucionHistorial(
    Guid EjecucionId,
    DateTime Inicio,
    DateTime? Fin,
    string Estado,
    bool Simulacion,
    int Leidos,
    int Validos,
    int Actualizados,
    int SinCoincidencia,
    int Ambiguos,
    string? Mensaje);
