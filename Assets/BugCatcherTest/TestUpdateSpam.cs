using IngameDebugConsole;
using UnityEngine;

namespace ThanhDV.BugCatcher.Test
{
    public sealed class TestUpdateSpam : MonoBehaviour
    {
        private static TestUpdateSpam active;
        private float stopAt;
        private int count;

        [ConsoleMethod("test.updateSpam", "Logs every frame from Update for up to 120 seconds", "seconds")]
        public static void StartSpam(int seconds)
        {
            if (active == null)
            {
                var host = new GameObject("[BugCatcher Update Spam Test]");
                DontDestroyOnLoad(host);
                active = host.AddComponent<TestUpdateSpam>();
            }

            active.count = 0;
            active.stopAt = Time.unscaledTime + Mathf.Clamp(seconds, 1, 120);
            active.enabled = true;
        }

        [ConsoleMethod("test.stopUpdateSpam", "Stops the Update spam test")]
        public static void StopSpam()
        {
            if (active != null) active.enabled = false;
        }

        private void Update()
        {
            if (Time.unscaledTime >= stopAt)
            {
                enabled = false;
                Debug.Log($"Update spam finished at #{count}");
                return;
            }

            Debug.Log($"Update spam #{++count}");
        }
    }
}
