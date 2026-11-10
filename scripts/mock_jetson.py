# ========== 1. 导入区：先告诉 Python 要用哪些工具 ==========
import datetime      # Python 自带，用来生成带时区的时间戳
import random        # Python 自带，用来随机决定戴不戴口罩、随机生成坐标和置信度
import time          # Python 自带，time.sleep() 用来"每隔 5 秒"发一次
import threading
import requests      # 第三方库（唯一要 pip 装的），负责发 HTTP 请求


# ========== 2. 常量区：改这里就能换目标 ==========
URL = "http://localhost:5027/api/detections"   # 后端上报接口地址（本地调试）
API_KEY = "dev-key-001"                        # 设备密钥，必须和库里 Devices.ApiKey 完全一致（区分大小写）
DEVICE = "jetson-01"                           # 设备编号，后面会写进每一条上报
HEARTBEAT_URL = "http://localhost:5027/api/devices/heartbeat"

# ========== 3. 构造"一个人"的结果，对应后端 PersonResultDto ==========
def build_person(n, has_mask):
    # n：第几次上报，用来生成 trackId，保证每次都是"新的人"（否则算同一个人，只会记一次违规）
    # has_mask：这次戴没戴，True 戴 / False 不戴
    return {
        # 跟踪编号。f-string 里 {n:04d} 表示把 n 补成 4 位，1 -> "0001"
        "trackId": f"p-{n:04d}",

        # 人体框 [x, y, w, h]，和 Jetson 上 YOLO 输出一致
        # 注意字段名是 bbox（小写），因为你给 PersonResultDto 加了 [JsonPropertyName("bbox")]
        "bbox": [
            random.randint(50, 600),    # x：画面里随机位置
            random.randint(30, 200),    # y
            180,                        # w：宽固定，省得写随机
            380,                        # h：高固定
        ],

        # 属性表：戴口罩时给个 0.6~0.99 的置信度；不戴时给 None（对应 JSON 的 null）
        # 后端靠"mask 是不是 null"来判断有没有口罩
        "attributes": {"mask": round(random.uniform(0.6, 0.99), 2)} if has_mask else {"mask": None},

        # 合规判定结果：戴了就是合规
        "compliant": has_mask,

        # 违规类型列表：戴了给空列表，没戴给 ["no_mask"]
        # 后端逻辑是"你给了 violations 就用你的，没给才按规则兜底"
        "violations": [] if has_mask else ["no_mask"],

        # 置信度：随机 0.75~0.95，模拟真实模型输出的抖动
        "confidence": round(random.uniform(0.75, 0.95), 2),

        # 抓拍图路径，现在还没做（Day 16 才做），先给 null
        "imageUrl": None,
    }
def heartbeat_loop():
    """每 20 秒向后端报到一次，证明设备还活着"""
    while True:
        try:
            r = requests.post(HEARTBEAT_URL,
                              json={"deviceCode": DEVICE},
                              headers={"X-Api-Key": API_KEY},
                              timeout=3)
            print(f"{datetime.datetime.now():%H:%M:%S}  心跳 -> {r.status_code}")
        except requests.RequestException as e:
            print(f"心跳失败: {e}")
        time.sleep(20)

# ========== 4. 主循环：这就是"假装成 Jetson"的部分 ==========
def main():
    n = 0
     # 新增：启动心跳线程，daemon=True 让 Ctrl+C 时它也一起退出
    threading.Thread(target=heartbeat_loop, daemon=True).start()
                                      # 计数器：第几条上报，同时也是 trackId 的来源
    print(f"开始上报 -> {URL}（Ctrl+C 停止）")

    while True:                            # 无限循环，直到你按 Ctrl+C 或程序崩溃
        n += 1                             # 每轮 +1
        has_mask = random.random() > 0.4   # random.random() 给 0~1 的小数，>0.4 即约 60% 概率戴口罩

        # 把这一条组装成后端要求的完整请求体
        payload = {
            "deviceCode": DEVICE,          # 哪台设备
            # 时间戳：取 UTC 时间再转成 ISO8601 字符串，如 2026-10-28T06:30:05.123456+00:00
            # 用 UTC 是为了和库里的 CreatedAt = DateTime.UtcNow 保持一致，避免统计差 8 小时
            "timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            # persons 是数组，这里固定放 1 个人；要模拟多人就 append 多个 build_person()
            "persons": [build_person(n, has_mask)],
        }

        # try 包住网络请求：网络不通时不能让脚本整个挂掉
        try:
            # requests.post 发 POST 请求
            # json=payload 会自动做两件事：把 dict 转成 JSON 字符串 + 设 Content-Type: application/json
            # headers= 传自定义请求头，后端从 X-Api-Key 里取设备密钥
            # timeout=3 表示 3 秒没响应就抛异常，防止卡死在一次请求上
            r = requests.post(URL, json=payload, headers={"X-Api-Key": API_KEY}, timeout=3)

            # 打印一行结果，方便肉眼盯着
            # {datetime.datetime.now():%H:%M:%S} 是格式化时间，只打时:分:秒
            # r.status_code 是 HTTP 状态码（201 成功 / 401 未授权）
            # r.text 是响应体原文，比如 {"saved":1,"violationsCreated":1}
            print(f"{datetime.datetime.now():%H:%M:%S}  第{n}条  mask={has_mask}  ->  {r.status_code} {r.text}")

        # 捕获所有 requests 相关的网络异常（超时、连接拒绝、DNS 失败等）
        except requests.RequestException as e:
            # 打印原因后继续循环——真实 Jetson 断网时也是这个策略：本地缓存，恢复后重传
            print(f"上报失败（本地缓存稍后重试）: {e}")

        # 睡 5 秒再发下一条。注意它在 try 外面：
        # 就算这次请求失败，也照样等 5 秒，不会变成疯狂重试打爆后端
        time.sleep(5)


# ========== 5. 入口：直接运行这个文件时才启动 main() ==========
# 这行的作用是：只有 "python mock_jetson.py" 这样直接跑才执行；
# 如果哪天被别的脚本 import 进去，不会自动开始无限循环
if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        # 捕获 Ctrl+C，打印一句再退出，而不是甩一大堆红色报错堆栈
        print("\n已停止")