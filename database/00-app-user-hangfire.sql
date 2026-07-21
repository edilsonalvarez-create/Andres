/* ============================================================================
   QA Guardian — Permisos Hangfire para el usuario de aplicación (mínimo privilegio)
   Sprint 20-A (B5): el esquema HangFire lo crea `05-hangfire-schema.sql` con sa/DBA.
   El API (PrepareSchemaIfNecessary=false) solo necesita DML sobre ese esquema.
   Hangfire.SqlServer 1.8.x no crea stored procedures propios; usa tablas +
   sp_getapplock/sp_releaseapplock del sistema (ejecutables por public por defecto).

   Ejecutar DESPUÉS de 05-hangfire-schema.sql y 00-app-user.sql.

   Uso (sqlcmd):
     sqlcmd -S <server> -U sa -P "<sa-password>" -C \
       -v AppLogin="qaguardian_app" HangFireSchema="HangFire" \
       -i database/00-app-user-hangfire.sql
   ============================================================================ */

/* AppLogin / HangFireSchema vía sqlcmd -v (sin :setvar: pisa -v en mssql-tools18).
   Ejemplo: -v AppLogin="qaguardian_app" -v HangFireSchema="HangFire" */

USE QAGuardian;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppLogin)')
BEGIN
    RAISERROR('Usuario de aplicación $(AppLogin) no existe. Ejecute 00-app-user.sql primero.', 16, 1);
    RETURN;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'$(HangFireSchema)')
BEGIN
    RAISERROR('Esquema $(HangFireSchema) no existe. Ejecute 05-hangfire-schema.sql primero.', 16, 1);
    RETURN;
END
GO

/* DML explícito sobre HangFire (complementa db_datareader/db_datawriter).
   Sin ALTER/CONTROL/CREATE — el API no debe poder hacer DDL Hangfire. */
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[$(HangFireSchema)] TO [$(AppLogin)];
GO

PRINT 'Permisos Hangfire (DML) otorgados a ' + '$(AppLogin)' + ' sobre esquema [$(HangFireSchema)]';
GO
