# iPhone 原生构建与 Xcode 安装

使用对应版本的 Unity iOS Build Support 导出 Xcode 工程，再在 Mac/Xcode 中完成签名、安装与归档。

## 导出

1. 在 Mac 安装 Apple Xcode，并完成首次启动、许可接受和平台组件安装。
2. 在国际版 Unity Hub 为 `6000.3.25f1` 安装 iOS Build Support。
3. 用该 Editor 打开 `TapDefense`，确认 Console 没有编译错误。
4. 在 Player Settings 填写自己的应用标识符、版本、构建号和目标系统要求，再执行 `Tap Defense > Configure > iOS`，配置竖屏、IL2CPP 与 Metal。
5. 在 `File > Build Profiles` 添加 iOS Profile 并切换到 iOS，等待重新导入。
6. 运行 Test Runner 的 EditMode 和 PlayMode 测试，再执行 `Tap Defense > Export iOS Xcode Project`。
7. 在 `Builds/iOS` 打开生成的 `Unity-iPhone.xcodeproj`。若实际导出包含 `.xcworkspace`，以 workspace 为准。

导出前检查 iOS 模块与项目配置，随后仅应用 iOS 的共享图形/脚本设置。菜单不会重置包名、版本、构建号、签名或最低系统版本；这些值在 Player Settings 中管理。不要使用原型默认标识符发布正式产品。

## 安装到 iPhone

1. 用数据线连接 iPhone，信任 Mac；在系统要求时启用开发者模式。
2. 在 Xcode 的 `Unity-iPhone` Target 打开 `Signing & Capabilities`。
3. 选择自己的 Apple Development Team，启用自动签名，并设定可用的 Bundle Identifier。
4. 在运行设备处选择真实 iPhone，执行 Product > Run。
5. 验证竖屏、刘海／灵动岛和 Home Indicator 区域、单指点按、音效、切后台与恢复。
6. 持续游玩并验证经验升级；主动漏过敌人，确认屏幕底部扣血、血量归零失败和重开。

签名证书、Team、设备授权由你的 Apple 开发环境提供，工程不包含这些私有内容。商店发布、TestFlight、图标和发布元数据不在本阶段交付内。

## 命令行导出

在项目目录执行，路径按安装位置修改：

```bash
UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity"
mkdir -p TestResults
"$UNITY_EDITOR" -batchmode -quit -buildTarget iOS \
  -projectPath "$PWD" \
  -executeMethod TapDefense.Editor.BuildAutomation.ExportIOS \
  -logFile "$PWD/TestResults/ios-export.log"
```

需要归档时，在完成 Xcode 签名配置后执行，例如：

```bash
xcodebuild -project Builds/iOS/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone -configuration Release \
  -destination 'generic/platform=iOS' \
  -archivePath Builds/TapDefense.xcarchive \
  -allowProvisioningUpdates archive
```

## Android 次目标

1. 安装同版本的 Android Build Support、SDK/NDK/OpenJDK。
2. 使用 `Tap Defense > Build Android APK` 构建。
3. 在 Android 真机安装 `Builds/Android/TapDefense.apk`，完成同一份验收清单。

默认 Android 最低 API 26、ARM64、IL2CPP、OpenGLES3。目标 API 由已安装 SDK 的自动设置决定，发布前需重新确认商店要求。原生与微信均需使用各自运行时完成设备验收。
