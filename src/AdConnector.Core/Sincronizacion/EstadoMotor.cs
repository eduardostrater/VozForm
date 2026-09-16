using AdConnector.Core.Modelos;

namespace AdConnector.Core.Sincronizacion;

/// <summary>Estado en memoria que alimenta el tablero de monitoreo.</summary>
public sealed class EstadoMotor
{
    private readonly object _candado = new();

    private ResultadoSincronizacion? _ultimo;
    private ResultadoSincronizacion? _enCurso;

    public DateTimeOffset IniciadoEn { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProximaEjecucion { get; private set; }
    public string? UltimoError { get; private set; }

    public bool EjecucionEnCurso
    {
        get { lock (_candado) return _enCurso is not null; }
    }

    public ResultadoSincronizacion? Ultimo
    {
        get { lock (_candado) return _ultimo; }
    }

    public ResultadoSincronizacion? EnCurso
    {
        get { lock (_candado) return _enCurso; }
    }

    public void MarcarInicio(ResultadoSincronizacion resultado)
    {
        lock (_candado) _enCurso = resultado;
    }

    public void MarcarFin(ResultadoSincronizacion resultado)
    {
        lock (_candado)
        {
            _enCurso = null;
            _ultimo = resultado;
            UltimoError = resultado.Estado == "ERROR" ? resultado.Mensaje : null;
        }
    }

    public void ProgramarProxima(DateTimeOffset momento)
    {
        lock (_candado) ProximaEjecucion = momento;
    }
}
