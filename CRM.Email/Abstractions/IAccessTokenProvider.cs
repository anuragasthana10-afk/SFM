namespace CRM.Email.Abstractions;

public interface IAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(string scope, CancellationToken cancellationToken);
}
