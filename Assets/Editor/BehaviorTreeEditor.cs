using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using SoR.AI;

namespace SoR.Editor
{
    public class BehaviorTreeEditor : EditorWindow
    {
        private BehaviorTreeSO _selectedTree;
        private Vector2 _scrollPosition;
        private int _selectedNodeIndex = -1;

        private const float NodeWidth = 160f;
        private const float NodeHeight = 40f;
        private const float HorizontalSpacing = 40f;
        private const float VerticalSpacing = 60f;

        // Node type colors
        private static readonly Color SelectorColor = new Color(0.3f, 0.5f, 0.9f, 1f);
        private static readonly Color SequenceColor = new Color(0.3f, 0.8f, 0.4f, 1f);
        private static readonly Color ConditionColor = new Color(0.9f, 0.85f, 0.3f, 1f);
        private static readonly Color ActionColor = new Color(0.9f, 0.35f, 0.3f, 1f);
        private static readonly Color DefaultColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        // Cached layout data
        private Dictionary<int, Rect> _nodePositions = new();
        private Dictionary<int, List<int>> _childrenMap = new();
        private bool _layoutDirty = true;

        [MenuItem("Window/Seeds of Ruin/Behavior Tree Editor")]
        public static void ShowWindow()
        {
            GetWindow<BehaviorTreeEditor>("Behavior Tree Editor");
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_selectedTree == null || _selectedTree.Nodes == null || _selectedTree.Nodes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Select a BehaviorTreeSO asset to visualize it.\n\n" +
                    "Drag and drop a BehaviorTreeSO onto this window, or use the object picker above.",
                    MessageType.Info);
                HandleDragAndDrop();
                return;
            }

            if (_layoutDirty)
            {
                RebuildLayout();
                _layoutDirty = false;
            }

            DrawTreeGraph();
            DrawNodeInspector();
            HandleDragAndDrop();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            _selectedTree = (BehaviorTreeSO)EditorGUILayout.ObjectField(
                _selectedTree, typeof(BehaviorTreeSO), false, GUILayout.Width(250));
            if (EditorGUI.EndChangeCheck())
            {
                _layoutDirty = true;
                _selectedNodeIndex = -1;
            }

            if (_selectedTree != null)
            {
                GUILayout.Label($"  \"{_selectedTree.TreeName}\" - {_selectedTree.Nodes.Count} nodes",
                    EditorStyles.toolbarButton);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Rebuild Layout", EditorStyles.toolbarButton, GUILayout.Width(100)))
                _layoutDirty = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTreeGraph()
        {
            Rect graphArea = new Rect(0, 20, position.width - 260, position.height - 20);
            GUI.Box(graphArea, GUIContent.none, EditorStyles.helpBox);

            float maxX = 0, maxY = 0;
            foreach (var rect in _nodePositions.Values)
            {
                if (rect.xMax > maxX) maxX = rect.xMax;
                if (rect.yMax > maxY) maxY = rect.yMax;
            }

            _scrollPosition = GUI.BeginScrollView(graphArea, _scrollPosition,
                new Rect(0, 0, Mathf.Max(maxX + 100, graphArea.width), Mathf.Max(maxY + 100, graphArea.height)));

            // Draw connections
            DrawConnections();

            // Draw nodes
            for (int i = 0; i < _selectedTree.Nodes.Count; i++)
            {
                if (!_nodePositions.ContainsKey(i))
                    continue;

                BTNodeData nodeData = _selectedTree.Nodes[i];
                Rect nodeRect = _nodePositions[i];
                bool isSelected = _selectedNodeIndex == i;

                DrawNode(i, nodeData, nodeRect, isSelected);
            }

            GUI.EndScrollView();
        }

        private void DrawConnections()
        {
            Handles.BeginGUI();

            foreach (var kvp in _childrenMap)
            {
                int parentIndex = kvp.Key;
                if (!_nodePositions.ContainsKey(parentIndex))
                    continue;

                Rect parentRect = _nodePositions[parentIndex];
                Vector2 parentBottom = new Vector2(
                    parentRect.x + parentRect.width * 0.5f,
                    parentRect.yMax);

                foreach (int childIndex in kvp.Value)
                {
                    if (!_nodePositions.ContainsKey(childIndex))
                        continue;

                    Rect childRect = _nodePositions[childIndex];
                    Vector2 childTop = new Vector2(
                        childRect.x + childRect.width * 0.5f,
                        childRect.y);

                    // Draw connection line with a step pattern
                    float midY = (parentBottom.y + childTop.y) * 0.5f;

                    Handles.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
                    Handles.DrawAAPolyLine(3f,
                        new Vector3(parentBottom.x, parentBottom.y, 0),
                        new Vector3(parentBottom.x, midY, 0),
                        new Vector3(childTop.x, midY, 0),
                        new Vector3(childTop.x, childTop.y, 0));
                }
            }

            Handles.EndGUI();
        }

        private void DrawNode(int index, BTNodeData nodeData, Rect nodeRect, bool isSelected)
        {
            Color nodeColor = GetNodeColor(nodeData.NodeType);
            if (isSelected)
                nodeColor = Color.Lerp(nodeColor, Color.white, 0.35f);

            // Shadow
            Rect shadowRect = new Rect(nodeRect.x + 2, nodeRect.y + 2, nodeRect.width, nodeRect.height);
            EditorGUI.DrawRect(shadowRect, new Color(0, 0, 0, 0.3f));

            // Node body
            EditorGUI.DrawRect(nodeRect, nodeColor);

            // Border
            Color borderColor = isSelected ? Color.white : new Color(0, 0, 0, 0.4f);
            float borderWidth = isSelected ? 2f : 1f;
            Handles.BeginGUI();
            Handles.color = borderColor;
            Vector3[] corners = new Vector3[]
            {
                new(nodeRect.x, nodeRect.y, 0),
                new(nodeRect.xMax, nodeRect.y, 0),
                new(nodeRect.xMax, nodeRect.yMax, 0),
                new(nodeRect.x, nodeRect.yMax, 0),
                new(nodeRect.x, nodeRect.y, 0)
            };
            Handles.DrawAAPolyLine(borderWidth, corners);
            Handles.EndGUI();

            // Type label (small, top)
            GUIStyle typeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(1, 1, 1, 0.6f) },
                fontSize = 9
            };
            Rect typeRect = new Rect(nodeRect.x, nodeRect.y + 2, nodeRect.width, 14);
            GUI.Label(typeRect, $"[{nodeData.NodeType}]", typeStyle);

            // Name label (centered)
            GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = 11
            };
            Rect nameRect = new Rect(nodeRect.x, nodeRect.y + 12, nodeRect.width, nodeRect.height - 12);
            GUI.Label(nameRect, nodeData.NodeName, nameStyle);

            // Click handling
            if (Event.current.type == EventType.MouseDown && nodeRect.Contains(Event.current.mousePosition))
            {
                _selectedNodeIndex = index;
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawNodeInspector()
        {
            Rect inspectorArea = new Rect(position.width - 258, 20, 256, position.height - 20);
            GUILayout.BeginArea(inspectorArea);

            EditorGUILayout.LabelField("Node Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (_selectedNodeIndex >= 0 && _selectedNodeIndex < _selectedTree.Nodes.Count)
            {
                BTNodeData node = _selectedTree.Nodes[_selectedNodeIndex];

                EditorGUILayout.LabelField("Index", _selectedNodeIndex.ToString());
                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();
                node.NodeName = EditorGUILayout.TextField("Name", node.NodeName);
                node.NodeType = EditorGUILayout.TextField("Type", node.NodeType);
                node.ParentIndex = EditorGUILayout.IntField("Parent Index", node.ParentIndex);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
                if (node.Parameters != null)
                {
                    for (int i = 0; i < node.Parameters.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        node.Parameters[i] = EditorGUILayout.TextField($"[{i}]", node.Parameters[i]);
                        if (GUILayout.Button("-", GUILayout.Width(20)))
                        {
                            node.Parameters.RemoveAt(i);
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                if (GUILayout.Button("Add Parameter"))
                    node.Parameters.Add("");

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(_selectedTree);
                    _layoutDirty = true;
                }

                EditorGUILayout.Space();
                // Color legend
                Color c = GetNodeColor(node.NodeType);
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(20, 20), c);
            }
            else
            {
                EditorGUILayout.HelpBox("Select a node to inspect.", MessageType.Info);
            }

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Color Legend", EditorStyles.boldLabel);
            DrawColorLegendEntry("Selector", SelectorColor);
            DrawColorLegendEntry("Sequence", SequenceColor);
            DrawColorLegendEntry("Condition", ConditionColor);
            DrawColorLegendEntry("Action", ActionColor);

            GUILayout.EndArea();
        }

        private void DrawColorLegendEntry(string label, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            Rect colorRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
            EditorGUI.DrawRect(colorRect, color);
            EditorGUILayout.LabelField(label);
            EditorGUILayout.EndHorizontal();
        }

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (DragAndDrop.objectReferences.Length > 0 &&
                    DragAndDrop.objectReferences[0] is BehaviorTreeSO)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        _selectedTree = (BehaviorTreeSO)DragAndDrop.objectReferences[0];
                        _layoutDirty = true;
                        _selectedNodeIndex = -1;
                    }

                    evt.Use();
                }
            }
        }

        private void RebuildLayout()
        {
            _nodePositions.Clear();
            _childrenMap.Clear();

            if (_selectedTree == null || _selectedTree.Nodes == null || _selectedTree.Nodes.Count == 0)
                return;

            // Build children map
            List<int> roots = new();
            for (int i = 0; i < _selectedTree.Nodes.Count; i++)
            {
                int parentIndex = _selectedTree.Nodes[i].ParentIndex;
                if (parentIndex < 0 || parentIndex >= _selectedTree.Nodes.Count)
                {
                    roots.Add(i);
                }
                else
                {
                    if (!_childrenMap.ContainsKey(parentIndex))
                        _childrenMap[parentIndex] = new List<int>();
                    _childrenMap[parentIndex].Add(i);
                }
            }

            // Layout each root tree
            float xCursor = 40f;
            foreach (int rootIndex in roots)
            {
                float subtreeWidth = CalculateSubtreeWidth(rootIndex);
                LayoutNode(rootIndex, xCursor, 40f, subtreeWidth);
                xCursor += subtreeWidth + HorizontalSpacing;
            }
        }

        private void LayoutNode(int nodeIndex, float xStart, float y, float availableWidth)
        {
            float nodeX = xStart + (availableWidth - NodeWidth) * 0.5f;
            _nodePositions[nodeIndex] = new Rect(nodeX, y, NodeWidth, NodeHeight);

            if (!_childrenMap.TryGetValue(nodeIndex, out var children) || children.Count == 0)
                return;

            // Calculate total children width
            List<float> childWidths = new();
            float totalChildWidth = 0;
            foreach (int child in children)
            {
                float w = CalculateSubtreeWidth(child);
                childWidths.Add(w);
                totalChildWidth += w;
            }
            totalChildWidth += (children.Count - 1) * HorizontalSpacing;

            // Center children under parent
            float childX = xStart + (availableWidth - totalChildWidth) * 0.5f;
            float childY = y + NodeHeight + VerticalSpacing;

            for (int i = 0; i < children.Count; i++)
            {
                LayoutNode(children[i], childX, childY, childWidths[i]);
                childX += childWidths[i] + HorizontalSpacing;
            }
        }

        private float CalculateSubtreeWidth(int nodeIndex)
        {
            if (!_childrenMap.TryGetValue(nodeIndex, out var children) || children.Count == 0)
                return NodeWidth;

            float totalWidth = 0;
            foreach (int child in children)
                totalWidth += CalculateSubtreeWidth(child);

            totalWidth += (children.Count - 1) * HorizontalSpacing;
            return Mathf.Max(totalWidth, NodeWidth);
        }

        private static Color GetNodeColor(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType))
                return DefaultColor;

            string lower = nodeType.ToLowerInvariant();
            if (lower.Contains("selector")) return SelectorColor;
            if (lower.Contains("sequence")) return SequenceColor;
            if (lower.Contains("condition")) return ConditionColor;
            if (lower.Contains("action")) return ActionColor;
            return DefaultColor;
        }
    }
}
