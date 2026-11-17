#if BUGCATCHER_ENABLED
using UnityEngine;

namespace ThanhDV.BugCatcher.Test
{
    /// <summary>
    /// Test commands for this project (not part of the package): with and without parameters,
    /// plus one overload, to exercise the command bar's suggestions and quick commands.
    /// </summary>
    public static class TestCommands
    {
        private static int spawned;

        // --- No parameters (show up as quick commands) ---------------------------------

        [ConsoleMethod("test.hello", "Logs a greeting")]
        public static void Hello() => Debug.Log("Hello from BugCatcher!");

        [ConsoleMethod("test.warn", "Logs a test warning")]
        public static void Warn() => Debug.LogWarning("Test warning from a command");

        [ConsoleMethod("test.error", "Logs a test error")]
        public static void Error() => Debug.LogError("Test error from a command");

        [ConsoleMethod("test.exception", "Throws an exception")]
        public static void Exception() => throw new System.InvalidOperationException("Test exception from a command");

        [ConsoleMethod("test.clear", "Destroys every cube made by test.spawn")]
        public static void Clear()
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name.StartsWith("TestCube")) Object.Destroy(go);
            }
            Debug.Log($"Cleared {spawned} test cube(s)");
            spawned = 0;
        }

        // --- With parameters ---------------------------------------------------------------

        [ConsoleMethod("test.echo", "Logs the given text", "message")]
        public static void Echo(string message) => Debug.Log(message);

        [ConsoleMethod("test.spam", "Logs the given number of lines", "count")]
        public static void Spam(int count)
        {
            count = Mathf.Clamp(count, 1, 1000);
            for (int i = 1; i <= count; i++) Debug.Log($"Spam line {i}/{count}");
        }

        [ConsoleMethod("test.add", "Adds two numbers", "a", "b")]
        public static void Add(float a, float b) => Debug.Log($"{a} + {b} = {a + b}");

        [ConsoleMethod("test.log", "Logs a message with the given type (Log, Warning, Error)", "type", "message")]
        public static void Log(LogType type, string message) => Debug.LogFormat(type, LogOption.None, null, "{0}", message);

        [ConsoleMethod("test.spawn", "Creates a cube at the given position", "position")]
        public static void Spawn(Vector3 position)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "TestCube " + ++spawned;
            cube.transform.position = position;
            Debug.Log($"Spawned {cube.name} at {position}");
        }

        // Temporary commands for scrolling and input tests.

        [ConsoleMethod("test.quick01", "Logs two lines in separate frames")]
        public static async void Quick01()
        {
            Debug.Log("Alpha 1/2");
            await System.Threading.Tasks.Task.Delay(150);
            Debug.Log("Alpha 2/2");
        }

        [ConsoleMethod("test.quick02", "Logs Bravo")]
        public static void Quick02() => Debug.Log("Bravo");

        [ConsoleMethod("test.quick03", "Logs Charlie")]
        public static void Quick03() => Debug.Log("Charlie");

        [ConsoleMethod("test.quick04", "Logs Delta")]
        public static void Quick04() => Debug.Log("Delta");

        [ConsoleMethod("test.quick05", "Logs Echo")]
        public static void Quick05() => Debug.Log("Echo");

        [ConsoleMethod("test.quick06", "Logs Foxtrot")]
        public static void Quick06() => Debug.Log("Foxtrot");

        [ConsoleMethod("test.quick07", "Logs Golf")]
        public static void Quick07() => Debug.Log("Golf");

        [ConsoleMethod("test.quick08", "Logs Hotel")]
        public static void Quick08() => Debug.Log("Hotel");

        [ConsoleMethod("test.quick09", "Logs India")]
        public static void Quick09() => Debug.Log("India");

        [ConsoleMethod("test.quick10", "Logs Juliet")]
        public static void Quick10() => Debug.Log("Juliet");

        [ConsoleMethod("test.quick11", "Logs Kilo")]
        public static void Quick11() => Debug.Log("Kilo");

        [ConsoleMethod("test.quick12", "Logs Lima")]
        public static void Quick12() => Debug.Log("Lima");

        [ConsoleMethod("test.quick13", "Logs Mike")]
        public static void Quick13() => Debug.Log("Mike");

        [ConsoleMethod("test.quick14", "Logs November")]
        public static void Quick14() => Debug.Log("November");

        [ConsoleMethod("test.quick15", "Logs Oscar")]
        public static void Quick15() => Debug.Log("Oscar");

        [ConsoleMethod("test.quick16", "Logs Papa")]
        public static void Quick16() => Debug.Log("Papa");

        [ConsoleMethod("test.quick17", "Logs Quebec")]
        public static void Quick17() => Debug.Log("Quebec");

        [ConsoleMethod("test.quick18", "Logs Romeo")]
        public static void Quick18() => Debug.Log("Romeo");

        [ConsoleMethod("test.quick19", "Logs Sierra")]
        public static void Quick19() => Debug.Log("Sierra");

        [ConsoleMethod("test.quick20", "Logs Tango")]
        public static void Quick20() => Debug.Log("Tango");

        [ConsoleMethod("test.arg01", "Echoes text")]
        public static void Arg01(string text) => Debug.Log(text);

        [ConsoleMethod("test.arg02", "Logs an integer")]
        public static void Arg02(int count) => Debug.Log(count);

        [ConsoleMethod("test.arg03", "Logs a decimal")]
        public static void Arg03(float value) => Debug.Log(value);

        [ConsoleMethod("test.arg04", "Logs a boolean")]
        public static void Arg04(bool enabled) => Debug.Log(enabled);

        [ConsoleMethod("test.arg05", "Logs a point")]
        public static void Arg05(Vector2 point) => Debug.Log(point);

        [ConsoleMethod("test.arg06", "Logs a position")]
        public static void Arg06(Vector3 position) => Debug.Log(position);

        [ConsoleMethod("test.arg07", "Adds two integers")]
        public static void Arg07(int left, int right) => Debug.Log(left + right);

        [ConsoleMethod("test.arg08", "Subtracts two integers")]
        public static void Arg08(int left, int right) => Debug.Log(left - right);

        [ConsoleMethod("test.arg09", "Multiplies two decimals")]
        public static void Arg09(float left, float right) => Debug.Log(left * right);

        [ConsoleMethod("test.arg10", "Finds the smaller value")]
        public static void Arg10(float left, float right) => Debug.Log(Mathf.Min(left, right));

        [ConsoleMethod("test.arg11", "Finds the larger value")]
        public static void Arg11(float left, float right) => Debug.Log(Mathf.Max(left, right));

        [ConsoleMethod("test.arg12", "Clamps a value")]
        public static void Arg12(float value, float min, float max) => Debug.Log(Mathf.Clamp(value, min, max));

        [ConsoleMethod("test.arg13", "Rounds a value")]
        public static void Arg13(float value) => Debug.Log(Mathf.RoundToInt(value));

        [ConsoleMethod("test.arg14", "Finds the absolute value")]
        public static void Arg14(float value) => Debug.Log(Mathf.Abs(value));

        [ConsoleMethod("test.arg15", "Measures a vector")]
        public static void Arg15(Vector3 vector) => Debug.Log(vector.magnitude);

        [ConsoleMethod("test.arg16", "Measures distance between points")]
        public static void Arg16(Vector3 from, Vector3 to) => Debug.Log(Vector3.Distance(from, to));

        [ConsoleMethod("test.arg17", "Combines two strings")]
        public static void Arg17(string first, string second) => Debug.Log(first + " " + second);

        [ConsoleMethod("test.arg18", "Logs text length")]
        public static void Arg18(string text) => Debug.Log(text.Length);

        [ConsoleMethod("test.arg19", "Logs a named value")]
        public static void Arg19(string name, float value) => Debug.Log($"{name} = {value}");

        [ConsoleMethod("test.arg20", "Logs a message at a given level")]
        public static void Arg20(LogType level, string message) => Debug.LogFormat(level, LogOption.None, null, "{0}", message);

        // --- Overload: no parameters reads, one parameter writes ---------------------------

        [ConsoleMethod("test.timescale", "Logs the current Time.timeScale")]
        public static void TimeScale() => Debug.Log("Time.timeScale = " + Time.timeScale);

        [ConsoleMethod("test.timescale", "Sets Time.timeScale", "scale")]
        public static void TimeScale(float scale)
        {
            Time.timeScale = Mathf.Max(0f, scale);
            Debug.Log("Time.timeScale = " + Time.timeScale);
        }
    }
}
#endif
