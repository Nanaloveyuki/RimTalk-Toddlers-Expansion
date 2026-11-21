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
- 原作者的 README、系统设计文档、规划稿和待办保留原位。文档删除或归档待后续确认。

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

## 验证与取舍

- 先用 Harmony、Toddlers 和 Biotech 的最小环境启动 1.7。每批次用涉及的可选模组复测，覆盖缺席和启用两种情况。对话批次另测无独立 API 配置时的固定任务文本。
- 用原版 1.7 存档验证 Continue 版替换，以及抱着幼儿、进行野游或保有无聊度记录时的保存和重读。背负改动另测放下、离图与地图切换。涉及 1.6 共用内容时，用原版二进制做加载检查。
- 涉及 tick 的变更在同一存档与游戏速度下比较空闲殖民地和活跃幼儿场景，记录扫描次数、分配量和耗时。优先保持上游已有的工作量限制，再根据测量调整。
- 每批围绕一条可验证的玩法或初始化路径推进。全量重写会同时触及 XML 实例化、补丁顺序和存档格式，出现回归时难以定位。

## 原作者资料

- [幼儿背负系统](../ToddlerCarryingSystem.md)
- [Toddlers 工作体系架构分析](../Comprehensive_Toddlers_Job_Architecture.md)
- [儿童野游系统](../ChildrenOutingSystem.md)
- [幼儿无聊机制](../../plans/toddler_boredom_system.md)
- [ThoughtDef 问题分析](../../plans/bug_fix_thoughtdef_not_memory.md)
- [项目 README](../../README.md)与[原有待办](../../todolist.md)
