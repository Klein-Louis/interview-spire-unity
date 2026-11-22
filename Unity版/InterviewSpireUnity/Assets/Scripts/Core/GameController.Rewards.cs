using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InterviewSpire
{
    public partial class GameController
    {
        public event Action NeedReward;
        public event Action NeedEvent;
        public event Action<bool> GameOver;

        public List<string> RandomRewardKeys(int count)
        {
            var pool = GameDatabase.Data.cards
                .Where(c => c.rarity != "starter")
                .OrderBy(c => NextFloat())
                .Take(count)
                .Select(c => c.key)
                .ToList();
            return pool;
        }

        public void TakeReward(string cardKey)
        {
            if (State == null || State.phase != 2) return;
            if (!string.IsNullOrEmpty(cardKey))
            {
                var gained = new RuntimeCard { key = cardKey, uid = State.uidSeq++ };
                State.masterDeck.Add(gained);
                State.drawPile.Add(gained);   // 奖励牌直接进入抽牌堆
                Log("新卡加入卡组并进入抽牌堆：" + CardName(cardKey), "good");
            }
            else { Heal(5); Log("跳过选牌，恢复 5 点自信。", "good"); }
            State.phase = 0;
            if (NeedEvent != null) NeedEvent(); else StartBattle(State.floor + 1);
        }

        public void AdvanceToNextBattle()
        {
            if (State != null) StartBattle(State.floor + 1);
        }

        public void ApplyEvent(string effect)
        {
            if (State == null) return;
            switch (effect)
            {
                case "rest": Heal(6); State.nextEnergy -= 1; break;
                case "trim":
                    if (State.masterDeck.Count > 5)
                        State.masterDeck.RemoveAt(NextInt(0, State.masterDeck.Count));
                    Heal(2); break;
                case "upgrade":
                    var keys = State.masterDeck.Select(c => c.key).Distinct()
                        .Where(k => !State.upgraded.Contains(k) && GameDatabase.Card(k) != null &&
                                    GameDatabase.Card(k).rarity != "starter").ToList();
                    if (keys.Count > 0)
                    {
                        string key = keys[NextInt(0, keys.Count)];
                        State.upgraded.Add(key);
                        Log("强化：" + GameDatabase.Card(key).name + "（来自你的卡组）", "good");
                    }
                    else Heal(3);
                    break;
                case "heal_small": Heal(2); break;
                case "heal_medium": Heal(4); break;
                case "draw_block": State.nextBlock += 3; State.nextEnergy += 1; break;
                case "remove_heal":
                    if (State.masterDeck.Count > 5) State.masterDeck.RemoveAt(NextInt(0, State.masterDeck.Count));
                    Heal(5); break;
                case "maxhp": State.maxConfidence += 2; Heal(4); break;
                case "energy_next": State.nextEnergy += 1; break;
                case "add_card_hurt":
                    var cards = GameDatabase.Data.cards.FindAll(c => c.rarity != "starter");
                    var pick = cards[NextInt(0, cards.Count)];
                    State.masterDeck.Add(new RuntimeCard { key = pick.key, uid = State.uidSeq++ });
                    State.confidence = Mathf.Max(1, State.confidence - 3);
                    break;
            }
            Emit();
        }

    }
}
