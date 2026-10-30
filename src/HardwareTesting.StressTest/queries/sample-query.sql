-- Placeholder workload query. Replace with real statements captured from AdvantageWorkload*.xel
-- (see ../../CaptureWorkload.sql) once a trace of real POS/report/end-of-day activity is available.
SELECT TOP 100 *
FROM dbo.InvSnapShot
ORDER BY InvSnapShotID DESC;
