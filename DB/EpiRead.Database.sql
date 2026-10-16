-- EpiRead database deployment script
-- Generated from Entity Framework migrations and stored procedures.
-- Safe to rerun: migrations are idempotent and procedures use CREATE OR ALTER.

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
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [UserName] nvarchar(255) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        [Password] nvarchar(255) NOT NULL,
        [PhoneNumber] nvarchar(50) NOT NULL,
        [Role] nvarchar(50) NOT NULL,
        [ProfileImageUrl] nvarchar(255) NULL,
        [IsBlocked] bit NOT NULL,
        [IsDeleted] int NOT NULL,
        [IsMfaEnabled] bit NOT NULL,
        [GoogleMfaSecretKey] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE TABLE [LoginAttempts] (
        [Id] int NOT NULL IDENTITY,
        [TryAttempt] int NOT NULL,
        [Ip] nvarchar(255) NULL,
        [IsBlocked] bit NOT NULL,
        [ExpiredAt] datetime2 NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_LoginAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LoginAttempts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE TABLE [LoginLogs] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(255) NOT NULL,
        [Ip] nvarchar(255) NULL,
        [City] nvarchar(255) NULL,
        [Region] nvarchar(255) NULL,
        [Country] nvarchar(255) NULL,
        [Loc] nvarchar(255) NULL,
        [Org] nvarchar(255) NULL,
        [Postal] nvarchar(255) NULL,
        [Timezone] nvarchar(255) NULL,
        [LoginTime] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_LoginLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LoginLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [OrderNumber] nvarchar(255) NOT NULL,
        [TransactionNumber] nvarchar(255) NOT NULL,
        [Status] int NOT NULL,
        [ServiceChargeRate] decimal(10,2) NOT NULL,
        [ServiceCharge] decimal(10,2) NOT NULL,
        [SubTotalAmount] decimal(10,2) NOT NULL,
        [TotalAmount] decimal(10,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE TABLE [ResetMfas] (
        [Id] int NOT NULL IDENTITY,
        [Token] nvarchar(255) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_ResetMfas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResetMfas_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE TABLE [ResetPasswords] (
        [Id] int NOT NULL IDENTITY,
        [Token] nvarchar(255) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_ResetPasswords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ResetPasswords_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE TABLE [Transactions] (
        [Id] int NOT NULL IDENTITY,
        [TransactionNumber] nvarchar(255) NOT NULL,
        [SessionId] nvarchar(255) NOT NULL,
        [PaymentIntentId] nvarchar(255) NOT NULL,
        [PaymentMethod] nvarchar(255) NOT NULL,
        [ServiceCharge] decimal(10,2) NOT NULL,
        [TotalAmount] decimal(10,2) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_Transactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Transactions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Id], [Name])
    VALUES (1, N''SuperAdmin''),
    (2, N''Admin''),
    (3, N''Member'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE INDEX [IX_LoginAttempts_UserId] ON [LoginAttempts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE INDEX [IX_LoginLogs_UserId] ON [LoginLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE INDEX [IX_ResetMfas_UserId] ON [ResetMfas] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE INDEX [IX_ResetPasswords_UserId] ON [ResetPasswords] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    CREATE INDEX [IX_Transactions_UserId] ON [Transactions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121161703_InitDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250121161703_InitDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121162357_UpdateUserRoles1'
)
BEGIN
    EXEC(N'UPDATE [Roles] SET [Name] = N''Staff''
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121162357_UpdateUserRoles1'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Id], [Name])
    VALUES (4, N''Member'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250121162357_UpdateUserRoles1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250121162357_UpdateUserRoles1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250612125946_UpdateUserRoles2'
)
BEGIN
    EXEC(N'DELETE FROM [Roles]
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250612125946_UpdateUserRoles2'
)
BEGIN
    EXEC(N'UPDATE [Roles] SET [Name] = N''Member''
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250612125946_UpdateUserRoles2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250612125946_UpdateUserRoles2', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250618165139_UpdateUserTableDB1'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'GoogleMfaSecretKey');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [Users] ALTER COLUMN [GoogleMfaSecretKey] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250618165139_UpdateUserTableDB1'
)
BEGIN
    EXEC(N'UPDATE [Roles] SET [Name] = N''Author''
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250618165139_UpdateUserTableDB1'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Id], [Name])
    VALUES (4, N''Member'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250618165139_UpdateUserTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250618165139_UpdateUserTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250708142137_UpdateUserTableDB2'
)
BEGIN
    DROP TABLE [Orders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250708142137_UpdateUserTableDB2'
)
BEGIN
    DROP TABLE [Transactions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250708142137_UpdateUserTableDB2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250708142137_UpdateUserTableDB2', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250821051426_UpdateUserTableDB3'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'UserName');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Users] ALTER COLUMN [UserName] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250821051426_UpdateUserTableDB3'
)
BEGIN
    ALTER TABLE [Users] ADD [DateOfBirth] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250821051426_UpdateUserTableDB3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250821051426_UpdateUserTableDB3', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250821121708_CreateBookCategoryTableDB'
)
BEGIN
    CREATE TABLE [BookCategories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(100) NOT NULL,
        [Description] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [ParentId] int NULL,
        CONSTRAINT [PK_BookCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BookCategories_BookCategories_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [BookCategories] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250821121708_CreateBookCategoryTableDB'
)
BEGIN
    CREATE INDEX [IX_BookCategories_ParentId] ON [BookCategories] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250821121708_CreateBookCategoryTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250821121708_CreateBookCategoryTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250821121945_UpdateUserTableDB4'
)
BEGIN
    EXEC sp_rename N'[Users].[ProfileImageUrl]', N'ProfileImagePath', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250821121945_UpdateUserTableDB4'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250821121945_UpdateUserTableDB4', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822030219_UpdateUserTableDB5'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'IsBlocked');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Users] DROP COLUMN [IsBlocked];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822030219_UpdateUserTableDB5'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'IsDeleted');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Users] DROP COLUMN [IsDeleted];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822030219_UpdateUserTableDB5'
)
BEGIN
    EXEC sp_rename N'[Users].[IsMfaEnabled]', N'MfaEnabled', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822030219_UpdateUserTableDB5'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'PhoneNumber');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Users] ALTER COLUMN [PhoneNumber] nvarchar(15) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822030219_UpdateUserTableDB5'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'DateOfBirth');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Users] ALTER COLUMN [DateOfBirth] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822030219_UpdateUserTableDB5'
)
BEGIN
    ALTER TABLE [Users] ADD [DeletedOn] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822030219_UpdateUserTableDB5'
)
BEGIN
    ALTER TABLE [Users] ADD [Status] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822030219_UpdateUserTableDB5'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822030219_UpdateUserTableDB5', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822044417_UpdateLoginHistoryTableDB1'
)
BEGIN
    DROP TABLE [LoginLogs];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822044417_UpdateLoginHistoryTableDB1'
)
BEGIN
    CREATE TABLE [LoginHistory] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(255) NOT NULL,
        [Ip] nvarchar(255) NULL,
        [City] nvarchar(255) NULL,
        [Region] nvarchar(255) NULL,
        [Country] nvarchar(255) NULL,
        [Loc] nvarchar(255) NULL,
        [Org] nvarchar(255) NULL,
        [Postal] nvarchar(255) NULL,
        [Timezone] nvarchar(255) NULL,
        [LoginTime] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_LoginHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LoginHistory_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822044417_UpdateLoginHistoryTableDB1'
)
BEGIN
    CREATE INDEX [IX_LoginHistory_UserId] ON [LoginHistory] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822044417_UpdateLoginHistoryTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822044417_UpdateLoginHistoryTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822045521_CreateChatSessionTableDB'
)
BEGIN
    CREATE TABLE [ChatSessions] (
        [ChatSessionsId] uniqueidentifier NOT NULL,
        [Title] nvarchar(255) NOT NULL,
        [Context] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_ChatSessions] PRIMARY KEY ([ChatSessionsId]),
        CONSTRAINT [FK_ChatSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822045521_CreateChatSessionTableDB'
)
BEGIN
    CREATE INDEX [IX_ChatSessions_UserId] ON [ChatSessions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822045521_CreateChatSessionTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822045521_CreateChatSessionTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822045811_UpdateChatSessionTableDB1'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChatSessions]') AND [c].[name] = N'ExpiresAt');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [ChatSessions] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [ChatSessions] DROP COLUMN [ExpiresAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822045811_UpdateChatSessionTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822045811_UpdateChatSessionTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822045903_UpdateChatSessionTableDB2'
)
BEGIN
    EXEC sp_rename N'[ChatSessions].[ChatSessionsId]', N'Id', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822045903_UpdateChatSessionTableDB2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822045903_UpdateChatSessionTableDB2', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822050606_CreateChatMessageTableDB'
)
BEGIN
    CREATE TABLE [ChatMessages] (
        [Id] uniqueidentifier NOT NULL,
        [Sender] nvarchar(50) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ChatSessionsId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMessages_ChatSessions_ChatSessionsId] FOREIGN KEY ([ChatSessionsId]) REFERENCES [ChatSessions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822050606_CreateChatMessageTableDB'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_ChatSessionsId] ON [ChatMessages] ([ChatSessionsId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822050606_CreateChatMessageTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822050606_CreateChatMessageTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822050746_UpdateChatSessionTableDB3'
)
BEGIN
    ALTER TABLE [ChatMessages] DROP CONSTRAINT [FK_ChatMessages_ChatSessions_ChatSessionsId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822050746_UpdateChatSessionTableDB3'
)
BEGIN
    DROP INDEX [IX_ChatMessages_ChatSessionsId] ON [ChatMessages];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822050746_UpdateChatSessionTableDB3'
)
BEGIN
    ALTER TABLE [ChatMessages] ADD [ChatSessionId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822050746_UpdateChatSessionTableDB3'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_ChatSessionId] ON [ChatMessages] ([ChatSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822050746_UpdateChatSessionTableDB3'
)
BEGIN
    ALTER TABLE [ChatMessages] ADD CONSTRAINT [FK_ChatMessages_ChatSessions_ChatSessionId] FOREIGN KEY ([ChatSessionId]) REFERENCES [ChatSessions] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822050746_UpdateChatSessionTableDB3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822050746_UpdateChatSessionTableDB3', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822051154_UpdateLoginHistoryTableDB2'
)
BEGIN
    ALTER TABLE [LoginHistory] DROP CONSTRAINT [FK_LoginHistory_Users_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822051154_UpdateLoginHistoryTableDB2'
)
BEGIN
    ALTER TABLE [LoginHistory] DROP CONSTRAINT [PK_LoginHistory];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822051154_UpdateLoginHistoryTableDB2'
)
BEGIN
    EXEC sp_rename N'[LoginHistory]', N'LoginHistories', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822051154_UpdateLoginHistoryTableDB2'
)
BEGIN
    EXEC sp_rename N'[LoginHistories].[IX_LoginHistory_UserId]', N'IX_LoginHistories_UserId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822051154_UpdateLoginHistoryTableDB2'
)
BEGIN
    ALTER TABLE [LoginHistories] ADD CONSTRAINT [PK_LoginHistories] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822051154_UpdateLoginHistoryTableDB2'
)
BEGIN
    ALTER TABLE [LoginHistories] ADD CONSTRAINT [FK_LoginHistories_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822051154_UpdateLoginHistoryTableDB2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822051154_UpdateLoginHistoryTableDB2', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822051755_CreateSearchHistoryTableDB'
)
BEGIN
    CREATE TABLE [SearchHistories] (
        [SearchHistoryId] int NOT NULL IDENTITY,
        [Context] nvarchar(100) NOT NULL,
        [IsSaved] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_SearchHistories] PRIMARY KEY ([SearchHistoryId]),
        CONSTRAINT [FK_SearchHistories_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822051755_CreateSearchHistoryTableDB'
)
BEGIN
    CREATE INDEX [IX_SearchHistories_UserId] ON [SearchHistories] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822051755_CreateSearchHistoryTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822051755_CreateSearchHistoryTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822053308_UpdateUserTableDB6'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'DeletedOn');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [Users] ALTER COLUMN [DeletedOn] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822053308_UpdateUserTableDB6'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822053308_UpdateUserTableDB6', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822054346_CreateBookTableDB'
)
BEGIN
    CREATE TABLE [Books] (
        [BookId] int NOT NULL IDENTITY,
        [AuthorName] nvarchar(255) NOT NULL,
        [Title] nvarchar(255) NOT NULL,
        [Description] nvarchar(max) NULL,
        [PublicationYear] date NULL,
        [LanguageCode] nvarchar(10) NULL,
        [CoverImagePath] nvarchar(255) NULL,
        [Status] nvarchar(50) NOT NULL,
        [DeletedOn] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Books] PRIMARY KEY ([BookId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822054346_CreateBookTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822054346_CreateBookTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822054642_UpdateBookTableDB1'
)
BEGIN
    EXEC sp_rename N'[Books].[BookId]', N'Id', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822054642_UpdateBookTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822054642_UpdateBookTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822054854_CreateBookHistoryTableDB'
)
BEGIN
    CREATE TABLE [BookHistories] (
        [Id] int NOT NULL IDENTITY,
        [BookProgress] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UserId] int NOT NULL,
        [BookId] int NOT NULL,
        CONSTRAINT [PK_BookHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BookHistories_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BookHistories_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822054854_CreateBookHistoryTableDB'
)
BEGIN
    CREATE INDEX [IX_BookHistories_BookId] ON [BookHistories] ([BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822054854_CreateBookHistoryTableDB'
)
BEGIN
    CREATE INDEX [IX_BookHistories_UserId] ON [BookHistories] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822054854_CreateBookHistoryTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822054854_CreateBookHistoryTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822062515_UpdateBookCategoryTableDB1'
)
BEGIN
    ALTER TABLE [ChatMessages] DROP CONSTRAINT [FK_ChatMessages_ChatSessions_ChatSessionId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822062515_UpdateBookCategoryTableDB1'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChatMessages]') AND [c].[name] = N'ChatSessionId');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [ChatMessages] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [ChatMessages] ALTER COLUMN [ChatSessionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822062515_UpdateBookCategoryTableDB1'
)
BEGIN
    ALTER TABLE [ChatMessages] ADD [UserId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822062515_UpdateBookCategoryTableDB1'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BookCategories]') AND [c].[name] = N'Description');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [BookCategories] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [BookCategories] ALTER COLUMN [Description] nvarchar(255) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822062515_UpdateBookCategoryTableDB1'
)
BEGIN
    ALTER TABLE [BookCategories] ADD [DeletedOn] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822062515_UpdateBookCategoryTableDB1'
)
BEGIN
    ALTER TABLE [BookCategories] ADD [Status] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822062515_UpdateBookCategoryTableDB1'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_UserId] ON [ChatMessages] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822062515_UpdateBookCategoryTableDB1'
)
BEGIN
    ALTER TABLE [ChatMessages] ADD CONSTRAINT [FK_ChatMessages_ChatSessions_ChatSessionId] FOREIGN KEY ([ChatSessionId]) REFERENCES [ChatSessions] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822062515_UpdateBookCategoryTableDB1'
)
BEGIN
    ALTER TABLE [ChatMessages] ADD CONSTRAINT [FK_ChatMessages_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822062515_UpdateBookCategoryTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822062515_UpdateBookCategoryTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822064712_CreateBookDocumentTableDB'
)
BEGIN
    CREATE TABLE [BookDocuments] (
        [Id] int NOT NULL IDENTITY,
        [FormatType] nvarchar(20) NOT NULL,
        [DocumentPath] nvarchar(255) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [BookId] int NOT NULL,
        CONSTRAINT [PK_BookDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BookDocuments_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822064712_CreateBookDocumentTableDB'
)
BEGIN
    CREATE INDEX [IX_BookDocuments_BookId] ON [BookDocuments] ([BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822064712_CreateBookDocumentTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822064712_CreateBookDocumentTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822065711_CreateBookCategoryMapTableDB'
)
BEGIN
    CREATE TABLE [BookCategoryMaps] (
        [BookId] int NOT NULL,
        [BookCategoryId] int NOT NULL,
        CONSTRAINT [PK_BookCategoryMaps] PRIMARY KEY ([BookId], [BookCategoryId]),
        CONSTRAINT [FK_BookCategoryMaps_BookCategories_BookCategoryId] FOREIGN KEY ([BookCategoryId]) REFERENCES [BookCategories] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BookCategoryMaps_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822065711_CreateBookCategoryMapTableDB'
)
BEGIN
    CREATE INDEX [IX_BookCategoryMaps_BookCategoryId] ON [BookCategoryMaps] ([BookCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822065711_CreateBookCategoryMapTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822065711_CreateBookCategoryMapTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822070005_CreatePreferenceBookTableDB'
)
BEGIN
    CREATE TABLE [PreferenceBooks] (
        [Id] int NOT NULL IDENTITY,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [BookId] int NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_PreferenceBooks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PreferenceBooks_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PreferenceBooks_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822070005_CreatePreferenceBookTableDB'
)
BEGIN
    CREATE INDEX [IX_PreferenceBooks_BookId] ON [PreferenceBooks] ([BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822070005_CreatePreferenceBookTableDB'
)
BEGIN
    CREATE INDEX [IX_PreferenceBooks_UserId] ON [PreferenceBooks] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822070005_CreatePreferenceBookTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822070005_CreatePreferenceBookTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822070456_CreateBookmarkTableDB'
)
BEGIN
    CREATE TABLE [Bookmarks] (
        [Id] int NOT NULL IDENTITY,
        [Page] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [UserId] int NOT NULL,
        [BookId] int NOT NULL,
        CONSTRAINT [PK_Bookmarks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Bookmarks_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Bookmarks_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822070456_CreateBookmarkTableDB'
)
BEGIN
    CREATE INDEX [IX_Bookmarks_BookId] ON [Bookmarks] ([BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822070456_CreateBookmarkTableDB'
)
BEGIN
    CREATE INDEX [IX_Bookmarks_UserId] ON [Bookmarks] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822070456_CreateBookmarkTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822070456_CreateBookmarkTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822071027_UpdateForeignKeyTable'
)
BEGIN
    ALTER TABLE [ChatMessages] DROP CONSTRAINT [FK_ChatMessages_ChatSessions_ChatSessionId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822071027_UpdateForeignKeyTable'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChatMessages]') AND [c].[name] = N'ChatSessionsId');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [ChatMessages] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [ChatMessages] DROP COLUMN [ChatSessionsId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822071027_UpdateForeignKeyTable'
)
BEGIN
    DROP INDEX [IX_ChatMessages_ChatSessionId] ON [ChatMessages];
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChatMessages]') AND [c].[name] = N'ChatSessionId');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [ChatMessages] DROP CONSTRAINT [' + @var11 + '];');
    EXEC(N'UPDATE [ChatMessages] SET [ChatSessionId] = ''00000000-0000-0000-0000-000000000000'' WHERE [ChatSessionId] IS NULL');
    ALTER TABLE [ChatMessages] ALTER COLUMN [ChatSessionId] uniqueidentifier NOT NULL;
    ALTER TABLE [ChatMessages] ADD DEFAULT '00000000-0000-0000-0000-000000000000' FOR [ChatSessionId];
    CREATE INDEX [IX_ChatMessages_ChatSessionId] ON [ChatMessages] ([ChatSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822071027_UpdateForeignKeyTable'
)
BEGIN
    ALTER TABLE [ChatMessages] ADD CONSTRAINT [FK_ChatMessages_ChatSessions_ChatSessionId] FOREIGN KEY ([ChatSessionId]) REFERENCES [ChatSessions] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822071027_UpdateForeignKeyTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822071027_UpdateForeignKeyTable', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822115220_CreateRatingTableDB'
)
BEGIN
    CREATE TABLE [Rating] (
        [Id] int NOT NULL IDENTITY,
        [StartCount] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [BookId] int NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_Rating] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Rating_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Rating_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822115220_CreateRatingTableDB'
)
BEGIN
    CREATE INDEX [IX_Rating_BookId] ON [Rating] ([BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822115220_CreateRatingTableDB'
)
BEGIN
    CREATE INDEX [IX_Rating_UserId] ON [Rating] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822115220_CreateRatingTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822115220_CreateRatingTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120352_UpdateRatingTableDB1'
)
BEGIN
    ALTER TABLE [Rating] DROP CONSTRAINT [FK_Rating_Books_BookId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120352_UpdateRatingTableDB1'
)
BEGIN
    ALTER TABLE [Rating] DROP CONSTRAINT [FK_Rating_Users_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120352_UpdateRatingTableDB1'
)
BEGIN
    ALTER TABLE [Rating] DROP CONSTRAINT [PK_Rating];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120352_UpdateRatingTableDB1'
)
BEGIN
    EXEC sp_rename N'[Rating]', N'Ratings', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120352_UpdateRatingTableDB1'
)
BEGIN
    EXEC sp_rename N'[Ratings].[IX_Rating_UserId]', N'IX_Ratings_UserId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120352_UpdateRatingTableDB1'
)
BEGIN
    EXEC sp_rename N'[Ratings].[IX_Rating_BookId]', N'IX_Ratings_BookId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120352_UpdateRatingTableDB1'
)
BEGIN
    ALTER TABLE [Ratings] ADD CONSTRAINT [PK_Ratings] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120352_UpdateRatingTableDB1'
)
BEGIN
    ALTER TABLE [Ratings] ADD CONSTRAINT [FK_Ratings_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120352_UpdateRatingTableDB1'
)
BEGIN
    ALTER TABLE [Ratings] ADD CONSTRAINT [FK_Ratings_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120352_UpdateRatingTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822120352_UpdateRatingTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120458_CreateDiscussionTableDB'
)
BEGIN
    CREATE TABLE [Discussions] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [BookId] int NOT NULL,
        CONSTRAINT [PK_Discussions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Discussions_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120458_CreateDiscussionTableDB'
)
BEGIN
    CREATE INDEX [IX_Discussions_BookId] ON [Discussions] ([BookId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822120458_CreateDiscussionTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822120458_CreateDiscussionTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822121137_CreateDiscussionPostTableDB'
)
BEGIN
    CREATE TABLE [DiscussionPosts] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(100) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [LikeCount] int NOT NULL,
        [ViewCount] int NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [DeletedOn] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [DiscussionId] int NOT NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_DiscussionPosts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DiscussionPosts_Discussions_DiscussionId] FOREIGN KEY ([DiscussionId]) REFERENCES [Discussions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DiscussionPosts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822121137_CreateDiscussionPostTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionPosts_DiscussionId] ON [DiscussionPosts] ([DiscussionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822121137_CreateDiscussionPostTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionPosts_UserId] ON [DiscussionPosts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822121137_CreateDiscussionPostTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822121137_CreateDiscussionPostTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822124937_CreateDiscussionCommentTableDB'
)
BEGIN
    CREATE TABLE [DiscussionComments] (
        [Id] int NOT NULL IDENTITY,
        [Content] nvarchar(max) NOT NULL,
        [LikeCount] int NOT NULL,
        [ViewCount] int NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [DeletedOn] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [DiscussionPostId] int NOT NULL,
        [ReplyCommentId] int NULL,
        [UserId] int NOT NULL,
        [UserId1] int NULL,
        CONSTRAINT [PK_DiscussionComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DiscussionComments_DiscussionComments_ReplyCommentId] FOREIGN KEY ([ReplyCommentId]) REFERENCES [DiscussionComments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DiscussionComments_DiscussionPosts_DiscussionPostId] FOREIGN KEY ([DiscussionPostId]) REFERENCES [DiscussionPosts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DiscussionComments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DiscussionComments_Users_UserId1] FOREIGN KEY ([UserId1]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822124937_CreateDiscussionCommentTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionComments_DiscussionPostId] ON [DiscussionComments] ([DiscussionPostId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822124937_CreateDiscussionCommentTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionComments_ReplyCommentId] ON [DiscussionComments] ([ReplyCommentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822124937_CreateDiscussionCommentTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionComments_UserId] ON [DiscussionComments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822124937_CreateDiscussionCommentTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionComments_UserId1] ON [DiscussionComments] ([UserId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822124937_CreateDiscussionCommentTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822124937_CreateDiscussionCommentTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822130219_CreateDiscussionAttachmentTableDB'
)
BEGIN
    CREATE TABLE [DiscussionAttachment] (
        [Id] int NOT NULL IDENTITY,
        [FilePath] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [FileSize] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [DiscussionCommentId] int NOT NULL,
        CONSTRAINT [PK_DiscussionAttachment] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DiscussionAttachment_DiscussionComments_DiscussionCommentId] FOREIGN KEY ([DiscussionCommentId]) REFERENCES [DiscussionComments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822130219_CreateDiscussionAttachmentTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionAttachment_DiscussionCommentId] ON [DiscussionAttachment] ([DiscussionCommentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822130219_CreateDiscussionAttachmentTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822130219_CreateDiscussionAttachmentTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822165522_UpdateBookCategoryTableDB2'
)
BEGIN
    ALTER TABLE [DiscussionAttachment] DROP CONSTRAINT [FK_DiscussionAttachment_DiscussionComments_DiscussionCommentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822165522_UpdateBookCategoryTableDB2'
)
BEGIN
    ALTER TABLE [DiscussionAttachment] DROP CONSTRAINT [PK_DiscussionAttachment];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822165522_UpdateBookCategoryTableDB2'
)
BEGIN
    EXEC sp_rename N'[DiscussionAttachment]', N'DiscussionAttachments', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822165522_UpdateBookCategoryTableDB2'
)
BEGIN
    EXEC sp_rename N'[DiscussionAttachments].[IX_DiscussionAttachment_DiscussionCommentId]', N'IX_DiscussionAttachments_DiscussionCommentId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822165522_UpdateBookCategoryTableDB2'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BookCategories]') AND [c].[name] = N'Slug');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [BookCategories] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [BookCategories] ALTER COLUMN [Slug] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822165522_UpdateBookCategoryTableDB2'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BookCategories]') AND [c].[name] = N'Description');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [BookCategories] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [BookCategories] ALTER COLUMN [Description] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822165522_UpdateBookCategoryTableDB2'
)
BEGIN
    ALTER TABLE [DiscussionAttachments] ADD CONSTRAINT [PK_DiscussionAttachments] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822165522_UpdateBookCategoryTableDB2'
)
BEGIN
    ALTER TABLE [DiscussionAttachments] ADD CONSTRAINT [FK_DiscussionAttachments_DiscussionComments_DiscussionCommentId] FOREIGN KEY ([DiscussionCommentId]) REFERENCES [DiscussionComments] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250822165522_UpdateBookCategoryTableDB2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250822165522_UpdateBookCategoryTableDB2', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250823160050_UpdateDiscussionPostTableDB1'
)
BEGIN
    ALTER TABLE [DiscussionPosts] ADD [BannedOn] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250823160050_UpdateDiscussionPostTableDB1'
)
BEGIN
    ALTER TABLE [DiscussionPosts] ADD [Reason] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250823160050_UpdateDiscussionPostTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250823160050_UpdateDiscussionPostTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250823160206_UpdateUserTableDB7'
)
BEGIN
    ALTER TABLE [Users] ADD [Language] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250823160206_UpdateUserTableDB7'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250823160206_UpdateUserTableDB7', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250824042929_UpdateDiscussionTableDB1'
)
BEGIN
    ALTER TABLE [Discussions] ADD [Description] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250824042929_UpdateDiscussionTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250824042929_UpdateDiscussionTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250824132825_CreateDiscussionModerationActionTableDB'
)
BEGIN
    CREATE TABLE [DiscussionModerationActions] (
        [DiscussionModerationActionId] int NOT NULL IDENTITY,
        [Remarks] nvarchar(255) NULL,
        [PercentageScore] decimal(18,2) NOT NULL,
        [ThresholdValue] decimal(18,2) NOT NULL,
        [IsRemoved] bit NOT NULL,
        [EvaluatedAt] datetime2 NOT NULL,
        [RemovedAt] datetime2 NULL,
        [DiscussionCommentId] int NULL,
        [DiscussionPostId] int NULL,
        [UserId] int NULL,
        CONSTRAINT [PK_DiscussionModerationActions] PRIMARY KEY ([DiscussionModerationActionId]),
        CONSTRAINT [FK_DiscussionModerationActions_DiscussionComments_DiscussionCommentId] FOREIGN KEY ([DiscussionCommentId]) REFERENCES [DiscussionComments] ([Id]),
        CONSTRAINT [FK_DiscussionModerationActions_DiscussionPosts_DiscussionPostId] FOREIGN KEY ([DiscussionPostId]) REFERENCES [DiscussionPosts] ([Id]),
        CONSTRAINT [FK_DiscussionModerationActions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250824132825_CreateDiscussionModerationActionTableDB'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DiscussionModerationActions_DiscussionCommentId] ON [DiscussionModerationActions] ([DiscussionCommentId]) WHERE [DiscussionCommentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250824132825_CreateDiscussionModerationActionTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionModerationActions_DiscussionPostId] ON [DiscussionModerationActions] ([DiscussionPostId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250824132825_CreateDiscussionModerationActionTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionModerationActions_UserId] ON [DiscussionModerationActions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250824132825_CreateDiscussionModerationActionTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250824132825_CreateDiscussionModerationActionTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250824133018_UpdateDiscussionModerationActionTableDB1'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionModerationActions]') AND [c].[name] = N'ThresholdValue');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionModerationActions] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [DiscussionModerationActions] ALTER COLUMN [ThresholdValue] decimal(5,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250824133018_UpdateDiscussionModerationActionTableDB1'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionModerationActions]') AND [c].[name] = N'PercentageScore');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionModerationActions] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [DiscussionModerationActions] ALTER COLUMN [PercentageScore] decimal(5,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250824133018_UpdateDiscussionModerationActionTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250824133018_UpdateDiscussionModerationActionTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251017141947_UpdateBookCategoryTableDB3'
)
BEGIN
    ALTER TABLE [Books] ADD [BookCategoryId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251017141947_UpdateBookCategoryTableDB3'
)
BEGIN
    CREATE INDEX [IX_Books_BookCategoryId] ON [Books] ([BookCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251017141947_UpdateBookCategoryTableDB3'
)
BEGIN
    ALTER TABLE [Books] ADD CONSTRAINT [FK_Books_BookCategories_BookCategoryId] FOREIGN KEY ([BookCategoryId]) REFERENCES [BookCategories] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251017141947_UpdateBookCategoryTableDB3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251017141947_UpdateBookCategoryTableDB3', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018082834_UpdateBookTableDB2'
)
BEGIN
    ALTER TABLE [Books] DROP CONSTRAINT [FK_Books_BookCategories_BookCategoryId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018082834_UpdateBookTableDB2'
)
BEGIN
    DROP INDEX [IX_Books_BookCategoryId] ON [Books];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018082834_UpdateBookTableDB2'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Books]') AND [c].[name] = N'BookCategoryId');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [Books] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [Books] DROP COLUMN [BookCategoryId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018082834_UpdateBookTableDB2'
)
BEGIN
    CREATE TABLE [BookBookCategory] (
        [BookCategoriesId] int NOT NULL,
        [BooksId] int NOT NULL,
        CONSTRAINT [PK_BookBookCategory] PRIMARY KEY ([BookCategoriesId], [BooksId]),
        CONSTRAINT [FK_BookBookCategory_BookCategories_BookCategoriesId] FOREIGN KEY ([BookCategoriesId]) REFERENCES [BookCategories] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BookBookCategory_Books_BooksId] FOREIGN KEY ([BooksId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018082834_UpdateBookTableDB2'
)
BEGIN
    CREATE INDEX [IX_BookBookCategory_BooksId] ON [BookBookCategory] ([BooksId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018082834_UpdateBookTableDB2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251018082834_UpdateBookTableDB2', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018113458_UpdateBookCategoryTableDB4'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BookCategories]') AND [c].[name] = N'DeletedOn');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [BookCategories] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [BookCategories] DROP COLUMN [DeletedOn];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018113458_UpdateBookCategoryTableDB4'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BookCategories]') AND [c].[name] = N'Status');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [BookCategories] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [BookCategories] DROP COLUMN [Status];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251018113458_UpdateBookCategoryTableDB4'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251018113458_UpdateBookCategoryTableDB4', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251025034728_UpdateBookTableDB3'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Books]') AND [c].[name] = N'LanguageCode');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [Books] DROP CONSTRAINT [' + @var19 + '];');
    EXEC(N'UPDATE [Books] SET [LanguageCode] = N'''' WHERE [LanguageCode] IS NULL');
    ALTER TABLE [Books] ALTER COLUMN [LanguageCode] nvarchar(10) NOT NULL;
    ALTER TABLE [Books] ADD DEFAULT N'' FOR [LanguageCode];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251025034728_UpdateBookTableDB3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251025034728_UpdateBookTableDB3', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251028154619_UpdateBookTableDB4'
)
BEGIN
    ALTER TABLE [Books] ADD [CoverImageLink] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251028154619_UpdateBookTableDB4'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251028154619_UpdateBookTableDB4', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251028165115_UpdateBookDocumentTableDB1'
)
BEGIN
    ALTER TABLE [BookDocuments] ADD [DocumentLink] nvarchar(500) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251028165115_UpdateBookDocumentTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251028165115_UpdateBookDocumentTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251029062333_UpdateUserTableDB8'
)
BEGIN
    ALTER TABLE [Users] ADD [ProfileImageLink] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251029062333_UpdateUserTableDB8'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251029062333_UpdateUserTableDB8', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251029125638_UpdateBookTableDB5'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Books]') AND [c].[name] = N'UpdatedAt');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [Books] DROP CONSTRAINT [' + @var20 + '];');
    EXEC(N'UPDATE [Books] SET [UpdatedAt] = ''0001-01-01T00:00:00.0000000'' WHERE [UpdatedAt] IS NULL');
    ALTER TABLE [Books] ALTER COLUMN [UpdatedAt] datetime2 NOT NULL;
    ALTER TABLE [Books] ADD DEFAULT '0001-01-01T00:00:00.0000000' FOR [UpdatedAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251029125638_UpdateBookTableDB5'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251029125638_UpdateBookTableDB5', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101074144_UpdateBookHistoryTableDB1'
)
BEGIN
    ALTER TABLE [BookHistories] ADD [CurrentPage] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101074144_UpdateBookHistoryTableDB1'
)
BEGIN
    ALTER TABLE [BookHistories] ADD [TotalPages] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101074144_UpdateBookHistoryTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251101074144_UpdateBookHistoryTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101074808_UpdateUserTableDB9'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'PhoneNumber');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [Users] DROP COLUMN [PhoneNumber];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101074808_UpdateUserTableDB9'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251101074808_UpdateUserTableDB9', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101080603_UpdateBookHistoryTableDB2'
)
BEGIN
    ALTER TABLE [BookHistories] ADD [BookDocumentId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101080603_UpdateBookHistoryTableDB2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251101080603_UpdateBookHistoryTableDB2', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101083753_UpdateBookHistoryTableDB3'
)
BEGIN
    CREATE INDEX [IX_BookHistories_BookDocumentId] ON [BookHistories] ([BookDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101083753_UpdateBookHistoryTableDB3'
)
BEGIN
    ALTER TABLE [BookHistories] ADD CONSTRAINT [FK_BookHistories_BookDocuments_BookDocumentId] FOREIGN KEY ([BookDocumentId]) REFERENCES [BookDocuments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101083753_UpdateBookHistoryTableDB3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251101083753_UpdateBookHistoryTableDB3', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101163431_UpdateBookmarkTableDB1'
)
BEGIN
    ALTER TABLE [Bookmarks] ADD [BookDocumentId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101163431_UpdateBookmarkTableDB1'
)
BEGIN
    CREATE INDEX [IX_Bookmarks_BookDocumentId] ON [Bookmarks] ([BookDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101163431_UpdateBookmarkTableDB1'
)
BEGIN
    ALTER TABLE [Bookmarks] ADD CONSTRAINT [FK_Bookmarks_BookDocuments_BookDocumentId] FOREIGN KEY ([BookDocumentId]) REFERENCES [BookDocuments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251101163431_UpdateBookmarkTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251101163431_UpdateBookmarkTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251102051645_UpdateTableDB1'
)
BEGIN
    ALTER TABLE [BookHistories] DROP CONSTRAINT [FK_BookHistories_BookDocuments_BookDocumentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251102051645_UpdateTableDB1'
)
BEGIN
    ALTER TABLE [Bookmarks] DROP CONSTRAINT [FK_Bookmarks_BookDocuments_BookDocumentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251102051645_UpdateTableDB1'
)
BEGIN
    ALTER TABLE [DiscussionComments] DROP CONSTRAINT [FK_DiscussionComments_Users_UserId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251102051645_UpdateTableDB1'
)
BEGIN
    DROP INDEX [IX_DiscussionComments_UserId1] ON [DiscussionComments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251102051645_UpdateTableDB1'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionComments]') AND [c].[name] = N'UserId1');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionComments] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [DiscussionComments] DROP COLUMN [UserId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251102051645_UpdateTableDB1'
)
BEGIN
    ALTER TABLE [BookHistories] ADD CONSTRAINT [FK_BookHistories_BookDocuments_BookDocumentId] FOREIGN KEY ([BookDocumentId]) REFERENCES [BookDocuments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251102051645_UpdateTableDB1'
)
BEGIN
    ALTER TABLE [Bookmarks] ADD CONSTRAINT [FK_Bookmarks_BookDocuments_BookDocumentId] FOREIGN KEY ([BookDocumentId]) REFERENCES [BookDocuments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251102051645_UpdateTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251102051645_UpdateTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251103171854_UpdateChatSessionTableDB4'
)
BEGIN
    ALTER TABLE [ChatSessions] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251103171854_UpdateChatSessionTableDB4'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251103171854_UpdateChatSessionTableDB4', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104165802_UpdateDiscussionPostTableDB2'
)
BEGIN
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionPosts]') AND [c].[name] = N'LikeCount');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionPosts] DROP CONSTRAINT [' + @var23 + '];');
    ALTER TABLE [DiscussionPosts] DROP COLUMN [LikeCount];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104165802_UpdateDiscussionPostTableDB2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251104165802_UpdateDiscussionPostTableDB2', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104165831_UpdateDiscussionCommentTableDB1'
)
BEGIN
    DECLARE @var24 sysname;
    SELECT @var24 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionComments]') AND [c].[name] = N'LikeCount');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionComments] DROP CONSTRAINT [' + @var24 + '];');
    ALTER TABLE [DiscussionComments] DROP COLUMN [LikeCount];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104165831_UpdateDiscussionCommentTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251104165831_UpdateDiscussionCommentTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170131_UpdateDiscussionAttachmentTableDB1'
)
BEGIN
    DECLARE @var25 sysname;
    SELECT @var25 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionAttachments]') AND [c].[name] = N'ContentType');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionAttachments] DROP CONSTRAINT [' + @var25 + '];');
    ALTER TABLE [DiscussionAttachments] DROP COLUMN [ContentType];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170131_UpdateDiscussionAttachmentTableDB1'
)
BEGIN
    DECLARE @var26 sysname;
    SELECT @var26 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionAttachments]') AND [c].[name] = N'FileSize');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionAttachments] DROP CONSTRAINT [' + @var26 + '];');
    ALTER TABLE [DiscussionAttachments] DROP COLUMN [FileSize];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170131_UpdateDiscussionAttachmentTableDB1'
)
BEGIN
    ALTER TABLE [DiscussionAttachments] ADD [DiscussionPostId] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170131_UpdateDiscussionAttachmentTableDB1'
)
BEGIN
    ALTER TABLE [DiscussionAttachments] ADD [FileLink] nvarchar(500) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170131_UpdateDiscussionAttachmentTableDB1'
)
BEGIN
    ALTER TABLE [DiscussionAttachments] ADD [FileType] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170131_UpdateDiscussionAttachmentTableDB1'
)
BEGIN
    CREATE INDEX [IX_DiscussionAttachments_DiscussionPostId] ON [DiscussionAttachments] ([DiscussionPostId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170131_UpdateDiscussionAttachmentTableDB1'
)
BEGIN
    ALTER TABLE [DiscussionAttachments] ADD CONSTRAINT [FK_DiscussionAttachments_DiscussionPosts_DiscussionPostId] FOREIGN KEY ([DiscussionPostId]) REFERENCES [DiscussionPosts] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170131_UpdateDiscussionAttachmentTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251104170131_UpdateDiscussionAttachmentTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170432_CreateDiscussionLikeCountTableDB'
)
BEGIN
    CREATE TABLE [DiscussionLikeCounts] (
        [Id] int NOT NULL IDENTITY,
        [CreatedAt] datetime2 NOT NULL,
        [DiscussionPostId] int NOT NULL,
        [DiscussionCommentId] int NULL,
        [UserId] int NOT NULL,
        CONSTRAINT [PK_DiscussionLikeCounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DiscussionLikeCounts_DiscussionComments_DiscussionCommentId] FOREIGN KEY ([DiscussionCommentId]) REFERENCES [DiscussionComments] ([Id]),
        CONSTRAINT [FK_DiscussionLikeCounts_DiscussionPosts_DiscussionPostId] FOREIGN KEY ([DiscussionPostId]) REFERENCES [DiscussionPosts] ([Id]),
        CONSTRAINT [FK_DiscussionLikeCounts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170432_CreateDiscussionLikeCountTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionLikeCounts_DiscussionCommentId] ON [DiscussionLikeCounts] ([DiscussionCommentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170432_CreateDiscussionLikeCountTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionLikeCounts_DiscussionPostId] ON [DiscussionLikeCounts] ([DiscussionPostId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170432_CreateDiscussionLikeCountTableDB'
)
BEGIN
    CREATE INDEX [IX_DiscussionLikeCounts_UserId] ON [DiscussionLikeCounts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104170432_CreateDiscussionLikeCountTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251104170432_CreateDiscussionLikeCountTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171144_UpdateDiscussionCommentTableDB2'
)
BEGIN
    ALTER TABLE [DiscussionComments] ADD [DiscussionAttachmentId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171144_UpdateDiscussionCommentTableDB2'
)
BEGIN
    CREATE INDEX [IX_DiscussionComments_DiscussionAttachmentId] ON [DiscussionComments] ([DiscussionAttachmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171144_UpdateDiscussionCommentTableDB2'
)
BEGIN
    ALTER TABLE [DiscussionComments] ADD CONSTRAINT [FK_DiscussionComments_DiscussionAttachments_DiscussionAttachmentId] FOREIGN KEY ([DiscussionAttachmentId]) REFERENCES [DiscussionAttachments] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171144_UpdateDiscussionCommentTableDB2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251104171144_UpdateDiscussionCommentTableDB2', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171436_UpdateDiscussionCommentTableDB3'
)
BEGIN
    ALTER TABLE [DiscussionComments] DROP CONSTRAINT [FK_DiscussionComments_DiscussionAttachments_DiscussionAttachmentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171436_UpdateDiscussionCommentTableDB3'
)
BEGIN
    DROP INDEX [IX_DiscussionComments_DiscussionAttachmentId] ON [DiscussionComments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171436_UpdateDiscussionCommentTableDB3'
)
BEGIN
    DROP INDEX [IX_DiscussionAttachments_DiscussionCommentId] ON [DiscussionAttachments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171436_UpdateDiscussionCommentTableDB3'
)
BEGIN
    DECLARE @var27 sysname;
    SELECT @var27 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionComments]') AND [c].[name] = N'DiscussionAttachmentId');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionComments] DROP CONSTRAINT [' + @var27 + '];');
    ALTER TABLE [DiscussionComments] DROP COLUMN [DiscussionAttachmentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171436_UpdateDiscussionCommentTableDB3'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DiscussionAttachments_DiscussionCommentId] ON [DiscussionAttachments] ([DiscussionCommentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171436_UpdateDiscussionCommentTableDB3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251104171436_UpdateDiscussionCommentTableDB3', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171548_UpdateDiscussionAttachmentTableDB2'
)
BEGIN
    ALTER TABLE [DiscussionAttachments] DROP CONSTRAINT [FK_DiscussionAttachments_DiscussionComments_DiscussionCommentId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171548_UpdateDiscussionAttachmentTableDB2'
)
BEGIN
    ALTER TABLE [DiscussionAttachments] DROP CONSTRAINT [FK_DiscussionAttachments_DiscussionPosts_DiscussionPostId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171548_UpdateDiscussionAttachmentTableDB2'
)
BEGIN
    DROP INDEX [IX_DiscussionAttachments_DiscussionCommentId] ON [DiscussionAttachments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171548_UpdateDiscussionAttachmentTableDB2'
)
BEGIN
    DECLARE @var28 sysname;
    SELECT @var28 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionAttachments]') AND [c].[name] = N'DiscussionPostId');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionAttachments] DROP CONSTRAINT [' + @var28 + '];');
    ALTER TABLE [DiscussionAttachments] ALTER COLUMN [DiscussionPostId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171548_UpdateDiscussionAttachmentTableDB2'
)
BEGIN
    DECLARE @var29 sysname;
    SELECT @var29 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionAttachments]') AND [c].[name] = N'DiscussionCommentId');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionAttachments] DROP CONSTRAINT [' + @var29 + '];');
    ALTER TABLE [DiscussionAttachments] ALTER COLUMN [DiscussionCommentId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171548_UpdateDiscussionAttachmentTableDB2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DiscussionAttachments_DiscussionCommentId] ON [DiscussionAttachments] ([DiscussionCommentId]) WHERE [DiscussionCommentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171548_UpdateDiscussionAttachmentTableDB2'
)
BEGIN
    ALTER TABLE [DiscussionAttachments] ADD CONSTRAINT [FK_DiscussionAttachments_DiscussionComments_DiscussionCommentId] FOREIGN KEY ([DiscussionCommentId]) REFERENCES [DiscussionComments] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171548_UpdateDiscussionAttachmentTableDB2'
)
BEGIN
    ALTER TABLE [DiscussionAttachments] ADD CONSTRAINT [FK_DiscussionAttachments_DiscussionPosts_DiscussionPostId] FOREIGN KEY ([DiscussionPostId]) REFERENCES [DiscussionPosts] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251104171548_UpdateDiscussionAttachmentTableDB2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251104171548_UpdateDiscussionAttachmentTableDB2', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251106123430_UpdateChatSessionTableDB5'
)
BEGIN
    DECLARE @var30 sysname;
    SELECT @var30 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChatSessions]') AND [c].[name] = N'Context');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [ChatSessions] DROP CONSTRAINT [' + @var30 + '];');
    ALTER TABLE [ChatSessions] DROP COLUMN [Context];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251106123430_UpdateChatSessionTableDB5'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251106123430_UpdateChatSessionTableDB5', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251107083440_UpdateBookTableDB6'
)
BEGIN
    ALTER TABLE [Books] ADD [UserId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251107083440_UpdateBookTableDB6'
)
BEGIN
    CREATE INDEX [IX_Books_UserId] ON [Books] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251107083440_UpdateBookTableDB6'
)
BEGIN
    ALTER TABLE [Books] ADD CONSTRAINT [FK_Books_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251107083440_UpdateBookTableDB6'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251107083440_UpdateBookTableDB6', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251109084657_UpdateDiscussionLikeCountTableDB1'
)
BEGIN
    ALTER TABLE [DiscussionLikeCounts] DROP CONSTRAINT [FK_DiscussionLikeCounts_DiscussionPosts_DiscussionPostId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251109084657_UpdateDiscussionLikeCountTableDB1'
)
BEGIN
    DECLARE @var31 sysname;
    SELECT @var31 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DiscussionLikeCounts]') AND [c].[name] = N'DiscussionPostId');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [DiscussionLikeCounts] DROP CONSTRAINT [' + @var31 + '];');
    ALTER TABLE [DiscussionLikeCounts] ALTER COLUMN [DiscussionPostId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251109084657_UpdateDiscussionLikeCountTableDB1'
)
BEGIN
    ALTER TABLE [DiscussionLikeCounts] ADD CONSTRAINT [FK_DiscussionLikeCounts_DiscussionPosts_DiscussionPostId] FOREIGN KEY ([DiscussionPostId]) REFERENCES [DiscussionPosts] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251109084657_UpdateDiscussionLikeCountTableDB1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251109084657_UpdateDiscussionLikeCountTableDB1', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251116050741_RemoveDiscussionModerationActionTableDB'
)
BEGIN
    DROP TABLE [DiscussionModerationActions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251116050741_RemoveDiscussionModerationActionTableDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251116050741_RemoveDiscussionModerationActionTableDB', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251122120726_UpdateBookCategoryTableDB5'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BookCategories_Slug] ON [BookCategories] ([Slug]) WHERE [Slug] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251122120726_UpdateBookCategoryTableDB5'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251122120726_UpdateBookCategoryTableDB5', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251122141406_UpdateUserTableDB10'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_UserName] ON [Users] ([UserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251122141406_UpdateUserTableDB10'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251122141406_UpdateUserTableDB10', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251123064658_UpdateUserTableDB11'
)
BEGIN
    DROP INDEX [IX_Users_UserName] ON [Users];
    DECLARE @var32 sysname;
    SELECT @var32 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'UserName');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var32 + '];');
    ALTER TABLE [Users] ALTER COLUMN [UserName] nvarchar(100) NOT NULL;
    CREATE UNIQUE INDEX [IX_Users_UserName] ON [Users] ([UserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251123064658_UpdateUserTableDB11'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251123064658_UpdateUserTableDB11', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124002528_AddBlockReasonToUser'
)
BEGIN
    ALTER TABLE [Users] ADD [BlockReason] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124002528_AddBlockReasonToUser'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251124002528_AddBlockReasonToUser', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124082627_UpdateUserTableDB12'
)
BEGIN
    DECLARE @var33 sysname;
    SELECT @var33 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'Language');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var33 + '];');
    EXEC(N'UPDATE [Users] SET [Language] = N'''' WHERE [Language] IS NULL');
    ALTER TABLE [Users] ALTER COLUMN [Language] nvarchar(50) NOT NULL;
    ALTER TABLE [Users] ADD DEFAULT N'' FOR [Language];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124082627_UpdateUserTableDB12'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251124082627_UpdateUserTableDB12', N'9.0.11');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124083459_UpdateBookCategoryTableDB6'
)
BEGIN
    DROP INDEX [IX_BookCategories_Slug] ON [BookCategories];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124083459_UpdateBookCategoryTableDB6'
)
BEGIN
    DECLARE @var34 sysname;
    SELECT @var34 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BookCategories]') AND [c].[name] = N'Slug');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [BookCategories] DROP CONSTRAINT [' + @var34 + '];');
    EXEC(N'UPDATE [BookCategories] SET [Slug] = N'''' WHERE [Slug] IS NULL');
    ALTER TABLE [BookCategories] ALTER COLUMN [Slug] nvarchar(100) NOT NULL;
    ALTER TABLE [BookCategories] ADD DEFAULT N'' FOR [Slug];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124083459_UpdateBookCategoryTableDB6'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BookCategories_Slug] ON [BookCategories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251124083459_UpdateBookCategoryTableDB6'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251124083459_UpdateBookCategoryTableDB6', N'9.0.11');
END;

COMMIT;
GO


GO
-- Source: DB/StoredProcedure/sp_DeleteBookCategories.sql
-- =============================================
-- Author:      Chong Jun Xiang
-- Create date: 2025-01-23
-- Description: Permanently deletes book categories and reassigns subcategories and books to General category.
--              Handles cascade reassignment before deletion to maintain data integrity.
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[sp_DeleteBookCategories]
    @CategoryIds NVARCHAR(MAX) -- Comma-separated list of category IDs (e.g., '1,2,3')
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ErrorMessage NVARCHAR(4000);
    DECLARE @ErrorSeverity INT;
    DECLARE @ErrorState INT;
    DECLARE @GeneralCategoryId INT;
    DECLARE @DeletedCount INT = 0;
    
    -- Create temp table to store category IDs
    CREATE TABLE #CategoryIdsToDelete (CategoryId INT PRIMARY KEY);
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Find General category (default category)
        SELECT @GeneralCategoryId = Id 
        FROM BookCategories 
        WHERE Slug = 'general' OR LOWER(Name) = 'general';
        
        IF @GeneralCategoryId IS NULL
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 
                0 AS Success,
                'Cannot find the default General category.' AS ErrorMessage,
                0 AS DeletedCount;
            RETURN;
        END
        
        -- Parse comma-separated IDs into temp table, excluding General category
        INSERT INTO #CategoryIdsToDelete (CategoryId)
        SELECT CAST(value AS INT)
        FROM STRING_SPLIT(@CategoryIds, ',')
        WHERE RTRIM(value) <> ''
            AND CAST(value AS INT) <> @GeneralCategoryId;
        
        -- Check if any valid categories to delete
        IF NOT EXISTS (SELECT 1 FROM #CategoryIdsToDelete)
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 
                0 AS Success,
                'The default General category cannot be deleted or no valid categories selected.' AS ErrorMessage,
                0 AS DeletedCount;
            RETURN;
        END
        
        -- Validate categories exist
        IF NOT EXISTS (SELECT 1 FROM BookCategories WHERE Id IN (SELECT CategoryId FROM #CategoryIdsToDelete))
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 
                0 AS Success,
                'One or more specified categories do not exist.' AS ErrorMessage,
                0 AS DeletedCount;
            RETURN;
        END
        
        -- ===== REASSIGNMENT PHASE =====
        
        -- 1. Reassign subcategories to General category
        UPDATE BookCategories
        SET ParentId = @GeneralCategoryId,
            UpdatedAt = GETDATE()
        WHERE ParentId IN (SELECT CategoryId FROM #CategoryIdsToDelete);
        
        -- 2. Handle BookCategoryMap reassignment
        -- Get affected book IDs before deleting maps
        DECLARE @AffectedBooks TABLE (BookId INT PRIMARY KEY);
        INSERT INTO @AffectedBooks (BookId)
        SELECT DISTINCT BookId
        FROM BookCategoryMaps
        WHERE BookCategoryId IN (SELECT CategoryId FROM #CategoryIdsToDelete);
        
        -- Delete BookCategoryMap entries for categories being deleted
        DELETE FROM BookCategoryMaps
        WHERE BookCategoryId IN (SELECT CategoryId FROM #CategoryIdsToDelete);
        
        -- Create new mappings to General category for affected books (if not exists)
        INSERT INTO BookCategoryMaps (BookId, BookCategoryId)
        SELECT ab.BookId, @GeneralCategoryId
        FROM @AffectedBooks ab
        WHERE NOT EXISTS (
            SELECT 1 
            FROM BookCategoryMaps bcm 
            WHERE bcm.BookId = ab.BookId 
                AND bcm.BookCategoryId = @GeneralCategoryId
        );
        
        -- Update Books.UpdatedAt timestamp for affected books
        UPDATE Books
        SET UpdatedAt = GETDATE()
        WHERE Id IN (SELECT BookId FROM @AffectedBooks);
        
        -- ===== DELETION PHASE =====
        
        -- Delete categories
        DELETE FROM BookCategories
        WHERE Id IN (SELECT CategoryId FROM #CategoryIdsToDelete);
        
        SET @DeletedCount = @@ROWCOUNT;
        
        COMMIT TRANSACTION;
        
        -- Return success result
        SELECT 
            1 AS Success,
            NULL AS ErrorMessage,
            @DeletedCount AS DeletedCount;
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        SELECT @ErrorMessage = ERROR_MESSAGE(),
               @ErrorSeverity = ERROR_SEVERITY(),
               @ErrorState = ERROR_STATE();
        
        -- Return error information
        SELECT 
            0 AS Success,
            @ErrorMessage AS ErrorMessage,
            0 AS DeletedCount;
    END CATCH;
    
    -- Cleanup temp tables
    DROP TABLE IF EXISTS #CategoryIdsToDelete;
END;
GO
GO

GO
-- Source: DB/StoredProcedure/sp_DeleteBooks.sql
-- =============================================
-- Author:      Chong Jun Xiang (refactored from C# code)
-- Create date: 2025-01-23
-- Description: Permanently deletes books and all related data with cascade deletion.
--              Returns file paths (Dropbox) for external cleanup.
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[sp_DeleteBooks]
    @BookIds NVARCHAR(MAX) -- Comma-separated list of book IDs (e.g., '1,2,3')
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ErrorMessage NVARCHAR(4000);
    DECLARE @ErrorSeverity INT;
    DECLARE @ErrorState INT;
    
    -- Create temp table to store book IDs
    CREATE TABLE #BookIdsToDelete (BookId INT PRIMARY KEY);
    
    -- Create temp table for file paths to return
    CREATE TABLE #FilesToDelete (
        FileType NVARCHAR(20),
        FilePath NVARCHAR(500),
        FileLink NVARCHAR(1000),
        BookId INT,
        DocumentId INT
    );
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Parse comma-separated IDs into temp table
        INSERT INTO #BookIdsToDelete (BookId)
        SELECT CAST(value AS INT)
        FROM STRING_SPLIT(@BookIds, ',')
        WHERE RTRIM(value) <> '';
        
        -- Validate books exist
        IF NOT EXISTS (SELECT 1 FROM Books WHERE Id IN (SELECT BookId FROM #BookIdsToDelete))
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 
                0 AS Success,
                'No books found to delete.' AS ErrorMessage,
                NULL AS FileType,
                NULL AS FilePath,
                NULL AS FileLink,
                NULL AS BookId,
                NULL AS DocumentId;
            RETURN;
        END
        
        -- Collect file paths for Dropbox cleanup BEFORE deletion
        -- 1. Collect BookDocument paths
        INSERT INTO #FilesToDelete (FileType, FilePath, FileLink, BookId, DocumentId)
        SELECT 
            'Document' AS FileType,
            bd.DocumentPath,
            bd.DocumentLink,
            bd.BookId,
            bd.Id AS DocumentId
        FROM BookDocuments bd
        WHERE bd.BookId IN (SELECT BookId FROM #BookIdsToDelete)
            AND bd.DocumentPath IS NOT NULL;
        
        -- 2. Collect Cover Image paths
        INSERT INTO #FilesToDelete (FileType, FilePath, FileLink, BookId, DocumentId)
        SELECT 
            'CoverImage' AS FileType,
            b.CoverImagePath,
            b.CoverImageLink,
            b.Id AS BookId,
            NULL AS DocumentId
        FROM Books b
        WHERE b.Id IN (SELECT BookId FROM #BookIdsToDelete)
            AND b.CoverImagePath IS NOT NULL
            AND RTRIM(b.CoverImagePath) <> '';
        
        -- ===== CASCADE DELETION BEGINS =====
        
        -- Get discussion IDs related to books
        DECLARE @DiscussionIds TABLE (DiscussionId INT);
        INSERT INTO @DiscussionIds (DiscussionId)
        SELECT d.Id 
        FROM Discussions d
        WHERE d.BookId IN (SELECT BookId FROM #BookIdsToDelete);
        
        -- Get post IDs from discussions
        DECLARE @PostIds TABLE (PostId INT);
        INSERT INTO @PostIds (PostId)
        SELECT dp.Id
        FROM DiscussionPosts dp
        WHERE dp.DiscussionId IN (SELECT DiscussionId FROM @DiscussionIds);
        
        -- Get comment IDs from posts
        DECLARE @CommentIds TABLE (CommentId INT);
        INSERT INTO @CommentIds (CommentId)
        SELECT dc.Id
        FROM DiscussionComments dc
        WHERE dc.DiscussionPostId IN (SELECT PostId FROM @PostIds);
        
        -- Get book document IDs
        DECLARE @BookDocumentIds TABLE (DocumentId INT);
        INSERT INTO @BookDocumentIds (DocumentId)
        SELECT bd.Id
        FROM BookDocuments bd
        WHERE bd.BookId IN (SELECT BookId FROM #BookIdsToDelete);
        
        -- 1. Delete DiscussionAttachments (references Posts and Comments)
        DELETE FROM DiscussionAttachments
        WHERE (DiscussionPostId IN (SELECT PostId FROM @PostIds))
           OR (DiscussionCommentId IN (SELECT CommentId FROM @CommentIds));
        
        -- 2. Delete DiscussionLikeCounts (references Posts and Comments)
        DELETE FROM DiscussionLikeCounts
        WHERE (DiscussionPostId IN (SELECT PostId FROM @PostIds))
           OR (DiscussionCommentId IN (SELECT CommentId FROM @CommentIds));
        
        -- 3. Delete DiscussionComments (references Posts)
        DELETE FROM DiscussionComments
        WHERE DiscussionPostId IN (SELECT PostId FROM @PostIds);
        
        -- 4. Delete DiscussionPosts (references Discussions)
        DELETE FROM DiscussionPosts
        WHERE DiscussionId IN (SELECT DiscussionId FROM @DiscussionIds);
        
        -- 5. Delete Discussions (references Books)
        DELETE FROM Discussions
        WHERE Id IN (SELECT DiscussionId FROM @DiscussionIds);
        
        -- 6. Delete BookHistories (references BookDocuments)
        DELETE FROM BookHistories
        WHERE BookDocumentId IN (SELECT DocumentId FROM @BookDocumentIds);
        
        -- 7. Delete Bookmarks (references BookDocuments)
        DELETE FROM Bookmarks
        WHERE BookDocumentId IN (SELECT DocumentId FROM @BookDocumentIds);
        
        -- 8. Delete BookDocuments (references Books)
        DELETE FROM BookDocuments
        WHERE BookId IN (SELECT BookId FROM #BookIdsToDelete);
        
        -- 9. Delete BookCategoryMaps (references Books)
        DELETE FROM BookCategoryMaps
        WHERE BookId IN (SELECT BookId FROM #BookIdsToDelete);
        
        -- 10. Delete Books (main entity)
        DELETE FROM Books
        WHERE Id IN (SELECT BookId FROM #BookIdsToDelete);
        
        COMMIT TRANSACTION;
        
        -- Return success with file paths for cleanup
        -- Always return at least one success row (even when no files to delete)
        IF EXISTS (SELECT 1 FROM #FilesToDelete)
        BEGIN
            SELECT 
                1 AS Success,
                NULL AS ErrorMessage,
                FileType,
                FilePath,
                FileLink,
                BookId,
                DocumentId
            FROM #FilesToDelete;
        END
        ELSE
        BEGIN
            -- No files to delete, but deletion was successful
            -- Return success with the deleted book IDs for logging purposes
            SELECT 
                1 AS Success,
                NULL AS ErrorMessage,
                NULL AS FileType,
                NULL AS FilePath,
                NULL AS FileLink,
                BookId,
                NULL AS DocumentId
            FROM #BookIdsToDelete;
        END
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        SELECT @ErrorMessage = ERROR_MESSAGE(),
               @ErrorSeverity = ERROR_SEVERITY(),
               @ErrorState = ERROR_STATE();
        
        -- Return error information
        SELECT 
            0 AS Success,
            @ErrorMessage AS ErrorMessage,
            NULL AS FileType,
            NULL AS FilePath,
            NULL AS FileLink,
            NULL AS BookId,
            NULL AS DocumentId;
    END CATCH;
    
    -- Cleanup temp tables
    DROP TABLE IF EXISTS #BookIdsToDelete;
    DROP TABLE IF EXISTS #FilesToDelete;
END;
GO
GO

GO
-- Source: DB/StoredProcedure/sp_DeleteDiscussionComment.sql
-- =============================================
-- Author:      Chong Jun Xiang
-- Create date: 2025-01-23
-- Description: Permanently deletes a discussion comment and all nested reply comments recursively.
--              Handles self-referencing foreign key constraint (ReplyCommentId).
--              Returns attachment file paths for external Dropbox cleanup.
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[sp_DeleteDiscussionComment]
    @CommentId INT,
    @PostId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ErrorMessage NVARCHAR(4000);
    DECLARE @ErrorSeverity INT;
    DECLARE @ErrorState INT;
    
    -- Create temp table for file paths to return
    CREATE TABLE #AttachmentsToDelete (
        FilePath NVARCHAR(500),
        CommentId INT
    );
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Verify comment exists and belongs to the post
        IF NOT EXISTS (SELECT 1 FROM DiscussionComments WHERE Id = @CommentId AND DiscussionPostId = @PostId)
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 
                0 AS Success,
                'Comment not found or does not belong to the specified post.' AS ErrorMessage,
                NULL AS FilePath,
                NULL AS CommentId;
            RETURN;
        END;
        
        -- Use recursive CTE to find all descendant comments (nested replies)
        WITH CommentHierarchy AS (
            -- Anchor: Start with the target comment
            SELECT Id, ReplyCommentId, 0 AS Level
            FROM DiscussionComments
            WHERE Id = @CommentId
            
            UNION ALL
            
            -- Recursive: Find all replies (children)
            SELECT dc.Id, dc.ReplyCommentId, ch.Level + 1
            FROM DiscussionComments dc
            INNER JOIN CommentHierarchy ch ON dc.ReplyCommentId = ch.Id
        )
        -- Store all comment IDs in the hierarchy
        SELECT Id, Level
        INTO #CommentsToDelete
        FROM CommentHierarchy;
        
        -- Collect attachment file paths BEFORE deletion
        INSERT INTO #AttachmentsToDelete (FilePath, CommentId)
        SELECT 
            da.FilePath,
            da.DiscussionCommentId
        FROM DiscussionAttachments da
        WHERE da.DiscussionCommentId IN (SELECT Id FROM #CommentsToDelete)
            AND da.FilePath IS NOT NULL
            AND RTRIM(da.FilePath) <> '';
        
        -- ===== CASCADE DELETION BEGINS =====
        -- Delete from deepest level to shallowest to avoid FK constraint violations
        
        -- 1. Delete DiscussionAttachments for all comments in hierarchy
        DELETE FROM DiscussionAttachments
        WHERE DiscussionCommentId IN (SELECT Id FROM #CommentsToDelete);
        
        -- 2. Delete DiscussionLikeCounts for all comments in hierarchy
        DELETE FROM DiscussionLikeCounts
        WHERE DiscussionCommentId IN (SELECT Id FROM #CommentsToDelete);
        
        -- 3. Delete comments from deepest level first (children before parents)
        -- This respects the self-referencing FK constraint
        DECLARE @MaxLevel INT;
        SELECT @MaxLevel = MAX(Level) FROM #CommentsToDelete;
        
        WHILE @MaxLevel >= 0
        BEGIN
            DELETE FROM DiscussionComments
            WHERE Id IN (
                SELECT Id 
                FROM #CommentsToDelete 
                WHERE Level = @MaxLevel
            );
            
            SET @MaxLevel = @MaxLevel - 1;
        END
        
        COMMIT TRANSACTION;
        
        -- Return success with attachment file paths for cleanup
        -- If no attachments, return single success row
        IF NOT EXISTS (SELECT 1 FROM #AttachmentsToDelete)
        BEGIN
            SELECT 
                1 AS Success,
                NULL AS ErrorMessage,
                NULL AS FilePath,
                NULL AS CommentId;
        END
        ELSE
        BEGIN
            SELECT 
                1 AS Success,
                NULL AS ErrorMessage,
                FilePath,
                CommentId
            FROM #AttachmentsToDelete;
        END
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        SELECT @ErrorMessage = ERROR_MESSAGE(),
               @ErrorSeverity = ERROR_SEVERITY(),
               @ErrorState = ERROR_STATE();
        
        -- Return error information
        SELECT 
            0 AS Success,
            @ErrorMessage AS ErrorMessage,
            NULL AS FilePath,
            NULL AS CommentId;
    END CATCH;
    
    -- Cleanup temp tables
    DROP TABLE IF EXISTS #CommentsToDelete;
    DROP TABLE IF EXISTS #AttachmentsToDelete;
END;
GO
GO

GO
-- Source: DB/StoredProcedure/sp_DeleteDiscussionPosts.sql
-- =============================================
-- Author:      Chong Jun Xiang
-- Create date: 2025-01-23
-- Description: Permanently deletes discussion posts and all related data with cascade deletion.
--              Returns attachment file paths (Dropbox) for external cleanup.
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[sp_DeleteDiscussionPosts]
    @PostIds NVARCHAR(MAX) -- Comma-separated list of post IDs (e.g., '1,2,3')
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ErrorMessage NVARCHAR(4000);
    DECLARE @ErrorSeverity INT;
    DECLARE @ErrorState INT;
    
    -- Create temp table to store post IDs
    CREATE TABLE #PostIdsToDelete (PostId INT PRIMARY KEY);
    
    -- Create temp table for file paths to return
    CREATE TABLE #AttachmentsToDelete (
        FileType NVARCHAR(20),
        FilePath NVARCHAR(500),
        PostId INT,
        CommentId INT
    );
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Parse comma-separated IDs into temp table
        INSERT INTO #PostIdsToDelete (PostId)
        SELECT CAST(value AS INT)
        FROM STRING_SPLIT(@PostIds, ',')
        WHERE RTRIM(value) <> '';
        
        -- Validate posts exist
        IF NOT EXISTS (SELECT 1 FROM DiscussionPosts WHERE Id IN (SELECT PostId FROM #PostIdsToDelete))
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 
                0 AS Success,
                'One or more specified posts do not exist.' AS ErrorMessage,
                NULL AS FileType,
                NULL AS FilePath,
                NULL AS PostId,
                NULL AS CommentId;
            RETURN;
        END
        
        -- ===== FILE COLLECTION PHASE =====
        
        -- 1. Collect post attachment paths
        INSERT INTO #AttachmentsToDelete (FileType, FilePath, PostId, CommentId)
        SELECT 
            'PostAttachment' AS FileType,
            da.FilePath,
            da.DiscussionPostId AS PostId,
            NULL AS CommentId
        FROM DiscussionAttachments da
        WHERE da.DiscussionPostId IN (SELECT PostId FROM #PostIdsToDelete)
            AND da.FilePath IS NOT NULL
            AND RTRIM(da.FilePath) <> '';
        
        -- 2. Get comment IDs for those posts
        DECLARE @CommentIds TABLE (CommentId INT);
        INSERT INTO @CommentIds (CommentId)
        SELECT dc.Id
        FROM DiscussionComments dc
        WHERE dc.DiscussionPostId IN (SELECT PostId FROM #PostIdsToDelete);
        
        -- 3. Collect comment attachment paths
        INSERT INTO #AttachmentsToDelete (FileType, FilePath, PostId, CommentId)
        SELECT 
            'CommentAttachment' AS FileType,
            da.FilePath,
            NULL AS PostId,
            da.DiscussionCommentId AS CommentId
        FROM DiscussionAttachments da
        WHERE da.DiscussionCommentId IN (SELECT CommentId FROM @CommentIds)
            AND da.FilePath IS NOT NULL
            AND RTRIM(da.FilePath) <> '';
        
        -- ===== CASCADE DELETION BEGINS =====
        
        -- 1. Delete DiscussionLikeCounts for comments
        DELETE FROM DiscussionLikeCounts
        WHERE DiscussionCommentId IN (SELECT CommentId FROM @CommentIds);
        
        -- 2. Delete DiscussionLikeCounts for posts
        DELETE FROM DiscussionLikeCounts
        WHERE DiscussionPostId IN (SELECT PostId FROM #PostIdsToDelete);
        
        -- 3. Delete DiscussionAttachments for comments
        DELETE FROM DiscussionAttachments
        WHERE DiscussionCommentId IN (SELECT CommentId FROM @CommentIds);
        
        -- 4. Delete DiscussionAttachments for posts
        DELETE FROM DiscussionAttachments
        WHERE DiscussionPostId IN (SELECT PostId FROM #PostIdsToDelete);
        
        -- 5. Delete DiscussionComments
        DELETE FROM DiscussionComments
        WHERE DiscussionPostId IN (SELECT PostId FROM #PostIdsToDelete);
        
        -- 6. Delete DiscussionPosts
        DELETE FROM DiscussionPosts
        WHERE Id IN (SELECT PostId FROM #PostIdsToDelete);
        
        COMMIT TRANSACTION;
        
        -- Return success with attachment file paths for cleanup
        -- If no attachments, return single success row
        IF NOT EXISTS (SELECT 1 FROM #AttachmentsToDelete)
        BEGIN
            SELECT 
                1 AS Success,
                NULL AS ErrorMessage,
                NULL AS FileType,
                NULL AS FilePath,
                NULL AS PostId,
                NULL AS CommentId;
        END
        ELSE
        BEGIN
            SELECT 
                1 AS Success,
                NULL AS ErrorMessage,
                FileType,
                FilePath,
                PostId,
                CommentId
            FROM #AttachmentsToDelete;
        END
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        SELECT @ErrorMessage = ERROR_MESSAGE(),
               @ErrorSeverity = ERROR_SEVERITY(),
               @ErrorState = ERROR_STATE();
        
        -- Return error information
        SELECT 
            0 AS Success,
            @ErrorMessage AS ErrorMessage,
            NULL AS FileType,
            NULL AS FilePath,
            NULL AS PostId,
            NULL AS CommentId;
    END CATCH;
    
    -- Cleanup temp tables
    DROP TABLE IF EXISTS #PostIdsToDelete;
    DROP TABLE IF EXISTS #AttachmentsToDelete;
END;
GO
GO

GO
-- Source: DB/StoredProcedure/sp_DeleteUserDiscussions.sql
-- =============================================
-- Author:      Chong Jun Xiang
-- Create date: 2025-11-28
-- Description: Permanently deletes all discussion posts and comments created by specified users.
--              Called when users are permanently deleted.
--              Returns attachment file paths (Dropbox) for external cleanup.
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[sp_DeleteUserDiscussions]
    @UserIds NVARCHAR(MAX) -- Comma-separated list of user IDs (e.g., '1,2,3')
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ErrorMessage NVARCHAR(4000);
    DECLARE @ErrorSeverity INT;
    DECLARE @ErrorState INT;
    
    -- Create temp table to store user IDs
    CREATE TABLE #UserIdsToProcess (UserId INT PRIMARY KEY);
    
    -- Create temp table for file paths to return
    CREATE TABLE #AttachmentsToDelete (
        FileType NVARCHAR(20),
        FilePath NVARCHAR(500),
        PostId INT,
        CommentId INT
    );
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Parse comma-separated IDs into temp table
        INSERT INTO #UserIdsToProcess (UserId)
        SELECT CAST(value AS INT)
        FROM STRING_SPLIT(@UserIds, ',')
        WHERE RTRIM(value) <> '';
        
        -- Check if any user IDs were provided
        IF NOT EXISTS (SELECT 1 FROM #UserIdsToProcess)
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 
                1 AS Success,
                NULL AS ErrorMessage,
                NULL AS FileType,
                NULL AS FilePath,
                NULL AS PostId,
                NULL AS CommentId;
            RETURN;
        END
        
        -- ===== GET ALL POSTS AND COMMENTS BY USERS =====
        
        -- Get all post IDs created by the users
        CREATE TABLE #PostIdsToDelete (PostId INT PRIMARY KEY);
        INSERT INTO #PostIdsToDelete (PostId)
        SELECT Id
        FROM DiscussionPosts
        WHERE UserId IN (SELECT UserId FROM #UserIdsToProcess);
        
        -- Get all comment IDs created by the users (regardless of post author)
        CREATE TABLE #CommentIdsToDelete (CommentId INT, Level INT DEFAULT 0);
        
        -- First, get direct comments by the users
        INSERT INTO #CommentIdsToDelete (CommentId, Level)
        SELECT Id, 0
        FROM DiscussionComments
        WHERE UserId IN (SELECT UserId FROM #UserIdsToProcess);
        
        -- Get all comments on posts to be deleted (including nested replies)
        INSERT INTO #CommentIdsToDelete (CommentId, Level)
        SELECT dc.Id, 0
        FROM DiscussionComments dc
        WHERE dc.DiscussionPostId IN (SELECT PostId FROM #PostIdsToDelete)
            AND dc.Id NOT IN (SELECT CommentId FROM #CommentIdsToDelete);
        
        -- Recursively get all reply comments to comments being deleted
        DECLARE @RowCount INT = 1;
        DECLARE @CurrentLevel INT = 0;
        
        WHILE @RowCount > 0
        BEGIN
            INSERT INTO #CommentIdsToDelete (CommentId, Level)
            SELECT dc.Id, @CurrentLevel + 1
            FROM DiscussionComments dc
            WHERE dc.ReplyCommentId IN (
                SELECT CommentId 
                FROM #CommentIdsToDelete 
                WHERE Level = @CurrentLevel
            )
            AND dc.Id NOT IN (SELECT CommentId FROM #CommentIdsToDelete);
            
            SET @RowCount = @@ROWCOUNT;
            SET @CurrentLevel = @CurrentLevel + 1;
        END
        
        -- ===== FILE COLLECTION PHASE =====
        
        -- 1. Collect post attachment paths
        INSERT INTO #AttachmentsToDelete (FileType, FilePath, PostId, CommentId)
        SELECT 
            'PostAttachment' AS FileType,
            da.FilePath,
            da.DiscussionPostId AS PostId,
            NULL AS CommentId
        FROM DiscussionAttachments da
        WHERE da.DiscussionPostId IN (SELECT PostId FROM #PostIdsToDelete)
            AND da.FilePath IS NOT NULL
            AND RTRIM(da.FilePath) <> '';
        
        -- 2. Collect comment attachment paths
        INSERT INTO #AttachmentsToDelete (FileType, FilePath, PostId, CommentId)
        SELECT 
            'CommentAttachment' AS FileType,
            da.FilePath,
            NULL AS PostId,
            da.DiscussionCommentId AS CommentId
        FROM DiscussionAttachments da
        WHERE da.DiscussionCommentId IN (SELECT CommentId FROM #CommentIdsToDelete)
            AND da.FilePath IS NOT NULL
            AND RTRIM(da.FilePath) <> '';
        
        -- ===== CASCADE DELETION BEGINS =====
        
        -- 1. Delete DiscussionLikeCounts for comments
        DELETE FROM DiscussionLikeCounts
        WHERE DiscussionCommentId IN (SELECT CommentId FROM #CommentIdsToDelete);
        
        -- 2. Delete DiscussionLikeCounts for posts
        DELETE FROM DiscussionLikeCounts
        WHERE DiscussionPostId IN (SELECT PostId FROM #PostIdsToDelete);
        
        -- 3. Delete DiscussionAttachments for comments
        DELETE FROM DiscussionAttachments
        WHERE DiscussionCommentId IN (SELECT CommentId FROM #CommentIdsToDelete);
        
        -- 4. Delete DiscussionAttachments for posts
        DELETE FROM DiscussionAttachments
        WHERE DiscussionPostId IN (SELECT PostId FROM #PostIdsToDelete);
        
        -- 5. Delete DiscussionComments from deepest level first (children before parents)
        -- This respects the self-referencing FK constraint
        DECLARE @MaxLevel INT;
        SELECT @MaxLevel = ISNULL(MAX(Level), -1) FROM #CommentIdsToDelete;
        
        WHILE @MaxLevel >= 0
        BEGIN
            DELETE FROM DiscussionComments
            WHERE Id IN (
                SELECT CommentId 
                FROM #CommentIdsToDelete 
                WHERE Level = @MaxLevel
            );
            
            SET @MaxLevel = @MaxLevel - 1;
        END
        
        -- 6. Delete DiscussionPosts
        DELETE FROM DiscussionPosts
        WHERE Id IN (SELECT PostId FROM #PostIdsToDelete);
        
        COMMIT TRANSACTION;
        
        -- Return success with attachment file paths for cleanup
        -- If no attachments, return single success row
        IF NOT EXISTS (SELECT 1 FROM #AttachmentsToDelete)
        BEGIN
            SELECT 
                1 AS Success,
                NULL AS ErrorMessage,
                NULL AS FileType,
                NULL AS FilePath,
                NULL AS PostId,
                NULL AS CommentId;
        END
        ELSE
        BEGIN
            SELECT 
                1 AS Success,
                NULL AS ErrorMessage,
                FileType,
                FilePath,
                PostId,
                CommentId
            FROM #AttachmentsToDelete;
        END
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        SELECT @ErrorMessage = ERROR_MESSAGE(),
               @ErrorSeverity = ERROR_SEVERITY(),
               @ErrorState = ERROR_STATE();
        
        -- Return error information
        SELECT 
            0 AS Success,
            @ErrorMessage AS ErrorMessage,
            NULL AS FileType,
            NULL AS FilePath,
            NULL AS PostId,
            NULL AS CommentId;
    END CATCH;
    
    -- Cleanup temp tables
    DROP TABLE IF EXISTS #UserIdsToProcess;
    DROP TABLE IF EXISTS #PostIdsToDelete;
    DROP TABLE IF EXISTS #CommentIdsToDelete;
    DROP TABLE IF EXISTS #AttachmentsToDelete;
END;
GO
GO
