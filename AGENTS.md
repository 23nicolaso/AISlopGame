# AGENTS.md

This file provides guidance to AI coding agents (Claude Code, Codex, etc.) when working with code in this repository.

## 项目概况

**RIFT** —— 一个 Unity 小行星空战/打捞竞技场原型。Unity 工程在 `My project/`（注意路径带空格），编辑器版本 **6000.6.1f1**（已安装在 `/Applications/Unity/Hub/Editor/6000.6.1f1`），URP 17.6，新版 Input System。设计文档与操作说明在 `PLAYTEST.md`，Unity MCP（Codex 侧）接入说明在 `MCP-SETUP.md`。

整个游戏只有约 1300 行 C#：**所有资产（星球、飞机、残骸、精炼环、音效、贴图）全部在运行时用代码生成**，没有 prefab、没有导入模型、没有 Collider/Rigidbody。场景 `Assets/Scenes/SampleScene.unity` 只是 URP 模板 + 一个挂着 `AerialCombatPrototype` 的 `AERIAL COMBAT // GAME MANAGER` 空物体；即使没有这个物体，`Boot()`（`RuntimeInitializeOnLoadMethod`）也会自建一个。`Awake` 会禁用场景里所有 Camera/Light/AudioListener 并自建替代品。`Assets/TutorialInfo/`、`Readme.asset`、`InputSystem_Actions.inputactions` 都是 URP 模板残留，游戏代码不依赖它们（输入直接读 `Keyboard.current` / `Mouse.current`）。

## 常用命令

没有命令行构建脚本、没有 lint 配置、没有 EditMode/PlayMode 单元测试（`Assets/` 下无 `Tests/`、无 asmdef）。

- **打开工程**：用 Unity Hub 打开 `My project`，播放 `SampleScene`。
- **命令行编译检查**（Editor 未占用该工程时才能跑，否则会报 lock）：
  ```bash
  "/Applications/Unity/Hub/Editor/6000.6.1f1/Unity.app/Contents/MacOS/Unity" \
    -batchmode -nographics -quit -projectPath "/Users/lishuyu/Codes/AISlopGame/My project" -logFile -
  ```
- **验证（唯一的自动化检查）**：进入 Play Mode 后跑 Editor 菜单：
  - `Rift > Verify planetary arena` → `Assets/Editor/RiftVerification.cs`：人口数、密度/高度公式、死亡掉货/保分、玩家与 AI 重生、拾取去重、占领环存分、争夺阻断、暂停阻断伤害、扫掠碰撞、10 秒中性飞行不坠毁。
  - `Rift > Verify flip stability and rival combat` → `Assets/Editor/RiftCombatVerification.cs`：30/60/144fps 下相机翻滚无极点跳变、boost 相机缓动、AI 交战/报复、真实弹道命中、重生保护/冷却/星球遮挡、地形预测拉起。`RiftCombatVerification.Run()` 是 public static 返回字符串，可被 MCP 之类外部调用。
  - 两者失败即抛 `Exception("... CHECK FAILED: ...")`，成功 `Debug.Log("... PASS ...")`；跑完会 `Spawn` 所有 pilot 重置局面。**改了模拟逻辑后必须两个都跑一遍。**
- **截图辅助**：`Rift > Stage atmosphere screenshot` / `Stage suborbital screenshot` 摆好机位并 `EditorApplication.isPaused=true`，`Rift > Return to launch` 恢复。截图实际存放在 `Assets/Screenshots/`（`PLAYTEST.md` 里写的 `My project/Captures` 目录并不存在）。
- **Orbit Snake WebGL / itch.io**（与 `unity-flappybird` 同一套）：`scripts/build-webgl.sh` = 30 项校验 → `RiftBuild.OrbitWebGL`（编辑模式检查 + Gzip/回退 + `Assets/WebGLTemplates/Itch` 模板，日志末尾 `BUILD_AND_TESTS_PASSED`）→ `node scripts/check-webgl.mjs Builds/ORBIT-web` → `node scripts/check-webgl-render.mjs Builds/ORBIT-web`（无头 Chrome 真画一帧查像素；1.0.0 那个主光源失效的包就是这样漏出去的）；`node --test scripts/*.test.mjs` 测校验器。`.github/workflows/publish.yml` 在 main 上用 GameCI 构建并以 butler 推 `stevenli-phoenix-work/orbit-snake:html`（需 `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD`/`BUTLER_API_KEY` 四个 secret，由仓库 admin 设置）。本地构建成功、草稿创建、上传成功、公开页面能玩，是四个要分别验证的状态。
- **Unity MCP**：`Packages/manifest.json` 里依赖 `com.coplaydev.unity-mcp`（git main），Editor 内 `Window → MCP for Unity` 起 HTTP 服务 `http://localhost:8080/mcp`。`.codex/config.toml` 是 Codex 的项目级 MCP 配置——若想把它导入 Claude Code，让用户回复 `/import` 扫描，再 `/import --yes=<digest>` 应用；不要手工读写。

## 代码架构

### 两个 partial class 拆在五个文件里

| 文件 | 属于 | 内容 |
|---|---|---|
| `Scripts/AerialCombatPrototype.cs` | `AerialCombatPrototype` | 单例 `I`、星球常量与球面工具函数、Awake 建场、`BuildSites`/`Spawn`/`Kill`/`Damage`/`Collect`/`Shoot`/`AimTarget`/`InterceptPoint`、玩家输入（Update）、追尾相机（`UpdateChaseCamera`） |
| `Scripts/PlanetArenaArt.cs` | `AerialCombatPrototype` | 材质、程序化网格（`Hull`/`MeshPart`/`BuildShip`/`BuildGate`/`BuildCore`/`BuildPlanet`）、程序化音频 `Sound`、粒子 `Burst`、`Ring` |
| `Scripts/PlanetArenaHUD.cs` | `AerialCombatPrototype` | 全部 UI 走 IMGUI `OnGUI`，用 `GUI.matrix` 缩放到 1280×720 虚拟分辨率；排行榜、雷达、导航标记、准星/前置量、摇杆指示 |
| `Scripts/PlanetArenaFlight.cs` | `ArenaPilot` + 其余实体 | 飞行物理 `Simulate(dt)`、渲染插值；以及 `SalvageCore`（可击破残骸）、`SalvageShard`（被吸附的货物）、`CaptureGate`（占领/存分环）、`ArenaBolt`（子弹/导弹）、`ArenaDebris` |
| `Scripts/PlanetArenaAI.cs` | `ArenaPilot` | `FlyTactics(dt)`：目标选择（存分/修理 → 报复 → 主动交战 → 拾取 → 打残骸）、脱离、地形预测拉起、姿态求解、点射 |

`Assets/Editor/` 下两个校验脚本是独立静态类，只通过 `AerialCombatPrototype.I` 的 public 成员操作。

### 关键设计约束（改代码前先理解）

- **球面世界**：`PlanetCenter=(0,-1200,0)`，`PlanetRadius=1200`。"上"永远是径向 `Up(p)`，高度是 `Altitude(p)`，大气密度 `Density(alt)` 指数衰减，重力随距离平方衰减。位置用 `SurfacePoint(lat,lon,alt)` 描述。飞机每步做平行移动：`rotation = FromToRotation(oldUp,newUp) * rotation`。任何新实体/导航逻辑都要按这套球面坐标写，不能假设世界 Y 朝上。
- **模拟与渲染分离、dt 参数化**：所有游戏逻辑在 `FixedUpdate` 里调用 `ArenaPilot.Simulate(dt)` / `CaptureGate.Tick(dt)` / `ArenaBolt.Tick(dt)`，相机是 `UpdateChaseCamera(dt,pos,rot)`。这些方法是 public 且只依赖传入的 dt，**Editor 校验脚本直接用固定 dt 循环调用它们来做确定性测试**。新逻辑必须放进这些 dt 方法，不要塞进 `Update`/用 `Time.deltaTime`，否则校验失效。飞机的视觉子物体 `art` 与相机在 `LateUpdate` 里按 `SampleRenderPose` 在上一/当前 FixedUpdate 位姿间插值。
- **全局暂停**：`AerialCombatPrototype.I.paused` 是唯一暂停开关，每个组件的 Update/FixedUpdate/Tick 开头都要检查它；`Damage` 在 paused 时直接 return（有校验项覆盖）。
- **无物理引擎**：`Shape()` 创建 primitive 后立刻 `Destroy(Collider)`。子弹用 `ArenaBolt.SegmentDistance` 做扫掠线段检测防穿透；星球遮挡统一用 `VisibleBetween(a,b)`（线段到星心距离 > 半径+2）；货物 `SalvageShard` 自己找 48m 内最近的 pilot 飞过去，<12m 即 `Collect`。
- **弹道解算**：`InterceptPoint(p,target,targetVel,muzzleSpeed)` 在射手运动系里解二次方程，AI 和玩家的辅助瞄准/HUD 前置量共用它。AI 开火走 `Shoot(p,false,preferredTarget)` 显式指定目标，避免附近残骸抢走对空射击。
- **资源生命周期**：运行时 `new Material/Mesh/Texture2D/AudioClip` 一律 `owned.Add()`，`OnDestroy` 统一销毁。材质通过 `Shader.Find("Universal Render Pipeline/Lit|Unlit")` 和自定义 `Rift/PlanetSurface`、`Rift/Atmosphere`（`Assets/Shaders/`）获得；发光色直接用 >1 的 HDR 颜色值。
- **占领环规则**（`CaptureGate.Tick`）：环内恰好 1 个活着的 pilot 才推进 `progress`（1.4s 满），满后首次易主 +25 分并把 `cargo` 全部转入 `score`、缓慢回血；≥2 人在环内即"争夺"，进度和存分都停。颜色：青=中立、蓝绿=玩家、橙=AI、红=争夺。
- **分数模型**：`cargo` 是随身货物（死亡按最多 16 块散落），`score` 只在环里存入且死亡不掉。排行榜按 `score` 再按 `cargo` 排序，每 0.5s 刷新。

### 代码风格

现有 C# 刻意写得非常紧凑：一行多语句、`if(x)stmt;` 不加空格、字段合并声明、magic number 直接内联并在需要处加一行英文注释解释物理/设计意图。修改时沿用这种密度，不要把它"格式化"成常规风格造成大 diff。

## 文档维护

- 玩法/操作/校验项有变动时同步 `PLAYTEST.md`（它是面向玩家和评审的唯一说明）。
- 新增校验项时直接加到对应的 `Rift*Verification.cs` 里，并把检查名写进最后的 PASS 日志字符串。
