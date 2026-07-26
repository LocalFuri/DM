import json
from pathlib import Path

from dmcsb import open_dungeon


LEVEL_INDEX = 0
DUNGEON_FILE = Path("DUNGEON.DAT")

RAW_TXT_OUTPUT = Path("HallOfChampions_Raw.txt")
VISUAL_TXT_OUTPUT = Path("HallOfChampions_Map.txt")
JSON_OUTPUT = Path("HallOfChampions.json")


def decode_tile(raw_value: int) -> tuple[str, str]:
    """
    Convert a raw Dungeon Master tile byte into:

    - a one-character symbol for the text map
    - a readable tile type name

    The upper three bits contain the basic tile category.
    The lower bits contain additional tile state and flags.
    """

    tile_group = raw_value & 0xE0

    if tile_group == 0x00:
        return "#", "Wall"

    if tile_group == 0x20:
        return ".", "Floor"

    if tile_group == 0x40:
        return "P", "Pit"

    if tile_group == 0x60:
        return "S", "Stairs"

    if tile_group == 0x80:
        return "D", "Door"

    if tile_group == 0xA0:
        return "T", "Teleporter"

    if tile_group == 0xC0:
        return "F", "FalseWall"

    if tile_group == 0xE0:
        return "?", "Special"

    return "?", "Unknown"


def decode_facing(direction: int) -> str:
    if direction == 0:
        return "North"
    if direction == 1:
        return "East"
    if direction == 2:
        return "South"
    if direction == 3:
        return "West"
    return "Unknown"


def decode_initial_party_location(location: int) -> dict[str, object]:
    x = location & 0x1F
    y = (location >> 5) & 0x1F
    facing = decode_facing((location >> 10) & 0x3)

    return {
        "x": x,
        "y": y,
        "facing": facing,
    }


def format_path(path: Path) -> str:
    return path.resolve().as_posix()


def main() -> None:
    if not DUNGEON_FILE.exists():
        raise FileNotFoundError(
            "Could not find the dungeon file "
            f"{format_path(DUNGEON_FILE)}"
        )

    dungeon = open_dungeon(DUNGEON_FILE)
    grid = dungeon.tile_grid(LEVEL_INDEX)

    numeric_grid = [
        [int(tile) for tile in row]
        for row in grid
    ]

    height = len(numeric_grid)
    width = len(numeric_grid[0]) if height > 0 else 0

    if width == 0 or height == 0:
        raise ValueError("The extracted dungeon map is empty.")

    if any(len(row) != width for row in numeric_grid):
        raise ValueError(
            "The extracted dungeon map contains rows with different widths."
        )

    player_start = decode_initial_party_location(
        dungeon.hdr["InitialPartyLocation"]
    )

    write_raw_text_file(
        grid=numeric_grid,
        width=width,
        height=height,
    )

    write_visual_text_file(
        grid=numeric_grid,
        width=width,
        height=height,
    )

    write_json_file(
        grid=numeric_grid,
        width=width,
        height=height,
        player_start=player_start,
    )

    print("Export completed.")
    print(f"Level = {LEVEL_INDEX}")
    print(f"Size = {width} x {height}")
    print(
        "PlayerStart = "
        f"({player_start['x']}, {player_start['y']}) "
        f"facing {player_start['facing']}"
    )
    print(f"Raw TXT = {format_path(RAW_TXT_OUTPUT)}")
    print(f"Visual TXT = {format_path(VISUAL_TXT_OUTPUT)}")
    print(f"JSON = {format_path(JSON_OUTPUT)}")

def write_raw_text_file(
    grid: list[list[int]],
    width: int,
    height: int,
) -> None:
    lines = [
        "Dungeon Master - Hall of Champions",
        f"Level: {LEVEL_INDEX}",
        f"Width: {width}",
        f"Height: {height}",
        "",
        "Raw tile values in hexadecimal:",
        "",
        "    " + " ".join(
            f"{x:02}"
            for x in range(width)
        ),
    ]

    for y, row in enumerate(grid):
        tile_values = " ".join(
            f"{tile:02X}"
            for tile in row
        )

        lines.append(
            f"{y:02}: {tile_values}"
        )

    RAW_TXT_OUTPUT.write_text(
        "\n".join(lines) + "\n",
        encoding="utf-8",
    )


def write_visual_text_file(
    grid: list[list[int]],
    width: int,
    height: int,
) -> None:
    lines = [
        "Dungeon Master - Hall of Champions",
        f"Level: {LEVEL_INDEX}",
        f"Width: {width}",
        f"Height: {height}",
        "",
        "Legend:",
        "# = Wall",
        ". = Floor",
        "P = Pit",
        "S = Stairs",
        "D = Door",
        "T = Teleporter",
        "F = False or illusionary wall",
        "? = Special or unknown",
        "",
        "    " + "".join(
            str(x % 10)
            for x in range(width)
        ),
    ]

    for y, row in enumerate(grid):
        symbols = "".join(
            decode_tile(tile)[0]
            for tile in row
        )

        lines.append(
            f"{y:02}: {symbols}"
        )

    VISUAL_TXT_OUTPUT.write_text(
        "\n".join(lines) + "\n",
        encoding="utf-8",
    )


def write_json_file(
    grid: list[list[int]],
    width: int,
    height: int,
    player_start: dict[str, object],
) -> None:
    decoded_rows: list[list[dict[str, object]]] = []

    for y, row in enumerate(grid):
        decoded_row: list[dict[str, object]] = []

        for x, raw_value in enumerate(row):
            symbol, tile_type = decode_tile(raw_value)

            decoded_row.append(
                {
                    "x": x,
                    "y": y,
                    "raw": raw_value,
                    "hex": f"{raw_value:02X}",
                    "type": tile_type,
                    "symbol": symbol,
                }
            )

        decoded_rows.append(decoded_row)

    output = {
        "name": "Hall of Champions",
        "level": LEVEL_INDEX,
        "width": width,
        "height": height,
        "coordinateSystem": {
            "origin": "top-left",
            "xDirection": "right",
            "yDirection": "down",
        },
        "playerStart": player_start,
        "legend": {
            "#": "Wall",
            ".": "Floor",
            "P": "Pit",
            "S": "Stairs",
            "D": "Door",
            "T": "Teleporter",
            "F": "FalseWall",
            "?": "Special",
        },
        "tiles": decoded_rows,
    }

    JSON_OUTPUT.write_text(
        json.dumps(
            output,
            indent=2,
        ),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()