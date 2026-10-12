namespace EpiRead.Models.Database;

public class BookCategoryMap
{
    // Composite PK, FK
    public int BookId { get; set; }
    public int BookCategoryId { get; set; }

    // Navigation properties
    public virtual Book? Book { get; set; }
    public virtual BookCategory? BookCategory { get; set; }

    // Note: Composite key (BookId, BookCategoryId) should be configured in DbContext.OnModelCreating using:
    // modelBuilder.Entity<BookCategoryMap>().HasKey(x => new { x.BookId, x.BookCategoryId });
}