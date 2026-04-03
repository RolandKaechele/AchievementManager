#if UNITY_EDITOR
using AchievementManager.Runtime;
using UnityEditor;
using UnityEngine;

namespace AchievementManager.Editor
{
    /// <summary>
    /// Custom Inspector for <see cref="AchievementManager.Runtime.AchievementManager"/>.
    /// Shows live unlock state, progress, and provides per-achievement controls at runtime.
    /// </summary>
    [CustomEditor(typeof(AchievementManager.Runtime.AchievementManager))]
    public class AchievementManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(6);

            // ── Validation ──────────────────────────────────────────────────────

            var achievementsProp = serializedObject.FindProperty("achievements");
            if (achievementsProp != null && achievementsProp.arraySize == 0)
                EditorGUILayout.HelpBox(
                    "No achievements defined. Add achievements in the Inspector or enable JSON loading.",
                    MessageType.Info);

            // ── Runtime controls (Play Mode only) ───────────────────────────────

            if (!Application.isPlaying) return;

            var mgr = (AchievementManager.Runtime.AchievementManager)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Achievements  —  {mgr.TotalPoints} pts unlocked", EditorStyles.boldLabel);

            var achievements = mgr.Achievements;
            if (achievements.Count == 0)
            {
                EditorGUILayout.LabelField("  (none defined)");
            }
            else
            {
                foreach (var a in achievements)
                {
                    if (a == null) continue;
                    bool unlocked = mgr.IsUnlocked(a.id);
                    int  current  = (a.type == AchievementType.Progress) ? mgr.GetProgress(a.id) : 0;

                    EditorGUILayout.BeginHorizontal();

                    string progressLabel = a.type == AchievementType.Progress
                        ? $"  {current}/{a.progressTarget}"
                        : "";
                    EditorGUILayout.LabelField($"  {a.title ?? a.id}  [{a.points} pts]{progressLabel}");
                    EditorGUILayout.LabelField(unlocked ? "✓" : "—", GUILayout.Width(20));
                    GUI.enabled = !unlocked;
                    if (GUILayout.Button("Unlock", GUILayout.Width(60))) mgr.Unlock(a.id);
                    GUI.enabled = true;
                    if (GUILayout.Button("Reset",  GUILayout.Width(55))) mgr.ResetProgress(a.id);

                    EditorGUILayout.EndHorizontal();

                    // Progress bar for Progress-type achievements
                    if (a.type == AchievementType.Progress && a.progressTarget > 0)
                    {
                        float normalised = Mathf.Clamp01((float)current / a.progressTarget);
                        Rect barRect = EditorGUILayout.GetControlRect(false, 4f);
                        barRect.x += 16f;
                        barRect.width -= 16f;
                        EditorGUI.ProgressBar(barRect, normalised, "");
                        EditorGUILayout.Space(2);
                    }
                }
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Reset All Achievements (Testing)")) mgr.ResetAll();
        }
    }
}
#endif
