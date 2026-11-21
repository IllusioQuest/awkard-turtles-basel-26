"""Persist a uniform desktop background before PCManFM starts; avoid resized SVG wallpaper artifacts."""
import configparser
import os
from pathlib import Path
import subprocess
import sys

CONFIG_DIRECTORY = Path.home() / ".config/pcmanfm-qt/lxqt"
CONFIG_PATH = CONFIG_DIRECTORY / "settings.conf"
DEFAULT_CONFIG_PATH = Path("/etc/xdg/pcmanfm-qt/lxqt/settings.conf")
BACKGROUND_COLOR = "#181320"  # Lab desktop theme; no image scaling/cache is involved.

settings = configparser.RawConfigParser(strict=False)
settings.optionxform = str
settings.read([DEFAULT_CONFIG_PATH, CONFIG_PATH])
if not settings.has_section("Desktop"):
    settings.add_section("Desktop")
for key, value in {"WallpaperMode": "color", "Wallpaper": "", "PerScreenWallpaper": "false", "BgColor": BACKGROUND_COLOR}.items():
    settings.set("Desktop", key, value)
CONFIG_DIRECTORY.mkdir(parents=True, exist_ok=True)
temporary = CONFIG_PATH.with_suffix(".new")
with temporary.open("w") as output:
    settings.write(output, space_around_delimiters=False)
temporary.replace(CONFIG_PATH)

if sys.argv[1:] == ["--desktop"]:
    os.execvp("pcmanfm-qt", ["pcmanfm-qt", "--desktop", "--profile=lxqt", "--wallpaper-mode=color"])
elif sys.argv[1:] == ["--repair"]:
    subprocess.run(["pcmanfm-qt", "--profile=lxqt", "--wallpaper-mode=color"], check=True)
else:
    raise SystemExit("Choose --desktop or --repair")
