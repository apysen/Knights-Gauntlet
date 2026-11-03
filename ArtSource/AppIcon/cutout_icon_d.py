"""Cuts the knight-and-gauntlet out of gauntlet_d.png onto transparency, dropping the
black backdrop, the gold ring and the two sparkle stars.

Usage: python cutout_icon_d.py gauntlet_d.png gauntlet_d_cutout.png

The art's outer outline is the same near-black as the backdrop, so it can't be keyed.
Instead the coloured fills are isolated, the ring is fitted and erased where it is a
thin line with dark on both sides (so subject pixels that sit on the circle survive),
the stars fall away as separate islands, and a matching dark outline is redrawn
around the silhouette with an antialiased edge.
"""
import sys

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

OUTLINE = 7.0          # px, matches the art's inner line weight at 1254 px
OUTLINE_RGB = (14, 13, 14)


def fit_circle(xs, ys):
    A = np.c_[2 * xs, 2 * ys, np.ones_like(xs)]
    b = xs ** 2 + ys ** 2
    cx, cy, c = np.linalg.lstsq(A, b, rcond=None)[0]
    return cx, cy, np.sqrt(c + cx ** 2 + cy ** 2)


def main():
    src, dst = sys.argv[1], sys.argv[2]
    rgb = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
    h, w, _ = rgb.shape
    colored = rgb.max(2) > 60
    gold = colored & (rgb[..., 0] > 150) & (rgb[..., 2] < 140)

    # Fit the ring (an axis-aligned ellipse; it is not quite round) from gold pixels
    # well clear of the knight: the side bands minus the stars, the lower arcs outside
    # the gauntlet, and the top away from the ears and mane.
    yy, xx = np.mgrid[0:h, 0:w]
    side = (xx < 330) | (xx > 930)
    stars = side & (yy > 480) & (yy < 680)
    top = (yy < 140) & ((xx < 530) | (xx > 760))
    seed = gold & ((side & ~stars) | top)

    def fit_ellipse(m):
        x, y = xx[m].astype(float), yy[m].astype(float)
        A = np.c_[x * x, y * y, x, y]
        p, q, s, t = np.linalg.lstsq(A, np.ones_like(x), rcond=None)[0]
        cx, cy = -s / (2 * p), -t / (2 * q)
        k = 1 + p * cx * cx + q * cy * cy
        return cx, cy, np.sqrt(k / p), np.sqrt(k / q)

    cx, cy, ax, ay = fit_ellipse(seed)
    for _ in range(3):
        rn = np.hypot((xx - cx) / ax, (yy - cy) / ay)
        band = np.abs(rn - 1) * (ax + ay) / 2
        cx, cy, ax, ay = fit_ellipse(seed & (band < 14))
    rn = np.hypot((xx - cx) / ax, (yy - cy) / ay)
    band = np.abs(rn - 1) * (ax + ay) / 2
    ring_w = np.percentile(band[seed & (band < 14)], 97)
    r = (ax + ay) / 2
    d = np.hypot(xx - cx, yy - cy)

    # Ring pixel = coloured, on the ellipse, and dark at a radial step either side.
    ux, uy = (xx - cx) / np.maximum(d, 1), (yy - cy) / np.maximum(d, 1)
    step = ring_w + 10
    dark = ~ndi.binary_dilation(colored, iterations=1)

    def dark_at(sign):
        sx = np.clip(np.rint(xx + sign * ux * step).astype(int), 0, w - 1)
        sy = np.clip(np.rint(yy + sign * uy * step).astype(int), 0, h - 1)
        return dark[sy, sx]

    ring = colored & (band <= ring_w + 8) & dark_at(+1) & dark_at(-1)
    ring = ndi.binary_dilation(ring, iterations=3) & colored & (band <= ring_w + 10)
    fills = colored & ~ring

    # Every coloured island is the subject except the two sparkle stars (and any ring
    # crumbs), which sit off to the sides of the knight.
    lab, n = ndi.label(fills)
    idx = np.arange(1, n + 1)
    area = ndi.sum(fills, lab, idx)
    cxs = np.array([c[1] for c in ndi.center_of_mass(fills, lab, idx)])
    keep_ids = idx[(area >= 30) & (cxs > 330) & (cxs < 930)]
    subject = np.isin(lab, keep_ids)
    # Close the interior line work with a round brush (it is part of the art, not holes).
    R = 16
    dil = ndi.distance_transform_edt(~subject) <= R
    solid = ndi.distance_transform_edt(np.pad(dil, R + 2, constant_values=True))[R + 2:-R - 2, R + 2:-R - 2] > R
    solid = ndi.binary_fill_holes(solid | subject)

    # The cuff runs into the frame at the bottom: find that cut so no outline is drawn on it.
    rows = np.where(solid.any(1))[0]
    cut = rows.max()

    # Antialiased silhouette with a redrawn dark outline.
    dist = ndi.distance_transform_edt(~solid)
    alpha = np.clip(OUTLINE + 0.5 - dist, 0, 1)
    alpha[cut + 1:, :] = 0
    out = np.zeros((h, w, 4), np.float32)
    out[..., :3] = np.where(solid[..., None], rgb, np.array(OUTLINE_RGB, np.float32))
    out[..., 3] = alpha * 255
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA").save(dst)

    ys, xs = np.where(alpha > 0)
    print(f"ring centre ({cx:.1f},{cy:.1f}) axes={ax:.1f}x{ay:.1f} width~{ring_w:.1f}; groups={n}; "
          f"subject bbox x{xs.min()}-{xs.max()} y{ys.min()}-{ys.max()}; cut row {cut}")


if __name__ == "__main__":
    main()
