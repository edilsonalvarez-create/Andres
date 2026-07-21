/* ============================================================================
   QA Guardian — Usuario de aplicación de mínimo privilegio (SQL Server)
   Sprint 16-B, Ítem 13: el proceso API en Production/QA/Staging NUNCA debe
   conectarse con `sa`. Ejecute este script una vez, con una cuenta sysadmin
   (sa / DBA), DESPUÉS de crear el esquema EF (01-schema.sql o
   `dotnet ef database update` / servicio compose `migrate`).

   Sprint 20-A (B5): el esquema Hangfire se aplica aparte
   (`05-hangfire-schema.sql`) y los permisos DML Hangfire en
   `00-app-user-hangfire.sql` (tras este script). Orden compose db-init:
   hangfire-schema → app-user → app-user-hangfire.

   Uso (sqlcmd):
     sqlcmd -S <server> -U sa -P "<sa-password>" -C \
       -v AppLogin="qaguardian_app" AppPassword="<contraseña-única>" \
       -i database/00-app-user.sql
   ============================================================================ */

/* AppLogin / AppPassword: NO declarar :setvar — en mssql-tools18 un :setvar
   pisa el -v de línea de comando. Obligatorio:
     sqlcmd -v AppLogin="qaguardian_app" -v AppPassword="..." */

USE master;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(AppLogin)')
    CREATE LOGIN [$(AppLogin)] WITH PASSWORD = N'$(AppPassword)', CHECK_POLICY = ON;
GO

USE QAGuardian;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppLogin)')
    CREATE USER [$(AppLogin)] FOR LOGIN [$(AppLogin)];
GO

/* Mínimo privilegio: solo lectura/escritura de filas. El proceso API NUNCA
   ejecuta DDL (CREATE/ALTER TABLE) — eso lo hace exclusivamente la cuenta de
   migración (sa / DBA) vía `dotnet ef database update`. Por eso NO se otorgan
   db_owner, db_ddladmin, ALTER ANY SCHEMA ni CONTROL SERVER a este login. */
ALTER ROLE db_datareader ADD MEMBER [$(AppLogin)];
ALTER ROLE db_datawriter ADD MEMBER [$(AppLogin)];
GO

PRINT 'Usuario de aplicación de mínimo privilegio listo: ' + '$(AppLogin)';
GO
