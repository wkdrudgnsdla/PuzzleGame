"""Rasterize the Cube Mascot's flat sprite frames. Requires Pillow."""

from math import cos, pi, sin
from pathlib import Path

from PIL import Image, ImageDraw


TILE = 96
SCALE = 4
APRICOT = "#FFC38A"
MINT = "#54BFA7"
INK = "#4B2B20"
REAR_FOOT = "#694134"


def coords(*values):
    return tuple(round(value * SCALE) for value in values)


def frame(pose, index, profile=False):
    image = Image.new("RGBA", (TILE * SCALE, TILE * SCALE))
    draw = ImageDraw.Draw(image)
    overhead = pose.startswith("top_")
    walking = pose in ("walk", "top_walk")
    stride = sin(index * pi / 3) if walking else 0
    step = cos(index * pi / 3) if walking else 0

    width = 68
    height = 68
    base = 73
    if walking:
        base -= abs(step) * 2
    elif pose == "idle":
        height += (0, 1, -1, 0)[index]
    elif pose == "jump":
        width, height, base = ((63, 75, 77), (64, 73, 75))[index]
    elif pose == "fall":
        width, height, base = ((66, 71, 73), (68, 69, 73))[index]
    elif pose == "land":
        width, height = ((76, 58), (72, 63), (68, 68))[index]

    if overhead:
        width = height = (59, 60, 58, 59)[index] if not walking else 59
        base = 48 + height / 2
        for x, reach, color in ((32, stride, REAR_FOOT), (62, -stride, INK)):
            y = 19 - reach * 6
            draw.rounded_rectangle(coords(x - 7, y - 7, x + 7, y + 7), 6 * SCALE, fill=color)
    else:
        foot_centers = (31, 65) if not profile else (38, 59)
        for i, (center, color) in enumerate(zip(foot_centers, (REAR_FOOT, INK))):
            phase = 1 if i == 0 else -1
            center += stride * phase * 12
            lift = max(0, phase * step) * 8
            if pose == "jump":
                lift += (i + index) % 2 * 4
            draw.rounded_rectangle(coords(center - 9, base - 5 - lift, center + 9,
                                          min(86, base + 13 - lift)), 7 * SCALE, fill=color)

    x0, y0 = 48 - width / 2, base - height
    x1 = x0 + width
    body = Image.new("L", image.size)
    ImageDraw.Draw(body).rounded_rectangle(coords(x0, y0, x1, base), 11 * SCALE, fill=255)
    image.paste(APRICOT, mask=body)
    # Clip the mint triangle to the same rounded outline as the body.
    corner = Image.new("RGBA", image.size)
    ImageDraw.Draw(corner).polygon([coords(x1 - 25, y0), coords(x1, y0), coords(x1, y0 + 25)], fill=MINT)
    corner.putalpha(Image.composite(corner.getchannel("A"), Image.new("L", image.size), body))
    image.alpha_composite(corner)

    if not overhead:
        eyes = (x1 - 12,) if profile else (33, 63)
        eye_y = y0 + height * 0.61
        blink = pose == "idle" and index == 3
        for x in eyes:
            if blink:
                draw.rounded_rectangle(coords(x - 4, eye_y, x + 4, eye_y + 2), SCALE, fill=INK)
            else:
                draw.ellipse(coords(x - 3.5, eye_y - 6.5, x + 3.5, eye_y + 6.5), fill=INK)
        mouth_x = x1 - 7.5 if profile else 48
        mouth_y = eye_y + 8
        if pose == "fall":
            draw.ellipse(coords(mouth_x - 2, mouth_y - 1, mouth_x + 2, mouth_y + 3), fill=INK)
        else:
            draw.arc(coords(mouth_x - 4, mouth_y - 3, mouth_x + 4, mouth_y + 4), 5, 175,
                     fill=INK, width=2 * SCALE)

    return image.resize((TILE, TILE), Image.Resampling.LANCZOS)


def main():
    rows = (("idle", 4, False), ("idle", 4, True), ("walk", 6, True),
            ("jump", 2, True), ("fall", 2, True), ("land", 3, True),
            ("top_idle", 4, False), ("top_walk", 6, False),
            ("jump", 2, False), ("fall", 2, False), ("land", 3, False))
    atlas = Image.new("RGBA", (6 * TILE, len(rows) * TILE))
    for row, (pose, count, profile) in enumerate(rows):
        for column in range(count):
            atlas.paste(frame(pose, column, profile), (column * TILE, row * TILE))

    destination = Path(__file__).resolve().parents[1] / "My project/Assets/_Game/Resources/CubeMascot/Sprites.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(destination)


if __name__ == "__main__":
    main()
