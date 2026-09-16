using AdConnector.Core.Configuracion;
using AdConnector.Core.Datos;
using AdConnector.Core.Directorio;
using AdConnector.Core.Sincronizacion;
using Microsoft.Extensions.Options;

namespace AdConnector.Service.Api;

public static class PuntosApi
{
    public static void MapearApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/estado", (EstadoMotor estado, IOptionsMonitor<OpcionesAdConnector> opciones) =>
        {
            var configuracion = opciones.CurrentValue;

            return Results.Ok(new
            {
                servicioIniciadoEn = estado.IniciadoEn,
                ejecucionEnCurso = estado.EjecucionEnCurso,
                proximaEjecucion = estado.ProximaEjecucion,
                intervaloMinutos = configuracion.Sincronizacion.IntervaloMinutos,
                modoSimulacion = configuracion.Sincronizacion.ModoSimulacion,
                soloCorreosVacios = configuracion.Sincronizacion.SoloCorreosVacios,
                servidorDirectorio = configuracion.ActiveDirectory.Servidor,
                baseDn = configuracion.ActiveDirectory.BaseDn,
                ultimoError = estado.UltimoError,
                enCurso = estado.EnCurso,
                ultimaEjecucion = estado.Ultimo
            });
        });

        api.MapGet("/historial", async (IRepositorioPersonas repositorio, CancellationToken cancelacion, int? top) =>
        {
            try
            {
                var filas = await repositorio.ObtenerHistorialAsync(Math.Clamp(top ?? 20, 1, 200), cancelacion);
                return Results.Ok(filas);
            }
            catch (Exception excepcion)
            {
                return Results.Problem(excepcion.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        api.MapPost("/ejecutar", async (MotorSincronizacion motor, CancellationToken cancelacion) =>
        {
            try
            {
                var resultado = await motor.EjecutarAsync(cancelacion);
                return Results.Ok(resultado);
            }
            catch (InvalidOperationException excepcion)
            {
                return Results.Conflict(new { mensaje = excepcion.Message });
            }
        });

        api.MapGet("/diagnostico", async (
            ILectorDirectorioActivo directorio,
            IRepositorioPersonas repositorio,
            CancellationToken cancelacion) =>
        {
            string? errorDirectorio = null;
            string? errorBaseDatos = null;

            try { directorio.ProbarConexion(); }
            catch (Exception excepcion) { errorDirectorio = excepcion.Message; }

            try { await repositorio.ProbarConexionAsync(cancelacion); }
            catch (Exception excepcion) { errorBaseDatos = excepcion.Message; }

            var todoBien = errorDirectorio is null && errorBaseDatos is null;

            return Results.Json(new
            {
                activeDirectory = new { ok = errorDirectorio is null, error = errorDirectorio },
                baseDatos = new { ok = errorBaseDatos is null, error = errorBaseDatos }
            }, statusCode: todoBien ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });

        // Sonda simple para el monitoreo de infraestructura.
        app.MapGet("/health", (EstadoMotor estado) =>
            estado.UltimoError is null
                ? Results.Ok(new { estado = "OK" })
                : Results.Json(new { estado = "DEGRADADO", error = estado.UltimoError },
                    statusCode: StatusCodes.Status503ServiceUnavailable));
    }
}
