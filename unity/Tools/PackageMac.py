"""Package a Unity macOS .app from Windows, then verify the portable ZIP."""

import argparse
import hashlib
import os
from pathlib import Path
import plistlib
import posixpath
import shutil
import stat
import struct
import sys
import tempfile
import zipfile
import zlib


INTEL = 0x01000007
APPLE_SILICON = 0x0100000C
UNIVERSAL = {INTEL, APPLE_SILICON}
THIN = {
    b"\xce\xfa\xed\xfe": "<",
    b"\xcf\xfa\xed\xfe": "<",
    b"\xfe\xed\xfa\xce": ">",
    b"\xfe\xed\xfa\xcf": ">",
}
FAT = {
    b"\xca\xfe\xba\xbe": (">", False),
    b"\xca\xfe\xba\xbf": (">", True),
    b"\xbe\xba\xfe\xca": ("<", False),
    b"\xbf\xba\xfe\xca": ("<", True),
}


def require(condition, error):
    if not condition:
        raise ValueError(error)


def is_native(header):
    return header[:4] in THIN or header[:4] in FAT


def architectures(image):
    """Return CPU types from real Mach-O slices, checking the fat wrapper."""
    magic = image[:4]
    if magic in THIN:
        require(len(image) >= 28, "Invalid, truncated Mach-O header")
        return {struct.unpack_from(THIN[magic] + "I", image, 4)[0]}
    require(magic in FAT and len(image) >= 8, "Main executable is not Mach-O")
    order, fat64 = FAT[magic]
    fmt = order + ("IIQQII" if fat64 else "IIIII")
    entry_size = struct.calcsize(fmt)
    count = struct.unpack_from(order + "I", image, 4)[0]
    table_end = 8 + count * entry_size
    require(0 < count <= 32 and table_end <= len(image), "Invalid Mach-O architecture table")
    cpu_types = set()
    for index in range(count):
        cpu, _, offset, length, *_ = struct.unpack_from(fmt, image, 8 + index * entry_size)
        require(offset >= table_end and length >= 28 and offset + length <= len(image),
                "Mach-O slice points outside the executable")
        header = image[offset:offset + 4]
        require(header in THIN and struct.unpack_from(THIN[header] + "I", image, offset + 4)[0] == cpu,
                "Mach-O slice does not match its architecture table")
        cpu_types.add(cpu)
    return cpu_types


def link_target(root, name, target):
    require(target and "\0" not in target and "\\" not in target and not target.startswith("/") and
            ":" not in target.split("/", 1)[0], f"Invalid relative symlink {name}")
    parts = target.split("/")
    first_path = next((i for i, part in enumerate(parts) if part not in {"..", ".", ""}), len(parts))
    require(".." not in parts[first_path:], f"Symlink has ambiguous parent traversal: {name}")
    resolved = posixpath.normpath(posixpath.join(posixpath.dirname(name), target))
    require(resolved == root or resolved.startswith(root + "/"), f"Symlink leaves the app: {name} -> {target}")
    return resolved


def follow(name, entries, links):
    """Resolve archive-local framework aliases without extracting the zip."""
    seen = set()
    while True:
        require(name not in seen, f"Cyclic symlink: {name}")
        seen.add(name)
        parts = name.split("/")
        for index in range(1, len(parts) + 1):
            prefix = "/".join(parts[:index])
            if prefix in links:
                name = "/".join([links[prefix], *parts[index:]])
                break
        else:
            require(name in entries, f"Missing bundle entry: {name}")
            return name
        require(len(seen) <= len(links) + 1, f"Cyclic symlink: {name}")


def verify(path):
    with zipfile.ZipFile(path) as archive:
        entries = {}
        folded_names = set()
        for item in archive.infolist():
            name = item.filename.rstrip("/")
            require(all(part not in ("", ".", "..") for part in name.split("/")) and "\\" not in name,
                    f"Unsafe ZIP entry: {item.filename}")
            require(name.casefold() not in folded_names, f"Duplicate ZIP entry: {name}")
            folded_names.add(name.casefold())
            require(item.create_system == 3, f"ZIP entry lacks Unix file type and permissions: {name}")
            mode = item.external_attr >> 16
            kind = stat.S_IFMT(mode)
            require(kind in {stat.S_IFREG, stat.S_IFDIR, stat.S_IFLNK}, f"Unknown file type: {name}")
            require(item.is_dir() == (kind == stat.S_IFDIR), f"Inconsistent directory mode: {name}")
            require(not item.is_dir() or mode & stat.S_IXUSR, f"Directory is not traversable: {name}")
            entries[name] = item

        roots = {name.split("/", 1)[0] for name in entries}
        require(len(roots) == 1, "Expected one top-level .app bundle")
        root = roots.pop()
        require(root.endswith(".app") and root in entries and entries[root].is_dir(), "No top-level .app directory")

        links = {}
        for name, item in entries.items():
            if not stat.S_ISLNK(item.external_attr >> 16):
                continue
            require(item.file_size <= 4096, f"Oversized symlink target: {name}")
            links[name] = link_target(root, name, archive.read(item).decode("utf-8"))
        for name in links:
            correct_name = follow(name, entries, links)
            require(correct_name not in links, f"Invalid symlink target: {name}")
            require(not any(path_name.startswith(name + "/") for path_name in entries),
                    f"ZIP stores files inside its symlink: {name}")

        contents = root + "/Contents/"
        info_name = follow(contents + "Info.plist", entries, links)
        info = plistlib.loads(archive.read(entries[info_name]))
        require(isinstance(info, dict), "Expected a dictionary in Info.plist")
        program = info.get("CFBundleExecutable")
        require(isinstance(program, str) and program and program not in {".", ".."} and
                "/" not in program and "\\" not in program, "CFBundleExecutable must name a program directly under Contents/MacOS")
        require(info.get("CFBundlePackageType") == "APPL", "Info.plist does not declare an application bundle")
        exe_name = follow(contents + "MacOS/" + program, entries, links)
        exe = entries[exe_name]
        require(stat.S_ISREG(exe.external_attr >> 16) and (exe.external_attr >> 16) & stat.S_IXUSR,
                "Main macOS executable is missing its Unix executable mode")
        require(UNIVERSAL.issubset(architectures(archive.read(exe))),
                "Main macOS executable must contain Intel x86_64 and Apple silicon arm64")

        def has_file(name):
            try:
                item = entries[follow(name, entries, links)]
                return stat.S_ISREG(item.external_attr >> 16) and item.file_size > 0
            except ValueError:
                return False

        data = contents + "Resources/Data/"
        require(has_file(data + "globalgamemanagers"), "Missing Unity player data")
        require(has_file(data + "Managed/Assembly-CSharp.dll") or
                has_file(data + "il2cpp_data/Metadata/global-metadata.dat"), "Missing Unity game code")
        for name, item in entries.items():
            mode = item.external_attr >> 16
            if not stat.S_ISREG(mode):
                continue
            with archive.open(item) as file:
                first = file.read(4)
            if is_native(first) or name.endswith(".dylib") or first.startswith(b"#!"):
                require(mode & stat.S_IXUSR, f"Missing executable mode: {name}")
            if name == contents + "Frameworks/UnityPlayer.dylib":
                require(UNIVERSAL.issubset(architectures(archive.read(item))),
                        "Shared UnityPlayer.dylib must support both macOS architectures")
        corrupt = archive.testzip()
        require(corrupt is None, f"Bad ZIP CRC: {corrupt}")
        return root, len(entries), len(links)


def entry(name, file_type, permissions):
    """Write predictable Unix ZIP metadata even on an NTFS host."""
    result = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
    result.create_system = 3
    result.compress_type = zipfile.ZIP_DEFLATED
    result.external_attr = (file_type | permissions) << 16
    if file_type == stat.S_IFDIR:
        result.external_attr |= 0x10
    return result


def populate(archive, source, root):
    def add(path, name):
        if path.is_symlink():
            target = os.readlink(path).replace("\\", "/")
            link_target(root, name, target)
            archive.writestr(entry(name, stat.S_IFLNK, 0o777), target.encode("utf-8"))
        elif path.is_dir():
            require(not getattr(path, "is_junction", lambda: False)(), f"Unsupported Windows folder junction: {path}")
            archive.writestr(entry(name + "/", stat.S_IFDIR, 0o755), b"")
            for child in sorted(path.iterdir(), key=lambda item: item.name):
                add(child, name + "/" + child.name)
        elif path.is_file():
            with path.open("rb") as contents:
                header = contents.read(4)
                contents.seek(0)
                executable = is_native(header) or name.endswith(".dylib") or header.startswith(b"#!")
                executable |= os.name != "nt" and bool(path.stat().st_mode & stat.S_IXUSR)
                mode = 0o755 if executable else 0o644
                with archive.open(entry(name, stat.S_IFREG, mode), "w") as target:
                    shutil.copyfileobj(contents, target)
        else:
            raise ValueError(f"Unsupported bundle object: {path}")

    add(source, root)


def pack(source, output):
    require(source.is_dir() and not source.is_symlink() and source.name.endswith(".app"), "Input must be a .app directory")
    require(output.suffix.lower() == ".zip", "Output filename must end with .zip")
    require(not output.resolve().is_relative_to(source.resolve()), "Output must be outside the .app")
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=output.parent, suffix=".zip", delete=False) as handle:
        temporary = Path(handle.name)
    try:
        with zipfile.ZipFile(temporary, "w") as archive:
            populate(archive, source, source.name)
        result = verify(temporary)
        temporary.replace(output)
        return result
    finally:
        temporary.unlink(missing_ok=True)


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as file:
        for chunk in iter(lambda: file.read(1048576), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    tasks = cli.add_subparsers(dest="task", required=True)
    packing = tasks.add_parser("pack", help="Create and verify a Mac ZIP")
    packing.add_argument("app", type=Path)
    packing.add_argument("zip", type=Path)
    verifying = tasks.add_parser("verify", help="Verify an existing Mac ZIP")
    verifying.add_argument("zip", type=Path)
    args = cli.parse_args()
    try:
        root, count, links = pack(args.app, args.zip) if args.task == "pack" else verify(args.zip)
    except (OSError, ValueError, zipfile.BadZipFile, plistlib.InvalidFileException, zlib.error) as error:
        cli.exit(1, f"Mac package error: {error}\n")
    print(f"{root}: x86_64 + arm64, {count} entries, {links} symlinks")
    print(f"SHA-256 {sha256(args.zip)}  {args.zip}")


if __name__ == "__main__":
    main()
