# 测试与发布验收指南

## 结构与工具检查

```bash
python3 -m pip install -r Tools/requirements.txt
python3 Tools/validate_project.py
python3 -m unittest discover -s Tools/tests -v
bash -n Tools/run_unity_checks.sh
```

结构检查验证 `.meta`/GUID、场景和 Prefab 引用、战斗参数、平台视觉预算、程序集边界、目标构建入口及 C# 括号。它不是 C# 编译器，也不替代 Unity 测试。GitHub `Project structure` 工作流运行这些检查。

默认只输出到终端。需要报告时使用 `--output TestResults/structure.json`，输出文件不提交仓库。

## Unity 测试

以 `ProjectSettings/ProjectVersion.txt` 为 Editor 版本来源。打开 Test Runner 分别运行 EditMode、PlayMode；或设置 `UNITY_EDITOR` 后执行：

```bash
UNITY_EDITOR="/path/to/Unity" bash Tools/run_unity_checks.sh
```

脚本先验证项目，再分别运行两种测试并检查 XML；缺少结果、空结果、跳过用例或失败都视为失败。日志和 XML 在 `TestResults/`，不要把旧输出当成本次结果或提交版本库。

测试范围：

- 纯规则：成长、元素数量与冷却、攻击快照、路线、盾/护甲/回血、Boss 调度。
- 表现：精灵循环/单次采样、无效数据、手动时间、冻结/减速/失衡、对象池重置；Web/原生视觉预算独立性。
- 场景：输入、暂停/升级、重开复用、云层遮蔽、Boss/召唤、伤害管线。

## 代表性战斗验收

1. 持续至 Boss 出现、击杀和再次出现，确认技能轮换与召唤上限。
2. 云内不泄露本体、血条、数字、局部音效和箭矢消失点；不同屏幕比例和震屏下仍正确遮蔽，盲射伤害正常。
3. 暂停、升级和后台恢复不推进路线、技能、防御计时、云、连射和元素恢复；重开没有遗留状态。
4. 盾破不溢出、直接箭剥甲、灼烧不剥甲、实际本体伤害打断回血、冰风控制遵守抗性。
5. 用实际动画资源验证池复用首帧、冻结/减速、透明混合、pivot 与世界尺寸。
6. 低视觉预算可以丢弃装饰效果，但不得改变敌人、攻击、元素消耗或战斗结果。
7. 低/中/高负载记录启动时间、内存、帧时间、透明叠加；分别测试微信 iOS/Android 与原生 iOS，而不是只测 Editor。

目标平台构建见 [Web 与微信](WEB_BUILD.md)、[iOS 与 Android](IOS_BUILD.md)。测试结果应跟随实际构建的提交、平台和设备记录在发布验收系统中，而不是覆盖此通用指南。
