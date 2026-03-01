using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace SoR.Editor
{
    public class GachaSimulator : EditorWindow
    {
        private int _pullCount = 10000;
        private int _commonCount, _rareCount, _legendaryCount, _mythicCount;
        private float _commonRate, _rareRate, _legendaryRate, _mythicRate;
        private bool _hasResults;

        // Configurable rates
        private float _inputCommonRate = 0.55f;
        private float _inputRareRate = 0.35f;
        private float _inputLegendaryRate = 0.09f;
        private float _inputMythicRate = 0.01f;

        // Pity settings
        private int _legendarySoftPity = 70;
        private int _legendaryHardPity = 90;
        private int _mythicHardPity = 180;
        private float _softPityBoost = 0.05f;

        [MenuItem("Window/Seeds of Ruin/Gacha Simulator")]
        public static void ShowWindow()
        {
            GetWindow<GachaSimulator>("Gacha Simulator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Gacha Pull Simulator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _pullCount = EditorGUILayout.IntField("Number of Pulls", _pullCount);

            EditorGUILayout.Space();
            GUILayout.Label("Base Rates", EditorStyles.boldLabel);
            _inputCommonRate = EditorGUILayout.FloatField("Common Rate", _inputCommonRate);
            _inputRareRate = EditorGUILayout.FloatField("Rare Rate", _inputRareRate);
            _inputLegendaryRate = EditorGUILayout.FloatField("Legendary Rate", _inputLegendaryRate);
            _inputMythicRate = EditorGUILayout.FloatField("Mythic Rate", _inputMythicRate);

            float rateSum = _inputCommonRate + _inputRareRate + _inputLegendaryRate + _inputMythicRate;
            if (Mathf.Abs(rateSum - 1f) > 0.001f)
                EditorGUILayout.HelpBox($"Rates sum to {rateSum:F3}, should be 1.0", MessageType.Warning);

            EditorGUILayout.Space();
            GUILayout.Label("Pity Settings", EditorStyles.boldLabel);
            _legendarySoftPity = EditorGUILayout.IntField("Legendary Soft Pity", _legendarySoftPity);
            _legendaryHardPity = EditorGUILayout.IntField("Legendary Hard Pity", _legendaryHardPity);
            _mythicHardPity = EditorGUILayout.IntField("Mythic Hard Pity", _mythicHardPity);
            _softPityBoost = EditorGUILayout.FloatField("Soft Pity Rate Boost", _softPityBoost);

            EditorGUILayout.Space();
            if (GUILayout.Button("Simulate Pulls"))
                RunSimulation();

            if (_hasResults)
            {
                EditorGUILayout.Space();
                GUILayout.Label("Results", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Common", $"{_commonCount} ({_commonRate:P2})");
                EditorGUILayout.LabelField("Rare", $"{_rareCount} ({_rareRate:P2})");
                EditorGUILayout.LabelField("Legendary", $"{_legendaryCount} ({_legendaryRate:P2})");
                EditorGUILayout.LabelField("Mythic", $"{_mythicCount} ({_mythicRate:P2})");

                EditorGUILayout.Space();
                float legendaryDiff = Mathf.Abs(_legendaryRate - _inputLegendaryRate);
                float mythicDiff = Mathf.Abs(_mythicRate - _inputMythicRate);

                string status = (legendaryDiff <= 0.005f && mythicDiff <= 0.005f)
                    ? "PASS - Rates within 0.5% tolerance"
                    : "FAIL - Rates outside 0.5% tolerance";
                EditorGUILayout.HelpBox(status, legendaryDiff <= 0.005f && mythicDiff <= 0.005f
                    ? MessageType.Info : MessageType.Error);
            }
        }

        private void RunSimulation()
        {
            _commonCount = _rareCount = _legendaryCount = _mythicCount = 0;
            int pullsSinceLegendary = 0;
            int pullsSinceMythic = 0;

            for (int i = 0; i < _pullCount; i++)
            {
                pullsSinceLegendary++;
                pullsSinceMythic++;

                // Hard pity
                if (pullsSinceMythic >= _mythicHardPity)
                {
                    _mythicCount++; pullsSinceMythic = 0; pullsSinceLegendary = 0;
                    continue;
                }
                if (pullsSinceLegendary >= _legendaryHardPity)
                {
                    _legendaryCount++; pullsSinceLegendary = 0;
                    continue;
                }

                // Soft pity
                float legendaryRate = _inputLegendaryRate;
                if (pullsSinceLegendary >= _legendarySoftPity)
                    legendaryRate += (pullsSinceLegendary - _legendarySoftPity) * _softPityBoost;

                float roll = Random.value;
                if (roll < _inputMythicRate) { _mythicCount++; pullsSinceMythic = 0; pullsSinceLegendary = 0; }
                else if (roll < _inputMythicRate + legendaryRate) { _legendaryCount++; pullsSinceLegendary = 0; }
                else if (roll < _inputMythicRate + legendaryRate + _inputRareRate) _rareCount++;
                else _commonCount++;
            }

            _commonRate = (float)_commonCount / _pullCount;
            _rareRate = (float)_rareCount / _pullCount;
            _legendaryRate = (float)_legendaryCount / _pullCount;
            _mythicRate = (float)_mythicCount / _pullCount;
            _hasResults = true;
        }
    }
}
