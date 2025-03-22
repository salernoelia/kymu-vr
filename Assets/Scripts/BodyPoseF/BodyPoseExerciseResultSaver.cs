using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BodyPoseF
{
    /// <summary>
    /// Saves exercise results for later analysis
    /// </summary>
    public class BodyPoseExerciseResultSaver : MonoBehaviour
    {
        [Tooltip("Directory where exercise results will be saved")]
        [SerializeField] private string resultsDirectory = "ExerciseResults";

        [SerializeField] private BodyPoseExerciseTracker _exerciseTracker;

        [System.Serializable]
        private class ExerciseResult
        {
            public string timestamp;
            public float duration;
            public Dictionary<int, float> poseAccuracies = new Dictionary<int, float>();
            public float overallAccuracy;
            public int totalPoses;
            public int detectedPoses;
        }

        private void Awake()
        {
            // Find the tracker if not assigned
            if (_exerciseTracker == null)
            {
                _exerciseTracker = GetComponent<BodyPoseExerciseTracker>();
                if (_exerciseTracker == null)
                {
                    _exerciseTracker = FindObjectOfType<BodyPoseExerciseTracker>();
                }
            }

            // Create directories if needed
            if (!Directory.Exists(resultsDirectory))
            {
                Directory.CreateDirectory(resultsDirectory);
            }
        }

        private void OnEnable()
        {
            if (_exerciseTracker != null)
            {
                // Subscribe to the exercise completed event
                _exerciseTracker.GetType()
                    .GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_exerciseTracker)
                    ?.GetType()
                    .GetMethod("AddListener")
                    ?.Invoke(_exerciseTracker.GetType().GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_exerciseTracker),
                        new object[] { new Action<Dictionary<int, float>, float>(SaveExerciseResults) });
            }
        }

        private void OnDisable()
        {
            if (_exerciseTracker != null)
            {
                // Unsubscribe from the exercise completed event
                _exerciseTracker.GetType()
                    .GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_exerciseTracker)
                    ?.GetType()
                    .GetMethod("RemoveListener")
                    ?.Invoke(_exerciseTracker.GetType().GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_exerciseTracker),
                        new object[] { new Action<Dictionary<int, float>, float>(SaveExerciseResults) });
            }
        }

        /// <summary>
        /// Saves the exercise results to a JSON file
        /// </summary>
        /// <param name="poseAccuracies">Dictionary mapping pose indices to accuracy scores</param>
        /// <param name="overallAccuracy">Overall exercise accuracy score</param>
        public void SaveExerciseResults(Dictionary<int, float> poseAccuracies, float overallAccuracy)
        {
            // Create result object
            ExerciseResult result = new ExerciseResult
            {
                timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss"),
                duration = _exerciseTracker.GetType().GetProperty("RemainingTime")?.GetGetMethod()?.Invoke(_exerciseTracker, null) is float remainingTime
                    ? (float)_exerciseTracker.GetType().GetField("_exerciseDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_exerciseTracker) - remainingTime
                    : 0f,
                poseAccuracies = poseAccuracies,
                overallAccuracy = overallAccuracy,
                totalPoses = poseAccuracies.Count,
                detectedPoses = 0
            };

            // Count detected poses
            foreach (var pair in poseAccuracies)
            {
                if (pair.Value > 0)
                {
                    result.detectedPoses++;
                }
            }

            // Convert to JSON
            string json = JsonUtility.ToJson(result, true);

            // Save to file
            string filename = $"{resultsDirectory}/Exercise-{result.timestamp}.json";
            File.WriteAllText(filename, json);

            Debug.Log($"Exercise results saved to {filename}");
        }
    }
}
