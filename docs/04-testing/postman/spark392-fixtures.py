"""Write synthetic, exact-size still PNGs into the given Postman working directory."""
import struct
import sys
import zlib
from pathlib import Path


def chunk(kind, data):
    checksum = zlib.crc32(kind + data)
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", checksum)


def make_png(size):
    header = chunk(b"IHDR", struct.pack(">IIBBBBB", 2, 1, 8, 2, 0, 0, 0))
    pixels = chunk(b"IDAT", zlib.compress(bytes([0, 32, 96, 180, 240, 240, 240])))
    start = b"\x89PNG\r\n\x1a\n" + header
    end = pixels + chunk(b"IEND", b"")
    label = b"Comment\x00"
    padding = size - len(start + end + label) - 12
    return start + chunk(b"tEXt", label + b"x" * padding) + end


if __name__ == "__main__":
    directory = Path(sys.argv[1]) / "spark392-fixtures"
    directory.mkdir(parents=True, exist_ok=True)
    for size in [4096, 65_537, 1_048_576, 1_048_577, 1_114_112, 5_242_880, 5_242_881]:
        (directory / f"{size}.png").write_bytes(make_png(size))
