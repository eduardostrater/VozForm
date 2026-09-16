using AdConnector.Core.Configuracion;
using AdConnector.Core.Datos;
using AdConnector.Core.Directorio;
using AdConnector.Core.Sincronizacion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdConnector.Core.Extensiones;

/// <summary>Registro del conector en contenedores de inyeccion de dependencias.</summary>
public static class ExtensionesServicios
{
    /// <summary>
    /// Registra el motor y sus dependencias. No arranca nada por si solo: el consumidor
    /// decide si lo dispara a demanda o si agrega el servicio programado.
    /// </summary>
    public static IServiceCollection AgregarAdConnector(
        this IServiceCollection servicios,
        IConfiguration configuracion,
        string seccion = OpcionesAdConnector.Seccion)
    {
        servicios.AddOptions<OpcionesAdConnector>().Bind(configuracion.GetSection(seccion));

        servicios.AddSingleton<EstadoMotor>();
        servicios.AddSingleton<ILectorDirectorioActivo, LectorDirectorioActivo>();
        servicios.AddSingleton<IRepositorioPersonas, RepositorioPersonas>();
        servicios.AddSingleton<MotorSincronizacion>();

        return servicios;
    }

    /// <summary>Agrega el ciclo residente que ejecuta la sincronizacion cada IntervaloMinutos.</summary>
    public static IServiceCollection AgregarAdConnectorProgramado(this IServiceCollection servicios)
    {
        servicios.AddHostedService<ServicioProgramado>();
        return servicios;
    }
}
