# 用法：py scripts\check_model.py models\best.pt samples 0.25 320
import sys
from ultralytics import YOLO
from pathlib import Path

WEIGHTS = sys.argv[1] if len(sys.argv) > 1 else "models/best.pt"
SOURCE  = sys.argv[2] if len(sys.argv) > 2 else "samples"
CONF    = float(sys.argv[3]) if len(sys.argv) > 3 else 0.25
IMGSZ   = int(sys.argv[4]) if len(sys.argv) > 4 else 320
ROOT = Path(__file__).resolve().parents[1]

model = YOLO(WEIGHTS)
print("classes:", model.names)

results = model.predict(source=SOURCE, conf=CONF, imgsz=IMGSZ,
                        save=True,
                        project=str(ROOT / "runs"),   # 固定输出到项目里的 runs
                        name=f"check_{IMGSZ}",        # 320 / 640 各一个目录，不覆盖
                        exist_ok=True)

for r in results:
    ms = sum(r.speed.values())
    print(f"\n{r.path}  {ms:.0f} ms")
    for b in r.boxes:
        name = model.names[int(b.cls)]
        conf = float(b.conf)
        x1, y1, x2, y2 = [float(v) for v in b.xyxy[0]]
        print(f"  {name:<24} conf={conf:.3f} bbox=[{x1:.0f},{y1:.0f},{x2-x1:.0f},{y2-y1:.0f}]")