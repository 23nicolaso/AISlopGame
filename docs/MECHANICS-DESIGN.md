# RIFT 新机制设计 —— 让它从"能玩"变成"想再来一局"

日期 2026-09-20。前提：RIFT 的概念已定型（球面打捞竞技场、7 个具名 AI 对手、占领环存分），本文不讨论要不要做，只讨论**加什么机制**能让每一局产生真正的决策、冲突和高潮。配套的诊断与技术底座见 `IMPROVEMENT-PLAN.md`。

设计准绳来自三份 skill：`genres:fps-shooter`（完整战斗循环 = 目标、命中反馈、有感知状态的 AI、比赛结构）、`disciplines:game-feel`（一次命中 = 5 到 8 层反馈在 100 ms 内叠加，按事件重要性分档，juice 必须回到静止态）、`disciplines:game-ai`（决策/转向/寻路分层，FSM 的转移写在状态里）。

**RIFT 的独特性是球面 + 大气密度随高度指数衰减 + 重力随距离平方衰减。** 好的新机制应该踩在这三条物理规则上，做出别的空战游戏做不出来的决策。下面按"每行代码带来的乐趣"排序。

---

## 0. 现状的 TTK 与公平性（改机制前先看清数字）

| 项 | 现值 | 来源 |
|---|---|---|
| 机炮伤害 / 射速 | 12 / 0.12 s 冷却 ≈ 8.3 发每秒 | `Shoot()`、`ArenaBolt.Tick` |
| 过热 | 每发 +0.045，每秒 -0.22，0.92 锁枪 → 连射约 20 发 | `Shoot()`、`Simulate()` |
| 100 血理论 TTK | 9 发 = 0.96 s（100% 命中） | 推算 |
| 导弹 | 60 伤害，7 s 冷却，2.3 rad/s 转向 | `Shoot(seeker)`、`ArenaBolt.Tick` |
| AI 瞄准 | 零误差、零反应延迟、80°/s 全轴瞬时转向 | `FlyTactics` 第 115 行 |
| 玩家转向 | pitch 44°/s、yaw 27°/s、roll 85°/s，带 5/s 惯性和空速衰减 | `Simulate()` 第 83-86 行 |

结论：TTK 本身在竞技场射击的合理区间（fps-shooter 建议伤害×射速×血量三者一起调），**问题不是 TTK，是 AI 拿的是另一套飞行模型**。AI 的有效 TTK 约 1 s，玩家在缠斗里实际约 3 s。这是"打不过"感的根源，必须先修（见 §6），否则任何新机制都会被"AI 作弊"的体感盖掉。

---

## 1. 货物有重量（最小改动，最大决策）

**机制**：`cargo` 影响飞行。每 10 单位货物让有效重力 +4%、阻力 +3%，上限 +60%。满载时爬升明显吃力、转弯半径变大、boost 更珍贵。

**它制造的决策**：小额频繁存 vs 攒大票冒险。攒大票的人变成全场最肥、最慢的靶子（AI 的目标评分本来就按 `cargo` 加权，`FlyTactics` 第 63 行），击落一个满载对手会像打爆一只皮纳塔。这是"贪婪 vs 稳妥"的核心张力，现在的 RIFT 没有这个张力，因为背 200 货和背 0 货飞起来一模一样。

**实现**：`ArenaPilot.Simulate()` 里 `gravity` 和 `drag` 两行各乘一个 `1+Mathf.Min(.6f,cargo*.004f)` 和 `1+Mathf.Min(.45f,cargo*.003f)`。HUD 的 CARGO 数字在 ≥60 时变金色并加 "HEAVY" 标签。AI 侧：`FlyTactics` 的存分阈值 `cargo>=35` 改为按个性（§7）浮动。

**校验**：同一初始状态，cargo=0 与 cargo=150 各 `Simulate` 5 s，后者高度损失更多、速度更低。

## 2. 易爆残骸（涌现式击杀工具）

**机制**：每个站点 4 个残骸中有 1 个是 **Volatile**（反应堆用红紫色 HDR，`weakPoint` 脉动更快）。被打爆时对 45 m 内所有 pilot 造成 55 伤害并给 0.6 的 trauma。掉落 1.5× 货物。

**它制造的决策**：追我的人跟得太紧？把他引过易爆残骸，一发机炮点燃。想要它的货？先确认没人在附近，或者接受被炸的风险从 300 m 外打。AI 也懂：`coreTarget` 选择时若自己离 Volatile < 60 m 则不打它（否则 AI 会自杀）；"Vulture" 个性（§7）会主动在有对手靠近时引爆。

**实现**：`SalvageCore` 加 `bool volatile`；`BuildSites` 里 `c==2` 的那个设为 volatile，`BuildCore` 换材质；`SalvageCore.Hit()` 血量归零时若 volatile：遍历 `pilots` 距离 < 45 → `Damage(p,55,shooter)`，`Burst(pos,40,4,true)`，音效用 `boomSound` 但 pitch 0.7。

**校验**：一个 pilot 放在 volatile 残骸 30 m 内，另一个从 200 m 外打爆它，前者掉血 55、后者不掉。

## 3. 占领环产生持续收益（把"占领"变成真正的目标）

**机制**：`CaptureGate.ownerAge` 现在只写不读。让它工作：环被持有的每 12 s 给持有者 +5 分（不经过 cargo，直接进 score，死亡不掉）。HUD 排行榜旁显示每人持有的环数。

**它制造的决策**：现在占领只值一次性 25 分，之后没人在乎谁的环。改完后，持有 3 个环的人每分钟白拿 75 分，其他人必须去抢——这就是 fps-shooter 说的"除了射击你还要做什么"：clear / capture / survive 三件事 RIFT 现在只有 capture 的一半。抢环时 ≥2 人在环内就"争夺"（现有规则），所以防守方需要**先把进攻方打出环**，这直接把战斗和目标绑在一起。

**实现**：`CaptureGate.Tick()` 里 `ownerAge` 累积到 12 → 归零、`pilots[owner].score+=5`、环闪一下（`progressRing` 宽度脉冲）。排行榜按 `score` 排序不变。

**校验**：`gate.owner=0; gate.Tick(12.1f)` 后 `player.score` +5。

### 3.1 两条高度带（WP-E 落地，2026-09-21）

上面这套持续收益原本作用在 6 个同构站点上，所以"去哪个环"是个没有内容的问题。现在站点分成两带，`BuildSites` 按 `s%2` 分叉：

| | 地表站（0/2/4） | 低轨站（1/3/5） |
|---|---|---|
| 环高度 | 170 m | 460 m |
| 残骸数 | 5 | 3 |
| 单个 value | 8 | **24** |
| 易爆 | c==2 | c==2 |
| 装甲 | c==3 且 `s%4==0`（1 号、5 号站） | 无 |
| 坞灯 / 雪佛龙 | 青 / 金 | 金 / 青 |

残骸总数仍是 24（3×3+3×5），所以不是"把竞技场做大"，而是把同样的量做得不对称。三重叠加让低轨成为真正的高风险高回报：单个 24 分、越过海岸线后拾取再翻倍（§ 悬浮打捞规则）、而空气稀薄意味着 `Density` 衰减下的升力与操纵权限都在掉——抢到手之后还得把重载的机体飞回环里存分，而 §1 的货物重量正好在这一段狠狠收税。

装甲残骸只放地表站，是因为它的定价对象是导引头（§10），而导引头需要有空间跑一趟攻击航线；把它放在空气稀薄、机动受限的低轨只会变成"必须打但打不动"。

**对 AI 的预期副作用**：`FlyTactics` 的 `coreTarget` 只按距离选，但 `AimTarget` 与拾取收益都读 `core.value`，所以贪婪型个性会被 24 分拖上低轨。这是要的效果——高价值空域自我组织成冲突点，和 §4 过载事件是同一个思路的静态版本。§7 的 `bankAt` 阈值不需要改。

**校验**（`RiftVerification`）：残骸总数仍 24；每个 core 的 `value` 等于 `site%2==1?24:8`；每站残骸数等于 `s%2==1?3:5`；`gates[s].lowOrbit` 与高度带一致；1 号环高度 > 400 m 而 0 号 < 300 m。

## 4. 过载事件（制造全场高潮）

**机制**：比赛进行到每 75 s，随机一个当前**无人持有或非领先者持有**的环进入 **OVERCHARGE** 30 s：存分 2×，环变紫色，从全球任何位置 HUD 都显示一个紫色导航标记和倒计时，所有 AI 的决策权重把它当作 cargo≥35 的存分目标。

**它制造的决策**：全场 8 架飞机同时飞向一个点 = 混战。带着货冲进去存 2× 还是在外围截杀带货冲进去的人？这是"节奏"——现在的 RIFT 是平的，没有波峰。fps-shooter 把这类 objective 列为必须项而非可选项。

**实现**：新 partial 文件 `PlanetArenaMatch.cs` 里的 `MatchTick(dt)`（由 `FixedUpdate` 调，检查 `paused`）。`CaptureGate` 加 `float overcharge`，`Tick` 里 `occupant.score+=occupant.cargo*(overcharge>0?2:1)`。颜色分支加一档紫。`FlyTactics` 的 `gateTarget` 选择：若存在过载环且自己 cargo≥15，优先它。

**校验**：`MatchTick(75.1f)` 后恰好一个 gate 的 `overcharge>0`；在过载环存 40 货得 80 分。

## 5. 反馈层（让每一次命中和击杀"读得出来"）

按 game-feel 的分档，RIFT 的事件分成三档，**每档的反馈是固定套餐**，用一个 `Feedback(tier,pos)` 函数统一发，不再各处零散 `shake=.35f`：

| 档 | 事件 | 套餐（100 ms 内全部触发） |
|---|---|---|
| small | 打中残骸、拾取货物 | 准星白闪 0.12 s，`hitSound` 0.2 音量，trauma +0.12 |
| medium | 打中敌机、自己被打中 | 准星白闪 + 命中方向指示（被打时 HUD 边缘红色楔形指向攻击者），trauma +0.35，`hitSound` 0.35，粒子 6 |
| large | 击杀、被击杀、易爆残骸爆炸、占领易主 | **hit-stop 0.08 s（timeScale 0.05）**，trauma +0.8，白闪 0.06 s，粒子 30，`boomSound`，击杀横幅 |

关键实现点：

- **trauma 取代线性 shake**：`shake` 改名 `trauma`，只加不设（`trauma=Mathf.Min(1,trauma+x)`），每帧 `trauma-=dt*1.4`，实际抖动量 = `trauma*trauma`（小击几乎不动，大击猛）。现有的 Perlin 采样保留，它已经是"平滑噪声而非每帧随机"的正确做法。
- **hit-stop 必须 pause-safe、dt-safe**：不用协程。`AerialCombatPrototype` 加 `float hitStop`；`Update()` 里 `hitStop-=Time.unscaledDeltaTime; Time.timeScale = paused?0:(hitStop>0?.05f:1)`；`OnDestroy` 恢复 `timeScale=1`。`FixedUpdate` 按 scaled time 走，所以 hit-stop 期间模拟自然停顿，dt 模型和校验脚本都不受影响（校验脚本直接调 `Simulate(dt)`）。**只在玩家参与的 large 事件触发**，AI 之间互杀不触发。
- **击杀横幅**：右上排行榜下方，"SPLASHED  Moth.exe   +38 SALVAGE" 大字 1.5 s，用 ease-out 从 1.3× 缩到 1×（IMGUI 用 `GUI.matrix` 缩放即可）。被击杀时 "SPLASHED BY  Kite-09"。
- **死亡镜头**：玩家死亡后 1.2 s 内 `Time.timeScale=.35`，相机不 Snap，`cameraRotation` 缓慢 slerp 朝向凶手方向，然后正常重生。凶手在这 1.2 s 里必须可见——这就是 fps-shooter 说的"死亡必须瞬间读懂"。
- **命中方向指示**：`Damage()` 里记录 `lastAttackDirection`，HUD 在屏幕边缘画一个红楔形，1 s 淡出。现在被打了只有全屏红闪，不知道从哪来，这是最影响"公平感"的缺失。

## 6. AI 用玩家的飞行模型 + 感知模型（对手从"作弊的"变成"活的"）

**6a. 同一套旋转通道。** `FlyTactics` 不再直接 `RotateTowards(desired,80°/s)`，而是输出 `controls`（三轴 -1..1），由 `Simulate()` 的 `rates/authority` 统一积分，去掉 `if(isPlayer)` 分支。姿态求解：把 `desired` 转到本地系，`controls = clamp(localEuler / 30°)`。这一步做完，AI 在稀薄空气里同样转不动，满载同样迟钝（§1 对 AI 也成立）。

**6b. 感知锥。** 现在 `CanEngage` 是"范围内 + 无遮挡"就看见，360° 全知。改成：
- 前方 110° 锥内、950 m 内、无遮挡 → **看见**。
- 锥外但 250 m 内 → 看见（近距离余光）。
- 锥外、远处，但对方在过去 1.5 s 内开过火且 600 m 内 → **听见**，进入 Alert 而不是直接 Engage。
- 被打中且攻击者不在锥内 → Alert + `searchDirection = lastAttackDirection`。

**6c. 状态机显式化。** 现有的 `tactic` 字符串已经是一个隐式 FSM，把它变成枚举 `Salvage / Collect / Bank / Alert / Engage / Extend / Search / Recover`，转移写在各状态里（game-ai 的原则：转移逻辑在状态内，不散落 if）。新增 **Alert**（0.4-0.8 s 反应延迟，按个性，期间不开火只转向）和 **Search**（朝 `searchDirection` 做一个 3 s 的翻滚扫视，找不到回 Salvage）。

**它制造的决策**：从后上方接近对手的盲区变成真实战术。第一发打中后对方有 0.4-0.8 s 反应窗口，够玩家再打 4 发。反过来玩家也会被从盲区偷袭，方向指示（§5）让这变成可以学习的东西而不是随机死亡。

**校验**：现有"engages / retaliates / hits"三项保留但把命中数阈值放宽到"650 步内 ≥1 次"；新增"AI 在玩家从其正后方 400 m 接近且未开火时，`CombatTarget==null`"。

## 7. 七个对手七种个性（让排行榜上的名字有意义）

用 `id` 索引一张只读参数表，字段：`aggression`（主动交战距离 350-800）、`bankAt`（存分阈值 20-80）、`aimJitter`（0.5°-3°）、`reaction`（0.3-0.9 s）、`greed`（目标评分里 cargo 的权重 0.5-2.5）、`revenge`（报复时长 5-15 s）。

| 呼号 | 原型 | 关键参数 |
|---|---|---|
| Moth.exe | 猎手 | aggression 800、reaction 0.3、jitter 0.8° —— 全场最危险 |
| Blue Finch | 囤积者 | bankAt 80、aggression 350 —— 很少打架、经常满载、最肥的靶子 |
| Periapsis | 秃鹫 | greed 2.5、会主动引爆 Volatile —— 专挑带货的打 |
| DustRunner | 稳健 | bankAt 25 —— 小额频繁存、分数稳步涨、很难被拉开 |
| Kite-09 | 复仇者 | revenge 15、reaction 0.9 —— 反应慢但记仇 |
| SoupDragon | 新手 | jitter 3°、aggression 450 —— 给玩家的"第一个击杀" |
| Last Comet | 王牌 | jitter 0.5°、reaction 0.35、bankAt 50 —— 综合最强 |

排行榜旁边给每个名字一个 4 字标签（HUNTER / HOARDER / VULTURE / …），玩家第二局就会开始"先打 Blue Finch，躲着 Moth.exe"。

## 8. 王牌连杀与悬赏（自动橡皮筋）

3 连杀不死 → **ACE**：曳光变金、存分 +50%、全场 HUD 显示 "BOUNTY: <呼号>"、所有 AI 的 `greed` 对该目标 ×2。死亡即清除。AI 也能成为 ACE。

领先者被围攻、落后者有翻盘路径，不需要任何显式难度调节。实现只要一个 `streak` 计数和 `Shoot`/`CaptureGate.Tick` 里各一处判断。

## 9. 再入过热（RIFT 独有的高风险高回报）

**机制**：高度 > 550 m（现在 HUD 已经叫 SUBORBITAL COAST）拾到的货物 2× 计值。但存分环全在 170-460 m。**下降时** `hullHeat += 垂直速度 × 密度 × 0.004 每秒`，超过 1 每秒掉 8 血，HUD 显示红色热量条和 "RE-ENTRY" 警告。缓慢盘旋下降不发热，带着满载货物垂直俯冲回去会烧穿。

**它制造的决策**：上去很慢（对抗重力、空气稀薄推力效率低），下来要么慢要么烫。这是只有球面 + 指数大气的游戏才能做的机制，也是 RIFT 的"签名"。上层的残骸站（`s%2==1` 那三个，高度 450）现在没人特别想去；改完后它是一条完整的"远征"路线。

**校验**：从 900 m 以 -120 m/s 垂直速度下降 4 s，`hullHeat>1` 且掉血；以 -25 m/s 下降 4 s 不掉血。

## 10. 装甲残骸（导弹的经济用法）

每站 1 个 **Armored** 残骸：血量 200（普通 65），机炮伤害对它 ×0.3，导弹全额。掉落 3× 货物。

决策：7 s 冷却的导弹留着打人还是打它？现在导弹只有一个用途。实现只在 `SalvageCore.Hit()` 里分支。

---

## 实施顺序（按依赖，不按"先易后难"）

1. **§5 反馈层 + §6a AI 同飞行模型** —— 没有这两个，其他机制的体感都会失真。`Feedback()` 函数是后面所有机制的输出口。
2. **§1 货物重量 + §3 环收益** —— 各十几行，立刻让"存不存"和"守不守"变成决策。
3. **§7 个性表 + §6b/6c 感知与状态机** —— 让对手活起来。
4. **§4 过载事件 + 比赛结构（5 分钟、结算、重开）** —— 节奏与闭环。这里合并 `IMPROVEMENT-PLAN.md` 里的 MatchState 设计。
5. **§2 易爆 + §10 装甲 + §8 王牌** —— 战术工具与橡皮筋。
6. **§9 再入过热** —— 最后做，因为它最需要实机调平衡。

每一步都在对应 `Rift*Verification.cs` 里加校验，都要 `-batchmode -nographics -quit` 编译通过再提交。

## 我可能忽略的

- 货物重量对 AI 的 `recover`（地形预测拉起）有影响：满载 AI 拉起能力下降，`Altitude<85+descent*1.8f` 的阈值可能要按重量放大，否则会出现满载 AI 频繁撞地——这反而是"贪婪的代价"的合理体现，但要看实际频率，撞地率 > 1 次/分钟就调阈值。
- hit-stop 期间 `Time.timeScale=.05`，`AudioSource` 默认不受 timeScale 影响，但 `engineSource.pitch` 是每帧按速度算的，会瞬间不变——可以接受，甚至可以在 hit-stop 时故意把 pitch 压低 0.3 做"时间凝滞"音效。
- 过载事件如果选中的环离所有人都 > 1.5 km，30 s 不够飞到。选择时排除"离最近 pilot > 1200 m"的环。
- 再入过热与现有的武器 `heat` 变量同名冲突，命名为 `hullHeat`，HUD 上两条热量条要有明显不同的颜色和位置。
- 七个个性参数会让"AI 校验"里的命中次数进一步波动，校验里用 `pilots[1]`（Moth.exe，猎手）跑战斗测试，用 `pilots[6]`（SoupDragon，新手）跑"不应命中太多"的反向测试。
