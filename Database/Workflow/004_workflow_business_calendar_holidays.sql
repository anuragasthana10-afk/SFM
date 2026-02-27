/*
    Workflow business-day holiday calendar.
    Store non-weekend holidays to exclude from workflow SLA day calculations.
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Workflow_BusinessCalendarHolidays', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Workflow_BusinessCalendarHolidays
    (
        ID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Workflow_BusinessCalendarHolidays PRIMARY KEY,
        HolidayName NVARCHAR(200) NOT NULL,
        HolidayDate DATE NOT NULL,
        CreateUserID INT NULL,
        CreateDate DATETIME2(7) NOT NULL CONSTRAINT DF_Workflow_BusinessCalendarHolidays_CreateDate DEFAULT (SYSUTCDATETIME()),
        ModifyUserID INT NULL,
        ModifyDate DATETIME2(7) NULL,
        DelFlag BIT NOT NULL CONSTRAINT DF_Workflow_BusinessCalendarHolidays_DelFlag DEFAULT (0)
    );

    CREATE UNIQUE INDEX UX_Workflow_BusinessCalendarHolidays_HolidayDate
        ON dbo.Workflow_BusinessCalendarHolidays(HolidayDate)
        WHERE DelFlag = 0;
END;
GO
