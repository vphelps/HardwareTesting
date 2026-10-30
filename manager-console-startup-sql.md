# Manager Console Startup: Database Queries

Source: `MyApplication.PerformStartup` in `src/main/apps/AdvantageConsole/AdvManager/Properties/Application.cs`.

Status: derived from code only. Not verified against CenterEdge Support Docs. Owner to approve before use as documentation: product or support SME.

Several startup steps use raw SQL. The rest use Entity Framework, and those queries are generated at runtime. Raw SQL is shown as written in code. EF queries are described by their LINQ. Parameters appear as `@Name`.

## Raw SQL

### 1. Database version

`UpgradeCheckProcessor.GetServerVersion`. Release builds only.

```sql
SELECT TOP 1 Version FROM VersionInfo ORDER BY UpgradeDate DESC, Version DESC
```

### 2. Application info

`AdvCommonCtrls.GetApplicationInfo`.

```sql
SELECT LocNo, LocName, Address1, Address2, City, State, ZipCode, PhoneNumber, FaxNumber, EnableTimeClock,
       TimeClockServer, TimeClockManager, TimeClockClockInOut, EnableCreditCards, CreditProcessor,
       CreditMerchant, CreditDataPath, CreditWaitSeconds, CreditTID, CreditMethod, CreditServerName, CreditServerPort,
       EnablePlayerCards, EmpPinRequired, RequireDecimalPoint, AllowDrawerOverride, ShowComputed, LicenseServer,
       NumDecPlaces, PrivateDebit, CreditOffLine, DeferValuePlayerCards, AskNewPlayerCard, PrintCashOutReceipt,
       AllowCheckOverPayment, IdlePinPadMessage, CashBackAmount, RequireCheckNumber,
       PrintClockInReceipt, PrintClockOutReceipt, CreditModemPort, CreditModemInitString, EnableEmployeeScheduling,
       RePrintReceiptNum, GetEmpNoForDiscount, CreditForceCVV2, CreditForcePostal, EnableDebit, ManualDial, PrintCreditFirst,
       AskCreditLastFourDigits, IdlePoleMsg_1, IdlePoleMsg_2, ShiftDateChangeTime, TicketGroupNum, TicketDelayMSecs,
       ManualCardDefaultInvNo, PrintRemoveTillReceipt, EnableAssetManagement, EnableWeb,
       WarnOffline, EnableNotifications, EnableInventory, EnableConsignment, EnableCustomers, ForceManualCVV2,
       CardNumber_Offset, CardNumber_Length, EnableWebmovies, PrintSalesItems, EnableWebGroups,
       EnableWebAreaTickets, EnableCorpModule, Country, EnableStoreAndForward, MaxStoredTransactions, PurchaseLimitStoredTransactions
FROM ApplicationInfo WHERE AppID=1
```

### 3. Version and last-used write

`AuditData.SaveAppVersionLastAccess`. This is the only direct write found during startup. The `MCVersion` pass runs first.

```sql
IF EXISTS (SELECT 1 FROM StationOptions WHERE OptionName = @AppString AND KeyNo = @StationNo)
    UPDATE StationOptions SET OptionValue = @AppVersion WHERE OptionName = @AppString AND KeyNo = @StationNo
ELSE
    INSERT INTO StationOptions (KeyNo, OptionName, OptionValue, TypeName)
    VALUES (@StationNo, @AppString, @AppVersion, 'System.String')
```

- `@AppString` = `MCVersion`
- `@AppVersion` = assembly version

The last-used pass uses the same pattern with `@AppString` = `MC_LastUsed`. `@LastUsed` is the UTC timestamp, and `TypeName` is `System.DateTime`.

### 4. Options tables

`OptionsTable.LoadAsync`. The table and key vary.

```sql
SELECT OptionName, OptionValue, TypeName FROM [{TableName}] WHERE KeyNo = @KeyNo ORDER BY OptionName
```

| Caller | TableName | KeyNo |
|---|---|---|
| `GetStationInfo` → `LoadSpecialOptions` | `StationOptions` | station number |
| `GetStationInfo` → `LoadSpecialOptions` | `DivisionOptions` | station's division number |
| `DivisionOptions` constructor (division number above 0) | `DivisionOptions` | `0` (base options) |
| `CustomerOptions.Load()` | `CustOptions` | `1` |
| `WaiverOptions.Load()` | `WaiverOptions` | `1` |

The base-options load is inferred from the constructor, not confirmed. `OptionsTable.Load` was not read through to the end.

### 5. Inventory master items

`InvMasterItem.LoadAll`, `src/main/libraries/AdvantageInventory/AdvantageInventory/InvMasterItem.vb` line 441. Column list abbreviated.

```sql
SELECT m.Description, m.CatNo, m.SubCatNo, ... m.DynamicPricingEnabled   -- ~100 columns from m, iad, ib, bgt, ipc
FROM InvMaster AS m
LEFT JOIN InvActivityDates As iad ON m.MasterInvNo = iad.MasterInvNo
LEFT JOIN InvBowling ib ON m.MasterInvNo = ib.MasterInvNo
LEFT JOIN BowlingGameTypes bgt ON bgt.GameType = ib.GameType
LEFT OUTER JOIN InvPlayerCard As ipc ON m.MasterInvNo = ipc.MasterInvNo
ORDER BY m.MasterInvNo
```

A `GetAllInventoryMasterItem` query follows. It loads detail items, barcodes, vendors, package items, units, pricing tiers, modifiers, passes, images, player card data, and bundle choice sets. Those queries were not read.

## Entity Framework (SQL generated at runtime)

| Step | LINQ |
|---|---|
| Station (`GetStationInfo`) | `dc.Stations.AsNoTracking().FirstOrDefault(p => p.StationNo == stationNo)` (`CommonDataContext`) |
| Division (`LoadDivisionInfo`) | `dc.Divisions.AsNoTracking().FirstOrDefault(p => p.DivNo == DivNo)` (`CoreDataContext`) |
| Restaurant defaults (`RestaurantDefaults.Load`) | `context.RestaurantDefaults.FirstOrDefault()` |

`RestaurantDefaults.Load` also writes. If no row exists, it calls `Save()` and inserts a default row.

## No SQL

- `AuditLog.DefaultAppNo` and `AdvantageLocks.DefaultApplication` are name-to-enum mappings in code.
- The patch check calls the API server, not SQL.

## Not traced

- `AdvSetups` constructor and `ServerTimeValidator`.
- EF-generated SQL. To capture exact statements, run a SQL Profiler or Extended Events trace while launching the Console.
