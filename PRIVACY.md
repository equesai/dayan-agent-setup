# Privacy

Dayan Agent Setup — the Windows program and the Linux scripts in this
repository — talks to two places, and only for these reasons.

## Dayan

`dayan.equesai.tech` (or the Dayan address the setup was started with):

- **Your choices** — the app you picked and whether you want the Arduino tools,
  read with the one-time code in the setup file's name (Windows) or in the
  Terminal line (Linux). Dayan answers with your first name.
- **Your key** — checked once (`/api/llm/whoami`), so the setup can tell you
  straight away if it is wrong or revoked.
- **Dayan's skills** — downloaded; and, if you asked for the Arduino tools,
  which Arduino file fits your computer.
- **Hello** — one short message through Dayan's model, with your first name, so
  you see Dayan answer before you start.

The check line (`check.ps1`, `check.sh`) tells Dayan only whether the app is
installed and its version number — nothing else, and it changes nothing.

## Arduino's downloads

Only if you asked for the Arduino tools: `arduino-cli` from its release page on
GitHub, and the board files from `downloads.arduino.cc`.

## What is never sent

Nothing else about your computer: no file names, no hardware details, no usage
data. The setup has no telemetry.

## Your key

It stays on your computer, in the app's settings folder, readable only by your
own account. It is never shown on screen, written to the setup's log, or put on
a command line.

## After the setup

The app you chose (OpenCode, Claude Code or Codex) then sends your
conversations with Dayan Agent to Dayan, which answers them. How Dayan handles
them is in Dayan's own privacy policy: <https://dayan.equesai.tech/privacy>.
For Claude Code the setup also turns off its own telemetry and error reporting.

## Removing it

See [Removing it](README.md#removing-it): the key, Dayan's settings and skills
go; your settings from before come back.
