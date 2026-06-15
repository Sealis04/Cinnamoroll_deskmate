#!/bin/bash
# SessionStart hook: install the .NET 8 SDK and restore the Godot C# project's
# NuGet packages so `dotnet build` works as a compile-check in web sessions.
#
# Note: a Godot 4 C# project compiles purely from the Godot.NET.Sdk + GodotSharp
# NuGet packages — the heavy Godot editor binary is NOT required just to build,
# so we deliberately don't download it here. (Running the app still needs the
# Godot 4.3 .NET editor on a desktop; that's a Windows-side step.)
set -euo pipefail

# Only run in the Claude Code on the web (remote) environment.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT="$CLAUDE_PROJECT_DIR/DesktopPet/DesktopPet.csproj"

SUDO=""
if [ "$(id -u)" -ne 0 ]; then SUDO="sudo"; fi

# 1. Install .NET 8 SDK (idempotent). The official dotnet-install.sh pulls from
#    builds.dotnet.microsoft.com, which is not in this environment's egress
#    allowlist, so we use Ubuntu's apt feed instead.
if ! command -v dotnet >/dev/null 2>&1; then
  echo "[session-start] installing .NET 8 SDK via apt"
  export DEBIAN_FRONTEND=noninteractive
  # apt-get update may return non-zero on unrelated third-party PPAs that aren't
  # allowlisted; the Ubuntu repos still refresh, which is all we need.
  $SUDO apt-get update >/dev/null 2>&1 || true
  $SUDO apt-get install -y dotnet-sdk-8.0 >/dev/null
else
  echo "[session-start] .NET SDK already present: $(dotnet --version)"
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# 2. Restore NuGet packages (Godot.NET.Sdk, GodotSharp). Cached in the container.
echo "[session-start] restoring NuGet packages"
dotnet restore "$PROJECT" >/dev/null

# 3. Persist env vars for the rest of the session.
{
  echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
  echo "export DOTNET_NOLOGO=1"
} >> "$CLAUDE_ENV_FILE"

echo "[session-start] ready — compile-check with: dotnet build $PROJECT"
