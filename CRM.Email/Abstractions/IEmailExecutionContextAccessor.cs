using System.Data.Common;
using CRM.Classes.Security;

namespace CRM.Email.Abstractions;

public interface IEmailExecutionContextAccessor
{
    RBACUser CurrentUser { get; }
    DbConnection CreateConnection();
}
