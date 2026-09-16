using AdConnector.Service.Api;
using AdConnector.Core.Extensiones;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((contexto, configuracion) =>
    configuracion.ReadFrom.Configuration(contexto.Configuration));

// El motor vive en la biblioteca AdConnector.Core; este proyecto solo lo aloja y lo expone.
builder.Services
    .AgregarAdConnector(builder.Configuration)
    .AgregarAdConnectorProgramado();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapearApi();

app.Run();
