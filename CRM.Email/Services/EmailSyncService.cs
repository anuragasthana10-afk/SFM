using System.Text.RegularExpressions;
using CRM.Core.Models;
using CRM.Core.Storage;
using CRM.Email.Abstractions;

namespace CRM.Email.Services;

public sealed class EmailSyncService
{
    private static readonly Regex AccountStampRegex = new(@"\[\[CRM-ACCOUNT:(?<guid>[0-9a-fA-F-]{36})\]\]", RegexOptions.Compiled);

    private readonly IEmailProvider _provider;
    private readonly IEmailThreadStore _threadStore;
    private readonly IAccountStore _accountStore;
    private readonly EmailStorageOptions _storageOptions;

    public EmailSyncService(
        IEmailProvider provider,
        IEmailThreadStore threadStore,
        IAccountStore accountStore,
        EmailStorageOptions storageOptions)
    {
        _provider = provider;
        _threadStore = threadStore;
        _accountStore = accountStore;
        _storageOptions = storageOptions;
    }

    public async Task<IReadOnlyList<EmailMessageMetadata>> SyncMailboxAsync(
        EmailSyncRequest request,
        CancellationToken cancellationToken)
    {
        var messages = (await _provider.SyncAsync(request, cancellationToken)).ToList();
        var accounts = await _accountStore.GetAllAsync(cancellationToken);
        var accountByGuid = accounts.ToDictionary(a => a.AccountGuid, a => a.Id);

        foreach (var message in messages)
        {
            var content = await _provider.FetchContentAsync(message.MessageId, cancellationToken);
            if (content is null)
            {
                continue;
            }

            var hintedAccountId = ResolveHintedAccountId(content, accountByGuid);
            if (hintedAccountId.HasValue)
            {
                message.AccountId = hintedAccountId;
            }

            if (_storageOptions.StoreFullMessages)
            {
                if (_storageOptions.StoreAttachments && _storageOptions.MaxAttachmentSizeMb is { } maxSize)
                {
                    var maxSizeBytes = maxSize * 1024L * 1024L;
                    content.Attachments = content.Attachments
                        .Where(att => att.SizeBytes <= maxSizeBytes)
                        .ToList();
                }

                if (_storageOptions.StoreAttachments && _storageOptions.AllowedAttachmentTypes.Count > 0)
                {
                    content.Attachments = content.Attachments
                        .Where(att => _storageOptions.AllowedAttachmentTypes.Contains(att.ContentType))
                        .ToList();
                }

                if (!_storageOptions.StoreAttachments)
                {
                    content.Attachments = new List<EmailAttachment>();
                }

                await _threadStore.SaveContentAsync(content, cancellationToken);
            }
        }

        await _threadStore.UpsertMessagesAsync(messages, cancellationToken);
        return messages;
    }

    private static int? ResolveHintedAccountId(EmailContent content, IReadOnlyDictionary<Guid, int> accountByGuid)
    {
        var stampGuid = ExtractGuidStamp(content.TextBody) ?? ExtractGuidStamp(content.HtmlBody);
        if (stampGuid.HasValue && accountByGuid.TryGetValue(stampGuid.Value, out var accountId))
        {
            return accountId;
        }

        return null;
    }

    private static Guid? ExtractGuidStamp(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var match = AccountStampRegex.Match(body);
        if (!match.Success)
        {
            return null;
        }

        return Guid.TryParse(match.Groups["guid"].Value, out var parsed) ? parsed : null;
    }
}
