"""Render Forge's text-grid sprites to PNG (stdlib only).

    python3 art/render.py                 # every art/sprites/*.txt -> art/out/<name>.png (1x) and <name>-x8.png
    python3 art/render.py kettlebell 4    # one sprite at 4x

Each sprite is a grid of palette keys (art/palette.txt); '.' is transparent. The palette was read
pixel-by-pixel from the hand-drawn Resources/Images/Equipment/dumbbell.png, which sets the house
style: warm greys, cream top-lit edge, black outline, slate cast shadow outside the outline, and a
2-pixel gold/orange rivet accent. Not wired into the app build yet.
"""
import struct, sys, zlib
from pathlib import Path

ART = Path(__file__).resolve().parent

def palette():
    pal = {'.': (0, 0, 0, 0)}
    for line in (ART / 'palette.txt').read_text().splitlines():
        if not line.strip() or line.startswith('#'):
            continue
        key, hexcolor, *_ = line.split()
        pal[key] = (int(hexcolor[1:3], 16), int(hexcolor[3:5], 16), int(hexcolor[5:7], 16), 255)
    return pal

def png(rows, scale, pal):
    h, w = len(rows), max(len(r) for r in rows)
    raw = b''.join(b'\x00' + b''.join(bytes(pal[r.ljust(w, '.')[x // scale]]) for x in range(w * scale))
                   for r in rows for _ in range(scale))
    chunk = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w * scale, h * scale, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))

def main():
    pal = palette()
    names = [sys.argv[1]] if len(sys.argv) > 1 else sorted(p.stem for p in (ART / 'sprites').glob('*.txt'))
    scales = [int(sys.argv[2])] if len(sys.argv) > 2 else [1, 8]
    out = ART / 'out'; out.mkdir(exist_ok=True)
    for name in names:
        rows = [l for l in (ART / 'sprites' / f'{name}.txt').read_text().splitlines() if not l.startswith('#')]
        for s in scales:
            (out / (f'{name}.png' if s == 1 else f'{name}-x{s}.png')).write_bytes(png(rows, s, pal))
        print(f'{name}: {len(rows[0])}x{len(rows)}')

if __name__ == '__main__':
    main()
