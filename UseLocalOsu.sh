#!/usr/bin/env bash

# UseLocalOsu.sh
#
# Switch between local osu project references and NuGet package references.
#
# This script modifies all .csproj files in the workspace to either use local
# project references from a sibling osu repository (../osu), or use NuGet
# packages with a specified version.
#
# When switching to local mode, the script adds XML comment markers to track
# which references were modified. When switching back to a NuGet version, the
# script uses these markers to correctly restore PackageReferences.
#
# Usage:
#   ./UseLocalOsu.sh              # Use local ../osu project references (default)
#   ./UseLocalOsu.sh local        # Use local ../osu project references
#   ./UseLocalOsu.sh <version>    # Use NuGet packages with specified version
#
# Prerequisites:
#   - perl (for multi-line regex substitution)

set -euo pipefail

ACTION="${1:-local}"

if [ "$ACTION" = "-h" ] || [ "$ACTION" = "--help" ]; then
    echo "Usage: $0 [local|<version>]"
    echo ""
    echo "  local       Use local ../osu project references (default)"
    echo "  <version>   Use NuGet packages with the specified version (e.g., 2025.1209.0)"
    echo ""
    echo "Examples:"
    echo "  $0              # Switch to local osu references"
    echo "  $0 local        # Switch to local osu references"
    echo "  $0 2025.1209.0  # Switch to NuGet packages with version 2025.1209.0"
    exit 0
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OSU_BASE="$(cd "$SCRIPT_DIR/.." 2>/dev/null && pwd)/osu"

# Check for perl
if ! command -v perl &>/dev/null; then
    echo "Error: perl is required but not found."
    exit 1
fi

if [ "$ACTION" = "local" ]; then
    if [ ! -d "$OSU_BASE" ]; then
        echo "Error: osu repository not found at: $OSU_BASE"
        echo "Expected the osu repository to be cloned as a sibling directory (../osu)."
        exit 1
    fi
    echo "Switching to local osu references from: $OSU_BASE"
else
    echo "Switching to osu NuGet version: $ACTION"
fi

echo ""

UPDATED=0

while IFS= read -r -d '' file; do
    cp "$file" "$file.bak"
    dir="$(dirname "$file")"

    # Compute relative path from csproj dir to osu repo using perl (portable)
    OSU_REL=$(perl -e '
        use File::Spec;
        my $rel = File::Spec->abs2rel($ARGV[0], $ARGV[1]);
        $rel =~ s|\\|/|g;
        print $rel;
    ' "$OSU_BASE" "$dir")

    if [ "$ACTION" = "local" ]; then
        # ==========================================================
        # LOCAL MODE: PackageReference -> ProjectReference
        # ==========================================================

        # Pattern 1: Multi-line <PackageReference> with <PrivateAssets>all</PrivateAssets>
        perl -0777 -i -pe '
            BEGIN { $osu_rel = "'"$OSU_REL"'"; }
            s{(?m)^([ \t]*)<PackageReference Include="(ppy\.osu\.[^"]*)" Version="[^"]*">[ \t]*\r?\n[ \t]*<PrivateAssets>all</PrivateAssets>[ \t]*\r?\n[ \t]*</PackageReference>}{
                my ($indent, $pkg) = ($1, $2);
                (my $proj = $pkg) =~ s/^ppy\.//;
                my $ref = "$osu_rel/$proj/$proj.csproj";
                "$indent<!-- UseLocalOsu: $pkg PrivateAssets=all -->\n$indent<ProjectReference Include=\"$ref\">\n$indent  <Private>false</Private>\n$indent</ProjectReference>";
            }ge' "$file"

        # Pattern 2: Self-closing <PackageReference ... />
        perl -0777 -i -pe '
            BEGIN { $osu_rel = "'"$OSU_REL"'"; }
            s{(?m)^([ \t]*)<PackageReference Include="(ppy\.osu\.[^"]*)" Version="[^"]*"\s*/>}{
                my ($indent, $pkg) = ($1, $2);
                (my $proj = $pkg) =~ s/^ppy\.//;
                my $ref = "$osu_rel/$proj/$proj.csproj";
                "$indent<!-- UseLocalOsu: $pkg -->\n$indent<ProjectReference Include=\"$ref\" />";
            }ge' "$file"
    else
        VERSION="$ACTION"

        # ==========================================================
        # VERSION MODE: Restore PackageReferences / update version
        # ==========================================================

        # Reverse Pattern 1: Restore multi-line ProjectReference (was PrivateAssets=all)
        perl -0777 -i -pe '
            BEGIN { $ver = "'"$VERSION"'"; }
            s{(?m)^([ \t]*)<!-- UseLocalOsu: (ppy\.osu\.\S+) PrivateAssets=all -->[ \t]*\r?\n[ \t]*<ProjectReference Include="[^"]*">[ \t]*\r?\n[ \t]*<Private>false</Private>[ \t]*\r?\n[ \t]*</ProjectReference>}{
                "$1<PackageReference Include=\"$2\" Version=\"$ver\">\n$1  <PrivateAssets>all</PrivateAssets>\n$1</PackageReference>";
            }ge' "$file"

        # Reverse Pattern 2: Restore self-closing ProjectReference
        perl -0777 -i -pe '
            BEGIN { $ver = "'"$VERSION"'"; }
            s{(?m)^([ \t]*)<!-- UseLocalOsu: (ppy\.osu\.\S+) -->[ \t]*\r?\n[ \t]*<ProjectReference Include="[^"]*"\s*/>}{
                "$1<PackageReference Include=\"$2\" Version=\"$ver\" />";
            }ge' "$file"

        # Update existing PackageReference versions
        perl -i -pe '
            BEGIN { $ver = "'"$VERSION"'"; }
            s/(<PackageReference Include="ppy\.osu\.[^"]*" Version=")[^"]*(")/\1$ver\2/g;
        ' "$file"
    fi

    if ! cmp -s "$file" "$file.bak"; then
        UPDATED=$((UPDATED + 1))
        rel_path="${file#"$SCRIPT_DIR"/}"
        echo "  Updated: $rel_path"
    fi
    rm -f "$file.bak"
done < <(find "$SCRIPT_DIR" -name "*.csproj" -print0)

echo ""
if [ "$UPDATED" -eq 0 ]; then
    echo "No files were modified."
elif [ "$ACTION" = "local" ]; then
    echo "Switched $UPDATED file(s) to local osu project references."
    echo "To restore NuGet references: ./UseLocalOsu.sh <version>"
else
    echo "Updated $UPDATED file(s) to osu NuGet version: $ACTION"
fi
