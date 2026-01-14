using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CRM.Classes.ExternalServices.Zoho.Books.Models;
using CRM.Classes.ExternalServices.Zoho.Books.Persistence;
using CRM.Classes.ExternalServices.Zoho.Books.Reporting;
using CRM.Classes.ExternalServices.Zoho.Books.Services;
using CRM.Classes.ExternalServices.Zoho.Persistence;
using CRM.Classes.ExternalServices.Zoho.Services;

namespace CRM.Classes.ExternalServices.Zoho.Books
{
    public static class Program2
    {
        public static async Task Main(string[] args)
        {
            var locationArg = args.Length > 0 ? args[0] : null;
            if (!ZohoBooksRunContext.TryCreate(locationArg, out var context, out var error))
            {
                Console.WriteLine(error);
                return;
            }

            if (string.IsNullOrWhiteSpace(context.ConnectionOptions.ActiveOrganizationId))
            {
                Console.WriteLine($"Configure {context.OrganizationIdEnvironmentVariable} to run the sync for {context.LocationName}.");
                return;
            }

            if (string.IsNullOrWhiteSpace(context.ConnectionOptions.SqlConnectionString))
            {
                Console.WriteLine($"Configure ZOHO_BOOKS_SQL_CONNECTION_STRING to run the sync for {context.LocationName}.");
                return;
            }

            using (var dbContext = new ZohoBooksDbContext(context.ConnectionOptions.SqlConnectionString))
            using (var httpClient = new HttpClient())
            {
                var runStartTimestamp = DateTime.UtcNow;
                var referenceStore = new ReferenceStore(dbContext.Database, context.Location);
                await referenceStore.InitializeAsync();

                var tokenStore = new TokenStore(dbContext.Database, context.TokenCode);
                await tokenStore.InitializeAsync();

                var tokenProvider = new ZohoTokenProvider(httpClient, context.Options, tokenStore);
                var zohoClient = new ZohoBooksClient(httpClient, context.Options, context.ConnectionOptions, referenceStore, tokenProvider, runStartTimestamp);

                var contacts = new List<Contact>
                {
                    new Contact
                    {
                        LocalId = 200,
                        Name = "Globex Corp",
                        Email = "ap@globex.test",
                        Phone = "+1-555-0200"
                    }
                };

                var inventoryItems = new List<InventoryItem>
                {
                    new InventoryItem
                    {
                        LocalId = 200,
                        Name = "Gizmo",
                        Sku = "GIZMO-200",
                        Rate = 79.95m,
                        Quantity = 10
                    }
                };

                var invoices = new List<Invoice>
                {
                    new Invoice
                    {
                        LocalId = 200,
                        ContactLocalId = 200,
                        InvoiceDate = DateTime.UtcNow.Date,
                        InvoiceNumber = "INV-200",
                        CurrencyCode = "USD",
                        Jurisdiction = "US",
                        RelationshipManager = "Jordan Lee",
                        Subject = "Services - Gizmo",
                        TaxName = "Sales Tax",
                        TaxPercentage = 5.0m,
                        TaxTreatment = "taxable",
                        PlaceOfSupply = "CA",
                        Notes = "Follow-up invoice.",
                        LineItems = new List<InvoiceLineItem>
                        {
                            new InvoiceLineItem
                            {
                                ItemLocalId = 200,
                                Description = "Gizmo",
                                AccountCode = "4000",
                                Rate = 79.95m,
                                Quantity = 1,
                                TaxName = "Sales Tax",
                                TaxPercentage = 5.0m
                            }
                        }
                    }
                };

                await EnsureContactsAsync(zohoClient, referenceStore, contacts);
                await EnsureInventoryItemsAsync(zohoClient, referenceStore, inventoryItems);

                await zohoClient.SyncInvoicesAsync(invoices);
                await zohoClient.UpdateInvoicesAsync(invoices);

                var invoiceAttachments = new Dictionary<int, string>
                {
                    [200] = "sample-invoice.pdf"
                };

                foreach (var attachment in invoiceAttachments)
                {
                    await zohoClient.AttachInvoicePdfAsync(attachment.Key, attachment.Value);
                }

                var syncOperations = await referenceStore.GetSyncOperationsAsync(runStartTimestamp);
                var htmlReport = SyncOperationReportBuilder.BuildHtmlTable(syncOperations, ResolveUserCodes);
                Console.WriteLine(htmlReport);
            }
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
    }
}
