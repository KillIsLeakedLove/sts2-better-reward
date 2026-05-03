# sts2-better-reward

Slay the Spire 2 的轻量玩法 Mod，用于提升战斗资源并强化精英与 Boss 战后的奖励。

## 实际修改内容

启用此 Mod 后：

- 所有角色的战斗最大能量额外增加 `1` 点。
  - 当前实现通过补丁 `Hook.ModifyMaxEnergy`，在原结果基础上执行 `+1`。
  - 通常表现为默认 3 点能量变为 4 点；如果其他角色、遗物或 Mod 已经改变最大能量，本 Mod 会在最终结果上继续追加 1 点。
- 击败精英后，奖励列表额外追加 `1` 个随机奖励选项。
- 击败 Boss 后，奖励列表额外追加 `1` 个随机奖励选项。
- 额外奖励选项会从以下 5 类中稳定随机抽取 1 项：
  1. 100 金币
  2. 1 个随机遗物
  3. 选择并移除 1 张卡
  4. 选择并升级 1 张卡
  5. 生命值上限 +6
- 奖励列表保存并重新加载后，已经掉落的额外奖励不会重新随机。
- 普通房间、事件房间等非精英/非 Boss 房间不会追加奖励选项。

本 Mod 有且只有以上两类玩法修改：最大能量 +1；精英/Boss 后额外掉落 1 项随机奖励。

## 安装方法

### 方法 1：使用构建脚本安装

1. 安装 [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)。
2. 确保本机已安装 Slay the Spire 2。
3. 在项目根目录运行：

   ```powershell
   powershell -ExecutionPolicy Bypass -File build-and-install.ps1
   ```

4. 脚本会构建 Release DLL，并把以下文件复制到游戏的 `mods` 目录：

   ```text
   sts2-better-reward.dll
   sts2-better-reward.json
   ```

5. 启动游戏，在 Mods 菜单中启用 `sts2-better-reward`。

> 如果脚本找不到游戏目录，请修改 `build-and-install.ps1` 中的 `$GAME_DIR`。
> 如果构建阶段提示找不到 `sts2.dll`，还需要让项目文件中的 `GameDirWin` 或 `GameDirWsl` 指向同一个游戏目录，或修改脚本里的 `dotnet build` 参数。

### 方法 2：手动安装

将构建得到的以下两个文件复制到游戏目录的 `mods` 文件夹：

```text
<Slay the Spire 2 游戏目录>\mods\
  sts2-better-reward.dll
  sts2-better-reward.json
```

常见 Steam 安装路径：

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\
D:\SteamLibrary\steamapps\common\Slay the Spire 2\
E:\SteamLibrary\steamapps\common\Slay the Spire 2\
```

## 构建说明

项目使用 Godot .NET SDK 和 .NET 9：

```powershell
dotnet build -c Release
```

项目文件默认会从以下位置查找游戏程序集：

- Windows：`E:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64`
- WSL：`/mnt/e/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64`

如果你的游戏安装在其他位置，可以通过 MSBuild 属性覆盖：

```powershell
dotnet build -c Release -p:GameDirWin="C:\path\to\Slay the Spire 2"
```

或在 WSL 中：

```bash
dotnet build -c Release -p:GameDirWsl="/mnt/c/path/to/Slay the Spire 2"
```

## 技术实现

Mod 入口位于 `ModEntry.cs`：

- `ModEntry.Initialize()` 创建 Harmony 实例并对当前程序集执行 `PatchAll`。
- `StartingEnergyBonusPatch` 定位并补丁 `MegaCrit.Sts2.Core.Hooks.Hook.ModifyMaxEnergy`，在 Postfix 中将返回值增加 `1m`。
- `ExtraRewardOptionsPatch` 定位并补丁 `MegaCrit.Sts2.Core.Hooks.Hook.ModifyRewards`，当房间类型为 `Elite` 或 `Boss` 时向奖励列表追加 1 个基于跑局和房间信息生成的稳定 RNG 选择的额外奖励选项。
- 额外奖励包含 5 种：`GoldReward(100)`、预先固定具体遗物的 `RelicReward`、`CardRemovalReward`、自定义升级卡奖励、自定义最大生命奖励。
- `ExtraRewardDeserializationPatch` 补丁 `Reward.FromSerializable`，用于在读取奖励列表存档时还原自定义奖励，避免已掉落奖励重新随机。

## 卸载

删除游戏目录 `mods` 文件夹中的：

```text
sts2-better-reward.dll
sts2-better-reward.json
```

## Mod 信息

- Mod ID：`sts2-better-reward`
- 作者：`STS2 Modder`
- 版本：`v1.0.3`
- 是否影响玩法：是

## 更新日志

### v1.0.3

- 明确 Mod 范围：只有初始能量 +1、精英/Boss 后额外掉落 1 项随机奖励
- 额外奖励池扩展为 5 项：100 金币、随机遗物、移除卡、升级卡、生命值上限 +6
- 额外奖励在生成时固定具体类型；遗物奖励会固定具体遗物，奖励列表读档不会重新随机
- 新增项目级 `CLAUDE.md`，记录以上范围和奖励规则

### v1.0.2

- 检查 STS2 奖励 API，确认 `RelicReward(player)` 会在奖励填充阶段通过游戏原生遗物池生成遗物
- 将额外奖励类型选择改为基于游戏 `Rng.NextInt(3)`，并为遗物奖励调用 `Reward.SetRng()` 绑定同源 RNG
- 保持额外奖励选择对同一跑局、楼层和房间稳定，避免与游戏奖励生成流程脱节

### v1.0.1

- 修复 `ExtraRewardOptionsPatch.Prefix` 中缺少空值检查导致的潜在崩溃问题
- 统一类名 `MainFile` → `ModEntry`，与入口文件名保持一致
- 修正 `TargetMethod()` 中的误导性日志文本
- 关闭项目中未使用的 `AllowUnsafeBlocks` 编译开关

## 开源协议

MIT License
