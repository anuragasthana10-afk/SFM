using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CRM.Classes.ExternalServices.Zoho.Books.Configuration;
using CRM.Classes.ExternalServices.Zoho.Books.Models;
using CRM.Classes.ExternalServices.Zoho.Books.Persistence;
using CRM.Classes.ExternalServices.Zoho.Books.Services;
using CRM.Classes.ExternalServices.Zoho.Persistence;
using CRM.Classes.ExternalServices.Zoho.Services;

namespace CRM.Classes.ExternalServices.Zoho.Books
{
    public static class Program2
    {
        public static async Task Main(string[] args)
        {
            var uaeOptions = UaeZohoAPIConfigurationOptions.FromEnvironment();
            var swissOptions = SwissZohoAPIConfigurationOptions.FromEnvironment();
            var connectionOptions = ZohoBooksConnectionOptions.FromEnvironment();
            var location = args.Length > 0
                ? args[0]
                : Environment.GetEnvironmentVariable("ZOHO_BOOKS_LOCATION") ?? "uae";
            var isUae = location.Equals("uae", StringComparison.OrdinalIgnoreCase);
            var options = isUae
                ? (IZohoApiConfigurationOptions)uaeOptions
                : swissOptions;
            connectionOptions.ActiveOrganizationId = isUae
                ? connectionOptions.UaeOrganizationId
                : connectionOptions.SwissOrganizationId;

            if (string.IsNullOrWhiteSpace(connectionOptions.ActiveOrganizationId))
            {
                Console.WriteLine("Configure ZOHO_BOOKS_UAE_ORGANIZATION_ID or ZOHO_BOOKS_SWISS_ORGANIZATION_ID to run the sync.");
                return;
            }

            if (string.IsNullOrWhiteSpace(connectionOptions.SqlConnectionString))
            {
                Console.WriteLine("Configure ZOHO_BOOKS_SQL_CONNECTION_STRING to run the sync.");
                return;
            }

            using (var dbContext = new ZohoBooksDbContext(connectionOptions.SqlConnectionString))
            using (var httpClient = new HttpClient())
            {
                var referenceStore = new ReferenceStore(dbContext.Database);
                await referenceStore.InitializeAsync();

                var tokenCode = isUae ? TokenStore.UaeTokenCode : TokenStore.SwissTokenCode;
                var tokenStore = new TokenStore(dbContext.Database, tokenCode);
                await tokenStore.InitializeAsync();

                var tokenProvider = new ZohoTokenProvider(httpClient, options, tokenStore);
                var zohoClient = new ZohoBooksClient(httpClient, options, connectionOptions, referenceStore, tokenProvider);

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
                        CurrencyCode = "USD",
                        Jurisdiction = "US",
                        RelationshipManager = "Jordan Lee",
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
            }
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
