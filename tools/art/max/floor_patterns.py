"""Floor patterns in world units (pure Python, so the same code runs on a Mac without numpy).

colour(pattern, x, z, tile) -> (r, g, b) in 0..255, sRGB. x, z in the room's own units; `tile` is
the checker square's side or the parquet plank's width.
"""
import math


def _hash(i, j, s=0):
    h = (i * 374761393 + j * 668265263 + s * 2147483647) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def _noise(x, y, s=0):
    i, j = math.floor(x), math.floor(y)
    fx, fy = x - i, y - j
    ux, uy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b = _hash(i, j, s), _hash(i + 1, j, s)
    c, d = _hash(i, j + 1, s), _hash(i + 1, j + 1, s)
    return (a + (b - a) * ux) + ((c + (d - c) * ux) - (a + (b - a) * ux)) * uy


def _fbm(x, y, s=0):
    return 0.5 * _noise(x, y, s) + 0.3 * _noise(2.1 * x, 2.1 * y, s + 1) + 0.2 * _noise(4.3 * x, 4.3 * y, s + 2)


def _mix(a, b, t):
    return tuple(a[k] + (b[k] - a[k]) * t for k in range(3))


def checker(x, z, tile):
    u, v = x / tile, z / tile
    i, j = math.floor(u), math.floor(v)
    fu, fv = u - i, v - j
    dark = (i + j) % 2 == 0
    # Marble: a soft cloud plus thin veins bent by noise.
    cloud = _fbm(u * 1.7, v * 1.7, 3 if dark else 7)
    # Each tile is its own slab: its veins run at its own angle and phase.
    a = _hash(i, j, 17) * math.pi
    ca, sa = math.cos(a), math.sin(a)
    w = 10.0 * _fbm(u * 0.7 + 13 * _hash(i, j, 5), v * 0.7, 11)
    vein = abs(math.sin((fu * ca + fv * sa) * 2.4 + w))
    vein = max(0.0, 1.0 - vein * 16.0) * (0.4 + 0.6 * _noise(u * 3, v * 3, 23))
    if dark:
        base = _mix((24, 25, 30), (52, 54, 62), cloud)
        col = _mix(base, (150, 150, 158), 0.55 * vein)
    else:
        base = _mix((214, 205, 186), (238, 232, 218), cloud)
        col = _mix(base, (150, 145, 138), 0.45 * vein)
    edge = min(fu, 1 - fu, fv, 1 - fv)
    if edge < 0.012:
        col = _mix(col, (95, 92, 88), 0.75)
    return col


def _plank(x, y, k):
    """Herringbone of k x 1 planks: (plank id, along 0..1, across 0..1, vertical?)."""
    X, Y = math.floor(x), math.floor(y)
    r = (x - Y) % (2 * k)
    if r < k:
        b = math.floor((x - Y) / (2 * k))
        return (b, Y, 0), r / k, y - Y, False
    q = (y - (X + 1 - 2 * k)) % (2 * k)
    b = math.floor((y - (X + 1 - 2 * k)) / (2 * k))
    return (X, b, 1), q / k, x - X, True


def herringbone(x, z, tile, k=4):
    pid, along, across, vertical = _plank(x / tile, z / tile, k)
    tint = _hash(*pid)
    base = _mix((168, 112, 62), (206, 150, 90), tint)
    # Grain runs along the plank.
    g = along * k * 3.0 + 0.35 * _noise(along * 6.0, across * 3.0, pid[0] + 31 * pid[1])
    grain = 0.5 + 0.5 * math.sin(g * 6.2831 + across * 18.0 + 4.0 * _noise(along * 2.0, across * 9.0, pid[2] + 5))
    col = _mix(base, (130, 82, 42), 0.22 * grain)
    edge = min(across, 1 - across, along * k, (1 - along) * k)
    if edge < 0.035:
        col = _mix(col, (70, 42, 22), 0.8)
    return col


PATTERNS = {"checker": checker, "herringbone": herringbone}
