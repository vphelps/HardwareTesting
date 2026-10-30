-- Extended Events capture of Advantage workload against the [centeredge] database.
-- Run in SSMS as sysadmin. Output goes to the instance's LOG folder
-- (e.g. C:\Program Files\Microsoft SQL Server\MSSQL17.<instance>\MSSQL\Log\AdvantageWorkload*.xel).

IF EXISTS (SELECT 1 FROM sys.server_event_sessions WHERE name = N'AdvantageWorkload')
    DROP EVENT SESSION AdvantageWorkload ON SERVER;
GO

CREATE EVENT SESSION AdvantageWorkload ON SERVER
ADD EVENT sqlserver.rpc_completed (
    ACTION (sqlserver.client_app_name, sqlserver.client_hostname, sqlserver.database_name, sqlserver.username, sqlserver.session_id)
    WHERE sqlserver.database_name = N'centeredge'
      AND sqlserver.client_app_name NOT LIKE N'%Management Studio%'
      AND sqlserver.client_app_name NOT LIKE N'%sqlcmd%'
      AND sqlserver.client_app_name NOT LIKE N'%Profiler%'),
ADD EVENT sqlserver.sql_batch_completed (
    ACTION (sqlserver.client_app_name, sqlserver.client_hostname, sqlserver.database_name, sqlserver.username, sqlserver.session_id)
    WHERE sqlserver.database_name = N'centeredge'
      AND sqlserver.client_app_name NOT LIKE N'%Management Studio%'
      AND sqlserver.client_app_name NOT LIKE N'%sqlcmd%'
      AND sqlserver.client_app_name NOT LIKE N'%Profiler%')
ADD TARGET package0.event_file (
    SET filename = N'AdvantageWorkload.xel',
        max_file_size = 256,        -- MB per file
        max_rollover_files = 4)
WITH (MAX_DISPATCH_LATENCY = 5 SECONDS, STARTUP_STATE = OFF);
GO

-- START: run, then use Advantage normally for 15-60 minutes
ALTER EVENT SESSION AdvantageWorkload ON SERVER STATE = START;
GO

-- STOP: run when finished
-- ALTER EVENT SESSION AdvantageWorkload ON SERVER STATE = STOP;
-- DROP EVENT SESSION AdvantageWorkload ON SERVER;
