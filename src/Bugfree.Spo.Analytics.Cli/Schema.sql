CREATE TABLE [dbo].[IPs] (
    [Id] INT           IDENTITY (1, 1) NOT NULL,
    [IP] VARCHAR (MAX) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

CREATE TABLE [dbo].[LoginNames] (
    [Id]        INT           IDENTITY (1, 1) NOT NULL,
    [LoginName] VARCHAR (MAX) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

CREATE TABLE [dbo].[SiteCollections] (
    [Id]                INT           IDENTITY (1, 1) NOT NULL,
    [SiteCollectionUrl] VARCHAR (MAX) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

CREATE TABLE [dbo].[UserAgents] (
    [Id]        INT           IDENTITY (1, 1) NOT NULL,
    [UserAgent] VARCHAR (MAX) NOT NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC)
);

CREATE TABLE [dbo].[Visits] (
    [Id]               INT              IDENTITY (1, 1) NOT NULL,
    [CorrelationId]    UNIQUEIDENTIFIER NOT NULL,
    [Timestamp]        DATETIME         NOT NULL,
    [Url]              VARCHAR (MAX)    NOT NULL,
    [PageLoadTime]     INT              NULL,
    [SiteCollectionId] INT              NOT NULL,
    [LoginNameId]      INT              NOT NULL,
    [IPId]             INT              NOT NULL,
    [UserAgentId]      INT              NULL,
    PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Visits_SiteCollections] FOREIGN KEY ([SiteCollectionId]) REFERENCES [dbo].[SiteCollections] ([Id]),
    CONSTRAINT [FK_Visits_LoginNames] FOREIGN KEY ([LoginNameId]) REFERENCES [dbo].[LoginNames] ([Id]),
	CONSTRAINT [FK_Visits_IPs] FOREIGN KEY ([IPId]) REFERENCES [dbo].[IPs] ([Id]),
    CONSTRAINT [FK_Visits_UserAgents] FOREIGN KEY ([UserAgentId]) REFERENCES [dbo].[UserAgents] ([Id])
);

CREATE NONCLUSTERED INDEX [IX_Visits_CorrelationId]
    ON [dbo].[Visits]([CorrelationId] ASC);

CREATE NONCLUSTERED INDEX [IX_Visits_Timestamp]
    ON [dbo].[Visits]([Timestamp] ASC);

