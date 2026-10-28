# Copilot Instructions

## Project Guidelines
- User's live/hosted database connection string (MonsterASP): Server=db64915.databaseasp.net;Database=db64915;User Id=db64915;Password=H_z4@b2D7-Nr;Encrypt=False;MultipleActiveResultSets=True; stored in appsettings.Production.json
- When SQL queries against the hosted MonsterASP database time out even with increased commandTimeout, the root cause is usually non-sargable filters (e.g. LEN(column) = N) and missing indexes. Fix by adding persisted computed columns and indexes, and batching multiple queries via QueryMultipleAsync.

## Reporting UI Guidelines
- Excel/PDF reports should fit to one page width.
- Avoid green color accents; use blue instead in the FinanceDashboard reporting UI.