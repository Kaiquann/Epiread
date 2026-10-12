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
