namespace EpiRead.Models.Share;

public static class AuthorizationPolicies
{
    public const string SuperAdminOnly = "SuperAdminOnly";
    public const string RequireAdminOrHigher = "RequireAdminOrHigher";
    public const string RequireAdminOrAuthor = "RequireAdminOrAuthor";
    public const string AuthorOnly = "AuthorOnly";
    public const string MemberOnly = "MemberOnly";
    public const string AuthenticatedUsersOnly = "AuthenticatedUsersOnly";
}