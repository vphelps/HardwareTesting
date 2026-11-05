-- From Manager Console startup (AdvCommonCtrls.GetApplicationInfo).
-- Source: manager-console-startup-sql.md
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
FROM ApplicationInfo WHERE AppID=1;
