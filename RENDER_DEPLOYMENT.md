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
- `SupabaseStorage__ServiceUrl`
- `SupabaseStorage__Region`
- `SupabaseStorage__AccessKeyId`
- `SupabaseStorage__SecretAccessKey`
- `SupabaseStorage__PublicUrl`
- `Gemini__ApiKey`

The Blueprint supplies the non-secret settings and configures `/health` as Render's health check endpoint.

## Supabase Storage

1. Create a Supabase project and open **Storage**.
2. Create a public bucket named `epiread`. Public access is required because the application saves direct URLs for book files and images.
3. Open **Storage > Configuration > S3** and enable the S3 protocol.
4. Generate an S3 access-key pair. Store the secret immediately because Supabase displays it only once.
5. Copy the S3 endpoint, region, access key ID, and secret access key into the matching Render variables.
6. Set `SupabaseStorage__PublicUrl` to `https://PROJECT_REF.supabase.co/storage/v1/object/public/epiread`.

Use the direct storage hostname shown by Supabase for `SupabaseStorage__ServiceUrl`; it normally resembles `https://PROJECT_REF.storage.supabase.co/storage/v1/s3`.

## Troubleshooting

- A startup failure before the port is detected usually means the SQL Server connection failed, because startup seeding accesses the database before the web server begins accepting requests.
- If login or form submissions loop, confirm Render is forwarding HTTPS and that no proxy strips `X-Forwarded-Proto`.
- Files stored only in the container are ephemeral. EpiRead stores durable book assets in Supabase Storage.
