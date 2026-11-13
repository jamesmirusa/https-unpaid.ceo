"""Slice two 4x4 RGBA icon sheets into named, square, padded PNG icons + a contact sheet."""
import sys
from pathlib import Path
from PIL import Image

NAMES = {
    "A": ["money", "energy", "close_ones", "knowledge",
          "reputation", "trust", "trust_cold", "action_token",
          "rent", "food", "loan", "salary",
          "reserve", "lesson", "money_date", "profit"],
    "B": ["tone_friendly", "tone_business", "tone_joke", "tone_firm",
          "emote_scene", "emote_work", "emote_lesson", "emote_level",
          "mood_slept", "mood_loan_shark", "mood_burnout", "mood_coffee",
          "chapter_trophy", "place_shop", "level_bonus", "advice"],
}
OUT = Path(sys.argv[3])
SIZE = 512
PAD = 0.06


def slice_sheet(path: str, key: str) -> list[Path]:
    sheet = Image.open(path).convert("RGBA")
    w, h = sheet.size
    cw, ch = w // 4, h // 4
    out = []
    for i, name in enumerate(NAMES[key]):
        r, c = divmod(i, 4)
        cell = sheet.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch))
        alpha = cell.getchannel("A").point(lambda v: 255 if v > 12 else 0)
        box = alpha.getbbox()
        if box is None:
            raise SystemExit(f"empty cell {key}{i} {name}")
        icon = cell.crop(box)
        side = max(icon.size)
        canvas_side = int(side / (1 - 2 * PAD))
        canvas = Image.new("RGBA", (canvas_side, canvas_side), (0, 0, 0, 0))
        canvas.paste(icon, ((canvas_side - icon.width) // 2, (canvas_side - icon.height) // 2), icon)
        canvas = canvas.resize((SIZE, SIZE), Image.LANCZOS)
        p = OUT / f"icon_{name}.png"
        canvas.save(p, optimize=True)
        out.append(p)
    return out


def contact(paths: list[Path], dest: Path) -> None:
    cols, cell = 8, 160
    rows = (len(paths) + cols - 1) // cols
    bg = Image.new("RGBA", (cols * cell, rows * cell), (27, 42, 74, 255))
    for i, p in enumerate(paths):
        im = Image.open(p).resize((cell - 16, cell - 16), Image.LANCZOS)
        r, c = divmod(i, cols)
        tile = Image.new("RGBA", (cell - 8, cell - 8), (255, 248, 236, 255) if (r + c) % 2 == 0 else (227, 220, 207, 255))
        bg.paste(tile, (c * cell + 4, r * cell + 4))
        bg.paste(im, (c * cell + 8, r * cell + 8), im)
    bg.convert("RGB").save(dest, quality=90)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    paths = slice_sheet(sys.argv[1], "A") + slice_sheet(sys.argv[2], "B")
    contact(paths, OUT / "contact_sheet.jpg")
    print(len(paths), "icons")
