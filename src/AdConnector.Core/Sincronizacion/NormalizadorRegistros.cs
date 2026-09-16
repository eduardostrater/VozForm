using System.Text;
using AdConnector.Core.Configuracion;
using AdConnector.Core.Modelos;

namespace AdConnector.Core.Sincronizacion;

/// <summary>
/// Convierte las entradas crudas de AD en registros aptos para el cruce con la tabla de personas.
/// El campo fax de AD suele traer separadores, espacios o extensiones; aqui se reduce a digitos.
/// </summary>
public static class NormalizadorRegistros
{
    public static List<RegistroSincronizable> Normalizar(
        IEnumerable<UsuarioDirectorio> usuarios,
        OpcionesSincronizacion opciones,
        ResultadoSincronizacion resultado)
    {
        var salida = new List<RegistroSincronizable>();

        foreach (var usuario in usuarios)
        {
            var correo = usuario.Correo?.Trim();
            if (string.IsNullOrWhiteSpace(correo) || !EsCorreoPlausible(correo))
            {
                resultado.DescartadosSinCorreo++;
                continue;
            }

            var documento = SoloDigitos(usuario.DocumentoCrudo);
            if (documento.Length == 0)
            {
                resultado.DescartadosSinDocumento++;
                continue;
            }

            if (opciones.LongitudDocumento > 0)
            {
                if (documento.Length < opciones.LongitudDocumento && opciones.RellenarConCeros)
                    documento = documento.PadLeft(opciones.LongitudDocumento, '0');

                if (documento.Length != opciones.LongitudDocumento)
                {
                    resultado.DescartadosDocumentoInvalido++;
                    continue;
                }
            }

            salida.Add(new RegistroSincronizable(
                Cuenta: Recortar(usuario.Cuenta, 256),
                NombreCompleto: Recortar(usuario.NombreCompleto, 256),
                Documento: documento,
                Correo: correo.ToLowerInvariant()));
        }

        resultado.Validos = salida.Count;
        return salida;
    }

    private static string SoloDigitos(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        var constructor = new StringBuilder(valor.Length);
        foreach (var caracter in valor)
        {
            if (char.IsDigit(caracter))
                constructor.Append(caracter);
        }

        return constructor.ToString();
    }

    private static bool EsCorreoPlausible(string correo)
    {
        var arroba = correo.IndexOf('@');
        if (arroba <= 0 || arroba == correo.Length - 1)
            return false;

        return correo.IndexOf('@', arroba + 1) < 0
               && correo.IndexOf(' ') < 0
               && correo.LastIndexOf('.') > arroba;
    }

    private static string? Recortar(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        valor = valor.Trim();
        return valor.Length <= maximo ? valor : valor.Substring(0, maximo);
    }
}
