/*******************************************************************
NOMBRE DEL OBJETO : ads (esquema) + objetos base de AD Connector
OBJETIVO          : Crear el esquema y las tablas de apoyo del conector de Active Directory:
                    instantanea del directorio, log de ejecuciones y auditoria de correos actualizados.
LLAMADO POR       : Servicio AD Connector (aplicacion .NET alojada en IIS).
REVISIONES:
Version  Fecha       Autor            Descripcion
-------  ----------  ---------------  -------------------------------------
1.0      2026-09-16  Eduardo Strater  Creacion de los objetos base.
*******************************************************************/

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'ads')
    EXEC (N'CREATE SCHEMA ads AUTHORIZATION dbo;');
GO

/*------------------------------------------------------------------
  Instantanea de lo leido de Active Directory en cada ejecucion.
  Se conserva unos dias para poder auditar de donde salio cada correo.
------------------------------------------------------------------*/
IF OBJECT_ID(N'ads.ADC_DIRECTORIO', N'U') IS NULL
BEGIN
    CREATE TABLE ads.ADC_DIRECTORIO
    (
        NDIRECTORIO_ID    BIGINT IDENTITY(1,1) NOT NULL,
        GEJECUCION_ID     UNIQUEIDENTIFIER NOT NULL,
        VCUENTA_CODIGO    NVARCHAR(256) NULL,
        VNOMBRE_COMPLETO  NVARCHAR(256) NULL,
        -- AJUSTAR: el tipo y longitud deben coincidir con la columna documento de la tabla de personas,
        -- de lo contrario el JOIN genera conversion implicita y pierde el indice.
        CDOCUMENTO        VARCHAR(20) NOT NULL,
        VCORREO           NVARCHAR(256) NOT NULL,
        DFECHA_CREACION   DATETIME2 NOT NULL
            CONSTRAINT DF_ADC_DIRECTORIO_FECHA DEFAULT SYSDATETIME(),
        CONSTRAINT PK_ADC_DIRECTORIO PRIMARY KEY (NDIRECTORIO_ID)
    );

    CREATE NONCLUSTERED INDEX IX_NCL_ADC_DIRECTORIO_01
        ON ads.ADC_DIRECTORIO (GEJECUCION_ID, CDOCUMENTO)
        INCLUDE (VCORREO);
END;
GO

/*------------------------------------------------------------------
  Control de procesos: una fila por ejecucion del motor.
------------------------------------------------------------------*/
IF OBJECT_ID(N'ads.ADC_SINCRONIZACION_LOG', N'U') IS NULL
BEGIN
    CREATE TABLE ads.ADC_SINCRONIZACION_LOG
    (
        NSINCRONIZACION_ID           BIGINT IDENTITY(1,1) NOT NULL,
        GEJECUCION_ID                UNIQUEIDENTIFIER NOT NULL,
        DFECHA_INICIO                DATETIME2 NOT NULL,
        DFECHA_FIN                   DATETIME2 NULL,
        CESTADO                      VARCHAR(20) NOT NULL,
        BSIMULACION                  BIT NOT NULL,
        NREGISTROS_LEIDOS            INT NOT NULL
            CONSTRAINT DF_ADC_SINCRONIZACION_LOG_LEIDOS DEFAULT (0),
        NREGISTROS_VALIDOS           INT NOT NULL
            CONSTRAINT DF_ADC_SINCRONIZACION_LOG_VALIDOS DEFAULT (0),
        NREGISTROS_ACTUALIZADOS      INT NOT NULL
            CONSTRAINT DF_ADC_SINCRONIZACION_LOG_ACTUALIZADOS DEFAULT (0),
        NREGISTROS_SIN_COINCIDENCIA  INT NOT NULL
            CONSTRAINT DF_ADC_SINCRONIZACION_LOG_SINCOINC DEFAULT (0),
        NREGISTROS_AMBIGUOS          INT NOT NULL
            CONSTRAINT DF_ADC_SINCRONIZACION_LOG_AMBIGUOS DEFAULT (0),
        NREGISTROS_CON_CORREO        INT NOT NULL
            CONSTRAINT DF_ADC_SINCRONIZACION_LOG_CONCORREO DEFAULT (0),
        VMENSAJE                     NVARCHAR(4000) NULL,
        CUSUARIO_CREACION            VARCHAR(50) NOT NULL,
        DFECHA_CREACION              DATETIME2 NOT NULL
            CONSTRAINT DF_ADC_SINCRONIZACION_LOG_FECHA DEFAULT SYSDATETIME(),
        CONSTRAINT PK_ADC_SINCRONIZACION_LOG PRIMARY KEY (NSINCRONIZACION_ID)
    );

    CREATE NONCLUSTERED INDEX IX_NCL_ADC_SINCRONIZACION_LOG_01
        ON ads.ADC_SINCRONIZACION_LOG (DFECHA_INICIO DESC);
END;
GO

/*------------------------------------------------------------------
  Auditoria fila a fila: que correo tenia la persona antes y cual quedo.
  Es la evidencia para revertir una corrida si algo sale mal.
------------------------------------------------------------------*/
IF OBJECT_ID(N'ads.ADC_ACTUALIZACION_AUD', N'U') IS NULL
BEGIN
    CREATE TABLE ads.ADC_ACTUALIZACION_AUD
    (
        NACTUALIZACION_ID  BIGINT IDENTITY(1,1) NOT NULL,
        GEJECUCION_ID      UNIQUEIDENTIFIER NOT NULL,
        CDOCUMENTO         VARCHAR(20) NOT NULL,
        VCORREO_ANTERIOR   NVARCHAR(256) NULL,
        VCORREO_NUEVO      NVARCHAR(256) NOT NULL,
        CUSUARIO_CREACION  VARCHAR(50) NOT NULL,
        DFECHA_CREACION    DATETIME2 NOT NULL
            CONSTRAINT DF_ADC_ACTUALIZACION_AUD_FECHA DEFAULT SYSDATETIME(),
        CONSTRAINT PK_ADC_ACTUALIZACION_AUD PRIMARY KEY (NACTUALIZACION_ID)
    );

    CREATE NONCLUSTERED INDEX IX_NCL_ADC_ACTUALIZACION_AUD_01
        ON ads.ADC_ACTUALIZACION_AUD (GEJECUCION_ID);
END;
GO

/*------------------------------------------------------------------
  Vista de consulta rapida del ultimo estado (para reportes o Power BI).
------------------------------------------------------------------*/
CREATE OR ALTER VIEW ads.VW_ADC_SINCRONIZACION_RESUMEN
AS
SELECT TOP (100)
       L.GEJECUCION_ID,
       L.DFECHA_INICIO,
       L.DFECHA_FIN,
       L.CESTADO,
       L.BSIMULACION,
       L.NREGISTROS_LEIDOS,
       L.NREGISTROS_VALIDOS,
       L.NREGISTROS_ACTUALIZADOS,
       L.NREGISTROS_SIN_COINCIDENCIA,
       L.NREGISTROS_AMBIGUOS,
       L.NREGISTROS_CON_CORREO,
       L.VMENSAJE
FROM ads.ADC_SINCRONIZACION_LOG AS L
ORDER BY L.DFECHA_INICIO DESC;
GO
