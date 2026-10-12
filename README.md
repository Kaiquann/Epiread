## Tutorial to host ASP.NET Core 8.0.x application on IIS

1. Install ASP.NET Core Runtime 8.0.x - https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-aspnetcore-8.0.22-windows-hosting-bundle-installer
2. Install IIS - Go to Server Manager -> Manage -> Add Roles and Features -> Select Web Server (IIS) and install
3. Restart World Wide Web Publishing Service after installation is complete

```
net stop was /y
net start w3svc
```

4. Install dotnet-ef on windows

```
dotnet tool install --global dotnet-ef --version 8.0.*
```

5. Setup your application in IIS
   - Open IIS Manager
   - Right click on "Sites" and select "Add Website..."
   - Fill in the details like Site name, Physical path (point to your published application folder), and Port
   - Click OK to create the website
6. Configure Application Pool
   - In IIS Manager, go to "Application Pools"
   - Find the application pool for your website, right click and select "Advanced Settings..."
   - Set ".NET CLR Version" to "No Managed Code"
   - Set "Identity" to "ApplicationPoolIdentity"
   - Click OK to save changes
7. Set folder permissions
   - Navigate to your published application folder
   - Right click the folder, select "Properties"
   - Go to the "Security" tab and click "Edit..."
   - Click "Add...", type "IIS AppPool\YourAppPoolName" (replace YourAppPoolName with your actual app pool name), and click "Check Names"
   - Grant "Read & execute", "List folder contents", and "Read" permissions
   - Click OK to apply the permissions
8. Restart IIS
   - Open Command Prompt as Administrator
   - Run the command: `iisreset`
9. Browse to your application using the configured port to verify it is running correctly.
