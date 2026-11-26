#!/usr/bin/env bash
# Cloudflare build for Tracker (Blazor WebAssembly, frontend only).
#
# Cloudflare Workers settings:
#   Build command:  bash build.sh
#   Deploy command: npx wrangler deploy   (serves output/wwwroot, see wrangler.jsonc)
#
# Cloudflare's build image has no .NET SDK, so this installs the version pinned in
# global.json, then publishes static files. No secrets or tokens are needed.
set -euo pipefail

DOTNET_DIR="$HOME/.dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
# Only affects the SDK process running the build, not the published app.
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --jsonfile global.json --install-dir "$DOTNET_DIR"
rm -f dotnet-install.sh
export PATH="$DOTNET_DIR:$PATH"

dotnet --version
rm -rf output
dotnet publish src/Tracker/Tracker.csproj -c Release -o output

echo "Published to output/wwwroot"
