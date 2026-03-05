#!/usr/bin/env bash

# Publishes project(s) for release while avoiding local osu project-reference asset leakage.
#
# If local osu references are active (UseLocalOsu markers found in .csproj files),
# this script temporarily switches to NuGet osu references, runs dotnet publish,
# and restores local references afterwards.
#
# Before publishing, the script packs required local osu projects from ../osu
# into a local NuGet feed and publishes using that feed.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
USE_LOCAL_SCRIPT="$SCRIPT_DIR/UseLocalOsu.sh"
OSU_REPO_PATH="$(cd "$SCRIPT_DIR/../osu" 2>/dev/null && pwd || true)"

if [ ! -f "$USE_LOCAL_SCRIPT" ]; then
    echo "Error: missing script: $USE_LOCAL_SCRIPT"
    exit 1
fi

OSU_VERSION=""
TARGET="osu-plugins.slnx"
CONFIGURATION="Release"
OUTPUT="artifacts/publish"
NO_RESTORE=0
NO_BUILD=0
DOTNET_ARGS=()

usage() {
    echo "Usage: $0 <version> [options] [-- <extra dotnet publish args>]"
    echo ""
    echo "Options:"
    echo "  --osu-version <version>   NuGet version of ppy.osu.* packages"
    echo "                           (or pass as first positional argument)"
    echo "  --target <path>           Publish target path"
    echo "                           (default: $TARGET)"
    echo "  --configuration <name>    Build configuration (default: $CONFIGURATION)"
    echo "  --output <path>           Output directory for publish"
    echo "                           (default: $OUTPUT; ignored for solution targets)"
    echo "  --no-restore              Pass --no-restore to dotnet publish"
    echo "  --no-build                Pass --no-build to dotnet publish"
    echo "  -h, --help                Show this help message"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --osu-version)
            OSU_VERSION="${2:-}"
            shift 2
            ;;
        --target)
            TARGET="${2:-}"
            shift 2
            ;;
        --configuration)
            CONFIGURATION="${2:-}"
            shift 2
            ;;
        --output)
            OUTPUT="${2:-}"
            shift 2
            ;;
        --no-restore)
            NO_RESTORE=1
            shift
            ;;
        --no-build)
            NO_BUILD=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        --)
            shift
            DOTNET_ARGS+=("$@")
            break
            ;;
        *)
            if [ -z "$OSU_VERSION" ]; then
                OSU_VERSION="$1"
                shift
            else
                echo "Unknown option: $1"
                usage
                exit 1
            fi
            ;;
    esac
done

cd "$SCRIPT_DIR"

if [ -z "$OSU_VERSION" ]; then
    echo "Error: please pass a version number (e.g. ./PackForRelease.sh 2025.1209.0)."
    exit 1
fi

if [ -z "$OSU_REPO_PATH" ] || [ ! -d "$OSU_REPO_PATH" ]; then
    echo "Error: osu repository not found at: $SCRIPT_DIR/../osu"
    exit 1
fi

if find . -name "*.csproj" -type f -print0 | xargs -0 grep -Fq "<!-- UseLocalOsu:"; then
    LOCAL_MODE_ACTIVE=1
else
    LOCAL_MODE_ACTIVE=0
fi

SWITCHED_FROM_LOCAL=0
TEMP_NUGET_CONFIG=""

cleanup_temp_files() {
    if [ -n "$TEMP_NUGET_CONFIG" ] && [ -f "$TEMP_NUGET_CONFIG" ]; then
        rm -f "$TEMP_NUGET_CONFIG"
    fi
}

restore_local_if_needed() {
    if [ "$SWITCHED_FROM_LOCAL" -eq 1 ]; then
        echo "Restoring local osu references..."
        "$USE_LOCAL_SCRIPT" local
        echo "Local references restored."
    fi
}

trap 'cleanup_temp_files; restore_local_if_needed' EXIT

LOCAL_FEED_PATH="$SCRIPT_DIR/artifacts/local-osu-feed"
# Clean previous local feed to avoid stale packages
rm -rf "$LOCAL_FEED_PATH"
mkdir -p "$LOCAL_FEED_PATH"

LOCAL_PACKAGES_PATH="$SCRIPT_DIR/artifacts/local-osu-packages"
rm -rf "$LOCAL_PACKAGES_PATH"
mkdir -p "$LOCAL_PACKAGES_PATH"

mapfile -t PACKAGE_IDS < <(
    {
        find . -name "*.csproj" -type f -print0 |
        xargs -0 grep -hoE '<PackageReference Include="ppy\.osu\.[^"]+"' |
        sed -E 's/^<PackageReference Include="([^"]+)"$/\1/'

        find . -name "*.csproj" -type f -print0 |
        xargs -0 grep -hoE '<!-- UseLocalOsu: ppy\.osu\.[^[:space:]]+' |
        sed -E 's/^<!-- UseLocalOsu: (ppy\.osu\.[^[:space:]]+)$/\1/'
    } | sort -u
)

if [ "${#PACKAGE_IDS[@]}" -eq 0 ]; then
    echo "Error: no ppy.osu.* PackageReference entries were found in this repository."
    exit 1
fi

echo "Packing local osu packages from: $OSU_REPO_PATH"
for package_id in "${PACKAGE_IDS[@]}"; do
    project_name="${package_id#ppy.}"
    project_path="$OSU_REPO_PATH/$project_name/$project_name.csproj"

    if [ ! -f "$project_path" ]; then
        echo "Error: local osu project not found for package $package_id at: $project_path"
        exit 1
    fi

    echo "  Packing $package_id"
    dotnet pack "$project_path" -c "$CONFIGURATION" -o "$LOCAL_FEED_PATH" -p:Version="$OSU_VERSION"
done

TEMP_NUGET_CONFIG="${TMPDIR:-/tmp}/nuget.local-osu.$$.config"
cat > "$TEMP_NUGET_CONFIG" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-osu" value="$LOCAL_FEED_PATH" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

if [ "$LOCAL_MODE_ACTIVE" -eq 1 ]; then
    echo "Detected local osu references. Switching to NuGet version $OSU_VERSION for publishing..."
    "$USE_LOCAL_SCRIPT" "$OSU_VERSION"
    SWITCHED_FROM_LOCAL=1
elif [ -n "$OSU_VERSION" ]; then
    echo "Applying requested NuGet version $OSU_VERSION before publishing..."
    "$USE_LOCAL_SCRIPT" "$OSU_VERSION"
fi

# Use isolated packages path to bypass NuGet global cache
PUBLISH_ARGS=(publish "$TARGET" -c "$CONFIGURATION" --configfile "$TEMP_NUGET_CONFIG" "-p:RestorePackagesPath=$LOCAL_PACKAGES_PATH")

if [[ "$TARGET" =~ \.slnx?$ ]]; then
    if [ -n "$OUTPUT" ]; then
        echo "Output path is ignored for solution target: $TARGET"
    fi
else
    if [ -n "$OUTPUT" ]; then
        PUBLISH_ARGS+=(-o "$OUTPUT")
    fi
fi

if [ "$NO_RESTORE" -eq 1 ]; then
    PUBLISH_ARGS+=(--no-restore)
fi
if [ "$NO_BUILD" -eq 1 ]; then
    PUBLISH_ARGS+=(--no-build)
fi
PUBLISH_ARGS+=("${DOTNET_ARGS[@]}")

echo ""
echo "Running: dotnet ${PUBLISH_ARGS[*]}"
dotnet "${PUBLISH_ARGS[@]}"

echo ""
echo "Publish completed successfully."
