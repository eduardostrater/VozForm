namespace AdConnector.Service.Modelos;

/// <summary>Registro crudo leido de Active Directory, antes de normalizar.</summary>
public sealed record UsuarioDirectorio(
    string? Cuenta,
    string? NombreCompleto,
    string? DocumentoCrudo,
    string? Correo);

/// <summary>Registro ya normalizado y validado, listo para enviar a la base de datos.</summary>
public sealed record RegistroSincronizable(
    string? Cuenta,
    string? NombreCompleto,
    string Documento,
    string Correo);
