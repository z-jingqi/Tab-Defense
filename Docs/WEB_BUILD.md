# WebGL 基线与微信小游戏

## 普通 WebGL 构建

1. 根据 `ProjectSettings/ProjectVersion.txt` 安装对应 Unity Editor 和 WebGL Build Support。
2. 打开主场景，运行测试并确认 Console 无错误。
3. 使用 `Tap Defense > Export WebGL Baseline` 导出至 `Builds/WebGL`。
4. 通过 HTTP 服务运行输出，检查触控、UI、音频首次交互、焦点切换、尺寸变化和重开。

此入口只构建普通 Unity WebGL 基线，不执行微信 SDK 转换，也不生成可发布的微信小游戏包。构建使用项目当前的渲染管线，不自动切换 URP。

命令行示例：

```bash
mkdir -p TestResults
"$UNITY_EDITOR" -batchmode -quit -buildTarget WebGL -projectPath "$PWD" \
  -executeMethod TapDefense.Editor.BuildAutomation.ExportWebGL \
  -logFile "$PWD/TestResults/webgl-build.log"
```

## 微信转换与发布

1. 选择微信官方支持的引擎版本与转换 SDK 组合，锁定版本；不要只根据“Unity 6”大版本推断所有小版本兼容。
2. 按所选 SDK 的正式文档安装转换工具、填写自己的 AppID 和包/资源配置。仓库不包含账号或凭据。
3. 通过 SDK 的构建/转换流程生成小游戏工程，在微信开发者工具中导入。普通 WebGL 导出成功不代表此步骤已成功。
4. 在 iOS 和 Android 的微信真机环境测试启动与资源加载、首次触控解锁音频、切后台/恢复、屏幕安全区、UI 按钮拦截、重开、Boss 云层遮蔽和持续战斗。
5. 分别测量包体、启动时间、峰值内存、帧时间和透明叠加；限制以所选 SDK/微信平台当前发布要求为准。

共同图形能力以精灵、普通粒子和简单材质为基础。不依赖 Compute Shader 或实验性 WebGPU；不要用原生 Metal 的能力推断微信运行时能力。Web 与原生分别配置视觉预算，但不得改变战斗规则。

官方入口：[微信转换 SDK](https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk)、[Unity Web 图形说明](https://docs.unity.com/zh-cn/engine/6000.3/manual/platform-specific/webgl/develop/graphics)。集成时以选定版本的正式支持矩阵与真机结果为准。
