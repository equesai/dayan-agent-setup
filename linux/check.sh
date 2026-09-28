#!/usr/bin/env bash
# Dayan Agent -- is the app really installed? (owner, 2026-09-26: "a test to
# detect the actual installation afterwards").
#
# Run from Dayan's guided chat flow, in Terminal, after the member installed
# the app from its maker's own page:
#   (curl -fsSL <dayan>/api/device-setup/<code>/check.sh || wget -qO- <the same>) 2>/dev/null | bash
# It looks for the app the member picked (OpenCode, Claude Code or Codex),
# says what it found, and tells Dayan -- found or not, and the version,
# nothing else. It changes nothing on the computer and needs no key.

DAYAN_API="${DAYAN_API:-__DAYAN_API__}"
DAYAN_CODE="${DAYAN_CODE:-__DAYAN_CODE__}"
APP="${DAYAN_APP:-__DAYAN_APP__}"

FOUND=false
VERSION=""
WHERE=""

if [ -t 1 ]; then GREEN=$'\033[38;2;90;200;120m'; RED=$'\033[38;2;235;100;90m'; GOLD=$'\033[38;2;227;179;65m'; RESET=$'\033[0m'
else GREEN=""; RED=""; GOLD=""; RESET=""; fi

name_of() { case "$1" in claude) echo "Claude Code" ;; codex) echo "Codex" ;; *) echo "OpenCode" ;; esac; }

version_in() { grep -Eo '[0-9]+\.[0-9]+(\.[0-9]+)?([-+][0-9A-Za-z.]+)?' | head -1; }

found_cli() { # found_cli <path>: a command-line app is there; ask it its version
  WHERE="$1"; FOUND=true
  VERSION=$(timeout 20 "$1" --version </dev/null 2>/dev/null | version_in)
}

find_cli() { # find_cli <command> <more places...>: on the PATH, or where its installer puts it
  local cmd="$1" place; shift
  place=$(command -v "$cmd" 2>/dev/null)
  if [ -n "$place" ] && [ -x "$place" ]; then found_cli "$place"; return 0; fi
  for place in "$@"; do
    if [ -x "$place" ] && [ ! -d "$place" ]; then found_cli "$place"; return 0; fi
  done
  return 1
}

package_version() { # the opencode package's version, from the system's package tool
  if command -v dpkg-query >/dev/null 2>&1; then dpkg-query -W -f='${Version}' opencode 2>/dev/null | version_in
  elif command -v rpm >/dev/null 2>&1; then rpm -q --qf '%{VERSION}' opencode 2>/dev/null | version_in; fi
}

find_opencode() {
  local app
  # The desktop app, as its .deb / .rpm installs it (or Dayan's earlier setup
  # in the member's own folder); then the terminal app.
  for app in "$(command -v ai.opencode.desktop 2>/dev/null)" /opt/OpenCode/ai.opencode.desktop \
             "$HOME/.local/share/dayan/opencode-desktop/AppRun" "$HOME/.local/share/dayan/OpenCode.AppImage"; do
    if [ -n "$app" ] && [ -x "$app" ]; then WHERE="$app"; FOUND=true; VERSION=$(package_version); return 0; fi
  done
  find_cli opencode "$HOME/.opencode/bin/opencode" "$HOME/.local/bin/opencode"
}

main() {
  local name status body
  name=$(name_of "$APP")
  echo
  echo "${GOLD}Dayan Agent -- looking for $name on this computer...${RESET}"
  case "$APP" in
    claude) find_cli claude "$HOME/.local/bin/claude" "$HOME/.claude/local/claude" /usr/bin/claude /usr/local/bin/claude ;;
    codex) find_cli codex "$HOME/.local/bin/codex" /usr/local/bin/codex /usr/bin/codex ;;
    *) find_opencode ;;
  esac
  if [ "$FOUND" = true ]; then
    echo "${GREEN}✓  $name ${VERSION:+$VERSION }is installed${RESET}  ($WHERE)"
  else
    echo "${RED}✗  $name isn't installed on this computer yet.${RESET}"
    echo "   Install it from its page (Dayan has the button), then run this line again."
  fi
  # Tell Dayan: found or not, and the version -- nothing else.
  VERSION=$(printf '%s' "$VERSION" | tr -cd '0-9A-Za-z.+_-' | cut -c1-40)
  body=$(printf '{"app":"%s","found":%s,"version":"%s"}' "$APP" "$FOUND" "$VERSION")
  if command -v curl >/dev/null 2>&1; then
    status=$(curl -sS -m 30 -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' \
      --data-binary "$body" "$DAYAN_API/api/device-setup/$DAYAN_CODE/check" 2>/dev/null)
  else
    status=$(wget -q -S -T 30 -t 1 -O /dev/null --header='Content-Type: application/json' --post-data="$body" \
      "$DAYAN_API/api/device-setup/$DAYAN_CODE/check" 2>&1 | sed -n 's/^ *HTTP\/[0-9.]* \([0-9][0-9][0-9]\).*/\1/p' | tail -1)
  fi
  if [ "$status" = 200 ]; then
    echo "Done -- go back to Dayan and press \"Show the result\"."
  else
    echo "${RED}Dayan couldn't be told (HTTP ${status:-000}) -- check your internet and run this line again.${RESET}"
  fi
  echo
}

main "$@"
