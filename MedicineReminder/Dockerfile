# syntax=docker/dockerfile:1

# ---- Stage 1: build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (better layer caching)
COPY MedicineReminder.csproj ./
RUN dotnet restore MedicineReminder.csproj

# Copy the rest of the source and publish a Release build
COPY . ./
RUN dotnet publish MedicineReminder.csproj -c Release -o /app/publish

# ---- Stage 2: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./

# Render provides PORT; the app also reads Scheduler__Enabled etc. from env vars
ENTRYPOINT ["dotnet", "MedicineReminder.dll", "--mode=dashboard"]
