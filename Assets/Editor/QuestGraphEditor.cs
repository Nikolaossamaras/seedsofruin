using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using SoR.Systems.Quests;

namespace SoR.Editor
{
    public class QuestGraphEditor : EditorWindow
    {
        private Vector2 _scrollPosition;
        private Vector2 _graphOffset;
        private List<QuestDefinitionSO> _quests = new();
        private Dictionary<string, Rect> _nodeRects = new();
        private QuestDefinitionSO _selectedQuest;
        private bool _isDragging;
        private Vector2 _dragStart;

        private const float NodeWidth = 200f;
        private const float NodeHeight = 60f;
        private const float NodePaddingX = 50f;
        private const float NodePaddingY = 30f;

        [MenuItem("Window/Seeds of Ruin/Quest Graph Editor")]
        public static void ShowWindow()
        {
            GetWindow<QuestGraphEditor>("Quest Graph Editor");
        }

        private void OnEnable()
        {
            RefreshQuests();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawGraph();
            DrawInspector();
            HandleInput();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                RefreshQuests();

            if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(80)))
                AutoLayout();

            GUILayout.FlexibleSpace();
            GUILayout.Label($"{_quests.Count} quests loaded", EditorStyles.toolbarButton);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGraph()
        {
            Rect graphArea = new Rect(0, 20, position.width - 250, position.height - 20);
            GUI.Box(graphArea, GUIContent.none, EditorStyles.helpBox);

            _scrollPosition = GUI.BeginScrollView(graphArea, _scrollPosition,
                new Rect(0, 0, 3000 + _graphOffset.x, 3000 + _graphOffset.y));

            // Draw connections first (behind nodes)
            DrawConnections();

            // Draw nodes
            foreach (var quest in _quests)
            {
                if (!_nodeRects.ContainsKey(quest.QuestId))
                    continue;

                Rect nodeRect = _nodeRects[quest.QuestId];
                bool isSelected = _selectedQuest == quest;

                // Node background color based on quest type
                Color nodeColor = quest.Type switch
                {
                    QuestType.MainStory => new Color(0.8f, 0.3f, 0.3f, 1f),
                    QuestType.SideQuest => new Color(0.3f, 0.6f, 0.8f, 1f),
                    QuestType.GuildContract => new Color(0.3f, 0.8f, 0.4f, 1f),
                    QuestType.CompanionQuest => new Color(0.8f, 0.6f, 0.3f, 1f),
                    _ => Color.gray
                };

                if (isSelected)
                    nodeColor = Color.Lerp(nodeColor, Color.white, 0.3f);

                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = nodeColor;
                GUI.Box(nodeRect, GUIContent.none, EditorStyles.helpBox);
                GUI.backgroundColor = prevBg;

                // Node label
                GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                GUIStyle idStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.7f) }
                };

                Rect nameRect = new Rect(nodeRect.x, nodeRect.y + 8, nodeRect.width, 20);
                Rect idRect = new Rect(nodeRect.x, nodeRect.y + 28, nodeRect.width, 16);
                Rect typeRect = new Rect(nodeRect.x, nodeRect.y + 42, nodeRect.width, 14);

                GUI.Label(nameRect, quest.QuestName, nameStyle);
                GUI.Label(idRect, quest.QuestId, idStyle);
                GUI.Label(typeRect, quest.Type.ToString(), idStyle);

                // Click detection
                if (Event.current.type == EventType.MouseDown && nodeRect.Contains(Event.current.mousePosition))
                {
                    _selectedQuest = quest;
                    Event.current.Use();
                    Repaint();
                }
            }

            GUI.EndScrollView();
        }

        private void DrawConnections()
        {
            Handles.BeginGUI();

            foreach (var quest in _quests)
            {
                if (quest.PrerequisiteQuestIds == null || !_nodeRects.ContainsKey(quest.QuestId))
                    continue;

                Rect targetRect = _nodeRects[quest.QuestId];
                Vector2 targetPoint = new Vector2(targetRect.x, targetRect.y + targetRect.height * 0.5f);

                foreach (string prereqId in quest.PrerequisiteQuestIds)
                {
                    if (string.IsNullOrEmpty(prereqId) || !_nodeRects.ContainsKey(prereqId))
                        continue;

                    Rect sourceRect = _nodeRects[prereqId];
                    Vector2 sourcePoint = new Vector2(
                        sourceRect.x + sourceRect.width,
                        sourceRect.y + sourceRect.height * 0.5f);

                    // Draw bezier curve
                    float tangentOffset = Mathf.Abs(targetPoint.x - sourcePoint.x) * 0.4f;
                    Vector3 startTangent = new Vector3(sourcePoint.x + tangentOffset, sourcePoint.y, 0);
                    Vector3 endTangent = new Vector3(targetPoint.x - tangentOffset, targetPoint.y, 0);

                    Handles.DrawBezier(
                        new Vector3(sourcePoint.x, sourcePoint.y, 0),
                        new Vector3(targetPoint.x, targetPoint.y, 0),
                        startTangent, endTangent,
                        new Color(1f, 1f, 0.4f, 0.8f), null, 3f);

                    // Draw arrow head
                    Vector2 dir = (targetPoint - sourcePoint).normalized;
                    Vector2 arrowBase = targetPoint - dir * 10f;
                    Vector2 perp = new Vector2(-dir.y, dir.x) * 5f;

                    Handles.color = new Color(1f, 1f, 0.4f, 0.8f);
                    Handles.DrawAAConvexPolygon(
                        new Vector3(targetPoint.x, targetPoint.y, 0),
                        new Vector3(arrowBase.x + perp.x, arrowBase.y + perp.y, 0),
                        new Vector3(arrowBase.x - perp.x, arrowBase.y - perp.y, 0));
                }
            }

            Handles.EndGUI();
        }

        private void DrawInspector()
        {
            Rect inspectorArea = new Rect(position.width - 248, 20, 246, position.height - 20);
            GUILayout.BeginArea(inspectorArea);
            EditorGUILayout.LabelField("Quest Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (_selectedQuest != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Name", _selectedQuest.QuestName);
                EditorGUILayout.TextField("ID", _selectedQuest.QuestId);
                EditorGUILayout.EnumPopup("Type", _selectedQuest.Type);
                EditorGUILayout.IntField("Required Level", _selectedQuest.RequiredLevel);
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_selectedQuest.Description, GUILayout.Height(60));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Prerequisites", EditorStyles.boldLabel);
                if (_selectedQuest.PrerequisiteQuestIds != null)
                {
                    foreach (string prereq in _selectedQuest.PrerequisiteQuestIds)
                        EditorGUILayout.LabelField("  - " + prereq);
                }
                else
                {
                    EditorGUILayout.LabelField("  (none)");
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Objectives: {_selectedQuest.Objectives?.Count ?? 0}");
                EditorGUILayout.LabelField($"Rewards: {_selectedQuest.Rewards?.Count ?? 0}");
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();
                if (GUILayout.Button("Select in Project"))
                {
                    Selection.activeObject = _selectedQuest;
                    EditorGUIUtility.PingObject(_selectedQuest);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a quest node to view details.", MessageType.Info);
            }

            GUILayout.EndArea();
        }

        private void HandleInput()
        {
            // Middle-mouse drag to pan
            if (Event.current.type == EventType.MouseDown && Event.current.button == 2)
            {
                _isDragging = true;
                _dragStart = Event.current.mousePosition;
                Event.current.Use();
            }

            if (Event.current.type == EventType.MouseDrag && _isDragging)
            {
                Vector2 delta = Event.current.mousePosition - _dragStart;
                _scrollPosition -= delta;
                _dragStart = Event.current.mousePosition;
                Event.current.Use();
                Repaint();
            }

            if (Event.current.type == EventType.MouseUp && Event.current.button == 2)
            {
                _isDragging = false;
                Event.current.Use();
            }
        }

        private void RefreshQuests()
        {
            _quests.Clear();
            _nodeRects.Clear();

            string[] guids = AssetDatabase.FindAssets("t:QuestDefinitionSO");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var quest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(path);
                if (quest != null)
                    _quests.Add(quest);
            }

            AutoLayout();
            Repaint();
        }

        private void AutoLayout()
        {
            _nodeRects.Clear();

            // Build dependency depth map
            Dictionary<string, int> depthMap = new();
            Dictionary<string, QuestDefinitionSO> questLookup = new();

            foreach (var quest in _quests)
            {
                if (!string.IsNullOrEmpty(quest.QuestId))
                    questLookup[quest.QuestId] = quest;
            }

            foreach (var quest in _quests)
                CalculateDepth(quest, questLookup, depthMap, new HashSet<string>());

            // Group quests by depth column
            Dictionary<int, List<QuestDefinitionSO>> columns = new();
            foreach (var quest in _quests)
            {
                int depth = depthMap.GetValueOrDefault(quest.QuestId, 0);
                if (!columns.ContainsKey(depth))
                    columns[depth] = new List<QuestDefinitionSO>();
                columns[depth].Add(quest);
            }

            // Position nodes
            float xOffset = 40f;
            foreach (var kvp in columns.OrderBy(k => k.Key))
            {
                float yOffset = 40f;
                foreach (var quest in kvp.Value)
                {
                    _nodeRects[quest.QuestId] = new Rect(xOffset, yOffset, NodeWidth, NodeHeight);
                    yOffset += NodeHeight + NodePaddingY;
                }
                xOffset += NodeWidth + NodePaddingX;
            }
        }

        private int CalculateDepth(QuestDefinitionSO quest,
            Dictionary<string, QuestDefinitionSO> lookup,
            Dictionary<string, int> depthMap,
            HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(quest.QuestId))
                return 0;

            if (depthMap.TryGetValue(quest.QuestId, out int cached))
                return cached;

            if (visited.Contains(quest.QuestId))
                return 0; // Prevent cycles

            visited.Add(quest.QuestId);

            int maxParentDepth = -1;
            if (quest.PrerequisiteQuestIds != null)
            {
                foreach (string prereqId in quest.PrerequisiteQuestIds)
                {
                    if (!string.IsNullOrEmpty(prereqId) && lookup.TryGetValue(prereqId, out var prereqQuest))
                    {
                        int parentDepth = CalculateDepth(prereqQuest, lookup, depthMap, visited);
                        if (parentDepth > maxParentDepth)
                            maxParentDepth = parentDepth;
                    }
                }
            }

            int depth = maxParentDepth + 1;
            depthMap[quest.QuestId] = depth;
            return depth;
        }
    }
}
