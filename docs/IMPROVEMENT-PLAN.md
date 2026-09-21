# RIFT 改进计划

分支：`feat/gameplay-improvements`（自 `main@4e7dd43` 切出）。日期：2026-09-20。

**这份计划的定位**：RIFT 的核心概念（球面打捞竞技场、7 个 AI 对手、占领环存分）已经定型，不需要再验证"这个点子值不值得做"。所以这里不用原型阶段那套 keep/kill 判据，也不设"先做风险最低的一刀"的分阶段修补门。判断标准只有一个：**一个成品竞技空战游戏里，这个系统应该长什么样**。差多少就补多少。

目标一句话：把"技术演示"变成"能玩 5 分钟、玩完想再来一局"的竞技场。三个衡量标准：**看得清、有输赢、打得爽**。

配套文档：`docs/MECHANICS-DESIGN.md` 讨论"加什么新机制能让每局产生决策与高潮"（货物重量、易爆残骸、AI 个性等）。本文只管**现有系统离成品还差什么、怎么补**，两者在 WP-D 和 WP-E 上衔接。

---

## 1. 现状诊断

来源：通读全部 5 个脚本 + 2 个校验脚本 + 2 个 shader、工程设置、`Assets/Screenshots/` 里 9 张截图。

### 1.1 画面（最严重）

| 问题 | 根因 | 位置 |
|---|---|---|
| 引擎光/曳光/金色反应堆完全没有辉光，画面死黑 | `Awake` 自建的 `Camera` 没有 `UniversalAdditionalCameraData.renderPostProcessing=true`，默认 false。场景里 `Global Volume`（Bloom / Tonemapping / Vignette 均 active）对它不生效；所有 >1 的 HDR 颜色（teal 1.9、gold 1.9、red 2.0）被 clamp 成平色 | `AerialCombatPrototype.cs:64-66` |
| 低空地面几乎全黑 | 星球贴图基色 `(.018,.075,.14)`，shader 光照 `.32+.85·N·L`，没有 tonemapping 也没有大气散射叠在地表上 | `PlanetArenaArt.cs:137-138`、`PlanetSurface.shader:23` |
| 飞机只有几十像素、没有尺度感和速度感 | 机身约 4 m 长，追尾相机 20 m；地面无任何散布物，云是 28 个压扁的球 | `AerialCombatPrototype.cs:25`、`PlanetArenaArt.cs:181-187` |
| 无抗锯齿 | 相机 `antialiasing` 未设，Quality 里 `antiAliasing: 0` | 同上 |

### 1.2 玩法结构

- **没有比赛**：无限循环，无倒计时、无胜负、无结算画面。排行榜数字只是在涨，没有意义。
- **反馈薄弱**：没有击杀提示、占领/存分提示、"谁打死了我"。Seeker 60 伤害（玩家 100 血）却没有任何导弹告警。
- **AI 不对等**：AI 直接 `RotateTowards(desired, 80°/s)` 全轴（`PlanetArenaAI.cs:115`），玩家 pitch 上限 44°/s、yaw 27°/s 且有惯性和空速衰减（`PlanetArenaFlight.cs:83-86`）；AI 瞄准零误差、油门瞬时切换。玩家在缠斗里被 out-turn 是结构性的。
- **深度不足**：6 个站点 × 4 残骸结构完全一致，仅 value 8/16 之分；没有制造冲突点的机制。

### 1.3 可测试性与债务

- `SalvageShard` / `SalvageCore` / `ArenaDebris` 用 `Update()+Time.deltaTime`，不在 AGENTS.md 规定的 dt 模型里，校验脚本无法确定性覆盖"货物吸附 → 拾取"。
- `com.unity.test-framework` 1.8.0 已在 manifest 里，但没有 `Tests/`、没有 asmdef，校验只能在 Editor 菜单里手点，CLI 跑不了。
- 小债务：`CaptureGate.ownerAge` 只写不读；`PLAYTEST.md` 说截图在 `My project/Captures`（实际 `Assets/Screenshots/`）；`productName` 还是 "My project"；没有 `CHANGELOG.md`；撞地死亡和被击落用同一套特效。

---

## 2. 工作包

五个工作包，**全部都要做**。顺序按技术依赖排（反馈层需要先有事件钩子，信息层需要先有比赛状态，AI 重写需要先有统一的旋转通道），不按"哪个最安全先做"排。没有哪一包是"看情况再说"的——一个系统如果对"完整"是必要的，它就在计划里。

### WP-A — 可读性（画面基建）

1. **开后处理**：`cam.GetUniversalAdditionalCameraData()` 取一次缓存到字段，设 `renderPostProcessing=true`、`antialiasing=SMAA`、`antialiasingQuality=High`。场景 `SampleSceneProfile` 的 Bloom（threshold 1 / intensity .25 / scatter .5）、Neutral Tonemapping、Vignette .2 从此对运行时相机生效。
2. **HDR 重平衡**：Bloom threshold 是 1，原来的 teal 1.9 / gold 1.9 / red 2.0 会把 0.9~1.0 的超额亮度全部灌进 bloom 糊成白团。压到 1.14~1.5 区间——仍在阈值之上（还会发光），但超额量减半。
3. **星球分层提亮**：`BuildPlanet()` 的贴图从"海洋/陆地"两档改成五档（深海 / 浅滩 / 滩涂 / 陆地 / 山脊雪线），基色从 `(.018,.075,.14)` 提到 `(.042,.115,.225)` 起步。`PlanetSurface.shader` 的夜侧底光 `.32` → `.44`，并加菲涅尔边缘光（`pow(1-N·V, 3.5)` × 冷蓝）让低空时星球轮廓从黑天空里分出来。
4. **环境光分层**：`RenderSettings.ambientMode` 从 `Flat` 改 `Trilight`，天空冷青 `(.34,.5,.72)` / 赤道深蓝 `(.13,.19,.34)` / 地面暖橙 `(.36,.25,.17)`。机身从纯平光变成有上下方向感。
5. **尺度感**：相机距离 20 → 14，飞机 `art` 放大 1.35×（只缩放视觉子物体，不动 4 m / 9 m 的命中常量）；每个站点周围散布约 30 个岩柱/信标塔；相机挂风粒子，emission 随 `player.Speed` 和 boost 增长。
6. **天空与地标**：太阳圆盘（unlit HDR sphere，Bloom 会把它放成光晕）；每个 refinery ring 一根 400 m 高的垂直光柱，颜色跟随归属色，2 km 外可见；云从单个压扁球改成 5~7 个球的 cluster。
7. **发光细节**：boost 时 `Exhaust` 沿 z 拉长 2.5×；tracer 宽度 .18 → .3。

### WP-B — 反馈层

按 §7 的分层表实现。前置条件：把 `shake` 从"线性覆盖值"改成 trauma 模型；把三个仍用 `Update()` 的实体拉进 `Tick(dt)`，让校验脚本能确定性覆盖货物流程。

### WP-C — 比赛结构与信息层

1. **`MatchState`**（新 partial 文件 `PlanetArenaMatch.cs`）：`Countdown → Playing(300 s) → Ended`。`matchActive` 和 `paused` 并列，在 `Damage` / `Shoot` / `Collect` / `CaptureGate.Tick` 的开头一起检查。结算面板：排名、kills / deaths / banked、按 Enter 重开（对所有 pilot `Spawn(initial:true)`、清 cargo/score、重置 gate）。
2. **HUD 顶部中央倒计时**，最后 60 s 变红，每 10 s 一次心跳音。
3. **事件流 toast**：右侧中部最多 4 条、3 s 淡出。击杀（含凶手/受害者）、占领、存分、玩家被击杀显示凶手。
4. **导弹告警**：`ArenaBolt.seeker && target==player.transform` 时屏幕边缘出现来袭方向红箭头 + 脉冲音（频率随距离升高）。给躲避手段但不加按键：boost 期间 seeker 转向率 2.3 → 1.2 rad/s。
5. **目标框**：`AimTarget` 命中的敌机画方框 + 血条 + 距离；`cargo ≥ 50` 时 CARGO 数字变金并脉动，导航标记优先指向最近 ring。
6. **过热状态可见**：heat 锁枪要有名字。见 §6 第 4 条。

### WP-D — 对手

1. **AI 走玩家同一套旋转通道**：`FlyTactics` 输出 `controls`（pitch / yaw / roll 三轴 -1..1），由 `Simulate` 统一积分，删掉 `if(isPlayer)` 分支。
2. **技能参数**：每个 pilot 加 `aimJitter` 0~3°、`reaction` 0.2~0.6 s、`turnScale` 0.75~1.05，按 id 派生；结算面板可切难度档。
3. **显式状态机**：`tactic` 从字符串变 enum，补上 fps-shooter 要求的 `Search` 状态（丢失目标后朝最后已知位置飞 3 s 再放弃），取代现在"丢目标立刻 `combatRest=5` 转去捡货"的突兀切换。
4. **玩家手感**：stick 居中时轻微自动改平（roll 向 up 收敛 ~15°/s）；鼠标 x 加 0.25 的 yaw 耦合（协调转弯），减少对 Q/E 的依赖。两项都做成可关常量。
5. **武器**：cannon 加与 heat 挂钩的散布；seeker 加 1.2 s 锁定过程（准星停留 → 锁定圆收缩 → 才能发射），让 60 伤害有代价。
6. **重生安全**：3 个候选出生点，选距最近敌机 ≥ 300 m 的；`Kill(this,null)` 撞地用独立的白色碎片 + 低频音，和被击落区分开。

### WP-E — 深度与音频

本包的玩法机制部分与 `docs/MECHANICS-DESIGN.md` 共用一套提案（货物重量、易爆残骸、定时冲突事件等），那份文档是机制的权威来源；这里只列本计划负责的落地项。

1. **站点差异化**：低轨高价值站（alt 450、value 24、空气稀薄机动差）vs 地表密集低价站，不再是 6 个同构站点。
2. **定时事件**：每 90 s 随机一个 ring 进入"双倍精炼" 30 s，全场导航标记提示——人为制造冲突点，这是唯一能让 8 架飞机真正撞在一起的机制。
3. **音频分层**：风噪随速度、锁定音、占领 chime、结算音、过热嘶声；引擎音按 throttle/boost 分两层交叉淡入而不是单 clip 调 pitch。
4. **无障碍开关**：暂停菜单里 `shakeScale`（0–100%，默认 80%）与 `reduceFlashing`。game-feel 把这两项列为必须出货的选项。

---

## 3. 通用规则

- 每个工作包改完 **必须** 跑 `Rift > Verify planetary arena` 和 `Rift > Verify flip stability and rival combat`。
- 新逻辑全部放进 `Simulate(dt)` / `Tick(dt)` / `UpdateChaseCamera(dt,…)`，不进 `Update`（读输入除外）。
- 沿用现有紧凑代码风格，不重排格式。
- 每个工作包一个 PR，commit 用 `feat:` / `fix:` / `refactor:` 前缀；同步 `CHANGELOG.md` 和 `PLAYTEST.md`。
- 新增校验项直接写进对应的 `Rift*Verification.cs`，并把名字加进最后的 PASS 日志字符串。

---

## 4. 风险与我可能忽略的点

- **Bloom 过曝**：HDR 1.9 的材质开 Bloom 后会糊成一片，WP-A 的开关和调色必须同一次改完，不能只开开关。
- **地形位移不做**：本来想用顶点噪声做山脉，但会破坏 `Altitude<4` 撞地判定和 AI `recover` 模型的球面假设。改用地面散布物达到同样的尺度感，碰撞模型不动。
- **相机拉近影响校验**：`RiftCombatVerification` 检查飞机在视口 0.3~0.7 范围内，改 `cameraDistance` 后要重跑并可能微调 `(0,5,-distance)` 偏移。
- **AI 变笨的连锁**：WP-D 让 AI 走玩家通道后，"actual projectile hits" 校验的命中数可能掉到 0，需要把 `combatHits>0` 的判定改成在更长的模拟窗口内统计。
- **CLI 和 Editor 互斥**：Unity Editor 打开工程时 CLI 编译/测试会报 lock，任何脚本化验证都要在报错时给出明确提示。
- **性能**：`Burst` 每颗碎片 new 一个 GameObject；WP-A 加粒子和散布物后 8 个 pilot 的场景预计仍然没问题，但要在 Profiler 里看一眼 draw call，超过 1500 就把散布物合并成一个 Mesh。
- **手感无法校验**：鼠标绝对位置 stick 是否顺手、自动改平强度是否合适，只能靠实机试飞；WP-D 的这两项默认做成可关的常量。

---

## 5. Unity 6 最佳实践对照（2026-09-20 搜索）

只列和本项目有交集的条目，来源见文末。

| 官方建议 | 本项目现状 | 处理 |
|---|---|---|
| URP 下每个 Camera 必须带 `UniversalAdditionalCameraData`；脚本里用 `camera.GetUniversalAdditionalCameraData()` 取并缓存 | 运行时 `new GameObject().AddComponent<Camera>()`，从未取过该组件；`renderPostProcessing` 源码默认 false，截图也证实没有 Bloom | WP-A 第 1 步：`Awake` 里取一次并设 `renderPostProcessing=true`、`antialiasing=SMAA`，引用存字段 |
| Test Framework CLI：`-runTests -testPlatform PlayMode -testResults <xml> -batchmode`；PlayMode 测试 asmdef 的 `includePlatforms` 必须是 `[]` 而不是 `["Editor"]`；生产代码一个编译错误就会导致 0 个测试运行 | 无 Tests、无 asmdef | 按此配置建 PlayMode 测试壳包装现有两个 `Verification`；脚本要检查 XML 里 `total="0"` 并报错 |
| 避免每帧分配：缓存 List、避免字符串拼接、`Shader.PropertyToID` 代替字符串、不用 LINQ | `OnGUI` 每帧拼十几个字符串；`CaptureGate.Tick` 每步 `SetColor("_BaseColor",…)` 走字符串；`Burst`/`SpawnShard` 每颗碎片 `CreatePrimitive` 再 `Destroy(Collider)` | `_BaseColor` 改 `PropertyToID`；HUD 字符串每 0.1 s 刷新缓存；碎片改 `UnityEngine.Pool.ObjectPool` |
| `GetComponent` 放 Awake，别放热路径 | `Shoot` 和 `ArenaBolt.Tick` 每次调用都 `GetComponent<ArenaPilot>()` / `<SalvageCore>()` | 给 `ArenaBolt` 存类型化引用，`Shoot` 的 `preferredTarget` 改成传 `ArenaPilot`/`SalvageCore` 而不是 `Transform` |
| Update 里读输入、FixedUpdate 里应用，渲染插值 | 已经是这么做的（`Simulate(dt)` + `SampleRenderPose`） | 保持，把剩余三个实体也拉进来 |
| 集中式 Update 管理器减少 MonoBehaviour 调用开销 | 每颗 shard / debris / bolt 各自一个 `Update`/`FixedUpdate` | 碎片数量级 <200，暂不做；若加粒子后 Profiler 显示脚本开销明显再集中 |
| GPU Resident Drawer：Forward+ + SRP Batcher + 大量同 mesh 的 MeshRenderer 时收益最大，只对静态 MeshRenderer 生效 | 站点散布物（WP-A 第 5 步）正是"大量同 mesh 静态物体" | 先用 `Shape` 直接铺；draw call 超 1500 再评估开 Forward+ 与 GPU Resident Drawer，否则合并成单 Mesh |
| 用 `Awaitable` 而非 `Task`，禁用 domain reload 加快迭代 | 无异步代码；`I` 单例静态字段在禁用 domain reload 时不会重置 | 若开启 Enter Play Mode Options，`Boot()` 里要显式置空 `I` |
| Editor 代码用 asmdef 隔离 | `Assets/Editor/` 靠文件夹约定隔离，能用 | 建 Tests asmdef 时顺手给 Editor 建一个，避免测试程序集引用不到校验脚本 |

来源：
- [Unity 6 Manual: Unity programming best practices](https://docs.unity3d.com/6000.0/Documentation/Manual/programming-best-practices.html)
- [Unity: Advanced programming and code architecture](https://unity.com/how-to/advanced-programming-and-code-architecture)
- [Unity 6.6 Manual: Universal Additional Camera Data](https://docs.unity.com/en-us/engine/6000.6/manual/cameras/urp/universal-additional-camera-data)
- [URP 17 API: UniversalAdditionalCameraData](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/api/UnityEngine.Rendering.Universal.UniversalAdditionalCameraData.html)
- [Test Framework: Running tests from the command line](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html)
- [Bugnet: Fix Unity Play Mode Tests Not Running](https://bugnet.io/blog/fix-unity-play-mode-tests-not-running)
- [Unity 6 Manual: Enable the GPU Resident Drawer in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html)
- [Unity blog: Unity 6 optimization guides](https://unity.com/blog/unity-6-game-optimization-guides)

---

## 6. 完整度差距：对照 fps-shooter 的八项必备系统

RIFT 不是第一人称射击，但它的核心循环和 FPS 完全同构：**扫描 → 锁定目标 → 瞄准开火 → 确认击杀 → 重新占位**。所以直接拿 fps-shooter 那八项 must-have 当尺子量，比泛泛说"手感不够好"有用。下表每一行都对到具体函数和数值。

| # | 标准要求 | RIFT 现状（函数 / 数值） | 判定 |
|---|---|---|---|
| 1 | 控制器 | `ArenaPilot.Simulate` 有完整气动模型：升力系数用周期函数避免迎角 ±180° 跳变、`stall` 在 22–48 m/s 间插值、侧滑阻尼用 `Vector3.Slerp` 重定向动量 | **达标**，且质量高于同类原型 |
| 2 | 相机 | `UpdateChaseCamera` 单四元数机位、boost 时 FOV 66→76 缓动、距离 20→24 | 缺后坐冲击（开火没有任何镜头反作用）、缺 FOV/灵敏度选项 |
| 3 | 射击模型 | 投射物制：`ArenaBolt` 360 m/s，`SegmentDistance<4` 扫掠检测防穿透；`InterceptPoint` 在射手运动系解二次方程做前置量 | **骨架达标**，但**无距离衰减**（650 m 内恒 12 伤害）、**无散布**（第一发和第一百发一样精确） |
| 4 | 武器与弹药 | cannon：12 伤害 / `fireCooldown=.12` / `heat+=.045`；seeker：60 伤害 / 7 s CD。冷却 `heat-=dt*.22`，`heat>.92` 锁枪 | 有"过热"这个节奏机制，但**玩家看不见它是个状态**——HUD 只有一根无标签的条，锁枪时没有文字、没有声音、没有滞回，读起来像卡了 |
| 5 | 生命与伤害 | 100 HP。TTK = `ceil(100/12)=9` 发 × `(9-1)×0.12s` = **0.96 s** 全中。seeker 起手则 60+4×12=108，约 0.5 s | **数值本身健康**（FPS 常见区间 0.3–1.0 s）。缺技巧奖励：没有弱点/精准命中倍率 |
| 6 | 敌人 AI | `FlyTactics` 的 if 链 + `decision` 计时器；有交战、报复、脱离、地形预测拉起 | 缺 fps-shooter 要求的 `Search` 状态；无反应延迟（`NotifyAttacked` 直接 `decision=0`）；瞄准零误差；旋转绕过玩家通道 —— 详见 §8 第 3 条 |
| 7 | 反馈 | `hitFlash=.16` 把准星染白、`hitSound` 音量 .2、`shake` 线性衰减、`damageFlash=.25` | **最大的洞**。没有 hitmarker、没有伤害数字、没有击杀确认、没有 kill feed —— 详见 §7 与 §8 第 1 条 |
| 8 | 目标 | `CaptureGate` 规则干净完整：环内恰好 1 人推进、1.4 s 满、首占 +25、cargo 转 score、≥2 人争夺阻断 | **有 capture 的机制，没有 capture 的目标**。没有比赛、没有终点、没有胜负 —— 详见 §8 第 2 条 |

三条落到数值上的整改（配合 WP-C / WP-D）：

- **距离衰减**：fps-shooter 的 "full to 20 m, floor by 60 m" 是人体尺度的建议值，RIFT 的交战距离是它的十倍量级，按比例缩放为 **250 m 内满伤害，600 m 衰减到 0.55×**，线性插值。这样贴身缠斗有回报，远距离骚扰不再和抵近攻击等价。
- **散布挂 heat**：`spread = .1° + heat × .55°`。冷枪第一发几乎零散布；持续压枪到 heat≈1 时散布 0.65°，在 400 m 处约 4.5 m 偏移，刚好和 4 m 命中半径同量级。符合"第一发准、连射发散、模式可学习"的要求，而且复用了已有的 heat 状态，不引入新变量。
- **精准命中**：RIFT 没有 hitbox，但 `ArenaBolt.Tick` 已经算出了 `SegmentDistance`。`<1.4 m` 判定为精准命中，**1.75× 伤害（21）**，配一个更亮的 hitmarker 和更高的确认音。这是在现有碰撞模型里凭空加出技巧维度，零新结构。
- **过热变成一个有名字的状态**：`heat>.92` 锁枪后加滞回，`heat<.45` 才解锁；HUD 条变红并显示 `OVERHEAT`，锁上和解锁各一声。fps-shooter 把"换弹 = 脆弱窗口"列为节奏的必要部分，heat 是 RIFT 的换弹。

---

## 7. 反馈层设计（按 game-feel 的数值标准）

### 7.1 现在为什么不够

game-feel 的核心判据：**一次令人满意的命中，是 5–8 个微反馈在 ~100 ms 内一起发生**。逐事件数一下 RIFT 现在有几层：

| 事件 | 现有层数 | 具体 |
|---|---|---|
| 玩家开火命中敌机 | 2 | `hitFlash=.16f`（准星变白）+ `PlayOneShot(hitSound,.2f)` |
| 玩家击杀敌机 | 1.5 | `Burst(24)` 在受害者位置 + `boomSound`（还要 `Distance<350` 才响） |
| 玩家被击杀 | 3 | `Burst(24)` + `boomSound` + `shake=.8f` + `damageFlash=.5f` |
| 拾取货物 | 2 | `hitFlash=.12f` + `collectSound` |
| 占领 / 存分 | **0** | `CaptureGate.Tick` 里只改了 `score` 和环的颜色 |

`AerialCombatPrototype.Kill(victim,attacker)`（`AerialCombatPrototype.cs:126-141`）里，`attacker` 只被用来 `attacker.kills++`；所有画面/音频反馈都挂在 `victim` 身上。**玩家打掉一架敌机时，玩家这边一层反馈都不加。** 这是整个项目最能说明"半成品"的一行代码。

### 7.2 trauma 模型（先修地基）

现在的 `shake` 有三个和 game-feel 标准相悖的地方：

1. **线性输出**：`cam.transform.position=position+cameraRotation*(offset+noise*shake)`，shake 直接乘噪声。标准要求 `shake = trauma²`，这样小事件几乎不动屏、大事件才真正打击。现在 0.05（开火）和 0.8（死亡）之间只是 16 倍线性差，结果是开火太吵、死亡不够重。
2. **覆盖而非累加**：`shake=.35f` 是赋值。连续挨三发和挨一发抖得一样。标准要求事件 **加** trauma 并钳在 [0,1]。
3. **衰减过快且只有位移**：`Mathf.MoveTowards(shake,0,dt*2)` = 2.0/s，game-feel 建议 1.0–1.5/s；而且完全没有 roll，冲击感少一半。

改法（全部在 `UpdateChaseCamera` 内，保持 dt 参数化）：

```
trauma = Mathf.Clamp01(trauma + amount);                  // 事件累加
trauma = Mathf.MoveTowards(trauma, 0, dt * 1.25f);        // 1.25/s，落在 1.0–1.5 区间
float k = trauma * trauma * shakeScale;                   // 二次曲线 + 无障碍缩放
// 位移用已有的 Perlin，roll 用一条不同频率的正弦，避免和位移同相
noise  = new Vector3(Perlin(t*19,0)-.5f, Perlin(0,t*17)-.5f, 0) * k * 2.4f;
roll   = 3.5f * k * Mathf.Sin(t * 11f);                   // 3.5° ≈ 0.061 rad，取 game-feel 建议区间的低段
cam.transform.rotation = cameraRotation * Quaternion.Euler(2, 0, roll);
```

飞行游戏的视角 roll 比平台跳跃更容易引起不适，所以取 game-feel 建议的 `0.05–0.12 rad`（2.9°–6.9°）的下沿 3.5°，并默认 `shakeScale=.8`。

### 7.3 hit-stop 在 RIFT 里怎么做才不破坏确定性

game-feel 给的做法是 `Time.timeScale` + `WaitForSecondsRealtime`。RIFT 的约束是"模拟必须 dt 参数化、校验脚本用固定 dt 循环调用"，乍看冲突，其实不冲突：

- `Time.fixedDeltaTime` **不随 `timeScale` 变化**。timeScale 降低时，Unity 减少的是单位真实时间内 `FixedUpdate` 的**次数**，不是每次的**步长**。
- 所以 `Simulate(Time.fixedDeltaTime)` 收到的 dt 恒定，hit-stop 表现为"模拟步数变少"，物理积分完全不受影响。
- 校验脚本根本不走 `FixedUpdate`，它直接 `p.Simulate(.02f)` 循环，`timeScale` 对它无影响。

结论：可以安全使用 `Time.timeScale`，但**必须**用 `WaitForSecondsRealtime` 恢复（`WaitForSeconds` 在 timeScale 极低时几乎不推进，是 game-feel 点名的经典 bug），而且只给 large tier 事件用，每次冲击只触发一次。

### 7.4 分层表

tier 按 game-feel 的 small / medium / large 三档预设分配，trauma、hit-stop、粒子数直接取它给的区间。

| 事件 | tier | trauma | hit-stop | 粒子 | 音频 | HUD / 镜头 | 层数 |
|---|---|:---:|:---:|:---:|---|---|:---:|
| 命中敌机 | small | +0.14 | 无 | 4（已有 `Burst(...,4,.8f,false)`） | `hitSound` .2 | hitmarker：准星四角外弹 6 px，0.12 s ease-out 回位；伤害数字上浮淡出 | 5 |
| 精准命中（`SegmentDistance<1.4`） | small→medium | +0.22 | 无 | 8 | 更高音的确认音 | hitmarker 变金 + `×1.75` 角标 | 6 |
| 自己中弹 | medium | +0.35 | 无 | 4 | 新增低频 thud | `damageFlash` 改成**方向性**：从来袭方向的屏幕边缘渗红，0.3 s | 5 |
| 击破 `SalvageCore` | medium | +0.40 | 0.05 s | 24 | boom | `+8 SALVAGE` 数字 | 6 |
| **玩家击杀敌机** | large | +0.75 | 0.10 s | 36 | boom（去掉 350 m 距离门）+ 上扬确认音 | 中央 `SPLASH  Moth.exe` 0.9 s；kill feed 一条；FOV −4° 冲击 0.15 s 回弹 | **8** |
| 玩家被击杀 | large | +0.90 | 0.12 s | 36 | boom + 下坠音 | 全屏红闪 0.5；`KILLED BY <凶手>`；死亡镜头拉远到 40 m | 7 |
| 撞地自毁 | large | +0.85 | 0.10 s | 36（白色碎片，和被击落区分） | 低频撞击 | `TERRAIN IMPACT` | 6 |
| 拾取货物 | small | +0.08 | 无 | 0 | `collectSound` | CARGO 数字 pop：1.25× → 1.0，0.18 s 过冲回弹 | 4 |
| 占领完成 | medium | +0.30 | 无 | 12 | chime | `REFINERY 3 CLAIMED +25` toast；环闪一次归属色 | 5 |
| 存分 | medium | +0.20 | 无 | 8 | 上升琶音 | `+64 BANKED` 从 CARGO 位置飞到 BANKED 位置；BANKED 数字 pop | 5 |
| 导弹来袭 | — | 0 | 无 | 0 | 脉冲告警，重复频率随距离升高 | 屏幕边缘来袭方向红箭头 + 中央 `MISSILE` 闪烁 | 3 |

"玩家击杀敌机"这一行凑到 8 层，正好落在 game-feel 说的 5–8 层上限——因为它是这个游戏里玩家最想反复体验的瞬间。拾取货物只有 4 层且无 hit-stop、无 shake，因为它一局要发生几十次，按标准属于"routine action"，过度加料会掩盖真正的冲击。

另外要**拆开现在被复用的 `hitFlash`**：它同时表示"打中了"和"捡到了"，两个语义完全不同的事件共用一个白闪，是玩家读不出反馈的直接原因之一。拆成 `hitFlash` / `pickupFlash` 两个计时器。

### 7.5 缓动

Unity 没有内建 tween。RIFT 的 HUD 是 IMGUI 每帧重画，所以不需要协程：加一个 `float popTimer`，在 `Update` 里 `popTimer=Mathf.Max(0,popTimer-dt)`，在 `OnGUI` 里按 `k=1-popTimer/duration` 求过冲缩放，用 `GUI.matrix` 局部缩放那个数字。game-feel 的缓动对照表里，"UI pop in" 用过冲（`TRANS_BACK` 等价），"settle" 用 ease-out——数字弹出用过冲，血条/燃料条变化用 ease-out。一律不要线性。

### 7.6 无障碍

game-feel 明确要求出货这几个开关，放进暂停面板：`shakeScale`（0–100%，默认 80%）、`reduceFlashing`（把白闪换成静态淡色调）、`reduceCameraMotion`（关掉 roll 和 FOV 冲击）。

---

## 8. 最像半成品的三个地方

对照 fps-shooter 的核心循环、game-feel 的分层标准、game-ui-ux 的 HUD 架构，这三处是"技术上没错，但明显没做完"的重灾区。

### 8.1 击杀没有确认 —— 核心循环断在第四步

**为什么**：fps-shooter 的循环是"扫描 → 锁定 → 开火 → **确认击杀** → 重新占位"，并把 "distinct kill confirm" 列进 must-have #7。RIFT 在第四步整个断掉。

**证据**：`AerialCombatPrototype.cs:126-141` 的 `Kill(ArenaPilot victim, ArenaPilot attacker)` 里，`attacker` 只出现在 `attacker.kills++` 一行；`shake`、`damageFlash` 都包在 `if(victim==player)` 里，`boomSound` 还额外要求 `Vector3.Distance(victim.transform.position, player.transform.position) < 350`。玩家在 400 m 外把一架敌机打散，屏幕上**什么都不会发生**——排行榜要等到 `standingsRefresh` 的 0.5 s 周期才刷新数字。玩家甚至不能确定是自己打掉的还是对方撞山了。

**怎么改到位**：`Kill` 里加 `if(attacker==player)` 分支，按 §7.4 的 large 档触发全部 8 层：`trauma+=.75`、`HitStop(.10f)`、`Burst(pos,36,3,true)`、`boomSound` 去掉距离门、一条上扬确认音、中央 `SPLASH <victim.callsign>` 0.9 s、kill feed 推一条、`cam.fieldOfView` 瞬间 −4° 再 0.15 s ease-out 回弹。同时把 `standings` 的 0.5 s 刷新改成击杀时立即标脏，否则排行榜会比反馈慢半拍。

### 8.2 有占领机制，没有比赛 —— must-have #8 缺席

**为什么**：fps-shooter 把 objectives 定义为"除了射击之外你要做的事：清场、占领、生存"。RIFT 的 `CaptureGate.Tick` 已经实现了一套相当完整的占领规则（环内恰好 1 人才推进、1.4 s 满、首次易主 +25、cargo 全额转 score、≥2 人争夺时进度和存分都停、四色状态），但这套规则**没有终点**。

**证据**：`elapsed` 这个字段在 `Update` 里累加，全工程只有两个用途——`SalvageCore.Tick` 里驱动弱点脉动，和 `PlanetArenaHUD.cs:147` 的 `if(elapsed<18 || paused)` 控制操作提示显示 18 秒。没有任何代码读它来结束什么。玩家在第 5 分钟和第 1 分钟的处境完全一样：分数更高，但没有更接近任何东西。game-ui-ux 说 HUD 要回答"我现在处境如何"，RIFT 的排行榜只回答了"我第几名"，没回答"还剩多久"和"赢要多少"。

**怎么改到位**：WP-C 第 1–2 条。新 partial `PlanetArenaMatch.cs`，`MatchPhase{Countdown, Playing, Ended}`，`Playing` 300 s。`matchActive` 和 `paused` 并列，插进 `Damage`（`AerialCombatPrototype.cs:143`）、`Shoot`（同文件 :210）、`Collect`（:181）、`CaptureGate.Tick`（`PlanetArenaFlight.cs:180`）这四个已有的检查点——复用现成的暂停拦截位置，不新增散落的判断。HUD 顶部中央倒计时，最后 60 s 变红并每 10 s 一次心跳。`Ended` 时弹结算面板：排名 / score / kills / deaths / banked，Enter 重开（所有 pilot `Spawn(initial:true)`、清 cargo 与 score、重置所有 gate 的 `owner/claimant/progress`）。

**新增校验**（写进新的 `RiftMatchVerification`）：倒计时走到 0 后 `phase==Ended`；`Ended` 状态下 `Damage` 不扣血、`Shoot` 返回 false、`CaptureGate.Tick` 不推进 `progress`；重开后所有 pilot `Alive && cargo==0 && score==0` 且所有 gate `owner==-1`。

### 8.3 AI 不是对手，是另一个物种 —— must-have #6 只做了决策没做执行

**为什么**：fps-shooter 要求敌人 AI 走 `perceive → alert → attack → search` 四态，并明确要 cover 和 **reaction delays**。game-ai 的通用要求是 AI 和玩家共享同一套执行层，差异体现在参数而不是机制。RIFT 两条都没做到。

**证据**（数字全部来自代码）：

| | 玩家 | AI |
|---|---|---|
| 旋转 | `Simulate` 的 `isPlayer` 分支（`PlanetArenaFlight.cs:81-87`）：pitch 44°/s、yaw 27°/s、roll 85°/s，乘 `authority = Lerp(.4,1, Clamp01(speed/65) × density)`，再过一阶惯性 `Lerp(…, 1-Exp(-5dt))` | `PlanetArenaAI.cs:115`：`Quaternion.RotateTowards(rotation, desired, dt*80)` —— 全轴 80°/s，**无惯性、无空速衰减、无密度衰减**，直接写 `transform.rotation` |
| 瞄准 | 手动，`InterceptPoint` 只画一个前置量圆圈供参考 | `InterceptPoint` 精确解，`Angle(forward, aim) < 7°` 即开火，**零误差** |
| 反应 | 人类反应时间 | `NotifyAttacked` 直接 `decision=0`（`PlanetArenaAI.cs:21`），**零延迟**立刻转火 |
| 油门 | `throttle` 每秒最多变 0.35（`AerialCombatPrototype.cs` 的输入段） | `throttle = recover?1 : gateTarget?.42f : …`，**瞬时跳变** |

在 30 m/s 低速、高空低密度的时候，玩家的 `authority` 会掉到 0.4 左右，pitch 实际只有 17.6°/s，而 AI 始终是 80°/s——**4.5 倍**。玩家在缠斗里被咬住是结构性的，不是练习能解决的。

另外 `Search` 状态完全缺失：`FlyTactics` 第 38-39 行，一旦 `CanEngage` 失败或 `engagement` 归零就直接 `rivalTarget=null; combatRest=5;` 转去捡货。表现出来就是敌机在你眼前突然转身飞走，读起来像 bug 不像战术。

**怎么改到位**（WP-D 第 1–3 条）：

1. `FlyTactics` 的输出从"直接写 `transform.rotation`"改成"填 `controls`（pitch/yaw/roll 三轴 −1..1）"：把 `Quaternion.RotateTowards` 求出的 `desired` 转成三轴误差角，除以各轴上限再 `Clamp(-1,1)`。
2. `Simulate` 删掉 `if(isPlayer)` 判断，所有 pilot 走同一段积分。AI 自动继承 `authority`、惯性、密度衰减。
3. 三个技能参数按 id 派生：`aimJitter` 0–3°（叠加到 `InterceptPoint` 的结果上）、`reaction` 0.2–0.6 s（`NotifyAttacked` 不再立刻 `decision=0`，而是 `decision=reaction`）、`turnScale` 0.75–1.05（乘在 `controls` 上）。结算面板切难度 = 整体缩放这三个参数。
4. `tactic` 从 `string` 改 enum，加 `Search`：丢失目标后保留 `lastKnownPosition`，朝它飞 3 s，期间 `CanEngage` 一旦恢复立刻回 `Attack`，3 s 后才降级到 `Salvage`。

**新增校验**（加进 `RiftCombatVerification`）：固定 dt 跑 10 s，统计每个 AI 的逐帧角速度峰值，`Check(peak <= playerMaxRate * 1.05f, "AI shares the player rotation channel")`。同时既有的 "engages / retaliates / actual projectile hits" 三项仍须 PASS——AI 变钝后命中数会下降，允许把统计窗口拉长，但不允许把阈值降到 0。

---

## 9. 交付顺序

每个增量都是一个能独立说清楚、能独立评审的提交批次。

| 增量 | 内容 | 完成判据 |
|---|---|---|
| **1（已完成）** | 三个实体 dt 化 + 后处理开关与 HDR 重平衡 + 星球/环境光分层（WP-A 1–4，WP-B 前置） | 两个既有校验 PASS；`RiftVerification` 新增 "shard attraction" 两项；截图前后对比 |
| 2 | trauma 模型 + §7.4 全部分层 + hit-stop + hitFlash 拆分 + 无障碍开关（WP-B） | 击杀敌机 8 层反馈全部触发；`RiftCombatVerification` 的相机测试在 30/60/144 fps 下仍 PASS |
| 3 | `MatchState` + 倒计时 + toast + kill feed + 导弹告警 + 目标框 + 过热状态（WP-C） | 新增 `RiftMatchVerification` 三项全绿；`PLAYTEST.md` 增 "Match" 一节 |
| 4 | AI 走玩家旋转通道 + 技能参数 + `Search` 状态 + 散布/衰减/精准命中 + 重生安全（WP-D） | 角速度对等校验 PASS；既有交战三项仍 PASS；实机试飞确认缠斗可赢 |
| 5 | 站点差异化 + 双倍精炼事件 + 音频分层 + 尺度感与地标（WP-A 5–7，WP-E） | 两个校验 PASS；draw call < 1500；README 顶图更新 |

增量 1 已在本分支落地：`AerialCombatPrototype.cs`（后处理 / HDR / Trilight）、`PlanetArenaArt.cs`（五段地表分层）、`PlanetSurface.shader`（底光提亮 + 菲涅尔边缘光）、`PlanetArenaFlight.cs`（`SalvageCore` / `SalvageShard` / `ArenaDebris` 三个 `Tick(dt)`）、`RiftVerification.cs`（货物吸附与自动拾取两项确定性校验）。
