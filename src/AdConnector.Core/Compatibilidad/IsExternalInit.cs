#if NETSTANDARD2_0

using System.ComponentModel;

// Los tipos 'record' y las propiedades 'init' requieren este tipo marcador, que no existe
// en netstandard2.0. Declararlo aqui permite compilar la misma fuente para ambos destinos.
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}

#endif
