"""Render Forge's text-grid sprites to PNG (stdlib only).

    python3 art/render.py                 # every art/sprites/*.txt -> art/out/<name>.png (1x) and <name>-x8.png
    python3 art/render.py kettlebell 4    # one sprite at 4x
    python3 art/render.py --install       # art/manifest.txt -> Forge/Forge/Resources/Images/Pixel/<name>.png

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

def read_sprite(path, pal):
    """A sprite's rows, and the palette to draw it with: the house palette plus any per-sprite
    colours declared in '#! <key> #RRGGBB' header lines (characters and metal tiers need more
    colours than the shared palette has single-character keys)."""
    rows, local = [], dict(pal)
    for line in Path(path).read_text().splitlines():
        if line.startswith('#!'):
            _, key, hexcolor = line.split()[:3]
            local[key] = (int(hexcolor[1:3], 16), int(hexcolor[3:5], 16), int(hexcolor[5:7], 16), 255)
        elif not line.startswith('#'):
            rows.append(line)
    return rows, local

def png(rows, scale, pal):
    h, w = len(rows), max(len(r) for r in rows)
    raw = b''.join(b'\x00' + b''.join(bytes(pal[r.ljust(w, '.')[x // scale]]) for x in range(w * scale))
                   for r in rows for _ in range(scale))
    chunk = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w * scale, h * scale, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))

APP_PIXEL = ART.parent / 'Forge' / 'Forge' / 'Resources' / 'Images' / 'Pixel'

def install(pal):
    """Render every sprite in art/manifest.txt at its scale into the app's Pixel image folder.
    Pre-scaled with nearest-neighbour so the app never resamples small pixels (blur); the
    csproj marks Resources/Images/Pixel/* Resize="False" for the same reason."""
    APP_PIXEL.mkdir(parents=True, exist_ok=True)
    for line in (ART / 'manifest.txt').read_text().splitlines():
        if not line.strip() or line.startswith('#'):
            continue
        name, scale = line.split()[:2]
        rows, local = read_sprite(ART / 'sprites' / f'{name}.txt', pal)
        (APP_PIXEL / f'{name}.png').write_bytes(png(rows, int(scale), local))
        print(f'installed {name} x{scale} -> {len(rows[0]) * int(scale)}x{len(rows) * int(scale)}')

def main():
    pal = palette()
    if sys.argv[1:2] == ['--install']:
        install(pal)
        return
    names = [sys.argv[1]] if len(sys.argv) > 1 else sorted(p.stem for p in (ART / 'sprites').glob('*.txt'))
    scales = [int(sys.argv[2])] if len(sys.argv) > 2 else [1, 8]
    out = ART / 'out'; out.mkdir(exist_ok=True)
    for name in names:
        rows, local = read_sprite(ART / 'sprites' / f'{name}.txt', pal)
        for s in scales:
            (out / (f'{name}.png' if s == 1 else f'{name}-x{s}.png')).write_bytes(png(rows, s, local))
        print(f'{name}: {len(rows[0])}x{len(rows)}')

if __name__ == '__main__':
    main()
