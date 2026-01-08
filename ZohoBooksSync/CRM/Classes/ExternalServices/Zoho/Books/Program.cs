using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CRM.Classes.ExternalServices.Zoho.Books.Configuration;
using CRM.Classes.ExternalServices.Zoho.Books.Models;
using CRM.Classes.ExternalServices.Zoho.Books.Persistence;
using CRM.Classes.ExternalServices.Zoho.Persistence;
using CRM.Classes.ExternalServices.Zoho.Books.Services;
using CRM.Classes.ExternalServices.Zoho.Services;

namespace CRM.Classes.ExternalServices.Zoho.Books
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var options = ZohoAPIConfigurationOptions.FromEnvironment();
            var connectionOptions = ZohoBooksConnectionOptions.FromEnvironment();

            if (string.IsNullOrWhiteSpace(connectionOptions.OrganizationId))
            {
                Console.WriteLine("Configure ZOHO_BOOKS_ORGANIZATION_ID to run the sync.");
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

                var tokenStore = new TokenStore(dbContext.Database);
                await tokenStore.InitializeAsync();

                var tokenProvider = new ZohoTokenProvider(httpClient, options, tokenStore);
                var zohoClient = new ZohoBooksClient(httpClient, options, connectionOptions, referenceStore, tokenProvider);

                var inventoryItems = new List<InventoryItem>
                {
                    new InventoryItem
                    {
                        LocalId = 100,
                        Name = "Widget",
                        Sku = "WIDGET-001",
                        Rate = 49.99m,
                        Quantity = 25
                    }
                };

                var contacts = new List<Contact>
                {
                    new Contact
                    {
                        LocalId = 100,
                        Name = "Acme Corp",
                        Email = "billing@acme.test",
                        Phone = "+1-555-0100"
                    }
                };

                var invoices = new List<Invoice>
                {
                    new Invoice
                    {
                        LocalId = 100,
                        ContactLocalId = 100,
                        InvoiceDate = DateTime.UtcNow.Date,
                        CurrencyCode = "USD",
                        Jurisdiction = "US",
                        RelationshipManager = "Taylor Reed",
                        TaxName = "Sales Tax",
                        TaxPercentage = 7.5m,
                        Notes = "Thanks for your business.",
                        LineItems = new List<InvoiceLineItem>
                        {
                            new InvoiceLineItem
                            {
                                ItemLocalId = 100,
                                Description = "Widget",
                                Rate = 49.99m,
                                Quantity = 2,
                                TaxName = "Sales Tax",
                                TaxPercentage = 7.5m
                            }
                        }
                    }
                };

                await zohoClient.SyncInventoryItemsAsync(inventoryItems);
                await zohoClient.SyncContactsAsync(contacts);
                await zohoClient.SyncInvoicesAsync(invoices);

                await zohoClient.UpdateInventoryItemsAsync(inventoryItems);
                await zohoClient.UpdateContactsAsync(contacts);
                await zohoClient.UpdateInvoicesAsync(invoices);

                var payments = await zohoClient.PullPaymentsAsync(new[] { 100 });
                foreach (var payment in payments)
                {
                    Console.WriteLine($"Payment {payment.PaymentId} for {payment.Amount} on {payment.Date:d}");
                }
            }
        }
    }
}
