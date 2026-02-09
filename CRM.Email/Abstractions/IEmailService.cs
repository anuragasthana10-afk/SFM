namespace CRM.Email.Abstractions;

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
