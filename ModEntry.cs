using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Sts2Rng = MegaCrit.Sts2.Core.Random.Rng;

namespace Sts2BetterReward;

[ModInitializer(nameof(Initialize))]
public partial class ModEntry : Node
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
            GD.PrintErr($"[{ModEntry.ModId}] 找不到 Hook 类型");
            return null;
        }

        var combatStateType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatState");
        var playerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Players.Player");
        if (combatStateType == null || playerType == null)
        {
            GD.PrintErr($"[{ModEntry.ModId}] 找不到 CombatState 或 Player 类型");
            return null;
        }

        var method = AccessTools.Method(hookType, "ModifyMaxEnergy", new[] { combatStateType, playerType, typeof(decimal) });
        if (method == null)
        {
            GD.PrintErr($"[{ModEntry.ModId}] 找不到 Hook.ModifyMaxEnergy 方法");
            return null;
        }

        GD.Print($"[{ModEntry.ModId}] 已找到 Hook.ModifyMaxEnergy 目标方法");
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
    private const int ExtraRewardKindCount = 5;
    private const int GoldAmount = 100;
    private const int MaxHpAmount = 6;
    private static readonly ModelId UpgradeCardRewardSaveId = new(ModEntry.ModId, "upgrade_card");
    private static readonly ModelId MaxHpRewardSaveId = new(ModEntry.ModId, "max_hp");

    static MethodBase? TargetMethod()
    {
        var hookType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Hooks.Hook");
        if (hookType == null)
        {
            GD.PrintErr($"[{ModEntry.ModId}] 找不到 Hook 类型");
            return null;
        }

        var runStateType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.IRunState");
        if (runStateType == null)
        {
            GD.PrintErr($"[{ModEntry.ModId}] 找不到 IRunState 类型");
            return null;
        }

        var method = AccessTools.Method(hookType, "ModifyRewards", new[] { runStateType, typeof(Player), typeof(List<Reward>), typeof(AbstractRoom) });
        if (method == null)
        {
            GD.PrintErr($"[{ModEntry.ModId}] 找不到 Hook.ModifyRewards 方法");
            return null;
        }

        GD.Print($"[{ModEntry.ModId}] 已找到 Hook.ModifyRewards 目标方法");
        return method;
    }

    static void Prefix(IRunState runState, Player player, List<Reward> rewards, AbstractRoom room)
    {
        if (room == null || runState?.Rng == null || player == null || rewards == null)
        {
            return;
        }

        if (room.RoomType != RoomType.Elite && room.RoomType != RoomType.Boss)
        {
            return;
        }

        rewards.Add(CreateExtraReward(runState, player, room));
        GD.Print($"[{ModEntry.ModId}] {GetRoomName(room.RoomType)}奖励已追加 1 个额外奖励选项");
    }

    private static Reward CreateExtraReward(IRunState runState, Player player, AbstractRoom room)
    {
        var rewardRng = CreateRewardRng(runState, room);
        return rewardRng.NextInt(ExtraRewardKindCount) switch
        {
            0 => new GoldReward(GoldAmount, player, false),
            1 => CreateRelicReward(player, rewardRng),
            2 => new CardRemovalReward(player),
            3 => new UpgradeCardReward(player),
            _ => new MaxHpReward(player),
        };
    }

    private static Reward CreateRelicReward(Player player, Sts2Rng rewardRng)
    {
        var relic = MegaCrit.Sts2.Core.Factories.RelicFactory.PullNextRelicFromFront(player, rewardRng).ToMutable();
        return new RelicReward(relic, player);
    }

    private static Sts2Rng CreateRewardRng(IRunState runState, AbstractRoom room)
    {
        var seed = GetStableRewardSeed(runState, room);
        return new Sts2Rng(seed, $"{ModEntry.ModId}:extra-reward");
    }

    private static uint GetStableRewardSeed(IRunState runState, AbstractRoom room)
    {
        var hash = 2166136261u;
        hash = AddHash(hash, ModEntry.ModId);
        hash = AddHash(hash, runState.Rng.Seed);
        hash = AddHash(hash, runState.CurrentActIndex);
        hash = AddHash(hash, runState.TotalFloor);
        hash = AddHash(hash, runState.CurrentRoomCount);
        hash = AddHash(hash, room.Id ?? 0);
        hash = AddHash(hash, (int)room.RoomType);

        return hash;
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

    public static bool TryDeserializeExtraReward(SerializableReward save, Player player, out Reward reward)
    {
        reward = null!;

        if (save.PredeterminedModelId == UpgradeCardRewardSaveId)
        {
            reward = new UpgradeCardReward(player);
            return true;
        }

        if (save.PredeterminedModelId == MaxHpRewardSaveId)
        {
            reward = new MaxHpReward(player);
            return true;
        }

        return false;
    }

    private sealed class UpgradeCardReward(Player player) : Reward(player)
    {
        protected override RewardType RewardType => RewardType.None;
        public override int RewardsSetIndex => 99;
        public override LocString Description => CardSelectorPrefs.UpgradeSelectionPrompt;
        public override bool IsPopulated => true;
        protected override string IconPath => ImageHelper.GetImagePath("ui/reward_screen/reward_icon_special_card.png");

        public override Task Populate()
        {
            return Task.CompletedTask;
        }

        protected override async Task<bool> OnSelect()
        {
            var cards = await CardSelectCmd.FromDeckForUpgrade(Player, new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 1));
            CardCmd.Upgrade(cards, MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.GridLayout);
            return true;
        }

        public override SerializableReward ToSerializable()
        {
            return new SerializableReward
            {
                RewardType = RewardType.None,
                PredeterminedModelId = UpgradeCardRewardSaveId,
            };
        }

        public override void MarkContentAsSeen()
        {
        }
    }

    private sealed class MaxHpReward(Player player) : Reward(player)
    {
        protected override RewardType RewardType => RewardType.None;
        public override int RewardsSetIndex => 100;
        public override LocString Description
        {
            get
            {
                var description = new LocString("gameplay_ui", "EVENT_CHOICE_GAIN_MAX_HP");
                description.Add("Amount", MaxHpAmount);
                return description;
            }
        }
        public override bool IsPopulated => true;
        protected override string IconPath => ImageHelper.GetImagePath("ui/reward_screen/reward_icon_heal.png");

        public override Task Populate()
        {
            return Task.CompletedTask;
        }

        protected override async Task<bool> OnSelect()
        {
            await CreatureCmd.GainMaxHp(Player.Creature, MaxHpAmount);
            return true;
        }

        public override SerializableReward ToSerializable()
        {
            return new SerializableReward
            {
                RewardType = RewardType.None,
                PredeterminedModelId = MaxHpRewardSaveId,
            };
        }

        public override void MarkContentAsSeen()
        {
        }
    }
}

[HarmonyPatch]
public static class ExtraRewardDeserializationPatch
{
    static MethodBase? TargetMethod()
    {
        return AccessTools.Method(typeof(Reward), nameof(Reward.FromSerializable), new[] { typeof(SerializableReward), typeof(Player) });
    }

    static bool Prefix(SerializableReward save, Player player, ref Reward __result)
    {
        if (save == null || player == null)
        {
            return true;
        }

        if (ExtraRewardOptionsPatch.TryDeserializeExtraReward(save, player, out var reward))
        {
            __result = reward;
            return false;
        }

        return true;
    }
}
