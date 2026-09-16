/*******************************************************************
NOMBRE DEL OBJETO : ads.USP_ADC_ACTUALIZA_CORREO
OBJETIVO          : Cruzar la instantanea de Active Directory (ads.ADC_DIRECTORIO) contra la
                    tabla de personas usando el documento de identidad, y completar la columna
                    de correo electronico. Registra el resultado en ads.ADC_SINCRONIZACION_LOG
                    y el detalle fila a fila en ads.ADC_ACTUALIZACION_AUD.
LLAMADO POR       : Servicio AD Connector (aplicacion .NET alojada en IIS).
REVISIONES:
Version  Fecha       Autor            Descripcion
-------  ----------  ---------------  -------------------------------------
1.0      2026-09-16  Eduardo Strater  Creacion del procedimiento.
*******************************************************************/

/*==================================================================
  AJUSTAR ANTES DE EJECUTAR — reemplazar en todo el script:

    dbo.PER_PERSONA  -> esquema y nombre reales de la tabla de personas
    P.CDOCUMENTO     -> columna real del documento de identidad (DNI)
    P.VCORREO        -> columna real del correo electronico

  Ademas, el tipo de #CANDIDATO.CDOCUMENTO debe coincidir exactamente con
  el tipo de la columna documento, para no provocar conversion implicita.
==================================================================*/

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE ads.USP_ADC_ACTUALIZA_CORREO
(
    @pi_EjecucionId        UNIQUEIDENTIFIER,
    @pi_Simulacion         BIT = 1,
    @pi_SoloCorreosVacios  BIT = 1,
    @pi_RegistrosLeidos    INT = 0,
    @pi_Usuario            VARCHAR(50) = NULL,
    @po_Actualizados       INT OUTPUT,
    @po_SinCoincidencia    INT OUTPUT,
    @po_Ambiguos           INT OUTPUT,
    @po_YaTenianCorreo     INT OUTPUT,
    @po_Retorno            INT OUTPUT,
    @po_Mensaje            NVARCHAR(4000) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @v_FechaInicio    DATETIME2      = SYSDATETIME();
    DECLARE @v_Validos        INT            = 0;
    DECLARE @v_DiasRetencion  INT            = 7;
    DECLARE @v_NumeroError    INT            = 0;

    SET @po_Actualizados    = 0;
    SET @po_SinCoincidencia = 0;
    SET @po_Ambiguos        = 0;
    SET @po_YaTenianCorreo  = 0;
    SET @po_Retorno         = 0;
    SET @po_Mensaje         = N'Proceso ejecutado correctamente';
    SET @pi_Usuario         = ISNULL(@pi_Usuario, N'ADCONNECTOR');

    BEGIN TRY

        /* Un correo por documento. Si un mismo documento aparece en AD con correos
           distintos no hay forma de elegir sin criterio de negocio: se marca ambiguo
           y se deja intacto. */
        CREATE TABLE #CANDIDATO
        (
            CDOCUMENTO         VARCHAR(20) NOT NULL PRIMARY KEY,
            VCORREO            NVARCHAR(256) NOT NULL,
            NCORREOS_DISTINTOS INT NOT NULL
        );

        INSERT INTO #CANDIDATO (CDOCUMENTO, VCORREO, NCORREOS_DISTINTOS)
        SELECT D.CDOCUMENTO,
               MIN(D.VCORREO),
               COUNT(DISTINCT D.VCORREO)
        FROM ads.ADC_DIRECTORIO AS D
        WHERE D.GEJECUCION_ID = @pi_EjecucionId
        GROUP BY D.CDOCUMENTO;

        SET @v_Validos = @@ROWCOUNT;

        SELECT @po_Ambiguos = COUNT(1)
        FROM #CANDIDATO AS C
        WHERE C.NCORREOS_DISTINTOS > 1;

        IF @v_Validos = 0
        BEGIN
            SET @po_Mensaje = N'No hay registros de Active Directory para la ejecucion indicada.';
        END
        ELSE
        BEGIN
            SELECT @po_SinCoincidencia = COUNT(1)
            FROM #CANDIDATO AS C
            WHERE C.NCORREOS_DISTINTOS = 1
              AND NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.PER_PERSONA AS P
                      WHERE P.CDOCUMENTO = C.CDOCUMENTO
                  );

            SELECT @po_YaTenianCorreo = COUNT(1)
            FROM #CANDIDATO AS C
                 INNER JOIN dbo.PER_PERSONA AS P
                     ON P.CDOCUMENTO = C.CDOCUMENTO
            WHERE C.NCORREOS_DISTINTOS = 1
              AND NULLIF(LTRIM(RTRIM(P.VCORREO)), N'') IS NOT NULL;

            BEGIN TRANSACTION;

            IF @pi_Simulacion = 1
            BEGIN
                /* Modo simulacion: se cuenta exactamente lo que se habria actualizado,
                   sin tocar la tabla de personas. */
                SELECT @po_Actualizados = COUNT(1)
                FROM dbo.PER_PERSONA AS P
                     INNER JOIN #CANDIDATO AS C
                         ON C.CDOCUMENTO = P.CDOCUMENTO
                WHERE C.NCORREOS_DISTINTOS = 1
                  AND (@pi_SoloCorreosVacios = 0 OR NULLIF(LTRIM(RTRIM(P.VCORREO)), N'') IS NULL)
                  AND (P.VCORREO IS NULL OR P.VCORREO <> C.VCORREO);

                SET @po_Mensaje = CONCAT(
                    N'SIMULACION: se habrian actualizado ', @po_Actualizados, N' registro(s).');
            END
            ELSE
            BEGIN
                UPDATE P
                SET P.VCORREO = C.VCORREO
                OUTPUT @pi_EjecucionId,
                       INSERTED.CDOCUMENTO,
                       DELETED.VCORREO,
                       INSERTED.VCORREO,
                       @pi_Usuario
                  INTO ads.ADC_ACTUALIZACION_AUD
                       (GEJECUCION_ID, CDOCUMENTO, VCORREO_ANTERIOR, VCORREO_NUEVO, CUSUARIO_CREACION)
                FROM dbo.PER_PERSONA AS P
                     INNER JOIN #CANDIDATO AS C
                         ON C.CDOCUMENTO = P.CDOCUMENTO
                WHERE C.NCORREOS_DISTINTOS = 1
                  AND (@pi_SoloCorreosVacios = 0 OR NULLIF(LTRIM(RTRIM(P.VCORREO)), N'') IS NULL)
                  AND (P.VCORREO IS NULL OR P.VCORREO <> C.VCORREO);

                SET @po_Actualizados = @@ROWCOUNT;

                SET @po_Mensaje = CONCAT(
                    N'Se actualizaron ', @po_Actualizados, N' registro(s) de correo.');
            END

            COMMIT TRANSACTION;
        END

        INSERT INTO ads.ADC_SINCRONIZACION_LOG
        (
            GEJECUCION_ID, DFECHA_INICIO, DFECHA_FIN, CESTADO, BSIMULACION,
            NREGISTROS_LEIDOS, NREGISTROS_VALIDOS, NREGISTROS_ACTUALIZADOS,
            NREGISTROS_SIN_COINCIDENCIA, NREGISTROS_AMBIGUOS, NREGISTROS_CON_CORREO,
            VMENSAJE, CUSUARIO_CREACION
        )
        VALUES
        (
            @pi_EjecucionId, @v_FechaInicio, SYSDATETIME(), 'OK', @pi_Simulacion,
            @pi_RegistrosLeidos, @v_Validos, @po_Actualizados,
            @po_SinCoincidencia, @po_Ambiguos, @po_YaTenianCorreo,
            @po_Mensaje, @pi_Usuario
        );

        /* Purga de instantaneas antiguas: la evidencia fina vive en ADC_ACTUALIZACION_AUD. */
        DELETE FROM ads.ADC_DIRECTORIO
        WHERE DFECHA_CREACION < DATEADD(DAY, -@v_DiasRetencion, SYSDATETIME());

    END TRY
    BEGIN CATCH

        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        -- Las funciones de error no pueden pasarse como parametro de EXEC: se capturan en variables.
        SET @v_NumeroError = ERROR_NUMBER();
        SET @po_Retorno = -1;
        SET @po_Mensaje =
            CONCAT(
                N'Error ', ERROR_NUMBER(),
                N' - Procedimiento: ', COALESCE(ERROR_PROCEDURE(), OBJECT_NAME(@@PROCID)),
                N' - Linea: ', ERROR_LINE(),
                N' - Mensaje: ', ERROR_MESSAGE()
            );

        /* El log se escribe fuera de la transaccion ya revertida para que sobreviva al ROLLBACK. */
        INSERT INTO ads.ADC_SINCRONIZACION_LOG
        (
            GEJECUCION_ID, DFECHA_INICIO, DFECHA_FIN, CESTADO, BSIMULACION,
            NREGISTROS_LEIDOS, NREGISTROS_VALIDOS, NREGISTROS_ACTUALIZADOS,
            NREGISTROS_SIN_COINCIDENCIA, NREGISTROS_AMBIGUOS, NREGISTROS_CON_CORREO,
            VMENSAJE, CUSUARIO_CREACION
        )
        VALUES
        (
            @pi_EjecucionId, @v_FechaInicio, SYSDATETIME(), 'ERROR', @pi_Simulacion,
            @pi_RegistrosLeidos, @v_Validos, 0, 0, 0, 0,
            @po_Mensaje, @pi_Usuario
        );

        EXEC ads.USP_APPERROR_LOG
            @pi_Modulo       = N'AD_CONNECTOR',
            @pi_Opcion       = N'USP_ADC_ACTUALIZA_CORREO',
            @pi_Nivel        = N'ALTO',
            @pi_NumeroError  = @v_NumeroError,
            @pi_MensajeError = @po_Mensaje,
            @pi_Usuario      = @pi_Usuario;

        THROW;

    END CATCH;
END;
GO
