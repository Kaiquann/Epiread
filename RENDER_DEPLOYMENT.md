# Deploy EpiRead to Render

EpiRead deploys as a Docker web service using the repository's `render.yaml` Blueprint.

## Before deploying

1. Rotate the database, SMTP, Cloudflare Turnstile, IPInfo, MEGA, Gemini, and initial administrator credentials if they have ever been shared or committed.
2. Confirm the external SQL Server permits connections from Render. Render does not provide a managed SQL Server database.
3. Make sure the target database schema already exists. EpiRead seeds application data at startup, but it does not automatically apply Entity Framework migrations.

## Create the service

1. Push the deployment files to the GitHub repository.
2. In the Render Dashboard, choose **New > Blueprint**.
3. Connect the EpiRead GitHub repository. Render detects `render.yaml` in the repository root.
4. Enter every value marked as required. Render stores these values outside Git.
5. Apply the Blueprint and follow the first deployment in the Render logs.

ASP.NET Core maps configuration environment variables with a double underscore. For example, `ConnectionStrings__DBConnection` overrides `ConnectionStrings:DBConnection`.

## Required secret values

- `ConnectionStrings__DBConnection`
- `InitialAccount__FirstName`
- `InitialAccount__LastName`
- `InitialAccount__UserName`
- `InitialAccount__Email`
- `InitialAccount__Password`
- `Smtp__Host`
- `Smtp__Username`
- `Smtp__Password`
- `Smtp__From`
- `CfCaptcha__SiteKey`
- `CfCaptcha__SecretKey`
- `IpInfo__Token`
- `MegaStorage__Email`
- `MegaStorage__Password`
- `Gemini__ApiKey`

The Blueprint supplies the non-secret settings and configures `/health` as Render's health check endpoint.

## MEGA Storage

1. Use a dedicated MEGA account for the application rather than a personal account.
2. Set `MegaStorage__Email` and `MegaStorage__Password` in Render.
3. Keep `MegaStorage__RootFolder` set to `EpiRead`. The application creates this folder and its subfolders automatically.
4. Do not commit the account credentials. Files are streamed through `/Storage/Mega`; the MEGA password is never sent to browsers.
5. Re-upload any files that belonged to a deleted storage provider. Changing providers cannot restore deleted files.

### Book cover recovery

Original library covers are bundled in `EpiRead/wwwroot/images/books`, with a title-to-file catalog.
Public book views use these copies for the retired Supabase project and as a fallback when a current
storage URL fails. Unknown covers fall back to the existing unavailable-cover image. Current uploads
continue to use their stored URLs; this recovery does not restore missing book documents or custom uploads.
Run `./scripts/Build-BookCovers.ps1` to regenerate the bundled covers from `TestData/Books` and
`TestData/TestBooks`. Commit the generated assets and catalog along with the application changes,
then redeploy the Render service.

## Troubleshooting

- A startup failure before the port is detected usually means the SQL Server connection failed, because startup seeding accesses the database before the web server begins accepting requests.
- If login or form submissions loop, confirm Render is forwarding HTTPS and that no proxy strips `X-Forwarded-Proto`.
- Files stored only in the container are ephemeral. EpiRead stores durable book assets in MEGA.
