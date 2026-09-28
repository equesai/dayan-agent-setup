"""Build DayanSetup.exe from DayanSetup.cs with the C# compiler that ships with
Windows (.NET Framework 4.x) -- nothing to install.

    python build.py                -> the setup file: in the Dayan tree
                                      backend/device_kit/windows/DayanSetup.exe (what Dayan
                                      serves), in the public repository dist/DayanSetup.exe
    python build.py --out PATH     -> there instead
    python build.py --verify PATH  -> checks a built (or signed) file: built from this
                                      DayanSetup.cs, with this version
    python build.py --sign         -> build, then sign with a code-signing certificate

The source's SHA-256 is compiled in (BuildInfo.SourceSha256), so a test can
prove the committed .exe was built from the committed source
(backend/tests/test_device_kit.py); a signed file keeps the same source hash.
The version comes from ../VERSION (BuildInfo.Version and the file's version
details), so a new version number does not change the source hash.

Signing (docs/16 §7): until the file is signed and known, SmartScreen warns
("Windows protected your PC") and Smart App Control blocks it -- Defender
itself scans it clean. The public repository's build signs it through SignPath
(free for open-source projects); --sign here uses signtool.exe (Windows SDK) with
    DAYAN_SIGN_THUMBPRINT   the certificate's SHA-1 thumbprint in the Windows
                            certificate store (a hardware token or a cloud
                            signing service's local store), or
    DAYAN_SIGN_PFX          a .pfx file (+ DAYAN_SIGN_PFX_PASSWORD),
    DAYAN_SIGN_TIMESTAMP    an RFC 3161 time-stamp server
                            (default http://timestamp.digicert.com),
    DAYAN_SIGNTOOL          signtool.exe when it is not in the SDK's usual place,
and verifies the signature afterwards.
"""
import hashlib
import os
import pathlib
import re
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
SOURCE = HERE / "DayanSetup.cs"
VERSION_FILE = HERE.parent / "VERSION"
_SERVED = HERE.parents[1] / "backend" / "device_kit" / "windows"
# In the Dayan tree the file goes where Dayan serves it; in the public repository, to dist/.
OUT = _SERVED / "DayanSetup.exe" if _SERVED.is_dir() else HERE.parent / "dist" / "DayanSetup.exe"
ICON = HERE / "dayan.ico"
WINDIR = os.environ.get("WINDIR", r"C:\Windows")
CSC = pathlib.Path(WINDIR) / "Microsoft.NET" / "Framework64" / "v4.0.30319" / "csc.exe"
REFS = ["System.Web.Extensions.dll", "System.IO.Compression.dll", "System.IO.Compression.FileSystem.dll"]


def source_sha256() -> str:
    # Line endings do not change the program: hash the LF form.
    return hashlib.sha256(SOURCE.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def version() -> str:
    text = VERSION_FILE.read_text(encoding="utf-8").strip()
    if not re.fullmatch(r"\d+\.\d+\.\d+", text):
        sys.exit(f"{VERSION_FILE} must hold a version like 1.1.0, not {text!r}")
    return text


def verify(path: pathlib.Path) -> int:
    data = path.read_bytes()
    problems = []
    if data[:2] != b"MZ":
        problems.append("not a Windows program")
    if source_sha256().encode("utf-16-le") not in data:
        problems.append("not built from this DayanSetup.cs (the source hash differs)")
    if f'"{version()}"'.encode("utf-16-le") not in data and version().encode("utf-16-le") not in data:
        problems.append(f"not version {version()}")
    for problem in problems:
        print(f"{path.name}: {problem}")
    if not problems:
        print(f"{path.name}: built from this source ({source_sha256()[:16]}…), version {version()}")
    return 1 if problems else 0


def signtool() -> pathlib.Path:
    explicit = os.environ.get("DAYAN_SIGNTOOL", "").strip()
    if explicit:
        return pathlib.Path(explicit)
    kits = pathlib.Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) / "Windows Kits" / "10" / "bin"
    found = sorted(kits.glob("*/x64/signtool.exe"))
    if not found:
        sys.exit("signtool.exe not found: install the Windows SDK, or set DAYAN_SIGNTOOL")
    return found[-1]


def sign(path: pathlib.Path) -> int:
    tool = signtool()
    stamp = os.environ.get("DAYAN_SIGN_TIMESTAMP", "").strip() or "http://timestamp.digicert.com"
    cmd = [str(tool), "sign", "/fd", "sha256", "/tr", stamp, "/td", "sha256", "/d", "Dayan Agent Setup"]
    thumbprint = os.environ.get("DAYAN_SIGN_THUMBPRINT", "").strip()
    pfx = os.environ.get("DAYAN_SIGN_PFX", "").strip()
    if thumbprint:
        cmd += ["/sha1", thumbprint]
    elif pfx:
        cmd += ["/f", pfx]
        if os.environ.get("DAYAN_SIGN_PFX_PASSWORD"):
            cmd += ["/p", os.environ["DAYAN_SIGN_PFX_PASSWORD"]]
    else:
        sys.exit("--sign needs DAYAN_SIGN_THUMBPRINT or DAYAN_SIGN_PFX (docs/16 §7)")
    for step in (cmd + [str(path)], [str(tool), "verify", "/pa", str(path)]):
        result = subprocess.run(step, capture_output=True, text=True)
        print((result.stdout + result.stderr).strip())
        if result.returncode != 0:
            return result.returncode
    print(f"signed and verified {path.name}")
    return 0


def main() -> int:
    args = sys.argv[1:]
    if "--verify" in args:
        return verify(pathlib.Path(args[args.index("--verify") + 1]))
    out = pathlib.Path(args[args.index("--out") + 1]).resolve() if "--out" in args else OUT
    if not CSC.exists():
        sys.exit(f"C# compiler not found at {CSC}")
    number = version()
    info = HERE / "BuildInfo.g.cs"
    info.write_text(
        f'[assembly: System.Reflection.AssemblyVersion("{number}.0")]\n'
        f'[assembly: System.Reflection.AssemblyFileVersion("{number}.0")]\n'
        f'[assembly: System.Reflection.AssemblyInformationalVersion("{number}")]\n'
        "namespace Dayan { static class BuildInfo {\n"
        f'    public const string SourceSha256 = "{source_sha256()}";\n'
        f'    public const string Version = "{number}";\n'
        "} }\n", encoding="utf-8")
    out.parent.mkdir(parents=True, exist_ok=True)
    cmd = [str(CSC), "/nologo", "/target:exe", "/platform:anycpu", "/optimize+", "/codepage:65001",
           f"/win32icon:{ICON}", f"/out:{out}"] + [f"/r:{r}" for r in REFS] + [str(SOURCE), str(info)]
    result = subprocess.run(cmd, capture_output=True, text=True)
    info.unlink(missing_ok=True)
    print(result.stdout.strip() or "(no compiler output)")
    if result.returncode != 0:
        print(result.stderr)
        return result.returncode
    print(f"built {out.name} {number}: {out.stat().st_size:,} bytes, source {source_sha256()[:16]}…")
    if "--sign" in args:
        return sign(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
