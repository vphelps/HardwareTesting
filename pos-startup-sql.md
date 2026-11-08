# POS Startup: Database Queries

Source: `MyApplication.PerformStartup` in `src/main/apps/AdvantagePos/Pos/My Project/Application.vb`, plus the shared libraries it calls.

Status: derived from code only. Not verified against CenterEdge Support Docs, which have no document describing POS startup. Owner to approve before use as documentation: product or support SME.

Raw SQL is shown as written in code. Entity Framework (EF) queries are described by their LINQ because the SQL is generated at runtime. Parameters appear as `@Name`. Steps marked "release only" are compiled out of Debug builds.

## Raw SQL

### 1. Database version

`UpgradeCheckProcessor.GetServerVersion`. Release only. The same query the Manager Console runs.

```sql
SELECT TOP 1 Version FROM VersionInfo ORDER BY UpgradeDate DESC, Version DESC
```

### 2. Application info

`AdvCommonCtrls.GetApplicationInfo`. Startup aborts if no row is returned.

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

`AuditData.SaveAppVersionLastAccess`. Direct write at startup.

```sql
IF EXISTS (SELECT 1 FROM StationOptions WHERE OptionName = @AppString AND KeyNo = @StationNo)
    UPDATE StationOptions SET OptionValue = @AppVersion WHERE OptionName = @AppString AND KeyNo = @StationNo
ELSE
    INSERT INTO StationOptions (KeyNo, OptionName, OptionValue, TypeName)
    VALUES (@StationNo, @AppString, @AppVersion, 'System.String')
```

- `@AppString` = `POSVersion`
- `@AppVersion` = assembly version

The last-used pass uses the same pattern with `@AppString` = `POS_LastUsed`. `@LastUsed` is the UTC timestamp, and `TypeName` is `System.DateTime`.

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
| `AppOptions.Load()` | Not read. Table name and key were not traced. | Not read |

The base-options load is inferred from the constructor, not confirmed.

### 5. Station options written at startup

`HardwareInfoProvider.GetHardwareInfo()` supplies the values. `StationInfo.Options.Save()` persists them.

| OptionName (`StationOptions`) | Source |
|---|---|
| `ComputerName` | `Dns.GetHostName` |
| `ComputerIPAddress` | First IPv4 address from `Dns.GetHostEntry` |

The SQL emitted by `OptionsTable.Save` was not read. Expect an update-or-insert per changed option, as in step 3.

### 6. Station options read for startup behavior

These come from the `StationOptions` load in step 4. No separate query runs.

| OptionName | Startup use |
|---|---|
| `SecurityServerName`, `SecurityServerPort` | A security feed connection is created when the name is set. |
| `MacAddress`, `IntercardMacAddress` | Read only if a consumer uses them. Not traced at startup. |

### 7. Hardware-adjacent queries

`CommonRoutines.CheckForNewInventory` and the license check read the database before touching hardware.

License check (release only), `LicenseHandler.VerifyWithServerAsync`:

```sql
SELECT LocName, Address1, Address2, City, State, ZipCode, Country, PhoneNumber, LicenseServer FROM ApplicationInfo WHERE AppID = 1
```

License server lookup, `LicenseHandler.GetLicenseFromServer`:

```sql
SELECT LicenseServer FROM ApplicationInfo WHERE AppID = 1
```

The results feed a TCP request to the license server on port 15050. See "Non-database communication" below.

### 8. Reader defaults

`Load_ReaderDefaults` in `DatabaseGeneralRoutines.vb`.

```sql
SELECT * FROM ReaderDefaults WHERE KeyID=1
```

### 9. Group defaults and till codes

`Application.vb`, direct SQL block.

```sql
SELECT Description FROM TillCodes WHERE TillCode = 9
SELECT AllowPosAdd, AllowPosPay, DepositInvNo FROM GroupDefaults WHERE KeyNo=1
```

`LoadRefundPlayerInvNo` in `DatabaseGeneralRoutines.vb`:

```sql
SELECT RefundPlayerInvNo FROM GroupDefaults WHERE KeyNo = 1
```

### 10. POS screens

`LoadScreenInfo` in `DatabaseKeyRoutines.vb`. The query falls back to `StationNo=0`, then to `DivNo=0`, when the first returns no rows.

```sql
SELECT ScreenID, ScreenName, ScreenEnable, ModuleID
FROM PosScreens
WHERE StationNo=@StationNo AND DivNo=@DivNo AND ScreenEnable=1
```

`PosScreenModules`, `PosKeyGroups`, `PosKeys`, and `PosTender` are also queried by the same file. Their SQL was not extracted.

### 11. POS keypad

`LoadPosKeyPad` in `DatabaseKeyRoutines.vb`. Three scopes are combined with OR: global, division, and station within division.

```sql
SELECT * FROM PosKeyPad
WHERE (StationNo=0 AND DivNo=0)
   OR (StationNo=0 AND DivNo=@DivNo)
   OR (StationNo=@StationNo AND DivNo=@DivNo)
```

## Inventory load

`CheckForNewInventory(False)` calls `SharedData.CheckForNewInventory` (`src/main/libraries/AdvantageGroupData/AdvGroupData/Classes/SharedData.vb` line 206). When the stored inventory change date is newer than the last load time, it constructs `Inventory` (`src/main/libraries/AdvantageInventory/AdvantageInventory/Inventory.vb` line 220 onward).

- The constructor calls `LoadDataAsync` synchronously.
- It loads about 16 lookup sets in parallel: tax codes, categories, classes, GL numbers, modifiers, stock rooms, units, vendors, web groups, matrices, and others.
- `loadAll` is false at startup, so items are not bulk loaded.
- The individual lookup SQL statements were not extracted.

The Manager Console runs `InvMasterItem.LoadAll`. See `manager-console-startup-sql.md`, section 5.

## Entity Framework (SQL generated at runtime)

| Step | Context | LINQ or purpose |
|---|---|---|
| Station (`GetStationInfo`, `GetStation`) | `CommonDataContext` | `dc.Stations.AsNoTracking().FirstOrDefault(p => p.StationNo == stationNo)` |
| Division (`LoadDivisionInfo`, `frmPos_Load`) | `CoreDataContext` | `dc.Divisions.AsNoTracking().FirstOrDefault(p => p.DivNo == DivNo)` |
| Restaurant defaults (`RestaurantDefaults.Load`) | `RestaurantDbContext` | `context.RestaurantDefaults.FirstOrDefault()` |

`RestaurantDefaults.Load` also writes. If no row exists, it calls `Save()` and inserts a default row.

## Non-database communication

Included so the database steps can be sequenced against the rest of startup.

| Step | Target | Detail |
|---|---|---|
| Version check (release only) | API server | Reads the server version and the patch list through `IVersion` and `IUpdates.GetPatchesAsync`. |
| License check (release only) | License server, TCP 15050 | `LicenseCheck` XML request and response. 15-second timeouts. The request carries the MAC address, module name `POS`, station number, and location details. |
| Server time check | API server, `ITime.GetTimeAsync` | 2-second timeout. Warns on UTC offset difference or drift over 1 minute. |
| API base address | `http(s)://{host}:15059` | Host is the `CoreServiceServerName` app option, then `LicenseServer` from `ApplicationInfo`, then `localhost`. JWT bearer from the `AuthenticationKey` app option. |
| Telemetry | OTLP exporter | Endpoint, protocol, and headers come from app options stored in the database. |
| Security feed | `SecurityServerName`:`SecurityServerPort` | Only when configured. Protocol not read. |
| Pole display, PIN pad | Local serial ports | No network or database traffic. |
| Credit initialization | Mercury PDC-X initializer | Runs as a background task. Behavior not read. |

## No SQL

- `/training` swaps the connection catalog in code. It does not issue a query.
- `RunAsSingleInstance` is a local process check.
- `SecurityFeed`, pole display, and PIN pad setup read values already loaded in step 4.

## Not traced

- `AppOptions.Load`, `DiscountHandler`, `TicketsHandler`, `RentalControlHandler`, `RewardHandler`, `PassHandler`, `AreaOptions`, `ConsignDefaults`, `EventType.LoadAll`, and `Defaults.Load_Defaults` constructors and loaders.
- `OptionsTable.Save` SQL.
- `CreditInitialization.Initialize`.
- The rest of `frmPos_Load` (`src/main/apps/AdvantagePos/Pos/Forms/FormPos.vb` line 519 onward): donation item, movie panel, `LoadPosTender`, `GetStationTill`, and the signage license check.
- SignalR hub and feature flag provider connections.
- EF-generated SQL. To capture exact statements, run a SQL Profiler or Extended Events trace while launching the POS against a test database.

## Load testing notes

- Every POS start writes `StationOptions` rows (`POSVersion`, `POS_LastUsed`, `ComputerName`, `ComputerIPAddress`). Use a disposable database.
- Simulated stations need real, enabled station records. A missing station sends the POS to the Change Station dialog.
- The license service counts stations by MAC address. Do not point simulated stations at a customer license service.
- Startup is mostly sequential, short connections. The inventory lookup load is the parallel burst.
