using CRM.Core.Models;
using CRM.Email.Abstractions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace CRM.Email.Providers.Microsoft;

public sealed class MicrosoftMailKitClient : IEmailProvider, IEmailService
{
    private const string ImapScope = "https://outlook.office365.com/.default";
    private const string SmtpScope = "https://outlook.office365.com/.default";

    private readonly EmailProviderOptions _options;
    private readonly IAccessTokenProvider _tokenProvider;

    public MicrosoftMailKitClient(EmailProviderOptions options, IAccessTokenProvider tokenProvider)
    {
        _options = options;
        _tokenProvider = tokenProvider;
    }

    public async Task<IReadOnlyList<EmailMessageMetadata>> SyncAsync(
        EmailSyncRequest request,
        CancellationToken cancellationToken)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(_options.ImapHost, _options.ImapPort, SecureSocketOptions.SslOnConnect, cancellationToken);

        var token = await _tokenProvider.GetAccessTokenAsync(ImapScope, cancellationToken);
        var oauth2 = new SaslMechanismOAuth2(request.MailboxAddress, token);
        await client.AuthenticateAsync(oauth2, cancellationToken);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        var searchQuery = request.Since.HasValue
            ? SearchQuery.DeliveredAfter(request.Since.Value.UtcDateTime)
            : SearchQuery.All;

        var uids = await inbox.SearchAsync(searchQuery, cancellationToken);
        var summaries = await inbox.FetchAsync(
            uids,
            MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure,
            cancellationToken);

        var results = new List<EmailMessageMetadata>();
        foreach (var summary in summaries)
        {
            if (summary.Envelope is null)
            {
                continue;
            }

            var messageId = summary.Envelope.MessageId ?? summary.UniqueId.Id.ToString();
            var conversationId = ResolveConversationId(summary.Headers["References"], messageId);

            results.Add(new EmailMessageMetadata
            {
                MessageId = messageId,
                ConversationId = conversationId,
                Subject = summary.Envelope.Subject ?? string.Empty,
                Snippet = summary.Envelope.Subject,
                ReceivedAt = summary.Envelope.Date ?? DateTimeOffset.UtcNow,
                Participants = BuildParticipants(summary.Envelope),
                HasAttachments = summary.Attachments != null && summary.Attachments.Any()
            });
        }

        await client.DisconnectAsync(true, cancellationToken);
        return results;
    }

    public async Task<EmailContent?> FetchContentAsync(string messageId, CancellationToken cancellationToken)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(_options.ImapHost, _options.ImapPort, SecureSocketOptions.SslOnConnect, cancellationToken);

        var token = await _tokenProvider.GetAccessTokenAsync(ImapScope, cancellationToken);
        var oauth2 = new SaslMechanismOAuth2(_options.ServiceMailboxAddress, token);
        await client.AuthenticateAsync(oauth2, cancellationToken);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        var uids = await inbox.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId), cancellationToken);
        if (uids.Count == 0)
        {
            await client.DisconnectAsync(true, cancellationToken);
            return null;
        }

        var mimeMessage = await inbox.GetMessageAsync(uids[0], cancellationToken);
        var content = new EmailContent
        {
            MessageId = mimeMessage.MessageId ?? messageId,
            HtmlBody = mimeMessage.HtmlBody,
            TextBody = mimeMessage.TextBody,
            Attachments = ExtractAttachments(mimeMessage)
        };

        await client.DisconnectAsync(true, cancellationToken);
        return content;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(MailboxAddress.Parse(message.From));
        mimeMessage.To.AddRange(message.To.Select(MailboxAddress.Parse));
        mimeMessage.Cc.AddRange(message.Cc.Select(MailboxAddress.Parse));
        mimeMessage.Bcc.AddRange(message.Bcc.Select(MailboxAddress.Parse));
        mimeMessage.Subject = message.Subject;

        if (!string.IsNullOrWhiteSpace(message.InReplyToMessageId))
        {
            mimeMessage.InReplyTo = message.InReplyToMessageId;
            mimeMessage.References.Add(message.InReplyToMessageId);
        }

        if (!string.IsNullOrWhiteSpace(message.ConversationId))
        {
            mimeMessage.Headers.Replace("X-CRM-Conversation-Id", message.ConversationId);
        }

        var builder = new BodyBuilder
        {
            HtmlBody = StampAccountGuid(message.HtmlBody, message.AccountGuidStamp, isHtml: true),
            TextBody = StampAccountGuid(message.TextBody, message.AccountGuidStamp, isHtml: false)
        };

        foreach (var attachment in message.Attachments)
        {
            if (attachment.Content is null)
            {
                continue;
            }

            builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        mimeMessage.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);

        var token = await _tokenProvider.GetAccessTokenAsync(SmtpScope, cancellationToken);
        var oauth2 = new SaslMechanismOAuth2(message.From, token);
        await client.AuthenticateAsync(oauth2, cancellationToken);

        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static string ResolveConversationId(string? referencesHeader, string messageId)
    {
        if (!string.IsNullOrWhiteSpace(referencesHeader))
        {
            var references = referencesHeader.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (references.Length > 0)
            {
                return references[0];
            }
        }

        return messageId;
    }

    private static List<EmailParticipant> BuildParticipants(Envelope envelope)
    {
        var participants = new List<EmailParticipant>();

        participants.AddRange(ToParticipants(envelope.From, "From"));
        participants.AddRange(ToParticipants(envelope.To, "To"));
        participants.AddRange(ToParticipants(envelope.Cc, "Cc"));
        participants.AddRange(ToParticipants(envelope.Bcc, "Bcc"));

        return participants;
    }

    private static IEnumerable<EmailParticipant> ToParticipants(InternetAddressList? list, string participantType)
    {
        if (list is null)
        {
            yield break;
        }

        foreach (var address in list.Mailboxes)
        {
            yield return new EmailParticipant
            {
                Address = address.Address,
                DisplayName = address.Name,
                ParticipantType = participantType
            };
        }
    }

    private static List<EmailAttachment> ExtractAttachments(MimeMessage message)
    {
        var attachments = new List<EmailAttachment>();

        foreach (var attachment in message.Attachments)
        {
            if (attachment is MimePart part)
            {
                using var stream = new MemoryStream();
                part.Content.DecodeTo(stream);

                attachments.Add(new EmailAttachment
                {
                    AttachmentId = part.ContentId ?? Guid.NewGuid().ToString("N"),
                    FileName = part.FileName ?? "attachment",
                    ContentType = part.ContentType.MimeType,
                    SizeBytes = stream.Length,
                    Content = stream.ToArray()
                });
            }
        }

        return attachments;
    }

    private static string? StampAccountGuid(string? body, Guid? accountGuid, bool isHtml)
    {
        if (accountGuid is null)
        {
            return body;
        }

        var stamp = $"[[CRM-ACCOUNT:{accountGuid}]]";
        if (string.IsNullOrWhiteSpace(body))
        {
            return stamp;
        }

        return isHtml
            ? $"{body}\n<!-- {stamp} -->"
            : $"{body}\n{stamp}";
    }
}
