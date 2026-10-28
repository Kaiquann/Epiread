using Microsoft.AspNetCore.Razor.TagHelpers;

namespace EpiRead.TagHelpers;

[HtmlTargetElement("img", Attributes = "book-cover", TagStructure = TagStructure.WithoutEndTag)]
public sealed class BookCoverTagHelper(BookCoverCatalog catalog) : TagHelper
{
    private const string Placeholder = "/images/book-cover-not-available.webp";
    public string BookCover { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        string? recovered = catalog.Find(BookCover);
        string? source = output.Attributes["src"]?.Value?.ToString();
        // This retired project's URLs cannot resolve. Serve the original cover locally.
        bool retired = Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                       uri.Host.Equals("gncqxgrcjkjmgluswqwh.supabase.co", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(source) || source == Placeholder || retired)
            source = recovered ?? Placeholder;

        output.Attributes.SetAttribute("src", source);
        output.Attributes.SetAttribute("alt", BookCover);
        // Try the recovery copy once, then the placeholder; never loop on an error.
        string fallback = recovered ?? Placeholder;
        output.Attributes.SetAttribute("onerror",
            $"this.onerror=function(){{this.onerror=null;this.src='{Placeholder}';}};this.src='{fallback}';");
    }
}
