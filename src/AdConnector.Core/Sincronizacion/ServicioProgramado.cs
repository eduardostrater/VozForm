using AdConnector.Core.Configuracion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdConnector.Core.Sincronizacion;

/// <summary>
/// Motor residente: dispara la sincronizacion cada IntervaloMinutos mientras la aplicacion viva.
/// Requiere que el grupo de aplicaciones de IIS este en AlwaysRunning y sin reciclaje por inactividad.
/// </summary>
public sealed class ServicioProgramado : BackgroundService
{
    private readonly IServiceScopeFactory _fabricaAmbitos;
    private readonly IOptionsMonitor<OpcionesAdConnector> _opciones;
    private readonly EstadoMotor _estado;
    private readonly ILogger<ServicioProgramado> _log;

    public ServicioProgramado(
        IServiceScopeFactory fabricaAmbitos,
        IOptionsMonitor<OpcionesAdConnector> opciones,
        EstadoMotor estado,
        ILogger<ServicioProgramado> log)
    {
        _fabricaAmbitos = fabricaAmbitos;
        _opciones = opciones;
        _estado = estado;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        var sincronizacion = _opciones.CurrentValue.Sincronizacion;

        if (!sincronizacion.EjecutarAlIniciar)
        {
            var espera = Intervalo();
            _estado.ProgramarProxima(DateTimeOffset.UtcNow.Add(espera));
            await EsperarAsync(espera, cancelacion);
        }

        while (!cancelacion.IsCancellationRequested)
        {
            try
            {
                using var ambito = _fabricaAmbitos.CreateScope();
                var motor = ambito.ServiceProvider.GetRequiredService<MotorSincronizacion>();
                await motor.EjecutarAsync(cancelacion);
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                break;
            }
            catch (Exception excepcion)
            {
                // El motor ya registra sus propios errores; esto cubre fallos al resolver dependencias.
                _log.LogError(excepcion, "Error no controlado en el ciclo programado.");
            }

            var intervalo = Intervalo();
            _estado.ProgramarProxima(DateTimeOffset.UtcNow.Add(intervalo));
            await EsperarAsync(intervalo, cancelacion);
        }
    }

    private TimeSpan Intervalo()
    {
        var minutos = _opciones.CurrentValue.Sincronizacion.IntervaloMinutos;
        return TimeSpan.FromMinutes(minutos < 1 ? 1 : minutos);
    }

    private static async Task EsperarAsync(TimeSpan espera, CancellationToken cancelacion)
    {
        try
        {
            await Task.Delay(espera, cancelacion);
        }
        catch (OperationCanceledException)
        {
            // Apagado ordenado de la aplicacion.
        }
    }
}
