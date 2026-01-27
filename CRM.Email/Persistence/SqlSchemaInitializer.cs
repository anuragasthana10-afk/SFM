using Microsoft.Data.SqlClient;

namespace CRM.Email.Persistence;

public sealed class SqlSchemaInitializer
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlSchemaInitializer(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
IF OBJECT_ID('dbo.Accounts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Accounts (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        AccountGuid UNIQUEIDENTIFIER NOT NULL
    );
END;

IF OBJECT_ID('dbo.EmailMessageMetadata', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailMessageMetadata (
        MessageId NVARCHAR(200) NOT NULL PRIMARY KEY,
        ConversationId NVARCHAR(200) NOT NULL,
        Subject NVARCHAR(500) NOT NULL,
        Snippet NVARCHAR(500) NULL,
        ReceivedAt DATETIMEOFFSET NOT NULL,
        AccountId INT NULL,
        HasAttachments BIT NOT NULL
    );
END;

IF OBJECT_ID('dbo.EmailParticipants', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailParticipants (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MessageId NVARCHAR(200) NOT NULL,
        Address NVARCHAR(320) NOT NULL,
        DisplayName NVARCHAR(200) NULL
    );
END;

IF OBJECT_ID('dbo.EmailContent', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailContent (
        MessageId NVARCHAR(200) NOT NULL PRIMARY KEY,
        HtmlBody NVARCHAR(MAX) NULL,
        TextBody NVARCHAR(MAX) NULL
    );
END;

IF OBJECT_ID('dbo.EmailAttachments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailAttachments (
        AttachmentId NVARCHAR(200) NOT NULL PRIMARY KEY,
        MessageId NVARCHAR(200) NOT NULL,
        FileName NVARCHAR(400) NOT NULL,
        ContentType NVARCHAR(200) NOT NULL,
        SizeBytes BIGINT NOT NULL,
        Content VARBINARY(MAX) NULL
    );
END;

IF OBJECT_ID('dbo.EmailThreads', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailThreads (
        ConversationId NVARCHAR(200) NOT NULL PRIMARY KEY,
        AccountId INT NOT NULL,
        LastUpdatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID('dbo.EmailThreadMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailThreadMessages (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ConversationId NVARCHAR(200) NOT NULL,
        MessageId NVARCHAR(200) NOT NULL
    );
END;
";

        await command.ExecuteNonQueryAsync(cancellationToken);

        var seedCommand = connection.CreateCommand();
        seedCommand.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM dbo.Accounts)
BEGIN
    INSERT INTO dbo.Accounts (Name, AccountGuid)
    VALUES ('Contoso Ltd', NEWID()), ('Fabrikam Inc', NEWID());
END;
";
        await seedCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
