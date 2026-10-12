using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using DB = EpiRead.Models.Database.DB;

namespace EpiRead;

/**
 * @description: Program Setup by Chong Jun Xiang
 */
public class Program
{
    public static void Main(string[] args)
    {
        // Step 1: Create the builder
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Step 2: Configure appsettings.json
        builder.Configuration.AddJsonFile("appsettings.json", false, true);

        // Start application logging (Serilog) as early as possible
        // Place logs into a "Logs" folder under the application's content root.
        string logsDir = Path.Combine(builder.Environment.ContentRootPath, "Logs");
        Directory.CreateDirectory(logsDir);
        string logFilePath = Path.Combine(logsDir, $"{builder.Environment.ApplicationName}.log");
        LoggerUtilities.StartLog(builder.Environment.ApplicationName, logFilePath);

        // Step 3: Configure database context
        // For connect server use this one with the connection string in appsettings.json
        string connectionString = builder.Configuration.GetConnectionString("DBConnection") ??
                                  throw new InvalidOperationException("Connection string 'DBConnection' not found.");
        builder.Services.AddDbContext<DB>(options => options.UseSqlServer(connectionString));

        // For connect local database use this one
        //builder.Services.AddSqlServer<DB>($@"Data Source=(LocalDB)\MSSQLLocalDB;
        //                                     AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
        //                                     Trusted_Connection=True;");

        // Step 4: Configure authentication and authorization
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie("Cookies", options =>
            {
                options.LoginPath = "/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Home/AccessDenied";
                options.Cookie.Name = "logged_in";
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.SuperAdminOnly, policy =>
                policy.RequireRole(nameof(UserRoleType.SuperAdmin)));

            options.AddPolicy(AuthorizationPolicies.RequireAdminOrHigher, policy =>
                policy.RequireRole(nameof(UserRoleType.Admin), nameof(UserRoleType.SuperAdmin)));

            options.AddPolicy(AuthorizationPolicies.RequireAdminOrAuthor, policy =>
            {
                policy.RequireRole(nameof(UserRoleType.Admin), nameof(UserRoleType.SuperAdmin),
                    nameof(UserRoleType.Author));
            });

            options.AddPolicy(AuthorizationPolicies.AuthorOnly, policy =>
                policy.RequireRole(nameof(UserRoleType.Author)));

            options.AddPolicy(AuthorizationPolicies.MemberOnly, policy =>
                policy.RequireRole(nameof(UserRoleType.Member)));

            options.AddPolicy(AuthorizationPolicies.AuthenticatedUsersOnly, policy =>
                policy.RequireAuthenticatedUser());
        });

        // Step 5: Configure session
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        // Step 6: Configure CORS
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // Step 7: Bind configuration sections
        ConfigurationManager configuration = builder.Configuration;
        RegisterConfigSection<InitialAccountConfig>(builder.Services, configuration, "InitialAccount");
        RegisterConfigSection<SmtpConfig>(builder.Services, configuration, "Smtp");
        RegisterConfigSection<CfCaptchaConfig>(builder.Services, configuration, "CfCaptcha");
        RegisterConfigSection<IpInfoConfig>(builder.Services, configuration, "IpInfo");
        RegisterConfigSection<DropboxConfig>(builder.Services, configuration, "Dropbox");
        RegisterConfigSection<GeminiConfig>(builder.Services, configuration, "Gemini");

        // Step 8: Add application services

        // Singleton services

        // Scoped services
        builder.Services.AddScoped<Helper>();
        builder.Services.AddScoped<DbInitializer>();
        // Library services
        builder.Services.AddScoped<IEmailService, EmailService>();
        builder.Services.AddScoped<IDropboxHelper, DropboxHelper>();
        builder.Services.AddScoped<IImageHelper, ImageHelper>();
        builder.Services.AddScoped<IImageDropboxHelper, ImageDropboxHelper>();
        // Dropbox services
        builder.Services.AddScoped<IDropboxService, DropboxService>();
        // User services
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IUserClaimsSyncService, UserClaimsSyncService>();
        builder.Services.AddScoped<ILoginService, LoginService>();
        builder.Services.AddScoped<IResetMfaService, ResetMfaService>();
        builder.Services.AddScoped<IResetPasswordService, ResetPasswordService>();
        builder.Services.AddScoped<IUserInactivityService, UserInactivityService>();
        // Book category services
        builder.Services.AddScoped<IBookCategoryService, BookCategoryService>();
        // Book services
        builder.Services.AddScoped<IBookService, BookService>();
        // Discussion services
        builder.Services.AddScoped<IDiscussionService, DiscussionService>();
        // AI services
        builder.Services.AddScoped<IGeminiHelper, GeminiHelper>();
        builder.Services.AddScoped<IContentModerationService, ContentModerationService>();
        // Chatbot services
        builder.Services.AddScoped<IChatbotService, ChatbotService>();

        // Transient services
        builder.Services.AddTransient<IMailerHelper, MailerHelper>();
        builder.Services.AddTransient<ICfCaptchaHelper, CfCaptchaHelper>();
        builder.Services.AddTransient<IIpInfoHelper, IpInfoHelper>();

        // Step 9: Add essential services and middleware
        builder.Services.AddHttpContextAccessor();

        // Configure session to store TempData (prevents 431 Request Header Fields Too Large errors)
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.Name = ".EpiRead.Session";
        });

        builder.Services.AddControllersWithViews()
            .AddSessionStateTempDataProvider(); // Use session instead of cookies for TempData
        builder.Services.AddRazorTemplating();

        // Configure Kestrel for large file uploads (chunk size + overhead)
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB max per request (5MB chunk + overhead)
        });

        // Configure form options for multipart uploads
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
        });

        // Add cron job
        // SyncService will run every 5 minutes
        builder.Services.AddHostedService<SyncService>();
        builder.Services.AddCronJob<SyncService>(config =>
        {
            config.TimeZoneInfo = TimeZoneInfo.Local;
            config.CronExpression = @"*/5 * * * *";
        });

        // Set localization
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        CultureInfo[] supportedCultures =
        [
            new("en-MY"),
            new("ms-MY")
        ];
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture("en-MY", "en-MY");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
        });

        // Register Serilog with the generic host so ASP.NET uses it
        builder.Host.UseSerilog();

        // Build the application
        WebApplication app = builder.Build();

        FileExtensionContentTypeProvider provider = new()
        {
            Mappings =
            {
                [".epub"] = "application/epub+zip"
            }
        };

        // Step 10: Configure middleware pipeline
        app.UseHttpsRedirection(); // Enforce HTTPS
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = provider
        }); // Serve static files
        app.UseRouting(); // Enable routing
        app.UseCors(corsPolicyBuilder =>
            corsPolicyBuilder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()); // Configure CORS
        app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>()
            .Value); // Localization middleware
        app.UseAuthentication(); // Authentication middleware
        app.UseAuthorization(); // Authorization middleware
        app.UseSession(); // Session middleware

        // Custom middleware
        app.UseMiddleware<UserClaimsSyncMiddleware>();
        //app.UseMiddleware<SyncMiddleware>();

        // Custom middleware to handle 405 Method Not Allowed
        app.Use(async (context, next) =>
        {
            await next();
            switch (context.Response.StatusCode)
            {
                case StatusCodes.Status405MethodNotAllowed:
                    context.Response.Redirect("/");
                    break;
            }
        });

        // Adding endpoints to the request pipeline
        app.UseEndpoints(endpoints => endpoints.MapControllers());

        // Step 11: Configure routing
        // Area routing
        app.MapControllerRoute(
            "areas",
            "{area:exists}/{controller=Home}/{action=Index}/{id?}");

        // Default route (no area)
        app.MapControllerRoute(
            "default",
            "{controller=Home}/{action=Index}/{id?}");

        // Step 12: Seed roles and super admin account before running the application
        using (IServiceScope scope = app.Services.CreateScope())
        {
            DbInitializer initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
            initializer.SeedRoles();
            initializer.SeedSuperAdmin();
            initializer.SeedDefaultBookCategory();
        }

        // Step 12: Run the application
        try
        {
            app.Run();
        }
        catch (Exception ex)
        {
            // Log fatal exception and rethrow to preserve behavior
            LoggerUtilities.Fatal(ex, "Application terminated unexpectedly.");
            throw;
        }
        finally
        {
            // Ensure logs are flushed on shutdown
            LoggerUtilities.StopLog();
        }
    }

    private static T RegisterConfigSection<T>(IServiceCollection services, IConfiguration configuration,
        string sectionName) where T : class, new()
    {
        T? configValue = configuration.GetSection(sectionName).Get<T>();
        if (configValue == null)
            throw new InvalidOperationException(
                $"Configuration section '{sectionName}' is missing or invalid for type {typeof(T).Name}.");
        services.AddSingleton(configValue);
        return configValue;
    }
}