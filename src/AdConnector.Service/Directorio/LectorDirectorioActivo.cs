using System.DirectoryServices.Protocols;
using System.Net;
using AdConnector.Service.Configuracion;
using AdConnector.Service.Modelos;
using Microsoft.Extensions.Options;

namespace AdConnector.Service.Directorio;

public interface ILectorDirectorioActivo
{
    IReadOnlyList<UsuarioDirectorio> Leer(CancellationToken cancelacion);
    void ProbarConexion();
}

public sealed class LectorDirectorioActivo : ILectorDirectorioActivo
{
    private readonly IOptionsMonitor<OpcionesAdConnector> _opciones;
    private readonly ILogger<LectorDirectorioActivo> _log;

    public LectorDirectorioActivo(IOptionsMonitor<OpcionesAdConnector> opciones, ILogger<LectorDirectorioActivo> log)
    {
        _opciones = opciones;
        _log = log;
    }

    public void ProbarConexion()
    {
        var ad = _opciones.CurrentValue.ActiveDirectory;
        using var conexion = AbrirConexion(ad);
        conexion.Bind();
    }

    public IReadOnlyList<UsuarioDirectorio> Leer(CancellationToken cancelacion)
    {
        var ad = _opciones.CurrentValue.ActiveDirectory;

        if (string.IsNullOrWhiteSpace(ad.Servidor))
            throw new InvalidOperationException("AdConnector:ActiveDirectory:Servidor no esta configurado.");
        if (string.IsNullOrWhiteSpace(ad.BaseDn))
            throw new InvalidOperationException("AdConnector:ActiveDirectory:BaseDn no esta configurado.");

        using var conexion = AbrirConexion(ad);
        conexion.Bind();

        var resultados = new List<UsuarioDirectorio>(capacity: 1024);
        var control = new PageResultRequestControl(ad.TamanoPagina);

        var atributos = new[]
        {
            ad.AtributoCuenta,
            ad.AtributoNombreCompleto,
            ad.AtributoDocumento,
            ad.AtributoCorreo
        };

        var pagina = 0;
        while (true)
        {
            cancelacion.ThrowIfCancellationRequested();

            var peticion = new SearchRequest(ad.BaseDn, ad.Filtro, SearchScope.Subtree, atributos);
            peticion.Controls.Add(control);

            var respuesta = (SearchResponse)conexion.SendRequest(peticion, TimeSpan.FromSeconds(ad.TimeoutSegundos));

            if (respuesta.ResultCode != ResultCode.Success)
                throw new InvalidOperationException(
                    $"La busqueda LDAP devolvio {respuesta.ResultCode}: {respuesta.ErrorMessage}");

            foreach (SearchResultEntry entrada in respuesta.Entries)
            {
                resultados.Add(new UsuarioDirectorio(
                    Cuenta: LeerAtributo(entrada, ad.AtributoCuenta),
                    NombreCompleto: LeerAtributo(entrada, ad.AtributoNombreCompleto),
                    DocumentoCrudo: LeerAtributo(entrada, ad.AtributoDocumento),
                    Correo: LeerAtributo(entrada, ad.AtributoCorreo)));
            }

            pagina++;
            _log.LogDebug("Pagina LDAP {Pagina} leida, acumulado {Total} entradas.", pagina, resultados.Count);

            var respuestaPaginacion = respuesta.Controls
                .OfType<PageResultResponseControl>()
                .FirstOrDefault();

            // Cookie vacia = el directorio ya no tiene mas paginas.
            if (respuestaPaginacion is null || respuestaPaginacion.Cookie.Length == 0)
                break;

            control.Cookie = respuestaPaginacion.Cookie;
        }

        _log.LogInformation("Active Directory devolvio {Total} entradas en {Paginas} pagina(s).",
            resultados.Count, pagina);

        return resultados;
    }

    private LdapConnection AbrirConexion(OpcionesDirectorioActivo ad)
    {
        var identificador = new LdapDirectoryIdentifier(ad.Servidor, ad.Puerto, fullyQualifiedDnsHostName: false, connectionless: false);

        var conexion = ad.UsarCredencialesDelProceso
            ? new LdapConnection(identificador)
            : new LdapConnection(identificador, new NetworkCredential(ad.Usuario, ad.Password, ad.Dominio));

        conexion.AuthType = AuthType.Negotiate;
        conexion.SessionOptions.ProtocolVersion = 3;
        conexion.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
        conexion.Timeout = TimeSpan.FromSeconds(ad.TimeoutSegundos);

        if (ad.UsarSsl)
        {
            conexion.SessionOptions.SecureSocketLayer = true;

            if (ad.ConfiarCertificadoServidor)
            {
                _log.LogWarning(
                    "ConfiarCertificadoServidor esta activo: no se valida el certificado del controlador de dominio. " +
                    "Desactivar en produccion.");
                conexion.SessionOptions.VerifyServerCertificate = (_, _) => true;
            }
        }

        return conexion;
    }

    private static string? LeerAtributo(SearchResultEntry entrada, string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre) || !entrada.Attributes.Contains(nombre))
            return null;

        var atributo = entrada.Attributes[nombre];
        if (atributo.Count == 0)
            return null;

        // GetValues(typeof(string)) evita que un valor binario se materialice como "System.Byte[]".
        var valores = atributo.GetValues(typeof(string));
        return valores.Length > 0 ? valores[0] as string : null;
    }
}
