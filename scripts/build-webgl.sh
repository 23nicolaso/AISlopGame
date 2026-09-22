#!/usr/bin/env bash
# Orbit Snake WebGL release build, the same shape as unity-flappybird/scripts/build-webgl.sh:
#   1. the 30 deterministic rule checks (OrbitHeadlessRunner, play mode, exit 0 required)
#   2. the WebGL player (RiftBuild.OrbitWebGL: edit-mode checks + build, prints BUILD_AND_TESTS_PASSED)
#   3. the bundle validator (scripts/check-webgl.mjs: itch.io limits, relative URLs, responsive canvas)
#   4. the render check (scripts/check-webgl-render.mjs: headless Chrome draws the start screen, pixels are inspected)
# The Editor must be closed. Logs go to $RUNNER_TEMP or /tmp.
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
project="$repo_root/My project"
unity_editor="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.1f1/Unity.app/Contents/MacOS/Unity}"
log_dir="${RUNNER_TEMP:-/tmp}"
verify_log="$log_dir/orbit-verify.log"
build_log="$log_dir/orbit-webgl-build.log"
test -x "$unity_editor" || { echo "Unity editor missing: $unity_editor" >&2; exit 1; }
if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null 2>&1; then echo "Close the Unity Editor first (project lock)" >&2; exit 1; fi

run_unity() { # $1 = label, $2 = log, rest = Unity arguments; prints progress every 15 s and tails the log on failure
  local label="$1" log="$2"; shift 2
  echo "$label; log: $log"
  "$unity_editor" -batchmode -nographics -projectPath "$project" "$@" -logFile "$log" &
  local pid=$!
  trap 'kill "$pid" 2>/dev/null || true' INT TERM
  while kill -0 "$pid" 2>/dev/null; do echo "$label running ($SECONDS seconds elapsed)"; sleep 15; done
  if ! wait "$pid"; then tail -80 "$log"; return 1; fi
}

if [ "${SKIP_VERIFY:-0}" != 1 ]; then
  run_unity "Rule checks" "$verify_log" -executeMethod OrbitHeadlessRunner.Run
  grep -q "ORBIT VERIFICATION PASS" "$verify_log"
fi
run_unity "WebGL build" "$build_log" -buildTarget WebGL -executeMethod RiftBuild.OrbitWebGL -quit
grep -q BUILD_AND_TESTS_PASSED "$build_log"
node "$repo_root/scripts/check-webgl.mjs" "$repo_root/Builds/ORBIT-web"
# 4. the bundle actually drawn in headless Chrome: lit land on the start screen, frame not mostly black. Node 20 needs the
#    WebSocket flag; Node 22+ has it built in.
ws_flag=""; node -e 'process.exit(typeof WebSocket==="function"?0:1)' 2>/dev/null || ws_flag="--experimental-websocket"
node $ws_flag "$repo_root/scripts/check-webgl-render.mjs" "$repo_root/Builds/ORBIT-web" --keep "$log_dir/orbit-webgl-render.png"
echo "WebGL build, Unity checks, package and render checks passed"
