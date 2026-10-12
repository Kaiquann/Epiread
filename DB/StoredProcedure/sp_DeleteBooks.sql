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
