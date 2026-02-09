using System.Data.Common;
using CRM.Classes.Security;
using CRM.Email.Abstractions;
using CRM.Email.Persistence;

namespace CRM.Web.Infrastructure;

public sealed class EmailExecutionContextAccessor : IEmailExecutionContextAccessor
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EmailExecutionContextAccessor(SqlConnectionFactory connectionFactory, IHttpContextAccessor httpContextAccessor)
    {
        _connectionFactory = connectionFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public RBACUser CurrentUser
    {
        get
        {
            var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                username = "demo.user@crm.local";
            }

            return new RBACUser
            {
                Username = username,
                FirstName = "Demo",
                LastName = "User"
            };
        }
    }

    public DbConnection CreateConnection() => _connectionFactory.CreateConnection();
}
