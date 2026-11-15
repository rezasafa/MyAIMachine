IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108170422_InitialCreate'
)
BEGIN
    CREATE TABLE [AppLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [CreatedAt] datetime2 NOT NULL,
        [Level] nvarchar(20) NOT NULL,
        [Event] nvarchar(100) NOT NULL,
        [Detail] nvarchar(2000) NOT NULL,
        CONSTRAINT [PK_AppLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108170422_InitialCreate'
)
BEGIN
    CREATE TABLE [Conversations] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerId] nvarchar(64) NOT NULL,
        [Title] nvarchar(100) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108170422_InitialCreate'
)
BEGIN
    CREATE TABLE [ModelSettings] (
        [Id] int NOT NULL,
        [MinimumSimilarity] real NOT NULL,
        [FallbackResponse] nvarchar(2000) NOT NULL,
        CONSTRAINT [PK_ModelSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108170422_InitialCreate'
)
BEGIN
    CREATE TABLE [ModelVersions] (
        [Id] bigint NOT NULL IDENTITY,
        [CreatedAt] datetime2 NOT NULL,
        [ExampleCount] int NOT NULL,
        [Artifact] varbinary(max) NOT NULL,
        [ExamplesJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ModelVersions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108170422_InitialCreate'
)
BEGIN
    CREATE TABLE [TrainingExamples] (
        [Id] bigint NOT NULL IDENTITY,
        [Input] nvarchar(4000) NOT NULL,
        [Response] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TrainingExamples] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108170422_InitialCreate'
)
BEGIN
    CREATE TABLE [ChatMessages] (
        [Id] bigint NOT NULL IDENTITY,
        [ConversationId] uniqueidentifier NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Similarity] real NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMessages_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108170422_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FallbackResponse', N'MinimumSimilarity') AND [object_id] = OBJECT_ID(N'[ModelSettings]'))
        SET IDENTITY_INSERT [ModelSettings] ON;
    EXEC(N'INSERT INTO [ModelSettings] ([Id], [FallbackResponse], [MinimumSimilarity])
    VALUES (1, N''هنوز پاسخ این پرسش را یاد نگرفته‌ام. یک نمونه در بخش آموزش اضافه کنید و مدل را آموزش دهید.'', CAST(0.45 AS real))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'FallbackResponse', N'MinimumSimilarity') AND [object_id] = OBJECT_ID(N'[ModelSettings]'))
        SET IDENTITY_INSERT [ModelSettings] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108170422_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_ConversationId] ON [ChatMessages] ([ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108170422_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Conversations_OwnerId_UpdatedAt] ON [Conversations] ([OwnerId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108170422_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108170422_InitialCreate', N'9.0.2');
END;

COMMIT;
GO

