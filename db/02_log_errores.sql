/*******************************************************************
NOMBRE DEL OBJETO : ads.TBL_APPERROR_LOG / ads.USP_APPERROR_LOG
OBJETIVO          : Registro centralizado de errores de los procedimientos del conector.
                    Si CMH ya cuenta con su mecanismo corporativo de logging, este script
                    puede omitirse y el procedimiento 03 debe apuntar a ese objeto.
LLAMADO POR       : ads.USP_ADC_ACTUALIZA_CORREO.
REVISIONES:
Version  Fecha       Autor            Descripcion
-------  ----------  ---------------  -------------------------------------
1.0      2026-09-16  Eduardo Strater  Creacion del mecanismo de log de errores.
*******************************************************************/

SET NOCOUNT ON;
GO

IF OBJECT_ID(N'ads.TBL_APPERROR_LOG', N'U') IS NULL
BEGIN
    CREATE TABLE ads.TBL_APPERROR_LOG
    (
        NAPPERROR_ID       BIGINT IDENTITY(1,1) NOT NULL,
        DFECHA_CREACION    DATETIME2 NOT NULL
            CONSTRAINT DF_TBL_APPERROR_LOG_FECHA DEFAULT SYSDATETIME(),
        VBASE_DATOS        NVARCHAR(128) NOT NULL
            CONSTRAINT DF_TBL_APPERROR_LOG_BD DEFAULT DB_NAME(),
        VMODULO            NVARCHAR(128) NULL,
        VOPCION            NVARCHAR(128) NULL,
        VNIVEL             VARCHAR(20) NULL,
        NNUMERO_ERROR      INT NULL,
        VMENSAJE_ERROR     NVARCHAR(4000) NULL,
        CUSUARIO_CREACION  VARCHAR(50) NULL,
        CONSTRAINT PK_TBL_APPERROR_LOG PRIMARY KEY (NAPPERROR_ID)
    );
END;
GO

CREATE OR ALTER PROCEDURE ads.USP_APPERROR_LOG
(
    @pi_Modulo       NVARCHAR(128),
    @pi_Opcion       NVARCHAR(128),
    @pi_Nivel        VARCHAR(20),
    @pi_NumeroError  INT,
    @pi_MensajeError NVARCHAR(4000),
    @pi_Usuario      VARCHAR(50) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO ads.TBL_APPERROR_LOG
    (
        VMODULO, VOPCION, VNIVEL, NNUMERO_ERROR, VMENSAJE_ERROR, CUSUARIO_CREACION
    )
    VALUES
    (
        @pi_Modulo, @pi_Opcion, @pi_Nivel, @pi_NumeroError, @pi_MensajeError, @pi_Usuario
    );
END;
GO
