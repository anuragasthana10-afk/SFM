/*
    Workflow template designer persistence and audit tables
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Workflow_StepDesignerLayouts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Workflow_StepDesignerLayouts
    (
        ID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Workflow_StepDesignerLayouts PRIMARY KEY,
        Workflows_ID SMALLINT NOT NULL,
        Workflow_Steps_ID SMALLINT NOT NULL,
        PositionX FLOAT NOT NULL,
        PositionY FLOAT NOT NULL,
        LastClientSyncDate DATETIME2(7) NULL,
        CreateUserID INT NOT NULL,
        CreateDate DATETIME2(7) NOT NULL CONSTRAINT DF_Workflow_StepDesignerLayouts_CreateDate DEFAULT (SYSUTCDATETIME()),
        ModifyUserID INT NULL,
        ModifyDate DATETIME2(7) NULL,
        DelFlag BIT NOT NULL CONSTRAINT DF_Workflow_StepDesignerLayouts_DelFlag DEFAULT (0)
    );

    ALTER TABLE dbo.Workflow_StepDesignerLayouts WITH CHECK
        ADD CONSTRAINT FK_Workflow_StepDesignerLayouts_Workflows
            FOREIGN KEY (Workflows_ID) REFERENCES dbo.Workflows(ID);

    ALTER TABLE dbo.Workflow_StepDesignerLayouts WITH CHECK
        ADD CONSTRAINT FK_Workflow_StepDesignerLayouts_Steps
            FOREIGN KEY (Workflow_Steps_ID) REFERENCES dbo.Workflow_Steps(ID);

    CREATE UNIQUE INDEX UX_Workflow_StepDesignerLayouts_WorkflowStep
        ON dbo.Workflow_StepDesignerLayouts(Workflows_ID, Workflow_Steps_ID)
        WHERE DelFlag = 0;
END;
GO

IF OBJECT_ID(N'dbo.Workflow_TemplateAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Workflow_TemplateAudit
    (
        ID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Workflow_TemplateAudit PRIMARY KEY,
        Workflows_ID SMALLINT NOT NULL,
        ActionName NVARCHAR(100) NOT NULL,
        PayloadJson NVARCHAR(MAX) NULL,
        CreateUserID INT NOT NULL,
        CreateDate DATETIME2(7) NOT NULL CONSTRAINT DF_Workflow_TemplateAudit_CreateDate DEFAULT (SYSUTCDATETIME())
    );

    ALTER TABLE dbo.Workflow_TemplateAudit WITH CHECK
        ADD CONSTRAINT FK_Workflow_TemplateAudit_Workflows
            FOREIGN KEY (Workflows_ID) REFERENCES dbo.Workflows(ID);

    CREATE INDEX IX_Workflow_TemplateAudit_WorkflowDate
        ON dbo.Workflow_TemplateAudit(Workflows_ID, CreateDate DESC);
END;
GO
