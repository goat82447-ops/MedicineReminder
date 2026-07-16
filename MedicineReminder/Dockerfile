# syntax=docker/dockerfile:1

# Build stage: restore and publish the app.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MedicineReminder.csproj ./
RUN dotnet restore MedicineReminder.csproj

COPY . .
RUN dotnet publish MedicineReminder.csproj -c Release -o /app/publish --no-restore

# Runtime stage: aspnet image needed because --mode=dashboard uses
# WebApplication/minimal APIs (FrameworkReference to Microsoft.AspNetCore.App).
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Runs the always-on dashboard. Set PORT (Render sets this automatically for
# Docker web services) so the app binds 0.0.0.0:$PORT instead of localhost.
# Set Scheduler__Enabled=true to also send the medicine/daily reminder
# emails on an internal timer instead of via GitHub Actions cron.
ENTRYPOINT ["dotnet", "MedicineReminder.dll", "--mode=dashboard"]
