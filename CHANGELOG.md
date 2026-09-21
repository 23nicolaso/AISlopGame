# Changelog

本项目所有值得记录的改动都写在这里。

格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Added

- **比赛结构**（`IMPROVEMENT-PLAN.md` WP-C 第 1–2 条）：新 partial `PlanetArenaMatch.cs`，`MatchPhase{Countdown,Playing,Ended}` —— 倒计时 3 s（武器冷）→ 比赛 300 s → 结算（无限期）。`MatchActive` 与 `paused` 并列插进 `Damage` / `Shoot` / `Collect` / `CaptureGate.Tick` 四个已有检查点。`RestartMatch()` 按 Enter 触发：全员 `Spawn(initial:true)`、清 score/cargo/kills/deaths、所有环归中立、清扫散落货物与已破残骸。
- **HUD 比赛信息层**：顶部中央 `M:SS` 倒计时（最后 60 s 变红并随秒脉冲，每整 10 s 一次心跳音，最后 10 s 加重）；倒计时阶段中央 3 / 2 / 1 / GO 大字（1.5× pop + 外扩环）；结算面板含排名 / 呼号 / kills / losses / banked，冠军行金色，底部脉动 "PRESS ENTER TO RESTART"。
- **货物有重量**（`MECHANICS-DESIGN.md` §1）：每 10 单位 cargo 让重力 +4%、阻力 +3%，上限 +60% / +45%，对 AI 同样生效。HUD 的 CARGO 在 ≥60 时变金并显示脉动的 "HEAVY" 标签。
- **占领环持续产分**（§3）：`CaptureGate.ownerAge` 从"只写不读"改为每 12 s 给持有者 +5 banked 分并让环白闪一下；无主环不累积。
- `RiftVerification`：四项新校验 —— Ended 阶段阻断 Damage / Shoot / Collect、重开后全员与环回到干净倒计时、满载 150 与空载同起点飞 5 s 后高度更低速度更慢、持环 11.9 s + `Tick(.2f)` 恰好 +5 分且 `ownerAge` 归零（并验证不会在同一周期内重复计分）。PASS 日志串加入 `refinery income, ended gating, match restart, cargo weight`。
- **AI 感知模型**（`MECHANICS-DESIGN.md` §6b）：`Sense(other)` 分三级——前方 110° 锥内 950 m 内直接看见、250 m 内余光看见、锥外 600 m 内对方 1.5 s 内开过火则只"听见"。`CanEngage` 收敛为 `Sense==2`。
- **Alert / Search 两个新状态**（§6c）：被锥外攻击或听见枪声先进 Alert，按 `id` 派生 0.4–0.8 s 反应延迟，期间只转向不开火（报复也要等延迟走完）；丢失目标改为沿最后方位做 3 s ±38° 扫视 Search，而不是立刻 `combatRest=5` 转头捡货。
- `RiftCombatVerification`：新增 "Blind-spot approach goes unnoticed"——玩家从 AI 正后方 400 m 接近且未开火时 `CombatTarget` 必须为空。
- **反馈层**（`MECHANICS-DESIGN.md` §5）：统一入口 `Feedback(tier,pos,involved,voice)` 按 small / medium / large 三档发送固定套餐（粒子 + 准星白闪 + 音效 + trauma），`Damage` / `Kill` / `Collect` / `SalvageCore.Hit` 全部改走这个入口。
- **Hit-stop**：`hitStop` 字段 + `Update()` 里 `Time.timeScale=paused?0:(hitStop>0?.05f:1)`，只在玩家参与的 large 事件触发 0.08 s，`OnDestroy` 保底恢复 `timeScale=1`。
- **命中方向指示**：`Damage` / `Kill` 记录 `lastAttackDirection`，HUD 在准星外圈按相机空间方位画红色楔形，1 s 淡出。
- **击杀横幅**：排行榜下方 "SPLASHED <呼号> +<货物>" / "SPLASHED BY <呼号>"，1.3× ease-out 缩放，1.5 s 后淡出。
- `docs/IMPROVEMENT-PLAN.md`：把改进计划从"分阶段修补"重写为五个工作包的成品打磨框架，新增对照 fps-shooter 八项必备系统的完整度差距分析、按 game-feel 数值标准设计的反馈分层表，以及三处"最像半成品"的定位与整改方案。
- `PlanetSurface.shader`：菲涅尔边缘光，低空飞行时星球轮廓能从黑天空里分出来。
- `RiftVerification`：两项确定性校验——货物被最近的 pilot 吸引、进入拾取半径后自动 `Collect`。PASS 日志串加入 `shard attraction`。
- 本文件。

### Changed

- 两个 `Rift*Verification` 都像固定 `paused` 一样固定 `phase=MatchPhase.Playing`，并在 `finally` 里恢复 —— 它们直接调用现在受比赛阶段门控的 `Damage` / `Shoot` / `Collect` / `Tick`。
- **AI 与玩家共用同一条旋转通道**（`MECHANICS-DESIGN.md` §6a）：`Simulate()` 里去掉 `if(isPlayer)` 分支，`FlyTactics` 不再 `RotateTowards` 直接写 rotation，而是把目标姿态分解成 pitch / yaw / roll 三轴误差映射成 `controls`（-1..1），由共享的 `authority`/`rates`/`angularVelocity` 积分。AI 从此在稀薄空气和低速下同样转不动。地形预测拉起（`recovering`）保留 0.9 的 authority 下限和更快的舵面响应，属于生存反射而非缠斗优势。
- `RiftCombatVerification` 的战斗测试从 1400 m 挪到 320 m（1400 m 空气密度只有 0.7%，统一飞行模型后那里量的是"双方都转不动"而非战斗能力）；命中窗口 650 步放宽到 1600 步，并让玩家每 5 s 补一枪维持交战；报复检查从 1 步放宽到 60 步以覆盖新的反应延迟。
- `shake` 从"线性覆盖值"改为 trauma 模型：只加不设（`Trauma(amount)`），每秒线性衰减 1.3，相机实际偏移用 `shake²`（小击几乎不动、击杀猛踢），Perlin 噪声采样保留并补上 z 轴。
- 运行时相机接入 URP 后处理：缓存 `UniversalAdditionalCameraData`，开启 `renderPostProcessing` 与 SMAA（High）。场景里的 Global Volume（Bloom / Neutral Tonemapping / Vignette）从此对游戏相机生效。
- 重新平衡发光材质的 HDR 值（teal / red / gold / white / violet），从 1.9–2.0 压到 1.14–1.5，避免 Bloom 阈值 1 之上的超额亮度糊成白团。
- 星球表面贴图从"海洋 / 陆地"两档改为五档（深海 / 浅滩 / 滩涂 / 陆地 / 山脊雪线）并整体提亮；`PlanetSurface.shader` 的夜侧底光从 `.32` 提到 `.44`。
- 环境光从 `Flat` 改为 `Trilight`（冷青天空 / 深蓝赤道 / 暖橙地面），机身着色有了上下方向感。

### Refactored

- `SalvageCore`、`SalvageShard`、`ArenaDebris` 从 `Update() + Time.deltaTime` 改为 `FixedUpdate()` 调用 public `Tick(float dt)`，与 `CaptureGate` / `ArenaBolt` 保持一致，Editor 校验脚本得以用固定 dt 循环做确定性测试。行为不变。
