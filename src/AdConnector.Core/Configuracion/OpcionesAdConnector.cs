namespace AdConnector.Core.Configuracion;

/// <summary>Raiz de configuracion del conector, enlazada a la seccion "AdConnector".</summary>
public sealed class OpcionesAdConnector
{
    public const string Seccion = "AdConnector";

    public OpcionesDirectorioActivo ActiveDirectory { get; set; } = new();
    public OpcionesBaseDatos BaseDatos { get; set; } = new();
    public OpcionesSincronizacion Sincronizacion { get; set; } = new();
}

public sealed class OpcionesDirectorioActivo
{
    /// <summary>Host o FQDN del controlador de dominio (o del dominio, para que resuelva por SRV).</summary>
    public string Servidor { get; set; } = string.Empty;

    public int Puerto { get; set; } = 389;

    /// <summary>LDAPS. Requiere puerto 636 y un certificado confiable en el servidor IIS.</summary>
    public bool UsarSsl { get; set; }

    /// <summary>
    /// Omite la validacion del certificado del controlador de dominio.
    /// Solo para entornos con CA interna aun no distribuida en el servidor. Mantener en false en produccion.
    /// </summary>
    public bool ConfiarCertificadoServidor { get; set; }

    /// <summary>DN base de la busqueda, por ejemplo "OU=Usuarios,DC=cmh,DC=local".</summary>
    public string BaseDn { get; set; } = string.Empty;

    /// <summary>Filtro LDAP. Por defecto usuarios persona habilitados (excluye cuentas deshabilitadas).</summary>
    public string Filtro { get; set; } =
        "(&(objectClass=user)(objectCategory=person)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))";

    /// <summary>
    /// true = se autentica con la identidad del grupo de aplicaciones de IIS (recomendado: no guarda contrasenas).
    /// false = usa Usuario/Password/Dominio.
    /// </summary>
    public bool UsarCredencialesDelProceso { get; set; } = true;

    public string Usuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Dominio { get; set; } = string.Empty;

    /// <summary>Tamano de pagina LDAP. AD limita a 1000 por consulta, de ahi la paginacion.</summary>
    public int TamanoPagina { get; set; } = 500;

    public int TimeoutSegundos { get; set; } = 120;

    // Atributos LDAP configurables: si el campo "fax" de CMH no es el estandar, se cambia aqui.
    public string AtributoNombreCompleto { get; set; } = "displayName";
    public string AtributoDocumento { get; set; } = "facsimileTelephoneNumber";
    public string AtributoCorreo { get; set; } = "mail";
    public string AtributoCuenta { get; set; } = "sAMAccountName";
}

public sealed class OpcionesBaseDatos
{
    public string CadenaConexion { get; set; } = string.Empty;

    public string Esquema { get; set; } = "ads";
    public string TablaDirectorio { get; set; } = "ADC_DIRECTORIO";
    public string ProcedimientoActualiza { get; set; } = "USP_ADC_ACTUALIZA_CORREO";
    public string TablaLog { get; set; } = "ADC_SINCRONIZACION_LOG";

    public int TimeoutSegundos { get; set; } = 300;
}

public sealed class OpcionesSincronizacion
{
    public int IntervaloMinutos { get; set; } = 60;

    public bool EjecutarAlIniciar { get; set; } = true;

    /// <summary>
    /// true = lee AD y calcula coincidencias pero NO actualiza la tabla de personas.
    /// Arranca en true a proposito: la primera ejecucion real debe ser una decision explicita.
    /// </summary>
    public bool ModoSimulacion { get; set; } = true;

    /// <summary>true = solo completa correos vacios; false = tambien sobrescribe los existentes.</summary>
    public bool SoloCorreosVacios { get; set; } = true;

    /// <summary>Longitud esperada del documento (DNI peruano = 8). 0 desactiva la validacion.</summary>
    public int LongitudDocumento { get; set; } = 8;

    /// <summary>Completa con ceros a la izquierda los documentos mas cortos que LongitudDocumento.</summary>
    public bool RellenarConCeros { get; set; } = true;

    /// <summary>Corta la ejecucion si AD devuelve menos registros que este minimo (proteccion ante lecturas parciales).</summary>
    public int MinimoRegistrosEsperados { get; set; } = 1;
}
