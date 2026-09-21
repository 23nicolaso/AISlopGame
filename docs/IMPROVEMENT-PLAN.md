# RIFT 改进计划

分支：`feat/gameplay-improvements`（自 `main@4e7dd43` 切出）。日期：2026-09-20。

目标一句话：把"技术演示"变成"能玩 5 分钟、玩完想再来一局"的竞技场。三个衡量标准：**看得清、有输赢、打得爽**。

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

## 2. 阶段划分

优先级按"单位改动的体验收益"排：Phase 1 的后处理一行修复收益最大、风险最低，先做。

### Phase 0 — 基建（先于一切，半天）

1. **PlayMode 测试壳**：`Assets/Tests/PlayMode/Rift.Tests.asmdef` + 一个 `[UnityTest]` 包装现有 `RiftCombatVerification.Run()`；把 `RiftVerification.Verify()` 的主体也抽成 public `Run()`。CLI：
   ```bash
   "/Applications/Unity/Hub/Editor/6000.6.1f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics \
     -projectPath "/Users/lishuyu/Codes/AISlopGame/My project" \
     -runTests -testPlatform PlayMode -testResults /tmp/rift-tests.xml -logFile -
   ```
   Editor 菜单入口保留。
2. **dt 化剩余实体**：`SalvageShard.Tick(dt)`、`SalvageCore.Tick(dt)`、`ArenaDebris.Tick(dt)`，由各自 `FixedUpdate` 调用（行为不变）。新增校验项："shard 在 48 m 内 2 s 内被吸附并 `Collect`"。
3. **文档/元数据**：建 `CHANGELOG.md`（Keep a Changelog）、`productName=RIFT`、修正 `PLAYTEST.md` 截图路径。

DoD：CLI PlayMode 测试绿；两个既有校验 PASS 日志字符串新增 "shard attraction"。

### Phase 1 — 看得清（视觉可读性）

1. **开后处理**（1 行）：`cam.GetUniversalAdditionalCameraData().renderPostProcessing=true; .antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing`。Bloom 打开后现有 HDR 值会过曝，同步把 gold/teal/red 从 1.9~2.0 降到约 1.3~1.5 重新平衡。
2. **星球**：贴图基色整体提亮 2~3 倍并按噪声分层（深海 / 浅滩 / 陆地 / 雪线 4 档）；`PlanetSurface.shader` 加菲涅尔边缘光（limb）和淡淡的经纬网格线作为速度参考；`ambientMode` 改 `Trilight`（天青 / 地平橙 / 地面深蓝）。
3. **天空**：保留现有按高度 lerp 的背景色，加一个太阳圆盘（unlit HDR sphere，Bloom 会把它放大成光晕）。
4. **尺度感**：相机距离 20 → 14，飞机 art 放大 1.35×（只缩放 `art`，不动碰撞距离常量 4 m / 9 m）；每个站点周围散布约 30 个岩柱/信标塔（`Shape` 拉长 cube，alloy/dark 材质）；相机挂一个 `ParticleSystem` 做风粒子，emission 随 `player.Speed` 和 boost 增长。
5. **云**：每片云用 5~7 个不同尺寸的重叠 sphere 组成 cluster，替换单个压扁球。
6. **站点远距标识**：每个 refinery ring 加一根垂直光柱（unlit HDR 拉长 cube，高 400 m），配合 Bloom 在 2 km 外可见；颜色跟随 ring 的归属色。
7. **发光细节**：boost 时尾焰 `Exhaust` 沿 z 拉长 2.5×；tracer 宽度 .22 → .35。

DoD：两个校验 PASS（相机测试依赖 `cameraDistance`，改距离后重跑）；`Rift > Stage atmosphere screenshot` / `Stage suborbital screenshot` 前后对比图放 `docs/before-after/`；README 顶图更新。

### Phase 2 — 有输赢（比赛结构 + 反馈）

1. **`MatchState`**（新 partial 文件 `PlanetArenaMatch.cs`）：`Countdown → Playing(300 s) → Ended`。HUD 顶部中央倒计时，最后 60 s 变红 + 心跳音；`Ended` 时 `Damage`/`Shoot`/`Collect`/`CaptureGate.Tick` 全部阻断（复用 `paused` 的检查位置，加一个 `matchActive` 判断）。结算面板：排名、kills / deaths / banked、按 Enter 重开（对所有 pilot `Spawn(initial:true)`、清 cargo/score、重置 gate）。
2. **事件流 toast**：右侧中部最多 4 条、3 s 淡出。事件：击杀（含凶手/受害者）、占领（"Blue Finch claimed Refinery 3"）、存分（"+64 BANKED"）、玩家被击杀显示凶手。
3. **导弹告警**：`ArenaBolt.seeker && target==player.transform` 时 HUD 中央闪 "MISSILE" + 上升音调；给玩家躲避手段但不加新按键：boost 期间 seeker 的转向率 2.3 → 1.2 rad/s。
4. **货物风险提示**：`cargo ≥ 50` 时 HUD 的 CARGO 数字变金并脉动，导航标记优先指向最近 ring。
5. **目标锁定框**：`AimTarget` 命中的敌机画方框 + 血条 + 距离。
6. **校验**：新增 `RiftMatchVerification`：倒计时到 0 → `Ended`；`Ended` 下 `Damage` 不扣血、`Shoot` 返回 false；重开后所有 pilot `Alive && cargo==0 && score==0`。

DoD：三个校验 PASS；`PLAYTEST.md` 加 "Match" 一节。

### Phase 3 — 打得爽（AI 与飞行平衡）

1. **AI 走玩家同一套旋转通道**：`FlyTactics` 输出 `controls`（pitch / yaw / roll 三轴 -1..1），由 `Simulate` 里的 rates/authority 统一积分（去掉 `isPlayer` 分支的特判）。每个 pilot 加技能参数：`aimJitter` 0~3°、`reaction` 0.2~0.6 s、`turnScale` 0.75~1.05。校验：跑 10 s 统计 AI 角速度峰值 ≤ 玩家上限 × 1.05；既有"engages / retaliates / hits"三项仍 PASS（AI 变笨后可能需要放宽命中次数阈值，允许调）。
2. **玩家手感**：stick 居中时轻微自动改平（roll 向 up 收敛 ~15°/s）；鼠标 x 加 0.25 的 yaw 耦合（协调转弯），减少对 Q/E 的依赖。
3. **武器**：cannon 加 0.6° 散布；seeker 加 1.2 s 锁定（准星在目标上停留 → 锁定圆收缩 → RMB 才能发射），让 60 伤害有代价。
4. **重生安全**：3 个候选出生点，选距最近敌机 ≥ 300 m 的；`Kill(this,null)` 撞地用独立的白色碎片 + 低频音。

DoD：所有校验 PASS；用户实机试飞确认手感（校验证明不了"爽"）。

### Phase 4 — 玩法深度（stretch，用户看完 Phase 1–3 效果再定）

- 站点差异化：低轨高价值站（alt 450、value 24、空气稀薄机动差）vs 地表密集低价站。
- 定时事件：每 90 s 随机一个 ring 进入 "双倍精炼" 30 s，全场导航标记提示 → 人为制造冲突点。
- 音频：风噪随速度、锁定音、占领 chime、结算音。
- 难度选择：结算面板上切换 AI 技能档位。

---

## 3. 通用规则

- 每阶段改完 **必须** 跑 `Rift > Verify planetary arena` 和 `Rift > Verify flip stability and rival combat`，Phase 0 之后再跑 CLI PlayMode。
- 新逻辑全部放进 `Simulate(dt)` / `Tick(dt)` / `UpdateChaseCamera(dt,…)`，不进 `Update`。
- 沿用现有紧凑代码风格，不重排格式。
- 每阶段一个 PR，commit 用 `feat:` / `fix:` / `refactor:` 前缀；每阶段同步 `CHANGELOG.md` 和 `PLAYTEST.md`。

---

## 4. 风险与我可能忽略的点

- **Bloom 过曝**：HDR 1.9 的材质开 Bloom 后会糊成一片，Phase 1 第 1 步必须连着调色一起做，不能只开开关。
- **地形位移不做**：本来想用顶点噪声做山脉，但会破坏 `Altitude<4` 撞地判定和 AI `recover` 模型的球面假设。改用地面散布物达到同样的尺度感，碰撞模型不动。
- **相机拉近影响校验**：`RiftCombatVerification` 检查飞机在视口 0.3~0.7 范围内，改 `cameraDistance` 后要重跑并可能微调 `(0,5,-distance)` 偏移。
- **AI 变笨的连锁**：Phase 3 让 AI 走玩家通道后，"actual projectile hits" 校验的命中数可能掉到 0，需要把 `combatHits>0` 的判定改成在更长的模拟窗口内统计。
- **CLI 和 Editor 互斥**：Unity Editor 打开工程时 CLI 测试会报 lock，Phase 0 的测试脚本要在报错时给出明确提示。
- **性能**：`Burst` 每颗碎片 new 一个 GameObject；Phase 1 加粒子和散布物后 8 个 pilot 的场景预计仍然没问题，但要在 Profiler 里看一眼 draw call，超过 1500 就把散布物合并成一个 Mesh。
- **手感无法校验**：鼠标绝对位置 stick 是否顺手、自动改平强度是否合适，只能靠用户实机试；Phase 3 的这两项默认做成可关的常量。

---

## 5. Unity 6 最佳实践对照（2026-09-20 搜索）

只列和本项目有交集的条目，来源见文末。

| 官方建议 | 本项目现状 | 处理 |
|---|---|---|
| URP 下每个 Camera 必须带 `UniversalAdditionalCameraData`；脚本里用 `camera.GetUniversalAdditionalCameraData()` 取并缓存 | 运行时 `new GameObject().AddComponent<Camera>()`，从未取过该组件；`renderPostProcessing` 源码默认 false，截图也证实没有 Bloom | Phase 1 第 1 步：`Awake` 里取一次并设 `renderPostProcessing=true`、`antialiasing=SMAA`，引用存字段 |
| Test Framework CLI：`-runTests -testPlatform PlayMode -testResults <xml> -batchmode`；PlayMode 测试 asmdef 的 `includePlatforms` 必须是 `[]` 而不是 `["Editor"]`；生产代码一个编译错误就会导致 0 个测试运行 | 无 Tests、无 asmdef | Phase 0 第 1 步按此配置；脚本要检查 XML 里 `total="0"` 并报错 |
| 避免每帧分配：缓存 List、避免字符串拼接、`Shader.PropertyToID` 代替字符串、不用 LINQ | `OnGUI` 每帧拼十几个字符串；`CaptureGate.Tick` 每步 `SetColor("_BaseColor",…)` 走字符串；`Burst`/`SpawnShard` 每颗碎片 `CreatePrimitive` 再 `Destroy(Collider)` | Phase 0 顺手：`_BaseColor` 改 `PropertyToID`；HUD 字符串每 0.1 s 刷新缓存；碎片改 `UnityEngine.Pool.ObjectPool` |
| `GetComponent` 放 Awake，别放热路径 | `Shoot` 和 `ArenaBolt.Tick` 每次调用都 `GetComponent<ArenaPilot>()` / `<SalvageCore>()` | Phase 0：给 `ArenaBolt` 存类型化引用，`Shoot` 的 `preferredTarget` 改成传 `ArenaPilot`/`SalvageCore` 而不是 `Transform` |
| Update 里读输入、FixedUpdate 里应用，渲染插值 | 已经是这么做的（`Simulate(dt)` + `SampleRenderPose`） | 保持，Phase 0 把剩余三个实体也拉进来 |
| 集中式 Update 管理器减少 MonoBehaviour 调用开销 | 每颗 shard / debris / bolt 各自一个 `Update`/`FixedUpdate` | 碎片数量级 <200，暂不做；若 Phase 1 加粒子后 Profiler 显示脚本开销明显再集中 |
| GPU Resident Drawer：Forward+ + SRP Batcher + 大量同 mesh 的 MeshRenderer 时收益最大，只对静态 MeshRenderer 生效 | 站点散布物（Phase 1 第 4 步）正是"大量同 mesh 静态物体" | 先用 `Shape` 直接铺；draw call 超 1500 再评估开 Forward+ 与 GPU Resident Drawer，否则合并成单 Mesh |
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

## 6. 建议的第一个 PR

Phase 0 全部 + Phase 1 的第 1、2 步（后处理 + 星球提亮）。改动集中在 `Awake`、`BuildPlanet`、`PlanetSurface.shader` 和新建的 `Tests/`，风险最低、视觉收益最大，做完立刻能出前后对比图。
