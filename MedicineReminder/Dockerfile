# syntax=docker/dockerfile:1

# Build stage: restore and publish the app in a single step. (A separate
# "dotnet restore" layer followed by "dotnet publish --no-restore" can fail
# with NETSDK1064 "Package ... was not found" if a build cache reuses a
# stale/incomplete restore layer — merging avoids that class of failure.)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish MedicineReminder.csproj -c Release -o /app/publish

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
