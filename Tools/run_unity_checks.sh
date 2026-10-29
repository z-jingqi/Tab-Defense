#!/usr/bin/env bash
set -euo pipefail
# Set UNITY_EDITOR to your international Unity 6.3 LTS executable.
: "${UNITY_EDITOR:?Set UNITY_EDITOR to the Unity executable}"
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$project_dir/TestResults"
"$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$project_dir" \
  -executeMethod TapDefense.Editor.BuildAutomation.ValidateProject \
  -logFile "$project_dir/TestResults/validation.log"
for test_mode in EditMode PlayMode; do
  # A failed launch must not leave an old successful XML available to the result checker.
  rm -f "$project_dir/TestResults/$test_mode.xml"
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$project_dir" \
    -runTests -testPlatform "$test_mode" \
    -testResults "$project_dir/TestResults/$test_mode.xml" \
    -logFile "$project_dir/TestResults/$test_mode.log"
done
python3 "$project_dir/Tools/check_test_results.py" "$project_dir/TestResults"
