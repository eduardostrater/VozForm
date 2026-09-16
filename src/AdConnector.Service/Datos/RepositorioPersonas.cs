using System.Data;
using AdConnector.Service.Configuracion;
using AdConnector.Service.Modelos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AdConnector.Service.Datos;

public interface IRepositorioPersonas
{
    Task ProbarConexionAsync(CancellationToken cancelacion);

    Task CargarStagingAsync(Guid ejecucionId, IReadOnlyList<RegistroSincronizable> registros, CancellationToken cancelacion);

    Task<(int retorno, string mensaje)> EjecutarActualizacionAsync(
        Guid ejecucionId, ResultadoSincronizacion resultado, CancellationToken cancelacion);

    Task<IReadOnlyList<Dictionary<string, object?>>> ObtenerHistorialAsync(int top, CancellationToken cancelacion);
}

public sealed class RepositorioPersonas : IRepositorioPersonas
{
    private readonly IOptionsMonitor<OpcionesAdConnector> _opciones;
    private readonly ILogger<RepositorioPersonas> _log;

    public RepositorioPersonas(IOptionsMonitor<OpcionesAdConnector> opciones, ILogger<RepositorioPersonas> log)
    {
        _opciones = opciones;
        _log = log;
    }

    public async Task ProbarConexionAsync(CancellationToken cancelacion)
    {
        await using var conexion = await AbrirAsync(cancelacion);
    }

    public async Task CargarStagingAsync(
        Guid ejecucionId, IReadOnlyList<RegistroSincronizable> registros, CancellationToken cancelacion)
    {
        var bd = _opciones.CurrentValue.BaseDatos;
        var destino = NombreCompleto(bd.Esquema, bd.TablaDirectorio);

        await using var conexion = await AbrirAsync(cancelacion);

        using var tabla = ConstruirTabla(ejecucionId, registros);

        using var copia = new SqlBulkCopy(conexion)
        {
            DestinationTableName = destino,
            BatchSize = 2000,
            BulkCopyTimeout = bd.TimeoutSegundos
        };

        foreach (DataColumn columna in tabla.Columns)
            copia.ColumnMappings.Add(columna.ColumnName, columna.ColumnName);

        await copia.WriteToServerAsync(tabla, cancelacion);

        _log.LogInformation("Staging cargado con {Total} registros para la ejecucion {EjecucionId}.",
            registros.Count, ejecucionId);
    }

    public async Task<(int retorno, string mensaje)> EjecutarActualizacionAsync(
        Guid ejecucionId, ResultadoSincronizacion resultado, CancellationToken cancelacion)
    {
        var configuracion = _opciones.CurrentValue;
        var bd = configuracion.BaseDatos;

        await using var conexion = await AbrirAsync(cancelacion);
        await using var comando = conexion.CreateCommand();

        comando.CommandType = CommandType.StoredProcedure;
        comando.CommandText = NombreCompleto(bd.Esquema, bd.ProcedimientoActualiza);
        comando.CommandTimeout = bd.TimeoutSegundos;

        comando.Parameters.Add("@pi_EjecucionId", SqlDbType.UniqueIdentifier).Value = ejecucionId;
        comando.Parameters.Add("@pi_Simulacion", SqlDbType.Bit).Value = configuracion.Sincronizacion.ModoSimulacion;
        comando.Parameters.Add("@pi_SoloCorreosVacios", SqlDbType.Bit).Value = configuracion.Sincronizacion.SoloCorreosVacios;
        comando.Parameters.Add("@pi_RegistrosLeidos", SqlDbType.Int).Value = resultado.LeidosDelDirectorio;
        comando.Parameters.Add("@pi_Usuario", SqlDbType.VarChar, 50).Value = UsuarioDelProceso();

        var actualizados = Salida(comando, "@po_Actualizados", SqlDbType.Int);
        var sinCoincidencia = Salida(comando, "@po_SinCoincidencia", SqlDbType.Int);
        var ambiguos = Salida(comando, "@po_Ambiguos", SqlDbType.Int);
        var yaTenian = Salida(comando, "@po_YaTenianCorreo", SqlDbType.Int);
        var retorno = Salida(comando, "@po_Retorno", SqlDbType.Int);

        var mensaje = comando.Parameters.Add("@po_Mensaje", SqlDbType.NVarChar, 4000);
        mensaje.Direction = ParameterDirection.Output;

        await comando.ExecuteNonQueryAsync(cancelacion);

        resultado.Actualizados = Entero(actualizados);
        resultado.SinCoincidencia = Entero(sinCoincidencia);
        resultado.Ambiguos = Entero(ambiguos);
        resultado.YaTenianCorreo = Entero(yaTenian);

        return (Entero(retorno), mensaje.Value as string ?? string.Empty);
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ObtenerHistorialAsync(
        int top, CancellationToken cancelacion)
    {
        var bd = _opciones.CurrentValue.BaseDatos;

        await using var conexion = await AbrirAsync(cancelacion);
        await using var comando = conexion.CreateCommand();

        comando.CommandTimeout = bd.TimeoutSegundos;
        comando.CommandText = $"""
            SELECT TOP (@pi_Top)
                   GEJECUCION_ID,
                   DFECHA_INICIO,
                   DFECHA_FIN,
                   CESTADO,
                   BSIMULACION,
                   NREGISTROS_LEIDOS,
                   NREGISTROS_VALIDOS,
                   NREGISTROS_ACTUALIZADOS,
                   NREGISTROS_SIN_COINCIDENCIA,
                   NREGISTROS_AMBIGUOS,
                   VMENSAJE
            FROM {NombreCompleto(bd.Esquema, bd.TablaLog)}
            ORDER BY DFECHA_INICIO DESC;
            """;
        comando.Parameters.Add("@pi_Top", SqlDbType.Int).Value = top;

        var filas = new List<Dictionary<string, object?>>();
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);

        while (await lector.ReadAsync(cancelacion))
        {
            var fila = new Dictionary<string, object?>(lector.FieldCount);
            for (var i = 0; i < lector.FieldCount; i++)
                fila[lector.GetName(i)] = lector.IsDBNull(i) ? null : lector.GetValue(i);

            filas.Add(fila);
        }

        return filas;
    }

    private async Task<SqlConnection> AbrirAsync(CancellationToken cancelacion)
    {
        var cadena = _opciones.CurrentValue.BaseDatos.CadenaConexion;
        if (string.IsNullOrWhiteSpace(cadena))
            throw new InvalidOperationException("AdConnector:BaseDatos:CadenaConexion no esta configurada.");

        var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);
        return conexion;
    }

    private static DataTable ConstruirTabla(Guid ejecucionId, IReadOnlyList<RegistroSincronizable> registros)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("GEJECUCION_ID", typeof(Guid));
        tabla.Columns.Add("VCUENTA_CODIGO", typeof(string));
        tabla.Columns.Add("VNOMBRE_COMPLETO", typeof(string));
        tabla.Columns.Add("CDOCUMENTO", typeof(string));
        tabla.Columns.Add("VCORREO", typeof(string));

        foreach (var registro in registros)
        {
            tabla.Rows.Add(
                ejecucionId,
                (object?)registro.Cuenta ?? DBNull.Value,
                (object?)registro.NombreCompleto ?? DBNull.Value,
                registro.Documento,
                registro.Correo);
        }

        return tabla;
    }

    private static SqlParameter Salida(SqlCommand comando, string nombre, SqlDbType tipo)
    {
        var parametro = comando.Parameters.Add(nombre, tipo);
        parametro.Direction = ParameterDirection.Output;
        return parametro;
    }

    private static int Entero(SqlParameter parametro) =>
        parametro.Value is int valor ? valor : 0;

    private static string UsuarioDelProceso()
    {
        var usuario = Environment.UserName;
        return string.IsNullOrWhiteSpace(usuario) ? "ADCONNECTOR" : Acotar(usuario, 50);
    }

    private static string Acotar(string valor, int maximo) =>
        valor.Length <= maximo ? valor : valor[..maximo];

    /// <summary>
    /// Arma [esquema].[objeto] validando el identificador: los nombres vienen de configuracion
    /// y terminan concatenados en T-SQL, asi que no pueden aceptarse tal cual.
    /// </summary>
    private static string NombreCompleto(string esquema, string objeto) =>
        $"{Identificador(esquema)}.{Identificador(objeto)}";

    private static string Identificador(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException("Se configuro un nombre de objeto de base de datos vacio.");

        foreach (var caracter in valor)
        {
            if (!char.IsLetterOrDigit(caracter) && caracter != '_')
                throw new InvalidOperationException(
                    $"El nombre de objeto '{valor}' contiene caracteres no permitidos. Use solo letras, digitos y guion bajo.");
        }

        return $"[{valor}]";
    }
}
