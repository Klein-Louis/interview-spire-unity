using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InterviewSpire
{
    public partial class GameController
    {
        private void RunOps(CardData def)
        {
            bool upgraded = State.upgraded.Contains(def.key);
            foreach (var op in def.ops)
            {
                // 数据格式：dmg_if=[base, tag, cond, bonus]；block_if=[base, cond, bonus]
                int a = Arg(op.args, 0, 0);
                string tag = Arg(op.args, 1, "通用");
                string cond = "";
                int bonus = 0;
                if (op.type == "dmg_if")
                {
                    cond = Arg(op.args, 2, "");
                    bonus = Arg(op.args, 3, 0);
                }
                else if (op.type == "block_if")
                {
                    cond = Arg(op.args, 1, "");
                    bonus = Arg(op.args, 2, 0);
                }
                int plus = upgraded && (op.type == "dmg" || op.type == "dmg_if" ||
                                        op.type == "block" || op.type == "block_if" ||
                                        op.type == "heal") ? 3 : 0;
                switch (op.type)
                {
                    case "dmg": Hit(a + plus, tag); break;
                    case "dmg_if": Hit(a + plus + (CondMet(cond) ? bonus : 0), tag); break;
                    case "block": State.block += a + plus; break;
                    case "block_if": State.block += a + plus + (CondMet(cond) ? bonus : 0); break;
                    case "draw": DrawCards(a + (upgraded ? 1 : 0)); break;
                    case "heal": Heal(a + plus); break;
                    case "energy": State.energy += a + (upgraded ? 1 : 0); break;
                    case "cleanse": State.selfDoubt = State.vulnerable = false; if (upgraded) State.block += 5; break;
                    case "discount": State.discount = 1; break;
                }
            }
            // 无 ops 的老卡使用简化效果表
            if (def.ops.Count == 0) LegacyEffect(def.key, upgraded);
        }

        private static int Arg(List<string> args, int index, int fallback)
        {
            return args != null && args.Count > index ? ParseInt(args[index], fallback) : fallback;
        }

        private static string Arg(List<string> args, int index, string fallback)
        {
            return args != null && args.Count > index ? args[index] : fallback;
        }

        private void LegacyEffect(string key, bool up)
        {
            int p = up ? 3 : 0;
            switch (key)
            {
                case "basic": Hit(7 + p, "通用"); break;
                case "example": Hit(6 + p + (State.lastTag == "通用" ? 4 : 0), "行为"); break;
                case "breath": State.block += 5 + p; break;
                case "quantify": Hit(Mathf.Min(10, 6 + p + (State.lastTag == "行为" ? 4 : 0)), "项目"); break;
                case "review": DrawCards(2 + (up ? 1 : 0)); break;
                case "data": Hit(10 + p, "项目"); break;
                case "tech": Hit(Mathf.Min(10, 10 + p), "技术"); break;
                case "clarify": DrawCards(1 + (up ? 1 : 0)); State.discount = 1; break;
                case "pause": State.block += 10 + p; break;
                case "askback": Hit(8 + p, "通用"); Heal(2); break;
                case "admit": State.block += 8 + p; Hit(4 + p, "通用"); break;
                case "story": Hit(Mathf.Min(10, 6 + p + (State.cardsPlayed >= 3 ? 4 : 0)), "行为"); break;
                case "structure": Hit(Mathf.Min(10, 10 + p), "通用"); DrawCards(1 + (up ? 1 : 0)); break;
                case "humble": State.block += 6 + p; break;
                case "calm": State.block += 8 + p; State.selfDoubt = false; break;
                case "iterate": Hit(Mathf.Min(10, 8 + p), "项目"); Heal(3); break;
                case "keyword": Hit(Mathf.Min(10, 5 + p + (State.question.type != "通用" ? 5 : 0)), "通用"); break;
                case "portfolio": Hit(Mathf.Min(10, 10 + p), "项目"); break;
                case "own": Hit(7 + p, "行为"); State.block += 5 + p; break;
                case "time": State.energy += 2 + (up ? 1 : 0); DrawCards(1 + (up ? 1 : 0)); break;
                case "sincere": Hit(10 + p, "动机"); break;
                case "pressure": Hit(Mathf.Min(10, 6 + p + (State.confidence < State.maxConfidence * .5f ? 4 : 0)), "压力"); break;
                case "logic": Hit(Mathf.Min(10, 7 + p + (State.lastTag == "通用" ? 3 : 0)), "技术"); break;
                case "proof": Hit(8 + p, "项目"); State.block += 5 + p; break;
                case "empathy": Hit(7 + p, "行为"); Heal(2); break;
                case "boundary": State.block += 4 + p; break;
            }
        }

        private bool CondMet(string cond)
        {
            if (cond == "low_hp") return State.confidence < State.maxConfidence * .5f;
            if (cond == "non_common") return State.question.type != "通用";
            if (cond == "first_card") return State.cardsPlayed == 1;
            if (cond.StartsWith("last_tag:")) return State.lastTag == cond.Substring(9);
            if (cond.StartsWith("cards>=")) return State.cardsPlayed >= ParseInt(cond.Substring(7), 0);
            return false;
        }

        private void Hit(int baseDamage, string tag, bool ignoreSelfDoubt = false)
        {
            int d = baseDamage + (tag == "通用" ? State.universalBonus : 0);
            State.universalBonus = 0;
            bool match = tag != "通用" && tag == State.question.type;
            if (match) d = Mathf.RoundToInt(d * Tuning.matchMult);
            if (!ignoreSelfDoubt && State.selfDoubt) d = Mathf.Max(0, d - 3);
            int absorbed = Mathf.Min(State.enemy.block, d);
            State.enemy.block -= absorbed;
            int actual = d - absorbed;
            State.enemy.hp = Mathf.Max(0, State.enemy.hp - actual);
            State.totalDamage += actual;
            if (match && Effect != null) Effect("fx_match_flash_alpha@v1.png", 1);
            if (Effect != null) Effect("fx_hit_spark_alpha@v1.png", 1);
            Log("造成 " + actual + " 点认可伤害" + (match ? "（题意契合）" : "") + "。", match ? "hit" : "");
        }

        private void Heal(int v)
        {
            State.confidence = Mathf.Min(State.maxConfidence, State.confidence + v);
        }

        private static int ParseInt(string s, int fallback)
        {
            int v; return int.TryParse(s, out v) ? v : fallback;
        }

        private static bool DealsDamage(CardData c)
        {
            return c.ops.Any(o => o.type == "dmg" || o.type == "dmg_if");
        }

        private static string CardName(string key)
        {
            var d = GameDatabase.Card(key);
            return d == null ? key : d.name;
        }
    }
}
