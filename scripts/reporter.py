# reporter.py —— 检测结果上报模块（Day 11）
# 由 real_time_yolo.sh 内嵌的 Python 调用：
#   import reporter; reporter.start()
#   reporter.report(results[0], model.names)
# 职责：过滤（ROI/最小尺寸）→ 简易跨帧去重 → 队列 → 每 N 秒批量 POST 后端
import os
import queue
import math
import time
import itertools
import threading
import datetime
import requests

API_URL = os.environ.get("API_URL", "http://192.168.31.95:5027/api/detections")
API_KEY = os.environ.get("API_KEY", "dev-key-001")
DEVICE = os.environ.get("DEVICE_CODE", "Jetson-01")
REPORT_SEC = float(os.environ.get("REPORT_SEC", "2"))     # 每 2 秒发一批
DEDUP_SEC = float(os.environ.get("DEDUP_SEC", "5"))       # 同一人 5 秒只报一次
MIN_FACE_RATIO = float(os.environ.get("MIN_FACE_RATIO", "0.06"))
ROI = (0.05, 0.05, 0.95, 0.95)                            # 只判画面中间区域

_queue = queue.Queue(maxsize=500)
_tracks = {}                                              # 合成 id -> 位置/时间
_next_id = itertools.count(1)
_SESSION = str(int(time.time()))                          # 会话前缀：重启后 id 不重号
_started = False
_stats = {"sent": 0, "failed": 0, "dropped": 0}


def _utc_now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def _filtered_boxes(result, names):
    """ROI + 最小尺寸过滤，返回原始框信息。"""
    h, w = result.orig_shape
    out = []
    for b in result.boxes:
        cls = names[int(b.cls)]
        conf = float(b.conf)
        x1, y1, x2, y2 = [float(v) for v in b.xyxy[0]]
        if (x2 - x1) < w * MIN_FACE_RATIO:
            continue
        cx, cy = (x1 + x2) / 2, (y1 + y2) / 2
        rx1, ry1, rx2, ry2 = ROI
        if not (rx1 * w <= cx <= rx2 * w and ry1 * h <= cy <= ry2 * h):
            continue
        out.append({"cls": cls, "conf": conf, "x1": x1, "y1": y1,
                    "x2": x2, "y2": y2, "cx": cx, "cy": cy, "w": w})
    return out


def _track_id(p):
    """用框中心距离做简易跟踪：固定机位下够用，免去引入 tracking。"""
    now = time.time()
    best_id, best_d = None, 1e9
    for tid, t in list(_tracks.items()):
        if now - t["seen"] > 3:
            continue
        d = math.hypot(p["cx"] - t["cx"], p["cy"] - t["cy"])
        if d < best_d and d < p["w"] * 0.08:
            best_d, best_id = d, tid
    if best_id is None:
        best_id = next(_next_id)
        _tracks[best_id] = {"cx": p["cx"], "cy": p["cy"], "seen": now, "sent": False}
    t = _tracks[best_id]
    t["cx"], t["cy"], t["seen"] = p["cx"], p["cy"], now
    return best_id, t


def report(result, names):
    """每帧调用：过滤、去重、入队。异常一律吞掉，不能影响主流程。"""
    now = time.time()
    for p in _filtered_boxes(result, names):
        tid, t = _track_id(p)
        if t.get("sent"):
            continue
        t["sent"] = True
        mask = round(p["conf"], 3) if p["cls"] == "with_mask" else None
        person = {
            "trackId": f"p-{_SESSION}-{tid}",
            "bbox": [round(p["x1"]), round(p["y1"]),
                     round(p["x2"] - p["x1"]), round(p["y2"] - p["y1"])],
            "attributes": {"mask": mask},
            "compliant": mask is not None,
            "violations": [] if mask is not None else ["no_mask"],
            "confidence": round(p["conf"], 3),
            "imageUrl": None,
        }
        try:
            _queue.put_nowait(person)
        except queue.Full:
            _stats["dropped"] += 1


def _worker():
    while True:
        time.sleep(REPORT_SEC)
        batch = []
        while len(batch) < 50:
            try:
                batch.append(_queue.get_nowait())
            except queue.Empty:
                break
        if not batch:
            continue
        payload = {"deviceCode": DEVICE, "timestamp": _utc_now(), "persons": batch}
        try:
            r = requests.post(API_URL, json=payload,
                              headers={"X-Api-Key": API_KEY}, timeout=3)
            if r.status_code >= 300:
                raise RuntimeError(f"HTTP {r.status_code}")
            _stats["sent"] += len(batch)
            print(f"[REPORT] sent {len(batch)} -> {r.text.strip()}")
        except Exception as e:
            # 断网/后端没开：这批直接丢，管线不能崩
            _stats["failed"] += len(batch)
            print(f"[REPORT] failed {len(batch)}: {e}")


def start():
    global _started
    if _started:
        return
    _started = True
    threading.Thread(target=_worker, daemon=True).start()
    print(f"[REPORT] enabled -> {API_URL} device={DEVICE} batch={REPORT_SEC}s")
