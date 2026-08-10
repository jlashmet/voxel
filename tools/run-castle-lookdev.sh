#!/bin/bash
# Run the castle exterior lookdev tests and display the generated images
# Usage: ./tools/run-castle-lookdev.sh [view-filter]
# Examples:
#   ./tools/run-castle-lookdev.sh              # capture all views
#   ./tools/run-castle-lookdev.sh approach    # capture only "approach" view
#   ./tools/run-castle-lookdev.sh smooth      # capture views matching "smooth"

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="/tmp/castle_lookdev"
VIEW_FILTER="${1:-}"

echo "Running castle lookdev tests..."
UNITY_MAX_MINUTES=30 UNITY_MIN_FREE_MB=2048 \
    VOXEL_LOOKDEV_FILTER="$VIEW_FILTER" \
    "$SCRIPT_DIR/unity-run.sh" \
        -batchmode \
        -projectPath "$PROJECT_PATH" \
        -runTests \
        -testPlatform playmode \
        -testFilter "CastleExteriorLookdevTests" \
        -testResults /tmp/castle_lookdev_results.xml \
        -logFile /tmp/test.log 2>&1 | tail -40

echo ""
ls -lh "$OUTPUT_DIR"/*.png 2>/dev/null
