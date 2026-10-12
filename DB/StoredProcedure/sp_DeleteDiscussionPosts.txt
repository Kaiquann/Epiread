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
