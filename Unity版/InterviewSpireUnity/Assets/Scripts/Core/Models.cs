using System;
using System.Collections.Generic;
using UnityEngine;

namespace InterviewSpire
{
    [Serializable] public class RuntimeCard
    {
        public string key;
        public int uid;
    }

    [Serializable] public class EnemyRuntime
    {
        public int index;
        public int hp, maxHp, block;
        public IntentData intent;
        public int lastIntentIndex;
    }

    [Serializable] public class QuestionRuntime { public string id, type, text; }

    [Serializable] public class LogEntry { public string turn, text, kind; }

    /// <summary>一局游戏的完整可序列化状态。</summary>
    [Serializable]
    public class GameState
    {
        public string saveName = "", savedAt = "", job = "软件开发", diff = "标准";
        public long runId;
        public int saveVersion = 2;
        public int rngCounter;
        public int floor, turn, phase; // phase: 0 玩家 1 敌方 2 奖励 3 结束
        public int confidence, maxConfidence, block, energy, maxEnergy;
        public int discount, cardsPlayed, universalBonus;
        public bool universalUsed, vulnerable, selfDoubt, energyPenalty, techChain;
        public bool skipDrawThisTurn;
        public List<string> openingHand = new List<string>();
        public int nextBlock, nextEnergy;
        public List<RuntimeCard> masterDeck = new List<RuntimeCard>();
        public List<RuntimeCard> hand = new List<RuntimeCard>();
        public List<RuntimeCard> drawPile = new List<RuntimeCard>();
        public List<RuntimeCard> discard = new List<RuntimeCard>();
        public List<RuntimeCard> exhaust = new List<RuntimeCard>();
        public List<string> upgraded = new List<string>();
        public EnemyRuntime enemy = new EnemyRuntime();
        public QuestionRuntime question = new QuestionRuntime();
        public List<LogEntry> logs = new List<LogEntry>();
        public string lastTag = "";
        public int seed, uidSeq;
        public int totalTurns, totalDamage, totalCards;
        public string openingBonus = "";
        public bool starUsedThisTurn;
        public bool interviewerPressured;
        public string currentQuestionId = "";
        public List<string> askedQuestionIds = new List<string>();
    }

    [Serializable] public class ScoreReport
    {
        public int score;
        public string grade;
        public int damage;
        public List<string> hitTerms = new List<string>();
        public List<string> missingTerms = new List<string>();
        public string suggestion = "";
    }

    [Serializable]
    public class SaveRecord
    {
        public string path, name, time, job, diff;
        public int floor;
        public long runId;
        public int saveVersion = 2;
        public int rngCounter;
    }
}
