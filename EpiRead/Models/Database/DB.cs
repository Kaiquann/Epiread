using Microsoft.EntityFrameworkCore;

namespace EpiRead.Models.Database;

public class DB : DbContext
{
    public DB(DbContextOptions<DB> options)
        : base(options)
    {
    }

    // System Section
    public DbSet<Role> Roles { get; set; }
    public DbSet<LoginAttempt> LoginAttempts { get; set; }

    // User Section
    public DbSet<User> Users { get; set; }
    public DbSet<LoginHistory> LoginHistories { get; set; }
    public DbSet<SearchHistory> SearchHistories { get; set; }
    public DbSet<BookHistory> BookHistories { get; set; }
    public DbSet<PreferenceBook> PreferenceBooks { get; set; }
    public DbSet<Bookmark> Bookmarks { get; set; }
    public DbSet<Rating> Ratings { get; set; }

    // Recovery Section
    public DbSet<ResetMfa> ResetMfas { get; set; }
    public DbSet<ResetPassword> ResetPasswords { get; set; }

    // Discussion Section
    public DbSet<Discussion> Discussions { get; set; }
    public DbSet<DiscussionPost> DiscussionPosts { get; set; }
    public DbSet<DiscussionComment> DiscussionComments { get; set; }
    public DbSet<DiscussionLikeCount> DiscussionLikeCounts { get; set; }
    public DbSet<DiscussionAttachment> DiscussionAttachments { get; set; }

    // Book Section
    public DbSet<BookCategory> BookCategories { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<BookDocument> BookDocuments { get; set; }
    public DbSet<BookCategoryMap> BookCategoryMaps { get; set; }

    // Chatbot Section
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        List<UserRoleType> userRoles = EnumHelper.ToList<UserRoleType>();
        // Seed Roles
        builder.Entity<Role>()
            .HasData(userRoles.Select((role, index) => new Role
            {
                Id = index + 1, // Assign a 1-based ID
                Name = role.ToString() // Use the enum member's string representation as the role name
            }));

        // Book Category Map Composite Key
        builder.Entity<BookCategoryMap>().HasKey(x => new { x.BookId, x.BookCategoryId });

        // Configure one-to-many relationship between book and user
        builder.Entity<Book>()
            .HasOne(b => b.User)
            .WithMany(u => u.Books)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure self-referencing one-to-many for DiscussionComment replies
        builder.Entity<DiscussionComment>()
            .HasMany(dc => dc.Replies)
            .WithOne(dc => dc.ParentComment)
            .HasForeignKey(dc => dc.ReplyCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Remove cascade delete from UserId foreign key to avoid multiple cascade paths
        builder.Entity<DiscussionComment>()
            .HasOne(dc => dc.User)
            .WithMany(u => u.DiscussionComments)
            .HasForeignKey(dc => dc.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Remove cascade delete from BookDocumentId foreign key to avoid multiple cascade paths
        builder.Entity<Bookmark>()
            .HasOne(b => b.BookDocument)
            .WithMany(bd => bd.Bookmarks)
            .HasForeignKey(b => b.BookDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Remove cascade delete from BookDocumentId foreign key to avoid multiple cascade paths
        builder.Entity<BookHistory>()
            .HasOne(bh => bh.BookDocument)
            .WithMany(bd => bd.BookHistories)
            .HasForeignKey(bh => bh.BookDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /**
     * @ModelName: Product
     * @TableName: Products
     *
     * Remember Table Name Must Add "s" at the end
     */
    //public DbSet<Product> Products { get; set; }
}