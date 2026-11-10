# scripts/judge.py —— Day 9：从模型输出到后端 persons
# 用法：
#   py scripts\judge.py models\best.pt samples_real 0.25 640      # 图片模式
#   py scripts\judge.py models\best.pt samples_source\walk.mp4 0.25 640   # 视频模式
import sys, json, time
from pathlib import Path
from collections import deque, defaultdict
import cv2
from ultralytics import YOLO

WEIGHTS = sys.argv[1] if len(sys.argv) > 1 else "models/best.pt"
SOURCE  = sys.argv[2] if len(sys.argv) > 2 else "samples_real"
CONF    = float(sys.argv[3]) if len(sys.argv) > 3 else 0.25
IMGSZ   = int(sys.argv[4]) if len(sys.argv) > 4 else 640

CONFIRM_FRAMES = 3        # 连续几帧一致才采信（= 规则表 ConfirmFrames）
DEDUP_SECONDS  = 5.0      # 同一 trackId 5 秒内不重复上报
MIN_FACE_RATIO = 0.06     # 人脸框宽 < 画面宽 6% 就丢弃（太远）
ROI = (0.05, 0.05, 0.95, 0.95)   # 归一化区域 x1,y1,x2,y2：只判中间这块
TRACKER = "configs/bytetrack_mask.yaml"   # 低阈值跟踪参数，避免丢掉低置信度的违规框

model = YOLO(WEIGHTS)
CLASSES = model.names
is_video = Path(SOURCE).is_file()

# 视频用真实帧率算“视频时间”，否则离线跑完 5 秒窗口就失效了
fps = None
if is_video:
    cap = cv2.VideoCapture(SOURCE)
    fps = cap.get(cv2.CAP_PROP_FPS) or 25
    cap.release()

history = defaultdict(lambda: deque(maxlen=CONFIRM_FRAMES))   # trackId -> 最近几帧“戴没戴”
last_report = {}                                             # trackId -> 上次上报的视频时间

def pass_roi(x1, y1, x2, y2, w, h):
    """太小或不在 ROI 内都丢掉。"""
    if (x2 - x1) < w * MIN_FACE_RATIO:
        return False
    cx, cy = (x1 + x2) / 2, (y1 + y2) / 2
    rx1, ry1, rx2, ry2 = ROI
    return rx1 * w <= cx <= rx2 * w and ry1 * h <= cy <= ry2 * h

def build_persons(boxes, w, h, frame_idx, now):
    persons = []
    for k, b in enumerate(boxes):
        cls_name = CLASSES[int(b.cls)]
        conf = float(b.conf)
        x1, y1, x2, y2 = [float(v) for v in b.xyxy[0]]

        if not pass_roi(x1, y1, x2, y2, w, h):      # ① 区域/尺寸过滤
            continue

        # ② 类别映射：只有 with_mask 才算“戴了”
        mask_conf = round(conf, 3) if cls_name == "with_mask" else None

        tid = int(b.id.item()) if b.id is not None else None

        # 视频模式必须有跟踪 ID：没有 ID 就没法去重，逐帧输出会刷屏（实测 10 秒刷出 17 条）。
        # 图片模式没有跟踪器，用「帧号-框序号」区分即可。
        if is_video and tid is None:
            continue

        # ③ 多帧确认（图片模式没有 id，跳过）
        if tid is not None:
            history[tid].append(mask_conf is not None)
            if len(history[tid]) < CONFIRM_FRAMES:
                continue                             # 帧数不够，先攒着
            if len(set(history[tid])) > 1:
                continue                             # 最近 N 帧结果不一致，不稳，跳过

        # ④ 去重：同一人 5 秒（视频时间）内只报一次
        if tid is not None:
            if now - last_report.get(tid, -999) < DEDUP_SECONDS:
                continue
            last_report[tid] = now

        # 图片模式没有跟踪 ID，用「帧号-框序号」保证同一张图里的多人 ID 不重复
        track_id = f"p-{tid}" if tid is not None else f"img-{frame_idx}-{k}"

        persons.append({
            "trackId": track_id,
            "bbox": [round(x1), round(y1), round(x2 - x1), round(y2 - y1)],  # 后端要 xywh
            "attributes": {"mask": mask_conf},
            "compliant": mask_conf is not None,
            "violations": [] if mask_conf is not None else ["no_mask"],
            "confidence": round(conf, 3),
            "imageUrl": None,
        })
    return persons

stream = (model.track(source=SOURCE, conf=CONF, imgsz=IMGSZ, persist=True,
                      tracker=TRACKER, stream=True, verbose=False)
          if is_video else
          model.predict(source=SOURCE, conf=CONF, imgsz=IMGSZ, stream=True, verbose=False))

total = 0
for i, r in enumerate(stream):
    h, w = r.orig_shape
    now = (i / fps) if fps else time.time()
    persons = build_persons(r.boxes, w, h, i, now)
    for p in persons:
        print(json.dumps(p, ensure_ascii=False))
    total += len(persons)

print(f"\n共输出 {total} 条 person")