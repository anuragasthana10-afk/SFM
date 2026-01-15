using System.Data.Entity;

namespace CRM.Classes.ExternalServices.Zoho.Books.Persistence
{
public sealed class ZohoBooksDbContext : DbContext
{
    public ZohoBooksDbContext(string connectionString)
        : base(connectionString)
    {
    }
}
}
