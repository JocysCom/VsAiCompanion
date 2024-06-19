-- When dealing with Azure SQL Database and Azure Active Directory (AAD) identities
-- create Azure AD users using the CREATE USER/LOGIN ... FROM EXTERNAL PROVIDER syntax.

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = N'AI_RiskLevel_Low')
    DROP USER [AI_RiskLevel_Low];
CREATE USER [AI_RiskLevel_Low] FROM EXTERNAL PROVIDER;

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = N'AI_RiskLevel_Medium')
    DROP USER [AI_RiskLevel_Medium];
CREATE USER [AI_RiskLevel_Medium] FROM EXTERNAL PROVIDER;

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = N'AI_RiskLevel_High')
    DROP USER [AI_RiskLevel_High];
CREATE USER [AI_RiskLevel_High] FROM EXTERNAL PROVIDER;

IF EXISTS (SELECT * FROM sys.database_principals WHERE name = N'AI_RiskLevel_Critical')
    DROP USER [AI_RiskLevel_Critical];
CREATE USER [AI_RiskLevel_Critical] FROM EXTERNAL PROVIDER;


IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm JOIN sys.database_principals p ON rm.member_principal_id = p.principal_id WHERE p.name = N'AI_RiskLevel_Low' AND rm.role_principal_id = (SELECT principal_id FROM sys.database_principals WHERE name = N'db_datareader'))
    ALTER ROLE [db_datareader] ADD MEMBER [AI_RiskLevel_Low];


IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm JOIN sys.database_principals p ON rm.member_principal_id = p.principal_id WHERE p.name = N'AI_RiskLevel_Medium' AND rm.role_principal_id = (SELECT principal_id FROM sys.database_principals WHERE name = N'db_datawriter'))
    ALTER ROLE [db_datawriter] ADD MEMBER [AI_RiskLevel_Medium];

    IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm JOIN sys.database_principals p ON rm.member_principal_id = p.principal_id WHERE p.name = N'AI_RiskLevel_High' AND rm.role_principal_id = (SELECT principal_id FROM sys.database_principals WHERE name = N'db_datawriter'))
    ALTER ROLE [db_datawriter] ADD MEMBER [AI_RiskLevel_High];


IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm JOIN sys.database_principals p ON rm.member_principal_id = p.principal_id WHERE p.name = N'AI_RiskLevel_Critical' AND rm.role_principal_id = (SELECT principal_id FROM sys.database_principals WHERE name = N'db_owner'))
    ALTER ROLE [db_owner] ADD MEMBER [AI_RiskLevel_Critical];
