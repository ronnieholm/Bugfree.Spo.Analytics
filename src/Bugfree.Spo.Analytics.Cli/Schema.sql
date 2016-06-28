CREATE TABLE [dbo].[Visits] (
    [Id]            INT              IDENTITY (1, 1) NOT NULL,
    [CorrelationId] UNIQUEIDENTIFIER NOT NULL,
    [Timestamp]     DATETIME         NOT NULL,
    [LoginName]     VARCHAR (MAX)    NOT NULL,
    [Url]           VARCHAR (MAX)    NOT NULL,
    [PageLoadTime]  INT              NULL,
    [IP]            VARCHAR (MAX)    NULL,
    [UserAgent]     VARCHAR (MAX)    NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

--GO
CREATE NONCLUSTERED INDEX [IX_Visits_CorrelationId]
    ON [dbo].[Visits]([CorrelationId] ASC);

--GO
CREATE NONCLUSTERED INDEX [IX_Visits_Timestamp]
    ON [dbo].[Visits]([Timestamp] ASC);
