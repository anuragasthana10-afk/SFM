using CRM.Classes;
using CRM.Classes.ExternalServices.Zoho.Books;
using CRM.Classes.ExternalServices.Zoho.Books.Models;
using CRM.Classes.ExternalServices.Zoho.Books.Persistence;
using CRM.Classes.ExternalServices.Zoho.Books.Reporting;
using CRM.Classes.ExternalServices.Zoho.Books.Services;
using CRM.Classes.ExternalServices.Zoho.Persistence;
using CRM.Classes.ExternalServices.Zoho.Services;
using CRM.Classes.Helpers;
using CRM.Models.ECom;
using CRM.Models.Shared;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Dynamic;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Invoice = CRM.Classes.ExternalServices.Zoho.Books.Models.Invoice;

namespace CRM.Tasks.TaskHandlers
{
    class ZohoBooksDataPush : TaskHandlerBase
    {
        const bool m_bTrialMode = false;
        const bool m_bMigrationMode = true;
        const string m_AccountingIntegration_StartDate = "01-January-2026";   //"01-January-2026";
        const string m_AccountingMigration_EndDate = "02-January-2026 23:59:59";
        const int m_iMaxInvoiceLineItemDescriptionLength = 199;


        const bool m_bIsCashBasedAccountingSystem = true;
        const bool m_bOnlyPaymentClosedInvoicesToBeConsideredAsPaid = false;


        ZohoBooksDataPush(JObject Parameters) : base(Parameters) { }

        public override async Task<int> RunTaskAsync()
        {
            int iRetVal = 1;

            CurrentContext.ScreenCode = AppSettings.ScreenCode_Accounts;
            System.Web.HttpContext.Current.Server.ScriptTimeout = 6000; //10 minutes.

            using (var dbContext = new SharedModel())
            using (var httpClient = new HttpClient())
            {
                var runId = Guid.NewGuid();
                var strSQLInvoiceUpdate = GetInvoiceUpdateSQLQueryString();
                var listAllInvoices = dbContext.Database.SqlQuery<vwZBInvoice>(strSQLInvoiceUpdate, new object[] { }).ToList();


                Exception syncError = null;
                var locations = new[] { "uae", "swiss" };
                foreach (var locationArg in locations)
                {
                    try
                    {
                        await ProcessLocationAsync(listAllInvoices, dbContext, httpClient, runId, locationArg);
                    }
                    catch (Exception ex)
                    {
                        syncError = ex;
                        break;
                    }
                }

                var syncOperations = await new ReferenceStore(dbContext.Database, ZohoBooksLocation.Uae)
                    .GetSyncOperationsAsync(runId);
                var htmlReport = SyncOperationReportBuilder.BuildHtmlTable(syncOperations, ResolveUserCodes);

                string NotificationType_ZohoBooksDataPush = AppSettings.SysConfiguration["NotificationType_ZohoBooksDataPush"];
                Business.Communication.Helper.Notify(NotificationType_ZohoBooksDataPush, "Zoho Books data push report", htmlReport, _dbConnection: dbContext.Database.Connection);

                if (syncError != null)
                {
                    iRetVal = 1;
                    ExceptionDispatchInfo.Capture(syncError).Throw();
                }
            }
            return iRetVal;
        }

        private static async Task ProcessLocationAsync(
            IReadOnlyCollection<vwZBInvoice> listAllInvoices,
            SharedModel dbContext,
            HttpClient httpClient,
            Guid runId,
            string locationArg)
        {
            var locationInvoices = listAllInvoices.Where(i => i.AccountingLocation == locationArg).ToList();
            if (locationInvoices.Count == 0)
            {
                return;
            }

            if (!ZohoBooksRunContext.TryCreate(locationArg, out var context, out var error))
            {
                throw new Exception("CRM:Job:ZohoBooksDataPush::RunTask() - " + error);
            }
            if (string.IsNullOrWhiteSpace(context.ConnectionOptions.ActiveOrganizationId))
            {
                throw new Exception($"CRM:Job:ZohoBooksDataPush::RunTask() - Configure {context.OrganizationIdEnvironmentVariable} to run the sync for {context.LocationName}.");
            }

            var referenceStore = new ReferenceStore(dbContext.Database, context.Location);
            await referenceStore.InitializeAsync();

            var tokenStore = new TokenStore(dbContext.Database, context.TokenCode);
            await tokenStore.InitializeAsync();

            var tokenProvider = new ZohoTokenProvider(httpClient, context.Options, tokenStore);
            var zohoClient = new ZohoBooksClient(httpClient, context.Options, context.ConnectionOptions, referenceStore, tokenProvider, runId);

            List<Contact> contacts;
            contacts = locationInvoices.Select(i => new Contact { LocalId = i.ContactAccID, Name = i.CompanyName }).Distinct(new ContactComparer()).ToList();
            if (m_bTrialMode)
            {
                foreach (var contact in contacts)
                {
                    contact.Name = contact.Name.MaskChars('x', 60, ExtensionMethods.MaskOption.InTheMiddleOfString);
                }
            }

            List<InventoryItem> inventoryItems;
            inventoryItems = locationInvoices.Select(i => new InventoryItem { LocalId = i.InventoryItemID, Name = i.InventoryItemName, Sku = i.InventoryItemCode }).Distinct(new InventoryItemComparer()).ToList();

            var invoices = new List<Invoice>();
            var groupList = locationInvoices.GroupBy(v => v.InvoiceNumber).Select(v => v.ToList()).OrderBy(v => v.Max(i => i.InvoiceModifyDate)).ToList();
            foreach (var group in groupList)
            {
                List<InvoiceLineItem> lstLineItems;
                lstLineItems = group.Select(l => new InvoiceLineItem
                {
                    ItemLocalId = l.InventoryItemID,
                    Description = (l.InvoiceItemDescription).Truncate(m_iMaxInvoiceLineItemDescriptionLength, true),
                    AccountCode = l.Accounting_AccountCode,
                    Quantity = l.Quantity,
                    Rate = l.UnitAmount,
                    TaxPercentage = l.TaxRate,
                    TaxAmount = l.TaxAmount,
                    Discount = l.ItemDiscount
                }).ToList();

                var invoice = new Invoice
                {
                    LocalId = group[0].InvoiceID,
                    ContactLocalId = group[0].ContactAccID,
                    InvoiceDate = group[0].InvoiceDate ?? DateTime.UtcNow,
                    InvoiceNumber = group[0].InvoiceNumber,
                    CurrencyCode = group[0].Currency,
                    ExchangeRate = group[0].ExchangeRate,
                    Jurisdiction = group[0].Jurisdiction,
                    RelationshipManager = group[0].AccountManager,
                    //PlaceOfSupply = "DU",
                    Subject = group[0].InvoiceSubject,
                    LineItems = lstLineItems,
                    InvoiceFileAttachment_FileName = group[0].InvoiceDocument_FileName
                };

                invoices.Add(invoice);
            }

            await EnsureContactsAsync(zohoClient, referenceStore, contacts);
            await EnsureInventoryItemsAsync(zohoClient, referenceStore, inventoryItems);

            await zohoClient.SyncInvoicesAsync(invoices);
            await zohoClient.UpdateInvoicesAsync(invoices);
        }

        private static IDictionary<int, string> ResolveUserCodes(string entityType, IReadOnlyCollection<int> localKeys)
        {
            var resolvedCodes = new Dictionary<int, string>();
            foreach (var localKey in localKeys)
            {
                resolvedCodes[localKey] = $"{entityType}-{localKey}";
            }

            return resolvedCodes;
        }

        private static async Task EnsureContactsAsync(ZohoBooksClient zohoClient, ReferenceStore referenceStore, IReadOnlyCollection<Contact> contacts)
        {
            var existingContacts = await referenceStore.GetContactIdsAsync(contacts.Select(contact => contact.LocalId));
            var missingContacts = contacts.Where(contact => !existingContacts.ContainsKey(contact.LocalId)).ToList();
            if (missingContacts.Count > 0)
            {
                await zohoClient.SyncContactsAsync(missingContacts);
            }
        }

        private static async Task EnsureInventoryItemsAsync(ZohoBooksClient zohoClient, ReferenceStore referenceStore, IReadOnlyCollection<InventoryItem> items)
        {
            var existingItems = await referenceStore.GetItemIdsAsync(items.Select(item => item.LocalId));
            var missingItems = items.Where(item => !existingItems.ContainsKey(item.LocalId)).ToList();
            if (missingItems.Count > 0)
            {
                await zohoClient.SyncInventoryItemsAsync(missingItems);
            }
        }

        public override int AddTask(Dictionary<string, string> Parameters, DbConnection dbConn = null)
        {
            throw new NotImplementedException();
        }


        private string GetInvoiceUpdateSQLQueryString(List<int> p_listInvoiceIDs = null)
        {
            string strSQL = "";
            strSQL += @"
DECLARE @SwissProducts TABLE (ProdID int);
INSERT @SwissProducts(ProdID) VALUES(32); --,(32),(0);

SELECT 
InvoiceID, RNDenseInvoiceNumber, InvoiceModifyDate, LastInvoiceSyncDate, ContactAccID, AccountModifyDate
 ,CompanyName, Jurisdiction, AccountCode AS ContactAccountCode, '' AS EmailAddress
, InvoiceNumber AS InvoiceNumber, [Subject] AS InvoiceSubject
";
            if (m_bIsCashBasedAccountingSystem)
            {
                strSQL += @"
, ISNULL(FirstPaymentDate, InvoiceDate) AS InvoiceDate, ISNULL(FirstPaymentDate, InvoiceDate) AS DueDate
";
            }
            else
            {
                strSQL += @"
, InvoiceDate AS InvoiceDate, InvoiceDate AS DueDate
";
            }

            strSQL += @"
, Total AS Total, ItemDiscount, ProductID AS InventoryItemID, UPPER(InventoryItemCode) AS InventoryItemCode, InventoryItemName, InventoryItemModifyDate, ISNULL(InvoiceItemDescription, ProdDescription) AS [InvoiceItemDescription], Quantity AS Quantity, Price AS UnitAmount, Accounting_AccountCode, 'VAT' AS TaxType, ISNULL(VAT, 0.0) AS TaxAmount, ISNULL(VATRate, 0) AS TaxRate
, AccountManager, IntroducerName, Currency AS Currency, PaymentAmounts
, CAST(((SELECT EXCH_CONVERT.ExchangeRate FROM fnCurrencyExchangeRate(ConvertedBaseCurrencyCode, InvoiceDate) AS EXCH_CONVERT) / OriginExchangeRate) AS DECIMAL(7, 4)) AS ExchangeRate
, ConvertedBaseCurrencyCode AS BaseCurrencyCode, PaymentDates, HasMultiplePayments, AccountingLocation, InvoiceDocument_FileName
FROM
(
SELECT  
	*
	, (CASE WHEN PaymentDate IS NOT NULL AND PaymentDate >= '29-July-2022' AND CompanyLocation_ID IN (4) THEN 'Dubai' ELSE InvoiceLocation END) AS InvoiceLocation_Adjusted
FROM
(
SELECT *, ISNULL(VariantProductCode, ProductCode) AS InventoryItemCode, ISNULL(VariantProductDescription, ProductDescription) AS ProdDescription
    --,ISNULL(Product_Accounting_AccountCode, VariantProduct_Accounting_AccountCode) AS Product_Accounting_AccountCode
    ,(CASE WHEN (ISNULL(VariantProductCode, ProductCode) LIKE '%-FEES_SETUP' OR ISNULL(VariantProductCode, ProductCode) LIKE '%-FEES_ANNUAL') AND VariantProduct_Accounting_AccountCode IS NOT NULL THEN VariantProduct_Accounting_AccountCode ELSE Product_Accounting_AccountCode END) AS Accounting_AccountCode
	,ROW_NUMBER() OVER(PARTITION BY InvoiceID ORDER BY InvoiceID, PaymentDate ASC) AS RNPaymentNumber 
	,DENSE_RANK() OVER(PARTITION BY InvoiceID ORDER BY InvoiceID, PaymentRowID ASC) AS RNDensePaymentNumber
    ,DENSE_RANK() OVER(ORDER BY InvoiceModifyDate ASC, InvoiceID ASC) AS RNDenseInvoiceNumber
	FROM 
(
SELECT DISTINCT
INVP.ID AS PaymentRowID	/* Required for the DISTINCT keyword to correctly remove truly duplicate rows. */
, INV.ID AS InvoiceID
, ACC.AccountCode
, ACC.ID AS ContactAccID
,ISNULL(ACC.ModifyDate, ACC.CreateDate) AS AccountModifyDate
, CONCAT(ACC.[Name], ' [', ACC.AccountCode, ']') AS CompanyName
--,(SELECT PROD.[Name] FROM Products PROD WHERE PROD.ID IN (SELECT ACCJUR.Product_ID FROM Accounts ACCJUR WHERE ACCJUR.ID IN (SELECT OBJR.ObjectScreen_RefID FROM ObjectRelations OBJR WHERE OBJR.[ObjectScreen_Code]='" + AppSettings.ScreenCode_Accounts + @"' AND OBJR.AssociatedObject_Code='" + new Client_Orders().GetThisObjectCode() + @"' AND OBJR.AssociatedObject_RefID=INV.Client_Orders_ID AND ISNULL(OBJR.DelFlag, 0)=0))) AS Jurisdiction_Code
,(SELECT JP.Name FROM Report_Products_CodeNameMap JP WHERE JP.Code=(SELECT PROD.[Name] FROM Products PROD WHERE PROD.ID IN (SELECT ACCJUR.Product_ID FROM Accounts ACCJUR WHERE ACCJUR.ID IN (SELECT OBJR.ObjectScreen_RefID FROM ObjectRelations OBJR WHERE OBJR.[ObjectScreen_Code]='" + AppSettings.ScreenCode_Accounts + @"' AND OBJR.AssociatedObject_Code='" + new Client_Orders().GetThisObjectCode() + @"' AND OBJR.AssociatedObject_RefID=INV.Client_Orders_ID AND ISNULL(OBJR.DelFlag, 0)=0)))) Jurisdiction
, INV.OrderType_Code AS OrderType
, (SELECT ORD.OrderNumber FROM Client_Orders ORD WHERE ORD.ID=INV.Client_Orders_ID) AS OrderNumber
, (CASE WHEN PROD.ProductCategory_ID IN (11,20,21) AND INV.OrderType_Code IN ('SETUP', 'TRANSFERIN') THEN PROD.Code + '-FEES_SETUP' ELSE PROD.Code END) AS ProductCode
, PROD.[Description] AS ProductDescription
, PROD.Accounting_AccountCode AS Product_Accounting_AccountCode
, PROD.ID AS ProductID
, ISNULL(PROD.Accounting_ProductName, PROD.Name) AS InventoryItemName
, ISNULL(PROD.ModifyDate, PROD.CreateDate) AS InventoryItemModifyDate
--, ORDITEMS.ProductVariants_ID
--, ORDITEMS.ID AS OrderItemID
, (SELECT TOP 1 PRODVARS.Code FROM ProductVariants PRODVARS WHERE PRODVARS.ProductID=PROD.ID AND PRODVARS.ID=ORDITEMS.ProductVariants_ID) AS VariantProductCode
, (SELECT TOP 1 PRODVARS.[Name] FROM ProductVariants PRODVARS WHERE PRODVARS.ProductID=PROD.ID AND PRODVARS.ID=ORDITEMS.ProductVariants_ID) AS VariantProductDescription
, (SELECT TOP 1 PRODVARS.Accounting_AccountCode FROM ProductVariants PRODVARS WHERE PRODVARS.ProductID=PROD.ID AND PRODVARS.ID=ORDITEMS.ProductVariants_ID) AS VariantProduct_Accounting_AccountCode
, (CASE WHEN TRIM(ISNULL(ORDITEMS.[Description], ''))='' THEN NULL ELSE ORDITEMS.[Description] END) AS InvoiceItemDescription
, ORDITEMS.Quantity
, ORDITEMS.Price
, ORDITEMS.VAT
, ORDITEMS.Total
, ORDITEMS.VATRate
, ORDITEMS.Discount AS ItemDiscount
, INV.[Subject]
, INV.InvoiceNumber
,INV.[Date] AS [InvoiceDate]
, ISNULL(INV.ModifyDate, INV.CreateDate) AS InvoiceModifyDate
, INV_SYNC_LOG.OccurredAtUtc AS LastInvoiceSyncDate
,FORMAT(INV.[Date], 'dd-MMMM-yyyy') AS [InvoiceDate_Formatted]
, (CASE INV.PaidFlag WHEN 1 THEN 'Yes' ELSE 'No' END) AS InvoiceMarkedPaid
, INV.InvoiceCurrencyCode AS Currency
, INV.InvoiceTotal
, INV.DiscountTotal
, INVP.PaymentDate
, FORMAT(INVP.PaymentDate, 'dd-MMMM-yyyy') AS [PaymentDate_Formatted]
, INVP.PaymentAmount
, PMODE.[Name]
, (CASE WHEN ACC.Product_ID IN (SELECT ProdID FROM @SwissProducts) THEN 'CHF' ELSE 'AED' END) AS ConvertedBaseCurrencyCode
--, ((SELECT EXCH_CONVERT.ExchangeRate FROM fnCurrencyExchangeRate('AED', INV.[Date]) AS EXCH_CONVERT) / CEXCH.ExchangeRate) AS ConvertedExchangeRate
, CEXCH.BaseCurrencyCode AS OriginBaseCurrencyCode
, CEXCH.ExchangeRate AS OriginExchangeRate
, CAST(ROUND(INVP.PaymentAmount / CEXCH.ExchangeRate, 0) AS DECIMAL(16,2)) AS PaymentAmountInBaseCurrency
, CLIORD.CompanyLocation_ID
, COLOC.[name] AS InvoiceLocation
, INV.BalanceBroughtForward
, INV.BalanceCarryForward
, (SELECT SUM(INVP2.PaymentAmount) FROM InvoicePayments INVP2 WHERE INVP2.Invoice_ID=INV.ID AND ISNULL(INVP2.DelFlag, 0)=0 AND INVP2.PaymentMode_ID NOT IN (6,7)) AS TotalPaymentReceivedTilldate
, (SELECT STRING_AGG(PaymentAmount, ', ') FROM (SELECT TOP 100 PERCENT INVP2.PaymentAmount FROM InvoicePayments INVP2 WHERE INVP2.Invoice_ID=INV.ID AND ISNULL(INVP2.DelFlag, 0)=0 AND INVP2.PaymentMode_ID NOT IN (6,7) ORDER BY INVP2.PaymentDate DESC, INVP2.ID DESC) AS TMP) AS PaymentAmounts
, (SELECT STRING_AGG(PaymentDate, ', ') FROM (SELECT TOP 100 PERCENT FORMAT(INVP2.PaymentDate, 'dd-MMMM-yyyy') AS PaymentDate FROM InvoicePayments INVP2 WHERE INVP2.Invoice_ID=INV.ID AND ISNULL(INVP2.DelFlag, 0)=0 AND INVP2.PaymentMode_ID NOT IN (6,7) ORDER BY INVP2.PaymentDate DESC, INVP2.ID DESC) AS TMP) AS PaymentDates
, (SELECT TOP 1 PaymentDate FROM (SELECT TOP 100 PERCENT INVP2.PaymentDate AS PaymentDate FROM InvoicePayments INVP2 WHERE INVP2.Invoice_ID=INV.ID AND ISNULL(INVP2.DelFlag, 0)=0 AND INVP2.PaymentMode_ID NOT IN (6,7) ORDER BY INVP2.PaymentDate ASC, INVP2.ID ASC) AS TMP) AS FirstPaymentDate
, (CASE WHEN (SELECT COUNT(INVP2.PaymentAmount) FROM InvoicePayments INVP2 WHERE INVP2.Invoice_ID=INV.ID AND ISNULL(INVP2.DelFlag, 0)=0 AND INVP2.PaymentMode_ID NOT IN (6,7)) > 1 THEN 'Yes' ELSE 'No' END) AS HasMultiplePayments
, (SELECT SU.Firstname + ' ' + SU.Lastname FROM Security_Users SU WHERE SU.[User_Id]=ACC.[AccountManagerUser_ID]) AccountManager
,(CASE WHEN ACC.Introducer IS NOT NULL THEN (SELECT CLIINTRO.FirstName + ' ' + CLIINTRO.LastName FROM Clients CLIINTRO WHERE CLIINTRO.ID=ACC.Introducer) ELSE '' END) AS IntroducerName
,INVDOC.[File] AS InvoiceDocument_FileName
, (CASE WHEN ACC.Product_ID IN (32) THEN 'swiss' ELSE 'uae' END) AS AccountingLocation


FROM Invoices INV
LEFT OUTER JOIN Client_Orders CLIORD ON CLIORD.ID=INV.Client_Orders_ID AND ISNULL(CLIORD.DelFlag, 0)=0
LEFT OUTER JOIN Documents INVDOC ON INVDOC.ID=INV.Document_ID
LEFT OUTER JOIN
     (
      SELECT CLIORDtmp.ID, CLIORDtmp.Client_Orders_ID, CLIORDtmp.Products_ID, CLIORDtmp.[Description] AS [Description], CLIORDtmp.Total AS Total, CLIORDtmp.VAT AS VAT, CLIORDtmp.Price AS Price, CLIORDtmp.Quantity AS Quantity, CLIORDtmp.VATRate AS VATRate, CLIORDtmp.Discount AS Discount, CLIORDtmp.ProductVariants_ID AS ProductVariants_ID FROM Client_OrderItems CLIORDtmp WHERE ISNULL(CLIORDtmp.DelFlag, 0)=0
      UNION ALL
      SELECT SHIPtmp.ID, SHIPtmp.Client_Orders_ID, SHIPMTHDtmp.ProductID AS Products_ID, SHIPMTHDtmp.[Description] AS [Description], SHIPtmp.Total AS Total, SHIPtmp.VAT AS VAT, SHIPtmp.Price AS Price, 1 AS Quantity, SHIPtmp.VATRate AS VATRate, SHIPtmp.Discount AS Discount, NULL AS ProductVariants_ID FROM Client_OrderShippings SHIPtmp LEFT OUTER JOIN ShippingMethods SHIPMTHDtmp ON SHIPtmp.ShippingMethods_ID = SHIPMTHDtmp.ID WHERE ISNULL(SHIPtmp.DelFlag, 0)=0
     ) ORDITEMS ON ORDITEMS.Client_Orders_ID=INV.Client_Orders_ID
LEFT OUTER JOIN Products PROD ON PROD.ID=ORDITEMS.Products_ID
LEFT OUTER JOIN CompanyLocations COLOC ON COLOC.ID=CLIORD.CompanyLocation_ID
LEFT OUTER JOIN InvoicePayments INVP ON INVP.Invoice_ID=INV.ID AND ISNULL(INVP.DelFlag, 0)=0 AND INVP.PaymentMode_ID NOT IN (6,7) /*Exclude Adjustment entries and 'NA' entry*/
LEFT OUTER JOIN PaymentModes PMODE ON PMODE.ID=INVP.PaymentMode_ID
LEFT OUTER JOIN ObjectRelations OBJRACC ON OBJRACC.ObjectScreen_Code IN ('
