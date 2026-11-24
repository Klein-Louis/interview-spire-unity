using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InterviewSpire
{
    /// <summary>核心战斗控制器：Python 版 game.py 的 C# 对应实现（MVP）。</summary>
    public partial class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }
        public GameState State { get; private set; }
        public string Job = "软件开发";
        public string Difficulty = "标准";
        public string EnemyMood = "沉思"; // 愤怒 / 得意 / 沉思
        public event Action Changed;
        public event Action<string, int> Effect; // fx 文件名, 目标：0 玩家 1 面试官 -1 全屏

        private int sessionId;
        private bool gameStarted;

        private void Awake()
        {
            Instance = this;
            GameDatabase.Load();
            GameDatabase.LoadImported();
        }

        private TuningData Tuning
        {
            get { return GameDatabase.Data.tuning ?? (GameDatabase.Data.tuning = new TuningData()); }
        }

        // 确定性随机：seed + 调用计数随存档保存，读档后可复现同一序列
        private int NextInt(int min, int maxExclusive)
        {
            var r = new System.Random(unchecked(State.seed ^ State.rngCounter));
            int v = r.Next(min, maxExclusive);
            State.rngCounter = unchecked(State.rngCounter + 1);
            return v;
        }

        private float NextFloat()
        {
            var r = new System.Random(unchecked(State.seed ^ State.rngCounter));
            float v = (float)r.NextDouble();
            State.rngCounter = unchecked(State.rngCounter + 1);
            return v;
        }

        private void Emit() { if (Changed != null) Changed(); }

        public bool HasActiveRun
        {
            get { return gameStarted && State != null && State.phase != 3; }
        }

        public void NewGame()
        {
            var diff = GameDatabase.Data.difficulties.Find(d => d.name == Difficulty)
                       ?? GameDatabase.Data.difficulties[1];
            var deck = new List<RuntimeCard>();
            string bonus = "";
            foreach (string key in GameDatabase.Data.starter) deck.Add(new RuntimeCard { key = key });
            if (Difficulty == "硬核")
            {
                var uncommon = GameDatabase.Data.cards.FindAll(c => c.rarity == "uncommon");
                if (uncommon.Count > 0)
                {
                    bonus = uncommon[NextInt(0, uncommon.Count)].key;
                    deck.Add(new RuntimeCard { key = bonus });
                }
            }
            int hp = diff.hp;
            State = new GameState
            {
                runId = DateTime.UtcNow.Ticks,
                job = Job, diff = Difficulty, openingBonus = bonus,
                floor = 0, turn = 1, phase = 0,
                confidence = hp, maxConfidence = hp, block = 0,
                energy = 4, maxEnergy = 4,
                seed = System.Guid.NewGuid().GetHashCode(),
                starUsedThisTurn = false, interviewerPressured = false,
                uidSeq = 1
            };
            foreach (var card in deck) { card.uid = State.uidSeq++; State.masterDeck.Add(card); }
            State.askedQuestionIds = new List<string>();
            gameStarted = true;
            StartBattle(0);
        }

        private void StartBattle(int index)
        {
            sessionId++;
            State.floor = index;
            State.turn = 1;
            State.phase = 0;
            State.block = 0;
            State.energy = State.maxEnergy;
            State.vulnerable = State.selfDoubt = State.energyPenalty = State.techChain = false;
            State.discount = 0;
            State.cardsPlayed = 0;
            State.universalUsed = false;
            State.universalBonus = 0;

            var pile = State.masterDeck.Select(c => new RuntimeCard { key = c.key, uid = State.uidSeq++ }).ToList();
            Shuffle(pile);   // 每打赢一位面试官，新战斗重新洗牌
            if (index > 0) Log("新一轮面试开始：抽牌堆已重新洗牌。", "");
            State.drawPile = pile;
            State.hand = new List<RuntimeCard>();
            State.discard = new List<RuntimeCard>();
            State.exhaust = new List<RuntimeCard>();

            var def = GameDatabase.Data.enemies[index];
            var diff = GameDatabase.Data.difficulties.Find(d => d.name == State.diff);
            float mult = diff == null ? 1f : diff.hp_mult;
            int ehp = Mathf.RoundToInt(def.hp * mult);
            State.enemy = new EnemyRuntime { index = index, hp = ehp, maxHp = ehp, block = 0 };
            EnemyMood = "沉思";
            PickIntent();
            State.logs = new List<LogEntry>();
            Log("进入「" + def.sub + "」，面试官：" + def.name, "good");
            StartPlayerTurn(true);
        }

        private void StartPlayerTurn(bool first)
        {
            if (State.phase == 3) return;
            State.phase = 0;
            if (!first) State.turn++;
            State.block += State.nextBlock;   // 护盾跨回合保留并叠加
            State.nextBlock = 0;
            State.energy = Mathf.Max(1, State.maxEnergy - (State.energyPenalty ? 1 : 0)) + State.nextEnergy;
            State.nextEnergy = 0;
            // 负面状态保留到面试官下一次行动时才结算/清除
            State.discount = 0;
            State.cardsPlayed = 0;
            State.starUsedThisTurn = false;
            State.universalUsed = false;
            State.universalBonus = 0;

            State.question = RandomQuestion();
            // 不重洗抽牌堆：按当前牌堆顺序从上往下抽
            DrawCards(first ? Tuning.openingHandSize : Tuning.drawPerTurn);

            if (first && !string.IsNullOrEmpty(State.openingBonus) &&
                !State.hand.Any(c => c.key == State.openingBonus))
            {
                if (State.hand.Count > 0)
                {
                    var swap = State.hand[NextInt(0, State.hand.Count)];
                    State.hand.Remove(swap);
                    State.drawPile.Add(swap);
                }
                State.hand.Add(new RuntimeCard { key = State.openingBonus, uid = State.uidSeq++ });
                Log("硬核奖励：进阶卡「" + CardName(State.openingBonus) + "」进入起手。", "good");
            }
            State.openingBonus = "";
            State.totalTurns++;
            Emit();
        }

        private void DrawCards(int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (State.hand.Count >= Tuning.handLimit) break;
                if (State.drawPile.Count == 0)
                {
                    if (State.discard.Count == 0) break;
                    State.drawPile.AddRange(State.discard);   // 按顺序转入，不重洗
                    State.discard.Clear();
                    Log("弃牌堆按顺序转入抽牌堆。", "");
                }
                State.hand.Add(State.drawPile[State.drawPile.Count - 1]);
                State.drawPile.RemoveAt(State.drawPile.Count - 1);
            }
        }

        public void PlayCard(int uid)
        {
            if (State == null || State.phase != 0) return;
            var card = State.hand.Find(c => c.uid == uid);
            if (card == null) return;
            var def = GameDatabase.Card(card.key);
            if (def == null) return;
            int cost = Mathf.Max(0, def.cost - State.discount);
            if (cost > State.energy) { Log("精力不足，无法打出「" + def.name + "」。", "hurt"); Emit(); return; }

            State.energy -= cost;
            State.discount = 0;
            State.hand.Remove(card);
            (def.exhaust ? ExhaustPile() : State.discard).Add(card);
            State.cardsPlayed++;
            State.totalCards++;
            Log("打出「" + def.name + "」（" + cost + " 精力）。", "");
            State.universalBonus = (def.tag == "通用" && !State.universalUsed && DealsDamage(def)) ? 2 : 0;
            if (State.universalBonus > 0) State.universalUsed = true;

            RunOps(def);
            State.universalBonus = 0;
            bool matched = def.tag != "通用" && def.tag == State.question.type;
            // 面试官表情：切题 -> 沉思；没对上题目 -> 愤怒
            EnemyMood = matched ? "沉思" : "愤怒";
            if (def.tag == "技术" && GameDatabase.Data.enemies[State.floor].mechanic == "tech_chain")
            {
                State.techChain = true;
                Log("追问链已触发：下一击 +4 伤害。", "hurt");
            }
            State.lastTag = def.tag;
            Emit();

            CheckEnemyDefeat();
        }

        private List<RuntimeCard> ExhaustPile() { return State.exhaust; }

        public void EndTurn()
        {
            if (State == null || State.phase != 0) return;
            int sid = sessionId;
            if (!State.starUsedThisTurn) SkipStar();   // 未答题直接结束等同于跳过
            State.phase = 1;
            // 结束回合不弃掉手牌
            Emit();
            StartCoroutine(EnemyAct(sid));
        }

        private IEnumerator EnemyAct(int sid)
        {
            yield return new WaitForSeconds(0.45f);
            if (sid != sessionId || State == null || State.phase != 1) yield break;
            SimulateEnemyAct();
        }

        /// <summary>同步执行一次敌方行动，供命令行自动对局测试使用。</summary>
        public void SimulateEnemyAct()
        {
            if (State == null || State.phase != 1) return;
            var def = GameDatabase.Data.enemies[State.floor];
            var it = State.enemy.intent;
            // 上一次敌方行动施加的负面状态到此结束；本次新施加的会保留到下一次行动
            bool oldVulnerable = State.vulnerable;
            State.selfDoubt = false;
            State.energyPenalty = false;
            // 面试官护盾跨回合保留并叠加，直到被消耗
            Log(def.name + " 使用「" + it.name + "」。", "hurt");
            if (it.type == "attack" || it.type == "attackDebuff")
            {
                int d = it.value;
                if (def.mechanic == "tech_chain" && State.techChain)
                {
                    d += 4; State.techChain = false; Log("追问链生效：伤害 +4。", "hurt");
                }
                bool pressureApplied = State.interviewerPressured;
                if (pressureApplied)
                {
                    d = Mathf.RoundToInt(d * 1.5f);
                    Log("加压生效：本次攻击 ×1.5。", "hurt");
                }
                if (oldVulnerable) { d = Mathf.RoundToInt(d * 1.5f); State.vulnerable = false; oldVulnerable = false; }
                int absorbed = Mathf.Min(State.block, d);
                State.block -= absorbed;
                State.confidence = Mathf.Max(0, State.confidence - (d - absorbed));
                if (Effect != null) Effect("fx_hit_spark_alpha@v1.png", 0);
                int actual = d - absorbed;
                Log("你失去 " + actual + " 点自信。", "hurt");
                if (pressureApplied && actual > 0)
                {
                    State.interviewerPressured = false;
                    Log("加压在造成伤害后结束。", "");
                }
                EnemyMood = "得意";   // 面试官攻击完
                if (it.debuff == "nervous") State.vulnerable = true;
                else if (it.debuff == "doubt") State.selfDoubt = true;
                else if (it.debuff == "energy") State.energyPenalty = true;
            }
            else if (it.type == "block") { State.enemy.block += it.value; EnemyMood = "沉思"; }
            else { State.enemy.hp = Mathf.Min(State.enemy.maxHp, State.enemy.hp + it.value); EnemyMood = "沉思"; }

            if (oldVulnerable) State.vulnerable = false;   // 非攻击轮次也在此结束上一轮紧张
            Emit();
            if (State.confidence <= 0)
            {
                State.phase = 3;
                SaveSystem.DeleteForRun(State.runId);
                StatsSystem.Record(State, false);
                if (GameOver != null) GameOver(false);
                return;
            }
            PickIntent();
            StartPlayerTurn(false);
        }

        private void PickIntent()
        {
            var def = GameDatabase.Data.enemies[State.floor];
            var src = def.intents[NextInt(0, def.intents.Count)];
            float scale = GameDatabase.Data.intent_scale[Mathf.Min(State.floor, GameDatabase.Data.intent_scale.Count - 1)];
            var diff = GameDatabase.Data.difficulties.Find(d => d.name == State.diff);
            scale *= diff == null ? 1f : diff.scale_mult;
            State.enemy.intent = new IntentData
            {
                name = src.name, type = src.type, debuff = src.debuff,
                value = Mathf.RoundToInt(src.value * scale)
            };
        }

        private QuestionRuntime RandomQuestion()
        {
            var w = GameDatabase.Data.job_weights.Find(x => x.job == State.job);
            string type = "通用";
            if (w != null && w.weights.Count > 0)
            {
                int total = w.weights.Sum(x => x.weight);
                int roll = NextInt(0, total);
                foreach (var x in w.weights) { roll -= x.weight; if (roll < 0) { type = x.type; break; } }
            }

            var metas = GameDatabase.Data.question_meta
                .Where(m => m.type == type && QuestionMatchesJob(m.job))
                .ToList();
            if (metas.Count == 0)
                metas = GameDatabase.Data.question_meta.Where(m => m.type == "通用").ToList();

            if (metas.Count > 0)
            {
                if (State.askedQuestionIds == null) State.askedQuestionIds = new List<string>();
                var fresh = metas.Where(m => !State.askedQuestionIds.Contains(m.id)).ToList();
                if (fresh.Count == 0)
                {
                    foreach (var m in metas) State.askedQuestionIds.Remove(m.id);
                    fresh = metas;
                }
                var pick = fresh[NextInt(0, fresh.Count)];
                State.askedQuestionIds.Add(pick.id);
                State.currentQuestionId = pick.id;
                return new QuestionRuntime { id = pick.id, type = pick.type, text = pick.text };
            }

            var pool = new List<string>();
            var jobBank = GameDatabase.Data.job_questions.Find(x => x.job == State.job);
            if (jobBank != null)
            {
                var group = jobBank.groups.Find(g => g.type == type);
                if (group != null) for (int i = 0; i < 3; i++) pool.AddRange(group.texts);
            }
            var baseGroup = GameDatabase.Data.questions.Find(g => g.type == type);
            if (baseGroup != null) pool.AddRange(baseGroup.texts);
            if (pool.Count == 0)
            {
                baseGroup = GameDatabase.Data.questions.Find(g => g.type == "通用");
                if (baseGroup != null) pool.AddRange(baseGroup.texts);
                type = "通用";
            }
            return new QuestionRuntime { id = "", type = type,
                text = pool.Count > 0 ? pool[NextInt(0, pool.Count)] : "请做一分钟自我介绍。" };
        }

        private bool QuestionMatchesJob(string questionJob)
        {
            if (questionJob == "通用" || questionJob == State.job) return true;
            return State.job == "市场营销" && (questionJob == "管培生" || questionJob == "产品设计");
        }

        public void LoadState(GameState s)
        {
            State = s;
            gameStarted = true;
            sessionId++;
            if (State.phase != 0) State.phase = 0;
            // 读档时敌人已是 0 血（上次击杀后未结算），补触发奖励/通关，避免卡在无响应战斗
            CheckEnemyDefeat();
            // 旧存档字段补全，避免 UI 空引用
            if (State.exhaust == null) State.exhaust = new List<RuntimeCard>();
            if (State.hand == null) State.hand = new List<RuntimeCard>();
            if (State.drawPile == null) State.drawPile = new List<RuntimeCard>();
            if (State.discard == null) State.discard = new List<RuntimeCard>();
            if (State.masterDeck == null) State.masterDeck = new List<RuntimeCard>();
            if (State.upgraded == null) State.upgraded = new List<string>();
            if (State.logs == null) State.logs = new List<LogEntry>();
            if (State.question == null) State.question = new QuestionRuntime();
            if (State.saveVersion < 2) State.saveVersion = 2;
            if (State.job == "技术面") State.job = "软件开发";
            // 用当前数据表刷新敌人定义，避免旧存档保留旧血量/旧意图
            if (State.enemy != null && State.floor >= 0 &&
                State.floor < GameDatabase.Data.enemies.Count)
            {
                var def = GameDatabase.Data.enemies[State.floor];
                State.enemy.index = State.floor;
                var diff = GameDatabase.Data.difficulties.Find(d => d.name == State.diff);
                float mult = diff == null ? 1f : diff.hp_mult;
                State.enemy.maxHp = Mathf.RoundToInt(def.hp * mult);
                State.enemy.hp = Mathf.Clamp(State.enemy.hp, 1, State.enemy.maxHp);
            }
            Job = State.job; Difficulty = State.diff;
            Emit();
        }

        public void ClearCurrentRun()
        {
            if (State != null) SaveSystem.DeleteForRun(State.runId);
            gameStarted = false;
        }

        public bool Save(string name, out string message)
        {
            if (State == null || State.phase != 0) { message = "只能在玩家回合存档"; return false; }
            return SaveSystem.Write(State, name, out message);
        }

        public bool SaveOnExit(out string message)
        {
            if (!HasActiveRun) { message = "没有未完成的对局"; return false; }
            return SaveSystem.Write(State, "退出自动存档", out message);
        }

        private void Log(string text, string kind)
        {
            if (State == null) return;
            State.logs.Insert(0, new LogEntry { turn = "T" + State.turn, text = text, kind = kind });
            if (State.logs.Count > Tuning.logLimit) State.logs.RemoveAt(State.logs.Count - 1);
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(0, i + 1);
                var t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

    }
}
