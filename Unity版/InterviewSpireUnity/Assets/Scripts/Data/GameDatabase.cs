using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace InterviewSpire
{
    [Serializable] public class OpData { public string type; public List<string> args = new List<string>(); }
    [Serializable] public class CardData
    {
        public string key, name, kind, tag, rarity, desc;
        public int cost;
        public bool exhaust;
        public List<OpData> ops = new List<OpData>();
    }
    [Serializable] public class DifficultyData
    {
        public string name, desc; public int hp; public float scale_mult, hp_mult;
    }
    [Serializable] public class IntentData
    {
        public string name, type, debuff; public int value;
    }
    [Serializable] public class EnemyData
    {
        public string name, sub, emoji, mechanic, mechanic_desc;
        public int hp; public List<IntentData> intents = new List<IntentData>();
    }
    [Serializable] public class KeyPointData { public string term; public List<string> synonyms = new List<string>(); }
    [Serializable] public class QuestionMeta
    {
        public string id, type, job, text;
        public List<KeyPointData> keyPoints = new List<KeyPointData>();
    }
    [Serializable] public class QuestionGroup { public string type; public List<string> texts = new List<string>(); }
    [Serializable] public class JobQuestionData { public string job; public List<QuestionGroup> groups = new List<QuestionGroup>(); }
    [Serializable] public class WeightData { public string type; public int weight; }
    [Serializable] public class JobWeightData { public string job; public List<WeightData> weights = new List<WeightData>(); }
    [Serializable] public class StatusData { public string key, text; }
    [Serializable] public class EventOptionData { public string label, effect; }
    [Serializable] public class EventData { public string name, desc; public List<EventOptionData> options = new List<EventOptionData>(); }
    [Serializable] public class TuningData
    {
        public int handSize = 5, handLimit = 8, logLimit = 12, drawPerTurn = 3, openingHandSize = 5;
        public float matchMult = 1.5f;
    }

    [Serializable] public class GameData
    {
        public List<CardData> cards = new List<CardData>();
        public List<string> starter = new List<string>();
        public List<DifficultyData> difficulties = new List<DifficultyData>();
        public List<float> intent_scale = new List<float>();
        public List<EnemyData> enemies = new List<EnemyData>();
        public List<QuestionGroup> questions = new List<QuestionGroup>();
        public List<QuestionMeta> question_meta = new List<QuestionMeta>();
        public List<JobQuestionData> job_questions = new List<JobQuestionData>();
        public List<JobWeightData> job_weights = new List<JobWeightData>();
        public List<StatusData> status_info = new List<StatusData>();
        public List<EventData> events = new List<EventData>();
        public TuningData tuning = new TuningData();
    }

    [Serializable] public class ImportedBank { public List<QuestionGroup> groups = new List<QuestionGroup>(); }

    /// <summary>读取 StreamingAssets/game_data.json，供全部系统共用。</summary>
    public static class GameDatabase
    {
        public static GameData Data { get; private set; }
        public static ImportedBank Imported { get; set; }

        public static string ImportedPath
        {
            get { return Path.Combine(Application.persistentDataPath, "题库.json"); }
        }

        public static void LoadImported()
        {
            Imported = new ImportedBank();
            if (!File.Exists(ImportedPath)) return;
            try { Imported = JsonUtility.FromJson<ImportedBank>(File.ReadAllText(ImportedPath, Encoding.UTF8)) ?? Imported; }
            catch (Exception ex) { Debug.LogWarning("导入题库读取失败：" + ex.Message); }
        }

        public static void SaveImported()
        {
            try { File.WriteAllText(ImportedPath, JsonUtility.ToJson(Imported, true), Encoding.UTF8); }
            catch (Exception ex) { Debug.LogWarning("导入题库保存失败：" + ex.Message); }
        }

        public static void Load()
        {
            if (Data != null) return;
            string path = Path.Combine(Application.streamingAssetsPath, "game_data.json");
            if (!File.Exists(path))
            {
                Debug.LogError("找不到数据表：" + path);
                Data = new GameData();
                return;
            }
            Data = JsonUtility.FromJson<GameData>(File.ReadAllText(path));
            Debug.Log("题库载入：" + Data.cards.Count + " 张卡，" + Data.enemies.Count + " 轮面试官");
        }

        public static CardData Card(string key)
        {
            return Data.cards.Find(c => c.key == key);
        }
    }
}
