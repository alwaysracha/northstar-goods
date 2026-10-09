/*
    app_login.sql
    Least-privilege access for the web application and for reporting. Re-applied on every
    deploy; it also resets the application login's password to the configured value.
    sqlcmd variables: $(DatabaseName), $(AppLoginName), $(AppLoginPassword)

    northstar_app    -> app_role: read/write data in the operational schemas, read reference
                        data and views, EXECUTE procedures. No DDL, no access to the deployment journal.
    reporting_reader -> role only (no login is created): SELECT on reporting views.
*/
SET NOCOUNT ON;
GO
USE [master];
GO
IF SUSER_ID(N'$(AppLoginName)') IS NULL
    CREATE LOGIN [$(AppLoginName)] WITH PASSWORD = N'$(AppLoginPassword)', DEFAULT_DATABASE = [$(DatabaseName)], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
ELSE
    ALTER LOGIN [$(AppLoginName)] WITH PASSWORD = N'$(AppLoginPassword)';
GO
USE [$(DatabaseName)];
GO
IF USER_ID(N'$(AppLoginName)') IS NULL
    CREATE USER [$(AppLoginName)] FOR LOGIN [$(AppLoginName)] WITH DEFAULT_SCHEMA = sales;
GO
IF DATABASE_PRINCIPAL_ID(N'app_role') IS NULL CREATE ROLE app_role AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'reporting_reader') IS NULL CREATE ROLE reporting_reader AUTHORIZATION dbo;
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::auth     TO app_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::catalog  TO app_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::customer TO app_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::sales    TO app_role;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::payment  TO app_role;
GRANT SELECT ON SCHEMA::ref       TO app_role;
GRANT SELECT ON SCHEMA::reporting TO app_role;
GRANT EXECUTE ON SCHEMA::sales    TO app_role;
GRANT SELECT ON SCHEMA::reporting TO reporting_reader;
GO
IF IS_ROLEMEMBER(N'app_role', N'$(AppLoginName)') = 0
    ALTER ROLE app_role ADD MEMBER [$(AppLoginName)];
GO
