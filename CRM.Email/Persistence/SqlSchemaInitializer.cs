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
IF OBJECT_ID('dbo.Messaging_Accounts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messaging_Accounts (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        AccountGuid UNIQUEIDENTIFIER NOT NULL
    );
END;

IF OBJECT_ID('dbo.Messaging_EmailMessageMetadata', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messaging_EmailMessageMetadata (
        MessageId NVARCHAR(200) NOT NULL PRIMARY KEY,
        ConversationId NVARCHAR(200) NOT NULL,
        Subject NVARCHAR(500) NOT NULL,
        Snippet NVARCHAR(500) NULL,
        ReceivedAt DATETIMEOFFSET NOT NULL,
        AccountId INT NULL,
        AccountAssociationSource NVARCHAR(30) NOT NULL DEFAULT('Unknown'),
        HasAttachments BIT NOT NULL
    );
END;

IF COL_LENGTH('dbo.Messaging_EmailMessageMetadata', 'AccountAssociationSource') IS NULL
BEGIN
    ALTER TABLE dbo.Messaging_EmailMessageMetadata
    ADD AccountAssociationSource NVARCHAR(30) NOT NULL CONSTRAINT DF_Messaging_EmailMessageMetadata_AccountAssociationSource DEFAULT('Unknown');
END;

IF OBJECT_ID('dbo.Messaging_EmailParticipants', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messaging_EmailParticipants (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MessageId NVARCHAR(200) NOT NULL,
        Address NVARCHAR(320) NOT NULL,
        DisplayName NVARCHAR(200) NULL,
        ParticipantType NVARCHAR(20) NOT NULL CONSTRAINT DF_Messaging_EmailParticipants_ParticipantType DEFAULT('Unknown')
    );
END;

IF OBJECT_ID('dbo.Messaging_Contacts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messaging_Contacts (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        EmailAddress NVARCHAR(320) NOT NULL UNIQUE
    );
END;

IF OBJECT_ID('dbo.Messaging_ContactAccounts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messaging_ContactAccounts (
        ContactId INT NOT NULL,
        AccountId INT NOT NULL,
        CONSTRAINT PK_Messaging_ContactAccounts PRIMARY KEY (ContactId, AccountId)
    );
END;


IF COL_LENGTH('dbo.Messaging_EmailParticipants', 'ParticipantType') IS NULL
BEGIN
    ALTER TABLE dbo.Messaging_EmailParticipants
    ADD ParticipantType NVARCHAR(20) NOT NULL CONSTRAINT DF_Messaging_EmailParticipants_ParticipantType DEFAULT('Unknown');
END;

IF OBJECT_ID('dbo.Messaging_EmailContent', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messaging_EmailContent (
        MessageId NVARCHAR(200) NOT NULL PRIMARY KEY,
        HtmlBody NVARCHAR(MAX) NULL,
        TextBody NVARCHAR(MAX) NULL
    );
END;

IF OBJECT_ID('dbo.Messaging_EmailAttachments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messaging_EmailAttachments (
        AttachmentId NVARCHAR(200) NOT NULL PRIMARY KEY,
        MessageId NVARCHAR(200) NOT NULL,
        FileName NVARCHAR(400) NOT NULL,
        ContentType NVARCHAR(200) NOT NULL,
        SizeBytes BIGINT NOT NULL,
        Content VARBINARY(MAX) NULL
    );
END;

IF OBJECT_ID('dbo.Messaging_EmailThreads', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messaging_EmailThreads (
        ConversationId NVARCHAR(200) NOT NULL PRIMARY KEY,
        AccountId INT NOT NULL,
        LastUpdatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID('dbo.Messaging_EmailThreadMessages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Messaging_EmailThreadMessages (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ConversationId NVARCHAR(200) NOT NULL,
        MessageId NVARCHAR(200) NOT NULL
    );
END;
";

        await command.ExecuteNonQueryAsync(cancellationToken);

        var seedCommand = connection.CreateCommand();
        seedCommand.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM dbo.Messaging_Accounts)
BEGIN
    INSERT INTO dbo.Messaging_Accounts (Name, AccountGuid)
    VALUES ('Contoso Ltd', NEWID()), ('Fabrikam Inc', NEWID()), ('Northwind Traders', NEWID());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Messaging_Contacts)
BEGIN
    INSERT INTO dbo.Messaging_Contacts (Name, EmailAddress)
    VALUES
        ('Alex Johnson', 'alex.johnson@contoso-example.com'),
        ('Priya Patel', 'priya.patel@fabrikam-example.com'),
        ('Morgan Lee', 'morgan.lee@shared-client.com');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Messaging_ContactAccounts)
BEGIN
    INSERT INTO dbo.Messaging_ContactAccounts (ContactId, AccountId)
    SELECT c.Id, a.Id
    FROM dbo.Messaging_Contacts c
    JOIN dbo.Messaging_Accounts a ON a.Name IN ('Contoso Ltd', 'Northwind Traders')
    WHERE c.EmailAddress = 'morgan.lee@shared-client.com';

    INSERT INTO dbo.Messaging_ContactAccounts (ContactId, AccountId)
    SELECT c.Id, a.Id
    FROM dbo.Messaging_Contacts c
    JOIN dbo.Messaging_Accounts a ON a.Name = 'Contoso Ltd'
    WHERE c.EmailAddress = 'alex.johnson@contoso-example.com';

    INSERT INTO dbo.Messaging_ContactAccounts (ContactId, AccountId)
    SELECT c.Id, a.Id
    FROM dbo.Messaging_Contacts c
    JOIN dbo.Messaging_Accounts a ON a.Name = 'Fabrikam Inc'
    WHERE c.EmailAddress = 'priya.patel@fabrikam-example.com';
END;


IF NOT EXISTS (SELECT 1 FROM dbo.Messaging_EmailMessageMetadata WHERE MessageId = 'demo-orphan-001')
BEGIN
    INSERT INTO dbo.Messaging_EmailMessageMetadata
        (MessageId, ConversationId, Subject, Snippet, ReceivedAt, AccountId, AccountAssociationSource, HasAttachments)
    VALUES
        ('demo-orphan-001', 'demo-orphan-conv-001', 'Need quote for shared-client service', 'Hello, please send service proposal.', SYSUTCDATETIME(), NULL, 'Unknown', 0);

    INSERT INTO dbo.Messaging_EmailParticipants (MessageId, Address, DisplayName, ParticipantType)
    VALUES
        ('demo-orphan-001', 'morgan.lee@shared-client.com', 'Morgan Lee', 'From'),
        ('demo-orphan-001', 'service@crm-demo.local', 'CRM Service Mailbox', 'To');
END;

";
        await seedCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
