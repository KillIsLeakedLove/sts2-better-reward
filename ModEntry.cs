using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sts2BetterReward;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "sts2-better-reward";

    public static void Initialize()
    {
        var harmony = new Harmony(ModId);
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        GD.Print($"[{ModId}] sts2-better-reward Mod 已加载");
    }
}

[HarmonyPatch]
public static class StartingEnergyBonusPatch
{
    static MethodBase? TargetMethod()
    {
        var hookType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
        if (hookType == null)
        {
            GD.PrintErr($"[{MainFile.ModId}] 找不到 Hook 类型");
            return null;
        }

        var combatStateType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatState");
        var playerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Players.Player");
        if (combatStateType == null || playerType == null)
        {
            GD.PrintErr($"[{MainFile.ModId}] 找不到 CombatState 或 Player 类型");
            return null;
        }

        var method = AccessTools.Method(hookType, "ModifyMaxEnergy", new[] { combatStateType, playerType, typeof(decimal) });
        if (method == null)
        {
            GD.PrintErr($"[{MainFile.ModId}] 找不到 Hook.ModifyMaxEnergy 方法");
            return null;
        }

        GD.Print($"[{MainFile.ModId}] 已补丁 Hook.ModifyMaxEnergy");
        return method;
    }

    static void Postfix(ref decimal __result)
    {
        __result += 1m;
    }
}

[HarmonyPatch]
public static class ExtraRewardOptionsPatch
{
    private const int ExtraRewardKindCount = 3;

    static MethodBase? TargetMethod()
    {
        var hookType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
        if (hookType == null)
        {
            GD.PrintErr($"[{MainFile.ModId}] 找不到 Hook 类型");
            return null;
        }

        var runStateType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.IRunState");
        if (runStateType == null)
        {
            GD.PrintErr($"[{MainFile.ModId}] 找不到 IRunState 类型");
            return null;
        }

        var method = AccessTools.Method(hookType, "ModifyRewards", new[] { runStateType, typeof(Player), typeof(List<Reward>), typeof(AbstractRoom) });
        if (method == null)
        {
            GD.PrintErr($"[{MainFile.ModId}] 找不到 Hook.ModifyRewards 方法");
            return null;
        }

        GD.Print($"[{MainFile.ModId}] 已补丁 Hook.ModifyRewards");
        return method;
    }

    static void Prefix(IRunState runState, Player player, List<Reward> rewards, AbstractRoom room)
    {
        if (room.RoomType != RoomType.Elite && room.RoomType != RoomType.Boss)
        {
            return;
        }

        rewards.Add(CreateExtraReward(runState, player, room));
        GD.Print($"[{MainFile.ModId}] {GetRoomName(room.RoomType)}奖励已追加 1 个额外奖励选项");
    }

    private static Reward CreateExtraReward(IRunState runState, Player player, AbstractRoom room)
    {
        return GetStableRewardKind(runState, room) switch
        {
            0 => new RelicReward(player),
            1 => new GoldReward(100, player, false),
            _ => new CardRemovalReward(player),
        };
    }

    private static int GetStableRewardKind(IRunState runState, AbstractRoom room)
    {
        var hash = 2166136261u;
        hash = AddHash(hash, MainFile.ModId);
        hash = AddHash(hash, runState.Rng.Seed);
        hash = AddHash(hash, runState.CurrentActIndex);
        hash = AddHash(hash, runState.TotalFloor);
        hash = AddHash(hash, runState.CurrentRoomCount);
        hash = AddHash(hash, room.Id ?? 0);
        hash = AddHash(hash, (int)room.RoomType);

        return (int)(hash % ExtraRewardKindCount);
    }

    private static uint AddHash(uint hash, string value)
    {
        foreach (var character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return hash;
    }

    private static uint AddHash(uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            return hash * 16777619u;
        }
    }

    private static uint AddHash(uint hash, uint value)
    {
        hash ^= value;
        return hash * 16777619u;
    }

    private static string GetRoomName(RoomType roomType)
    {
        return roomType == RoomType.Elite ? "精英" : "Boss";
    }
}
