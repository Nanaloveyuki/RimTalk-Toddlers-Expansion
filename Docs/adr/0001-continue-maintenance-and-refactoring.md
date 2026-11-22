# ADR 0001 · Continue 版维护范围与重构顺序

- 日期 2026-11-21
- 状态 已接受
- 上游基线 `635417dd3184081332a4cc38f18b9b2e1b0118c0`

原作者已确认停止维护和发布。本 ADR 记录 Continue 版的维护范围、分步重构顺序和当前验证基线。

## 维护范围

- Continue 版使用 `cj.RimtalkToddlersExpansion.continue` 作为 RimWorld `packageId`。原版存档头记录旧包名，替换加载时会提示模组列表差异。设置文件按安装目录名和 `ToddlersExpansionMod` 类名查找，更换目录时需验证设置迁移。原版与 Continue 版包含同一套 Def，验证时只启用其中一个版本。
- RimWorld 1.7 是后续维护目标。RimWorld 1.6 保留基线中的原版 DLL 和 PDB，以及现有 1.6 加载入口。后续构建只面向 1.7，发布检查对照基线验证 1.6 二进制的哈希。
- 1.7 使用现有 Toddlers 1.6 DLL 作为依赖基线。本机 RimTalk 声明支持 1.7，运行时按启用状态接入，独立 LLM 和幼儿玩法可在没有 RimTalk 的环境使用。
- `LoadFolders.xml` 目前让两个版本共用根目录。修改共享 Def、XML 补丁、语言或资源时，先检查原版 1.6 程序集是否仍能加载对应内容。需要 1.7 专用类型或行为时，把受影响的内容放进对应的版本目录，并验证两边各加载一份 Def。
- 早期系统设计移至 `plans/archive/`。上游基线保留原作者的 README、`todolist.md` 和架构盘点。

## 实施顺序

1. **建立可复现的 1.7 基线。** 从 [项目文件](../../Source/RimTalk_ToddlersExpansion.csproj) 核对 .NET 10、游戏程序集、Harmony 和 Toddlers 的实际引用。先把构建输出放到临时目录，记录构建参数和核心玩法的现状。整理游戏通过 XML 实例化的类型、`defName`、翻译键、`Scribe` 键及默认值。每批重构保留其契约，涉及改名时单独验证存档迁移。
2. **梳理初始化和兼容补丁。** 从 [模组入口](../../Source/Core/ToddlersExpansionMod.cs) 到 [HarmonyBootstrap](../../Source/Harmony/HarmonyBootstrap.cs) 记录补丁注册、Def 加载完成后的初始化和 `GameComponent` 建立顺序。先以一组 RimTalk 补丁为试点，理清目标方法探测与注册的职责，保留已有注册顺序和失败处理。再按需处理 Hospitality、Dubs Bad Hygiene 和 Yayo，确保可选模组缺席或签名变化时，其他初始化仍能完成。
3. **厘清背负和照料的状态归属。** 以 [ToddlerCarryingTracker](../../Source/Integration/Toddlers/ToddlerCarryingTracker.cs) 为背负关系入口，由 [ToddlerCarryingGameComponent](../../Source/Integration/Toddlers/ToddlerCarryingGameComponent.cs) 负责存读档与调度。先从 Component 中理顺访客重新抱起与离图规则，再处理背负的 Job、保护 Hediff 和渲染同步。保持无背负关系时的快速返回、访客重试限额和原有 tick 间隔。将 [自助洗澡](../../Source/Integration/Toddlers/ToddlerSelfBathGameComponent.cs) 单独处理，沿现有入口拆开候选跟踪、设施匹配和 Job 分配。
4. **整理玩耍、对话与动画的调用边界。** 追踪 `JobDriver` 到 [ToddlerPlayRegistry](../../Source/Core/ToddlerPlayRegistry.cs)、无聊度 Component 和 [ToddlerPlayDialogueEvents](../../Source/Integration/RimTalk/ToddlerPlayDialogueEvents.cs) 的路径。先固定活动分类、完成时机和收益，再整理 RimTalk 对话事件、独立 LLM 文本和固定文本回退。动画单独处理，将 [YayoAnimationCompatUtility](../../Source/Integration/YayoAnimation/YayoAnimationCompatUtility.cs) 的反射调用、动画选择和状态清理按现有依赖拆开，保留无 Yayo 时的原生动画。
5. **按玩法分批整理剩余模块。** 分别处理访客及商队生成、儿童野游和设置 UI。先从 [TravelingPawnInjectionUtility](../../Source/Integration/Toddlers/TravelingPawnInjectionUtility.cs) 梳理组队资格、幼儿生成、亲属安排和库存调整的顺序。野游保持跟随自然奔跑和聚会野游各自的入口。设置页按现有分页逐步整理，保留已有键、默认值和应用时机。

## 第一批实现与验证基线

- 从 `8c42af4` 复现源码构建，使用 .NET SDK 10.0.401、RimWorld 1.7.5014 rev1195、游戏目录中的 Harmony 1.7 和 Toddlers 1.6。基线和本批变更的 Release 构建均为零警告、零错误。用 `GameVersion=1.6` 构建会在编译前报错。
- 根目录的 Def 与补丁引用 48 个本模组 CLR 类型，共有 112 条 `defName` 声明。按 Def 类型和名称组合核对标识。语言目录提供 1,300 条标识符，源码有 104 处 `Scribe` 读取和写入调用。此次对比了共享内容及序列化调用。
- 模组入口先加载设置、安装 Harmony 补丁，再从长事件回调应用 Def 设置并尝试注册组件。游戏创建或加载时也会发现 `GameComponent` 类型。RimTalk 的上下文、文本修复、对话与人格补丁按原顺序独立注册。继续使用原 Harmony owner 和 RimTalk 变量注册来源，提示词变量名为 `pawn.toddler_language` 与 `pawn.toddler_play`。
- Lead Your Pets 依赖背负相关完整类型名及 `TryMountToddler`、`DismountToddler`。重构背负时保留这两个入口。Prison Extra Expanded 的 `loadAfter` 引用原包名，其 Continue 版加载顺序需要在对应项目验证。

在仓库根目录运行以下命令，`RIMWORLD_DIR` 指向已安装的 1.7 游戏根目录。构建产物放在临时目录。

```bash
build_output=$(mktemp -d)
dotnet build Source/RimTalk_ToddlersExpansion.csproj -c Release -o "$build_output" \
  "-p:RimWorldDir=$RIMWORLD_DIR" \
  "-p:HarmonyModDir=$RIMWORLD_DIR/Mods/2009463077" \
  "-p:ToddlersDir=$RIMWORLD_DIR/Mods/2903359152"
```

在隔离的 1.7 游戏副本中通过两组启动检查。禁用 RimTalk 时跳过其集成，核心补丁和 Def 仍完成初始化。启用 RimTalk 时成功注册两个幼儿上下文变量，并通过 Harmony 检查确认五个目标方法的补丁均已安装。

用开发模式自动加载一份来自 1.7.5005 的原版存档副本。Continue 版进入地图后成功另存，存档清单写入新包名。两组幼儿和背负者的引用、当前 Job 及背负保护 Hediff 与原存档一致。互动式模组列表差异提示仍需单独验证。

## 首轮局部修复验证

- 在隔离的 1.7 游戏中保存并重读正在独玩、正在互玩和正前往玩伴的幼儿。核对任务阶段和剩余时间、动画、活动描述、结算、玩伴退出和失败冷却。用移除新字段的存档副本验证旧格式回退。旧档缺少互玩的起始娱乐值时继续结算，社交记忆仍按可用起始值和原版年龄规则判定。
- 用模拟响应验证读档后复用任务 ID、取消后重发以及旧响应延迟到达。Dubs Bad Hygiene 完整版和 Lite 分别验证原地长大、年龄退出、阵营未变化的俘虏与释放。最小环境也通过相同生命周期检查。
- 首次回归中，并发绘制同时创建动画 worker，触发游戏线程共享字典异常。将本模组使用的玩耍动画 worker 提前到 Def 加载后的初始化阶段创建，复测未再出现该异常。
- 在 RimWorld 1.7.5014 的 250 × 250 沙漠地图上，启用默认 16 个目的地池，用相同随机种子交替执行新旧聚会资格检查。只有一名儿童时，33 对调用均拒绝开始聚会，单次中位耗时从 6.73 ms 降到 8.1 µs，分配从 147,608 B 降到 216 B。两名合格儿童的 33 对调用均通过，耗时与分配基本持平。
- 同一张地图上，自然奔跑与聚会野游直接选点的中位耗时分别为 4.18 ms 和 4.96 ms。记录的是单次调用耗时。地标池在两条路径上都占有可测成本，可在后续优化时单独比较。

## 性能与维护验证

- 在同一份 250 × 250 野游存档重载后进行 37 组测量。提前结束资格检查，并在单次选点内按 `ThingDef` 复用地标关键词匹配结果。资格检查中位耗时从 4.34 ms 降到 44.7 µs，地标收集从 2.94 ms 降到 0.88 ms，资格检查加正式选点从 9.23 ms 降到 3.72 ms。在同一游戏会话用 121 个种子对照，五个目的地池、选中的格子和后续随机数逐次一致。17 个种子的地标候选格序列也一致。
- 在 250 × 250 的隔离地图上，48 名角色无访客时，同一 600 tick 边界的心情和离图维护中位耗时分别为 3.2 µs 和 4.4 µs，合计测量约 7.1 µs。加入 12 名有成人陪同的访客后分别为 3.5 µs 和 6.0 µs，合计 9.5 µs，分配 2,976 B。心情清理还要覆盖已经不符合访客条件但仍留有记忆的角色。按访客另维护候选集的收益有限，保留现有扫描。
- 12 名背负意愿候选在默认每 tick 遍历时约耗时 0.18–0.19 µs。12 名野游参与者的维护在常规 tick 和每 60 tick 的检查点分别约 2.13 µs 和 2.53 µs。保留现有频率。
- 在 RimWorld 1.7.5014 的隔离环境中，49 名角色的 Yayo 候选地图扫描中位耗时为 2.51 µs，配置命中约 5.9 ns，重建约 969 ns 并分配 2,296 B。对本机 Yayo 1.6 程序集的绘制状态读写分别约 64 ns 和 309 ns。实机验证离图角色及时移除，全图重扫移除其他地图的空闲角色，并保留仍在玩耍的角色。保留现有配置缓存和反射路径。
- 最小模组环境的 300 tick 采样中，`HostileTo` 两个重载共调用 854 次，`StartJob` 和 `GetCachedDriver` 分别调用 65 次和 73 次。单独测量现有 RTE 钩子时，常见分支耗时约 6–44 ns。背负 `DrawPos` 钩子被调用 20,515 次，单次约 8.4 ns。保留现有补丁点及其兼容性诊断。
- 启用 Dubs Bad Hygiene 1.7 和经 Waylith 兼容层加载的 Yayo Continued 1.6，实机验证抱持者引用、朝向、自助洗澡的位置与身体角度、衣服和帽子渲染标记。活跃背负的 `DrawPos` 约 122 ns，自助洗澡的位置、角度和绘制参数约 57、230 和 117 ns。Yayo 独玩 `CheckAni` 前缀约 2.71 µs，分配 200 B。
- 由游戏创建组件。实机分别新建游戏、加载包含两条旧空组件记录的存档，以及加载没有 RTE 组件记录的存档。三种路径均创建 8 个有效组件，各一份。迁移保留了旧档中已存的偷吃检查 tick，另存后移除了空组件记录。
- 在当地时间分别为 06:00 和 02:00 的两张已加载地图上验证偷吃调度，并切换当前地图和世界视图。验证旧档无聊度继续累计、内置与 XML 注册权重生效，以及移除空调用后抛高高和转圈的动画与冷却正常。

## 1.0.0 兼容性验证

- RimWorld 1.6.4871 rev590 使用冻结程序集进入地图。核对互玩 JobDef 和简体中文报告、幼儿寻找玩伴和离图抱起的补丁，以及婴儿睡眠点显示身体的补丁。
- RimWorld 1.7.5014 rev1195 由主菜单选择原版 1.7.5005 备份，经正常的模组差异弹窗继续读档。核对背负者、幼儿和当前任务，另存后存档头记录 Continue 包名，源备份哈希保持一致。
- 换安装目录的测试设置包含模拟的独立 API 类型、密钥、模型和 URL。旧文件成功复制并读回，目标目录已有设置时仍读取目标文件。在 HAR 实例种族上把阈值从 30 日改为 90 日，标准人类和标准 HAR 对照分别取 0.5 岁和 1.5 岁，自定义婴儿阶段的 HAR 种族维持 1 岁。
- 实机检查 RimTalk 的五个补丁和 Interaction Bubbles 共载入。模拟 RimTalk 一处上下文方法返回类型改变，剩余监听者上下文仍注册。Yayo Continued 1.6 经 Waylith 加载。Dubs Bad Hygiene 完整版与 Lite 均注册三处自助洗澡渲染补丁。Hospitality 1.2.0 的三处补丁已挂载，四参数床位评分保持幼儿 0 分和成人正常评分。
- Prison Extra Expanded 在 Continue 之后加载。Lead Your Pets 挂上 RTE 背负入口的前缀，实机验证抱起、背负者引用和放下。

## 验证与取舍

- 先用 Harmony、Toddlers 和 Biotech 的最小环境启动 1.7。每批次用涉及的可选模组复测，覆盖缺席和启用两种情况。对话批次另测无独立 API 配置时的固定任务文本。
- 用原版 1.7 存档验证 Continue 版替换，以及抱着幼儿、进行野游或保有无聊度记录时的保存和重读。背负改动另测放下、离图与地图切换。涉及 1.6 共用内容时，用原版二进制做加载检查。
- 涉及 tick 的变更在同一存档与游戏速度下比较空闲殖民地和活跃幼儿场景，记录扫描次数、分配量和耗时。优先保持上游已有的工作量限制，再根据测量调整。
- 每批围绕一条可验证的玩法或初始化路径推进。全量重写会同时触及 XML 实例化、补丁顺序和存档格式，出现回归时难以定位。

## 原作者资料

- [幼儿背负系统早期设计](../../plans/archive/ToddlerCarryingSystem.md)
- [儿童野游系统早期设计](../../plans/archive/ChildrenOutingSystem.md)
- Toddlers 工作体系架构分析见上游基线的 `Docs/Comprehensive_Toddlers_Job_Architecture.md`
- 幼儿无聊机制的早期设计和 ThoughtDef 问题分析见上游基线的 `plans/`。

当前玩家说明见 [README](../../README.md)，后续工作见 [todo.md](../../todo.md)。
