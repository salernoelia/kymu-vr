using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.IO;
using System;

namespace BodyPoseF
{
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
            if (_exerciseTracker == null)
            {
                _exerciseTracker = GetComponent<BodyPoseExerciseTracker>();
                if (_exerciseTracker == null)
                {
                    _exerciseTracker = FindObjectOfType<BodyPoseExerciseTracker>();
                }
            }

            if (!Directory.Exists(resultsDirectory))
            {
                Directory.CreateDirectory(resultsDirectory);
            }
        }

        private void OnEnable()
        {
            if (_exerciseTracker != null)
            {
                UnityAction<Dictionary<int, float>, float> listener = SaveExerciseResults;
                _exerciseTracker.GetType()
                    .GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_exerciseTracker)
                    ?.GetType()
                    .GetMethod("AddListener")
                    ?.Invoke(_exerciseTracker.GetType().GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_exerciseTracker),
                        new object[] { listener });
            }
        }

        private void OnDisable()
        {
            if (_exerciseTracker != null)
            {
                UnityAction<Dictionary<int, float>, float> listener = SaveExerciseResults;
                _exerciseTracker.GetType()
                    .GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_exerciseTracker)
                    ?.GetType()
                    .GetMethod("RemoveListener")
                    ?.Invoke(_exerciseTracker.GetType().GetField("_onExerciseCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_exerciseTracker),
                        new object[] { listener });
            }
        }

        public void SaveExerciseResults(Dictionary<int, float> poseAccuracies, float overallAccuracy)
        {
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

            foreach (var pair in poseAccuracies)
            {
                if (pair.Value > 0)
                {
                    result.detectedPoses++;
                }
            }

            string json = JsonUtility.ToJson(result, true);
            string filename = $"{resultsDirectory}/Exercise-{result.timestamp}.json";
            File.WriteAllText(filename, json);

            Debug.Log($"Exercise results saved to {filename}");
        }
    }
}
