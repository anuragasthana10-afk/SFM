/*
    Drop legacy step-link columns now that Workflow_StepTransitions is the
    single source of truth for step transitions.
*/

SET NOCOUNT ON;

IF COL_LENGTH('dbo.Workflow_Steps', 'Workflow_Steps_NextStep_ID') IS NOT NULL
BEGIN
    DECLARE @dfNext sysname;
    SELECT @dfNext = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = 'Workflow_Steps' AND c.name = 'Workflow_Steps_NextStep_ID';

    IF @dfNext IS NOT NULL
        EXEC('ALTER TABLE dbo.Workflow_Steps DROP CONSTRAINT ' + QUOTENAME(@dfNext));

    ALTER TABLE dbo.Workflow_Steps DROP COLUMN Workflow_Steps_NextStep_ID;
END;
GO

IF COL_LENGTH('dbo.Workflow_Steps', 'Workflow_Steps_PreviousStep_ID') IS NOT NULL
BEGIN
    DECLARE @dfPrev sysname;
    SELECT @dfPrev = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = 'Workflow_Steps' AND c.name = 'Workflow_Steps_PreviousStep_ID';

    IF @dfPrev IS NOT NULL
        EXEC('ALTER TABLE dbo.Workflow_Steps DROP CONSTRAINT ' + QUOTENAME(@dfPrev));

    ALTER TABLE dbo.Workflow_Steps DROP COLUMN Workflow_Steps_PreviousStep_ID;
END;
GO
