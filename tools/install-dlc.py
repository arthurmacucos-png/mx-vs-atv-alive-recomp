"""Install Xbox 360 DLC packages (STFS LIVE/PIRS/CON) for the native build.

rexglue mounts marketplace content as plain host folders, not as STFS packages,
and reads the license from a sidecar .header file. This script extracts each
package and writes that header, using the same layout as rexglue's
ContentManager:

  <save root>/0000000000000000/<title>/00000002/<package file name>/...files
  <save root>/0000000000000000/<title>/Headers/00000002/<package file name>.header

The STFS reader follows rexglue's StfsContainerDevice (block/hash math).

usage: python tools/install-dlc.py <folder with packages> [--dest <save root>]
       [--title 5451087B] [--dry-run]
"""
import argparse
import ctypes
import os
import struct
import sys

BLOCK = 0x1000
L0 = 170
L1 = 170 * 170
END_OF_CHAIN = 0xFFFFFF
MAGICS = (b"LIVE", b"PIRS", b"CON ")
CONTENT_TYPE_MARKETPLACE = 0x00000002


def u24le(b):
    return b[0] | (b[1] << 8) | (b[2] << 16)


class Stfs:
    def __init__(self, path):
        self.path = path
        self.f = open(path, "rb")
        h = self.f.read(0x971A)
        if len(h) < 0x971A or h[:4] not in MAGICS:
            raise ValueError("not an STFS package")
        self.magic = h[:4].decode().strip()
        self.licenses = [struct.unpack_from(">QII", h, 0x22C + i * 16) for i in range(16)]
        self.header_size = struct.unpack_from(">I", h, 0x340)[0]
        self.content_type = struct.unpack_from(">I", h, 0x344)[0]
        self.title_id = struct.unpack_from(">I", h, 0x360)[0]
        self.volume_type = struct.unpack_from(">I", h, 0x3A9)[0]
        name = h[0x411:0x411 + 0x80].decode("utf-16-be", "ignore")
        self.display_name = name.split("\x00", 1)[0]
        vd = 0x379
        self.flags = h[vd + 2]
        self.read_only = bool(self.flags & 1)
        self.root_active = bool(self.flags & 2)
        self.ft_count = struct.unpack_from("<H", h, vd + 3)[0]
        self.ft_block = u24le(h[vd + 5:vd + 8])
        self.total_blocks = struct.unpack_from(">I", h, vd + 0x1C)[0]
        self.bpht = 1 if self.read_only else 2
        self.step0 = L0 + self.bpht
        self.step1 = L1 + (L0 + 1) * self.bpht
        self.base = (self.header_size + BLOCK - 1) & ~(BLOCK - 1)
        self.hash_cache = {}

    # --- rexglue StfsContainerDevice::BlockToOffsetSTFS ---
    def block_offset(self, index):
        base = L0
        block = index
        for _ in range(3):
            block += ((index + base) // base) * self.bpht
            if index < base:
                break
            base *= L0
        return self.base + (block << 12)

    def hash_block_number(self, index, level):
        if level == 0:
            if index < L0:
                return 0
            block = (index // L0) * self.step0
            block += ((index // L1) + 1) * self.bpht
            if index < L1:
                return block
            return block + self.bpht
        if level == 1:
            if index < L1:
                return self.step0
            return (index // L1) * self.step1 + self.bpht
        return self.step1

    def hash_table(self, offset):
        if offset not in self.hash_cache:
            self.f.seek(offset)
            self.hash_cache[offset] = self.f.read(BLOCK)
        return self.hash_cache[offset]

    def entry_info(self, table, record):
        return struct.unpack_from(">I", table, record * 0x18 + 0x14)[0]

    def next_block(self, index):
        secondary = BLOCK if self.root_active else 0
        if self.read_only:
            secondary = 0
        else:
            if self.total_blocks > L0:
                if self.total_blocks > L1:
                    off2 = self.base + (self.hash_block_number(index, 2) << 12)
                    t2 = self.hash_table(off2 + secondary)
                    info = self.entry_info(t2, (index // L1) % L0)
                    secondary = BLOCK if info & 0x40000000 else 0
                off1 = self.base + (self.hash_block_number(index, 1) << 12)
                t1 = self.hash_table(off1 + secondary)
                info = self.entry_info(t1, (index // L0) % L0)
                secondary = BLOCK if info & 0x40000000 else 0
        off0 = self.base + (self.hash_block_number(index, 0) << 12)
        t0 = self.hash_table(off0 + secondary)
        return self.entry_info(t0, index % L0) & 0xFFFFFF

    def entries(self):
        out = []
        block = self.ft_block
        for _ in range(self.ft_count):
            self.f.seek(self.block_offset(block))
            data = self.f.read(BLOCK)
            for m in range(64):
                e = data[m * 0x40:(m + 1) * 0x40]
                if e[0] == 0:
                    break
                flags = e[0x28]
                name = e[:flags & 0x3F].decode("latin-1")
                out.append({
                    "name": name,
                    "dir": bool(flags & 0x80),
                    "contiguous": bool(flags & 0x40),
                    "blocks": u24le(e[0x2C:0x2F]),
                    "start": u24le(e[0x2F:0x32]),
                    "parent": struct.unpack_from(">H", e, 0x32)[0],
                    "size": struct.unpack_from(">I", e, 0x34)[0],
                })
            block = self.next_block(block)
            if block == END_OF_CHAIN:
                break
        return out

    def read_file(self, entry):
        remaining = entry["size"]
        block = entry["start"]
        chunks = []
        while remaining and block != END_OF_CHAIN:
            n = min(BLOCK, remaining)
            self.f.seek(self.block_offset(block))
            chunks.append(self.f.read(n))
            remaining -= n
            block = (block + 1) if entry["contiguous"] else self.next_block(block)
        if remaining:
            raise IOError("{}: {} bytes missing".format(entry["name"], remaining))
        return b"".join(chunks)

    def paths(self, entries):
        result = []
        for e in entries:
            parts = [e["name"]]
            p = e["parent"]
            guard = 0
            while p != 0xFFFF and guard < 64:
                parts.append(entries[p]["name"])
                p = entries[p]["parent"]
                guard += 1
            result.append(os.path.join(*reversed(parts)))
        return result


def aggregate_header(file_name, display_name, title_id, license_mask):
    """XCONTENT_AGGREGATE_DATA (0x148, big-endian) + host-order u32 license."""
    buf = bytearray(0x148)
    struct.pack_into(">II", buf, 0, 1, CONTENT_TYPE_MARKETPLACE)  # device HDD, type
    name = display_name.encode("utf-16-be")[:254]
    buf[8:8 + len(name)] = name
    fn = file_name.encode("ascii")[:42]
    buf[0x108:0x108 + len(fn)] = fn
    # 0x134 is alignment padding; be<u64> xuid sits at 0x138, title id at 0x140.
    struct.pack_into(">Q", buf, 0x138, 0)  # xuid 0 = shared marketplace content
    struct.pack_into(">I", buf, 0x140, title_id)
    return bytes(buf) + struct.pack("<I", license_mask)


def documents_dir():
    try:
        buf = ctypes.create_unicode_buffer(260)
        if ctypes.windll.shell32.SHGetFolderPathW(None, 5, None, 0, buf) == 0:
            return buf.value
    except Exception:
        pass
    return os.path.join(os.path.expanduser("~"), "Documents")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("source")
    ap.add_argument("--dest", default=os.path.join(documents_dir(), "mx_alive"))
    ap.add_argument("--title", default="5451087B")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    title = int(args.title, 16)

    content_root = os.path.join(args.dest, "0000000000000000", "{:08X}".format(title))
    pkg_root = os.path.join(content_root, "00000002")
    hdr_root = os.path.join(content_root, "Headers", "00000002")

    installed, skipped = 0, 0
    for dirpath, _, files in os.walk(args.source):
        for fn in sorted(files):
            path = os.path.join(dirpath, fn)
            try:
                pkg = Stfs(path)
            except Exception:
                continue
            if pkg.title_id != title or pkg.content_type != CONTENT_TYPE_MARKETPLACE:
                print("skip  {}  (title {:08X}, type {:08X})".format(fn, pkg.title_id, pkg.content_type))
                skipped += 1
                continue
            if pkg.volume_type != 0:
                print("skip  {}  (SVOD package, not supported here)".format(fn))
                skipped += 1
                continue
            entries = pkg.entries()
            rel = pkg.paths(entries)
            files_n = sum(1 for e in entries if not e["dir"])
            size = sum(e["size"] for e in entries if not e["dir"])
            print("DLC   {}  {}  [{} files, {:.1f} MB]".format(fn, pkg.display_name, files_n, size / 1e6))
            if args.dry_run:
                continue
            out = os.path.join(pkg_root, fn)
            for e, r in zip(entries, rel):
                target = os.path.join(out, r)
                if e["dir"]:
                    os.makedirs(target, exist_ok=True)
                    continue
                os.makedirs(os.path.dirname(target), exist_ok=True)
                with open(target, "wb") as o:
                    o.write(pkg.read_file(e))
            os.makedirs(hdr_root, exist_ok=True)
            with open(os.path.join(hdr_root, fn + ".header"), "wb") as o:
                o.write(aggregate_header(fn, pkg.display_name, title, 0xFFFFFFFF))
            installed += 1
    print("installed {} package(s), skipped {}; destination: {}".format(installed, skipped, pkg_root))
    return 0 if installed or args.dry_run else 1


if __name__ == "__main__":
    sys.exit(main())
