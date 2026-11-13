"""Cut a white-background icon sheet to RGBA without halos.

Background = near-white pixels connected to each cell border. Inside that region, colour is
"unblended" from white (GIMP colour-to-alpha), so anti-aliased outlines and soft glows keep
their real colour with partial alpha. Everything enclosed by the dark outlines stays opaque.
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

T = 96          # how far from white a pixel may be and still belong to the background region
MIN_A = 0.035   # alpha below this is dropped


def cut_cell(rgb: np.ndarray) -> np.ndarray:
    f = rgb.astype(np.float32)
    dark = 255.0 - f.min(axis=2)                     # 0 = white
    cand = dark < T
    lab, _ = ndimage.label(cand)
    border = np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))
    border = border[border != 0]
    bg = np.isin(lab, border)

    alpha = np.ones(dark.shape, np.float32)
    a = (255.0 - f).max(axis=2) / 255.0              # colour-to-alpha against white
    alpha[bg] = a[bg]
    alpha[alpha < MIN_A] = 0.0

    out = f.copy()
    safe = np.where(alpha > 0, alpha, 1.0)[..., None]
    unblended = 255.0 - (255.0 - f) / safe
    out[bg] = np.clip(unblended[bg], 0, 255)
    rgba = np.dstack([out, alpha * 255.0]).round().astype(np.uint8)
    return rgba


def main(src: str, dst: str) -> None:
    im = np.array(Image.open(src).convert("RGB"))
    h, w, _ = im.shape
    ch, cw = h // 4, w // 4
    res = np.zeros((h, w, 4), np.uint8)
    for r in range(4):
        for c in range(4):
            sl = (slice(r * ch, (r + 1) * ch), slice(c * cw, (c + 1) * cw))
            res[sl] = cut_cell(im[sl])
    Image.fromarray(res, "RGBA").save(dst)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
