using AdConnector.Core.Configuracion;
using AdConnector.Core.Datos;
using AdConnector.Core.Directorio;
using AdConnector.Core.Modelos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AdConnector.Core.Sincronizacion;

/// <summary>
/// Orquesta el ciclo completo: leer AD, normalizar, cargar staging y ejecutar la actualizacion en SQL Server.
/// Una sola ejecucion a la vez: el disparo manual y el temporizador comparten el mismo semaforo.
/// </summary>
public sealed class MotorSincronizacion
{
    private readonly SemaphoreSlim _exclusion = new(1, 1);

    private readonly ILectorDirectorioActivo _directorio;
    private readonly IRepositorioPersonas _repositorio;
    private readonly IOptionsMonitor<OpcionesAdConnector> _opciones;
    private readonly EstadoMotor _estado;
    private readonly ILogger<MotorSincronizacion> _log;

    public MotorSincronizacion(
        ILectorDirectorioActivo directorio,
        IRepositorioPersonas repositorio,
        IOptionsMonitor<OpcionesAdConnector> opciones,
        EstadoMotor estado,
        ILogger<MotorSincronizacion> log)
    {
        _directorio = directorio;
        _repositorio = repositorio;
        _opciones = opciones;
        _estado = estado;
        _log = log;
    }

    public async Task<ResultadoSincronizacion> EjecutarAsync(CancellationToken cancelacion)
    {
        if (!await _exclusion.WaitAsync(TimeSpan.Zero, cancelacion))
            throw new InvalidOperationException("Ya hay una sincronizacion en curso.");

        var configuracion = _opciones.CurrentValue;

        var resultado = new ResultadoSincronizacion
        {
            EjecucionId = Guid.NewGuid(),
            Inicio = DateTimeOffset.UtcNow,
            Simulacion = configuracion.Sincronizacion.ModoSimulacion
        };

        _estado.MarcarInicio(resultado);
        var cronometro = Stopwatch.StartNew();

        try
        {
            _log.LogInformation("Ejecucion {EjecucionId} iniciada (simulacion: {Simulacion}).",
                resultado.EjecucionId, resultado.Simulacion);

            // 1. Leer Active Directory. Es sincrono por naturaleza (LdapConnection no expone API async).
            var usuarios = await Task.Run(() => _directorio.Leer(cancelacion), cancelacion);
            resultado.LeidosDelDirectorio = usuarios.Count;

            if (usuarios.Count < configuracion.Sincronizacion.MinimoRegistrosEsperados)
                throw new InvalidOperationException(
                    $"Active Directory devolvio {usuarios.Count} registros, por debajo del minimo esperado " +
                    $"({configuracion.Sincronizacion.MinimoRegistrosEsperados}). Se aborta para no dejar la tabla a medias.");

            // 2. Normalizar y descartar lo que no sirve para el cruce.
            var registros = NormalizadorRegistros.Normalizar(usuarios, configuracion.Sincronizacion, resultado);

            if (registros.Count == 0)
            {
                resultado.Estado = "SIN_DATOS";
                resultado.Mensaje = "Ningun registro de AD quedo utilizable tras la normalizacion.";
                return resultado;
            }

            // 3. Cargar staging y delegar el cruce y la actualizacion al procedimiento almacenado.
            await _repositorio.CargarStagingAsync(resultado.EjecucionId, registros, cancelacion);

            var (retorno, mensaje) = await _repositorio.EjecutarActualizacionAsync(
                resultado.EjecucionId, resultado, cancelacion);

            resultado.Mensaje = mensaje;
            resultado.Estado = retorno == 0 ? "OK" : "ERROR";

            if (retorno != 0)
                _log.LogError("La ejecucion {EjecucionId} termino con retorno {Retorno}: {Mensaje}",
                    resultado.EjecucionId, retorno, mensaje);
            else
                _log.LogInformation(
                    "Ejecucion {EjecucionId} completada: {Actualizados} actualizados, {SinCoincidencia} sin coincidencia, " +
                    "{Ambiguos} ambiguos, {YaTenian} ya tenian correo.",
                    resultado.EjecucionId, resultado.Actualizados, resultado.SinCoincidencia,
                    resultado.Ambiguos, resultado.YaTenianCorreo);

            return resultado;
        }
        catch (OperationCanceledException)
        {
            resultado.Estado = "CANCELADO";
            resultado.Mensaje = "La ejecucion fue cancelada.";
            throw;
        }
        catch (Exception excepcion)
        {
            resultado.Estado = "ERROR";
            resultado.Mensaje = excepcion.Message;
            _log.LogError(excepcion, "Fallo la ejecucion {EjecucionId}.", resultado.EjecucionId);
            return resultado;
        }
        finally
        {
            cronometro.Stop();
            resultado.Fin = DateTimeOffset.UtcNow;
            _estado.MarcarFin(resultado);
            _exclusion.Release();
        }
    }
}
