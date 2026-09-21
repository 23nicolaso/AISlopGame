# Changelog

本项目所有值得记录的改动都写在这里。

格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Added

- `docs/IMPROVEMENT-PLAN.md`：把改进计划从"分阶段修补"重写为五个工作包的成品打磨框架，新增对照 fps-shooter 八项必备系统的完整度差距分析、按 game-feel 数值标准设计的反馈分层表，以及三处"最像半成品"的定位与整改方案。
- `PlanetSurface.shader`：菲涅尔边缘光，低空飞行时星球轮廓能从黑天空里分出来。
- `RiftVerification`：两项确定性校验——货物被最近的 pilot 吸引、进入拾取半径后自动 `Collect`。PASS 日志串加入 `shard attraction`。
- 本文件。

### Changed

- 运行时相机接入 URP 后处理：缓存 `UniversalAdditionalCameraData`，开启 `renderPostProcessing` 与 SMAA（High）。场景里的 Global Volume（Bloom / Neutral Tonemapping / Vignette）从此对游戏相机生效。
- 重新平衡发光材质的 HDR 值（teal / red / gold / white / violet），从 1.9–2.0 压到 1.14–1.5，避免 Bloom 阈值 1 之上的超额亮度糊成白团。
- 星球表面贴图从"海洋 / 陆地"两档改为五档（深海 / 浅滩 / 滩涂 / 陆地 / 山脊雪线）并整体提亮；`PlanetSurface.shader` 的夜侧底光从 `.32` 提到 `.44`。
- 环境光从 `Flat` 改为 `Trilight`（冷青天空 / 深蓝赤道 / 暖橙地面），机身着色有了上下方向感。

### Refactored

- `SalvageCore`、`SalvageShard`、`ArenaDebris` 从 `Update() + Time.deltaTime` 改为 `FixedUpdate()` 调用 public `Tick(float dt)`，与 `CaptureGate` / `ArenaBolt` 保持一致，Editor 校验脚本得以用固定 dt 循环做确定性测试。行为不变。
