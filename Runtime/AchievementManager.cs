using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AchievementManager.Runtime
{
    // -------------------------------------------------------------------------
    // AchievementType
    // -------------------------------------------------------------------------

    /// <summary>How an achievement is unlocked.</summary>
    public enum AchievementType
    {
        /// <summary>Unlocked in a single call to <see cref="AchievementManager.Unlock"/>.</summary>
        Trigger,

        /// <summary>Unlocked automatically when accumulated progress reaches <see cref="AchievementDefinition.progressTarget"/>.</summary>
        Progress
    }

    // -------------------------------------------------------------------------
    // AchievementDefinition
    // -------------------------------------------------------------------------

    /// <summary>
    /// Defines a single achievement.
    /// Serializable so it can be defined in the Inspector and loaded from JSON.
    /// </summary>
    [Serializable]
    public class AchievementDefinition
    {
        [Tooltip("Unique identifier (e.g. 'first_chapter_complete').")]
        public string id;

        [Tooltip("Short achievement title shown in the UI.")]
        public string title;

        [Tooltip("Longer description shown in the achievement panel.")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Icon displayed in the achievement list and unlock notification.")]
        public Sprite icon;

        [Tooltip("Points awarded when this achievement is unlocked.")]
        public int points;

        public AchievementType type;

        [Tooltip("Target accumulation count for Progress-type achievements.")]
        public int progressTarget = 1;

        [Tooltip("If true, this achievement is not shown in the UI until it is unlocked.")]
        public bool hidden;
    }

    // -------------------------------------------------------------------------
    // JSON wrapper
    // -------------------------------------------------------------------------

    [Serializable]
    internal class AchievementsJson
    {
        public AchievementDefinition[] achievements;
    }

    // -------------------------------------------------------------------------
    // AchievementManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// <b>AchievementManager</b> tracks achievement unlock state and progress.
    ///
    /// <para><b>Responsibilities:</b>
    /// <list type="number">
    ///   <item>Store achievement definitions (id, title, description, icon, points, type).</item>
    ///   <item>Persist unlock state and progress via <c>PlayerPrefs</c>.</item>
    ///   <item>Automatically unlock <see cref="AchievementType.Progress"/> achievements when the target is reached.</item>
    ///   <item>Optionally merge definitions from a JSON file for modding.</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Modding / JSON:</b> Enable <c>loadFromJson</c> and place an
    /// <c>achievements.json</c> in <c>StreamingAssets/</c>.
    /// JSON entries are <b>merged by id</b>: JSON overrides Inspector entries with the same id and can add new ones.</para>
    ///
    /// <para><b>Optional integration defines:</b>
    /// <list type="bullet">
    ///   <item><c>ACHIEVEMENTMANAGER_SM</c> — SaveManager: also sets a save flag <c>ach_&lt;id&gt;</c> when an achievement is unlocked.</item>
    ///   <item><c>ACHIEVEMENTMANAGER_GM</c> — GalleryManager: calls <c>GalleryManager.UnlockStatic(id)</c> on achievement unlock.</item>
    ///   <item><c>ACHIEVEMENTMANAGER_EM</c> — EventManager: fires <c>AchievementUnlocked</c> as a named GameEvent.</item>
    /// </list>
    /// </para>
    /// </summary>
    [AddComponentMenu("AchievementManager/Achievement Manager")]
    [DisallowMultipleComponent]
    public class AchievementManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("Achievements")]
        [Tooltip("All achievement definitions for this game.")]
        [SerializeField] private AchievementDefinition[] achievements = Array.Empty<AchievementDefinition>();

        [Header("Modding / JSON")]
        [Tooltip("When enabled, merge achievement definitions from a JSON file in StreamingAssets/ at startup.")]
        [SerializeField] private bool loadFromJson = false;

        [Tooltip("Path relative to StreamingAssets/ (e.g. 'achievements.json').")]
        [SerializeField] private string jsonPath = "achievements.json";

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fired when an achievement is unlocked. Parameter: achievement id.</summary>
        public event Action<string> OnAchievementUnlocked;

        /// <summary>
        /// Fired when progress is updated on a Progress-type achievement.
        /// Parameters: (id, currentProgress, progressTarget).
        /// </summary>
        public event Action<string, int, int> OnProgressUpdated;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------

        private const string UnlockPrefix   = "ach_unlock_";
        private const string ProgressPrefix = "ach_progress_";

        private readonly List<AchievementDefinition> _achievements = new();
        private readonly Dictionary<string, AchievementDefinition> _index = new();

        /// <summary>Read-only achievement list (merged Inspector + JSON).</summary>
        public IReadOnlyList<AchievementDefinition> Achievements => _achievements;

        /// <summary>Total points from all currently unlocked achievements.</summary>
        public int TotalPoints
        {
            get
            {
                int total = 0;
                foreach (var a in _achievements)
                    if (a != null && IsUnlocked(a.id))
                        total += a.points;
                return total;
            }
        }

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            BuildIndex();
            if (loadFromJson) LoadJson();
        }

        private void BuildIndex()
        {
            _achievements.Clear();
            _index.Clear();
            foreach (var a in achievements)
            {
                if (a == null || string.IsNullOrEmpty(a.id)) continue;
                _achievements.Add(a);
                _index[a.id] = a;
            }
        }

        private void LoadJson()
        {
            string path = Path.Combine(Application.streamingAssetsPath, jsonPath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[AchievementManager] JSON not found: {path}");
                return;
            }
            try
            {
                var wrapper = JsonUtility.FromJson<AchievementsJson>(File.ReadAllText(path));
                if (wrapper?.achievements == null) return;
                foreach (var a in wrapper.achievements)
                {
                    if (a == null || string.IsNullOrEmpty(a.id)) continue;
                    if (_index.ContainsKey(a.id))
                    {
                        int i = _achievements.FindIndex(x => x.id == a.id);
                        if (i >= 0) _achievements[i] = a;
                        _index[a.id] = a;
                    }
                    else
                    {
                        _achievements.Add(a);
                        _index[a.id] = a;
                    }
                }
                Debug.Log($"[AchievementManager] Achievements merged from {path}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AchievementManager] Failed to load JSON: {ex.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // Unlock
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns true if the achievement identified by <paramref name="id"/> is unlocked.
        /// Check order: PlayerPrefs → SaveManager flag (if <c>ACHIEVEMENTMANAGER_SM</c>).
        /// </summary>
        public bool IsUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (PlayerPrefs.GetInt(UnlockPrefix + id, 0) == 1) return true;
#if ACHIEVEMENTMANAGER_SM
            var sm = FindFirstObjectByType<SaveManager.Runtime.SaveManager>();
            if (sm != null && sm.IsSet("ach_" + id)) return true;
#endif
            return false;
        }

        /// <summary>
        /// Unlock the achievement with <paramref name="id"/>.
        /// Persists to PlayerPrefs, fires <see cref="OnAchievementUnlocked"/>, and triggers cross-manager bridges.
        /// Idempotent — safe to call multiple times.
        /// </summary>
        public void Unlock(string id)
        {
            if (string.IsNullOrEmpty(id) || IsUnlocked(id)) return;

            PlayerPrefs.SetInt(UnlockPrefix + id, 1);
            PlayerPrefs.Save();

#if ACHIEVEMENTMANAGER_SM
            var sm = FindFirstObjectByType<SaveManager.Runtime.SaveManager>();
            sm?.SetFlag("ach_" + id);
#endif

            OnAchievementUnlocked?.Invoke(id);

#if ACHIEVEMENTMANAGER_EM
            FindFirstObjectByType<EventManager.Runtime.EventManager>()?.Fire("AchievementUnlocked", id);
#endif
#if ACHIEVEMENTMANAGER_GM
            GalleryManager.Runtime.GalleryManager.UnlockStatic(id);
#endif

            if (_index.TryGetValue(id, out var def))
                Debug.Log($"[AchievementManager] Unlocked: '{def.title}' (id '{id}', {def.points} pts)");
            else
                Debug.Log($"[AchievementManager] Unlocked: {id}");
        }

        // -------------------------------------------------------------------------
        // Progress
        // -------------------------------------------------------------------------

        /// <summary>
        /// Get the current accumulated progress for a <see cref="AchievementType.Progress"/> achievement.
        /// Returns 0 for Trigger-type or unknown ids.
        /// </summary>
        public int GetProgress(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            return PlayerPrefs.GetInt(ProgressPrefix + id, 0);
        }

        /// <summary>
        /// Set the progress for a <see cref="AchievementType.Progress"/> achievement to an absolute value.
        /// Fires <see cref="OnProgressUpdated"/>. Automatically unlocks when progress reaches the target.
        /// </summary>
        public void SetProgress(string id, int value)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!_index.TryGetValue(id, out var def) || def.type != AchievementType.Progress)
            {
                Debug.LogWarning($"[AchievementManager] SetProgress called on non-Progress achievement '{id}'.");
                return;
            }
            int clamped = Mathf.Max(0, value);
            PlayerPrefs.SetInt(ProgressPrefix + id, clamped);
            PlayerPrefs.Save();
            OnProgressUpdated?.Invoke(id, clamped, def.progressTarget);
            if (clamped >= def.progressTarget)
                Unlock(id);
        }

        /// <summary>Add <paramref name="amount"/> to the progress of achievement <paramref name="id"/>.</summary>
        public void AddProgress(string id, int amount = 1)
        {
            SetProgress(id, GetProgress(id) + amount);
        }

        // -------------------------------------------------------------------------
        // Reset
        // -------------------------------------------------------------------------

        /// <summary>Reset unlock state and progress for a single achievement.</summary>
        public void ResetProgress(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            PlayerPrefs.DeleteKey(UnlockPrefix + id);
            PlayerPrefs.DeleteKey(ProgressPrefix + id);
            PlayerPrefs.Save();
        }

        /// <summary>Reset all achievements and progress. Useful for testing.</summary>
        public void ResetAll()
        {
            foreach (var a in _achievements)
            {
                if (a == null || string.IsNullOrEmpty(a.id)) continue;
                PlayerPrefs.DeleteKey(UnlockPrefix + a.id);
                PlayerPrefs.DeleteKey(ProgressPrefix + a.id);
            }
            PlayerPrefs.Save();
            Debug.Log("[AchievementManager] All achievements reset.");
        }

        // -------------------------------------------------------------------------
        // Queries
        // -------------------------------------------------------------------------

        /// <summary>Returns the <see cref="AchievementDefinition"/> for <paramref name="id"/>, or null.</summary>
        public AchievementDefinition GetDefinition(string id) =>
            _index.TryGetValue(id, out var a) ? a : null;

        /// <summary>Returns all achievements that are currently unlocked.</summary>
        public List<AchievementDefinition> GetUnlocked()
        {
            var result = new List<AchievementDefinition>();
            foreach (var a in _achievements)
                if (a != null && IsUnlocked(a.id))
                    result.Add(a);
            return result;
        }

        // -------------------------------------------------------------------------
        // Static helper
        // -------------------------------------------------------------------------

        /// <summary>
        /// Unlock an achievement via PlayerPrefs without needing a scene reference.
        /// Use from gameplay scripts when AchievementManager may not be in the same scene.
        /// Does not fire events or trigger cross-manager bridges.
        /// </summary>
        public static void UnlockStatic(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            PlayerPrefs.SetInt(UnlockPrefix + id, 1);
            PlayerPrefs.Save();
        }
    }
}
