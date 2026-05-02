using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
    private static readonly Random Random = new();

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

        rewards.Add(CreateRandomExtraReward(runState, player));
        GD.Print($"[{MainFile.ModId}] {GetRoomName(room.RoomType)}奖励已追加 1 个额外奖励选项");
    }

    private static Reward CreateRandomExtraReward(IRunState runState, Player player)
    {
        return Random.Next(5) switch
        {
            0 => new RelicReward(player),
            1 => new GoldReward(100, player, false),
            2 => new UpgradeCardsReward(player),
            3 => new RemoveCardReward(runState, player),
            _ => new MaxHpReward(player),
        };
    }

    private static CardSelectorPrefs CreateSelectorPrefs(string prompt)
    {
        return new CardSelectorPrefs(new LocString("card_selection", prompt), 1, 1)
        {
            Cancelable = true
        };
    }

    private static string GetRoomName(RoomType roomType)
    {
        return roomType == RoomType.Elite ? "精英" : "Boss";
    }

    private sealed class UpgradeCardsReward : Reward
    {
        public UpgradeCardsReward(Player player) : base(player)
        {
        }

        protected override RewardType RewardType => RewardType.SpecialCard;

        public override int RewardsSetIndex => 0;

        public override LocString Description => new LocString("card_reward", "选择一张卡牌升级");

        public override bool IsPopulated => true;

        public override Task Populate()
        {
            return Task.CompletedTask;
        }

        protected override async Task<bool> OnSelect()
        {
            var selectedCard = (await CardSelectCmd.FromDeckForUpgrade(Player, CreateSelectorPrefs("选择一张卡牌升级"))).FirstOrDefault();
            if (selectedCard == null)
            {
                return false;
            }

            CardCmd.Upgrade(selectedCard, CardPreviewStyle.EventLayout);
            return true;
        }

        public override void MarkContentAsSeen()
        {
        }
    }

    private sealed class RemoveCardReward : Reward
    {
        private readonly IRunState runState;

        public RemoveCardReward(IRunState runState, Player player) : base(player)
        {
            this.runState = runState;
        }

        protected override RewardType RewardType => RewardType.RemoveCard;

        public override int RewardsSetIndex => 0;

        public override LocString Description => new LocString("card_reward", "选择一张卡牌移除");

        public override bool IsPopulated => true;

        public override Task Populate()
        {
            return Task.CompletedTask;
        }

        protected override async Task<bool> OnSelect()
        {
            var selectedCard = (await CardSelectCmd.FromDeckForRemoval(Player, CreateSelectorPrefs("选择一张卡牌移除"), _ => true)).FirstOrDefault();
            if (selectedCard == null)
            {
                return false;
            }

            if (runState is not RunState concreteRunState)
            {
                return false;
            }

            concreteRunState.RemoveCard(selectedCard);
            return true;
        }

        public override void MarkContentAsSeen()
        {
        }
    }

    private sealed class MaxHpReward : Reward
    {
        public MaxHpReward(Player player) : base(player)
        {
        }

        protected override RewardType RewardType => RewardType.None;

        public override int RewardsSetIndex => 0;

        public override LocString Description => new LocString("card_reward", "生命上限 +6");

        public override bool IsPopulated => true;

        public override Task Populate()
        {
            return Task.CompletedTask;
        }

        protected override async Task<bool> OnSelect()
        {
            await CreatureCmd.GainMaxHp(Player.Creature, 6m);
            return true;
        }

        public override void MarkContentAsSeen()
        {
        }
    }
}
