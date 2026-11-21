# RimTalk - Expand Toddlers（鼠蛋拓展）

给 RimWorld 的幼儿添些日常。小家伙会找玩伴、玩玩具、跟着大孩子去野游，大人也可以抱着他们到处走。原作由 changlusss 开发，Continue 版由 Nanaloveyuki 维护。

## 安装与换版

- 需要生物科技（Biotech）DLC、Harmony 和 Toddlers。把本模组排在 Harmony 和 Toddlers 之后。
- RimTalk 和 Interaction Bubbles 可选。启用其中任意一个时，把它排在本模组之前。
- 支持 RimWorld 1.6 和 1.7。1.7 持续维护，已搭配 Toddlers 1.6 验证启动。RimWorld 1.6 沿用原版。
- 从原版换到 Continue 版时，先备份存档，再停用原版并启用 Continue 版。读档会显示模组列表变更，确认后即可继续。两个版本包含同名 Def，须择一启用。
- RimWorld 1.7 首次载入 Continue 时，若新目录没有设置文件，会复制已安装原版的设置，包括独立 API 配置。已卸载原版时也会查找原 Steam 工坊目录 `3659064387` 对应的设置。RimWorld 1.6 或手动更名其他安装目录时，可在游戏关闭后把 `Config/Mod_<旧目录名>_ToddlersExpansionMod.xml` 复制为 `Config/Mod_<新目录名>_ToddlersExpansionMod.xml`，并保留已有的新设置。

幼儿起始年龄设置适用于采用标准婴儿、幼儿和儿童生命阶段的种族，包括符合条件的 HAR 种族。自定义婴儿生命阶段、儿童起始年龄或由其他模组管理的年龄表沿用其自身规则。

### 从源码部署

```bash
bash scripts/deploy.sh --rimworld-dir "/path/to/RimWorld"
```

脚本需要 Python 3.9 或更新版本。Windows PowerShell 可用 `python scripts/python/deploy.py` 并传入相同参数。在 RimWorld 1.7 环境下默认使用 .NET 10 SDK 构建，在 1.6 环境下则使用仓库中的程序集。两个版本都会打包到 `artifacts/rte_continue/release` 并安装到游戏的 `Mods/RTEContinue`。部署前请关闭游戏。

- `--build-only` 只构建和打包。加上 `--skip-build` 可以直接打包仓库中的程序集。
- `--preflight` 检查游戏路径和部署目标。`--target` 可以指定 `Mods` 下其他已安装的 Continue 目录。
- Harmony 或 Toddlers 装在其他目录时，用 `--property HarmonyModDir=路径` 或 `--property ToddlersDir=路径` 指定。

## 玩法

- **玩耍与陪伴。** 幼儿会自己玩、找同伴一起玩，也会玩玩具和观察大人工作。大人可以在旁边看幼儿玩耍。反复玩同类活动会腻，和同伴玩能缓解孤独。
- **抱起来玩。** 选中能抱孩子的成人，右键点击婴幼儿，选择「抱起」。抱着时仍可走动，也可以玩飞高高、转圈圈和挠痒痒。
- **儿童野游。** 附近的幼儿和儿童可能跟上自然奔跑的孩子。殖民地也会随机举办儿童野游聚会，可以在「野游设置」里调整频率和目的地。
- **照料与成长。** 幼儿会牙牙学语，也可能摔跤、和玩伴打闹，或偷吃零食。偷吃有机会蛀牙，和家人同房睡觉也会影响心情。启用 Dubs Bad Hygiene 后，符合条件的幼儿可以自己洗澡。
- **来访的孩子。** 商队、访客和过路者可能带着幼儿或儿童，年幼的孩子会跟随照看他们的大人。

## 对话与活动描述

启用 RimTalk 后，幼儿的语言学习进度会影响生成的对话，部分玩耍和照料互动也会触发对话。

在模组设置的「聊天和API」页，可以为玩耍活动的简短描述选用 RimTalk API，也可以配置独立 LLM API。没有可用的 API 时，游戏会显示固定活动文本。

