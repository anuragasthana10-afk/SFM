using CRM.Core.Models;
using CRM.Core.Storage;
using CRM.Email.Abstractions;

namespace CRM.Email.Services;

public sealed class EmailSyncService
{
    private readonly IEmailProvider _provider;
    private readonly IEmailThreadStore _threadStore;
    private readonly EmailStorageOptions _storageOptions;

    public EmailSyncService(
        IEmailProvider provider,
        IEmailThreadStore threadStore,
        EmailStorageOptions storageOptions)
    {
        _provider = provider;
        _threadStore = threadStore;
        _storageOptions = storageOptions;
    }

    public async Task<IReadOnlyList<EmailMessageMetadata>> SyncMailboxAsync(
        EmailSyncRequest request,
        CancellationToken cancellationToken)
    {
        var messages = await _provider.SyncAsync(request, cancellationToken);
        await _threadStore.UpsertMessagesAsync(messages, cancellationToken);

        if (_storageOptions.StoreAttachments)
        {
            foreach (var message in messages)
            {
                var content = await _provider.FetchContentAsync(message.MessageId, cancellationToken);
                if (content is null)
                {
                    continue;
                }

                if (_storageOptions.MaxAttachmentSizeMb is { } maxSize)
                {
                    var maxSizeBytes = maxSize * 1024L * 1024L;
                    content.Attachments = content.Attachments
                        .Where(att => att.SizeBytes <= maxSizeBytes)
                        .ToList();
                }

                if (_storageOptions.AllowedAttachmentTypes.Count > 0)
                {
                    content.Attachments = content.Attachments
                        .Where(att => _storageOptions.AllowedAttachmentTypes.Contains(att.ContentType))
                        .ToList();
                }

                await _threadStore.SaveContentAsync(content, cancellationToken);
            }
        }

        return messages;
    }
}
