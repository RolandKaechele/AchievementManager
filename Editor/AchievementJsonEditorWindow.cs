#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AchievementManager.Runtime;
using UnityEditor;
using UnityEngine;

namespace AchievementManager.Editor
{
    // ────────────────────────────────────────────────────────────────────────────
    // Achievement JSON Editor Window
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Editor window for creating and editing <c>achievements.json</c> in StreamingAssets.
    /// Open via <b>JSON Editors → Achievement Manager</b> or via the Manager Inspector button.
    /// </summary>
    public class AchievementJsonEditorWindow : EditorWindow
    {
        private const string JsonFolderName   = "achievements";
        private const string JsonSaveFileName = "achievements.json";

        private AchievementEditorBridge  _bridge;
        private UnityEditor.Editor       _bridgeEditor;
        private Vector2                  _scroll;
        private string                   _status;
        private bool                     _statusError;

        [MenuItem("JSON Editors/Achievement Manager")]
        public static void ShowWindow() =>
            GetWindow<AchievementJsonEditorWindow>("Achievement JSON");

        private void OnEnable()
        {
            _bridge = CreateInstance<AchievementEditorBridge>();
            Load();
        }

        private void OnDisable()
        {
            if (_bridgeEditor != null) DestroyImmediate(_bridgeEditor);
            if (_bridge      != null) DestroyImmediate(_bridge);
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.HelpBox(
                "Note: Sprite/AudioClip (UnityEngine.Object) fields like 'icon' cannot be stored in JSON " +
                "and will be null after a Load or Save round-trip.",
                MessageType.Warning);

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _statusError ? MessageType.Error : MessageType.Info);

            if (_bridge == null) return;
            if (_bridgeEditor == null)
                _bridgeEditor = UnityEditor.Editor.CreateEditor(_bridge);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _bridgeEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(
                $"StreamingAssets/{JsonFolderName}/",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50))) Load();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50))) Save();
            EditorGUILayout.EndHorizontal();
        }

        private void Load()
        {
            string folderPath = Path.Combine(Application.streamingAssetsPath, JsonFolderName);
            try
            {
                var list = new List<AchievementDefinition>();
                if (Directory.Exists(folderPath))
                {
                    foreach (var file in Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        var w = JsonUtility.FromJson<AchievementsEditorWrapper>(File.ReadAllText(file));
                        if (w?.achievements != null) list.AddRange(w.achievements);
                    }
                }
                else
                {
                    Directory.CreateDirectory(folderPath);
                    File.WriteAllText(Path.Combine(folderPath, JsonSaveFileName), JsonUtility.ToJson(new AchievementsEditorWrapper(), true));
                    AssetDatabase.Refresh();
                }
                _bridge.achievements = list;
                if (_bridgeEditor != null) { DestroyImmediate(_bridgeEditor); _bridgeEditor = null; }
                _status = $"Loaded {list.Count} achievements from {JsonFolderName}/.";
                _statusError = false;
            }
            catch (Exception e) { _status = $"Load error: {e.Message}"; _statusError = true; }
        }

        private void Save()
        {
            try
            {
                string folderPath = Path.Combine(Application.streamingAssetsPath, JsonFolderName);
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                var w = new AchievementsEditorWrapper { achievements = _bridge.achievements.ToArray() };
                var path = Path.Combine(folderPath, JsonSaveFileName);
                File.WriteAllText(path, JsonUtility.ToJson(w, true));
                AssetDatabase.Refresh();
                _status = $"Saved {_bridge.achievements.Count} achievements to {JsonFolderName}/{JsonSaveFileName}.";
                _statusError = false;
            }
            catch (Exception e) { _status = $"Save error: {e.Message}"; _statusError = true; }
        }
    }

    // ── ScriptableObject bridge: lets Unity's Inspector (and Odin) render the list ──
    internal class AchievementEditorBridge : ScriptableObject
    {
        public List<AchievementDefinition> achievements = new List<AchievementDefinition>();
    }

    // ── Local wrapper mirrors the internal AchievementsJson ──────────────────
    [Serializable]
    internal class AchievementsEditorWrapper
    {
        public AchievementDefinition[] achievements = Array.Empty<AchievementDefinition>();
    }
}
#endif
