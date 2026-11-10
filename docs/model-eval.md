# 口罩检测模型评估卡

> 生成于 2026-10-31（Day 8）。数据来源：本机 CPU 实测 + 仓库 README。

## 模型信息

| 项 | 值 |
|---|---|
| 权重文件 | `models/best.pt`（6,258,915 字节） |
| 来源 | [jianxi-Erin/FaceMaskDetection](https://github.com/jianxi-Erin/FaceMaskDetection) → `runs/detect/train/weights/best.pt` |
| 许可证 | MIT |
| 类别 | `{0: 'with_mask', 1: 'without_mask', 2: 'mask_weared_incorrect'}` |
| 结构 / 训练 | YOLOv8n，100 epoch，Tesla T4，imgsz 800 |
| 训练数据 | Kaggle `andrewmvd/face-mask-detection`（853 张） |
| 公开指标 | mAP50-95 = 0.708（with_mask 0.727 / without_mask 0.667 / incorrect 0.732） |

## 本机实测（CPU，`samples_check` 15 张随机样本）

| 输入尺寸 | 图片数 | 无框图片 | with_mask | without_mask | incorrect | 平均耗时 |
|---|---|---|---|---|---|---|
| 320 | 15 | 0 | 41 | **1** | 1 | **75.5 ms** |
| 640 | 15 | 0 | 45 | **8** | 1 | **165.7 ms** |

## 320 vs 640 的关键差异

| 图片 | 320（with/without） | 640（with/without） | 现象 |
|---|---|---|---|
| `maksssksksss336` | 5 / 0 | 5 / 6 | 远景人群，320 完全漏掉 6 张脸 |
| `maksssksksss621` | 2 / 1 | 4 / 1 | 320 漏 2 个 |
| `maksssksksss719` | 4 / 0 | 8 / 1 | 320 漏 4–5 个 |
| `maksssksksss258` | 0 / 0（incorrect 1） | 同 | 三分类有效，识别出“佩戴不规范” |

**结论**：320 会漏掉远景/小尺寸人脸；640 能检出，但远景小脸的标签置信度偏低（0.76–0.86），可能是“看不清就判没戴”。

## 使用结论

1. 模型可用，加载、类别、三分类均正常。
2. **输入尺寸用 640**；320 只适合“单人近距离”场景（门口 1.5–2 m 时人脸足够大）。
3. 数据集与训练同源，以上指标**偏乐观**；真实场景精度需用自采照片（`samples_real`）验证。
4. 速度参考是 CPU 的，不代表 Jetson；Jetson 上走 TensorRT，需 Day 10 复测。

## 真机阈值调优（2026-11-02）

| `conf` | 不戴 10 次漏报 | 备注 |
|---|---|---|
| 0.25 | 1 次 | 基准 |
| 0.15 | 0 次 | 全中，但会收进更多低分框 |

**数据观察**：0.15 那轮落库的违规里，最低置信度是 **0.202**，说明低于 0.20 的框没有产生任何违规。0.15 相对 0.20 并没有多抓到什么，只是放宽了没有产出的区间。

**结论**：生产用 **`conf=0.20`**——大概率接住 0.25 漏的那一次，同时避免 0.15 的低分噪声。`ComplianceRules.MinConfidence` 保持 `0.5`（它管“戴了算不算戴好”，与检测阈值无关）。

**尚未验证**：戴口罩的人是否会被低阈值误报（需要 10 次“只戴不摘”的对照）。
