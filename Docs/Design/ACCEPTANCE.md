# 验收与实现映射

| 规则 | 预期代码位置 | 验收方式 |
| --- | --- | --- |
| 箭矢、经验与元素选择 | Core/RunModel、Elements；BulletSystem、GameHud | 既有 EditMode/PlayMode；本地点击验证 |
| 路线、控制抗性、出场 | Core/EnemyRules；EnemyView、GameConfig、EnemySystem | 路线与出场单元测试；场景观察 |
| 盾、护甲、回血打断 | Core/BossRules；BossDirector、EnemyView | 纯模型数值测试；本地 Boss 战 |
| 单 Boss 调度与召唤上限 | BossDirector、EnemySystem | 调度模型及场景测试；长局观察 |
| 云几何边界/单实例 | Core/EnemyRules；FogOfWar | 边界单测、重复施放和清理场景测试 |
| 隐藏敌人反馈与音效 | FogOfWar、FeedbackSystem、GameHud | 场景抑制计数断言；本地遮蔽视觉验收 |

必须本地执行：Unity 导入/编译和全部测试；盲射云内怪不出现血条、数字、碎片、声音定位及箭矢消失点；不同屏幕比例云不超顶部 1/3；暂停/升级冻结 Boss/云/召唤；击杀、失败、重开无残留；移动端性能和 Xcode 构建。

云环境只做结构检查、资源引用和文档一致性检查，不能据此宣称 Unity 已运行通过。已实现状态和具体测试记录随代码完成更新到 `Docs/TEST_REPORT.md`。
