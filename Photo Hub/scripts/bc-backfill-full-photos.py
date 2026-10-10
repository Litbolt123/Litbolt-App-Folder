# One-off: copy full originals for BC's six face_*.jpg thumbnails (faces_meta + audit/inv.json).
import json
import os
import shutil
from datetime import datetime

FOLD = r"C:\Users\Augus\Pictures\People\_fold-log.txt"
DST_DIR = r"C:\Users\Augus\Pictures\People\_face-groups\BC"
META = r"C:\Users\Augus\Pictures\_sort-tools\faces_meta.json"
TAG = "photo-hub-bc-backfill"

# Group 05 - BC folder crops -> old face indices (audit/inv.json)
MAPPING = {
    "face_1.jpg": 1063,
    "face_2.jpg": 1065,
    "face_3.jpg": 1067,
    "face_4.jpg": 1055,
    "face_5.jpg": 1045,
    "face_6.jpg": 1048,
}


def main():
    meta = json.load(open(META, encoding="utf-8"))
    now = datetime.now()
    lines = [f"\n=== BC face crop full-photo backfill {now:%Y-%m-%d %H:%M:%S} ===\n"]
    for crop, idx in MAPPING.items():
        src = meta[idx]["people"]
        if not os.path.isfile(src):
            lines.append(
                f"{now:%Y-%m-%dT%H:%M:%S} ERROR\tmissing source {src}\t(crop {crop} idx {idx})\n"
            )
            continue
        dst = os.path.join(DST_DIR, os.path.basename(src))
        if os.path.exists(dst):
            lines.append(f"{now:%Y-%m-%dT%H:%M:%S} SKIP\t{src}\t->\t{dst}\t(already present)\n")
            continue
        shutil.copy2(src, dst)
        note = f"{TAG}; crop {crop} faces_meta[{idx}]"
        lines.append(f"{now:%Y-%m-%dT%H:%M:%S} COPY\t{src}\t->\t{dst}\t({note})\n")
    with open(FOLD, "a", encoding="utf-8") as f:
        f.writelines(lines)
    print("Done. Logged", len(lines) - 1, "lines to", FOLD)


if __name__ == "__main__":
    main()
