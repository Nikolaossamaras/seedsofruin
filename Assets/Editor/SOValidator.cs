using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

namespace SoR.Editor
{
    public class SOValidator : EditorWindow
    {
        private Vector2 _scrollPosition;
        private List<ValidationResult> _results = new();
        private bool _hasRun;
        private int _passCount;
        private int _failCount;
        private bool _showPassingOnly;
        private bool _showFailingOnly = true;
        private string _searchFilter = "";

        private struct ValidationResult
        {
            public ScriptableObject Asset;
            public string AssetPath;
            public string AssetName;
            public bool Passed;
            public List<string> Errors;
            public List<string> Warnings;
        }

        [MenuItem("Window/Seeds of Ruin/SO Validator")]
        public static void ShowWindow()
        {
            GetWindow<SOValidator>("SO Validator");
        }

        private void OnGUI()
        {
            GUILayout.Label("ScriptableObject Validator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate All", GUILayout.Height(30)))
                RunValidation();

            if (_hasRun)
            {
                GUILayout.Label($"  Pass: {_passCount}  |  Fail: {_failCount}  |  Total: {_results.Count}",
                    EditorStyles.boldLabel, GUILayout.Height(30));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Filters
            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);
            _showPassingOnly = GUILayout.Toggle(_showPassingOnly, "Show Passing", EditorStyles.toolbarButton, GUILayout.Width(100));
            _showFailingOnly = GUILayout.Toggle(_showFailingOnly, "Show Failing", EditorStyles.toolbarButton, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            if (!_showPassingOnly && !_showFailingOnly)
            {
                _showPassingOnly = true;
                _showFailingOnly = true;
            }

            EditorGUILayout.Space();

            if (!_hasRun)
            {
                EditorGUILayout.HelpBox(
                    "Click 'Validate All' to scan all ScriptableObjects in the project.\n\n" +
                    "Checks performed:\n" +
                    "  - Null references on serialized fields\n" +
                    "  - Empty strings on required fields (Name, Id, Description)\n" +
                    "  - Rate sums for gacha/probability fields\n" +
                    "  - Empty lists that should have entries",
                    MessageType.Info);
                return;
            }

            // Results list
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var result in _results)
            {
                if (!_showPassingOnly && result.Passed) continue;
                if (!_showFailingOnly && !result.Passed) continue;
                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !result.AssetName.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()) &&
                    !result.AssetPath.ToLowerInvariant().Contains(_searchFilter.ToLowerInvariant()))
                    continue;

                DrawResult(result);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawResult(ValidationResult result)
        {
            Color bgColor = result.Passed
                ? new Color(0.2f, 0.5f, 0.2f, 0.15f)
                : new Color(0.6f, 0.2f, 0.2f, 0.15f);

            Rect boxRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.DrawRect(boxRect, bgColor);

            EditorGUILayout.BeginHorizontal();

            string statusIcon = result.Passed ? "[PASS]" : "[FAIL]";
            GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = result.Passed ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.3f) }
            };
            GUILayout.Label(statusIcon, statusStyle, GUILayout.Width(50));
            GUILayout.Label(result.AssetName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Select", GUILayout.Width(50)))
            {
                Selection.activeObject = result.Asset;
                EditorGUIUtility.PingObject(result.Asset);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Path", result.AssetPath, EditorStyles.miniLabel);

            if (result.Errors != null && result.Errors.Count > 0)
            {
                foreach (string error in result.Errors)
                {
                    GUIStyle errorStyle = new GUIStyle(EditorStyles.label)
                    {
                        normal = { textColor = new Color(1f, 0.4f, 0.4f) },
                        wordWrap = true
                    };
                    EditorGUILayout.LabelField("  ERROR: " + error, errorStyle);
                }
            }

            if (result.Warnings != null && result.Warnings.Count > 0)
            {
                foreach (string warning in result.Warnings)
                {
                    GUIStyle warnStyle = new GUIStyle(EditorStyles.label)
                    {
                        normal = { textColor = new Color(1f, 0.8f, 0.3f) },
                        wordWrap = true
                    };
                    EditorGUILayout.LabelField("  WARN: " + warning, warnStyle);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void RunValidation()
        {
            _results.Clear();
            _passCount = 0;
            _failCount = 0;

            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/ScriptableObjects" });

            // If no SOs found in the dedicated folder, search the whole project
            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (so == null)
                    continue;

                var result = ValidateSO(so, path);
                _results.Add(result);

                if (result.Passed) _passCount++;
                else _failCount++;
            }

            // Sort: failures first
            _results.Sort((a, b) =>
            {
                if (a.Passed != b.Passed) return a.Passed ? 1 : -1;
                return string.Compare(a.AssetName, b.AssetName, System.StringComparison.Ordinal);
            });

            _hasRun = true;
            Debug.Log($"[SO Validator] Validation complete: {_passCount} passed, {_failCount} failed out of {_results.Count} total.");
        }

        private ValidationResult ValidateSO(ScriptableObject so, string path)
        {
            var result = new ValidationResult
            {
                Asset = so,
                AssetPath = path,
                AssetName = so.name,
                Passed = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            var soType = so.GetType();
            var fields = soType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                // Check SerializeField or public fields
                bool isSerialized = field.IsPublic ||
                                    field.GetCustomAttribute<SerializeField>() != null;
                if (!isSerialized)
                    continue;

                object value = field.GetValue(so);
                string fieldName = field.Name;

                // Check null references for UnityEngine.Object types
                if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                {
                    UnityEngine.Object objValue = value as UnityEngine.Object;
                    if (objValue == null)
                    {
                        result.Errors.Add($"Null reference: '{fieldName}' ({field.FieldType.Name})");
                        result.Passed = false;
                    }
                }

                // Check empty strings on fields that look required
                if (field.FieldType == typeof(string))
                {
                    string strValue = value as string;
                    string lowerName = fieldName.ToLowerInvariant();

                    bool isRequiredField = lowerName.Contains("name") ||
                                           lowerName.Contains("id") ||
                                           lowerName.Contains("description") ||
                                           lowerName.Contains("title");

                    if (isRequiredField && string.IsNullOrWhiteSpace(strValue))
                    {
                        result.Errors.Add($"Empty required string: '{fieldName}'");
                        result.Passed = false;
                    }
                }

                // Check for empty lists/arrays that should have entries
                if (field.FieldType.IsArray)
                {
                    var array = value as System.Array;
                    if (array != null && array.Length == 0)
                    {
                        result.Warnings.Add($"Empty array: '{fieldName}'");
                    }
                }
                else if (typeof(System.Collections.IList).IsAssignableFrom(field.FieldType))
                {
                    var list = value as System.Collections.IList;
                    if (list != null && list.Count == 0)
                    {
                        result.Warnings.Add($"Empty list: '{fieldName}'");
                    }
                }

                // Check rate fields that should sum to 1.0
                CheckRateFields(so, fields, ref result);
            }

            return result;
        }

        private void CheckRateFields(ScriptableObject so, FieldInfo[] fields, ref ValidationResult result)
        {
            // Look for groups of float fields containing "rate" or "chance" or "probability"
            List<FieldInfo> rateFields = new();
            foreach (var field in fields)
            {
                string lowerName = field.Name.ToLowerInvariant();
                if (field.FieldType == typeof(float) &&
                    (lowerName.Contains("rate") || lowerName.Contains("chance") || lowerName.Contains("probability")))
                {
                    rateFields.Add(field);
                }
            }

            if (rateFields.Count >= 2)
            {
                float sum = 0;
                foreach (var rateField in rateFields)
                    sum += (float)rateField.GetValue(so);

                if (sum > 0.01f && Mathf.Abs(sum - 1f) > 0.01f)
                {
                    result.Errors.Add($"Rate fields sum to {sum:F3} (expected 1.0): " +
                                      string.Join(", ", rateFields.ConvertAll(f => f.Name)));
                    result.Passed = false;
                }
            }
        }
    }
}
