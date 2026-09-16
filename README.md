# AD Connector

Motor residente que lee Active Directory y completa el correo electronico en la tabla de
personas de SQL Server, cruzando el **documento de identidad (DNI)** contra el campo **fax**
del directorio.

- **Backend**: servicio .NET 8 alojado en IIS, con un `BackgroundService` que se ejecuta en intervalos configurables.
- **Frontend**: tablero de monitoreo en `/` (estado del motor, contadores, historial, ejecucion manual).

## Como funciona

1. Consulta LDAP paginada a Active Directory (`displayName`, `facsimileTelephoneNumber`, `mail`).
2. Normaliza el fax a solo digitos y valida la longitud del documento (8 por defecto).
   Descarta registros sin correo, sin documento o con documento invalido.
3. Carga la instantanea en `ads.ADC_DIRECTORIO` via `SqlBulkCopy`.
4. Ejecuta `ads.USP_ADC_ACTUALIZA_CORREO`, que cruza por documento y actualiza el correo.
5. Registra el resultado en `ads.ADC_SINCRONIZACION_LOG` y el detalle en `ads.ADC_ACTUALIZACION_AUD`.

Un documento que aparece en AD con **mas de un correo distinto** se marca como *ambiguo* y no se toca.

## Despliegue

### 1. Base de datos

Ejecutar en orden sobre la base de datos destino:

```
db/01_objetos.sql
db/02_log_errores.sql
db/03_usp_adc_actualiza_correo.sql
db/04_permisos.sql
```

Antes de ejecutar `03` y `04`, reemplazar los marcadores indicados en la cabecera de cada script:
`dbo.PER_PERSONA`, la columna de documento, la columna de correo y la cuenta de servicio.

### 2. Servidor IIS

Requisitos: Windows Server con IIS y el **ASP.NET Core 8.0 Hosting Bundle** instalado.

1. Descargar el artefacto `AdConnector-IIS-win-x64` desde la pestana **Actions** de este
   repositorio (lo genera GitHub Actions en cada push) y descomprimirlo, por ejemplo en
   `C:\inetpub\AdConnector`.
2. Crear un grupo de aplicaciones dedicado:
   - **.NET CLR version**: *No Managed Code*
   - **Identity**: la cuenta de dominio de servicio (necesita lectura en AD y permisos del script `04`)
   - **Start Mode**: `AlwaysRunning`
   - **Idle Time-out (minutes)**: `0`
   - **Recycling → Regular time interval (minutes)**: `0`
3. Crear el sitio o aplicacion apuntando a esa carpeta y a ese grupo de aplicaciones.
4. Instalar la caracteristica **Application Initialization** de IIS y activar
   `Preload Enabled = True` en el sitio, para que el motor arranque sin esperar la primera visita.
5. Autenticacion: habilitar **Windows Authentication** y deshabilitar la anonima
   (el tablero no tiene control de acceso propio).

> Los tres ajustes del paso 2 son lo que hace que el motor corra de forma permanente.
> Sin ellos IIS apaga el proceso por inactividad y la sincronizacion se detiene.

### 3. Configuracion

Editar `appsettings.json` (o mejor, crear `appsettings.Production.json`, que esta excluido de git):

| Clave | Descripcion |
|---|---|
| `ActiveDirectory:Servidor` / `BaseDn` | Controlador de dominio y rama de busqueda |
| `ActiveDirectory:UsarCredencialesDelProceso` | `true` = usa la identidad del grupo de aplicaciones (recomendado, no guarda contrasenas) |
| `BaseDatos:CadenaConexion` | Preferir `Integrated Security=True` |
| `Sincronizacion:IntervaloMinutos` | Frecuencia del ciclo |
| `Sincronizacion:ModoSimulacion` | **Arranca en `true`**: lee y calcula, pero no escribe |
| `Sincronizacion:SoloCorreosVacios` | `true` = solo completa correos ausentes |

**La primera puesta en marcha debe hacerse con `ModoSimulacion: true`.** Recien cuando los
contadores del tablero cuadren con lo esperado, cambiar a `false` y reiniciar el sitio.

## API

| Metodo | Ruta | Descripcion |
|---|---|---|
| GET | `/api/estado` | Estado del motor y ultima ejecucion |
| GET | `/api/historial?top=20` | Historial de ejecuciones |
| GET | `/api/diagnostico` | Prueba conectividad con AD y SQL Server |
| POST | `/api/ejecutar` | Dispara una sincronizacion manual |
| GET | `/health` | Sonda para monitoreo de infraestructura |

## Desarrollo

```bash
dotnet restore AdConnector.sln
dotnet build AdConnector.sln -c Release
dotnet run --project src/AdConnector.Service
```

Los logs quedan en `logs/adconnector-<fecha>.log` (rotacion diaria, 30 dias).
