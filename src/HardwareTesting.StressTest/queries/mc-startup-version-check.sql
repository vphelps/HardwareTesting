-- From Manager Console startup (UpgradeCheckProcessor.GetServerVersion), release builds only.
-- Source: manager-console-startup-sql.md
SELECT TOP 1 Version FROM VersionInfo ORDER BY UpgradeDate DESC, Version DESC;
