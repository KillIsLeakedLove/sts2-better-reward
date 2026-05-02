using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using System.Reflection;

namespace StartingEnergyMod;

/// <summary>
/// Mod 主入口类
/// 使用 [ModInitializer] 标记初始化方法
/// </summary>
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "StartingEnergyMod";

    /// <summary>
    /// Mod 初始化方法，由 STS2 加载器自动调用
    /// </summary>
    public static void Initialize()
    {
        var harmony = new Harmony(ModId);
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        GD.Print($"[{ModId}] 开局能量+1 Mod 已加载");
    }
}

/// <summary>
/// Harmony 补丁：修改玩家初始能量
/// 目标：CombatManager.SetUpCombat 或 StartCombatInternal 完成后将能量从 3 增加到 4
/// </summary>
[HarmonyPatch]
public static class StartingEnergyPatch
{
    /// <summary>
    /// 动态查找 CombatManager 中初始化战斗的方法
    /// </summary>
    static MethodBase? TargetMethod()
    {
        // 使用完整命名空间查找 CombatManager
        var combatManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatManager")
            ?? AccessTools.TypeByName("CombatManager");

        if (combatManagerType == null)
        {
            GD.PrintErr($"[{MainFile.ModId}] 找不到 CombatManager 类型");
            return null;
        }

        GD.Print($"[{MainFile.ModId}] 找到类型: {combatManagerType.FullName}");

        // 尝试 SetUpCombat 方法（设置战斗的方法）
        var method = AccessTools.Method(combatManagerType, "SetUpCombat");
        if (method != null)
        {
            GD.Print($"[{MainFile.ModId}] 找到目标方法: SetUpCombat");
            return method;
        }

        // 备选：StartCombatInternal
        method = AccessTools.Method(combatManagerType, "StartCombatInternal");
        if (method != null)
        {
            GD.Print($"[{MainFile.ModId}] 找到目标方法: StartCombatInternal");
            return method;
        }

        // 备选：StartCombat
        method = AccessTools.Method(combatManagerType, "StartCombat");
        if (method != null)
        {
            GD.Print($"[{MainFile.ModId}] 找到目标方法: StartCombat");
            return method;
        }

        GD.PrintErr($"[{MainFile.ModId}] 警告：未能找到 CombatManager 的战斗初始化方法");
        return null;
    }

    /// <summary>
    /// 后置补丁：战斗初始化完成后修改玩家能量
    /// </summary>
    static void Postfix(object __instance)
    {
        if (__instance == null) return;

        try
        {
            var combatManagerType = __instance.GetType();

            // CombatManager 中玩家状态可能通过 PlayerState 属性访问
            // 尝试获取 playerState 或 player 字段/属性
            object? playerState = GetPlayerState(__instance, combatManagerType);

            if (playerState != null)
            {
                ModifyPlayerEnergy(playerState);
            }
            else
            {
                GD.PrintErr($"[{MainFile.ModId}] 无法获取玩家状态");
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[{MainFile.ModId}] 能量修改失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 CombatManager 获取玩家状态对象
    /// </summary>
    private static object? GetPlayerState(object instance, System.Type type)
    {
        // 尝试常见字段名
        string[] fieldNames = { "playerState", "_playerState", "player", "_player", "PlayerState", "Player" };
        foreach (var name in fieldNames)
        {
            var field = AccessTools.Field(type, name);
            if (field != null)
            {
                var value = field.GetValue(instance);
                if (value != null) return value;
            }
        }

        // 尝试常见属性名
        string[] propNames = { "PlayerState", "Player", "playerState", "player" };
        foreach (var name in propNames)
        {
            var prop = AccessTools.Property(type, name);
            if (prop != null && prop.CanRead)
            {
                var value = prop.GetValue(instance);
                if (value != null) return value;
            }
        }

        return null;
    }

    /// <summary>
    /// 修改玩家能量：如果当前是默认值 3，则增加到 4
    /// </summary>
    private static void ModifyPlayerEnergy(object playerState)
    {
        var playerType = playerState.GetType();

        // 尝试 Energy 属性（最常见的能量属性名）
        var energyProp = AccessTools.Property(playerType, "Energy")
            ?? AccessTools.Property(playerType, "energy")
            ?? AccessTools.Property(playerType, "CurrentEnergy")
            ?? AccessTools.Property(playerType, "currentEnergy");

        if (energyProp != null && energyProp.CanRead && energyProp.CanWrite)
        {
            var currentEnergy = (int)energyProp.GetValue(playerState)!;
            if (currentEnergy == 3)
            {
                energyProp.SetValue(playerState, 4);
                GD.Print($"[{MainFile.ModId}] 能量已修改: 3 -> 4");
            }
        }
        else
        {
            // 尝试直接修改字段
            var energyField = AccessTools.Field(playerType, "energy")
                ?? AccessTools.Field(playerType, "_energy")
                ?? AccessTools.Field(playerType, "Energy");

            if (energyField != null)
            {
                var currentEnergy = (int)energyField.GetValue(playerState)!;
                if (currentEnergy == 3)
                {
                    energyField.SetValue(playerState, 4);
                    GD.Print($"[{MainFile.ModId}] 能量字段已修改: 3 -> 4");
                }
            }
            else
            {
                GD.PrintErr($"[{MainFile.ModId}] 无法找到玩家能量字段/属性");
            }
        }
    }
}
