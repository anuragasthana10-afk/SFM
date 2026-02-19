/*
    Workflow explicit transition-entity migration
    --------------------------------------------
    This script creates Workflow_StepTransitions and backfills transition rows
    from legacy Workflow_Steps_NextStep_ID / Workflow_Steps_PreviousStep_ID links.

    Notes:
    - Keep legacy columns during migration for rollback safety.
    - Application code dual-reads: transition table first, legacy columns fallback.
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Workflow_StepTransitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Workflow_StepTransitions
    (
        ID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Workflow_StepTransitions PRIMARY KEY,
        Workflows_ID SMALLINT NOT NULL,
        From_Workflow_Steps_ID SMALLINT NOT NULL,
        To_Workflow_Steps_ID SMALLINT NOT NULL,
        ConditionExpression NVARCHAR(1000) NULL,
        DisplayLabel NVARCHAR(150) NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_Workflow_StepTransitions_SortOrder DEFAULT (0),
        IsDefaultPath BIT NULL,
        IsActive BIT NULL CONSTRAINT DF_Workflow_StepTransitions_IsActive DEFAULT (1),
        CreateUserID INT NOT NULL,
        CreateDate DATETIME2(7) NOT NULL CONSTRAINT DF_Workflow_StepTransitions_CreateDate DEFAULT (SYSUTCDATETIME()),
        ModifyUserID INT NULL,
        ModifyDate DATETIME2(7) NULL,
        DelFlag BIT NULL CONSTRAINT DF_Workflow_StepTransitions_DelFlag DEFAULT (0)
    );

    ALTER TABLE dbo.Workflow_StepTransitions WITH CHECK
        ADD CONSTRAINT FK_Workflow_StepTransitions_Workflows
            FOREIGN KEY (Workflows_ID) REFERENCES dbo.Workflows(ID);

    ALTER TABLE dbo.Workflow_StepTransitions WITH CHECK
        ADD CONSTRAINT FK_Workflow_StepTransitions_FromStep
            FOREIGN KEY (From_Workflow_Steps_ID) REFERENCES dbo.Workflow_Steps(ID);

    ALTER TABLE dbo.Workflow_StepTransitions WITH CHECK
        ADD CONSTRAINT FK_Workflow_StepTransitions_ToStep
            FOREIGN KEY (To_Workflow_Steps_ID) REFERENCES dbo.Workflow_Steps(ID);

    CREATE INDEX IX_Workflow_StepTransitions_Workflows_From
        ON dbo.Workflow_StepTransitions (Workflows_ID, From_Workflow_Steps_ID)
        INCLUDE (To_Workflow_Steps_ID, IsActive, DelFlag, SortOrder);

    CREATE INDEX IX_Workflow_StepTransitions_ToStep
        ON dbo.Workflow_StepTransitions (To_Workflow_Steps_ID)
        INCLUDE (From_Workflow_Steps_ID, Workflows_ID, IsActive, DelFlag);
END;
GO

;WITH LegacyLinks AS
(
    SELECT
        ws.Workflows_ID,
        ws.ID AS FromStepID,
        ws.Workflow_Steps_NextStep_ID AS ToStepID,
        ws.CreateUserID,
        ws.CreateDate,
        CAST(1 AS INT) AS SortOrder
    FROM dbo.Workflow_Steps ws
    WHERE ws.Workflow_Steps_NextStep_ID IS NOT NULL
      AND (ws.DelFlag IS NULL OR ws.DelFlag = 0)

    UNION

    SELECT
        parent.Workflows_ID,
        parent.ID AS FromStepID,
        child.ID AS ToStepID,
        parent.CreateUserID,
        parent.CreateDate,
        CAST(2 AS INT) AS SortOrder
    FROM dbo.Workflow_Steps child
    INNER JOIN dbo.Workflow_Steps parent
        ON child.Workflow_Steps_PreviousStep_ID = parent.ID
    WHERE child.Workflow_Steps_PreviousStep_ID IS NOT NULL
      AND (child.DelFlag IS NULL OR child.DelFlag = 0)
      AND (parent.DelFlag IS NULL OR parent.DelFlag = 0)
),
DedupedLegacyLinks AS
(
    SELECT
        Workflows_ID,
        FromStepID,
        ToStepID,
        MIN(CreateUserID) AS CreateUserID,
        MIN(CreateDate) AS CreateDate,
        MIN(SortOrder) AS SortOrder
    FROM LegacyLinks
    GROUP BY Workflows_ID, FromStepID, ToStepID
)
INSERT INTO dbo.Workflow_StepTransitions
(
    Workflows_ID,
    From_Workflow_Steps_ID,
    To_Workflow_Steps_ID,
    SortOrder,
    IsDefaultPath,
    IsActive,
    CreateUserID,
    CreateDate,
    DelFlag
)
SELECT
    l.Workflows_ID,
    l.FromStepID,
    l.ToStepID,
    l.SortOrder,
    CASE WHEN l.SortOrder = 1 THEN 1 ELSE 0 END AS IsDefaultPath,
    1 AS IsActive,
    ISNULL(l.CreateUserID, 0) AS CreateUserID,
    ISNULL(l.CreateDate, SYSUTCDATETIME()) AS CreateDate,
    0 AS DelFlag
FROM DedupedLegacyLinks l
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Workflow_StepTransitions t
    WHERE t.Workflows_ID = l.Workflows_ID
      AND t.From_Workflow_Steps_ID = l.FromStepID
      AND t.To_Workflow_Steps_ID = l.ToStepID
      AND (t.DelFlag IS NULL OR t.DelFlag = 0)
);
GO

/*
Validation examples (optional):

SELECT Workflows_ID, COUNT(*) AS TransitionCount
FROM dbo.Workflow_StepTransitions
WHERE (DelFlag IS NULL OR DelFlag = 0)
GROUP BY Workflows_ID;

SELECT TOP 100 *
FROM dbo.Workflow_StepTransitions
ORDER BY Workflows_ID, From_Workflow_Steps_ID, SortOrder, ID;
*/
