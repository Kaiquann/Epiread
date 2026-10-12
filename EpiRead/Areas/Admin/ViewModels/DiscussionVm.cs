namespace EpiRead.Areas.Admin.ViewModels;

public class EditDiscussionVm
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; } = string.Empty;
}

// View model for show page (forum details + posts)
public class DiscussionShowVm
{
    public Discussion? Discussion { get; set; }

    public IPagedList<DiscussionPost>? Posts { get; set; }
}