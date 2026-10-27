#if UNITY_EDITOR && ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;
using UnityEngine.UIElements;

namespace ThanhDV.BugCatcher.Test
{
    /// <summary>
    /// Editor-only test helper for this project (not part of the package): in Play mode,
    /// 1 / 2 / 3 write a test Log / Warning / Error. Ignored while a text field has focus.
    /// </summary>
    internal class TestLogHotkeys : MonoBehaviour
    {
        private int count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("[BugCatcher Test Hotkeys]");
            DontDestroyOnLoad(go);
            go.AddComponent<TestLogHotkeys>();
        }

        private void Update()
        {
            if (Pressed(KeyCode.Alpha1, KeyCode.Keypad1) && !IsTyping()) Debug.Log($"Test log #{++count}");
            if (Pressed(KeyCode.Alpha2, KeyCode.Keypad2) && !IsTyping()) Debug.LogWarning($"Test warning #{++count}");
            if (Pressed(KeyCode.Alpha3, KeyCode.Keypad3) && !IsTyping()) Debug.LogError($"Test error #{++count}");
        }

        private static bool Pressed(KeyCode key, KeyCode keypad) => Input.GetKeyDown(key) || Input.GetKeyDown(keypad);

        // Typing "1" into the Bug Catcher command or search field must not create logs.
        private static bool IsTyping()
        {
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (document.rootVisualElement?.panel?.focusController?.focusedElement is VisualElement focused &&
                    focused.GetFirstOfType<TextField>() != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
#endif
