using System.Data.Entity;

namespace ZohoBooksSync.Persistence
{
public sealed class ZohoBooksDbContext : DbContext
{
    public ZohoBooksDbContext(string connectionString)
        : base(connectionString)
    {
    }
}
}
