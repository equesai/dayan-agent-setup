# Dayan Agent Setup

Connects **OpenCode**, **Claude Code** or **Codex** on your computer to
[Dayan](https://dayan.equesai.tech), Eques AI's assistant for universities — so
Dayan can tutor you and build projects with you (websites, games, Arduino)
right on your own machine.

This repository is everything that runs on your computer:

| Path | What it is |
|---|---|
| [`windows/DayanSetup.cs`](windows/DayanSetup.cs) | The Windows setup program, `DayanSetup.exe` — one C# file |
| [`windows/build.py`](windows/build.py) | Builds it with the C# compiler that ships with Windows |
| [`windows/check.ps1`](windows/check.ps1) | The Windows check line: is the app installed, and which version? |
| [`linux/setup.sh`](linux/setup.sh) | The Linux setup |
| [`linux/check.sh`](linux/check.sh) | The Linux check line |
| [`pack/`](pack) | Dayan's Tutor and Maker, as the setups add them to OpenCode (Dayan words them for Claude Code and Codex) |

## How you get it

Not from here: Dayan's chat hands it to you — **⚡ Quick → 💻 Run Dayan Agent on my
device**. You first install the app from its maker's own page (the chat has the
button), then run the setup file Dayan gives you (Windows) or paste the line
Dayan gives you into Terminal (Linux). The setup reads the choices you made in
the chat through a one-time code, finds the app, and asks for your Dayan key
(Dayan › Settings › Device keys).

## What it changes

Before it changes anything, it shows every change and where it lands, and waits
for you to agree. It needs no administrator rights: nothing outside your own
account changes.

- **Your Dayan key** — in the app's own settings folder, for you only.
- **The app's settings** — Dayan's model and tools added; your own settings kept.
- **Dayan's skills** — the Tutor and the Maker (with the Maker's helpers).
- **A work folder** — `Dayan` in your home folder, for Claude Code and Codex.
- **The Arduino tools**, if you asked for them — `arduino-cli` and the board files.
- **A way to remove it all** — an entry in Installed apps (Windows), an uninstall script (Linux).

A log of every step is kept in `%LOCALAPPDATA%\Dayan\setup.log` (Windows) or
`~/.local/share/dayan/setup.log` (Linux) — never your key.

## Removing it

- **Windows:** Settings › Apps › Installed apps › **Dayan Agent** › Uninstall
  (or `DayanSetup.exe --uninstall`).
- **Linux:** `bash ~/.local/share/dayan/uninstall.sh`

It takes out what the setup added and puts back the values it replaced in the
app's settings. The app itself, your work folder and the Arduino board files
stay. Your Dayan key keeps working until you revoke it in Dayan (Settings ›
Device keys).

## Privacy

The setup talks only to Dayan — and, if you asked for the Arduino tools, to
Arduino's downloads. It sends nothing else about your computer. Details:
[PRIVACY.md](PRIVACY.md).

## Building it yourself

On Windows 10 or 11 with Python 3:

```
python windows/build.py                          # -> dist/DayanSetup.exe
python windows/build.py --verify dist/DayanSetup.exe
```

The program carries the SHA-256 of `windows/DayanSetup.cs` and the version from
[`VERSION`](VERSION), so `--verify` tells whether any `DayanSetup.exe` — signed
or not — was built from this source.

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

- Committers and reviewers: the members of [Eques AI](https://github.com/EquesAI)
- Approvers: the owners of [Eques AI](https://github.com/EquesAI)

Every release is built from this repository by GitHub Actions on GitHub's own
machines, and each signing request is approved by an approver before it is
signed. Changes from anyone else are reviewed before they are merged.
Privacy policy: [PRIVACY.md](PRIVACY.md).

## License

[MIT](LICENSE). The names Dayan and Eques AI and the Dayan logo belong to Eques
AI; the licence covers the code, not the brand.
