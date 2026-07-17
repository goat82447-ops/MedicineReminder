# Keep the Docker build context clean and reproducible. Without this,
# `COPY . .` pulls in Windows-built bin/ and obj/ folders, which corrupt the
# Linux `dotnet publish` (stale intermediate files, wrong RIDs) and can cause
# spurious "type could not be found" / NETSDK errors during deploy.

# Build outputs / intermediates (must be rebuilt inside the container)
**/bin/
**/obj/

# Local secrets — never ship these into an image
**/appsettings.Local.json

# Git / IDE / OS noise
.git/
.gitignore
.vs/
.vscode/
*.user

# Docs not needed at runtime
**/*.md
