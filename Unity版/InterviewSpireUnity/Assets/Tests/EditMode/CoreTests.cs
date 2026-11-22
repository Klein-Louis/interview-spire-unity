using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace InterviewSpire.Tests
{
    public class CoreTests
    {
        private GameObject go;
        private GameController game;
        private string oldStatsPath, oldSaveDir;

        [SetUp]
        public void SetUp()
        {
            GameDatabase.Load();
            GameDatabase.LoadImported();
            oldStatsPath = StatsSystem.PathOverride;
            oldSaveDir = SaveSystem.SaveDirOverride;
            string temp = Path.Combine(Path.GetTempPath(), "interviewspire_nunit_" + System.Guid.NewGuid().ToString("N"));
            StatsSystem.PathOverride = Path.Combine(temp, "stats.json");
            SaveSystem.SaveDirOverride = Path.Combine(temp, "saves");
            go = new GameObject("TestGame");
            game = go.AddComponent<GameController>();
            game.Job = "软件开发";
            game.Difficulty = "标准";
            game.NewGame();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            StatsSystem.PathOverride = oldStatsPath;
            SaveSystem.SaveDirOverride = oldSaveDir;
        }

        [Test]
        public void DmgIfLowHpAddsBonus()
        {
            var s = game.State;
            s.enemy.hp = 1000; s.enemy.maxHp = 1000;
            s.confidence = 5; s.maxConfidence = 20;
            s.question = new QuestionRuntime { type = "通用", text = "测试" };
            s.hand.Clear();
            s.hand.Add(new RuntimeCard { key = "perf", uid = 9001 });
            s.phase = 0; s.energy = 10;
            int before = s.enemy.hp;
            game.PlayCard(9001);
            Assert.AreEqual(10, before - s.enemy.hp);
        }

        [Test]
        public void SaveRoundtripKeepsDeckAndHand()
        {
            string msg;
            Assert.IsTrue(SaveSystem.Write(game.State, "单元测试", out msg));
            string error;
            var loaded = SaveSystem.Read(msg, out error);
            Assert.IsNotNull(loaded, error);
            Assert.AreEqual(game.State.masterDeck.Count, loaded.masterDeck.Count);
            Assert.AreEqual(game.State.hand.Count, loaded.hand.Count);
            Assert.AreEqual(2, loaded.saveVersion);
            Assert.IsTrue(SaveSystem.Delete(msg));
        }

        [Test]
        public void SameSeedProducesSameRewardPool()
        {
            var otherGo = new GameObject("TestGame2");
            var other = otherGo.AddComponent<GameController>();
            other.NewGame();
            game.State.seed = 12345; game.State.rngCounter = 0;
            other.State.seed = 12345; other.State.rngCounter = 0;
            var a = game.RandomRewardKeys(3);
            var b = other.RandomRewardKeys(3);
            Object.DestroyImmediate(otherGo);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void AutoPlayReachesGameOver()
        {
            int guard = 0;
            while (game.State.phase != 3 && guard++ < 300)
            {
                if (game.State.phase == 0)
                {
                    foreach (int uid in game.State.hand.Select(c => c.uid).ToList())
                    {
                        if (game.State.phase != 0) break;
                        game.PlayCard(uid);
                    }
                    if (game.State.phase == 0) game.EndTurn();
                }
                else if (game.State.phase == 1) game.SimulateEnemyAct();
                else if (game.State.phase == 2) game.TakeReward("");
            }
            Assert.AreEqual(3, game.State.phase, "自动对局未在 300 步内结束");
            Assert.Greater(StatsSystem.Load().runs.Count, 0);
        }

        [Test]
        public void StarExcellentAnswerDealsHighDamage()
        {
            var s = game.State;
            s.enemy.hp = 1000; s.enemy.maxHp = 1000;
            s.phase = 0; s.starUsedThisTurn = false;
            var meta = GameDatabase.Data.question_meta.Find(m => m.id == s.currentQuestionId);
            Assert.IsNotNull(meta);
            string answer = "当时的背景是接口优化，我的目标是降低延迟。" +
                            "我负责缓存改造，使用" + meta.keyPoints[0].term + "和" +
                            meta.keyPoints[1].term + "完成验证，" +
                            "最终上线后耗时从200ms降到50ms，提升30%。";
            int before = s.enemy.hp;
            var report = game.UseStar(answer);
            Assert.AreEqual("优秀", report.grade);
            Assert.AreEqual(10, report.damage);
            Assert.Greater(before - s.enemy.hp, 0);
            Assert.IsTrue(s.starUsedThisTurn);
            Assert.IsNull(game.UseStar(answer));
        }

        [Test]
        public void EmptyStarAnswerAppliesPressureOncePerTurn()
        {
            var s = game.State;
            s.phase = 0; s.starUsedThisTurn = false; s.interviewerPressured = false;
            var report = game.UseStar("");
            Assert.AreEqual("不合格", report.grade);
            Assert.AreEqual(3, report.damage);
            Assert.IsTrue(s.interviewerPressured);
            Assert.IsNull(game.UseStar("再来一次"));
        }

        [Test]
        public void SkipStarCapsEnemyBlockAtTwenty()
        {
            var s = game.State;
            s.phase = 0; s.starUsedThisTurn = false;
            s.enemy.block = 15;
            Assert.IsTrue(game.SkipStar());
            Assert.AreEqual(20, s.enemy.block);
            Assert.IsTrue(s.starUsedThisTurn);
        }

        [Test]
        public void PressureConsumedOnNextAttack()
        {
            var s = game.State;
            s.phase = 1;
            s.interviewerPressured = true;
            s.enemy.intent = new IntentData { name = "追问", type = "attack", value = 10 };
            s.confidence = 100; s.maxConfidence = 100; s.block = 0;
            int before = s.confidence;
            game.SimulateEnemyAct();
            Assert.IsFalse(s.interviewerPressured);
            Assert.AreEqual(15, before - s.confidence);
        }

        [Test]
        public void StarQualifiedAnswerDealsFiveDamage()
        {
            var s = game.State;
            s.phase = 0; s.starUsedThisTurn = false;
            string[] markers = { "背景", "当时", "情况", "场景", "任务", "目标", "职责", "需要",
                                 "我负责", "我的动作", "我先", "我推动", "我采取", "行动",
                                 "结果", "最终", "指标", "上线", "提升", "达成" };
            var meta = GameDatabase.Data.question_meta.Find(m =>
                m.keyPoints.Count >= 2 &&
                m.keyPoints.Take(2).All(k => !markers.Any(w => k.term.Contains(w))));
            Assert.IsNotNull(meta);
            s.currentQuestionId = meta.id;
            s.question = new QuestionRuntime { id = meta.id, type = meta.type, text = meta.text };
            var report = game.UseStar("当时的情况是" + meta.keyPoints[0].term + "和" +
                                      meta.keyPoints[1].term + "，用小时级数据说明。");
            Assert.AreEqual(60, report.score);
            Assert.AreEqual("合格", report.grade);
            Assert.AreEqual(5, report.damage);
            Assert.IsFalse(s.interviewerPressured);
        }

        [Test]
        public void PressurePersistsWhenAttackFullyBlocked()
        {
            var s = game.State;
            s.phase = 1;
            s.interviewerPressured = true;
            s.enemy.intent = new IntentData { name = "追问", type = "attack", value = 10 };
            s.confidence = 100; s.maxConfidence = 100; s.block = 100;
            game.SimulateEnemyAct();
            Assert.IsTrue(s.interviewerPressured);
            Assert.AreEqual(100, s.confidence);
        }

        [Test]
        public void MultipleSynonymsCountAsOneKeyPoint()
        {
            var s = game.State;
            s.phase = 0; s.starUsedThisTurn = false;
            var meta = GameDatabase.Data.question_meta.Find(m => m.id == "软件开发_技术_01");
            Assert.IsNotNull(meta);
            s.currentQuestionId = meta.id;
            s.question = new QuestionRuntime { id = meta.id, type = meta.type, text = meta.text };
            var kp = meta.keyPoints.Find(k => k.term == "内存管理");
            var report = game.UseStar(kp.term + "、" + kp.synonyms[0] + "、" +
                                      kp.synonyms[1] + "这些表述都可以。");
            Assert.AreEqual(1, report.hitTerms.Count);
            Assert.AreEqual("内存管理", report.hitTerms[0]);
        }

        [Test]
        public void StarDoesNotEnterAnyPile()
        {
            var s = game.State;
            s.phase = 0; s.starUsedThisTurn = false;
            int draw = s.drawPile.Count, discard = s.discard.Count, exhaust = s.exhaust.Count;
            game.UseStar("");
            Assert.AreEqual(draw, s.drawPile.Count);
            Assert.AreEqual(discard, s.discard.Count);
            Assert.AreEqual(exhaust, s.exhaust.Count);
        }

        [Test]
        public void QuestionIdsDoNotRepeatWithinRun()
        {
            var method = typeof(GameController).GetMethod("RandomQuestion",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method);
            game.State.askedQuestionIds.Clear();
            var weight = GameDatabase.Data.job_weights.Find(w => w.job == game.State.job);
            Assert.IsNotNull(weight);
            var original = new System.Collections.Generic.List<WeightData>(weight.weights);
            try
            {
                weight.weights.Clear();
                weight.weights.Add(new WeightData { type = "通用", weight = 1 });
                var seen = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < 15; i++)
                {
                    var q = (QuestionRuntime)method.Invoke(game, null);
                    Assert.IsFalse(string.IsNullOrEmpty(q.id));
                    Assert.IsTrue(seen.Add(q.id), "同局出现重复问题：" + q.id);
                }
                Assert.AreEqual(15, seen.Count);
            }
            finally { weight.weights.Clear(); weight.weights.AddRange(original); }
        }

        [Test]
        public void PressureDoesNotStack()
        {
            var s = game.State;
            s.phase = 0; s.starUsedThisTurn = false; s.interviewerPressured = true;
            game.UseStar("");
            Assert.IsTrue(s.interviewerPressured);
            s.starUsedThisTurn = false;
            game.UseStar("仍然不合格");
            Assert.IsTrue(s.interviewerPressured);
        }

        [Test]
        public void SaveLoadPreservesStarState()
        {
            var s = game.State;
            s.phase = 0; s.starUsedThisTurn = false; s.interviewerPressured = false;
            s.enemy.hp = 1000; s.enemy.maxHp = 1000;
            game.UseStar("");
            Assert.IsTrue(s.starUsedThisTurn);
            Assert.IsTrue(s.interviewerPressured);
            string msg;
            Assert.IsTrue(SaveSystem.Write(s, "Star状态测试", out msg));
            string error;
            var loaded = SaveSystem.Read(msg, out error);
            Assert.IsNotNull(loaded, error);
            Assert.IsTrue(loaded.starUsedThisTurn);
            Assert.IsTrue(loaded.interviewerPressured);
            Assert.AreEqual(s.currentQuestionId, loaded.currentQuestionId);
            CollectionAssert.AreEqual(s.askedQuestionIds, loaded.askedQuestionIds);
            SaveSystem.Delete(msg);
        }

        [Test]
        public void AllCardDamageAtMostTen()
        {
            foreach (var card in GameDatabase.Data.cards)
            {
                int max = 0;
                foreach (var op in card.ops)
                {
                    if (op.type == "dmg" && op.args.Count > 0) max += int.Parse(op.args[0]);
                    if (op.type == "dmg_if" && op.args.Count > 0)
                    {
                        max += int.Parse(op.args[0]);
                        if (op.args.Count > 3) max += int.Parse(op.args[3]);
                    }
                }
                Assert.LessOrEqual(max, 10, card.key + " 伤害超过 10");
            }
        }

        [Test]
        public void HighDamageOrMultiEffectCardsCostTwo()
        {
            foreach (var card in GameDatabase.Data.cards)
            {
                int max = 0;
                foreach (var op in card.ops)
                {
                    if (op.type == "dmg" && op.args.Count > 0) max += int.Parse(op.args[0]);
                    if (op.type == "dmg_if" && op.args.Count > 0)
                    {
                        max += int.Parse(op.args[0]);
                        if (op.args.Count > 3) max += int.Parse(op.args[3]);
                    }
                }
                if (max > 7 || card.ops.Count >= 2)
                    Assert.AreEqual(2, card.cost, card.key + " 费用应为 2");
            }
        }

        [Test]
        public void LoadStateRefreshesEnemyHpFromCurrentData()
        {
            var s = game.State;
            s.floor = 1;
            s.diff = "标准";
            s.enemy.hp = 999; s.enemy.maxHp = 999;
            var snapshot = JsonUtility.ToJson(s);
            var loaded = JsonUtility.FromJson<GameState>(snapshot);
            game.LoadState(loaded);
            Assert.AreEqual(GameDatabase.Data.enemies[1].hp, game.State.enemy.maxHp);
            Assert.LessOrEqual(game.State.enemy.hp, game.State.enemy.maxHp);
        }

        [Test]
        public void NewGameStartsWithFiveEnergy()
        {
            game.NewGame();
            Assert.AreEqual(5, game.State.maxEnergy);
            Assert.AreEqual(5, game.State.energy);
        }

        [Test]
        public void OpeningDrawsFiveFromTenCardDeck()
        {
            game.NewGame();
            Assert.AreEqual(10, game.State.masterDeck.Count);
            Assert.AreEqual(5, game.State.hand.Count);
            Assert.AreEqual(5, game.State.drawPile.Count);
            Assert.AreEqual(0, game.State.discard.Count);
        }

        [Test]
        public void EndTurnKeepsHandOutOfDiscard()
        {
            game.NewGame();
            int hand = game.State.hand.Count;
            int discard = game.State.discard.Count;
            game.EndTurn();
            Assert.AreEqual(hand, game.State.hand.Count);
            Assert.AreEqual(discard, game.State.discard.Count);
        }

        [Test]
        public void PlayerBlockPersistsAndStacks()
        {
            game.NewGame();
            game.State.block = 7;
            game.State.nextBlock = 3;
            var method = typeof(GameController).GetMethod("StartPlayerTurn",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(game, new object[] { false });
            Assert.AreEqual(10, game.State.block);
        }

        [Test]
        public void EnemyBlockPersistsAcrossEnemyAction()
        {
            game.NewGame();
            var s = game.State;
            s.phase = 1;
            s.enemy.block = 5;
            s.enemy.intent = new IntentData { name = "测试", type = "block", value = 4 };
            game.SimulateEnemyAct();
            Assert.AreEqual(9, s.enemy.block);
        }

        [Test]
        public void RewardCardEntersNextBattleDrawPile()
        {
            game.NewGame();
            game.State.phase = 2;
            game.TakeReward("tech");
            Assert.IsTrue(game.State.masterDeck.Any(c => c.key == "tech"));
            Assert.IsTrue(game.State.hand.Any(c => c.key == "tech") ||
                          game.State.drawPile.Any(c => c.key == "tech"));
        }

        [Test]
        public void EndTurnWithoutStarGivesEnemyTenBlock()
        {
            game.NewGame();
            game.State.enemy.block = 0;
            game.EndTurn();
            Assert.AreEqual(10, game.State.enemy.block);
            Assert.IsTrue(game.State.starUsedThisTurn);
        }
    }
}
