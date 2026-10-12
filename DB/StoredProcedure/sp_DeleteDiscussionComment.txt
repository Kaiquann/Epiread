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
