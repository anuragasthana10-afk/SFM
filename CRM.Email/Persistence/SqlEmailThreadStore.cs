using CRM.Models;
using CRM.Email.Abstractions;
using Microsoft.Data.SqlClient;

namespace CRM.Email.Persistence;

public sealed class SqlEmailThreadStore : IEmailThreadStore, IEmailThreadQuery
{
    private readonly IEmailExecutionContextAccessor _context;

    public SqlEmailThreadStore(IEmailExecutionContextAccessor context)
    {
        _context = context;
    }

    public async Task UpsertThreadAsync(EmailThread thread, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
MERGE dbo.Messaging_EmailThreads AS target
USING (SELECT @ConversationId AS ConversationId) AS source
ON target.ConversationId = source.ConversationId
WHEN MATCHED THEN
    UPDATE SET AccountId = @AccountId, LastUpdatedAt = @LastUpdatedAt
WHEN NOT MATCHED THEN
    INSERT (ConversationId, AccountId, LastUpdatedAt) VALUES (@ConversationId, @AccountId, @LastUpdatedAt);
", connection);

        command.Parameters.AddWithValue("@ConversationId", thread.ConversationId);
        command.Parameters.AddWithValue("@AccountId", thread.AccountId);
        command.Parameters.AddWithValue("@LastUpdatedAt", thread.LastUpdatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);

        if (thread.MessageIds.Count > 0)
        {
            var deleteCommand = new SqlCommand("DELETE FROM dbo.Messaging_EmailThreadMessages WHERE ConversationId = @ConversationId", connection);
            deleteCommand.Parameters.AddWithValue("@ConversationId", thread.ConversationId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

            foreach (var messageId in thread.MessageIds)
            {
                var insertCommand = new SqlCommand(
                    "INSERT INTO dbo.Messaging_EmailThreadMessages (ConversationId, MessageId) VALUES (@ConversationId, @MessageId)",
                    connection);
                insertCommand.Parameters.AddWithValue("@ConversationId", thread.ConversationId);
                insertCommand.Parameters.AddWithValue("@MessageId", messageId);
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public async Task UpsertMessagesAsync(IEnumerable<EmailMessageMetadata> messages, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        foreach (var message in messages)
        {
            var command = new SqlCommand(@"
MERGE dbo.Messaging_EmailMessageMetadata AS target
USING (SELECT @MessageId AS MessageId) AS source
ON target.MessageId = source.MessageId
WHEN MATCHED THEN
    UPDATE SET ConversationId = @ConversationId,
               Subject = @Subject,
               Snippet = @Snippet,
               ReceivedAt = @ReceivedAt,
               AccountId = CASE
                    WHEN target.AccountAssociationSource IN ('ManualMessage', 'ManualThread') THEN target.AccountId
                    WHEN @AccountId IS NOT NULL THEN @AccountId
                    ELSE target.AccountId
               END,
               AccountAssociationSource = CASE
                    WHEN target.AccountAssociationSource IN ('ManualMessage', 'ManualThread') THEN target.AccountAssociationSource
                    WHEN @AccountId IS NOT NULL THEN 'StampHint'
                    ELSE target.AccountAssociationSource
               END,
               HasAttachments = @HasAttachments
WHEN NOT MATCHED THEN
    INSERT (MessageId, ConversationId, Subject, Snippet, ReceivedAt, AccountId, AccountAssociationSource, HasAttachments)
    VALUES (@MessageId, @ConversationId, @Subject, @Snippet, @ReceivedAt, @AccountId,
            CASE WHEN @AccountId IS NOT NULL THEN 'StampHint' ELSE 'Unknown' END,
            @HasAttachments);
", connection);

            command.Parameters.AddWithValue("@MessageId", message.MessageId);
            command.Parameters.AddWithValue("@ConversationId", message.ConversationId);
            command.Parameters.AddWithValue("@Subject", message.Subject);
            command.Parameters.AddWithValue("@Snippet", (object?)message.Snippet ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceivedAt", message.ReceivedAt);
            command.Parameters.AddWithValue("@AccountId", (object?)message.AccountId ?? DBNull.Value);
            command.Parameters.AddWithValue("@HasAttachments", message.HasAttachments);
            await command.ExecuteNonQueryAsync(cancellationToken);

            var deleteParticipants = new SqlCommand("DELETE FROM dbo.Messaging_EmailParticipants WHERE MessageId = @MessageId", connection);
            deleteParticipants.Parameters.AddWithValue("@MessageId", message.MessageId);
            await deleteParticipants.ExecuteNonQueryAsync(cancellationToken);

            foreach (var participant in message.Participants)
            {
                var insertParticipant = new SqlCommand(
                    "INSERT INTO dbo.Messaging_EmailParticipants (MessageId, Address, DisplayName, ParticipantType) VALUES (@MessageId, @Address, @DisplayName, @ParticipantType)",
                    connection);
                insertParticipant.Parameters.AddWithValue("@MessageId", message.MessageId);
                insertParticipant.Parameters.AddWithValue("@Address", participant.Address);
                insertParticipant.Parameters.AddWithValue("@DisplayName", (object?)participant.DisplayName ?? DBNull.Value);
                insertParticipant.Parameters.AddWithValue("@ParticipantType", participant.ParticipantType);
                await insertParticipant.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public async Task<EmailContent?> GetContentAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand("SELECT HtmlBody, TextBody FROM dbo.Messaging_EmailContent WHERE MessageId = @MessageId", connection);
        command.Parameters.AddWithValue("@MessageId", messageId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var content = new EmailContent
        {
            MessageId = messageId,
            HtmlBody = reader.IsDBNull(0) ? null : reader.GetString(0),
            TextBody = reader.IsDBNull(1) ? null : reader.GetString(1)
        };

        await reader.CloseAsync();

        var attachmentCommand = new SqlCommand(
            "SELECT AttachmentId, FileName, ContentType, SizeBytes, Content FROM dbo.Messaging_EmailAttachments WHERE MessageId = @MessageId",
            connection);
        attachmentCommand.Parameters.AddWithValue("@MessageId", messageId);

        await using var attachmentReader = await attachmentCommand.ExecuteReaderAsync(cancellationToken);
        while (await attachmentReader.ReadAsync(cancellationToken))
        {
            content.Attachments.Add(new EmailAttachment
            {
                AttachmentId = attachmentReader.GetString(0),
                FileName = attachmentReader.GetString(1),
                ContentType = attachmentReader.GetString(2),
                SizeBytes = attachmentReader.GetInt64(3),
                Content = attachmentReader.IsDBNull(4) ? null : (byte[])attachmentReader[4]
            });
        }

        return content;
    }

    public async Task SaveContentAsync(EmailContent content, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
MERGE dbo.Messaging_EmailContent AS target
USING (SELECT @MessageId AS MessageId) AS source
ON target.MessageId = source.MessageId
WHEN MATCHED THEN
    UPDATE SET HtmlBody = @HtmlBody, TextBody = @TextBody
WHEN NOT MATCHED THEN
    INSERT (MessageId, HtmlBody, TextBody) VALUES (@MessageId, @HtmlBody, @TextBody);
", connection);

        command.Parameters.AddWithValue("@MessageId", content.MessageId);
        command.Parameters.AddWithValue("@HtmlBody", (object?)content.HtmlBody ?? DBNull.Value);
        command.Parameters.AddWithValue("@TextBody", (object?)content.TextBody ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var deleteAttachments = new SqlCommand("DELETE FROM dbo.Messaging_EmailAttachments WHERE MessageId = @MessageId", connection);
        deleteAttachments.Parameters.AddWithValue("@MessageId", content.MessageId);
        await deleteAttachments.ExecuteNonQueryAsync(cancellationToken);

        foreach (var attachment in content.Attachments)
        {
            var insertAttachment = new SqlCommand(@"
INSERT INTO dbo.Messaging_EmailAttachments (AttachmentId, MessageId, FileName, ContentType, SizeBytes, Content)
VALUES (@AttachmentId, @MessageId, @FileName, @ContentType, @SizeBytes, @Content);
", connection);

            insertAttachment.Parameters.AddWithValue("@AttachmentId", attachment.AttachmentId);
            insertAttachment.Parameters.AddWithValue("@MessageId", content.MessageId);
            insertAttachment.Parameters.AddWithValue("@FileName", attachment.FileName);
            insertAttachment.Parameters.AddWithValue("@ContentType", attachment.ContentType);
            insertAttachment.Parameters.AddWithValue("@SizeBytes", attachment.SizeBytes);
            insertAttachment.Parameters.AddWithValue("@Content", (object?)attachment.Content ?? DBNull.Value);
            await insertAttachment.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<EmailMessageMetadata>> GetMessagesByAccountAsync(int accountId, CancellationToken cancellationToken)
    {
        var results = new List<EmailMessageMetadata>();

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT MessageId, ConversationId, Subject, Snippet, ReceivedAt, AccountId, HasAttachments
FROM dbo.Messaging_EmailMessageMetadata
WHERE AccountId = @AccountId
ORDER BY ReceivedAt DESC", connection);
        command.Parameters.AddWithValue("@AccountId", accountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new EmailMessageMetadata
            {
                MessageId = reader.GetString(0),
                ConversationId = reader.GetString(1),
                Subject = reader.GetString(2),
                Snippet = reader.IsDBNull(3) ? null : reader.GetString(3),
                ReceivedAt = reader.GetDateTimeOffset(4),
                AccountId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                HasAttachments = reader.GetBoolean(6)
            });
        }

        foreach (var message in results)
        {
            message.Participants = await GetParticipantsAsync(message.MessageId, cancellationToken);
        }

        return results;
    }

    public async Task<IReadOnlyList<EmailThreadSummary>> GetThreadsByAccountAsync(int accountId, CancellationToken cancellationToken)
    {
        var results = new List<EmailThreadSummary>();

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT ConversationId,
       MAX(Subject) AS Subject,
       MAX(ReceivedAt) AS LastReceivedAt,
       COUNT(*) AS MessageCount,
       MAX(AccountId) AS AccountId
FROM dbo.Messaging_EmailMessageMetadata
WHERE AccountId = @AccountId
GROUP BY ConversationId
ORDER BY MAX(ReceivedAt) DESC", connection);
        command.Parameters.AddWithValue("@AccountId", accountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new EmailThreadSummary
            {
                ConversationId = reader.GetString(0),
                Subject = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                LastReceivedAt = reader.GetDateTimeOffset(2),
                MessageCount = reader.GetInt32(3),
                AccountId = reader.IsDBNull(4) ? null : reader.GetInt32(4)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<EmailMessageMetadata>> GetOrphanMessagesAsync(CancellationToken cancellationToken)
    {
        var results = new List<EmailMessageMetadata>();

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT MessageId, ConversationId, Subject, Snippet, ReceivedAt, AccountId, HasAttachments
FROM dbo.Messaging_EmailMessageMetadata
WHERE AccountId IS NULL
  AND AccountAssociationSource <> 'Discarded'
ORDER BY ReceivedAt DESC", connection);
        
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new EmailMessageMetadata
            {
                MessageId = reader.GetString(0),
                ConversationId = reader.GetString(1),
                Subject = reader.GetString(2),
                Snippet = reader.IsDBNull(3) ? null : reader.GetString(3),
                ReceivedAt = reader.GetDateTimeOffset(4),
                AccountId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                HasAttachments = reader.GetBoolean(6)
            });
        }

        foreach (var message in results)
        {
            message.Participants = await GetParticipantsAsync(message.MessageId, cancellationToken);
        }

        return results;
    }

    public async Task<IReadOnlyList<int>> GetAccountSuggestionsBySenderAsync(string senderAddress, CancellationToken cancellationToken)
    {
        var results = new List<int>();

        if (string.IsNullOrWhiteSpace(senderAddress))
        {
            return results;
        }

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT ca.AccountId
FROM dbo.Messaging_Contacts c
JOIN dbo.Messaging_ContactAccounts ca ON ca.ContactId = c.Id
WHERE LOWER(c.EmailAddress) = LOWER(@SenderAddress)
ORDER BY ca.AccountId", connection);
        command.Parameters.AddWithValue("@SenderAddress", senderAddress.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(reader.GetInt32(0));
        }

        return results;
    }


    public async Task<IReadOnlyList<EmailMessageMetadata>> GetDiscardedMessagesAsync(CancellationToken cancellationToken)
    {
        var results = new List<EmailMessageMetadata>();

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT MessageId, ConversationId, Subject, Snippet, ReceivedAt, AccountId, HasAttachments
FROM dbo.Messaging_EmailMessageMetadata
WHERE AccountId IS NULL
  AND AccountAssociationSource = 'Discarded'
ORDER BY ReceivedAt DESC", connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new EmailMessageMetadata
            {
                MessageId = reader.GetString(0),
                ConversationId = reader.GetString(1),
                Subject = reader.GetString(2),
                Snippet = reader.IsDBNull(3) ? null : reader.GetString(3),
                ReceivedAt = reader.GetDateTimeOffset(4),
                AccountId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                HasAttachments = reader.GetBoolean(6)
            });
        }

        foreach (var message in results)
        {
            message.Participants = await GetParticipantsAsync(message.MessageId, cancellationToken);
        }

        return results;
    }

    public async Task<EmailMessageMetadata?> GetMessageByIdAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT MessageId, ConversationId, Subject, Snippet, ReceivedAt, AccountId, HasAttachments
FROM dbo.Messaging_EmailMessageMetadata
WHERE MessageId = @MessageId", connection);
        command.Parameters.AddWithValue("@MessageId", messageId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var message = new EmailMessageMetadata
        {
            MessageId = reader.GetString(0),
            ConversationId = reader.GetString(1),
            Subject = reader.GetString(2),
            Snippet = reader.IsDBNull(3) ? null : reader.GetString(3),
            ReceivedAt = reader.GetDateTimeOffset(4),
            AccountId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            HasAttachments = reader.GetBoolean(6)
        };

        message.Participants = await GetParticipantsAsync(message.MessageId, cancellationToken);
        return message;
    }

    public async Task<IReadOnlyList<EmailMessageMetadata>> GetMessagesByConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        var results = new List<EmailMessageMetadata>();

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT MessageId, ConversationId, Subject, Snippet, ReceivedAt, AccountId, HasAttachments
FROM dbo.Messaging_EmailMessageMetadata
WHERE ConversationId = @ConversationId
ORDER BY ReceivedAt ASC", connection);
        command.Parameters.AddWithValue("@ConversationId", conversationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new EmailMessageMetadata
            {
                MessageId = reader.GetString(0),
                ConversationId = reader.GetString(1),
                Subject = reader.GetString(2),
                Snippet = reader.IsDBNull(3) ? null : reader.GetString(3),
                ReceivedAt = reader.GetDateTimeOffset(4),
                AccountId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                HasAttachments = reader.GetBoolean(6)
            });
        }

        foreach (var message in results)
        {
            message.Participants = await GetParticipantsAsync(message.MessageId, cancellationToken);
        }

        return results;
    }

    public async Task ReassignThreadAsync(string conversationId, int newAccountId, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var updateMessages = new SqlCommand(@"
UPDATE dbo.Messaging_EmailMessageMetadata
SET AccountId = @AccountId,
    AccountAssociationSource = 'ManualThread'
WHERE ConversationId = @ConversationId", connection);
        updateMessages.Parameters.AddWithValue("@AccountId", newAccountId);
        updateMessages.Parameters.AddWithValue("@ConversationId", conversationId);
        await updateMessages.ExecuteNonQueryAsync(cancellationToken);

        var upsertThread = new SqlCommand(@"
MERGE dbo.Messaging_EmailThreads AS target
USING (SELECT @ConversationId AS ConversationId) AS source
ON target.ConversationId = source.ConversationId
WHEN MATCHED THEN
    UPDATE SET AccountId = @AccountId, LastUpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (ConversationId, AccountId, LastUpdatedAt) VALUES (@ConversationId, @AccountId, SYSUTCDATETIME());", connection);
        upsertThread.Parameters.AddWithValue("@ConversationId", conversationId);
        upsertThread.Parameters.AddWithValue("@AccountId", newAccountId);
        await upsertThread.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetAccountForMessageAsync(string messageId, int accountId, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(
            "UPDATE dbo.Messaging_EmailMessageMetadata SET AccountId = @AccountId, AccountAssociationSource = 'ManualMessage' WHERE MessageId = @MessageId",
            connection);
        command.Parameters.AddWithValue("@AccountId", accountId);
        command.Parameters.AddWithValue("@MessageId", messageId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DiscardMessageAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(
            "UPDATE dbo.Messaging_EmailMessageMetadata SET AccountId = NULL, AccountAssociationSource = 'Discarded' WHERE MessageId = @MessageId",
            connection);
        command.Parameters.AddWithValue("@MessageId", messageId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RestoreDiscardedMessageAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(
            "UPDATE dbo.Messaging_EmailMessageMetadata SET AccountId = NULL, AccountAssociationSource = 'Unknown' WHERE MessageId = @MessageId AND AccountAssociationSource = 'Discarded'",
            connection);
        command.Parameters.AddWithValue("@MessageId", messageId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DisassociateMessageAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(
            "UPDATE dbo.Messaging_EmailMessageMetadata SET AccountId = NULL, AccountAssociationSource = 'Unknown' WHERE MessageId = @MessageId",
            connection);
        command.Parameters.AddWithValue("@MessageId", messageId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }


    public async Task<int> EnsureUserAsync(string username, string firstName, string lastName, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
MERGE dbo.CRM_Users AS target
USING (SELECT @Username AS Username) AS source
ON target.Username = source.Username
WHEN MATCHED THEN
    UPDATE SET FirstName = @FirstName, LastName = @LastName
WHEN NOT MATCHED THEN
    INSERT (Username, FirstName, LastName) VALUES (@Username, @FirstName, @LastName)
OUTPUT inserted.User_Id;", connection);
        command.Parameters.AddWithValue("@Username", username.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("@FirstName", firstName);
        command.Parameters.AddWithValue("@LastName", lastName);
        var userId = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(userId);
    }

    public async Task<IReadOnlySet<string>> GetReadMessageIdsAsync(int accountId, int userId, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT r.MessageId
FROM dbo.Messaging_EmailReadState r
JOIN dbo.Messaging_EmailMessageMetadata m ON m.MessageId = r.MessageId
WHERE r.User_Id = @UserId AND m.AccountId = @AccountId", connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@AccountId", accountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            set.Add(reader.GetString(0));
        }

        return set;
    }

    public async Task<IReadOnlySet<string>> GetReadConversationIdsAsync(int accountId, int userId, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT m.ConversationId
FROM dbo.Messaging_EmailMessageMetadata m
LEFT JOIN dbo.Messaging_EmailReadState r ON r.MessageId = m.MessageId AND r.User_Id = @UserId
WHERE m.AccountId = @AccountId
GROUP BY m.ConversationId
HAVING COUNT(*) = COUNT(r.MessageId)", connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@AccountId", accountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            set.Add(reader.GetString(0));
        }

        return set;
    }

    public async Task MarkMessageOpenedAsync(string messageId, int userId, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
MERGE dbo.Messaging_EmailReadState AS target
USING (SELECT @MessageId AS MessageId, @UserId AS User_Id) AS source
ON target.MessageId = source.MessageId AND target.User_Id = source.User_Id
WHEN MATCHED THEN
    UPDATE SET OpenedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (MessageId, User_Id, OpenedAt) VALUES (@MessageId, @UserId, SYSUTCDATETIME());", connection);
        command.Parameters.AddWithValue("@MessageId", messageId);
        command.Parameters.AddWithValue("@UserId", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordAuditAsync(int userId, string username, string actionType, string? messageId, string? conversationId, int? oldAccountId, int? newAccountId, string? details, CancellationToken cancellationToken)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
INSERT INTO dbo.Messaging_UserAuditTrail
    (OccurredAt, User_Id, Username, ActionType, MessageId, ConversationId, OldAccountId, NewAccountId, Details)
VALUES
    (SYSUTCDATETIME(), @UserId, @Username, @ActionType, @MessageId, @ConversationId, @OldAccountId, @NewAccountId, @Details);", connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@ActionType", actionType);
        command.Parameters.AddWithValue("@MessageId", (object?)messageId ?? DBNull.Value);
        command.Parameters.AddWithValue("@ConversationId", (object?)conversationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@OldAccountId", (object?)oldAccountId ?? DBNull.Value);
        command.Parameters.AddWithValue("@NewAccountId", (object?)newAccountId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Details", (object?)details ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<List<EmailParticipant>> GetParticipantsAsync(
        string messageId,
        CancellationToken cancellationToken)
    {
        var participants = new List<EmailParticipant>();

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT Address, DisplayName, ParticipantType
FROM dbo.Messaging_EmailParticipants
WHERE MessageId = @MessageId", connection);
        command.Parameters.AddWithValue("@MessageId", messageId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            participants.Add(new EmailParticipant
            {
                Address = reader.GetString(0),
                DisplayName = reader.IsDBNull(1) ? null : reader.GetString(1),
                ParticipantType = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2)
            });
        }

        return participants;
    }
}
