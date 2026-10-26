# P7 / P8 · 纸艺强光影与电影光（渲染专项）

2026-10-25。DeepSeek 渲染专项。基于本地未提交的 V5 工作现场（HEAD 仍为 `d6800bd`），只改纸面材质光照、灯光/天空参数、阴影与影调链，不动机型、地图、布局、玩法、动态与 UI。

新增两档，默认仍是 V5：

| 档位 | 名称 | 定位 | 入口 |
| --- | --- | --- | --- |
| 5 | V5 D 纸艺世界 | 原样，控制组 | `.\验收纸艺世界V5.ps1` |
| **7** | **P7 纸艺强光影 NPR** | 克制档：方向统一、明暗分家，不加额外滤镜 | `.\验收纸艺强光影P7.ps1` 或面板按钮 |
| **8** | **P8 纸艺电影光影** | 强档：金色主光 + 冷蓝天光补光 + 更饱和 + 纸面辉光 + 轻暗角 | `.\验收纸艺电影光P8.ps1` 或面板按钮 |

## 0. 一句话结论

同一相机、同一资产、同一帧：只重构灯光与材质光照后，区域机位明暗跨度 p95−p05 从 **0.623 → 0.744（P8）**、饱和度 chroma 从 **0.291 → 0.345（+18%）**、深部占比从 4.1% → 9.7%，而平均亮度基本不变（0.591 → 0.544）。关掉最终调色时，P7 已经做到 0.561 → 0.750，证明不是靠滤镜。

## 1. 先确认了真正生效的渲染链

| 项 | 实测 |
| --- | --- |
| 后端 | `forward_plus`（RTX 5000 Ada Laptop，Vulkan 1.4.312） |
| 相机 | `CameraRig/Camera3D`（正交，`Size` 由 MapCameraController 控制） |
| 渲染入口 | `GameMapScene._Ready → ArtRenderProfile.Apply` → `Preset(index)` |
| 当前默认 | `CurrentPreset=5`（V5 D） |
| 环境 | `WorldEnvironment("Environment")`，`BackgroundMode=Color`，Sky 只提供环境光照 |
| 主光 | `DirectionalLight3D`（节点名固定），副光 `PaperBounce` 运行时创建 |
| 纸面 | 全部由 `ConceptPaperStyle` 创建/缓存纸面 shader 材质 |
| 影调 | 3D → `PaperAtmosphere`（Layer=-10，LDR Canvas，`BackBufferCopy`）→ HUD |
| 截图歧义 | HUD 按钮直接调用 `Palette(index)`，不存在"按钮滞后"；`--art-capture` 走同一条链 |

## 2. 画面为什么平——单变量分离

`region` 机位、关最终调色，一次只改一件事：

| 变体 | 做了什么 | 对比度 p95−p05 |
| --- | --- | ---: |
| `v5-nograde` | V5 原样 | 0.562 |
| `p7-no-shadow-nofold` | 新光照链，关阴影+关折面 | 0.611 |
| `p7-plain-mat` | 新光照链 + **旧材质** | 0.775 |
| `p7-nograde` | 新光照链 + NPR 材质 | 0.750 |
| `p7` | 以上 + 最终调色 | 0.779 |

结论（本工程实测）：
1. **"太平"的第一原因是阴影**。原主光 50° 仰角 + `shadow_blur 1.6` + 42m，投影像柔黑团块，读不出方向。
2. 第二原因是**材质光照曲线**：纯 Lambert 让背光面仍保留 0.2–0.4 直射。
3. 第三是**几何事实**：六角顶面法线全是 `Vector3.Up`，`N·L` 处处相同——不加人工折面，地面不可能有明暗组织。
4. 滤镜只占最后 ~0.03。

## 3. P7（克制档）做了什么

| 参数 | V5 | P7 |
| --- | --- | --- |
| 太阳朝向 / 能量 / 颜色 | (-50,-42) / 1.36 / `fff1dc` | **(-35,-42) / 1.60 / `ffedd0`** |
| 副光 | 仰角 +41（其实是"从下往上照"） | 保留但仅 0.09 |
| 环境光 | Sky 0.27，天光占比 0.65 | **Sky 0.30，占比 0.45，`8fa2e0`** |
| 阴影 | 0.90 / blur 1.6 / 42m / 2 分片 | **0.95 / 1.15 / 30m / 2 分片** |
| SSAO | 半径 0.11、强度 0.65、影响直射 0 | **0.50 / 1.25 / 0.18** |
| 色调映射 | Filmic | **ACES + 曝光 1.02** |

材质：新增 `physical_paper_npr.gdshader`（V5 原 shader 一字未改），自定义 `light()`：

```glsl
float w = clamp((ndl + npr_wrap) / (1.0 + npr_wrap), 0.0, 1.0);
float lit = smoothstep(npr_band_lo, npr_band_hi, w);
float response = mix(npr_shade, 1.0, lit) * mix(1.0, w, npr_grad);
vec3 tint = mix(vec3(1.0), npr_shade_tint, (1.0 - lit) * npr_shade_tint_mix);
DIFFUSE_LIGHT += LIGHT_COLOR * (1.0/PI) * ATTENUATION * response * tint * npr_lit_gain;
```

按交接要求逐条核对：`LIGHT`/`NORMAL` 同空间；用引擎 `ATTENUATION`（含阴影）与 `LIGHT_COLOR`（含 energy×PI），**除以 PI** 与内置 Lambert 同标定；**没有**在任何一盏灯里加固定背光底色（`npr_shade` 是乘性下限，且乘在 `ATTENUATION` 上，被投影遮住处仍归零）；不按 `LIGHT_IS_DIRECTIONAL` 认定太阳；无 vertex lighting；保留 UV / 顶点色 / 透明贴片 / 风动 / `detail_fade`。

地形折面：只对朝上的地形纸面加极低频法线倾斜（只改着色法线，不改几何、剪影、阴影 bias），`npr_fold=0` 可关闭。

## 4. P8（强档）在 P7 之上做了什么

1. **主光更暖更低**：`ffd79a` 金色、能量 1.75、仰角 -33°——影子更长，亮面偏金。
2. **真正的冷色天空补光**：`PaperBounce` 从"仰角 +41（从下往上照，几乎无效）"改成 **-48° / 135°、能量 0.55、`9db4ff`**，从太阳反侧上方补冷光。这是本档最大的可见变化：背光面不再发黑，而是**冷蓝的暗部**。实测补光 0 → 0.55 让区域平均亮度 0.4018 → 0.5410。
3. **更饱和**：`GradeSaturation 1.20→1.38`，chroma 区域 0.308 → 0.345。
4. **纸面辉光**：`Glow` 重新打开但只吃 HDR 高光（阈值 1.15、强度 0.35、Screen 混合），只在最亮的日照纸面上产生一层薄光，不是全屏泛光。
5. **轻暗角**：LDR 调色里加 `npr_vignette=0.18`，仅压四角，P7 为 0。
6. 折面取 0.28（比 P7 更大），但更慢的频率，避免出现"污渍"感。

P8 的受光:背光 = **2.71:1**（V5 3.23:1，P7 3.18:1）——落在交接建议的 2:1–3:1 内，而且暗部值更高（0.284 vs 0.224），即"暗部更亮但更冷"，不是靠压黑换对比。

## 5. 验收数据（1760×1000，冻结动态，同一相机）

| 机位 | 指标 | V5 | P7 | **P8** |
| --- | --- | ---: | ---: | ---: |
| 总览 | 对比度 | 0.475 | 0.630 | **0.628** |
| 总览 | 饱和度 | 0.291 | 0.309 | **0.329** |
| 总览 | 深部占比 <0.22 | 1.2% | 3.8% | **2.6%** |
| 区域 | 对比度 | 0.623 | 0.779 | **0.744** |
| 区域 | 饱和度 | 0.291 | 0.308 | **0.345** |
| 区域 | 深部占比 | 4.1% | 11.9% | **9.7%** |
| 近景 | 对比度 | 0.662 | 0.804 | **0.785** |
| 近景 | 饱和度 | 0.299 | 0.315 | **0.332** |
| 近景 | 深部占比 | 6.9% | 20.3% | **18.4%** |
| 区域（关最终调色） | 对比度 | 0.562 | 0.750 | **0.698** |

平均亮度没有大幅漂移（总览 0.683→0.701，区域 0.591→0.544，近景 0.520→0.450）。

**P7 与 P8 的分工**：P7 更"黑白分明"（对比 0.779、深部 11.9%），P8 更"彩、暖、暗部有颜色"（饱和 +12%、受光/背光比 2.71）。想要戏剧感选 P8，想要稳一点选 P7。

### 灰盒校准（同一灰 0.58 纸盒，17×17 像素均值）

| 档位 | 受光 | 背光 | 比值 |
| --- | ---: | ---: | ---: |
| V5 | 0.724 | 0.224 | 3.23 : 1 |
| P7 | 0.781 | 0.246 | 3.18 : 1 |
| **P8** | 0.770 | 0.284 | **2.71 : 1** |

### 性能

同一进程、同分辨率、每档 120 帧预热后 12 秒采样。**这台机器的 GPU P95 短样本噪声很大**（同一档位同一机位重复测会出现 2.4–5.5ms 的差异），所以下表给 CPU/draw 的稳定项与多次采样后的 GPU 增量区间：

| 条件 | V5 CPU P95 / draw | P7 CPU P95 / draw | P8 CPU P95 / draw | GPU 增量（多次采样） |
| --- | --- | --- | --- | --- |
| forward_plus / overview | 12.9 ms / 4384 | 13.5 ms / 4627 | 13.9 ms / 4717 | P7 +0.1~0.2 ms；**P8 +0.2~0.6 ms** |
| forward_plus / region | 9.8 ms / 3353 | 10.5 ms / 3388 | 9.5 ms / 3393 | 在噪声内（±0.2 ms） |
| forward_plus / close | 6.5 ms / 2297 | 6.8 ms / 2295 | 4.3 ms / 2276 | 在噪声内（±0.2 ms） |

能确定的结论：
- **P7 的新增成本基本为 0**（多 243 个 draw，GPU 在噪声内）。
- **P8 多约 0.2–0.6 ms GPU、+38 MiB 显存**，来自辉光金字塔；`Glow=false` 即可去掉。
- draw 增量：P7 +243 / P8 +333（overview），来自自定义 `light()` 的额外光照分支统计；4 分片阴影才会 +3600 draw、+6ms CPU，已否掉。

### 后端

- Forward+ / Mobile：P7、P8 均生效（Mobile 实测 P8 区域 mean 0.541 / 对比 0.748，与 Forward+ 基本一致）。
- Compatibility：**P7/P8 显式回退 V5**（无天空辐照、无 SSAO，硬套会过暗过饱和）。实测 GL 下 P8 与 V5 的整图统计完全相同（mean 0.396 / p05 0.169 / p95 0.712 / 对比 0.543）。

## 6. 回归

- 六套离线测试 `RunUnitTests / RunEnvironmentArtTests / RunHexCellTests / RunGameMapTests / RunOrigamiArtTests / RunGameScreenTests` 全部 `exit=0`，断言一条未改。
- **preset 5 不受影响**：改动前后同机位截图 **96.97%** 像素逐字节相同，差异全部落在细节 LOD 的抖动淡出区域（异步资源加载时机不同，不是光照差异）；整图 mean / p50 / 对比度差异 ≤0.002。
- 138 格 / 385 边 / 树根锚点 / 棋子落点 / 铁路中心线：未改任何几何或布局代码。
- 动态：未改 `PaperEnvironmentMotion`；lookdev 在 `Frozen=true` 下多次截图一致。
- 退出：所有采集进程完成 `ART_DETACHED_MATERIAL_HANDLES_RELEASED` 正常退出，无 `material_set_shader` 循环。

## 7. 已知问题

1. 总览时海面（`unshaded`）在 P8 下偏亮偏青，占画面大；要更沉可单独压 `paper_ocean`，未做以免影响 V5。
2. 远景山体对比没跟着变强（资产本身低对比）。
3. 近景整体比 V5 暗约 13%（P7 12%）；嫌暗调 `Exposure`（P8 1.16）或 `AmbientEnergy`（0.30）。
4. 辉光只吃 HDR 高光，低端设备若嫌贵可 `Glow=false`，画面差异只在最亮的纸面。
5. 未做 20 秒录像；未在真机手机验证。

## 8. 文件

```
新增 assets/shaders/physical_paper_npr.gdshader
新增 scripts/art/PaperNprStyle.cs            P7/P8 全部参数
新增 tests/RunPaperNprLookDev.cs             对比采集器
新增 验收纸艺强光影P7.ps1 / 验收纸艺电影光P8.ps1
改  scripts/art/ArtRenderProfile.cs          档位 7/8、GL 回退
改  scripts/art/ConceptPaperStyle.cs         按档位选 shader
改  scripts/art/PaperAtmosphere.cs           preset≥7 的调色参数
改  assets/shaders/paper_grade.gdshader      npr 分支（默认 0 = 旧路径）
改  scripts/art/OrigamiGallery.cs            面板新增 P7/P8
改  验收纸艺世界V5.ps1                       -PaperPreset 上限 0..8
```

证据：`artifacts/npr-lookdev/`（PNG、`tri-region.png`、`tri-close-crop.png`、`report.json`、`baseline-orig/`、`gl/`、`mobile/`）；回归日志 `artifacts/npr-regression/*.log`。
