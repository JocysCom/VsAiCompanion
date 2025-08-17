-- When dealing with Azure SQL Database and Azure Active Directory (AAD) identities
-- create Azure AD users using the CREATE USER/LOGIN ... FROM EXTERNAL PROVIDER syntax.

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'AI_RiskLevel_Low')
    CREATE LOGIN [AI_RiskLevel_Low] FROM EXTERNAL PROVIDER;

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'AI_RiskLevel_Medium')
    CREATE LOGIN [AI_RiskLevel_Medium] FROM EXTERNAL PROVIDER;

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'AI_RiskLevel_High')
    CREATE LOGIN [AI_RiskLevel_High] FROM EXTERNAL PROVIDER;

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'AI_RiskLevel_Critical')
    CREATE LOGIN [AI_RiskLevel_Critical] FROM EXTERNAL PROVIDER;
