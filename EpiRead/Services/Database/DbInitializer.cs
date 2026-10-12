namespace EpiRead.Services.Database;

/// <summary>
///     Provides methods to seed initial data into the database.
///     This includes roles, a super-admin user, and a default book category.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class DbInitializer
{
    private readonly InitialAccountConfig _config;
    private readonly DB _db;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DbInitializer" /> class.
    /// </summary>
    /// <param name="db">The database context used to perform seeding operations.</param>
    /// <param name="config">Configuration for the initial super-admin account.</param>
    /// <exception cref="ArgumentNullException">Throws when <paramref name="db" /> or <paramref name="config" /> is null.</exception>
    public DbInitializer(DB db, InitialAccountConfig config)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    ///     Seeds application roles into the database.
    ///     The method will add any roles defined in the <see cref="UserRoleType" /> enum that do not yet
    ///     exist in the database Roles table. If no new roles are found, the operation is skipped.
    /// </summary>
    /// <remarks>
    ///     This method uses <see cref="EnumHelper.ToList{UserRoleType}" /> to enumerate all roles and compares
    ///     them to existing role names in the database.
    /// </remarks>
    public void SeedRoles()
    {
        LoggerUtilities.Info("[Start] Starting role seeding.");
        List<UserRoleType> userRoles = EnumHelper.ToList<UserRoleType>();
        List<string> existingRoleNames = _db.Roles.Select(r => r.Name).ToList();
        List<Role> newRoles = userRoles
            .Where(role => !existingRoleNames.Contains(role.ToString()))
            .Select(role => new Role
            {
                Name = role.ToString()
            })
            .ToList();
        if (newRoles.Any())
        {
            LoggerUtilities.Info("[Process] Adding " + newRoles.Count + " new role(s) to database.");
            try
            {
                _db.Roles.AddRange(newRoles);
                _db.SaveChanges();
                LoggerUtilities.Info("[End] Seeded roles: " + string.Join(", ", newRoles.Select(r => r.Name)));
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error("[Process] Error while seeding roles: " + ex.Message);
                throw;
            }
        }
        else
        {
            LoggerUtilities.Warning("[End] No new roles to seed. Skipping.");
        }
    }

    /// <summary>
    ///     Creates a super-admin user using values from <see cref="InitialAccountConfig" /> when one does not exist.
    /// </summary>
    /// <remarks>
    ///     The created user will be assigned the role "SuperAdmin" and status <see cref="UserStatusType.Active" />.
    ///     If a super-admin already exists, this method will not modify the database.
    /// </remarks>
    public void SeedSuperAdmin()
    {
        const string superAdminRoleName = nameof(UserRoleType.SuperAdmin);
        User? currentSuperAdmin = _db.Users.FirstOrDefault(u => u.Role == superAdminRoleName);
        LoggerUtilities.Info("[Start] Starting super-admin seeding.");
        if (currentSuperAdmin == null)
        {
            User user = new User
            {
                FirstName = _config.FirstName,
                LastName = _config.LastName,
                UserName = _config.UserName,
                Email = _config.Email,
                Role = superAdminRoleName,
                Password = HashPassword(_config.Password),
                Status = nameof(UserStatusType.Active),
                CreatedAt = DateTime.Now.ToLocalTime(),
                UpdatedAt = DateTime.Now.ToLocalTime()
            };
            LoggerUtilities.Info("[Process] Creating super admin user '" + _config.UserName + "'.");
            try
            {
                _db.Users.Add(user);
                _db.SaveChanges();
                LoggerUtilities.Info("[End] Super admin user '" + _config.UserName + "' created.");
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error("[Process] Error while creating super admin user '" + _config.UserName + "': " +
                                      ex.Message);
                throw;
            }
        }
        else if (currentSuperAdmin.Status != nameof(UserStatusType.Active))
        {
            LoggerUtilities.Info("[Process] Reactivating existing super admin user '" + currentSuperAdmin.UserName +
                                 "'.");
            try
            {
                currentSuperAdmin.Status = nameof(UserStatusType.Active);
                currentSuperAdmin.UpdatedAt = DateTime.Now.ToLocalTime();
                _db.SaveChanges();
                LoggerUtilities.Info("[End] Super admin user '" + currentSuperAdmin.UserName + "' reactivated.");
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error("[Process] Error while reactivating super admin user '" +
                                      currentSuperAdmin.UserName + "': " + ex.Message);
                throw;
            }
        }
        else
        {
            LoggerUtilities.Warning("[End] Super admin user already exists. Skipping creation.");
        }
    }

    /// <summary>
    ///     Ensures a default book category exists in the database. If the category named "General" does not
    ///     exist it will be created with a slug of "general" and a short description.
    /// </summary>
    public void SeedDefaultBookCategory()
    {
        const string defaultCategoryName = "General";
        BookCategory? existingCategory = _db.BookCategories
            .FirstOrDefault(bc => bc.Name == defaultCategoryName);
        LoggerUtilities.Info("[Start] Starting default book category seeding.");
        if (existingCategory == null)
        {
            BookCategory bookCategory = new BookCategory
            {
                Name = "General",
                Slug = "general",
                Description = "Default category for uncategorized books.",
                CreatedAt = DateTime.Now.ToLocalTime(),
                UpdatedAt = DateTime.Now.ToLocalTime()
            };
            LoggerUtilities.Info("[Process] Creating default book category '" + defaultCategoryName + "'.");
            try
            {
                _db.BookCategories.Add(bookCategory);
                _db.SaveChanges();
                LoggerUtilities.Info("[End] Default book category '" + defaultCategoryName + "' created.");
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error("[Process] Error while creating default book category '" + defaultCategoryName +
                                      "': " + ex.Message);
                throw;
            }
        }
        else
        {
            LoggerUtilities.Warning(
                "[End] Default book category already exists. Skipping creation.");
        }
    }
}