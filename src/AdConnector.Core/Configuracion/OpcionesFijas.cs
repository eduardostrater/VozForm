using Microsoft.Extensions.Options;

namespace AdConnector.Core.Configuracion;

/// <summary>
/// Adaptador de <see cref="IOptionsMonitor{TOptions}"/> para consumidores que no usan
/// inyeccion de dependencias ni el sistema de configuracion de .NET.
/// </summary>
internal sealed class OpcionesFijas<T> : IOptionsMonitor<T>
{
    public OpcionesFijas(T valor) => CurrentValue = valor;

    public T CurrentValue { get; }

    public T Get(string? nombre) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> escucha) => new SinSuscripcion();

    private sealed class SinSuscripcion : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
