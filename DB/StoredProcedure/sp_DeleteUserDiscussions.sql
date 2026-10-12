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
