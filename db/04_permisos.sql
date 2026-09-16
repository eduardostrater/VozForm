/*******************************************************************
NOMBRE DEL OBJETO : Permisos minimos para la cuenta de servicio de AD Connector
OBJETIVO          : Otorgar solo los permisos necesarios a la cuenta de dominio bajo la cual
                    corre el grupo de aplicaciones de IIS. Principio de minimo privilegio:
                    la cuenta NO recibe db_datawriter ni db_owner sobre la base completa.
LLAMADO POR       : Ejecucion manual del DBA durante el despliegue.
REVISIONES:
Version  Fecha       Autor            Descripcion
-------  ----------  ---------------  -------------------------------------
1.0      2026-09-16  Eduardo Strater  Creacion del script de permisos.
*******************************************************************/

/*==================================================================
  AJUSTAR: reemplazar CMH\svc_adconnector por la cuenta de servicio real
  y dbo.PER_PERSONA por la tabla de personas.
==================================================================*/

SET NOCOUNT ON;
GO

DECLARE @v_Cuenta SYSNAME = N'CMH\svc_adconnector';
DECLARE @v_Sql    NVARCHAR(MAX);

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @v_Cuenta)
BEGIN
    SET @v_Sql = N'CREATE USER ' + QUOTENAME(@v_Cuenta) + N' FROM LOGIN ' + QUOTENAME(@v_Cuenta) + N';';
    EXEC sys.sp_executesql @v_Sql;
END;
GO

-- Carga de la instantanea del directorio y lectura del historial.
GRANT INSERT, SELECT, DELETE ON ads.ADC_DIRECTORIO          TO [CMH\svc_adconnector];
GRANT SELECT                  ON ads.ADC_SINCRONIZACION_LOG TO [CMH\svc_adconnector];
GRANT SELECT                  ON ads.VW_ADC_SINCRONIZACION_RESUMEN TO [CMH\svc_adconnector];

-- La escritura sobre la tabla de personas queda encapsulada en el procedimiento:
-- la cuenta no recibe UPDATE directo sobre dbo.PER_PERSONA.
GRANT EXECUTE                 ON ads.USP_ADC_ACTUALIZA_CORREO TO [CMH\svc_adconnector];
GO
