using AdConnector.Service.Api;
using AdConnector.Service.Configuracion;
using AdConnector.Service.Datos;
using AdConnector.Service.Directorio;
using AdConnector.Service.Sincronizacion;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((contexto, configuracion) =>
    configuracion.ReadFrom.Configuration(contexto.Configuration));

builder.Services
    .AddOptions<OpcionesAdConnector>()
    .Bind(builder.Configuration.GetSection(OpcionesAdConnector.Seccion));

builder.Services.AddSingleton<EstadoMotor>();
builder.Services.AddSingleton<ILectorDirectorioActivo, LectorDirectorioActivo>();
builder.Services.AddSingleton<IRepositorioPersonas, RepositorioPersonas>();
builder.Services.AddSingleton<MotorSincronizacion>();
builder.Services.AddHostedService<ServicioProgramado>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapearApi();

app.Run();
