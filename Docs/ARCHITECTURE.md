# 项目结构与平台边界

## 模块职责

- `Assets/Scripts/Core`：纯 C# 战斗规则、成长、元素、敌人路线与 Boss 模型。不得引用 Unity 或平台 SDK。
- `Assets/Scripts/Runtime`：场景协调、输入、对象池、敌人/箭矢运行与 HUD。`GameController` 驱动战斗时间；暂停和升级时不推进战斗。
- `Runtime/Presentation`：可选的精灵动画数据与手动播放逻辑。视觉资源不决定伤害、碰撞半径或敌人速度。
- `PresentationSettings`：Web 与原生的视觉预算；只减少可丢弃的反馈对象、震屏和目标帧率，不能削减真实敌人或攻击。
- `Assets/Scripts/Editor`：验证与独立目标构建入口。不将 Editor API 引入 Runtime。
- `Assets/Data/GameConfig.asset`：战斗参数和视觉资源入口。资产引用采用已有 `.meta` GUID，不通过移动/重建文件破坏场景引用。
- `Assets/Tests`：规则/数据的 EditMode 测试与场景交互的 PlayMode 测试。
- `Tools`：结构检查和测试结果检查；输出写入忽略的 `TestResults/`，不要提交生成报告。
- `ArtSource`：可编辑源工程及其说明；Unity 仅导入 `Assets/Art` 中整理好的运行时资源。

## 平台策略

微信小游戏是主要发布目标，iOS App 为另一条发布路径。当前保持 Built-in 渲染基线，不把候选管线的选择与素材制作绑定。怪物精灵、透明序列帧、普通粒子和简单材质作为共同能力；不引入 VFX Graph、Compute Shader 或 WebGPU 的硬依赖。

若评估 URP + 2D Renderer，必须先锁定引擎/转换 SDK 版本，用代表性战斗场景完成微信出包、iOS/Android 微信真机和原生 iOS 对照。测量首包与启动时间、内存、透明叠加和满屏战斗帧时间，再决定迁移。不能把普通 WebGL 构建当作微信小游戏成品。

构建设置按目标配置；包名、版本号、签名、SDK 最低版本由发布者在 Player Settings 管理，构建命令不重置它们。微信 AppID、广告、支付、登录、存储和生命周期等平台适配应留在独立平台模块中，只有接入真实 SDK 时才添加实现，不在 Core 中放置空接口或模拟成功逻辑。

## 视觉预算

`GameConfig > Presentation` 默认 Web 目标 30 FPS、48 个基础反馈、24 个伤害数字；原生为 60 FPS、96/48。它们是可调整的起点，不是性能保证；反馈池同时受原有 GameConfig 池上限约束。Editor 默认使用原生预算，Web 构建使用 Web 预算；测试时可直接调整相应数据。

实际敌人、子弹、连射和连锁队列的容量仍由战斗配置控制，不能为了画质档位改变战斗结果。先削减透明面积、粒子数、数字与灯光，再依据真机数据调预算。
