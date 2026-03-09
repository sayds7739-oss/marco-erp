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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE TABLE [Accounts] (
        [Id] int NOT NULL IDENTITY,
        [AccountCode] varchar(4) NOT NULL,
        [AccountNameAr] nvarchar(200) NOT NULL,
        [AccountNameEn] nvarchar(200) NULL,
        [AccountType] int NOT NULL,
        [NormalBalance] int NOT NULL,
        [ParentAccountId] int NULL,
        [Level] int NOT NULL,
        [IsLeaf] bit NOT NULL DEFAULT CAST(1 AS bit),
        [AllowPosting] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsSystemAccount] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CurrencyCode] varchar(3) NOT NULL,
        [Description] nvarchar(500) NULL,
        [HasPostings] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Accounts_Accounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] int NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [PerformedBy] nvarchar(100) NOT NULL,
        [Details] nvarchar(4000) NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE TABLE [FiscalYears] (
        [Id] int NOT NULL IDENTITY,
        [Year] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Status] int NOT NULL,
        [ClosedAt] datetime2 NULL,
        [ClosedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_FiscalYears] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE TABLE [CodeSequences] (
        [Id] int NOT NULL IDENTITY,
        [DocumentType] varchar(20) NOT NULL,
        [FiscalYearId] int NOT NULL,
        [Prefix] varchar(30) NOT NULL,
        [CurrentSequence] int NOT NULL DEFAULT 0,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_CodeSequences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CodeSequences_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [FiscalYears] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE TABLE [FiscalPeriods] (
        [Id] int NOT NULL IDENTITY,
        [FiscalYearId] int NOT NULL,
        [PeriodNumber] int NOT NULL,
        [Year] int NOT NULL,
        [Month] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Status] int NOT NULL,
        [LockedAt] datetime2 NULL,
        [LockedBy] nvarchar(100) NULL,
        [UnlockReason] nvarchar(500) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_FiscalPeriods] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FiscalPeriods_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [FiscalYears] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE TABLE [JournalEntries] (
        [Id] int NOT NULL IDENTITY,
        [JournalNumber] varchar(20) NULL,
        [DraftCode] varchar(20) NOT NULL,
        [JournalDate] date NOT NULL,
        [PostingDate] datetime2 NULL,
        [Description] nvarchar(500) NOT NULL,
        [ReferenceNumber] nvarchar(100) NULL,
        [Status] int NOT NULL,
        [SourceType] int NOT NULL,
        [SourceId] int NULL,
        [FiscalYearId] int NOT NULL,
        [FiscalPeriodId] int NOT NULL,
        [CostCenterId] int NULL,
        [ReversedEntryId] int NULL,
        [ReversalEntryId] int NULL,
        [AdjustedEntryId] int NULL,
        [ReversalReason] nvarchar(500) NULL,
        [PostedBy] nvarchar(100) NULL,
        [TotalDebit] decimal(18,2) NOT NULL,
        [TotalCredit] decimal(18,2) NOT NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_JournalEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JournalEntries_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [FiscalPeriods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_JournalEntries_FiscalYears_FiscalYearId] FOREIGN KEY ([FiscalYearId]) REFERENCES [FiscalYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_JournalEntries_JournalEntries_ReversedEntryId] FOREIGN KEY ([ReversedEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE TABLE [JournalEntryLines] (
        [Id] int NOT NULL IDENTITY,
        [JournalEntryId] int NOT NULL,
        [LineNumber] int NOT NULL,
        [AccountId] int NOT NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CostCenterId] int NULL,
        [WarehouseId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_JournalEntryLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JournalEntryLines_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_JournalEntryLines_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Accounts_AccountCode] ON [Accounts] ([AccountCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Accounts_AccountType] ON [Accounts] ([AccountType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Accounts_IsActive] ON [Accounts] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Accounts_ParentAccountId] ON [Accounts] ([ParentAccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Accounts_Postable] ON [Accounts] ([IsLeaf], [AllowPosting], [IsActive]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Entity] ON [AuditLogs] ([EntityType], [EntityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_PerformedBy] ON [AuditLogs] ([PerformedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CodeSequences_DocType_FiscalYear] ON [CodeSequences] ([DocumentType], [FiscalYearId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CodeSequences_FiscalYearId] ON [CodeSequences] ([FiscalYearId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FiscalPeriods_DateRange] ON [FiscalPeriods] ([StartDate], [EndDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FiscalPeriods_Status] ON [FiscalPeriods] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FiscalPeriods_Year_Month] ON [FiscalPeriods] ([FiscalYearId], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FiscalPeriods_Year_PeriodNumber] ON [FiscalPeriods] ([FiscalYearId], [PeriodNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FiscalYears_Status] ON [FiscalYears] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FiscalYears_Year] ON [FiscalYears] ([Year]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JournalEntries_DraftCode] ON [JournalEntries] ([DraftCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_FiscalPeriodId] ON [JournalEntries] ([FiscalPeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_FiscalYearId] ON [JournalEntries] ([FiscalYearId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_JournalDate] ON [JournalEntries] ([JournalDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_JournalEntries_JournalNumber] ON [JournalEntries] ([JournalNumber]) WHERE [JournalNumber] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_JournalEntries_ReversedEntryId] ON [JournalEntries] ([ReversedEntryId]) WHERE [ReversedEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_SourceType] ON [JournalEntries] ([SourceType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_Status] ON [JournalEntries] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_JournalEntries_Year_Status] ON [JournalEntries] ([FiscalYearId], [Status]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JournalEntryLines_AccountId] ON [JournalEntryLines] ([AccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JournalEntryLines_Entry_LineNumber] ON [JournalEntryLines] ([JournalEntryId], [LineNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_JournalEntryLines_JournalEntryId] ON [JournalEntryLines] ([JournalEntryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208175902_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260208175902_InitialCreate', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NULL,
        [ParentCategoryId] int NULL,
        [Level] int NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Description] nvarchar(500) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_Categories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE TABLE [Units] (
        [Id] int NOT NULL IDENTITY,
        [NameAr] nvarchar(50) NOT NULL,
        [NameEn] nvarchar(50) NULL,
        [AbbreviationAr] nvarchar(10) NOT NULL,
        [AbbreviationEn] nvarchar(10) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Units] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE TABLE [Warehouses] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(10) NOT NULL,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NULL,
        [Address] nvarchar(300) NULL,
        [Phone] nvarchar(20) NULL,
        [AccountId] int NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Warehouses] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [CategoryId] int NOT NULL,
        [BaseUnitId] int NOT NULL,
        [CostPrice] decimal(18,4) NOT NULL,
        [DefaultSalePrice] decimal(18,4) NOT NULL,
        [WeightedAverageCost] decimal(18,4) NOT NULL,
        [MinimumStock] decimal(18,4) NOT NULL,
        [ReorderLevel] decimal(18,4) NOT NULL,
        [VatRate] decimal(5,2) NOT NULL,
        [Barcode] nvarchar(50) NULL,
        [Description] nvarchar(500) NULL,
        [Status] int NOT NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Products_Units_BaseUnitId] FOREIGN KEY ([BaseUnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE TABLE [InventoryMovements] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [UnitId] int NOT NULL,
        [MovementType] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [QuantityInBaseUnit] decimal(18,4) NOT NULL,
        [UnitCost] decimal(18,4) NOT NULL,
        [TotalCost] decimal(18,4) NOT NULL,
        [MovementDate] datetime2 NOT NULL,
        [ReferenceNumber] nvarchar(50) NOT NULL,
        [SourceType] int NOT NULL,
        [SourceId] int NULL,
        [BalanceAfter] decimal(18,4) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_InventoryMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryMovements_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryMovements_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryMovements_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE TABLE [ProductUnits] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [UnitId] int NOT NULL,
        [ConversionFactor] decimal(18,6) NOT NULL,
        [SalePrice] decimal(18,4) NOT NULL,
        [PurchasePrice] decimal(18,4) NOT NULL,
        [Barcode] nvarchar(50) NULL,
        [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_ProductUnits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductUnits_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductUnits_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE TABLE [WarehouseProducts] (
        [Id] int NOT NULL IDENTITY,
        [WarehouseId] int NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL DEFAULT 0.0,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_WarehouseProducts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WarehouseProducts_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WarehouseProducts_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Categories_NameAr_Parent] ON [Categories] ([NameAr], [ParentCategoryId]) WHERE [ParentCategoryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE INDEX [IX_Categories_ParentCategoryId] ON [Categories] ([ParentCategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_Reference] ON [InventoryMovements] ([ReferenceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_InventoryMovements_Source] ON [InventoryMovements] ([SourceType], [SourceId]) WHERE [SourceId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_StockCard] ON [InventoryMovements] ([ProductId], [WarehouseId], [MovementDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_UnitId] ON [InventoryMovements] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_WarehouseId] ON [InventoryMovements] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Products_Barcode] ON [Products] ([Barcode]) WHERE [Barcode] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE INDEX [IX_Products_BaseUnitId] ON [Products] ([BaseUnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_Code] ON [Products] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE INDEX [IX_Products_NameAr] ON [Products] ([NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ProductUnits_Barcode] ON [ProductUnits] ([Barcode]) WHERE [Barcode] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductUnits_Product_Unit] ON [ProductUnits] ([ProductId], [UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE INDEX [IX_ProductUnits_UnitId] ON [ProductUnits] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Units_NameAr] ON [Units] ([NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE INDEX [IX_WarehouseProducts_ProductId] ON [WarehouseProducts] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WarehouseProducts_Warehouse_Product] ON [WarehouseProducts] ([WarehouseId], [ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Warehouses_Code] ON [Warehouses] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208224903_AddInventoryModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260208224903_AddInventoryModule', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [Phone] nvarchar(30) NULL,
        [Mobile] nvarchar(30) NULL,
        [Address] nvarchar(500) NULL,
        [City] nvarchar(100) NULL,
        [TaxNumber] nvarchar(50) NULL,
        [PreviousBalance] decimal(18,4) NOT NULL DEFAULT 0.0,
        [CreditLimit] decimal(18,4) NOT NULL DEFAULT 0.0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Notes] nvarchar(1000) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [Phone] nvarchar(30) NULL,
        [Mobile] nvarchar(30) NULL,
        [Address] nvarchar(500) NULL,
        [City] nvarchar(100) NULL,
        [TaxNumber] nvarchar(50) NULL,
        [PreviousBalance] decimal(18,4) NOT NULL DEFAULT 0.0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Notes] nvarchar(1000) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customers_Code] ON [Customers] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_Customers_IsActive] ON [Customers] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_Customers_NameAr] ON [Customers] ([NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Customers_TaxNumber] ON [Customers] ([TaxNumber]) WHERE [TaxNumber] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Suppliers_Code] ON [Suppliers] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_Suppliers_IsActive] ON [Suppliers] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    CREATE INDEX [IX_Suppliers_NameAr] ON [Suppliers] ([NameAr]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Suppliers_TaxNumber] ON [Suppliers] ([TaxNumber]) WHERE [TaxNumber] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208230955_AddCustomersAndSuppliers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260208230955_AddCustomersAndSuppliers', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE TABLE [PurchaseInvoices] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceNumber] varchar(30) NOT NULL,
        [InvoiceDate] date NOT NULL,
        [SupplierId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [Status] int NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [DiscountTotal] decimal(18,4) NOT NULL,
        [VatTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [JournalEntryId] int NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_PurchaseInvoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseInvoices_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseInvoices_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseInvoices_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE TABLE [PurchaseInvoiceLines] (
        [Id] int NOT NULL IDENTITY,
        [PurchaseInvoiceId] int NOT NULL,
        [ProductId] int NOT NULL,
        [UnitId] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [ConversionFactor] decimal(18,6) NOT NULL,
        [BaseQuantity] decimal(18,4) NOT NULL,
        [DiscountPercent] decimal(5,2) NOT NULL,
        [DiscountAmount] decimal(18,4) NOT NULL,
        [SubTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [VatRate] decimal(5,2) NOT NULL,
        [VatAmount] decimal(18,4) NOT NULL,
        [TotalWithVat] decimal(18,4) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_PurchaseInvoiceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseInvoiceLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseInvoiceLines_PurchaseInvoices_PurchaseInvoiceId] FOREIGN KEY ([PurchaseInvoiceId]) REFERENCES [PurchaseInvoices] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PurchaseInvoiceLines_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE TABLE [PurchaseReturns] (
        [Id] int NOT NULL IDENTITY,
        [ReturnNumber] varchar(30) NOT NULL,
        [ReturnDate] date NOT NULL,
        [SupplierId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [OriginalInvoiceId] int NULL,
        [Status] int NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [DiscountTotal] decimal(18,4) NOT NULL,
        [VatTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [JournalEntryId] int NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_PurchaseReturns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseReturns_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseReturns_PurchaseInvoices_OriginalInvoiceId] FOREIGN KEY ([OriginalInvoiceId]) REFERENCES [PurchaseInvoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseReturns_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseReturns_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE TABLE [PurchaseReturnLines] (
        [Id] int NOT NULL IDENTITY,
        [PurchaseReturnId] int NOT NULL,
        [ProductId] int NOT NULL,
        [UnitId] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [ConversionFactor] decimal(18,6) NOT NULL,
        [BaseQuantity] decimal(18,4) NOT NULL,
        [DiscountPercent] decimal(5,2) NOT NULL,
        [DiscountAmount] decimal(18,4) NOT NULL,
        [SubTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [VatRate] decimal(5,2) NOT NULL,
        [VatAmount] decimal(18,4) NOT NULL,
        [TotalWithVat] decimal(18,4) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_PurchaseReturnLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseReturnLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseReturnLines_PurchaseReturns_PurchaseReturnId] FOREIGN KEY ([PurchaseReturnId]) REFERENCES [PurchaseReturns] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PurchaseReturnLines_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoiceLines_InvoiceId] ON [PurchaseInvoiceLines] ([PurchaseInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoiceLines_ProductId] ON [PurchaseInvoiceLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoiceLines_UnitId] ON [PurchaseInvoiceLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoices_InvoiceDate] ON [PurchaseInvoices] ([InvoiceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseInvoices_InvoiceNumber] ON [PurchaseInvoices] ([InvoiceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PurchaseInvoices_JournalEntryId] ON [PurchaseInvoices] ([JournalEntryId]) WHERE [JournalEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoices_Status] ON [PurchaseInvoices] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoices_SupplierId] ON [PurchaseInvoices] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoices_WarehouseId] ON [PurchaseInvoices] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseReturnLines_ProductId] ON [PurchaseReturnLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseReturnLines_ReturnId] ON [PurchaseReturnLines] ([PurchaseReturnId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseReturnLines_UnitId] ON [PurchaseReturnLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PurchaseReturns_JournalEntryId] ON [PurchaseReturns] ([JournalEntryId]) WHERE [JournalEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PurchaseReturns_OriginalInvoiceId] ON [PurchaseReturns] ([OriginalInvoiceId]) WHERE [OriginalInvoiceId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseReturns_ReturnDate] ON [PurchaseReturns] ([ReturnDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseReturns_ReturnNumber] ON [PurchaseReturns] ([ReturnNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseReturns_Status] ON [PurchaseReturns] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseReturns_SupplierId] ON [PurchaseReturns] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_PurchaseReturns_WarehouseId] ON [PurchaseReturns] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208232942_AddPurchaseInvoicesAndReturns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260208232942_AddPurchaseInvoicesAndReturns', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE TABLE [SalesInvoices] (
        [Id] int NOT NULL IDENTITY,
        [InvoiceNumber] varchar(30) NOT NULL,
        [InvoiceDate] date NOT NULL,
        [CustomerId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [Status] int NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [DiscountTotal] decimal(18,4) NOT NULL,
        [VatTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [JournalEntryId] int NULL,
        [CogsJournalEntryId] int NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_SalesInvoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesInvoices_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesInvoices_JournalEntries_CogsJournalEntryId] FOREIGN KEY ([CogsJournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesInvoices_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesInvoices_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE TABLE [SalesInvoiceLines] (
        [Id] int NOT NULL IDENTITY,
        [SalesInvoiceId] int NOT NULL,
        [ProductId] int NOT NULL,
        [UnitId] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [ConversionFactor] decimal(18,6) NOT NULL,
        [BaseQuantity] decimal(18,4) NOT NULL,
        [DiscountPercent] decimal(5,2) NOT NULL,
        [DiscountAmount] decimal(18,4) NOT NULL,
        [SubTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [VatRate] decimal(5,2) NOT NULL,
        [VatAmount] decimal(18,4) NOT NULL,
        [TotalWithVat] decimal(18,4) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SalesInvoiceLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesInvoiceLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesInvoiceLines_SalesInvoices_SalesInvoiceId] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [SalesInvoices] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SalesInvoiceLines_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE TABLE [SalesReturns] (
        [Id] int NOT NULL IDENTITY,
        [ReturnNumber] varchar(30) NOT NULL,
        [ReturnDate] date NOT NULL,
        [CustomerId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [OriginalInvoiceId] int NULL,
        [Status] int NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [DiscountTotal] decimal(18,4) NOT NULL,
        [VatTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [JournalEntryId] int NULL,
        [CogsJournalEntryId] int NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_SalesReturns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesReturns_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesReturns_JournalEntries_CogsJournalEntryId] FOREIGN KEY ([CogsJournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesReturns_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesReturns_SalesInvoices_OriginalInvoiceId] FOREIGN KEY ([OriginalInvoiceId]) REFERENCES [SalesInvoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesReturns_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE TABLE [SalesReturnLines] (
        [Id] int NOT NULL IDENTITY,
        [SalesReturnId] int NOT NULL,
        [ProductId] int NOT NULL,
        [UnitId] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [ConversionFactor] decimal(18,6) NOT NULL,
        [BaseQuantity] decimal(18,4) NOT NULL,
        [DiscountPercent] decimal(5,2) NOT NULL,
        [DiscountAmount] decimal(18,4) NOT NULL,
        [SubTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [VatRate] decimal(5,2) NOT NULL,
        [VatAmount] decimal(18,4) NOT NULL,
        [TotalWithVat] decimal(18,4) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SalesReturnLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesReturnLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesReturnLines_SalesReturns_SalesReturnId] FOREIGN KEY ([SalesReturnId]) REFERENCES [SalesReturns] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SalesReturnLines_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesInvoiceLines_InvoiceId] ON [SalesInvoiceLines] ([SalesInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesInvoiceLines_ProductId] ON [SalesInvoiceLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesInvoiceLines_UnitId] ON [SalesInvoiceLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesInvoices_CogsJournalEntryId] ON [SalesInvoices] ([CogsJournalEntryId]) WHERE [CogsJournalEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesInvoices_CustomerId] ON [SalesInvoices] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesInvoices_InvoiceDate] ON [SalesInvoices] ([InvoiceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalesInvoices_InvoiceNumber] ON [SalesInvoices] ([InvoiceNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesInvoices_JournalEntryId] ON [SalesInvoices] ([JournalEntryId]) WHERE [JournalEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesInvoices_Status] ON [SalesInvoices] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesInvoices_WarehouseId] ON [SalesInvoices] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesReturnLines_ProductId] ON [SalesReturnLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesReturnLines_ReturnId] ON [SalesReturnLines] ([SalesReturnId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesReturnLines_UnitId] ON [SalesReturnLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesReturns_CogsJournalEntryId] ON [SalesReturns] ([CogsJournalEntryId]) WHERE [CogsJournalEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesReturns_CustomerId] ON [SalesReturns] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesReturns_JournalEntryId] ON [SalesReturns] ([JournalEntryId]) WHERE [JournalEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesReturns_OriginalInvoiceId] ON [SalesReturns] ([OriginalInvoiceId]) WHERE [OriginalInvoiceId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesReturns_ReturnDate] ON [SalesReturns] ([ReturnDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalesReturns_ReturnNumber] ON [SalesReturns] ([ReturnNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesReturns_Status] ON [SalesReturns] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    CREATE INDEX [IX_SalesReturns_WarehouseId] ON [SalesReturns] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260208235529_AddSalesInvoicesAndReturns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260208235529_AddSalesInvoicesAndReturns', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE TABLE [Cashboxes] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(10) NOT NULL,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NULL,
        [AccountId] int NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Cashboxes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE TABLE [CashPayments] (
        [Id] int NOT NULL IDENTITY,
        [PaymentNumber] varchar(30) NOT NULL,
        [PaymentDate] date NOT NULL,
        [CashboxId] int NOT NULL,
        [AccountId] int NOT NULL,
        [SupplierId] int NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [Status] int NOT NULL,
        [JournalEntryId] int NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CashPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashPayments_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashPayments_Cashboxes_CashboxId] FOREIGN KEY ([CashboxId]) REFERENCES [Cashboxes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashPayments_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashPayments_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE TABLE [CashReceipts] (
        [Id] int NOT NULL IDENTITY,
        [ReceiptNumber] varchar(30) NOT NULL,
        [ReceiptDate] date NOT NULL,
        [CashboxId] int NOT NULL,
        [AccountId] int NOT NULL,
        [CustomerId] int NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [Status] int NOT NULL,
        [JournalEntryId] int NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CashReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashReceipts_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashReceipts_Cashboxes_CashboxId] FOREIGN KEY ([CashboxId]) REFERENCES [Cashboxes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashReceipts_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashReceipts_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE TABLE [CashTransfers] (
        [Id] int NOT NULL IDENTITY,
        [TransferNumber] varchar(30) NOT NULL,
        [TransferDate] date NOT NULL,
        [SourceCashboxId] int NOT NULL,
        [TargetCashboxId] int NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [Status] int NOT NULL,
        [JournalEntryId] int NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CashTransfers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashTransfers_Cashboxes_SourceCashboxId] FOREIGN KEY ([SourceCashboxId]) REFERENCES [Cashboxes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashTransfers_Cashboxes_TargetCashboxId] FOREIGN KEY ([TargetCashboxId]) REFERENCES [Cashboxes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashTransfers_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Cashboxes_Code] ON [Cashboxes] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashPayments_AccountId] ON [CashPayments] ([AccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashPayments_CashboxId] ON [CashPayments] ([CashboxId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_CashPayments_JournalEntryId] ON [CashPayments] ([JournalEntryId]) WHERE [JournalEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashPayments_PaymentDate] ON [CashPayments] ([PaymentDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CashPayments_PaymentNumber] ON [CashPayments] ([PaymentNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashPayments_Status] ON [CashPayments] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_CashPayments_SupplierId] ON [CashPayments] ([SupplierId]) WHERE [SupplierId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashReceipts_AccountId] ON [CashReceipts] ([AccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashReceipts_CashboxId] ON [CashReceipts] ([CashboxId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_CashReceipts_CustomerId] ON [CashReceipts] ([CustomerId]) WHERE [CustomerId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_CashReceipts_JournalEntryId] ON [CashReceipts] ([JournalEntryId]) WHERE [JournalEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashReceipts_ReceiptDate] ON [CashReceipts] ([ReceiptDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CashReceipts_ReceiptNumber] ON [CashReceipts] ([ReceiptNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashReceipts_Status] ON [CashReceipts] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_CashTransfers_JournalEntryId] ON [CashTransfers] ([JournalEntryId]) WHERE [JournalEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashTransfers_SourceCashboxId] ON [CashTransfers] ([SourceCashboxId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashTransfers_Status] ON [CashTransfers] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashTransfers_TargetCashboxId] ON [CashTransfers] ([TargetCashboxId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE INDEX [IX_CashTransfers_TransferDate] ON [CashTransfers] ([TransferDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CashTransfers_TransferNumber] ON [CashTransfers] ([TransferNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209013159_AddTreasuryModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260209013159_AddTreasuryModule', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209112253_AddSecurityAndSettings'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] int NOT NULL IDENTITY,
        [NameAr] nvarchar(50) NOT NULL,
        [NameEn] nvarchar(50) NOT NULL,
        [Description] nvarchar(200) NULL,
        [IsSystem] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209112253_AddSecurityAndSettings'
)
BEGIN
    CREATE TABLE [SystemSettings] (
        [Id] int NOT NULL IDENTITY,
        [SettingKey] nvarchar(100) NOT NULL,
        [SettingValue] nvarchar(500) NULL,
        [Description] nvarchar(300) NULL,
        [GroupName] nvarchar(100) NULL,
        [DataType] nvarchar(20) NULL DEFAULT N'string',
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209112253_AddSecurityAndSettings'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] int NOT NULL,
        [PermissionKey] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209112253_AddSecurityAndSettings'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(50) NOT NULL,
        [PasswordHash] nvarchar(200) NOT NULL,
        [FullNameAr] nvarchar(100) NOT NULL,
        [FullNameEn] nvarchar(100) NULL,
        [Email] nvarchar(200) NULL,
        [Phone] nvarchar(20) NULL,
        [RoleId] int NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsLocked] bit NOT NULL DEFAULT CAST(0 AS bit),
        [FailedLoginAttempts] int NOT NULL DEFAULT 0,
        [LastLoginAt] datetime2 NULL,
        [MustChangePassword] bit NOT NULL DEFAULT CAST(1 AS bit),
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209112253_AddSecurityAndSettings'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_PermissionKey] ON [RolePermissions] ([RoleId], [PermissionKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209112253_AddSecurityAndSettings'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_NameEn] ON [Roles] ([NameEn]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209112253_AddSecurityAndSettings'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SystemSettings_SettingKey] ON [SystemSettings] ([SettingKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209112253_AddSecurityAndSettings'
)
BEGIN
    CREATE INDEX [IX_Users_RoleId] ON [Users] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209112253_AddSecurityAndSettings'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209112253_AddSecurityAndSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260209112253_AddSecurityAndSettings', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [PurchaseInvoiceLines] DROP CONSTRAINT [FK_PurchaseInvoiceLines_PurchaseInvoices_PurchaseInvoiceId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [PurchaseReturnLines] DROP CONSTRAINT [FK_PurchaseReturnLines_PurchaseReturns_PurchaseReturnId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [SalesInvoiceLines] DROP CONSTRAINT [FK_SalesInvoiceLines_SalesInvoices_SalesInvoiceId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [SalesReturnLines] DROP CONSTRAINT [FK_SalesReturnLines_SalesReturns_SalesReturnId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [AccountId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [SalesReturns] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [SalesReturns] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [SalesReturns] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [PurchaseReturns] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [PurchaseReturns] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [PurchaseReturns] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [Customers] ADD [AccountId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [CashTransfers] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [CashTransfers] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [CashTransfers] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [CashReceipts] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [CashReceipts] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [CashReceipts] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [CashPayments] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [CashPayments] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [CashPayments] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE TABLE [BackupHistory] (
        [Id] int NOT NULL IDENTITY,
        [FilePath] nvarchar(500) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [BackupDate] datetime2 NOT NULL,
        [PerformedBy] nvarchar(100) NOT NULL,
        [BackupType] nvarchar(50) NOT NULL,
        [IsSuccessful] bit NOT NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_BackupHistory] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE TABLE [PosSessions] (
        [Id] int NOT NULL IDENTITY,
        [SessionNumber] varchar(30) NOT NULL,
        [UserId] int NOT NULL,
        [CashboxId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [OpeningBalance] decimal(18,4) NOT NULL,
        [TotalSales] decimal(18,4) NOT NULL,
        [TotalCashReceived] decimal(18,4) NOT NULL,
        [TotalCardReceived] decimal(18,4) NOT NULL,
        [TotalOnAccount] decimal(18,4) NOT NULL,
        [TransactionCount] int NOT NULL,
        [ClosingBalance] decimal(18,4) NOT NULL,
        [Variance] decimal(18,4) NOT NULL,
        [Status] int NOT NULL,
        [OpenedAt] datetime2 NOT NULL,
        [ClosedAt] datetime2 NULL,
        [ClosingNotes] nvarchar(1000) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_PosSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PosSessions_Cashboxes_CashboxId] FOREIGN KEY ([CashboxId]) REFERENCES [Cashboxes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PosSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PosSessions_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE TABLE [PosPayments] (
        [Id] int NOT NULL IDENTITY,
        [SalesInvoiceId] int NOT NULL,
        [PosSessionId] int NOT NULL,
        [PaymentMethod] int NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [ReferenceNumber] nvarchar(100) NULL,
        [PaidAt] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_PosPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PosPayments_PosSessions_PosSessionId] FOREIGN KEY ([PosSessionId]) REFERENCES [PosSessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PosPayments_SalesInvoices_SalesInvoiceId] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [SalesInvoices] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_Suppliers_AccountId] ON [Suppliers] ([AccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_Products_Status] ON [Products] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_JournalEntries_Source] ON [JournalEntries] ([SourceType], [SourceId]) WHERE [SourceId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_MovementDate] ON [InventoryMovements] ([MovementDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_InventoryMovements_ProductId] ON [InventoryMovements] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_Customers_AccountId] ON [Customers] ([AccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_BackupHistory_BackupDate] ON [BackupHistory] ([BackupDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_PosPayments_PaymentMethod] ON [PosPayments] ([PaymentMethod]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_PosPayments_PosSessionId] ON [PosPayments] ([PosSessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_PosPayments_SalesInvoiceId] ON [PosPayments] ([SalesInvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_PosSessions_CashboxId] ON [PosSessions] ([CashboxId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_PosSessions_OpenedAt] ON [PosSessions] ([OpenedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PosSessions_SessionNumber] ON [PosSessions] ([SessionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_PosSessions_Status] ON [PosSessions] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_PosSessions_UserId] ON [PosSessions] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    CREATE INDEX [IX_PosSessions_WarehouseId] ON [PosSessions] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [Customers] ADD CONSTRAINT [FK_Customers_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [PurchaseInvoiceLines] ADD CONSTRAINT [FK_PurchaseInvoiceLines_PurchaseInvoices_PurchaseInvoiceId] FOREIGN KEY ([PurchaseInvoiceId]) REFERENCES [PurchaseInvoices] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [PurchaseReturnLines] ADD CONSTRAINT [FK_PurchaseReturnLines_PurchaseReturns_PurchaseReturnId] FOREIGN KEY ([PurchaseReturnId]) REFERENCES [PurchaseReturns] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [SalesInvoiceLines] ADD CONSTRAINT [FK_SalesInvoiceLines_SalesInvoices_SalesInvoiceId] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [SalesInvoices] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [SalesReturnLines] ADD CONSTRAINT [FK_SalesReturnLines_SalesReturns_SalesReturnId] FOREIGN KEY ([SalesReturnId]) REFERENCES [SalesReturns] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    ALTER TABLE [Suppliers] ADD CONSTRAINT [FK_Suppliers_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN

                    CREATE OR ALTER TRIGGER TR_JournalEntries_EnforceBalance
                    ON [JournalEntries]
                    AFTER UPDATE
                    AS
                    BEGIN
                        SET NOCOUNT ON;
                        IF EXISTS (
                            SELECT 1
                            FROM inserted i
                            WHERE i.[Status] = 1  -- Posted
                            AND EXISTS (
                                SELECT 1
                                FROM [JournalEntryLines] jel
                                WHERE jel.[JournalEntryId] = i.[Id]
                                GROUP BY jel.[JournalEntryId]
                                HAVING ABS(SUM(jel.[DebitAmount]) - SUM(jel.[CreditAmount])) > 0.001
                            )
                        )
                        BEGIN
                            RAISERROR (N'لا يمكن ترحيل قيد غير متوازن. مجموع المدين يجب أن يساوي مجموع الدائن.', 16, 1);
                            ROLLBACK TRANSACTION;
                            RETURN;
                        END
                    END;
                
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209200644_AddJournalBalanceCheckAndRestrictCascade'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260209200644_AddJournalBalanceCheckAndRestrictCascade', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209210038_FixJournalEntryMoneyPrecision'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JournalEntryLines]') AND [c].[name] = N'DebitAmount');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [JournalEntryLines] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [JournalEntryLines] ALTER COLUMN [DebitAmount] decimal(18,4) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209210038_FixJournalEntryMoneyPrecision'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JournalEntryLines]') AND [c].[name] = N'CreditAmount');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [JournalEntryLines] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [JournalEntryLines] ALTER COLUMN [CreditAmount] decimal(18,4) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209210038_FixJournalEntryMoneyPrecision'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JournalEntries]') AND [c].[name] = N'TotalDebit');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [JournalEntries] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [JournalEntries] ALTER COLUMN [TotalDebit] decimal(18,4) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209210038_FixJournalEntryMoneyPrecision'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JournalEntries]') AND [c].[name] = N'TotalCredit');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [JournalEntries] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [JournalEntries] ALTER COLUMN [TotalCredit] decimal(18,4) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209210038_FixJournalEntryMoneyPrecision'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260209210038_FixJournalEntryMoneyPrecision', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [SalesRepresentativeId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    ALTER TABLE [Customers] ADD [BlockedOnOverdue] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    ALTER TABLE [Customers] ADD [DaysAllowed] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    ALTER TABLE [Customers] ADD [DefaultSalesRepresentativeId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    ALTER TABLE [Customers] ADD [PriceListId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE TABLE [InventoryAdjustments] (
        [Id] int NOT NULL IDENTITY,
        [AdjustmentNumber] varchar(30) NOT NULL,
        [AdjustmentDate] date NOT NULL,
        [WarehouseId] int NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [Status] int NOT NULL,
        [TotalCostDifference] decimal(18,4) NOT NULL,
        [JournalEntryId] int NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_InventoryAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryAdjustments_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryAdjustments_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE TABLE [PriceLists] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [Description] nvarchar(1000) NULL,
        [ValidFrom] date NULL,
        [ValidTo] date NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_PriceLists] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE TABLE [SalesRepresentatives] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [Phone] nvarchar(30) NULL,
        [Mobile] nvarchar(30) NULL,
        [Email] nvarchar(200) NULL,
        [CommissionRate] decimal(5,2) NOT NULL,
        [CommissionBasedOn] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_SalesRepresentatives] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE TABLE [InventoryAdjustmentLines] (
        [Id] int NOT NULL IDENTITY,
        [InventoryAdjustmentId] int NOT NULL,
        [ProductId] int NOT NULL,
        [UnitId] int NOT NULL,
        [SystemQuantity] decimal(18,4) NOT NULL,
        [ActualQuantity] decimal(18,4) NOT NULL,
        [DifferenceQuantity] decimal(18,4) NOT NULL,
        [ConversionFactor] decimal(18,6) NOT NULL,
        [DifferenceInBaseUnit] decimal(18,4) NOT NULL,
        [UnitCost] decimal(18,4) NOT NULL,
        [CostDifference] decimal(18,4) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_InventoryAdjustmentLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryAdjustmentLines_InventoryAdjustments_InventoryAdjustmentId] FOREIGN KEY ([InventoryAdjustmentId]) REFERENCES [InventoryAdjustments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryAdjustmentLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_InventoryAdjustmentLines_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE TABLE [PriceTiers] (
        [Id] int NOT NULL IDENTITY,
        [PriceListId] int NOT NULL,
        [ProductId] int NOT NULL,
        [MinimumQuantity] decimal(18,4) NOT NULL,
        [Price] decimal(18,4) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_PriceTiers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PriceTiers_PriceLists_PriceListId] FOREIGN KEY ([PriceListId]) REFERENCES [PriceLists] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PriceTiers_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesInvoices_SalesRepresentativeId] ON [SalesInvoices] ([SalesRepresentativeId]) WHERE [SalesRepresentativeId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Customers_DefaultSalesRepresentativeId] ON [Customers] ([DefaultSalesRepresentativeId]) WHERE [DefaultSalesRepresentativeId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Customers_PriceListId] ON [Customers] ([PriceListId]) WHERE [PriceListId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE INDEX [IX_InventoryAdjustmentLines_AdjustmentId] ON [InventoryAdjustmentLines] ([InventoryAdjustmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE INDEX [IX_InventoryAdjustmentLines_ProductId] ON [InventoryAdjustmentLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE INDEX [IX_InventoryAdjustmentLines_UnitId] ON [InventoryAdjustmentLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE INDEX [IX_InventoryAdjustments_Date] ON [InventoryAdjustments] ([AdjustmentDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_InventoryAdjustments_JournalEntryId] ON [InventoryAdjustments] ([JournalEntryId]) WHERE [JournalEntryId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InventoryAdjustments_Number] ON [InventoryAdjustments] ([AdjustmentNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE INDEX [IX_InventoryAdjustments_Status] ON [InventoryAdjustments] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE INDEX [IX_InventoryAdjustments_WarehouseId] ON [InventoryAdjustments] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PriceLists_Code] ON [PriceLists] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE INDEX [IX_PriceLists_IsActive] ON [PriceLists] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PriceTiers_List_Product_MinQty] ON [PriceTiers] ([PriceListId], [ProductId], [MinimumQuantity]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE INDEX [IX_PriceTiers_PriceListId] ON [PriceTiers] ([PriceListId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE INDEX [IX_PriceTiers_ProductId] ON [PriceTiers] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalesRepresentatives_Code] ON [SalesRepresentatives] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    CREATE INDEX [IX_SalesRepresentatives_IsActive] ON [SalesRepresentatives] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    ALTER TABLE [Customers] ADD CONSTRAINT [FK_Customers_PriceLists_PriceListId] FOREIGN KEY ([PriceListId]) REFERENCES [PriceLists] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    ALTER TABLE [Customers] ADD CONSTRAINT [FK_Customers_SalesRepresentatives_DefaultSalesRepresentativeId] FOREIGN KEY ([DefaultSalesRepresentativeId]) REFERENCES [SalesRepresentatives] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD CONSTRAINT [FK_SalesInvoices_SalesRepresentatives_SalesRepresentativeId] FOREIGN KEY ([SalesRepresentativeId]) REFERENCES [SalesRepresentatives] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260210204726_AddPriceListsInventoryAdjustmentsAndCreditControl', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE TABLE [PurchaseQuotations] (
        [Id] int NOT NULL IDENTITY,
        [QuotationNumber] varchar(30) NOT NULL,
        [QuotationDate] date NOT NULL,
        [ValidUntil] date NOT NULL,
        [SupplierId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [Status] int NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [DiscountTotal] decimal(18,4) NOT NULL,
        [VatTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [ConvertedToInvoiceId] int NULL,
        [ConvertedDate] datetime2 NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_PurchaseQuotations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseQuotations_PurchaseInvoices_ConvertedToInvoiceId] FOREIGN KEY ([ConvertedToInvoiceId]) REFERENCES [PurchaseInvoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseQuotations_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseQuotations_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE TABLE [SalesQuotations] (
        [Id] int NOT NULL IDENTITY,
        [QuotationNumber] varchar(30) NOT NULL,
        [QuotationDate] date NOT NULL,
        [ValidUntil] date NOT NULL,
        [CustomerId] int NOT NULL,
        [WarehouseId] int NOT NULL,
        [SalesRepresentativeId] int NULL,
        [Status] int NOT NULL,
        [Subtotal] decimal(18,4) NOT NULL,
        [DiscountTotal] decimal(18,4) NOT NULL,
        [VatTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [ConvertedToInvoiceId] int NULL,
        [ConvertedDate] datetime2 NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_SalesQuotations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesQuotations_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesQuotations_SalesInvoices_ConvertedToInvoiceId] FOREIGN KEY ([ConvertedToInvoiceId]) REFERENCES [SalesInvoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesQuotations_SalesRepresentatives_SalesRepresentativeId] FOREIGN KEY ([SalesRepresentativeId]) REFERENCES [SalesRepresentatives] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesQuotations_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE TABLE [PurchaseQuotationLines] (
        [Id] int NOT NULL IDENTITY,
        [PurchaseQuotationId] int NOT NULL,
        [ProductId] int NOT NULL,
        [UnitId] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [ConversionFactor] decimal(18,6) NOT NULL,
        [BaseQuantity] decimal(18,4) NOT NULL,
        [DiscountPercent] decimal(5,2) NOT NULL,
        [DiscountAmount] decimal(18,4) NOT NULL,
        [SubTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [VatRate] decimal(5,2) NOT NULL,
        [VatAmount] decimal(18,4) NOT NULL,
        [TotalWithVat] decimal(18,4) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_PurchaseQuotationLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseQuotationLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseQuotationLines_PurchaseQuotations_PurchaseQuotationId] FOREIGN KEY ([PurchaseQuotationId]) REFERENCES [PurchaseQuotations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseQuotationLines_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE TABLE [SalesQuotationLines] (
        [Id] int NOT NULL IDENTITY,
        [SalesQuotationId] int NOT NULL,
        [ProductId] int NOT NULL,
        [UnitId] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [ConversionFactor] decimal(18,6) NOT NULL,
        [BaseQuantity] decimal(18,4) NOT NULL,
        [DiscountPercent] decimal(5,2) NOT NULL,
        [DiscountAmount] decimal(18,4) NOT NULL,
        [SubTotal] decimal(18,4) NOT NULL,
        [NetTotal] decimal(18,4) NOT NULL,
        [VatRate] decimal(5,2) NOT NULL,
        [VatAmount] decimal(18,4) NOT NULL,
        [TotalWithVat] decimal(18,4) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SalesQuotationLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesQuotationLines_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesQuotationLines_SalesQuotations_SalesQuotationId] FOREIGN KEY ([SalesQuotationId]) REFERENCES [SalesQuotations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesQuotationLines_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_PurchaseQuotationLines_ProductId] ON [PurchaseQuotationLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_PurchaseQuotationLines_QuotationId] ON [PurchaseQuotationLines] ([PurchaseQuotationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_PurchaseQuotationLines_UnitId] ON [PurchaseQuotationLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PurchaseQuotations_ConvertedToInvoiceId] ON [PurchaseQuotations] ([ConvertedToInvoiceId]) WHERE [ConvertedToInvoiceId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_PurchaseQuotations_QuotationDate] ON [PurchaseQuotations] ([QuotationDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseQuotations_QuotationNumber] ON [PurchaseQuotations] ([QuotationNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_PurchaseQuotations_Status] ON [PurchaseQuotations] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_PurchaseQuotations_SupplierId] ON [PurchaseQuotations] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_PurchaseQuotations_WarehouseId] ON [PurchaseQuotations] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_SalesQuotationLines_ProductId] ON [SalesQuotationLines] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_SalesQuotationLines_QuotationId] ON [SalesQuotationLines] ([SalesQuotationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_SalesQuotationLines_UnitId] ON [SalesQuotationLines] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesQuotations_ConvertedToInvoiceId] ON [SalesQuotations] ([ConvertedToInvoiceId]) WHERE [ConvertedToInvoiceId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_SalesQuotations_CustomerId] ON [SalesQuotations] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_SalesQuotations_QuotationDate] ON [SalesQuotations] ([QuotationDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalesQuotations_QuotationNumber] ON [SalesQuotations] ([QuotationNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesQuotations_SalesRepresentativeId] ON [SalesQuotations] ([SalesRepresentativeId]) WHERE [SalesRepresentativeId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_SalesQuotations_Status] ON [SalesQuotations] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    CREATE INDEX [IX_SalesQuotations_WarehouseId] ON [SalesQuotations] ([WarehouseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210224424_AddQuotationsModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260210224424_AddQuotationsModule', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210233800_AddTreasuryInvoiceLinks'
)
BEGIN
    ALTER TABLE [CashReceipts] ADD [SalesInvoiceId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210233800_AddTreasuryInvoiceLinks'
)
BEGIN
    ALTER TABLE [CashPayments] ADD [PurchaseInvoiceId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210233800_AddTreasuryInvoiceLinks'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_CashReceipts_SalesInvoiceId] ON [CashReceipts] ([SalesInvoiceId]) WHERE [SalesInvoiceId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210233800_AddTreasuryInvoiceLinks'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_CashPayments_PurchaseInvoiceId] ON [CashPayments] ([PurchaseInvoiceId]) WHERE [PurchaseInvoiceId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210233800_AddTreasuryInvoiceLinks'
)
BEGIN
    ALTER TABLE [CashPayments] ADD CONSTRAINT [FK_CashPayments_PurchaseInvoices_PurchaseInvoiceId] FOREIGN KEY ([PurchaseInvoiceId]) REFERENCES [PurchaseInvoices] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210233800_AddTreasuryInvoiceLinks'
)
BEGIN
    ALTER TABLE [CashReceipts] ADD CONSTRAINT [FK_CashReceipts_SalesInvoices_SalesInvoiceId] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [SalesInvoices] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210233800_AddTreasuryInvoiceLinks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260210233800_AddTreasuryInvoiceLinks', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [PaidAmount] decimal(18,4) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [PaymentStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [PaidAmount] decimal(18,4) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [PaymentStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    DECLARE @defaultSchema AS sysname;
    SET @defaultSchema = SCHEMA_NAME();
    DECLARE @description AS sql_variant;
    SET @description = N'Reserved for future Cost Center module';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', @defaultSchema, 'TABLE', N'JournalEntryLines', 'COLUMN', N'CostCenterId';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    DECLARE @defaultSchema AS sysname;
    SET @defaultSchema = SCHEMA_NAME();
    DECLARE @description AS sql_variant;
    SET @description = N'Reserved for future Cost Center module';
    EXEC sp_addextendedproperty 'MS_Description', @description, 'SCHEMA', @defaultSchema, 'TABLE', N'JournalEntries', 'COLUMN', N'CostCenterId';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    CREATE TABLE [BankAccounts] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(10) NOT NULL,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NULL,
        [BankName] nvarchar(200) NULL,
        [AccountNumber] nvarchar(50) NULL,
        [IBAN] nvarchar(34) NULL,
        [AccountId] int NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_BankAccounts] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    EXEC(N'ALTER TABLE [JournalEntries] ADD CONSTRAINT [CK_JournalEntries_Balance] CHECK ([Status] <> 1 OR [TotalDebit] = [TotalCredit])');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BankAccounts_Code] ON [BankAccounts] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    CREATE INDEX [IX_BankAccounts_IBAN] ON [BankAccounts] ([IBAN]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212010433_AddBankAccount'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212010433_AddBankAccount', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212011339_AddBankReconciliation'
)
BEGIN
    CREATE TABLE [BankReconciliations] (
        [Id] int NOT NULL IDENTITY,
        [BankAccountId] int NOT NULL,
        [ReconciliationDate] datetime2 NOT NULL,
        [StatementBalance] decimal(18,4) NOT NULL,
        [SystemBalance] decimal(18,4) NOT NULL,
        [Difference] decimal(18,4) NOT NULL,
        [IsCompleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Notes] nvarchar(500) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_BankReconciliations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BankReconciliations_BankAccounts_BankAccountId] FOREIGN KEY ([BankAccountId]) REFERENCES [BankAccounts] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212011339_AddBankReconciliation'
)
BEGIN
    CREATE TABLE [BankReconciliationItems] (
        [Id] int NOT NULL IDENTITY,
        [BankReconciliationId] int NOT NULL,
        [TransactionDate] datetime2 NOT NULL,
        [Description] nvarchar(300) NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Reference] nvarchar(100) NULL,
        [IsMatched] bit NOT NULL DEFAULT CAST(0 AS bit),
        [JournalEntryId] int NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_BankReconciliationItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BankReconciliationItems_BankReconciliations_BankReconciliationId] FOREIGN KEY ([BankReconciliationId]) REFERENCES [BankReconciliations] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212011339_AddBankReconciliation'
)
BEGIN
    CREATE INDEX [IX_BankReconciliationItems_ReconciliationId] ON [BankReconciliationItems] ([BankReconciliationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212011339_AddBankReconciliation'
)
BEGIN
    CREATE INDEX [IX_BankReconciliations_BankAccountId] ON [BankReconciliations] ([BankAccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212011339_AddBankReconciliation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212011339_AddBankReconciliation', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [CounterpartyType] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [SupplierId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [CounterpartyCustomerId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [CounterpartyType] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    ALTER TABLE [Products] ADD [DefaultSupplierId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesInvoices_SupplierId] ON [SalesInvoices] ([SupplierId]) WHERE [SupplierId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PurchaseInvoices_CounterpartyCustomerId] ON [PurchaseInvoices] ([CounterpartyCustomerId]) WHERE [CounterpartyCustomerId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Products_DefaultSupplierId] ON [Products] ([DefaultSupplierId]) WHERE [DefaultSupplierId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_Suppliers_DefaultSupplierId] FOREIGN KEY ([DefaultSupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD CONSTRAINT [FK_PurchaseInvoices_Customers_CounterpartyCustomerId] FOREIGN KEY ([CounterpartyCustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD CONSTRAINT [FK_SalesInvoices_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212132041_SyncMissingColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212132041_SyncMissingColumns', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Warehouses] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Warehouses] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Warehouses] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Warehouses] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Suppliers] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [SalesReturns] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [SalesQuotations] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [PurchaseReturns] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [PurchaseQuotations] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Products] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [JournalEntries] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [InventoryAdjustments] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Customers] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [CashTransfers] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [CashReceipts] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [CashPayments] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Cashboxes] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Cashboxes] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Cashboxes] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Cashboxes] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [BankAccounts] ADD [CompanyId] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [BankAccounts] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [BankAccounts] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [BankAccounts] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(10) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_Warehouses_CompanyId] ON [Warehouses] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_Suppliers_CompanyId] ON [Suppliers] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_SalesReturns_CompanyId] ON [SalesReturns] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_SalesQuotations_CompanyId] ON [SalesQuotations] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_SalesInvoices_CompanyId] ON [SalesInvoices] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_PurchaseReturns_CompanyId] ON [PurchaseReturns] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_PurchaseQuotations_CompanyId] ON [PurchaseQuotations] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_PurchaseInvoices_CompanyId] ON [PurchaseInvoices] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_Products_CompanyId] ON [Products] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_CompanyId] ON [JournalEntries] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_InventoryAdjustments_CompanyId] ON [InventoryAdjustments] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_Customers_CompanyId] ON [Customers] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_CashTransfers_CompanyId] ON [CashTransfers] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_CashReceipts_CompanyId] ON [CashReceipts] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_CashPayments_CompanyId] ON [CashPayments] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_Cashboxes_CompanyId] ON [Cashboxes] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE INDEX [IX_BankAccounts_CompanyId] ON [BankAccounts] ([CompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Companies_Code] ON [Companies] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN

                    IF NOT EXISTS (SELECT 1 FROM [Companies] WHERE [Id] = 1)
                    BEGIN
                        SET IDENTITY_INSERT [Companies] ON;
                        INSERT INTO [Companies] ([Id], [Code], [NameAr], [NameEn], [IsActive], [CreatedAt], [CreatedBy])
                        VALUES (1, N'DEF', N'الشركة الافتراضية', N'Default Company', 1, GETUTCDATE(), N'SYSTEM');
                        SET IDENTITY_INSERT [Companies] OFF;
                    END
                
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [BankAccounts] ADD CONSTRAINT [FK_BankAccounts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Cashboxes] ADD CONSTRAINT [FK_Cashboxes_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [CashPayments] ADD CONSTRAINT [FK_CashPayments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [CashReceipts] ADD CONSTRAINT [FK_CashReceipts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [CashTransfers] ADD CONSTRAINT [FK_CashTransfers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Customers] ADD CONSTRAINT [FK_Customers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [InventoryAdjustments] ADD CONSTRAINT [FK_InventoryAdjustments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [JournalEntries] ADD CONSTRAINT [FK_JournalEntries_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD CONSTRAINT [FK_PurchaseInvoices_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [PurchaseQuotations] ADD CONSTRAINT [FK_PurchaseQuotations_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [PurchaseReturns] ADD CONSTRAINT [FK_PurchaseReturns_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD CONSTRAINT [FK_SalesInvoices_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [SalesQuotations] ADD CONSTRAINT [FK_SalesQuotations_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [SalesReturns] ADD CONSTRAINT [FK_SalesReturns_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Suppliers] ADD CONSTRAINT [FK_Suppliers_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    ALTER TABLE [Warehouses] ADD CONSTRAINT [FK_Warehouses_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212150218_AddCompanyIsolation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212150218_AddCompanyIsolation', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212152637_AddFeatureGovernance'
)
BEGIN
    CREATE TABLE [Features] (
        [Id] int NOT NULL IDENTITY,
        [FeatureKey] nvarchar(100) NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NULL,
        [Description] nvarchar(500) NULL,
        [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [RiskLevel] nvarchar(20) NOT NULL DEFAULT N'Medium',
        [DependsOn] nvarchar(500) NULL,
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Features] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212152637_AddFeatureGovernance'
)
BEGIN
    CREATE TABLE [FeatureChangeLogs] (
        [Id] int NOT NULL IDENTITY,
        [FeatureId] int NOT NULL,
        [FeatureKey] nvarchar(100) NOT NULL,
        [OldValue] bit NOT NULL,
        [NewValue] bit NOT NULL,
        [ChangedBy] nvarchar(100) NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_FeatureChangeLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeatureChangeLogs_Features_FeatureId] FOREIGN KEY ([FeatureId]) REFERENCES [Features] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212152637_AddFeatureGovernance'
)
BEGIN
    CREATE INDEX [IX_FeatureChangeLogs_ChangedAt] ON [FeatureChangeLogs] ([ChangedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212152637_AddFeatureGovernance'
)
BEGIN
    CREATE INDEX [IX_FeatureChangeLogs_FeatureId] ON [FeatureChangeLogs] ([FeatureId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212152637_AddFeatureGovernance'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Features_FeatureKey] ON [Features] ([FeatureKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212152637_AddFeatureGovernance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212152637_AddFeatureGovernance', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212153458_AddProfileComplexityLayer'
)
BEGIN
    CREATE TABLE [SystemProfiles] (
        [Id] int NOT NULL IDENTITY,
        [ProfileName] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_SystemProfiles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212153458_AddProfileComplexityLayer'
)
BEGIN
    CREATE TABLE [ProfileFeatures] (
        [Id] int NOT NULL IDENTITY,
        [ProfileId] int NOT NULL,
        [FeatureKey] nvarchar(100) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_ProfileFeatures] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProfileFeatures_SystemProfiles_ProfileId] FOREIGN KEY ([ProfileId]) REFERENCES [SystemProfiles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212153458_AddProfileComplexityLayer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProfileFeatures_ProfileId_FeatureKey] ON [ProfileFeatures] ([ProfileId], [FeatureKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212153458_AddProfileComplexityLayer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SystemProfiles_ProfileName] ON [SystemProfiles] ([ProfileName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212153458_AddProfileComplexityLayer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212153458_AddProfileComplexityLayer', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212154546_AddImpactAnalyzerFields'
)
BEGIN
    ALTER TABLE [Features] ADD [AffectsAccounting] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212154546_AddImpactAnalyzerFields'
)
BEGIN
    ALTER TABLE [Features] ADD [AffectsData] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212154546_AddImpactAnalyzerFields'
)
BEGIN
    ALTER TABLE [Features] ADD [AffectsInventory] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212154546_AddImpactAnalyzerFields'
)
BEGIN
    ALTER TABLE [Features] ADD [AffectsReporting] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212154546_AddImpactAnalyzerFields'
)
BEGIN
    ALTER TABLE [Features] ADD [ImpactDescription] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212154546_AddImpactAnalyzerFields'
)
BEGIN
    ALTER TABLE [Features] ADD [RequiresMigration] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212154546_AddImpactAnalyzerFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212154546_AddImpactAnalyzerFields', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212155451_AddVersionIntegrityEngine'
)
BEGIN
    CREATE TABLE [FeatureVersions] (
        [Id] int NOT NULL IDENTITY,
        [FeatureKey] nvarchar(100) NOT NULL,
        [IntroducedInVersion] nvarchar(20) NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_FeatureVersions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212155451_AddVersionIntegrityEngine'
)
BEGIN
    CREATE TABLE [SystemVersions] (
        [Id] int NOT NULL IDENTITY,
        [VersionNumber] nvarchar(20) NOT NULL,
        [AppliedAt] datetime2 NOT NULL,
        [AppliedBy] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SystemVersions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212155451_AddVersionIntegrityEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FeatureVersions_FeatureKey] ON [FeatureVersions] ([FeatureKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212155451_AddVersionIntegrityEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SystemVersions_VersionNumber] ON [SystemVersions] ([VersionNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212155451_AddVersionIntegrityEngine'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212155451_AddVersionIntegrityEngine', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212160809_AddMigrationExecutionEngine'
)
BEGIN
    CREATE TABLE [MigrationExecutions] (
        [Id] int NOT NULL IDENTITY,
        [MigrationName] nvarchar(300) NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [IsSuccessful] bit NOT NULL,
        [ExecutedBy] nvarchar(100) NOT NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [BackupPath] nvarchar(500) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_MigrationExecutions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212160809_AddMigrationExecutionEngine'
)
BEGIN
    CREATE INDEX [IX_MigrationExecutions_Name] ON [MigrationExecutions] ([MigrationName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212160809_AddMigrationExecutionEngine'
)
BEGIN
    CREATE INDEX [IX_MigrationExecutions_StartedAt] ON [MigrationExecutions] ([StartedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212160809_AddMigrationExecutionEngine'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212160809_AddMigrationExecutionEngine', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    ALTER TABLE [BankReconciliationItems] DROP CONSTRAINT [FK_BankReconciliationItems_BankReconciliations_BankReconciliationId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    ALTER TABLE [PriceTiers] DROP CONSTRAINT [FK_PriceTiers_PriceLists_PriceListId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    ALTER TABLE [ProductUnits] DROP CONSTRAINT [FK_ProductUnits_Products_ProductId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    ALTER TABLE [ProfileFeatures] DROP CONSTRAINT [FK_ProfileFeatures_SystemProfiles_ProfileId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    ALTER TABLE [RolePermissions] DROP CONSTRAINT [FK_RolePermissions_Roles_RoleId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    ALTER TABLE [BankReconciliationItems] ADD CONSTRAINT [FK_BankReconciliationItems_BankReconciliations_BankReconciliationId] FOREIGN KEY ([BankReconciliationId]) REFERENCES [BankReconciliations] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    ALTER TABLE [PriceTiers] ADD CONSTRAINT [FK_PriceTiers_PriceLists_PriceListId] FOREIGN KEY ([PriceListId]) REFERENCES [PriceLists] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    ALTER TABLE [ProductUnits] ADD CONSTRAINT [FK_ProductUnits_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    ALTER TABLE [ProfileFeatures] ADD CONSTRAINT [FK_ProfileFeatures_SystemProfiles_ProfileId] FOREIGN KEY ([ProfileId]) REFERENCES [SystemProfiles] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    ALTER TABLE [RolePermissions] ADD CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260212234846_RemoveCascadeDeleteViolations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260212234846_RemoveCascadeDeleteViolations', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213094500_AddUsersLockedAtColumn'
)
BEGIN

    IF COL_LENGTH('Users', 'LockedAt') IS NULL
    BEGIN
        ALTER TABLE [Users] ADD [LockedAt] datetime2 NULL;
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213094500_AddUsersLockedAtColumn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260213094500_AddUsersLockedAtColumn', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101000_AddAuditLogChangeColumns'
)
BEGIN

    IF COL_LENGTH('AuditLogs', 'OldValues') IS NULL
    BEGIN
        ALTER TABLE [AuditLogs] ADD [OldValues] nvarchar(max) NULL;
    END

    IF COL_LENGTH('AuditLogs', 'NewValues') IS NULL
    BEGIN
        ALTER TABLE [AuditLogs] ADD [NewValues] nvarchar(max) NULL;
    END

    IF COL_LENGTH('AuditLogs', 'ChangedColumns') IS NULL
    BEGIN
        ALTER TABLE [AuditLogs] ADD [ChangedColumns] nvarchar(max) NULL;
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101000_AddAuditLogChangeColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260213101000_AddAuditLogChangeColumns', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [Users] ADD [LockedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [SalesInvoiceLines] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [SalesInvoiceLines] ADD [CreatedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [SalesInvoiceLines] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [SalesInvoiceLines] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [SalesInvoiceLines] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [SalesInvoiceLines] ADD [ModifiedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [SalesInvoiceLines] ADD [ModifiedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [PurchaseInvoiceLines] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [PurchaseInvoiceLines] ADD [CreatedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [PurchaseInvoiceLines] ADD [DeletedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [PurchaseInvoiceLines] ADD [DeletedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [PurchaseInvoiceLines] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [PurchaseInvoiceLines] ADD [ModifiedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [PurchaseInvoiceLines] ADD [ModifiedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [ChangedColumns] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [NewValues] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    ALTER TABLE [AuditLogs] ADD [OldValues] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260213101518_InvoiceLineSoftDeleteAndAuditColumns', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    ALTER TABLE [Products] DROP CONSTRAINT [FK_Products_Suppliers_DefaultSupplierId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DROP INDEX [IX_SalesInvoices_InvoiceNumber] ON [SalesInvoices];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DROP INDEX [IX_PurchaseInvoices_InvoiceNumber] ON [PurchaseInvoices];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesInvoiceLines]') AND [c].[name] = N'CreatedAt');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [SalesInvoiceLines] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [SalesInvoiceLines] DROP COLUMN [CreatedAt];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesInvoiceLines]') AND [c].[name] = N'CreatedBy');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [SalesInvoiceLines] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [SalesInvoiceLines] DROP COLUMN [CreatedBy];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesInvoiceLines]') AND [c].[name] = N'DeletedAt');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [SalesInvoiceLines] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [SalesInvoiceLines] DROP COLUMN [DeletedAt];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesInvoiceLines]') AND [c].[name] = N'DeletedBy');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [SalesInvoiceLines] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [SalesInvoiceLines] DROP COLUMN [DeletedBy];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesInvoiceLines]') AND [c].[name] = N'IsDeleted');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [SalesInvoiceLines] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [SalesInvoiceLines] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesInvoiceLines]') AND [c].[name] = N'ModifiedAt');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [SalesInvoiceLines] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [SalesInvoiceLines] DROP COLUMN [ModifiedAt];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesInvoiceLines]') AND [c].[name] = N'ModifiedBy');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [SalesInvoiceLines] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [SalesInvoiceLines] DROP COLUMN [ModifiedBy];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseInvoiceLines]') AND [c].[name] = N'CreatedAt');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseInvoiceLines] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [PurchaseInvoiceLines] DROP COLUMN [CreatedAt];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseInvoiceLines]') AND [c].[name] = N'CreatedBy');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseInvoiceLines] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [PurchaseInvoiceLines] DROP COLUMN [CreatedBy];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseInvoiceLines]') AND [c].[name] = N'DeletedAt');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseInvoiceLines] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [PurchaseInvoiceLines] DROP COLUMN [DeletedAt];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseInvoiceLines]') AND [c].[name] = N'DeletedBy');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseInvoiceLines] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [PurchaseInvoiceLines] DROP COLUMN [DeletedBy];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseInvoiceLines]') AND [c].[name] = N'IsDeleted');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseInvoiceLines] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [PurchaseInvoiceLines] DROP COLUMN [IsDeleted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseInvoiceLines]') AND [c].[name] = N'ModifiedAt');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseInvoiceLines] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [PurchaseInvoiceLines] DROP COLUMN [ModifiedAt];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseInvoiceLines]') AND [c].[name] = N'ModifiedBy');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseInvoiceLines] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [PurchaseInvoiceLines] DROP COLUMN [ModifiedBy];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SalesInvoices_InvoiceNumber] ON [SalesInvoices] ([InvoiceNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PurchaseInvoices_InvoiceNumber] ON [PurchaseInvoices] ([InvoiceNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_Suppliers_DefaultSupplierId] FOREIGN KEY ([DefaultSupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213122952_FilteredUniqueInvoiceNumbers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260213122952_FilteredUniqueInvoiceNumbers', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    DROP INDEX [IX_PurchaseReturns_SupplierId] ON [PurchaseReturns];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    DROP INDEX [IX_PurchaseInvoices_SupplierId] ON [PurchaseInvoices];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesReturns]') AND [c].[name] = N'CustomerId');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [SalesReturns] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [SalesReturns] ALTER COLUMN [CustomerId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [SalesReturns] ADD [CounterpartyType] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [SalesReturns] ADD [SalesRepresentativeId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [SalesReturns] ADD [SupplierId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseReturns]') AND [c].[name] = N'SupplierId');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseReturns] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [PurchaseReturns] ALTER COLUMN [SupplierId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [PurchaseReturns] ADD [CounterpartyCustomerId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [PurchaseReturns] ADD [CounterpartyType] int NOT NULL DEFAULT 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [PurchaseReturns] ADD [SalesRepresentativeId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseInvoices]') AND [c].[name] = N'SupplierId');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseInvoices] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [PurchaseInvoices] ALTER COLUMN [SupplierId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [SalesRepresentativeId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesReturns_SalesRepresentativeId] ON [SalesReturns] ([SalesRepresentativeId]) WHERE [SalesRepresentativeId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SalesReturns_SupplierId] ON [SalesReturns] ([SupplierId]) WHERE [SupplierId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PurchaseReturns_CounterpartyCustomerId] ON [PurchaseReturns] ([CounterpartyCustomerId]) WHERE [CounterpartyCustomerId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PurchaseReturns_SalesRepresentativeId] ON [PurchaseReturns] ([SalesRepresentativeId]) WHERE [SalesRepresentativeId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PurchaseReturns_SupplierId] ON [PurchaseReturns] ([SupplierId]) WHERE [SupplierId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PurchaseInvoices_SalesRepresentativeId] ON [PurchaseInvoices] ([SalesRepresentativeId]) WHERE [SalesRepresentativeId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_PurchaseInvoices_SupplierId] ON [PurchaseInvoices] ([SupplierId]) WHERE [SupplierId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD CONSTRAINT [FK_PurchaseInvoices_SalesRepresentatives_SalesRepresentativeId] FOREIGN KEY ([SalesRepresentativeId]) REFERENCES [SalesRepresentatives] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [PurchaseReturns] ADD CONSTRAINT [FK_PurchaseReturns_Customers_CounterpartyCustomerId] FOREIGN KEY ([CounterpartyCustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [PurchaseReturns] ADD CONSTRAINT [FK_PurchaseReturns_SalesRepresentatives_SalesRepresentativeId] FOREIGN KEY ([SalesRepresentativeId]) REFERENCES [SalesRepresentatives] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [SalesReturns] ADD CONSTRAINT [FK_SalesReturns_SalesRepresentatives_SalesRepresentativeId] FOREIGN KEY ([SalesRepresentativeId]) REFERENCES [SalesRepresentatives] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    ALTER TABLE [SalesReturns] ADD CONSTRAINT [FK_SalesReturns_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260213142701_AddCounterpartyAndSalesRepToInvoicesReturns', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    DROP INDEX [IX_SalesReturns_ReturnNumber] ON [SalesReturns];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    DROP INDEX [IX_SalesInvoices_InvoiceNumber] ON [SalesInvoices];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    DROP INDEX [IX_PurchaseReturns_ReturnNumber] ON [PurchaseReturns];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    DROP INDEX [IX_PurchaseInvoices_InvoiceNumber] ON [PurchaseInvoices];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    DROP INDEX [IX_JournalEntries_JournalNumber] ON [JournalEntries];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    ALTER TABLE [Cashboxes] ADD [Balance] decimal(18,4) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SalesReturns_Company_ReturnNumber] ON [SalesReturns] ([CompanyId], [ReturnNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [SalesReturnLines] ADD CONSTRAINT [CK_SalesReturnLines_Quantity] CHECK ([Quantity] > 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [SalesReturnLines] ADD CONSTRAINT [CK_SalesReturnLines_UnitPrice] CHECK ([UnitPrice] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SalesInvoices_Company_InvoiceNumber] ON [SalesInvoices] ([CompanyId], [InvoiceNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [SalesInvoices] ADD CONSTRAINT [CK_SalesInvoices_PaidAmount] CHECK ([PaidAmount] >= 0 AND [PaidAmount] <= [NetTotal])');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [SalesInvoiceLines] ADD CONSTRAINT [CK_SalesInvoiceLines_Quantity] CHECK ([Quantity] > 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [SalesInvoiceLines] ADD CONSTRAINT [CK_SalesInvoiceLines_UnitPrice] CHECK ([UnitPrice] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PurchaseReturns_Company_ReturnNumber] ON [PurchaseReturns] ([CompanyId], [ReturnNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [PurchaseReturnLines] ADD CONSTRAINT [CK_PurchaseReturnLines_Quantity] CHECK ([Quantity] > 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [PurchaseReturnLines] ADD CONSTRAINT [CK_PurchaseReturnLines_UnitPrice] CHECK ([UnitPrice] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PurchaseInvoices_Company_InvoiceNumber] ON [PurchaseInvoices] ([CompanyId], [InvoiceNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [PurchaseInvoices] ADD CONSTRAINT [CK_PurchaseInvoices_PaidAmount] CHECK ([PaidAmount] >= 0 AND [PaidAmount] <= [NetTotal])');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [PurchaseInvoiceLines] ADD CONSTRAINT [CK_PurchaseInvoiceLines_Quantity] CHECK ([Quantity] > 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [PurchaseInvoiceLines] ADD CONSTRAINT [CK_PurchaseInvoiceLines_UnitPrice] CHECK ([UnitPrice] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [JournalEntryLines] ADD CONSTRAINT [CK_JournalEntryLines_NonNegative] CHECK ([DebitAmount] >= 0 AND [CreditAmount] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [JournalEntryLines] ADD CONSTRAINT [CK_JournalEntryLines_SingleSide] CHECK (NOT ([DebitAmount] > 0 AND [CreditAmount] > 0))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_JournalEntries_JournalNumber] ON [JournalEntries] ([JournalNumber]) WHERE [JournalNumber] IS NOT NULL AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [InventoryMovements] ADD CONSTRAINT [CK_InventoryMovements_BaseQuantity] CHECK ([QuantityInBaseUnit] > 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    EXEC(N'ALTER TABLE [InventoryMovements] ADD CONSTRAINT [CK_InventoryMovements_TotalCost] CHECK ([TotalCost] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214093103_Phase2Phase3DatabaseHardening'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214093103_Phase2Phase3DatabaseHardening', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214105806_20260214_Phase7_CashboxBalanceConstraint'
)
BEGIN
    EXEC(N'ALTER TABLE [Cashboxes] ADD CONSTRAINT [CK_Cashboxes_Balance_NonNegative] CHECK ([Balance] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214105806_20260214_Phase7_CashboxBalanceConstraint'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214105806_20260214_Phase7_CashboxBalanceConstraint', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214170000_Phase1_NegativeStockAndReceiptSettings'
)
BEGIN

    IF NOT EXISTS (SELECT 1 FROM SystemSettings WHERE SettingKey = 'AllowNegativeStock')
    BEGIN
        INSERT INTO SystemSettings (SettingKey, SettingValue, Description, GroupName, DataType)
        VALUES ('AllowNegativeStock', 'false', N'السماح بالبيع بالسالب للمخزون', N'إعدادات النظام', 'bool');
    END

    IF NOT EXISTS (SELECT 1 FROM SystemSettings WHERE SettingKey = 'AllowNegativeCashboxBalance')
    BEGIN
        INSERT INTO SystemSettings (SettingKey, SettingValue, Description, GroupName, DataType)
        VALUES ('AllowNegativeCashboxBalance', 'false', N'السماح برصيد خزنة سالب', N'إعدادات النظام', 'bool');
    END

    IF NOT EXISTS (SELECT 1 FROM SystemSettings WHERE SettingKey = 'EnableReceiptPrinting')
    BEGIN
        INSERT INTO SystemSettings (SettingKey, SettingValue, Description, GroupName, DataType)
        VALUES ('EnableReceiptPrinting', 'false', N'تفعيل طباعة إيصالات نقطة البيع', N'إعدادات النظام', 'bool');
    END

    IF NOT EXISTS (SELECT 1 FROM Features WHERE FeatureKey = 'FEATURE_ALLOW_NEGATIVE_STOCK')
    BEGIN
        INSERT INTO Features
            (FeatureKey, NameAr, NameEn, Description, IsEnabled, RiskLevel, DependsOn,
             AffectsData, RequiresMigration, AffectsAccounting, AffectsInventory, AffectsReporting, ImpactDescription,
             CreatedAt, CreatedBy)
        VALUES
            ('FEATURE_ALLOW_NEGATIVE_STOCK', N'السماح بالمخزون السالب', 'Allow Negative Stock', N'السماح ببيع المخزون بالسالب عند تفعيله', 0, 'High', 'Inventory,Sales',
             1, 0, 1, 1, 1, N'السماح بالمخزون السالب قد يؤدي لفروقات مخزون وتكلفة', GETUTCDATE(), 'System');
    END

    IF NOT EXISTS (SELECT 1 FROM Features WHERE FeatureKey = 'FEATURE_ALLOW_NEGATIVE_CASH')
    BEGIN
        INSERT INTO Features
            (FeatureKey, NameAr, NameEn, Description, IsEnabled, RiskLevel, DependsOn,
             AffectsData, RequiresMigration, AffectsAccounting, AffectsInventory, AffectsReporting, ImpactDescription,
             CreatedAt, CreatedBy)
        VALUES
            ('FEATURE_ALLOW_NEGATIVE_CASH', N'السماح برصيد خزنة سالب', 'Allow Negative Cash', N'السماح بتحويل يؤدي إلى رصيد خزنة سالب عند تفعيله', 0, 'High', 'Treasury',
             1, 0, 1, 0, 1, N'السماح برصيد خزنة سالب يؤثر على سلامة النقدية', GETUTCDATE(), 'System');
    END

    IF NOT EXISTS (SELECT 1 FROM Features WHERE FeatureKey = 'FEATURE_RECEIPT_PRINTING')
    BEGIN
        INSERT INTO Features
            (FeatureKey, NameAr, NameEn, Description, IsEnabled, RiskLevel, DependsOn,
             AffectsData, RequiresMigration, AffectsAccounting, AffectsInventory, AffectsReporting, ImpactDescription,
             CreatedAt, CreatedBy)
        VALUES
            ('FEATURE_RECEIPT_PRINTING', N'طباعة إيصالات نقطة البيع', 'Receipt Printing', N'تفعيل طباعة إيصالات نقطة البيع', 0, 'Medium', 'POS',
             0, 0, 0, 0, 0, N'تعطيل طباعة الإيصالات لا يؤثر على البيانات', GETUTCDATE(), 'System');
    END

    DECLARE @advancedProfileId INT = (SELECT TOP 1 Id FROM SystemProfiles WHERE ProfileName = 'Advanced');
    IF @advancedProfileId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM ProfileFeatures WHERE ProfileId = @advancedProfileId AND FeatureKey = 'FEATURE_ALLOW_NEGATIVE_STOCK')
            INSERT INTO ProfileFeatures (ProfileId, FeatureKey) VALUES (@advancedProfileId, 'FEATURE_ALLOW_NEGATIVE_STOCK');

        IF NOT EXISTS (SELECT 1 FROM ProfileFeatures WHERE ProfileId = @advancedProfileId AND FeatureKey = 'FEATURE_ALLOW_NEGATIVE_CASH')
            INSERT INTO ProfileFeatures (ProfileId, FeatureKey) VALUES (@advancedProfileId, 'FEATURE_ALLOW_NEGATIVE_CASH');

        IF NOT EXISTS (SELECT 1 FROM ProfileFeatures WHERE ProfileId = @advancedProfileId AND FeatureKey = 'FEATURE_RECEIPT_PRINTING')
            INSERT INTO ProfileFeatures (ProfileId, FeatureKey) VALUES (@advancedProfileId, 'FEATURE_RECEIPT_PRINTING');
    END

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214170000_Phase1_NegativeStockAndReceiptSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214170000_Phase1_NegativeStockAndReceiptSettings', N'8.0.24');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [PurchaseQuotationLines] DROP CONSTRAINT [FK_PurchaseQuotationLines_PurchaseQuotations_PurchaseQuotationId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [SalesQuotationLines] DROP CONSTRAINT [FK_SalesQuotationLines_SalesQuotations_SalesQuotationId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [Cashboxes] DROP CONSTRAINT [CK_Cashboxes_Balance_NonNegative];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesInvoices]') AND [c].[name] = N'CustomerId');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [SalesInvoices] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [SalesInvoices] ALTER COLUMN [CustomerId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [DeliveryFee] decimal(18,4) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [HeaderDiscountAmount] decimal(18,4) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [SalesInvoices] ADD [HeaderDiscountPercent] decimal(18,4) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [DeliveryFee] decimal(18,4) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [HeaderDiscountAmount] decimal(18,4) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [PurchaseInvoices] ADD [HeaderDiscountPercent] decimal(18,4) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    CREATE INDEX [IX_Cashboxes_AccountId] ON [Cashboxes] ([AccountId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [Cashboxes] ADD CONSTRAINT [FK_Cashboxes_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [PurchaseQuotationLines] ADD CONSTRAINT [FK_PurchaseQuotationLines_PurchaseQuotations_PurchaseQuotationId] FOREIGN KEY ([PurchaseQuotationId]) REFERENCES [PurchaseQuotations] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    ALTER TABLE [SalesQuotationLines] ADD CONSTRAINT [FK_SalesQuotationLines_SalesQuotations_SalesQuotationId] FOREIGN KEY ([SalesQuotationId]) REFERENCES [SalesQuotations] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260215151548_AddInvoiceHeaderDiscountAndDelivery'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260215151548_AddInvoiceHeaderDiscountAndDelivery', N'8.0.24');
END;
GO

COMMIT;
GO

