# Groups Startup: Database Queries

Sources:
- `MyApplication.PerformStartup` in `src/main/apps/AdvantageGroups/AdvGroups/My Project/Application.vb`
- `frmAdvGroups_Load` in `src/main/apps/AdvantageGroups/AdvGroups/FormAdvGroups.vb` line 62
- `SharedData.LoadAllObjects` in `src/main/libraries/AdvantageGroupData/AdvGroupData/Classes/SharedData.vb` line 292

Status: derived from code only. Not verified against CenterEdge Support Docs, which have no document describing Groups startup. Owner to approve before use as documentation: product or support SME.

Groups shares most of its early startup with the POS. Shared steps point to `pos-startup-sql.md` instead of repeating long queries. Raw SQL is shown as written in code. Entity Framework (EF) queries are described by their LINQ. Parameters appear as `@Name`. Steps marked "release only" are compiled out of Debug builds.

## Startup order

| # | Step | Database or service |
|---|---|---|
| 0 | `RunAsSingleInstance(args)`. A second launch activates the first window and restores it to normal or maximized, based on `GroupsInWindows`. | None |
| 1 | Parse `/training`. Start telemetry (`_telemetry.Start("Groups")`). | OTLP settings come from the database app options. |
| 2 | `AdvCommonCtrls.Initialize("AdvantageGroups", ArgTraining, true)` | Reads the local config and builds the connection string. No query. |
| 3 | Test the database connection with an async open. | SQL connect |
| 4 | Version check (release only). | SQL, then API server |
| 5 | `GetApplicationInfo` | SQL |
| 6 | `SaveAppVersionLastAccess("GroupsVersion")` | SQL write |
| 7 | `SharedData.CheckForNewInventory(False)` | SQL, conditional |
| 8 | `New AdvSetups(SharedData.Inventory)` | Not read |
| 9 | `GetStation` | EF plus option tables |
| 10 | Station disabled check. | None |
| 11 | `UpdateStationInfo` | SQL read and write |
| 12 | License check (release only). | SQL, then TCP 15050 |
| 13 | `ServerTimeValidator` | API server |
| 14 | `InitializePinPad`, `PerformCreditInitialization` | Local serial port. Background credit task. |
| 15 | `New BowlingInterface()` | Not read |
| 16 | `frmAdvGroups_Load` | SQL, many reads. See below. |
| 17 | Sign-in timer fires (`tmrSignIn_Tick`). | Not traced. See "Not traced". |

## Raw SQL

### 1. Database version

`UpgradeCheckProcessor.GetServerVersion`. Release only. Identical to the POS.

```sql
SELECT TOP 1 Version FROM VersionInfo ORDER BY UpgradeDate DESC, Version DESC
```

### 2. Application info

`AdvCommonCtrls.GetApplicationInfo`. Identical to the POS. See `pos-startup-sql.md`, section 2, for the full column list.

```sql
SELECT ... FROM ApplicationInfo WHERE AppID=1
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

- `@AppString` = `GroupsVersion`
- `@AppVersion` = assembly version

The same pattern runs with `@AppString` = `Groups_LastUsed`. This name follows the POS and Console pattern (`POS_LastUsed`, `MC_LastUsed`) and was not confirmed in the Groups code path. `TypeName` for that pass is `System.DateTime`.

### 4. Options tables

`OptionsTable.LoadAsync` and `OptionsTable.Load`. The table and key vary.

```sql
SELECT OptionName, OptionValue, TypeName FROM [{TableName}] WHERE KeyNo = @KeyNo ORDER BY OptionName
```

| Caller | TableName | KeyNo |
|---|---|---|
| `GetStationInfo` → `LoadSpecialOptions` | `StationOptions` | station number |
| `GetStationInfo` → `LoadSpecialOptions` | `DivisionOptions` | station's division number |
| `DivisionOptions` constructor (division number above 0) | `DivisionOptions` | `0` (base options) |
| `UpdateStationInfo` (`sOptions.Load()`) | `StationOptions` | station number |
| `frmAdvGroups_Load` (`sOptions.Load()`) | `StationOptions` | station number |
| `SharedData.LoadOptions` (`GroupOptions`) | `GroupOptions` | `1` |
| `SharedData.LoadCustOptions` (`CustomerOptions`) | `CustOptions` | `1` |

`StationOptions` loads three times during startup: in `GetStationInfo`, in `UpdateStationInfo`, and in `frmAdvGroups_Load`. The base-options load is inferred from the constructor, not confirmed.

### 5. Station options written at startup

`UpdateStationInfo` calls `HardwareInfoProvider.GetHardwareInfo()` and `sOptions.Save()`.

| OptionName (`StationOptions`) | Source |
|---|---|
| `ComputerName` | `Dns.GetHostName` |
| `ComputerIPAddress` | First IPv4 address from `Dns.GetHostEntry` |

The SQL emitted by `OptionsTable.Save` was not read. The write is skipped if hardware info fails.

### 6. License check

Release only. `LicenseHandler.ValidateLicenseWithNagAsync(owner, "Groups", ...)`.

```sql
SELECT LocName, Address1, Address2, City, State, ZipCode, Country, PhoneNumber, LicenseServer FROM ApplicationInfo WHERE AppID = 1
```

The result feeds a TCP request to the license server on port 15050.

If the license returns a MAC address mismatch, a prompt offers to update the license. If accepted, and a security override for "Station Setup" is granted, `OptionsTable.WriteValue` writes the station's current MAC address:

- Table: `StationOptions`
- Key: station number
- Option: `MacAddress`

The SQL behind `WriteValue` was not read.

### 7. Group options, customer options, and reader defaults

`SharedData.LoadAllObjects` opens one connection and runs the loads below in order. If any step throws, the exception is displayed and the application exits.

`GroupOptions` and `CustomerOptions` use the options query in section 4.

Reader defaults, `SharedData.LoadReaderDefaults`:

```sql
SELECT NumDecPlaces, PricePerPoint, NameOfCard FROM ReaderDefaults WHERE KeyID=1
```

Calendar defaults, `SharedData.LoadCalendarDefaults`:

```sql
SELECT * FROM Calendars_Defaults
```

### 8. Group defaults

`GroupDefaults.Load`, `src/main/libraries/AdvantageGroupData/AdvGroupData/GroupDefaults.vb` line 101.

```sql
SELECT RefundPlayerInvNo, DepositInvNo, AskPreEncodeNumber, Host_Description,
       Greeter_Description, SalesPerson_Description,
       ForfeitDepositInvNo,
       AskLanguage, GroupDiscountID, MinColumnWidth, PrintKitchenNotes,
       IncludeHostName, IncludeGreeterName, IncludeSalespersonName, IncludeGroupName,
       IncludeEventDescription, IncludePhoneNumber, IncludeEventType, PrintColorStyle,
       DefaultEMailAddress, IncludeEventSize, IncludeEventChildSize, UseEmployeeEMail,
       AllowPosAdd, AllowPosPay, IncludeGroupNotes, IncludeEventNotes, IncludePrivateNotes,
       AdjustEventTimes, KitchenOffsetMinutes, FoodOffsetMinutes, IncludeEventTimes,
       DefaultCalendar, EmailAccountID
FROM GroupDefaults WHERE KeyNo=1
```

### 9. Customer types

`CustType.LoadAll`, `src/main/libraries/AdvantageCustomer/AdvantageRewards/CustType.vb` line 58.

```sql
SELECT CustTypeID, Description, Individual, Enabled, CustTypeGUID FROM CustTypes
```

### 10. Group extras

`GroupExtra.LoadAll`, `src/main/libraries/AdvantageAreaManagement/AdvantageAreaManagement/GroupExtra.cs` line 156.

```sql
SELECT GroupExtraID, Description, InvID, WebEnabled FROM GroupExtras
```

### 11. Areas

`Area.LoadAll`, `src/main/libraries/AdvantageAreaManagement/AdvantageAreaManagement/Area.cs` line 438. One batch with two result sets.

```sql
SELECT
    AreaGUID, Description, ShowArea, AreaType, Capacity, Min_Capacity, PreArrivalMinutes, PostDepartureMinutes, AreaShownAt, Min_DepositAmount,
    OpenAreaDesc, WebEnabled, InvID, WebCapacity, ShortDetails, LongDetails, ShowCapacity, LaserTagArea, GoKartArea
FROM
    Areas;

SELECT
    SubAreaGUID, AreaGUID, Description, Capacity, InvID, SOrder, ShowSubArea, WebEnabled, UseAreaItems, AlwaysBookFull, WebCapacity, MainAreaCapacityReserved
FROM
    Areas_SubAreas
ORDER BY
    AreaGUID
```

`AreaItem.LoadAll` follows. Its SQL was not extracted.

### 12. Event types

`EventType.LoadAll`, `src/main/libraries/AdvantageAreaManagement/AdvantageAreaManagement/EventType.cs` line 471.

```sql
SELECT EventTypeID, EventTypeNo, EventGroupType, Description, Details, GratuityPercentage, DefaultDepositType, DefaultDeposit, BaseItemID, IncrementItemID,
       BaseAttendees, IncrementAttendees, MaxAttendees, COrder, Enabled, WebEnabled, ReservationReport, InvoiceReport,
       ShortDetails, NULL, NULL, AdultIncrementItemID, AdultBaseAttendees, AdultIncrementAttendees, CommentReport, ThankYouReport,
       ReservationEmailTemplateID, InvoiceEmailTemplateID, CommentCardsEmailTemplateID, ThankYouEmailTemplateID, IncludeAdultsInAttendees, AlwaysShowCalendar, ScheduledActivities,
       IncludeAdultsInCapacity, HeadCountOption, GroupHCClassNo, BirthdayHCClassNo, ReservationSignatureEmailTemplateId, WebGroupID, WebPath, MaxAdvancedBookingInDays, MinAdvancedBookingInDays, FromEmailAddress
FROM GroupEventTypes
```

Four follow-up loads run after it: `EventTypeArea.LoadAll`, `EventTypeExtra.LoadAll`, `EventTypeScalingItem.LoadAll`, and `EventTypeMilestone.LoadAll`. Their SQL was not extracted.

### 13. Calendars

`Calendar.LoadAll`, `src/main/libraries/AdvantageAreaManagement/AdvantageAreaManagement/Calendar.cs` line 129.

```sql
SELECT CalID, Description, ShowCalendar, StartDate, EndDate, CalendarType, CalendarID FROM Calendars
```

`CalendarLocation.LoadAll` follows. Its SQL was not extracted.

## Entity Framework (SQL generated at runtime)

| Step | Context | LINQ or purpose |
|---|---|---|
| Station (`GetStationInfo`) | `CommonDataContext` | `dc.Stations.AsNoTracking().FirstOrDefault(p => p.StationNo == stationNo)` |
| Division (`LoadDivisionInfo`) | `CoreDataContext` | `dc.Divisions.AsNoTracking().FirstOrDefault(p => p.DivNo == DivNo)` |
| Kitchen printers (`frmAdvGroups_Load`) | `CommonDataContext` | `KitchenPrinterHandler.GetKitchenPrinters(db)`. Loaded into a dictionary keyed by `KitchenPrinterNo`. Each printer carries its `OrderPrepPrinters`. The generated SQL was not captured. |

## Inventory load

`SharedData.CheckForNewInventory(False)`, `SharedData.vb` line 206, runs before `AdvSetups` is created.

- When the stored inventory change date is newer than the last load time, it constructs `Inventory`. See `pos-startup-sql.md`, "Inventory load".
- The constructor loads about 16 lookup sets in parallel.
- `loadAll` is false, so items are not bulk loaded.
- In `frmAdvGroups_Load`, `AdvSetups` is created a second time with the same `SharedData.Inventory`. It does not reload inventory.

## Non-database communication

| Step | Target | Detail |
|---|---|---|
| Version check (release only) | API server | Reads the server version and, if versions match, the patch list. |
| License check (release only) | License server, TCP 15050 | `LicenseCheck` XML request and response. 15-second timeouts. The request carries the MAC address, module name `Groups`, station number, and location details. |
| Server time check | API server, `ITime.GetTimeAsync` | 2-second timeout. Warns on UTC offset difference or drift over 1 minute. |
| API base address | `http(s)://{host}:15059` | Host is the `CoreServiceServerName` app option, then `LicenseServer` from `ApplicationInfo`, then `localhost`. JWT bearer from the `AuthenticationKey` app option. |
| Telemetry | OTLP exporter | Endpoint, protocol, and headers come from app options stored in the database. |
| PIN pad | Local serial port | Opens the configured port with the idle message from `ApplicationInfo.IdlePinPadMessage`. |
| Credit initialization | Background task, `CreditInitialization.Default.Initialize()` | Messages are shown on the form thread when the task completes with messages. |

## Differences from POS startup

- **No `AppOptions.Load`:** Groups does not run it in `PerformStartup`.
- **No pole display, security feed, or reader defaults in `PerformStartup`:** Reader defaults load later through `SharedData.LoadAllObjects`.
- **Group-specific reads:** `GroupOptions`, `GroupDefaults`, `Areas`, `Areas_SubAreas`, `GroupEventTypes`, `GroupExtras`, `Calendars`, `Calendars_Defaults`, and `CustTypes`.
- **POS screens and keypad queries are absent.**
- **Sign-in is a timer after the main form loads,** not part of `PerformStartup`.

## No SQL

- `/training` swaps the connection catalog in code. It does not issue a query.
- `RunAsSingleInstance` is a local process check.
- The auto-lock handler is created from `StationInfo.AutoLock`, loaded earlier.

## Not traced

- `tmrSignIn_Tick`: `GetEmployee`, the `Group Sign In` access check, `Groups_SignIn`, and `GetSessionDefaults`. The sign-in runs after the main form loads and is the first user-driven step.
- `UpdateButtonStatesAsync` and `SetClickToPayButtonVisibleState`, called at the end of `frmAdvGroups_Load`.
- `pnlCalendar.Initialize()`. It may query the database.
- `AdvSetups` and `BowlingInterface` constructors.
- SQL for `AreaItem.LoadAll`, `CalendarLocation.LoadAll`, `EventTypeArea.LoadAll`, `EventTypeExtra.LoadAll`, `EventTypeScalingItem.LoadAll`, `EventTypeMilestone.LoadAll`, and `OptionsTable.Save`.
- `CreditInitialization.Initialize` internals.
- EF-generated SQL. To capture exact statements, run a SQL Profiler or Extended Events trace while launching Groups against a test database.

## Load testing notes

- Every Groups start writes `StationOptions` rows (`GroupsVersion`, the last-used row, `ComputerName`, `ComputerIPAddress`). Use a disposable database.
- Simulated stations need real, enabled station records. A missing station sends Groups to the Change Station dialog.
- The license service counts stations by MAC address. Do not point simulated stations at a customer license service.
- Groups does a larger reference-data load than the POS. Areas, event types, calendars, and their child tables load on every start, not only when inventory changed.
- `StationOptions` is read three times per start.
- Debug builds skip the version and license checks. Load test against Release builds to exercise them.
