using AdConnector.Core.Configuracion;
using AdConnector.Core.Datos;
using AdConnector.Core.Directorio;
using AdConnector.Core.Modelos;
using AdConnector.Core.Sincronizacion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AdConnector.Core;

/// <summary>
/// Construye el conector sin contenedor de dependencias, para backends que no usan
/// el host generico de .NET (por ejemplo una aplicacion de .NET Framework).
/// </summary>
public static class FabricaAdConnector
{
    public static ConectorAd Crear(OpcionesAdConnector opciones, ILoggerFactory? registro = null)
    {
        if (opciones is null)
            throw new ArgumentNullException(nameof(opciones));

        var fabrica = registro ?? NullLoggerFactory.Instance;
        var monitor = new OpcionesFijas<OpcionesAdConnector>(opciones);
        var estado = new EstadoMotor();

        var directorio = new LectorDirectorioActivo(monitor, fabrica.CreateLogger<LectorDirectorioActivo>());
        var repositorio = new RepositorioPersonas(monitor, fabrica.CreateLogger<RepositorioPersonas>());
        var motor = new MotorSincronizacion(
            directorio, repositorio, monitor, estado, fabrica.CreateLogger<MotorSincronizacion>());

        return new ConectorAd(motor, estado, directorio, repositorio);
    }
}

/// <summary>Fachada con todo lo que un backend necesita del conector.</summary>
public sealed class ConectorAd
{
    internal ConectorAd(
        MotorSincronizacion motor,
        EstadoMotor estado,
        ILectorDirectorioActivo directorio,
        IRepositorioPersonas repositorio)
    {
        Motor = motor;
        Estado = estado;
        Directorio = directorio;
        Repositorio = repositorio;
    }

    public MotorSincronizacion Motor { get; }

    /// <summary>Estado en memoria de la ultima ejecucion y de la que este en curso.</summary>
    public EstadoMotor Estado { get; }

    public ILectorDirectorioActivo Directorio { get; }

    public IRepositorioPersonas Repositorio { get; }

    /// <summary>Ejecuta una sincronizacion completa. Lanza si ya hay una en curso.</summary>
    public Task<ResultadoSincronizacion> SincronizarAsync(CancellationToken cancelacion = default) =>
        Motor.EjecutarAsync(cancelacion);
}
