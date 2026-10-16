# Deploy EpiRead to Render

EpiRead deploys as a Docker web service using the repository's `render.yaml` Blueprint.

## Before deploying

1. Rotate the database, SMTP, Cloudflare Turnstile, IPInfo, Dropbox, Gemini, and initial administrator credentials if they have ever been shared or committed.
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
- `Dropbox__RefreshToken`
- `Dropbox__ClientId`
- `Dropbox__ClientSecret`
- `Gemini__ApiKey`

The Blueprint supplies the non-secret settings and configures `/health` as Render's health check endpoint.

## Troubleshooting

- A startup failure before the port is detected usually means the SQL Server connection failed, because startup seeding accesses the database before the web server begins accepting requests.
- If login or form submissions loop, confirm Render is forwarding HTTPS and that no proxy strips `X-Forwarded-Proto`.
- Files stored only in the container are ephemeral. EpiRead's durable book assets should remain in Dropbox or another external store.
