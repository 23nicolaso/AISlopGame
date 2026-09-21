# Changelog

本项目所有值得记录的改动都写在这里。

格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Added

- **七个对手七种个性**（`MECHANICS-DESIGN.md` §7）：新 `ArenaPersonality` 只读表按 `id` 索引，字段 `aggression` / `bankAt` / `aimJitter` / `reaction` / `greed` / `revenge`。AI 里原先由 `id` 推导的常数（`id%3==0?720:470`、`cargo>=35`、`.4f+(id%5)*.1f`、cargo 权重 `1.5f`、`retaliation=9`）全部换成查表；`Shoot()` 里按 `aimJitter` 给非玩家的开火方向加 0.5°–3° 随机偏转。排行榜每个呼号右侧加 4 字标签（HUNT / HORD / VULT / STDY / AVNG / ROOK / ELIT）。
- **易爆与装甲残骸**（§2、§10）：`SalvageCore` 加 `CoreKind{Normal,Volatile,Armored}` 与 `maxHealth`。每个站点 `c==2` 是 Volatile（紫色反应堆 + 分裂约束笼 + 破裂冷却阀，`weakPoint` 脉动加快一倍），摧毁时对 45 m 内所有存活 pilot（含开火者本人）造成 55 伤害、走 `Feedback("large")`、掉落 1.5× 货物；`s%3==0` 的站点 `c==3` 是 Armored（新增 `slate` 暗色装甲材质 + 四道装甲带），血量 200、机炮伤害 ×0.3、导弹全额（`Hit` 新增 `bool missile` 参数，`ArenaBolt` 传 `seeker`）、掉落 3× 货物。AI 选 `coreTarget` 时跳过 60 m 内的 Volatile（`greed>=2` 的秃鹫只留 25 m）。
- **王牌连杀与悬赏**（§8）：`ArenaPilot.streak` + `AerialCombatPrototype.aceId`。`Kill()` 里受害者清零并让出悬赏，凶手连杀达 3 即成为 ACE。ACE 期间曳光与拖尾变金、在环里存分 ×1.5、排行榜标签变 "ACE"、屏幕顶部 `MatchClock` 下方出现脉动的 "BOUNTY <呼号>" 条（首次加冕有 1.35× 入场缩放）。所有 AI 的目标评分对 ACE 的 cargo 权重 ×2 并额外减 300 的平坦拉力，空手的 ACE 同样会被围攻。死亡即清除，`RestartMatch()` 一并重置。
- `RiftCombatVerification`：三项新校验 —— 装甲残骸对 100 机炮伤害只掉 30 血、对 100 导弹伤害掉满 100；易爆残骸炸毁时 30 m 外的 pilot 恰好掉 55 血、300 m 外的不掉；连续 3 杀后 `aceId` 指向凶手、该 pilot 被击落后 `aceId` 归 -1。PASS 日志串加入 `armored cannon discount, volatile blast radius, ace bounty crowning and clearing`；`finally` 一并复原 streak / aceId / 残骸血量并清扫爆炸掉落的货物。
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
- **无人值守校验入口** `Assets/Editor/RiftHeadlessRunner.cs`（`dc4e172`）：`-batchmode -nographics -executeMethod RiftHeadlessRunner.Run`（不带 `-quit`）自动进入 Play Mode、依次跑两套 `Rift*Verification`，退出码 0 = 全过、1 = 有检查失败、2 = 90 s 内没进入 Play Mode（通常是编译错误）。
- **无人值守截图入口** `Assets/Editor/RiftScreenshotRunner.cs`：`-batchmode -executeMethod RiftScreenshotRunner.Run`（不能带 `-nographics`）进入 Play Mode 后摆五个固定机位（发射点 / 亚轨道 / 精炼环 / 残骸场 / 交战），把追尾相机渲染进 1920×1080 RenderTexture 写成 PNG 到 `docs/screenshots/`（可用环境变量 `RIFT_SHOT_DIR` 覆盖）。每张图做平均亮度与对比度自检，纯黑或纯色帧直接判失败退出 1，所以它同时也是"渲染管线没坏"的回归测试。IMGUI HUD 不经过 `Camera.Render()`，截图里没有 HUD。
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

### Fixed

- **AI 会永久放弃报复且一枪不开**（`c5f2956`）。校验项 "Returns fire on attacker despite carrying cargo" 在批次 2/3 之后一直失败，实机逐帧打点后发现是四个叠加的 bug 而非单纯转向慢：① `gateTarget` 在 tactic / 导航 / 油门三处都压过正在进行的交战，且决策块里 `else if(gateTarget)rivalTarget=null;` 会在 `retaliation` 归零那一帧清掉一个正在拉近的合法目标，之后不再重新扫描 —— 现在报复或 Alert 期间根本不计算 `gateTarget`，并删掉了那行强制清空；② Search 状态飞的是随自身位置漂移的方位角，改成记住并飞向 `lastKnownPosition` 绝对坐标（`NotifyAttacked` / 丢失接触 / 听见枪声三处写入）；③ 近距离脱离机动与开火共用同一门槛，贴脸那一帧被脱离拦住 —— 开火判定挪到脱离触发之前，且 `rivalTarget` 为空时 `breakTime=0`；④ 开火锥角 `<7°` 在共享飞行模型下弯道追击只能收敛到恰好 7.0°，放宽到 `<9°`。`RiftCombatVerification` 的报复窗口从 60 步放宽到 550 步（11 s），注释说明正后方受击的最坏几何需要 9–10 s 才能重新咬住。
- `PLAYTEST.md` 里截图目录写成不存在的 `My project/Captures`，实际目录是 `docs/screenshots/`（由 `RiftScreenshotRunner` 生成）与历史的 `My project/Assets/Screenshots/`。

### Refactored

- `SalvageCore`、`SalvageShard`、`ArenaDebris` 从 `Update() + Time.deltaTime` 改为 `FixedUpdate()` 调用 public `Tick(float dt)`，与 `CaptureGate` / `ArenaBolt` 保持一致，Editor 校验脚本得以用固定 dt 循环做确定性测试。行为不变。
