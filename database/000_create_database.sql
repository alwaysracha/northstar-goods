/*
    000_create_database.sql
    Runs against [master] on every deploy and is safe to re-run.
    sqlcmd variables: $(DatabaseName)
*/
SET NOCOUNT ON;
GO
IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    CREATE DATABASE [$(DatabaseName)] COLLATE Latin1_General_100_CI_AS;
END
GO
-- SIMPLE recovery suits a development server with no log backups. A production
-- server would use FULL recovery with scheduled log backups for point-in-time restore.
IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'$(DatabaseName)') <> N'SIMPLE'
    ALTER DATABASE [$(DatabaseName)] SET RECOVERY SIMPLE;
-- Row versioning: readers under READ COMMITTED don't block writers.
-- Checkout still asks for SERIALIZABLE explicitly where it needs it.
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'$(DatabaseName)') = 0
    ALTER DATABASE [$(DatabaseName)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
IF (SELECT snapshot_isolation_state FROM sys.databases WHERE name = N'$(DatabaseName)') = 0
    ALTER DATABASE [$(DatabaseName)] SET ALLOW_SNAPSHOT_ISOLATION ON;
GO
USE [$(DatabaseName)];
GO
-- Deployment journal: one row per migration/seed script that has been applied.
IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL
CREATE TABLE dbo.SchemaVersions
(
    ScriptName nvarchar(260)     NOT NULL CONSTRAINT PK_SchemaVersions PRIMARY KEY,
    AppliedAt  datetimeoffset(0) NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAt DEFAULT SYSDATETIMEOFFSET(),
    AppliedBy  sysname           NOT NULL CONSTRAINT DF_SchemaVersions_AppliedBy DEFAULT SUSER_SNAME()
);
GO
