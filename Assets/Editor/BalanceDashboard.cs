using UnityEngine;
using UnityEditor;

namespace SoR.Editor
{
    public class BalanceDashboard : EditorWindow
    {
        // Stat growth assumptions per level
        private float _strPerLevel = 3f;
        private float _vigPerLevel = 5f;
        private float _resPerLevel = 2f;
        private float _agiPerLevel = 1.5f;

        // Base stats at level 1
        private float _baseHealth = 1000f;
        private float _baseStr = 10f;
        private float _baseVig = 10f;
        private float _baseRes = 10f;
        private float _baseAgi = 10f;

        // Scaling formulas
        private float _healthPerVig = 25f;
        private float _dpsPerStr = 4.5f;
        private float _defensePerRes = 3f;
        private float _dodgePerAgi = 0.15f;

        // XP curve
        private float _xpBase = 100f;
        private float _xpExponent = 1.45f;

        // Display
        private int _maxLevel = 60;
        private Vector2 _scrollPosition;
        private enum GraphTab { DPS, EHP, XP, All }
        private GraphTab _currentTab = GraphTab.All;

        private const float GraphHeight = 200f;
        private const float GraphMarginLeft = 60f;
        private const float GraphMarginBottom = 30f;

        [MenuItem("Window/Seeds of Ruin/Balance Dashboard")]
        public static void ShowWindow()
        {
            GetWindow<BalanceDashboard>("Balance Dashboard");
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawSettings();
            EditorGUILayout.Space(10);
            DrawTabBar();
            EditorGUILayout.Space(10);
            DrawGraphs();
            EditorGUILayout.Space(10);
            DrawDataTable();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettings()
        {
            GUILayout.Label("Balance Dashboard", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Growth Per Level", EditorStyles.boldLabel);
            _strPerLevel = EditorGUILayout.FloatField("STR / Level", _strPerLevel);
            _vigPerLevel = EditorGUILayout.FloatField("VIG / Level", _vigPerLevel);
            _resPerLevel = EditorGUILayout.FloatField("RES / Level", _resPerLevel);
            _agiPerLevel = EditorGUILayout.FloatField("AGI / Level", _agiPerLevel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Base Stats (Level 1)", EditorStyles.boldLabel);
            _baseStr = EditorGUILayout.FloatField("Base STR", _baseStr);
            _baseVig = EditorGUILayout.FloatField("Base VIG", _baseVig);
            _baseRes = EditorGUILayout.FloatField("Base RES", _baseRes);
            _baseAgi = EditorGUILayout.FloatField("Base AGI", _baseAgi);
            _baseHealth = EditorGUILayout.FloatField("Base Health", _baseHealth);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Scaling Factors", EditorStyles.boldLabel);
            _healthPerVig = EditorGUILayout.FloatField("HP per VIG", _healthPerVig);
            _dpsPerStr = EditorGUILayout.FloatField("DPS per STR", _dpsPerStr);
            _defensePerRes = EditorGUILayout.FloatField("DEF per RES", _defensePerRes);
            _dodgePerAgi = EditorGUILayout.FloatField("Dodge% per AGI", _dodgePerAgi);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("XP Curve", EditorStyles.boldLabel);
            _xpBase = EditorGUILayout.FloatField("XP Base", _xpBase);
            _xpExponent = EditorGUILayout.FloatField("XP Exponent", _xpExponent);
            _maxLevel = EditorGUILayout.IntSlider("Max Level", _maxLevel, 10, 100);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_currentTab == GraphTab.All, "All Graphs", EditorStyles.toolbarButton))
                _currentTab = GraphTab.All;
            if (GUILayout.Toggle(_currentTab == GraphTab.DPS, "DPS by Level", EditorStyles.toolbarButton))
                _currentTab = GraphTab.DPS;
            if (GUILayout.Toggle(_currentTab == GraphTab.EHP, "EHP by Level", EditorStyles.toolbarButton))
                _currentTab = GraphTab.EHP;
            if (GUILayout.Toggle(_currentTab == GraphTab.XP, "XP Curve", EditorStyles.toolbarButton))
                _currentTab = GraphTab.XP;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGraphs()
        {
            if (_currentTab == GraphTab.DPS || _currentTab == GraphTab.All)
            {
                GUILayout.Label("DPS by Level", EditorStyles.boldLabel);
                Rect dpsRect = GUILayoutUtility.GetRect(position.width - 20, GraphHeight);
                DrawDPSGraph(dpsRect);
                EditorGUILayout.Space(10);
            }

            if (_currentTab == GraphTab.EHP || _currentTab == GraphTab.All)
            {
                GUILayout.Label("Effective HP by Level", EditorStyles.boldLabel);
                Rect ehpRect = GUILayoutUtility.GetRect(position.width - 20, GraphHeight);
                DrawEHPGraph(ehpRect);
                EditorGUILayout.Space(10);
            }

            if (_currentTab == GraphTab.XP || _currentTab == GraphTab.All)
            {
                GUILayout.Label("XP Required per Level", EditorStyles.boldLabel);
                Rect xpRect = GUILayoutUtility.GetRect(position.width - 20, GraphHeight);
                DrawXPGraph(xpRect);
            }
        }

        private void DrawDPSGraph(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.15f, 1f));

            float maxDPS = CalculateDPS(_maxLevel);
            Vector3[] points = new Vector3[_maxLevel];

            for (int level = 1; level <= _maxLevel; level++)
            {
                float dps = CalculateDPS(level);
                float x = area.x + GraphMarginLeft + (level - 1f) / (_maxLevel - 1f) * (area.width - GraphMarginLeft - 10);
                float y = area.yMax - GraphMarginBottom - (dps / maxDPS) * (area.height - GraphMarginBottom - 10);
                points[level - 1] = new Vector3(x, y, 0);
            }

            Handles.BeginGUI();
            Handles.color = new Color(1f, 0.4f, 0.3f, 1f);
            Handles.DrawAAPolyLine(3f, points);
            Handles.EndGUI();

            DrawGraphAxes(area, "Level", "DPS", 0, maxDPS);
        }

        private void DrawEHPGraph(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.15f, 1f));

            float maxEHP = CalculateEHP(_maxLevel);
            Vector3[] points = new Vector3[_maxLevel];

            for (int level = 1; level <= _maxLevel; level++)
            {
                float ehp = CalculateEHP(level);
                float x = area.x + GraphMarginLeft + (level - 1f) / (_maxLevel - 1f) * (area.width - GraphMarginLeft - 10);
                float y = area.yMax - GraphMarginBottom - (ehp / maxEHP) * (area.height - GraphMarginBottom - 10);
                points[level - 1] = new Vector3(x, y, 0);
            }

            Handles.BeginGUI();
            Handles.color = new Color(0.3f, 0.8f, 0.4f, 1f);
            Handles.DrawAAPolyLine(3f, points);
            Handles.EndGUI();

            DrawGraphAxes(area, "Level", "EHP", 0, maxEHP);
        }

        private void DrawXPGraph(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.15f, 1f));

            float maxXP = CalculateXPForLevel(_maxLevel);
            Vector3[] points = new Vector3[_maxLevel];

            for (int level = 1; level <= _maxLevel; level++)
            {
                float xp = CalculateXPForLevel(level);
                float x = area.x + GraphMarginLeft + (level - 1f) / (_maxLevel - 1f) * (area.width - GraphMarginLeft - 10);
                float y = area.yMax - GraphMarginBottom - (xp / maxXP) * (area.height - GraphMarginBottom - 10);
                points[level - 1] = new Vector3(x, y, 0);
            }

            Handles.BeginGUI();
            Handles.color = new Color(0.4f, 0.7f, 1f, 1f);
            Handles.DrawAAPolyLine(3f, points);
            Handles.EndGUI();

            DrawGraphAxes(area, "Level", "XP", 0, maxXP);
        }

        private void DrawGraphAxes(Rect area, string xLabel, string yLabel, float yMin, float yMax)
        {
            GUIStyle axisLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) },
                alignment = TextAnchor.MiddleCenter
            };

            // Y axis labels
            GUIStyle yLabelStyle = new GUIStyle(axisLabelStyle)
            {
                alignment = TextAnchor.MiddleRight
            };

            int ySteps = 4;
            for (int i = 0; i <= ySteps; i++)
            {
                float ratio = (float)i / ySteps;
                float value = Mathf.Lerp(yMin, yMax, ratio);
                float y = area.yMax - GraphMarginBottom - ratio * (area.height - GraphMarginBottom - 10);
                Rect labelRect = new Rect(area.x, y - 8, GraphMarginLeft - 5, 16);
                GUI.Label(labelRect, FormatNumber(value), yLabelStyle);

                // Grid line
                Handles.BeginGUI();
                Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                float lineX = area.x + GraphMarginLeft;
                Handles.DrawLine(new Vector3(lineX, y, 0), new Vector3(area.xMax - 10, y, 0));
                Handles.EndGUI();
            }

            // X axis labels
            int xSteps = Mathf.Min(_maxLevel - 1, 6);
            for (int i = 0; i <= xSteps; i++)
            {
                float ratio = (float)i / xSteps;
                int level = Mathf.RoundToInt(Mathf.Lerp(1, _maxLevel, ratio));
                float x = area.x + GraphMarginLeft + ratio * (area.width - GraphMarginLeft - 10);
                Rect labelRect = new Rect(x - 15, area.yMax - GraphMarginBottom + 4, 30, 16);
                GUI.Label(labelRect, level.ToString(), axisLabelStyle);
            }

            // Axis titles
            Rect xTitleRect = new Rect(area.x + area.width * 0.5f - 20, area.yMax - 14, 40, 14);
            GUI.Label(xTitleRect, xLabel, axisLabelStyle);

            // Y-axis title (rotated via vertical text)
            GUIStyle yTitleStyle = new GUIStyle(axisLabelStyle) { alignment = TextAnchor.MiddleCenter };
            Rect yTitleRect = new Rect(area.x, area.y + 2, GraphMarginLeft - 5, 16);
            GUI.Label(yTitleRect, yLabel, yTitleStyle);
        }

        private void DrawDataTable()
        {
            GUILayout.Label("Data Table (every 10 levels)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Level", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.Label("STR", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.Label("VIG", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.Label("RES", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.Label("AGI", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.Label("DPS", EditorStyles.toolbarButton, GUILayout.Width(80));
            GUILayout.Label("HP", EditorStyles.toolbarButton, GUILayout.Width(80));
            GUILayout.Label("EHP", EditorStyles.toolbarButton, GUILayout.Width(80));
            GUILayout.Label("XP to Next", EditorStyles.toolbarButton, GUILayout.Width(100));
            GUILayout.Label("Cumulative XP", EditorStyles.toolbarButton, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            for (int level = 1; level <= _maxLevel; level += 10)
            {
                float str = _baseStr + (level - 1) * _strPerLevel;
                float vig = _baseVig + (level - 1) * _vigPerLevel;
                float res = _baseRes + (level - 1) * _resPerLevel;
                float agi = _baseAgi + (level - 1) * _agiPerLevel;
                float dps = CalculateDPS(level);
                float hp = CalculateHP(level);
                float ehp = CalculateEHP(level);
                float xpToNext = CalculateXPForLevel(level);
                float cumulativeXP = CalculateCumulativeXP(level);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(level.ToString(), GUILayout.Width(60));
                GUILayout.Label($"{str:F0}", GUILayout.Width(60));
                GUILayout.Label($"{vig:F0}", GUILayout.Width(60));
                GUILayout.Label($"{res:F0}", GUILayout.Width(60));
                GUILayout.Label($"{agi:F0}", GUILayout.Width(60));
                GUILayout.Label($"{dps:F0}", GUILayout.Width(80));
                GUILayout.Label($"{hp:F0}", GUILayout.Width(80));
                GUILayout.Label($"{ehp:F0}", GUILayout.Width(80));
                GUILayout.Label(FormatNumber(xpToNext), GUILayout.Width(100));
                GUILayout.Label(FormatNumber(cumulativeXP), GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
            }

            // Always show max level
            if (_maxLevel % 10 != 1)
            {
                int level = _maxLevel;
                float str = _baseStr + (level - 1) * _strPerLevel;
                float vig = _baseVig + (level - 1) * _vigPerLevel;
                float res = _baseRes + (level - 1) * _resPerLevel;
                float agi = _baseAgi + (level - 1) * _agiPerLevel;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(level.ToString(), GUILayout.Width(60));
                GUILayout.Label($"{str:F0}", GUILayout.Width(60));
                GUILayout.Label($"{vig:F0}", GUILayout.Width(60));
                GUILayout.Label($"{res:F0}", GUILayout.Width(60));
                GUILayout.Label($"{agi:F0}", GUILayout.Width(60));
                GUILayout.Label($"{CalculateDPS(level):F0}", GUILayout.Width(80));
                GUILayout.Label($"{CalculateHP(level):F0}", GUILayout.Width(80));
                GUILayout.Label($"{CalculateEHP(level):F0}", GUILayout.Width(80));
                GUILayout.Label(FormatNumber(CalculateXPForLevel(level)), GUILayout.Width(100));
                GUILayout.Label(FormatNumber(CalculateCumulativeXP(level)), GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
            }
        }

        // --- Calculation Methods ---

        private float CalculateDPS(int level)
        {
            float str = _baseStr + (level - 1) * _strPerLevel;
            return str * _dpsPerStr;
        }

        private float CalculateHP(int level)
        {
            float vig = _baseVig + (level - 1) * _vigPerLevel;
            return _baseHealth + vig * _healthPerVig;
        }

        private float CalculateEHP(int level)
        {
            float hp = CalculateHP(level);
            float res = _baseRes + (level - 1) * _resPerLevel;
            float agi = _baseAgi + (level - 1) * _agiPerLevel;

            // EHP = HP * (1 + DEF / 100) / (1 - dodge_chance)
            float defense = res * _defensePerRes;
            float defenseMultiplier = 1f + defense / 100f;
            float dodgeChance = Mathf.Clamp01(agi * _dodgePerAgi / 100f);
            float dodgeMultiplier = 1f / Mathf.Max(1f - dodgeChance, 0.01f);

            return hp * defenseMultiplier * dodgeMultiplier;
        }

        private float CalculateXPForLevel(int level)
        {
            return _xpBase * Mathf.Pow(level, _xpExponent);
        }

        private float CalculateCumulativeXP(int level)
        {
            float total = 0;
            for (int i = 1; i < level; i++)
                total += CalculateXPForLevel(i);
            return total;
        }

        private string FormatNumber(float value)
        {
            if (value >= 1000000f) return $"{value / 1000000f:F1}M";
            if (value >= 1000f) return $"{value / 1000f:F1}K";
            return $"{value:F0}";
        }
    }
}
