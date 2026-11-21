#!/usr/bin/env python3
"""Dungeon Master ZIP timestamp utility.

Commands:
  stamp FILE [FILE ...]                 Set new/edited file mtimes to now.
  pack SOURCE_DIRECTORY OUTPUT.zip      Package files with correct local ZIP dates.
  audit ARCHIVE.zip                     Check all entries recursively.

Defaults to a flat ZIP root, as required by Progression.json. --nested is
available only for explicitly requested archives with subdirectories.
Unmodified source files retain their existing modification times.
"""
from __future__ import annotations

import argparse
import datetime as dt
import os
from pathlib import Path
import sys
import time
import zipfile
from zoneinfo import ZoneInfo

GERMANY = ZoneInfo('Europe/Berlin')
MIN_ZIP_DATE = dt.datetime(1980, 1, 1)


def german_timestamp(epoch: float) -> dt.datetime:
    return dt.datetime.fromtimestamp(epoch, GERMANY).replace(tzinfo=None)


def zip_time(epoch: float) -> tuple[int, int, int, int, int, int]:
    value = german_timestamp(epoch)
    if value < MIN_ZIP_DATE:
        raise ValueError(f'File timestamp predates ZIP support (1980): {value}')
    # ZIP DOS timestamp precision is two seconds; round down, never up.
    return (value.year, value.month, value.day, value.hour, value.minute,
            value.second - value.second % 2)


def stamp(paths: list[str]) -> None:
    now = time.time()
    for text in paths:
        file = Path(text)
        if not file.is_file():
            raise ValueError(f'Not a file: {file}')
        os.utime(file, (now, now))
        print(f'STAMP {file} -> {german_timestamp(file.stat().st_mtime).isoformat(sep=" ")} Germany')


def pack(source: str, destination: str, nested: bool) -> None:
    root = Path(source).resolve()
    output = Path(destination).resolve()
    if not root.is_dir():
        raise ValueError(f'Not a directory: {root}')
    files = sorted(p for p in root.rglob('*') if p.is_file() and p.resolve() != output)
    if not files:
        raise ValueError('No input files found')
    members = {}
    for path in files:
        member = path.relative_to(root).as_posix() if nested else path.name
        if member in members:
            raise ValueError(f'Flat ZIP filename collision: {members[member]} and {path}')
        members[member] = path
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED) as z:
        for member, path in members.items():
            info = zipfile.ZipInfo(member, date_time=zip_time(path.stat().st_mtime))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = (0o100644 << 16)
            z.writestr(info, path.read_bytes())
    print(f'CREATED {output} ({len(files)} files; {"nested" if nested else "flat"} layout)')
    audit(str(output), allow_nested=nested, source=root)


def audit(filename: str, allow_nested: bool = False, source: Path | None = None) -> None:
    archive = Path(filename)
    errors = []
    seen = set()
    with zipfile.ZipFile(archive) as z:
        items = z.infolist()
        if not items:
            errors.append('Empty ZIP')
        for info in items:
            entry = info.filename
            if entry in seen:
                errors.append(f'Duplicate ZIP entry: {entry}')
            seen.add(entry)
            if info.is_dir():
                if not allow_nested:
                    errors.append(f'Directory entry violates flat rule: {entry}')
                continue
            if not allow_nested and ('/' in entry or '\\' in entry):
                errors.append(f'Nested entry violates default flat rule: {entry}')
            if entry.startswith('/') or '\\' in entry or '..' in Path(entry).parts:
                errors.append(f'Unsafe ZIP member: {entry}')
            try:
                dt.datetime(*info.date_time)
            except ValueError:
                errors.append(f'Invalid ZIP timestamp: {entry}: {info.date_time}')
            if info.date_time[5] % 2:
                errors.append(f'Invalid ZIP 2-second timestamp: {entry}: {info.date_time}')
            if source is not None:
                # Flat packaging compares against file discovered by filename;
                # collisions have already been rejected in pack().
                path = (source / entry) if allow_nested else next(
                    (p for p in source.rglob('*') if p.is_file() and p.name == entry), None)
                if path is None or not path.is_file():
                    errors.append(f'Cannot find source file for {entry}')
                elif tuple(info.date_time) != zip_time(path.stat().st_mtime):
                    errors.append(f'Wrong timestamp for {entry}: {info.date_time}')
            print(f'  {entry} : {dt.datetime(*info.date_time).strftime("%d.%m.%Y %H:%M:%S")} Europe/Berlin')
        corrupt = z.testzip()
        if corrupt:
            errors.append(f'Corrupt ZIP member: {corrupt}')
    if errors:
        for error in errors:
            print('ERROR:', error, file=sys.stderr)
        raise ValueError(f'{len(errors)} ZIP validation error(s)')
    print(f'PASS: {len([i for i in items if not i.is_dir()])} files checked recursively; ZIP CRC verified.')
    if source is None:
        print('Note: original filesystem mtime requires source files for comparison.')


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest='action', required=True)
    sub.add_parser('stamp').add_argument('files', nargs='+')
    pk = sub.add_parser('pack')
    pk.add_argument('directory')
    pk.add_argument('output_zip')
    pk.add_argument('--nested', action='store_true', help='Explicit exception to project flat ZIP rule')
    au = sub.add_parser('audit')
    au.add_argument('zip')
    au.add_argument('--nested', action='store_true', help='Permit nested entries, still check every file')
    args = p.parse_args()
    try:
        if args.action == 'stamp':
            stamp(args.files)
        elif args.action == 'pack':
            pack(args.directory, args.output_zip, args.nested)
        else:
            audit(args.zip, allow_nested=args.nested)
    except (ValueError, OSError, zipfile.BadZipFile) as exc:
        p.exit(1, f'FAILED: {exc}\n')


if __name__ == '__main__':
    main()
