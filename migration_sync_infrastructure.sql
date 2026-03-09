-- ============================================================================
-- MarcoERP — Sync Infrastructure Migration
-- Generated: 2026-03-06
-- Purpose: Add SyncVersion column to all SoftDeletableEntity tables,
--          create 3 new sync tables (SyncDevices, SyncConflicts, IdempotencyRecords)
-- ============================================================================
-- IMPORTANT: Run this against database [MarcoERP] on .\SQL2022
-- Always take a backup before running: BACKUP DATABASE [MarcoERP] TO DISK = 'MarcoERP_PreSync.bak'
-- ============================================================================

USE [MarcoERP];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ============================================================================
-- PART 0: Create GlobalSyncVersion SQL SEQUENCE
-- ============================================================================
-- Replaces the in-memory counter with an atomic, multi-instance-safe SEQUENCE.
-- Seed value is computed from the current MAX(SyncVersion) across all tables.

IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'GlobalSyncVersion' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    -- Find current max SyncVersion to seed the sequence
    DECLARE @maxVer BIGINT = 0;
    DECLARE @seekSql NVARCHAR(MAX);
    DECLARE @seekTable NVARCHAR(256);
    DECLARE @tv BIGINT;

    DECLARE seedCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME)
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE COLUMN_NAME = 'SyncVersion';

    OPEN seedCursor;
    FETCH NEXT FROM seedCursor INTO @seekTable;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @seekSql = N'SELECT @mv = ISNULL(MAX(SyncVersion), 0) FROM ' + @seekTable;
        EXEC sp_executesql @seekSql, N'@mv BIGINT OUTPUT', @mv = @tv OUTPUT;
        IF @tv > @maxVer SET @maxVer = @tv;
        FETCH NEXT FROM seedCursor INTO @seekTable;
    END

    CLOSE seedCursor;
    DEALLOCATE seedCursor;

    -- Create the sequence starting after the current maximum
    DECLARE @createSeqSql NVARCHAR(MAX) = N'CREATE SEQUENCE dbo.GlobalSyncVersion AS BIGINT START WITH '
        + CAST(@maxVer + 1 AS NVARCHAR(20))
        + N' INCREMENT BY 1 NO CYCLE NO CACHE;';
    EXEC sp_executesql @createSeqSql;

    PRINT 'Created SEQUENCE dbo.GlobalSyncVersion starting at ' + CAST(@maxVer + 1 AS NVARCHAR(20));
END
ELSE
BEGIN
    PRINT 'SEQUENCE dbo.GlobalSyncVersion already exists — skipping.';
END;
GO

-- ============================================================================
-- PART 1: Add SyncVersion column to all SoftDeletableEntity tables (20 tables)
-- ============================================================================
-- These tables inherit from SoftDeletableEntity (directly or via CompanyAwareEntity)
-- SyncVersion: bigint NOT NULL DEFAULT 0
-- Used by SyncVersionInterceptor to auto-increment on every insert/update/soft-delete

-- ── Inventory ──
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'SyncVersion')
    ALTER TABLE [Products] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_Products_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Warehouses') AND name = 'SyncVersion')
    ALTER TABLE [Warehouses] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_Warehouses_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryAdjustments') AND name = 'SyncVersion')
    ALTER TABLE [InventoryAdjustments] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_InventoryAdjustments_SyncVersion] DEFAULT (0);
GO

-- ── Sales ──
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customers') AND name = 'SyncVersion')
    ALTER TABLE [Customers] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_Customers_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesInvoices') AND name = 'SyncVersion')
    ALTER TABLE [SalesInvoices] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_SalesInvoices_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesReturns') AND name = 'SyncVersion')
    ALTER TABLE [SalesReturns] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_SalesReturns_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesQuotations') AND name = 'SyncVersion')
    ALTER TABLE [SalesQuotations] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_SalesQuotations_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SalesRepresentatives') AND name = 'SyncVersion')
    ALTER TABLE [SalesRepresentatives] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_SalesRepresentatives_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PriceLists') AND name = 'SyncVersion')
    ALTER TABLE [PriceLists] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_PriceLists_SyncVersion] DEFAULT (0);
GO

-- ── Purchases ──
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Suppliers') AND name = 'SyncVersion')
    ALTER TABLE [Suppliers] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_Suppliers_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseInvoices') AND name = 'SyncVersion')
    ALTER TABLE [PurchaseInvoices] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_PurchaseInvoices_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseReturns') AND name = 'SyncVersion')
    ALTER TABLE [PurchaseReturns] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_PurchaseReturns_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseQuotations') AND name = 'SyncVersion')
    ALTER TABLE [PurchaseQuotations] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_PurchaseQuotations_SyncVersion] DEFAULT (0);
GO

-- ── Treasury ──
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Cashboxes') AND name = 'SyncVersion')
    ALTER TABLE [Cashboxes] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_Cashboxes_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CashReceipts') AND name = 'SyncVersion')
    ALTER TABLE [CashReceipts] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_CashReceipts_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CashPayments') AND name = 'SyncVersion')
    ALTER TABLE [CashPayments] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_CashPayments_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CashTransfers') AND name = 'SyncVersion')
    ALTER TABLE [CashTransfers] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_CashTransfers_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('BankAccounts') AND name = 'SyncVersion')
    ALTER TABLE [BankAccounts] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_BankAccounts_SyncVersion] DEFAULT (0);
GO

-- ── Accounting ──
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Accounts') AND name = 'SyncVersion')
    ALTER TABLE [Accounts] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_Accounts_SyncVersion] DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JournalEntries') AND name = 'SyncVersion')
    ALTER TABLE [JournalEntries] ADD [SyncVersion] BIGINT NOT NULL CONSTRAINT [DF_JournalEntries_SyncVersion] DEFAULT (0);
GO


-- ============================================================================
-- PART 2: Create SyncVersion indexes on all 20 tables
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_Products_SyncVersion] ON [Products] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Warehouses_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_Warehouses_SyncVersion] ON [Warehouses] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryAdjustments_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_InventoryAdjustments_SyncVersion] ON [InventoryAdjustments] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Customers_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_Customers_SyncVersion] ON [Customers] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesInvoices_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_SalesInvoices_SyncVersion] ON [SalesInvoices] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesReturns_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_SalesReturns_SyncVersion] ON [SalesReturns] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesQuotations_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_SalesQuotations_SyncVersion] ON [SalesQuotations] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesRepresentatives_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_SalesRepresentatives_SyncVersion] ON [SalesRepresentatives] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PriceLists_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_PriceLists_SyncVersion] ON [PriceLists] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Suppliers_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_Suppliers_SyncVersion] ON [Suppliers] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseInvoices_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_PurchaseInvoices_SyncVersion] ON [PurchaseInvoices] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseReturns_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_PurchaseReturns_SyncVersion] ON [PurchaseReturns] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseQuotations_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_PurchaseQuotations_SyncVersion] ON [PurchaseQuotations] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cashboxes_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_Cashboxes_SyncVersion] ON [Cashboxes] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CashReceipts_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_CashReceipts_SyncVersion] ON [CashReceipts] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CashPayments_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_CashPayments_SyncVersion] ON [CashPayments] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CashTransfers_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_CashTransfers_SyncVersion] ON [CashTransfers] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BankAccounts_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_BankAccounts_SyncVersion] ON [BankAccounts] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Accounts_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_Accounts_SyncVersion] ON [Accounts] ([SyncVersion]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JournalEntries_SyncVersion')
    CREATE NONCLUSTERED INDEX [IX_JournalEntries_SyncVersion] ON [JournalEntries] ([SyncVersion]);
GO


-- ============================================================================
-- PART 3: Create SyncDevices table
-- ============================================================================
-- Tracks registered sync clients (mobile devices, desktops)
-- Each device has a checkpoint (LastSyncVersion) for delta sync

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SyncDevices') AND type = 'U')
BEGIN
    CREATE TABLE [SyncDevices] (
        [Id]               INT            IDENTITY(1,1) NOT NULL,
        [DeviceId]         NVARCHAR(100)  NOT NULL,
        [DeviceName]       NVARCHAR(200)  NOT NULL,
        [DeviceType]       NVARCHAR(50)   NOT NULL,
        [UserId]           INT            NOT NULL,
        [LastSyncVersion]  BIGINT         NOT NULL CONSTRAINT [DF_SyncDevices_LastSyncVersion] DEFAULT (0),
        [LastSyncAt]       DATETIME2      NULL,
        [IsActive]         BIT            NOT NULL CONSTRAINT [DF_SyncDevices_IsActive] DEFAULT (1),
        -- AuditableEntity fields
        [CreatedAt]        DATETIME2      NOT NULL,
        [CreatedBy]        NVARCHAR(256)  NULL,
        [ModifiedAt]       DATETIME2      NULL,
        [ModifiedBy]       NVARCHAR(256)  NULL,
        -- BaseEntity concurrency token
        [RowVersion]       ROWVERSION     NOT NULL,

        CONSTRAINT [PK_SyncDevices] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_SyncDevices_DeviceId]
        ON [SyncDevices] ([DeviceId]);

    CREATE NONCLUSTERED INDEX [IX_SyncDevices_UserId]
        ON [SyncDevices] ([UserId]);
END;
GO


-- ============================================================================
-- PART 4: Create SyncConflicts table
-- ============================================================================
-- Logs conflicts detected during push sync (server-wins strategy applied automatically)
-- Enables audit trail and manual review of overridden changes

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SyncConflicts') AND type = 'U')
BEGIN
    CREATE TABLE [SyncConflicts] (
        [Id]            INT            IDENTITY(1,1) NOT NULL,
        [EntityType]    NVARCHAR(200)  NOT NULL,
        [EntityId]      INT            NOT NULL,
        [DeviceId]      NVARCHAR(100)  NOT NULL,
        [ClientData]    NVARCHAR(MAX)  NOT NULL,
        [ServerData]    NVARCHAR(MAX)  NOT NULL,
        [Resolution]    NVARCHAR(50)   NOT NULL,
        [OccurredAt]    DATETIME2      NOT NULL,
        -- BaseEntity concurrency token
        [RowVersion]    ROWVERSION     NOT NULL,

        CONSTRAINT [PK_SyncConflicts] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_SyncConflicts_Entity]
        ON [SyncConflicts] ([EntityType], [EntityId]);

    CREATE NONCLUSTERED INDEX [IX_SyncConflicts_DeviceId]
        ON [SyncConflicts] ([DeviceId]);
END;
GO


-- ============================================================================
-- PART 5: Create IdempotencyRecords table
-- ============================================================================
-- Prevents duplicate operations from mobile clients retrying after network timeouts
-- Records expire after 24 hours

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('IdempotencyRecords') AND type = 'U')
BEGIN
    CREATE TABLE [IdempotencyRecords] (
        [Id]                  INT            IDENTITY(1,1) NOT NULL,
        [IdempotencyKey]      NVARCHAR(100)  NOT NULL,
        [RequestPath]         NVARCHAR(500)  NULL,
        [RequestBody]         NVARCHAR(MAX)  NULL,
        [ResponseStatusCode]  INT            NOT NULL CONSTRAINT [DF_IdempotencyRecords_StatusCode] DEFAULT (0),
        [ResponseBody]        NVARCHAR(MAX)  NULL,
        [CreatedAt]           DATETIME2      NOT NULL,
        [ExpiresAt]           DATETIME2      NOT NULL,
        -- BaseEntity concurrency token
        [RowVersion]          ROWVERSION     NOT NULL,

        CONSTRAINT [PK_IdempotencyRecords] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_IdempotencyRecords_Key]
        ON [IdempotencyRecords] ([IdempotencyKey]);

    CREATE NONCLUSTERED INDEX [IX_IdempotencyRecords_ExpiresAt]
        ON [IdempotencyRecords] ([ExpiresAt]);
END;
GO

-- ============================================================================
-- PART 5b: Add UserId column to IdempotencyRecords (user-scoped idempotency)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IdempotencyRecords') AND name = 'UserId')
BEGIN
    ALTER TABLE [IdempotencyRecords] ADD [UserId] INT NULL;

    -- Replace old single-key index with composite (Key + UserId)
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IdempotencyRecords_Key' AND object_id = OBJECT_ID('IdempotencyRecords'))
        DROP INDEX [IX_IdempotencyRecords_Key] ON [IdempotencyRecords];

    CREATE UNIQUE NONCLUSTERED INDEX [IX_IdempotencyRecords_Key_UserId]
        ON [IdempotencyRecords] ([IdempotencyKey], [UserId]);
END;
GO


-- ============================================================================
-- PART 6: Verification queries
-- ============================================================================
-- Run these after migration to verify everything was applied correctly

PRINT '=== Verifying SyncVersion columns ===';
SELECT 
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType
FROM sys.columns c
JOIN sys.tables t ON c.object_id = t.object_id
JOIN sys.types ty ON c.system_type_id = ty.system_type_id
WHERE c.name = 'SyncVersion'
ORDER BY t.name;

PRINT '=== Verifying SyncVersion indexes ===';
SELECT 
    t.name AS TableName,
    i.name AS IndexName
FROM sys.indexes i
JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name LIKE 'IX_%_SyncVersion'
ORDER BY t.name;

PRINT '=== Verifying new sync tables ===';
SELECT name AS TableName 
FROM sys.tables 
WHERE name IN ('SyncDevices', 'SyncConflicts', 'IdempotencyRecords')
ORDER BY name;

COMMIT TRANSACTION;
PRINT '=== Sync infrastructure migration completed successfully ===';
GO
