#!/usr/bin/env bash
# Dayan Agent setup for Linux -- "Run Dayan Agent on my device" (owner,
# 2026-09-25; Windows and Linux only since 2026-09-26 -- Windows has
# DayanSetup.exe, a Mac is not set up).
#
# Run from Dayan's guided chat flow, in Terminal:
#   (curl -fsSL <dayan>/api/device-setup/<code>/setup.sh || wget -qO- <the same>) 2>/dev/null | bash
# A fresh Ubuntu desktop has wget and no curl, Arch has curl and no wget: the
# line and this script work with either.
#
# The member installs the app first, from its maker's own page -- OpenCode,
# Claude Code or Codex (owner, 2026-09-26: "the student would be guided to
# install and then Dayan would proceed with what is left"). This setup finds
# it and connects it to Dayan; it installs no app itself. A missing app is
# named, with its maker's page, and nothing changes.
#
# It asks before it changes anything (owner, 2026-09-26): it lists what it
# will change and waits for Enter. The one step that needs the computer's
# password -- the USB serial-port group for Arduino uploads -- is asked on its
# own screen and has a way without it (the command to run later).
# The key never reaches the log, the screen, a command line or a process list.
#
# Removing it (owner, 2026-09-27 -- free code signing through SignPath
# Foundation asks for a way to uninstall): before its first change the setup
# writes ~/.local/share/dayan/uninstall.sh (these same functions), and
# `bash ~/.local/share/dayan/uninstall.sh` -- or this script with --uninstall --
# takes out what it added and puts back the values it replaced (its record:
# ~/.local/share/dayan/installed). The app, the work folder and the Arduino
# board files stay.
#
# Everything lives in functions and runs from the last line, so the whole
# script is read before anything runs (it arrives through a pipe).

DAYAN_API="${DAYAN_API:-__DAYAN_API__}"
DAYAN_CODE="${DAYAN_CODE:-__DAYAN_CODE__}"
DAYAN_HOME="${DAYAN_HOME:-$HOME}"
NO_INSTALL="${DAYAN_NO_INSTALL:-}" # tests: the Arduino tools are not installed
KEY_ENV="${DAYAN_KEY_ENV:-}"
AUTO_YES="${DAYAN_YES:-}"          # tests: nothing waits for a key; the password step is taken with sudo -n
NO_SUDO="${DAYAN_NO_SUDO:-}"       # tests: the member says no to the password step
APP="${DAYAN_APP:-}"               # opencode | claude | codex (from the choices; tests may set it)
UNINSTALL="${DAYAN_UNINSTALL:-}"   # or --uninstall: remove what the setup added

DATA_DIR="$HOME/.local/share/dayan"
LOG="$DATA_DIR/setup.log"
STATE="$DATA_DIR/installed"        # what the setup changed (name=value lines), for removing it
# The pack's files, for removing a setup from before the record existed.
KNOWN_PACK="agents/dayan.md agents/dayan-maker.md agents/dayan-web.md agents/dayan-game.md agents/dayan-hardware.md
agents/dayan-tester.md skills/arduino-project/SKILL.md skills/game-project/SKILL.md skills/study-coach/SKILL.md
skills/web-project/SKILL.md skills/dayan-tutor/SKILL.md skills/dayan-maker/SKILL.md"
WORK_DIR="$DAYAN_HOME/Dayan"       # where Claude Code and Codex start: the member's projects live here
TMP_DIR=""
KEY=""
FIRST_NAME=""
WANT_ARDUINO=""
APP_NAME=""
INSTALL_PAGE=""
APP_BIN=""       # the app this setup found
APP_KIND=""      # OpenCode: desktop | cli
APP_FLAGS=""
ARCH=""
FETCH=""         # curl | wget
SERIAL_GROUP=""  # the group that may use the USB serial port (dialout; uucp on Arch)
SERIAL_OK=""     # yes: add the member to it (asked, the password given)
SUDO_USED=""     # the password was given: forget it when the setup ends
SUDO_KEEPER=""   # the process that keeps it alive meanwhile
TOOLS=""         # the tools lookup, read once
PATH_LINE=""     # yes: ~/.local/bin goes on the PATH in the shell's startup files

colors() {
  if [ -t 1 ]; then
    GOLD=$'\033[38;2;227;179;65m'; SOFT=$'\033[38;2;170;170;170m'; GREEN=$'\033[38;2;90;200;120m'
    RED=$'\033[38;2;235;100;90m'; WHITE=$'\033[97m'; RESET=$'\033[0m'
  else
    GOLD=""; SOFT=""; GREEN=""; RED=""; WHITE=""; RESET=""
  fi
}

colors

LOGO='            ▄▄▄▄▄▄
       ▄▄█▀▀▀▀▀▀▀▀▀▀█▄▄
     ▄█▀          ▄   ▀█▄
   ▄█▀    ▄▄███████     ▀█▄
  ▄█▀   ▄███████████▄     █▄
 ▄█    ██████████████▄     █▄
 ██   ▄███████████████▄    ██
 ██   ███████▄▀████████▄   ██
 ██   ▀███████▄▄    ▀█▀    ██
 ▀█    ▀██████████▄▄       █▀
  ▀█    ▀███████████      █▀
   ▀█▄▄▄██████████████▄▄▄█▀
     ▀██████████████████▀
       ▀▀████████████▀▀
           ▀▀▀▀▀▀▀▀'

log() { # nothing once a removal has taken the folder away
  [ -d "${LOG%/*}" ] || return 0
  printf '%s  %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*" >>"$LOG" 2>/dev/null
}

# The record of what the setup changed: one name=value line each, written once.
state_add() { grep -qsxF "$1=$2" "$STATE" || printf '%s=%s\n' "$1" "$2" >>"$STATE"; }
state_get() { sed -n "s/^$1=//p" "$STATE" 2>/dev/null; }
state_has() { grep -qs "^$1=" "$STATE"; }

host_of() { printf '%s' "$1" | sed -e 's#^[a-zA-Z]*://##' -e 's#[/:].*##'; }

cols() { local c; c=$(tput cols 2>/dev/null || echo 80); [ "$c" -lt 40 ] && c=40; echo "$c"; }
indent() { local c; c=$(cols); local p=$(( (c - 72) / 2 )); [ "$p" -lt 2 ] && p=2; printf '%*s' "$p" ''; }
line() { printf '%s%s%s%s\n' "$(indent)" "${2:-}" "$1" "$RESET"; }
center() { local c; c=$(cols); local p=$(( (c - ${#1}) / 2 )); [ "$p" -lt 0 ] && p=0; printf '%*s%s%s%s\n' "$p" '' "${2:-}" "$1" "$RESET"; }
para() { # wrap words to the column (globbing off: the text may hold a '*')
  local width=72 text="$1" color="${2:-}" out="" word
  set -f
  for word in $text; do
    if [ -n "$out" ] && [ $(( ${#out} + 1 + ${#word} )) -gt $width ]; then line "$out" "$color"; out="$word"
    elif [ -z "$out" ]; then out="$word"
    else out="$out $word"; fi
  done
  set +f
  [ -n "$out" ] && line "$out" "$color"
  return 0
}

header() {
  printf '\033[2J\033[3J\033[H'; echo
  local c pad; c=$(cols); pad=$(( (c - 30) / 2 )); [ "$pad" -lt 0 ] && pad=0
  while IFS= read -r row; do printf '%*s%s%s%s\n' "$pad" '' "$GOLD" "$row" "$RESET"; done <<EOF
$LOGO
EOF
  echo; center "D A Y A N   A G E N T" "$GOLD"; center "$1" "$SOFT"; echo
}

has_tty() { { : </dev/tty; } 2>/dev/null; }  # a keyboard this script can read (opening it, not just its file)

readkey() { # one key from the keyboard; an arrow key's escape sequence is not Esc. A keyboard that
            # cannot be read answers Esc -- never a silent Enter, which would agree for her.
  local k rest
  IFS= read -rsn1 k 2>/dev/null </dev/tty || { printf '\033'; return; }
  if [ "$k" = $'\033' ]; then IFS= read -rsn2 -t 0.05 rest 2>/dev/null </dev/tty; [ -n "$rest" ] && k="~"; fi
  printf '%s' "$k"
}

wait_enter_or_esc() { # 0 = Enter, 1 = Esc
  [ -n "$AUTO_YES" ] && return 0
  local k
  while :; do
    k=$(readkey)
    [ -z "$k" ] && return 0
    [ "$k" = $'\033' ] && return 1
  done
}

ask_yes_no() { # 0 = yes (Enter or Y), 1 = no (N or Esc); tests answer by DAYAN_NO_SUDO
  if [ -n "$AUTO_YES" ]; then [ -z "$NO_SUDO" ]; return; fi
  local k
  while :; do
    k=$(readkey)
    case "$k" in ""|y|Y) return 0 ;; n|N|$'\033') return 1 ;; esac
  done
}

read_masked() {
  local key="" c
  while :; do
    IFS= read -rsn1 c 2>/dev/null </dev/tty || return 1
    case "$c" in
      "") break ;;
      $'\033') return 1 ;;
      $'\177'|$'\b') if [ -n "$key" ]; then key="${key%?}"; printf '\b \b' >/dev/tty; fi ;;
      *) case "$c" in [[:print:]]) if [ "$c" != " " ]; then key="$key$c"; printf '*' >/dev/tty; fi ;; esac ;;
    esac
  done
  KEY_READ="$key"
  return 0
}

fail() { line "✗  $1" "$RED"; log "failed: $1"; }

# ── Reading JSON: python3 when there is one, else plain text tools ──────────
has_python() { command -v python3 >/dev/null 2>&1; }

json_get() { # json_get <file> <key>  (a top-level string, number or bool)
  if has_python; then
    python3 -c "import json,sys
try: v=json.load(open(sys.argv[1])).get(sys.argv[2],'')
except Exception: v=''
print('' if v is None else (str(v).lower() if isinstance(v,bool) else v))" "$1" "$2" </dev/null 2>/dev/null
  else
    tr -d '\n' <"$1" | sed -n "s/.*\"$2\"[[:space:]]*:[[:space:]]*\"\{0,1\}\([^\",}]*\).*/\1/p" | head -1
  fi
}

tool_field() { # tool_field <tool> <field>, from the tools lookup (Dayan writes it compact, one level deep)
  if has_python; then
    python3 -c "import json,sys; print((json.load(open(sys.argv[1])).get(sys.argv[2]) or {}).get(sys.argv[3],''))" \
      "$TOOLS" "$1" "$2" </dev/null 2>/dev/null
  else
    tr -d '\n' <"$TOOLS" | sed -n "s/.*\"$1\":{\([^}]*\)}.*/\1/p" | sed -n "s/.*\"$2\":\"\{0,1\}\([^\",]*\).*/\1/p"
  fi
}

# ── Talking to Dayan: curl or wget ─────────────────────────────────────────
pick_fetch() {
  if command -v curl >/dev/null 2>&1; then FETCH=curl
  elif command -v wget >/dev/null 2>&1; then FETCH=wget
  else return 1; fi
}

http() { # http <method> <url> <out> [bodyfile] -> prints the HTTP status. The key goes through a
         # private file (curl: a header file; wget: its own config file), never on a command line.
  local method="$1" url="$2" out="$3" body="${4:-}" priv="$TMP_DIR/h" code=""
  : >"$priv"; chmod 600 "$priv"
  if [ "$FETCH" = curl ]; then
    [ -n "$KEY" ] && printf 'Authorization: Bearer %s\n' "$KEY" >"$priv"
    if [ -n "$body" ]; then
      code=$(curl -sS -m 120 -X "$method" -H @"$priv" -H 'Content-Type: application/json' --data-binary @"$body" \
        -o "$out" -w '%{http_code}' "$url" </dev/null 2>>"$LOG")
    else
      code=$(curl -sS -m 60 -X "$method" -H @"$priv" -o "$out" -w '%{http_code}' "$url" </dev/null 2>>"$LOG")
    fi
  else
    [ -n "$KEY" ] && printf 'header = Authorization: Bearer %s\n' "$KEY" >"$priv"
    local heads="$TMP_DIR/wh"
    if [ -n "$body" ]; then
      wget --config="$priv" -S -T 120 -t 1 --header='Content-Type: application/json' --post-file="$body" \
        -O "$out" "$url" </dev/null 2>"$heads"
    else
      wget --config="$priv" -S -T 60 -t 1 -O "$out" "$url" </dev/null 2>"$heads"
    fi
    code=$(sed -n 's/^ *HTTP\/[0-9.]* \([0-9][0-9][0-9]\).*/\1/p' "$heads" | tail -1)
    rm -f "$heads"
  fi
  rm -f "$priv"
  printf '%s' "${code:-000}"
}

sha256_of() { if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi; }

download() { # download <url> <file> <sha256|empty>, with a progress bar that is cleared afterwards
  local status=0
  if [ "$FETCH" = curl ]; then
    curl -fL --retry 2 -m 1800 --progress-bar -o "$2" "$1" </dev/null || status=$?
  else
    wget -q --show-progress -t 3 -T 60 -O "$2" "$1" </dev/null || status=$?
  fi
  [ -t 2 ] && printf '\033[1A\r\033[2K' >&2
  [ "$status" = 0 ] || { log "download failed ($status): $1"; rm -f "$2"; return 1; }
  if [ -n "$3" ] && [ "$(sha256_of "$2")" != "$3" ]; then rm -f "$2"; fail "The download was damaged (checksum mismatch)."; return 1; fi
  return 0
}

tools_lookup() { # tools_lookup [quiet]: which arduino-cli file fits this computer, with its SHA-256
  [ -n "$TOOLS" ] && [ -s "$TOOLS" ] && return 0
  TOOLS="$TMP_DIR/tools.json"
  local status; status=$(http GET "$DAYAN_API/api/device-setup/tools?os=linux&arch=$ARCH" "$TOOLS")
  [ "$status" = "200" ] && return 0
  TOOLS=""; log "tools lookup failed: HTTP $status"
  [ -n "${1:-}" ] || fail "Dayan couldn't tell which files fit this computer (HTTP $status)."
  return 1
}

# ── This computer ──────────────────────────────────────────────────────────
me() { id -un 2>/dev/null || echo "${USER:-}"; }

serial_group() { # who owns a plugged-in board's port, else the usual group (uucp on Arch)
  local dev g
  for dev in /dev/ttyACM0 /dev/ttyUSB0; do [ -e "$dev" ] && { stat -c %G "$dev" 2>/dev/null; return; }; done
  for g in dialout uucp; do getent group "$g" >/dev/null 2>&1 && { echo "$g"; return; }; done
}

in_group() { id -nG "$(me)" 2>/dev/null | tr ' ' '\n' | grep -qx "$1"; }

# ── The app (installed by the member from its maker's page) ────────────────
app_name() { case "$1" in claude) echo "Claude Code" ;; codex) echo "Codex" ;; *) echo "OpenCode" ;; esac; }

install_page() {
  case "$1" in
    claude) echo "https://code.claude.com/docs/en/setup#install-claude-code" ;;
    codex) echo "https://learn.chatgpt.com/docs/codex/cli" ;;
    *) echo "https://opencode.ai/download" ;;
  esac
}

first_program() { # the first of the paths that is a program
  local p
  for p in "$@"; do [ -n "$p" ] && [ -x "$p" ] && [ ! -d "$p" ] && { printf '%s' "$p"; return 0; }; done
  return 1
}

find_app() { # APP_BIN: where the app is -- on the PATH, or where its own installer puts it
  APP_BIN=""; APP_KIND=""
  case "$APP" in
    claude) APP_BIN=$(first_program "$(command -v claude 2>/dev/null)" "$HOME/.local/bin/claude" \
              "$HOME/.claude/local/claude" /usr/bin/claude /usr/local/bin/claude) ;;
    codex) APP_BIN=$(first_program "$(command -v codex 2>/dev/null)" "$HOME/.local/bin/codex" \
             /usr/local/bin/codex /usr/bin/codex) ;;
    *) # the desktop app (its .deb / .rpm, or Dayan's earlier setup in her own folder), else the terminal app
       if APP_BIN=$(first_program "$(command -v ai.opencode.desktop 2>/dev/null)" /opt/OpenCode/ai.opencode.desktop \
            "$DATA_DIR/opencode-desktop/AppRun" "$DATA_DIR/OpenCode.AppImage"); then APP_KIND=desktop
       elif APP_BIN=$(first_program "$(command -v opencode 2>/dev/null)" "$HOME/.opencode/bin/opencode" \
            "$HOME/.local/bin/opencode"); then APP_KIND=cli; fi ;;
  esac
  [ -n "$APP_BIN" ]
}

sandbox_flags() { # an OpenCode set up in her own folder by an earlier Dayan setup has no sandbox helper
  APP_FLAGS=""
  case "$APP_BIN" in "$DATA_DIR"/*) ;; *) return 0 ;; esac
  if [ "$(cat /proc/sys/kernel/apparmor_restrict_unprivileged_userns 2>/dev/null)" = 1 ] \
     || [ "$(cat /proc/sys/kernel/unprivileged_userns_clone 2>/dev/null)" = 0 ]; then
    APP_FLAGS="--no-sandbox"
    log "user namespaces are restricted here: OpenCode (own folder) starts with --no-sandbox"
  fi
}

config_dir() { # where the app keeps its settings, and Dayan's key and skills with them
  case "$APP" in
    claude) echo "$DAYAN_HOME/.claude" ;;
    codex) echo "$DAYAN_HOME/.codex" ;;
    *) echo "${XDG_CONFIG_HOME:-$DAYAN_HOME/.config}/opencode" ;;
  esac
}

local_bin_on_path() { # ~/.local/bin already on the PATH a new Terminal gets?
  local rc
  for rc in "$HOME/.profile" "$HOME/.bash_profile" "$HOME/.bashrc" "$HOME/.zshrc"; do
    grep -qs 'dayan: arduino-cli\|dayan: ~/.local/bin' "$rc" && return 0
  done
  case ":$PATH:" in *":$HOME/.local/bin:"*) return 0 ;; esac
  return 1
}

put_local_bin_on_path() {
  local rc
  # ~/.bash_profile too: where it exists (Arch) bash reads it instead of
  # ~/.profile, and ~/.bashrc stops early outside an interactive terminal.
  for rc in "$HOME/.profile" "$HOME/.bash_profile" "$HOME/.bashrc" "$HOME/.zshrc"; do
    if [ -f "$rc" ] || [ "$rc" = "$HOME/.profile" ]; then
      grep -qs 'dayan: arduino-cli\|dayan: ~/.local/bin' "$rc" \
        || printf '\nexport PATH="$HOME/.local/bin:$PATH"  # dayan: ~/.local/bin\n' >>"$rc"
    fi
  done
}

# ── Steps ──────────────────────────────────────────────────────────────────
step() { line "·  $1" "$SOFT"; }
ok() { printf '\033[1A\r\033[2K'; line "✓  $1${2:+  $2}" "$GREEN"; log "done: $1 ${2:-}"; }
skip() { printf '\033[1A\r\033[2K'; line "–  $1${2:+  $2}" "$SOFT"; }

private_file() { # private_file <path>: write stdin to it, readable only by her
  local target="$1"
  (umask 077; cat >"$target.dayan-new") && chmod 600 "$target.dayan-new" && mv -f "$target.dayan-new" "$target"
}

save_key() { # OpenCode reads the key from a file of its own (Claude Code and Codex: their settings)
  step "Saving your key on this computer"
  mkdir -p "$CONFIG_DIR"
  printf '%s' "$KEY" | private_file "$CONFIG_DIR/dayan-key"
  chmod 600 "$CONFIG_DIR/dayan-key"
  ok "Saving your key on this computer" "readable only by you"
}

keep_copy() { [ -f "$1" ] && cp "$1" "$1.before-dayan-$(date +%Y%m%d-%H%M%S)"; }

remember_created() { # remember_created <app> <file>: whether Dayan made that settings file (the first run decides)
  state_has "created_$1" && return 0
  if [ -f "$2" ]; then state_add "created_$1" no; else state_add "created_$1" yes; fi
}

connect_opencode() {
  local cfg="$CONFIG_DIR/opencode.json" out="" note=""
  remember_created opencode "$cfg"
  if has_python; then
    out=$(python3 - "$cfg" "$DAYAN_API" "$DATA_DIR/previous-opencode.json" <<'PY' 2>>"$LOG"
import json, os, sys
path, api, before = sys.argv[1], sys.argv[2], sys.argv[3]
c = {}
if os.path.exists(path):
    try:
        c = json.loads(open(path, encoding="utf-8-sig").read())
    except Exception:
        print("__UNREADABLE__"); sys.exit(0)
if not isinstance(c, dict):
    print("__UNREADABLE__"); sys.exit(0)
if not os.path.exists(before):  # what Dayan replaces, kept once (a later run would find Dayan's own values)
    ours = lambda v: isinstance(v, str) and (v.startswith(("dayan", "dyn_")) or v.endswith("/api/llm"))
    with os.fdopen(os.open(before, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "w") as f:
        json.dump({n: None if ours(c.get(n)) else c.get(n) for n in ("model", "small_model", "default_agent")}, f)
c.setdefault("$schema", "https://opencode.ai/config.json")
limit = {"context": 1000000, "output": 32000}
c.setdefault("provider", {})["dayan"] = {"npm": "@ai-sdk/openai-compatible", "name": "Dayan",
    "options": {"baseURL": api + "/api/llm/v1", "apiKey": "{file:dayan-key}"},
    "models": {"dayan": {"name": "Dayan", "limit": limit}, "dayan-pro": {"name": "Dayan Pro", "limit": limit}}}
# OpenCode 2 reads this 1.x form too; its timeout then also bounds each tool call.
c.setdefault("mcp", {})["dayan"] = {"type": "remote", "url": api + "/api/mcp", "enabled": True, "oauth": False,
    "headers": {"Authorization": "Bearer {file:dayan-key}"}, "timeout": 60000}
c["model"] = "dayan/dayan"; c["small_model"] = "dayan/dayan"; c["default_agent"] = "dayan"
print(json.dumps(c, indent=2))
PY
)
  fi
  if [ -z "$out" ] || [ "$out" = "__UNREADABLE__" ]; then
    keep_copy "$cfg" && note="your old settings were kept as a copy"
    state_add whole_opencode yes  # written whole: a removal puts her copy back
    out=$(cat <<JSON
{
  "\$schema": "https://opencode.ai/config.json",
  "provider": {
    "dayan": {
      "npm": "@ai-sdk/openai-compatible",
      "name": "Dayan",
      "options": { "baseURL": "$DAYAN_API/api/llm/v1", "apiKey": "{file:dayan-key}" },
      "models": {
        "dayan": { "name": "Dayan", "limit": { "context": 1000000, "output": 32000 } },
        "dayan-pro": { "name": "Dayan Pro", "limit": { "context": 1000000, "output": 32000 } }
      }
    }
  },
  "mcp": {
    "dayan": { "type": "remote", "url": "$DAYAN_API/api/mcp", "enabled": true, "oauth": false,
               "headers": { "Authorization": "Bearer {file:dayan-key}" }, "timeout": 60000 }
  },
  "model": "dayan/dayan",
  "small_model": "dayan/dayan",
  "default_agent": "dayan"
}
JSON
)
  fi
  printf '%s\n' "$out" | private_file "$cfg"
  CONNECT_NOTE="$note"
}

connect_claude() { # ~/.claude/settings.json: Dayan's AI and key; Dayan's tools through Claude Code's own command
  local cfg="$CONFIG_DIR/settings.json" out="" note=""
  remember_created claude "$cfg"
  if has_python; then
    out=$(DAYAN_SETUP_KEY="$KEY" python3 - "$cfg" "$DAYAN_API" "$DATA_DIR/previous-claude.json" <<'PY' 2>>"$LOG"
import json, os, sys
path, api, key, before = sys.argv[1], sys.argv[2], os.environ["DAYAN_SETUP_KEY"], sys.argv[3]
c = {}
if os.path.exists(path):
    try:
        c = json.loads(open(path, encoding="utf-8-sig").read())
    except Exception:
        print("__UNREADABLE__"); sys.exit(0)
if not isinstance(c, dict):
    print("__UNREADABLE__"); sys.exit(0)
env = c.get("env") if isinstance(c.get("env"), dict) else {}
if not os.path.exists(before):  # what Dayan replaces, kept once and readable only by her
    ours = lambda v: isinstance(v, str) and (v.startswith(("dayan", "dyn_")) or v.endswith("/api/llm"))
    names = ("ANTHROPIC_BASE_URL", "ANTHROPIC_AUTH_TOKEN", "DAYAN_API_KEY", "ANTHROPIC_MODEL",
             "ANTHROPIC_DEFAULT_OPUS_MODEL", "ANTHROPIC_DEFAULT_SONNET_MODEL", "ANTHROPIC_DEFAULT_HAIKU_MODEL",
             "CLAUDE_CODE_SUBAGENT_MODEL", "DISABLE_TELEMETRY", "DISABLE_ERROR_REPORTING")
    old = {n: None if ours(env.get(n)) else env.get(n) for n in names}
    old["agent"] = None if ours(c.get("agent")) else c.get("agent")
    with os.fdopen(os.open(before, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "w") as f:
        json.dump(old, f)
env.update({"ANTHROPIC_BASE_URL": api + "/api/llm", "ANTHROPIC_AUTH_TOKEN": key, "DAYAN_API_KEY": key,
            "ANTHROPIC_MODEL": "dayan[1m]", "ANTHROPIC_DEFAULT_OPUS_MODEL": "dayan-pro[1m]",
            "ANTHROPIC_DEFAULT_SONNET_MODEL": "dayan[1m]", "ANTHROPIC_DEFAULT_HAIKU_MODEL": "dayan",
            "CLAUDE_CODE_SUBAGENT_MODEL": "dayan", "DISABLE_TELEMETRY": "1", "DISABLE_ERROR_REPORTING": "1"})
c["env"] = env
c["agent"] = "dayan"
print(json.dumps(c, indent=2))
PY
)
  fi
  if [ -z "$out" ] || [ "$out" = "__UNREADABLE__" ]; then
    keep_copy "$cfg" && note="your old settings were kept as a copy"
    state_add whole_claude yes  # written whole: a removal puts her copy back
    out=$(cat <<JSON
{
  "env": {
    "ANTHROPIC_BASE_URL": "$DAYAN_API/api/llm",
    "ANTHROPIC_AUTH_TOKEN": "$KEY",
    "DAYAN_API_KEY": "$KEY",
    "ANTHROPIC_MODEL": "dayan[1m]",
    "ANTHROPIC_DEFAULT_OPUS_MODEL": "dayan-pro[1m]",
    "ANTHROPIC_DEFAULT_SONNET_MODEL": "dayan[1m]",
    "ANTHROPIC_DEFAULT_HAIKU_MODEL": "dayan",
    "CLAUDE_CODE_SUBAGENT_MODEL": "dayan",
    "DISABLE_TELEMETRY": "1",
    "DISABLE_ERROR_REPORTING": "1"
  },
  "agent": "dayan"
}
JSON
)
  fi
  printf '%s\n' "$out" | private_file "$cfg"
  # Dayan's tools: Claude Code writes its own ~/.claude.json. The header names
  # the key (from the settings above), never the key itself.
  "$APP_BIN" mcp remove dayan --scope user </dev/null >>"$LOG" 2>&1
  # shellcheck disable=SC2016  # ${DAYAN_API_KEY} is Claude Code's to expand, not this script's
  "$APP_BIN" mcp add --transport http --scope user dayan "$DAYAN_API/api/mcp" \
    --header 'Authorization: Bearer ${DAYAN_API_KEY}' </dev/null >>"$LOG" 2>&1 \
    || { fail "Claude Code didn't take Dayan's tools (see $LOG)."; return 1; }
  CONNECT_NOTE="$note"
}

connect_codex() { # ~/.codex/config.toml: Dayan's model and tools, the member's own lines kept
  local cfg="$CONFIG_DIR/config.toml" root="$TMP_DIR/root.toml" tables="$TMP_DIR/tables.toml"
  : >"$root"; : >"$tables"
  remember_created codex "$cfg"
  if [ ! -f "$DATA_DIR/previous-codex.toml" ]; then
    # Her top lines Dayan's three replace, kept once: a removal puts them back.
    { [ -f "$cfg" ] && awk '/^[[:space:]]*\[/ { exit }
        /^[[:space:]]*(model|model_provider|model_context_window)[[:space:]]*=/ && $0 !~ /"dayan"|=[[:space:]]*1000000[[:space:]]*$/' \
        "$cfg"; } | private_file "$DATA_DIR/previous-codex.toml"
  fi
  if [ -f "$cfg" ]; then
    # Keep every line of hers except Dayan's own: the three top keys and Dayan's two tables.
    awk -v ROOT="$root" -v TABLES="$tables" '
      /^[[:space:]]*\[/ { in_tables = 1
        skip = ($0 ~ /^[[:space:]]*\[[[:space:]]*(model_providers\.dayan|mcp_servers\.dayan)([.][^]]*)?[[:space:]]*\]/)
        if (!skip) print > TABLES; next }
      !in_tables { if ($0 !~ /^[[:space:]]*(model|model_provider|model_context_window)[[:space:]]*=/ && $0 !~ /^# Dayan Agent/) print > ROOT; next }
      !skip { print > TABLES }' "$cfg" || { fail "Your Codex settings could not be read."; return 1; }
    keep_copy "$cfg" >/dev/null
  fi
  {
    echo "# Dayan Agent: Dayan's model and tools (Run Dayan Agent on my device)"
    echo 'model = "dayan"'
    echo 'model_provider = "dayan"'
    echo 'model_context_window = 1000000'
    cat "$root"
    cat "$tables"
    echo
    echo '[model_providers.dayan]'
    echo 'name = "Dayan"'
    echo "base_url = \"$DAYAN_API/api/llm/v1\""
    echo 'wire_api = "responses"'
    printf 'experimental_bearer_token = "%s"\n' "$KEY"
    echo
    echo '[mcp_servers.dayan]'
    echo "url = \"$DAYAN_API/api/mcp\""
    printf 'http_headers = { "Authorization" = "Bearer %s" }\n' "$KEY"
    echo 'startup_timeout_sec = 20'
    echo 'tool_timeout_sec = 60'
  } | private_file "$cfg"
  CONNECT_NOTE=""
}

connect_app() {
  local title="Connecting $APP_NAME to Dayan"
  step "$title"
  CONNECT_NOTE=""
  mkdir -p "$CONFIG_DIR"
  case "$APP" in
    claude) connect_claude || return 1 ;;
    codex) connect_codex || return 1 ;;
    *) connect_opencode || return 1 ;;
  esac
  ok "$title" "$CONNECT_NOTE"
}

pack_root() { # where the app reads Dayan's skills (and agents) from
  case "$APP" in
    claude) echo "$DAYAN_HOME/.claude" ;;
    codex) echo "$DAYAN_HOME/.agents" ;;  # Codex reads its user skills from ~/.agents/skills
    *) echo "$CONFIG_DIR" ;;
  esac
}

add_skills() {
  step "Adding Dayan's Tutor and Maker skills"
  local pack="$TMP_DIR/pack.tar.gz" status root
  status=$(http GET "$DAYAN_API/api/device-setup/pack.tar.gz?app=$APP" "$pack")
  [ "$status" = "200" ] || { fail "The skills could not be downloaded (HTTP $status)."; return 1; }
  root=$(pack_root); mkdir -p "$root"
  tar -xzf "$pack" -C "$root" >>"$LOG" 2>&1 || { fail "The skills could not be unpacked."; return 1; }
  local file
  while IFS= read -r file; do state_add "pack_$APP" "$root/$file"; done <<EOF
$(tar -tzf "$pack" | grep -v '/$')
EOF
  ok "Adding Dayan's Tutor and Maker skills" "$(tar -tzf "$pack" | grep -vc '/$') files"
}

make_work_folder() {
  step "Making your work folder"
  mkdir -p "$WORK_DIR"
  [ "$PATH_LINE" = yes ] && put_local_bin_on_path
  # shellcheck disable=SC2088  # the words on her screen, not a path
  ok "Making your work folder" "~/Dayan"
}

install_arduino() {
  step "Installing the Arduino tools"
  if [ -n "$NO_INSTALL" ]; then skip "Installing the Arduino tools" "skipped (test run)"; return 0; fi
  local bin="$HOME/.local/bin" cli
  mkdir -p "$bin"; cli="$bin/arduino-cli"
  if [ ! -x "$cli" ] && ! command -v arduino-cli >/dev/null 2>&1; then
    tools_lookup || return 1
    local url sha name
    url=$(tool_field arduino_cli url); sha=$(tool_field arduino_cli sha256); name=$(tool_field arduino_cli name)
    [ -n "$url" ] || { fail "No Arduino tools download for this computer."; return 1; }
    download "$url" "$TMP_DIR/$name" "$sha" || { fail "The Arduino tools download failed."; return 1; }
    tar -xzf "$TMP_DIR/$name" -C "$bin" arduino-cli >>"$LOG" 2>&1 || { fail "The Arduino tools could not be unpacked."; return 1; }
    chmod +x "$cli"
    state_add arduino_cli "$cli"  # Dayan's to remove; one she had already is hers
  fi
  [ -x "$cli" ] || cli="$(command -v arduino-cli)"
  [ "$PATH_LINE" = yes ] && put_local_bin_on_path
  line "   getting the Arduino board files (a few minutes)…" "$SOFT"
  "$cli" core update-index >>"$LOG" 2>&1 </dev/null && "$cli" core install arduino:avr >>"$LOG" 2>&1 </dev/null \
    || { printf '\033[1A\r\033[2K'; fail "The Arduino board files could not be installed (see $LOG)."; return 1; }
  printf '\033[1A\r\033[2K'
  local note="ready"
  if [ -n "$SERIAL_GROUP" ] && ! in_group "$SERIAL_GROUP"; then
    # shellcheck disable=SC2024  # her own log, written as her
    if [ "$SERIAL_OK" = yes ] && sudo -n usermod -aG "$SERIAL_GROUP" "$(me)" >>"$LOG" 2>&1; then
      note="ready -- log out and back in once, then Dayan can upload to your board"
    else
      note="ready -- to upload to a board later, run: sudo usermod -aG $SERIAL_GROUP \$USER (then log out and in)"
    fi
  fi
  ok "Installing the Arduino tools" "$note"
}

say_hello() {
  step "Saying hello to Dayan"
  local body="$TMP_DIR/hello.json" out="$TMP_DIR/hello.out" status who
  who=""; [ -n "$FIRST_NAME" ] && who=" (I'm $FIRST_NAME)"
  printf '{"model":"dayan","stream":false,"max_tokens":800,"messages":[{"role":"user","content":"You have just been set up on my computer%s. Greet me in one short, friendly sentence."}]}' "$who" >"$body"
  status=$(http POST "$DAYAN_API/api/llm/v1/chat/completions" "$out" "$body")
  [ "$status" = "200" ] || { fail "Dayan didn't answer (HTTP $status)."; return 1; }
  if has_python; then
    HELLO=$(python3 -c "import json,sys; o=json.load(open(sys.argv[1])); print(((o.get('choices') or [{}])[0].get('message') or {}).get('content','').strip())" "$out" </dev/null 2>/dev/null)
  else
    HELLO=$(tr -d '\n' <"$out" | sed -n 's/.*"content"[[:space:]]*:[[:space:]]*"\(\([^"\\]\|\\.\)*\)".*/\1/p' | sed 's/\\n/ /g; s/\\"/"/g')
  fi
  ok "Saying hello to Dayan" "Dayan answered"
}

# ── Screens ────────────────────────────────────────────────────────────────
item() { printf '%s  %s◆%s  %s%-21s%s%s%s%s\n' "$(indent)" "$GOLD" "$RESET" "$WHITE" "$1" "$RESET" "$SOFT" "$2" "$RESET"; }

missing_app() { # the app isn't here: its maker's page, and nothing changed
  header "Install $APP_NAME first"
  para "Dayan Agent works inside $APP_NAME, and it isn't on this computer yet. Install it from its maker's own page -- the button in Dayan's chat opens it and says which part of the page to use:" "$WHITE"; echo
  line "$INSTALL_PAGE" "$GOLD"; echo
  para "Then run this same line again. Nothing was changed." "$SOFT"; echo
  log "$APP_NAME not found: nothing changed"
  exit 3
}

welcome() {
  header "Set up Dayan Agent on this computer"
  local hi="Hi!"; [ -n "$FIRST_NAME" ] && hi="Hi $FIRST_NAME!"
  para "$hi $APP_NAME is on this computer. In a few minutes it will be ready for you to work with Dayan Agent: your tutor for what you study, and your partner for building websites, games and Arduino projects." "$WHITE"
  echo; line "Here's what I'll set up -- nothing else on this computer changes:" "$GOLD"; echo
  case "$APP" in
    claude) item "Your Dayan key" "in Claude Code's settings (~/.claude), only for you"
            item "Claude Code settings" "Dayan's AI and tools; your own settings are kept"
            item "Dayan's skills" "Tutor and Maker, with Maker's helpers" ;;
    codex) item "Your Dayan key" "in Codex's settings (~/.codex), only for you"
           item "Codex settings" "Dayan's AI and tools; your own lines are kept"
           item "Dayan's skills" "Tutor and Maker (~/.agents/skills)" ;;
    *) item "Your Dayan key" "kept in ~/.config/opencode, readable only by you"
       item "OpenCode settings" "connected to Dayan; your own settings are kept"
       item "Dayan's skills" "Tutor for learning, Maker for projects" ;;
  esac
  # shellcheck disable=SC2088  # the words on her screen, not paths
  [ "$APP" != opencode ] && item "A work folder" "~/Dayan, where $APP_NAME starts"
  # shellcheck disable=SC2088
  [ "$PATH_LINE" = yes ] && item "Your PATH" "~/.local/bin, so a new Terminal finds your tools"
  [ "$WANT_ARDUINO" = "true" ] && item "Arduino tools" "arduino-cli in ~/.local/bin + board files (~400 MB)"
  # shellcheck disable=SC2088
  item "A way to remove it" "~/.local/share/dayan/uninstall.sh undoes all of this"
  echo; para "If a step needs your computer's password, I'll ask you first -- and you can say no." "$SOFT"
  # What it sends, and to whom (SignPath's rule: said before the setup starts).
  local arduino_hosts=""
  [ "$WANT_ARDUINO" = "true" ] && arduino_hosts=" and, for the Arduino tools, Arduino's downloads (GitHub, arduino.cc)"
  para "It talks only to Dayan ($(host_of "$DAYAN_API"))$arduino_hosts: to read your choices, check your key and say hello. It sends nothing else about this computer." "$SOFT"
  echo; line "Press Enter to agree and start   •   Esc to cancel" "$GOLD"
  wait_enter_or_esc
}

ask_key() {
  local problem="" status who
  for _ in 1 2 3 4 5 6 7 8; do
    header "Your Dayan key"
    para "Your key connects this computer to your Dayan account. To get it:" "$WHITE"; echo
    line "1.  Open Dayan, then Settings  ›  Device keys" "$WHITE"
    line "2.  Click \"Generate a new key\", then Copy" "$WHITE"
    line "3.  Paste it below and press Enter" "$WHITE"; echo
    [ -n "$problem" ] && { para "✗  $problem" "$RED"; echo; }
    if [ -n "$KEY_ENV" ]; then
      KEY_READ="$(printenv "$KEY_ENV")"
    else
      printf '%s%sKey: %s' "$(indent)" "$GOLD" "$RESET"
      read_masked || return 1
      echo
    fi
    case "$KEY_READ" in
      dyn_*) ;;
      "") problem="Paste your key first."; [ -n "$KEY_ENV" ] && return 1; continue ;;
      *) problem="That doesn't look like a Dayan key. It starts with dyn_ -- copy it again from Settings."; [ -n "$KEY_ENV" ] && return 1; continue ;;
    esac
    KEY="$KEY_READ"; KEY_READ=""
    line "Checking your key…" "$SOFT"
    who="$TMP_DIR/who.json"
    status=$(http GET "$DAYAN_API/api/llm/whoami" "$who")
    case "$status" in
      200) local first; first=$(json_get "$who" first_name); [ -n "$first" ] && FIRST_NAME="$first"; log "key accepted"; return 0 ;;
      401) problem="Dayan doesn't recognise this key -- it may have been revoked. Generate a new one and paste it." ;;
      403) problem="Keys are switched off for your account. Ask your Dayan administrator." ;;
      404) problem="Keys aren't available on this Dayan right now." ;;
      *) problem="Dayan can't be reached right now. Check your internet connection and try again." ;;
    esac
    KEY=""; log "key check failed: HTTP $status"
    [ -n "$KEY_ENV" ] && return 1
  done
  return 1
}

permissions() { # the one step that needs the password, asked before anything changes
  [ "$WANT_ARDUINO" = "true" ] && [ -n "$SERIAL_GROUP" ] && ! in_group "$SERIAL_GROUP" || return 0
  command -v sudo >/dev/null 2>&1 || { log "no sudo here: the password step is skipped"; return 0; }

  header "Your permission"
  para "Everything else stays inside your own account. This needs your computer's password (the one you sign in with) and an account that may change settings. Say no and I'll do without it:" "$WHITE"; echo
  local want_serial=""
  line "The USB port for your Arduino board" "$GOLD"
  para "    Lets Dayan upload to your board (adds you to the '$SERIAL_GROUP' group; log out and back in once). Without it: I'll show you the command for later." "$SOFT"
  line "    Enter = yes   •   N = not now" "$WHITE"
  ask_yes_no && want_serial=yes; echo
  [ -z "$want_serial" ] && { log "the password step: declined"; return 0; }
  if [ -n "$AUTO_YES" ]; then
    sudo -n true 2>/dev/null || { log "sudo needs a password in a test run: declined"; return 0; }
  else
    line "Your password, please (it doesn't show as you type):" "$WHITE"
    sudo -v </dev/tty || {
      para "That didn't work -- the password was wrong, or this account can't change settings. I'll do without it." "$SOFT"
      log "sudo refused"; sleep 3; return 0; }
  fi
  SERIAL_OK="$want_serial"
  keep_sudo
  log "the password step: serial port=${SERIAL_OK:-no}"
}

keep_sudo() { # the password given once lasts the whole setup (the board files outlive sudo's 5 minutes),
              # and is forgotten when the setup ends
  SUDO_USED=1
  ( while sleep 45; do kill -0 "$$" 2>/dev/null || exit 0; sudo -n -v 2>/dev/null || exit 0; done ) >/dev/null 2>&1 &
  SUDO_KEEPER=$!
}

finish_up() { # on every exit: the run's temporary files go, and so does the password sudo remembered
  [ -n "$SUDO_KEEPER" ] && kill "$SUDO_KEEPER" 2>/dev/null
  [ -n "$SUDO_USED" ] && sudo -k 2>/dev/null
  [ -n "$TMP_DIR" ] && rm -rf "$TMP_DIR"
  SUDO_KEEPER=""; SUDO_USED=""; TMP_DIR=""
}

run_step() { # run_step <function>: retry on failure
  while :; do
    "$1" && return 0
    [ -n "$AUTO_YES" ] && return 1
    line "Press Enter to try again   •   Esc to stop" "$GOLD"
    wait_enter_or_esc || return 1
  done
}

cancelled() { header "Nothing was changed"; line "You can run the same line again any time." "$SOFT"; exit 2; }

load_choices() {
  local choices="$TMP_DIR/choices.json" status
  status=$(http GET "$DAYAN_API/api/device-setup/$DAYAN_CODE" "$choices")
  if [ "$status" = "200" ]; then
    FIRST_NAME=$(json_get "$choices" first_name); WANT_ARDUINO=$(json_get "$choices" arduino)
    [ -n "$APP" ] || APP=$(json_get "$choices" app)
  else
    log "choices not found (HTTP $status)"
    echo "Dayan can't be reached right now (HTTP $status). Check your internet connection and run the line again."
    exit 1
  fi
  case "$APP" in claude|codex|opencode) ;; *) APP=opencode ;; esac
}

finish() {
  header "All set!"
  line "✓  Dayan Agent is ready in $APP_NAME." "$GREEN"; echo
  [ -n "$HELLO" ] && { para "Dayan says:  $HELLO" "$WHITE"; echo; }
  # shellcheck disable=SC2088
  para "To remove Dayan Agent later:  bash ~/.local/share/dayan/uninstall.sh" "$SOFT"; echo
  line "How to start:" "$GOLD"
  if [ "$APP_KIND" = desktop ]; then
    line "1.  Open OpenCode (it opens when you press Enter; it's in your apps menu too)" "$WHITE"
    line "2.  Choose a folder for your work" "$WHITE"
    line "3.  Tell Dayan what you want to learn or build" "$WHITE"; echo
    para "Tip: press Tab in OpenCode to switch between Dayan (your tutor) and Dayan Maker (projects)." "$SOFT"; echo
    line "Press Enter to open OpenCode   •   Esc to close" "$GOLD"
    if [ -z "$AUTO_YES" ] && wait_enter_or_esc; then
      sandbox_flags
      # OpenCode inherits this PATH, and the new ~/.local/bin (arduino-cli) is
      # on it only from her next sign-in: put it on for this first session.
      # APP_FLAGS is empty or one word:
      # shellcheck disable=SC2086
      PATH="$HOME/.local/bin:$PATH" nohup "$APP_BIN" $APP_FLAGS >/dev/null 2>&1 &
    fi
    log "setup finished"
    return 0
  fi
  local command; command=$(basename "$APP_BIN")
  line "1.  Open Terminal and go to your work folder:  cd ~/Dayan" "$WHITE"
  line "2.  Type  $command  and press Enter" "$WHITE"
  line "3.  Tell Dayan what you want to learn or build" "$WHITE"; echo
  [ "$APP" = claude ] && { para "The first time, $APP_NAME asks whether you trust the folder: choose Yes. Type /agents to see Dayan's helpers." "$SOFT"; echo; }
  [ "$APP" = codex ] && { para "The first time, $APP_NAME asks whether to trust the folder: choose Trust and continue." "$SOFT"; echo; }
  line "Press Enter to start $APP_NAME here now   •   Esc to close" "$GOLD"
  log "setup finished"
  if [ -z "$AUTO_YES" ] && wait_enter_or_esc; then
    local app="$APP_BIN"
    finish_up
    cd "$WORK_DIR" || return 0
    # The new ~/.local/bin is on the PATH only from the next Terminal: on it now.
    PATH="$HOME/.local/bin:$PATH" exec "$app" </dev/tty >/dev/tty 2>&1
  fi
}

# ── Removing Dayan Agent (--uninstall, or ~/.local/share/dayan/uninstall.sh) ──
has_dayan() { # has_dayan <app>: Dayan's settings are in that app
  local dir; APP="$1"; dir=$(config_dir)
  case "$1" in
    claude) grep -qs '"DAYAN_API_KEY"\|"agent": *"dayan"' "$dir/settings.json" ;;
    codex) grep -qsE '^[[:space:]]*\[[[:space:]]*(model_providers|mcp_servers)\.dayan[[:space:]]*\]' "$dir/config.toml" ;;
    *) [ -f "$dir/dayan-key" ] || grep -qs '"dayan"' "$dir/opencode.json" ;;
  esac
}

remove_pack() { # remove_pack <app>: the skills the setup put there, and the folders they leave empty
  local root file dir files n=0
  root=$(pack_root)
  if state_has "pack_$1"; then files=$(state_get "pack_$1")
  else files=$(for file in $KNOWN_PACK; do printf '%s/%s\n' "$root" "$file"; done); fi
  while IFS= read -r file; do
    case "$file" in ""|*..*) continue ;; esac
    [ -f "$file" ] && rm -f "$file" && n=$((n + 1))
    dir=$(dirname "$file")
    while [ "${#dir}" -gt "${#root}" ] && rmdir "$dir" 2>/dev/null; do dir=$(dirname "$dir"); done
  done <<EOF
$files
EOF
  PACK_REMOVED=$n
}

restore_whole() { # restore_whole <app> <file>, without python3: Dayan wrote the file whole, so her copy comes back
  local copy first=""
  if [ "$(state_get "whole_$1")" != yes ]; then
    REMOVE_NOTE="python3 is needed to take Dayan's parts out of $2 -- remove its \"dayan\" entries by hand"
    return 0
  fi
  for copy in "$2".before-dayan-*; do [ -f "$copy" ] && { first="$copy"; break; }; done  # the oldest: hers
  if [ -n "$first" ]; then mv -f "$first" "$2"; else rm -f "$2"; fi
}

drop_key_copies() { # drop_key_copies <file>: copies an earlier run kept beside it that hold a Dayan key
  local copy
  for copy in "$1".before-dayan-*; do [ -f "$copy" ] && grep -qs 'dyn_' "$copy" && rm -f "$copy"; done
  return 0
}

remove_from_opencode() {
  local cfg="$CONFIG_DIR/opencode.json" result=""
  rm -f "$CONFIG_DIR/dayan-key"
  [ -f "$cfg" ] || return 0
  if ! has_python; then restore_whole opencode "$cfg"; drop_key_copies "$cfg"; return 0; fi
  result=$(python3 - "$cfg" "$DATA_DIR/previous-opencode.json" "$(state_get created_opencode)" <<'PY' 2>>"$LOG"
import json, os, sys
path, before, made = sys.argv[1], sys.argv[2], sys.argv[3] == "yes"
try:
    c = json.loads(open(path, encoding="utf-8-sig").read())
    assert isinstance(c, dict)
except Exception:
    print("unreadable"); sys.exit(0)
old = json.load(open(before)) if os.path.exists(before) else {}
for section in ("provider", "mcp"):
    if isinstance(c.get(section), dict):
        c[section].pop("dayan", None)
        if not c[section]:
            c.pop(section)
for name in ("model", "small_model", "default_agent"):
    value = c.get(name)
    if isinstance(value, str) and value.startswith("dayan"):
        if old.get(name) is not None:
            c[name] = old[name]
        else:
            c.pop(name)
if made and set(c) <= {"$schema"}:
    os.remove(path)
else:
    with open(path, "w", encoding="utf-8") as f:
        f.write(json.dumps(c, indent=2) + "\n")
print("ok")
PY
)
  [ "$result" = ok ] || REMOVE_NOTE="opencode.json could not be read -- take out its \"dayan\" parts yourself"
  drop_key_copies "$cfg"
}

remove_from_claude() {
  local cfg="$CONFIG_DIR/settings.json" result=""
  if [ -f "$cfg" ] && has_python; then
    result=$(python3 - "$cfg" "$DATA_DIR/previous-claude.json" "$(state_get created_claude)" <<'PY' 2>>"$LOG"
import json, os, sys
path, before, made = sys.argv[1], sys.argv[2], sys.argv[3] == "yes"
try:
    c = json.loads(open(path, encoding="utf-8-sig").read())
    assert isinstance(c, dict)
except Exception:
    print("unreadable"); sys.exit(0)
old = json.load(open(before)) if os.path.exists(before) else {}
def put_back(settings, name):  # Dayan's value goes, hers from before comes back; one she changed since stays
    value = settings.get(name)
    ours = value == "1" if name.startswith("DISABLE_") else (
        isinstance(value, str) and (value.startswith(("dayan", "dyn_")) or value.endswith("/api/llm")))
    if not ours:
        return
    if old.get(name) is not None:
        settings[name] = old[name]
    else:
        settings.pop(name)
env = c.get("env")
if isinstance(env, dict):
    for name in ("ANTHROPIC_BASE_URL", "ANTHROPIC_AUTH_TOKEN", "DAYAN_API_KEY", "ANTHROPIC_MODEL",
                 "ANTHROPIC_DEFAULT_OPUS_MODEL", "ANTHROPIC_DEFAULT_SONNET_MODEL", "ANTHROPIC_DEFAULT_HAIKU_MODEL",
                 "CLAUDE_CODE_SUBAGENT_MODEL", "DISABLE_TELEMETRY", "DISABLE_ERROR_REPORTING"):
        put_back(env, name)
    if not env:
        c.pop("env")
put_back(c, "agent")
if made and not c:
    os.remove(path)
else:
    with open(path, "w", encoding="utf-8") as f:
        f.write(json.dumps(c, indent=2) + "\n")
print("ok")
PY
)
    [ "$result" = ok ] || REMOVE_NOTE="settings.json could not be read -- take out Dayan's lines yourself"
  elif [ -f "$cfg" ]; then
    restore_whole claude "$cfg"
  fi
  drop_key_copies "$cfg"
  # Dayan's tools, through Claude Code's own command while Claude Code is here.
  if find_app; then "$APP_BIN" mcp remove dayan --scope user </dev/null >>"$LOG" 2>&1; fi
  return 0
}

remove_from_codex() { # her lines stay; Dayan's comment, three top keys and two tables go, her old top lines come back
  local cfg="$CONFIG_DIR/config.toml" out="$TMP_DIR/codex.toml"
  [ -f "$cfg" ] || return 0
  {
    cat "$DATA_DIR/previous-codex.toml" 2>/dev/null
    awk '
      /^[[:space:]]*\[/ { in_tables = 1
        skip = ($0 ~ /^[[:space:]]*\[[[:space:]]*(model_providers\.dayan|mcp_servers\.dayan)([.][^]]*)?[[:space:]]*\]/)
        if (!skip) print; next }
      !in_tables { if ($0 ~ /^# Dayan Agent/ ||
                       $0 ~ /^[[:space:]]*(model|model_provider)[[:space:]]*=[[:space:]]*"dayan"[[:space:]]*$/ ||
                       $0 ~ /^[[:space:]]*model_context_window[[:space:]]*=[[:space:]]*1000000[[:space:]]*$/) next
                   print; next }
      !skip { print }' "$cfg"
  } | awk 'NF { if (gap && seen) print ""; print; gap = 0; seen = 1; next } { gap = 1 }' >"$out" \
    || { fail "Your Codex settings could not be read."; return 1; }
  if [ ! -s "$out" ] && [ "$(state_get created_codex)" = yes ]; then rm -f "$cfg"; else private_file "$cfg" <"$out"; fi
  drop_key_copies "$cfg"
}

remove_arduino() { # only the arduino-cli Dayan downloaded: one she had already is hers
  local cli
  while IFS= read -r cli; do [ -n "$cli" ] && rm -f "$cli"; done <<EOF
$(state_get arduino_cli)
EOF
  return 0
}

uninstall_main() {
  local apps="" app kept="" entry
  [ -n "$TMP_DIR" ] || { TMP_DIR=$(mktemp -d 2>/dev/null || mktemp -d -t dayan); trap finish_up EXIT; }
  for app in opencode claude codex; do
    if has_dayan "$app" || state_has "pack_$app" || state_has "created_$app"; then apps="$apps $app"; fi
  done
  if [ -z "$apps" ] && [ ! -d "$DATA_DIR" ]; then
    header "Remove Dayan Agent"
    para "Dayan Agent isn't set up on this computer, so there is nothing to remove." "$WHITE"; echo
    return 0
  fi
  header "Remove Dayan Agent from this computer"
  line "Here's what I'll remove -- nothing else on this computer changes:" "$GOLD"; echo
  for app in $apps; do
    item "Dayan in $(app_name "$app")" "its settings, your key and the skills; yours stay"
    kept="$kept$(app_name "$app"), "
  done
  state_has arduino_cli && item "Arduino tools" "the arduino-cli Dayan installed"
  # shellcheck disable=SC2088
  item "Dayan's own files" "~/.local/share/dayan"
  echo; para "Kept: ${kept}your work folder (~/Dayan), the Arduino board files, and the line that puts ~/.local/bin on your PATH (your apps use it)." "$SOFT"
  echo; line "Press Enter to remove   •   Esc to cancel" "$GOLD"
  wait_enter_or_esc || { header "Nothing was changed"; return 2; }

  header "Removing Dayan Agent"
  for app in $apps; do
    APP="$app"; APP_NAME=$(app_name "$app"); CONFIG_DIR=$(config_dir); REMOVE_NOTE=""
    step "Removing Dayan from $APP_NAME"
    "remove_from_$app" || return 1
    remove_pack "$app"
    ok "Removing Dayan from $APP_NAME" "${REMOVE_NOTE:-$PACK_REMOVED skill files}"
  done
  if state_has arduino_cli; then
    step "Removing the Arduino tools Dayan installed"
    remove_arduino
    ok "Removing the Arduino tools Dayan installed" "the board files stay: the Arduino IDE uses them too"
  fi
  step "Removing Dayan Agent's own files"
  for entry in "$DATA_DIR"/* "$DATA_DIR"/.[!.]*; do
    [ -e "$entry" ] || continue
    case "${entry##*/}" in opencode-desktop|OpenCode.AppImage) continue ;; esac  # an app an earlier setup installed stays
    rm -rf "$entry"
  done
  rmdir "$DATA_DIR" 2>/dev/null
  ok "Removing Dayan Agent's own files"
  echo; line "✓  Dayan Agent is no longer set up on this computer." "$GREEN"; echo
  para "Your Dayan key keeps working until you revoke it: in Dayan, Settings › Device keys." "$WHITE"; echo
  return 0
}

save_uninstaller() { # the way back, written before the first change: bash ~/.local/share/dayan/uninstall.sh
  # shellcheck disable=SC2016  # the $ lines are the uninstaller's own, expanded when it runs
  {
    printf '#!/usr/bin/env bash\n'
    printf '# Removes Dayan Agent from this computer -- written by Dayan Setup. Run: bash ~/.local/share/dayan/uninstall.sh\n'
    printf 'DATA_DIR="$HOME/.local/share/dayan"; LOG="$DATA_DIR/setup.log"; STATE="$DATA_DIR/installed"\n'
    printf 'DAYAN_HOME="${DAYAN_HOME:-$HOME}"; AUTO_YES="${DAYAN_YES:-}"; TMP_DIR=""; SUDO_KEEPER=""; SUDO_USED=""\n'
    # Written as they are (declare -p escapes the logo half-way where no locale is set).
    printf "LOGO='%s'\n" "$LOGO"
    printf 'KNOWN_PACK="%s"\n' "$KNOWN_PACK"
    declare -f colors log state_get state_has cols indent line center para header has_tty readkey wait_enter_or_esc \
      fail step ok item private_file has_python app_name first_program find_app config_dir pack_root finish_up \
      has_dayan remove_pack restore_whole drop_key_copies remove_from_opencode remove_from_claude remove_from_codex \
      remove_arduino uninstall_main
    printf 'colors\nuninstall_main "$@"\n'
  } | private_file "$DATA_DIR/uninstall.sh"
  chmod 700 "$DATA_DIR/uninstall.sh"
}

main() {
  # Windows and Linux only (owner, 2026-09-26): a Mac is not set up.
  case "$(uname -s)" in
    Linux) ;;
    Darwin) echo "Dayan Agent runs on Windows and Linux -- a Mac isn't supported."; exit 1 ;;
    *) echo "This setup is for Linux. On Windows, download Dayan Setup from Dayan."; exit 1 ;;
  esac
  if [ "$(id -u)" = 0 ] && [ -z "${DAYAN_ALLOW_ROOT:-}" ]; then
    echo "Run this as yourself -- without sudo -- so Dayan Agent is set up for your account."; exit 1
  fi
  [ "${1:-}" = "--uninstall" ] && UNINSTALL=yes
  if [ -n "$UNINSTALL" ]; then # the removal needs no network and no key
    has_tty || [ -n "$AUTO_YES" ] || { echo "Run this in a Terminal window."; exit 1; }
    uninstall_main
    exit $?
  fi
  mkdir -p "$DATA_DIR"; TMP_DIR=$(mktemp -d 2>/dev/null || mktemp -d -t dayan); trap finish_up EXIT
  case "$(uname -m)" in arm64|aarch64) ARCH="arm64" ;; *) ARCH="x64" ;; esac
  log "setup started (linux/$ARCH)"
  pick_fetch || { echo "This setup needs curl or wget to download (Ubuntu: sudo apt install curl)."; exit 1; }
  has_tty || { [ -n "$KEY_ENV" ] && [ -n "$AUTO_YES" ]; } || { echo "Run this in a Terminal window."; exit 1; }
  SERIAL_GROUP=$(serial_group)
  load_choices
  APP_NAME=$(app_name "$APP"); INSTALL_PAGE=$(install_page "$APP"); CONFIG_DIR=$(config_dir)
  log "app=$APP fetch=$FETCH serial group=${SERIAL_GROUP:-none} python=$(has_python && echo yes || echo no)"

  # The app first: the member installed it from its maker's page.
  find_app || missing_app
  log "found $APP_NAME: $APP_BIN"
  case "$APP_BIN" in "$HOME/.local/bin/"*) local_bin_on_path || PATH_LINE=yes ;; esac
  [ "$WANT_ARDUINO" = "true" ] && ! local_bin_on_path && PATH_LINE=yes

  welcome || cancelled
  ask_key || cancelled
  permissions
  save_uninstaller  # the way back exists before anything changes

  header "Setting things up"
  line "✓  Key accepted${FIRST_NAME:+ -- hello, $FIRST_NAME}" "$GREEN"
  [ "$APP" = opencode ] && { run_step save_key || exit 1; }
  run_step connect_app || exit 1
  run_step add_skills || exit 1
  [ "$APP" != opencode ] || [ "$PATH_LINE" = yes ] && { run_step make_work_folder || exit 1; }
  [ "$WANT_ARDUINO" = "true" ] && { run_step install_arduino || exit 1; }
  HELLO=""
  run_step say_hello || exit 1
  KEY=""
  finish
}

main "$@"
exit $?
