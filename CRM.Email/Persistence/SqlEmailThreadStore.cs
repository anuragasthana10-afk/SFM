using CRM.Core.Models;
using CRM.Email.Abstractions;
using Microsoft.Data.SqlClient;

namespace CRM.Email.Persistence;

public sealed class SqlEmailThreadStore : IEmailThreadStore, IEmailThreadQuery
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlEmailThreadStore(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task UpsertThreadAsync(EmailThread thread, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
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
        await using var connection = _connectionFactory.CreateConnection();
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
               AccountId = @AccountId,
               HasAttachments = @HasAttachments
WHEN NOT MATCHED THEN
    INSERT (MessageId, ConversationId, Subject, Snippet, ReceivedAt, AccountId, HasAttachments)
    VALUES (@MessageId, @ConversationId, @Subject, @Snippet, @ReceivedAt, @AccountId, @HasAttachments);
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
                    "INSERT INTO dbo.Messaging_EmailParticipants (MessageId, Address, DisplayName) VALUES (@MessageId, @Address, @DisplayName)",
                    connection);
                insertParticipant.Parameters.AddWithValue("@MessageId", message.MessageId);
                insertParticipant.Parameters.AddWithValue("@Address", participant.Address);
                insertParticipant.Parameters.AddWithValue("@DisplayName", (object?)participant.DisplayName ?? DBNull.Value);
                await insertParticipant.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public async Task<EmailContent?> GetContentAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
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
        await using var connection = _connectionFactory.CreateConnection();
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

        await using var connection = _connectionFactory.CreateConnection();
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
            message.Participants = await GetParticipantsAsync(connection, message.MessageId, cancellationToken);
        }

        return results;
    }

    public async Task<IReadOnlyList<EmailThreadSummary>> GetThreadsByAccountAsync(int accountId, CancellationToken cancellationToken)
    {
        var results = new List<EmailThreadSummary>();

        await using var connection = _connectionFactory.CreateConnection();
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

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(@"
SELECT MessageId, ConversationId, Subject, Snippet, ReceivedAt, AccountId, HasAttachments
FROM dbo.Messaging_EmailMessageMetadata
WHERE AccountId IS NULL
ORDER BY ReceivedAt DESC", connection);
        command.Parameters.AddWithValue("@AccountId", DBNull.Value);

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
            message.Participants = await GetParticipantsAsync(connection, message.MessageId, cancellationToken);
        }

        return results;
    }

    public async Task ReassignThreadAsync(string conversationId, int newAccountId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var updateMessages = new SqlCommand(@"
UPDATE dbo.Messaging_EmailMessageMetadata
SET AccountId = @AccountId
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
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = new SqlCommand(
            "UPDATE dbo.Messaging_EmailMessageMetadata SET AccountId = @AccountId WHERE MessageId = @MessageId",
            connection);
        command.Parameters.AddWithValue("@AccountId", accountId);
        command.Parameters.AddWithValue("@MessageId", messageId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<EmailParticipant>> GetParticipantsAsync(
        SqlConnection connection,
        string messageId,
        CancellationToken cancellationToken)
    {
        var participants = new List<EmailParticipant>();
        var command = new SqlCommand(@"
SELECT Address, DisplayName
FROM dbo.Messaging_EmailParticipants
WHERE MessageId = @MessageId", connection);
        command.Parameters.AddWithValue("@MessageId", messageId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            participants.Add(new EmailParticipant
            {
                Address = reader.GetString(0),
                DisplayName = reader.IsDBNull(1) ? null : reader.GetString(1)
            });
        }

        return participants;
    }
}
