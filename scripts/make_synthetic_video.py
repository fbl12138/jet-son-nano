# scripts/make_synthetic_video.py
# 用途：没有真实录像时，用数据集图片合成一段"静止 + 轻微平移"的视频，
#       专门用来验证「多帧确认」和「5 秒去重」这两个时序逻辑。
# 注意：它不是真实场景，别用它评估精度，只用来验证状态机。
import cv2
import numpy as np
from pathlib import Path
from ultralytics import YOLO

model = YOLO("models/best.pt")
names = model.names
OUT_DIR = Path("samples_source")


def crop_target(img_path, target_cls, margin=0.6):
    """在图片里找目标类别置信度最高的框，按 margin 外扩后裁出来。"""
    r = model.predict(source=str(img_path), conf=0.25, imgsz=640, verbose=False)[0]
    boxes = [b for b in r.boxes if names[int(b.cls)] == target_cls]
    if not boxes:
        raise SystemExit(f"{img_path} 里没有 {target_cls}")
    b = max(boxes, key=lambda x: float(x.conf))
    x1, y1, x2, y2 = [float(v) for v in b.xyxy[0]]
    w, h = x2 - x1, y2 - y1
    cx, cy = (x1 + x2) / 2, (y1 + y2) / 2
    hw, hh = w * (0.5 + margin), h * (0.5 + margin)
    img = cv2.imread(str(img_path))
    H, W = img.shape[:2]
    X1, Y1 = max(0, int(cx - hw)), max(0, int(cy - hh))
    X2, Y2 = min(W, int(cx + hw)), min(H, int(cy + hh))
    return img[Y1:Y2, X1:X2]


def make_video(crop, out_path, seconds=10, fps=24, size=(640, 480)):
    """把裁好的图片重复成视频，并加一点左右平移，让 ByteTrack 有运动可跟。"""
    crop = cv2.resize(crop, size)
    vw = cv2.VideoWriter(str(out_path), cv2.VideoWriter_fourcc(*"mp4v"), fps, size)
    for i in range(seconds * fps):
        dx = int(8 * np.sin(2 * np.pi * i / 60))
        M = np.float32([[1, 0, dx], [0, 1, 0]])
        frame = cv2.warpAffine(crop, M, size, borderMode=cv2.BORDER_REPLICATE)
        vw.write(frame)
    vw.release()
    print("written:", out_path)


def main():
    OUT_DIR.mkdir(exist_ok=True)
    mask = crop_target("samples_check/maksssksksss28.png", "with_mask")
    nomask = crop_target("samples_check/maksssksksss621.png", "without_mask")
    make_video(mask, OUT_DIR / "mask_synth.mp4")
    make_video(nomask, OUT_DIR / "nomask_synth.mp4")


if __name__ == "__main__":
    main()
