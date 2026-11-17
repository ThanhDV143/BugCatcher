using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ThanhDV.BugCatcher
{
    /// <summary>A console command as shown in suggestions.</summary>
    public readonly struct CommandInfo
    {
        public readonly string Name;
        public readonly string Parameters;
        public readonly string Description;
        public readonly int ParameterCount;

        public CommandInfo(string name, string parameters, string description, int parameterCount)
        {
            Name = name;
            Parameters = parameters;
            Description = description;
            ParameterCount = parameterCount;
        }
    }

    /// <summary>
    /// Console commands: the <see cref="ConsoleMethodAttribute"/> methods plus the ones added from code.
    /// Runs command lines, suggests commands and keeps the history.
    /// </summary>
    public class CommandService
    {
        private const string HelpCommand = "help";

        private readonly BugCatcherSettings settings;
        private readonly List<string> arguments = new List<string>(8);
        private List<Command> commands;

        public CommandService(BugCatcherSettings settings)
        {
            this.settings = settings;
        }

        public IReadOnlyList<string> Recent => settings.recentCommands;

        /// <summary>
        /// Sorted by name, then by parameter count. The attribute search runs on first use, not at startup.
        /// Commands whose target object was destroyed are dropped here.
        /// </summary>
        private List<Command> Commands
        {
            get
            {
                if (commands == null) commands = FindCommands();
                else commands.RemoveAll(c => !c.IsValid);
                return commands;
            }
        }

        // --- Adding and removing from code -------------------------------------------------

        /// <summary>
        /// Adds a command that calls <paramref name="method"/>: a static method, a method of an object, or a lambda.
        /// A command with the same name and parameter types is replaced. A command whose target is a destroyed
        /// Unity object is removed automatically. <paramref name="parameterNames"/> are the names shown in suggestions.
        /// </summary>
        public void Add(string command, string description, Delegate method, params string[] parameterNames)
        {
            if (method == null)
            {
                Debug.LogError("[BugCatcher] Command '" + command + "': method is null");
                return;
            }

            Command created = Command.Create(command, description, parameterNames, method.Method, method.Target);
            if (created != null) Insert(Commands, created);
        }

        public void Add(string command, string description, Action method) => Add(command, description, (Delegate)method);
        public void Add<T1>(string command, string description, Action<T1> method, params string[] parameterNames) => Add(command, description, (Delegate)method, parameterNames);
        public void Add<T1, T2>(string command, string description, Action<T1, T2> method, params string[] parameterNames) => Add(command, description, (Delegate)method, parameterNames);
        public void Add<T1, T2, T3>(string command, string description, Action<T1, T2, T3> method, params string[] parameterNames) => Add(command, description, (Delegate)method, parameterNames);
        public void Add<T1, T2, T3, T4>(string command, string description, Action<T1, T2, T3, T4> method, params string[] parameterNames) => Add(command, description, (Delegate)method, parameterNames);
        public void Add<TResult>(string command, string description, Func<TResult> method) => Add(command, description, (Delegate)method);
        public void Add<T1, TResult>(string command, string description, Func<T1, TResult> method, params string[] parameterNames) => Add(command, description, (Delegate)method, parameterNames);
        public void Add<T1, T2, TResult>(string command, string description, Func<T1, T2, TResult> method, params string[] parameterNames) => Add(command, description, (Delegate)method, parameterNames);
        public void Add<T1, T2, T3, TResult>(string command, string description, Func<T1, T2, T3, TResult> method, params string[] parameterNames) => Add(command, description, (Delegate)method, parameterNames);
        public void Add<T1, T2, T3, T4, TResult>(string command, string description, Func<T1, T2, T3, T4, TResult> method, params string[] parameterNames) => Add(command, description, (Delegate)method, parameterNames);

        /// <summary>Removes every command named <paramref name="command"/>, all overloads included.</summary>
        public void Remove(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            command = command.Trim();
            Commands.RemoveAll(c => string.Equals(c.Name, command, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Removes the commands that call <paramref name="method"/> on the same target; other overloads stay.</summary>
        public void Remove(Delegate method)
        {
            if (method == null) return;
            Commands.RemoveAll(c => c.Method == method.Method && ReferenceEquals(c.Target, method.Target));
        }

        // --- Running and suggesting ---------------------------------------------------------

        public void Execute(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            command = command.Trim();
            settings.AddRecentCommand(command);
            Debug.Log("> " + command);
            Run(command);
        }

        /// <summary>Matches the first word of <paramref name="input"/> against command names, one row per name.</summary>
        public void Suggest(string input, List<CommandInfo> output, int max)
        {
            output.Clear();
            string query = FirstWord(input);
            if (query.Length == 0) return;

            string lastName = null;
            foreach (Command method in Commands)
            {
                if (MatchIndex(method.Name, query) < 0) continue;
                if (string.Equals(method.Name, lastName, StringComparison.OrdinalIgnoreCase))
                {
                    // Overloads: keep the one with the most parameters.
                    if (method.ParameterTypes.Length > output[output.Count - 1].ParameterCount) output[output.Count - 1] = method.Info;
                    continue;
                }

                if (output.Count >= max) break;
                output.Add(method.Info);
                lastName = method.Name;
            }
        }

        /// <summary>Commands that take no parameters, so a single tap can run them.</summary>
        public void QuickCommands(List<string> output, int max)
        {
            output.Clear();
            List<Command> all = Commands;
            var withParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Command method in all)
            {
                if (method.ParameterTypes.Length > 0) withParameters.Add(method.Name);
            }

            foreach (Command method in all)
            {
                if (output.Count >= max) break;
                if (method.ParameterTypes.Length > 0 || withParameters.Contains(method.Name)) continue;
                if (method.Name == HelpCommand || output.Contains(method.Name)) continue;
                output.Add(method.Name);
            }
        }

        internal static int MatchIndex(string name, string query)
        {
            int index = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            return query.Length == 1 && index != 0 ? -1 : index;
        }

        internal static string FirstWord(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            input = input.TrimStart();
            int space = input.IndexOf(' ');
            return space < 0 ? input : input.Substring(0, space);
        }

        [ConsoleMethod(HelpCommand, "Prints all commands")]
        private static void Help()
        {
            var text = new StringBuilder("Commands:");
            foreach (Command method in BugCatcher.Commands.Commands) text.Append("\n    ").Append(method.Signature);
            Debug.Log(text.ToString());
        }

        private void Run(string line)
        {
            CommandParser.Split(line, arguments);
            string name = arguments.Count > 0 ? arguments[0] : string.Empty;
            if (name.Length == 0)
            {
                Debug.LogWarning("Can't find command '" + line + "'.");
                return;
            }

            int argumentCount = arguments.Count - 1;
            bool nameFound = false;
            string parseError = null;
            // A copy: the command may add or remove commands while it runs.
            foreach (Command method in Commands.ToArray())
            {
                if (!string.Equals(method.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                nameFound = true;
                if (method.ParameterTypes.Length != argumentCount) continue;

                var values = new object[argumentCount];
                if (!TryParseArguments(method, values, out parseError)) continue;

                Invoke(method, values);
                return;
            }

            if (parseError != null)
            {
                Debug.LogWarning(parseError);
                return;
            }

            // Wrong parameter count: list the overloads. Unknown name: list the commands that contain it.
            var text = new StringBuilder(nameFound
                ? "'" + name + "' doesn't take " + argumentCount + " parameter(s). Available command(s):"
                : "Can't find command '" + name + "'.");
            int listed = 0;
            foreach (Command method in Commands)
            {
                bool related = nameFound
                    ? string.Equals(method.Name, name, StringComparison.OrdinalIgnoreCase)
                    : method.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!related) continue;
                if (listed++ == 0 && !nameFound) text.Append(" Did you mean:");
                text.Append("\n    ").Append(method.Signature);
            }
            Debug.LogWarning(text.ToString());
        }

        private bool TryParseArguments(Command method, object[] values, out string error)
        {
            for (int i = 0; i < values.Length; i++)
            {
                string argument = arguments[i + 1];
                Type type = method.ParameterTypes[i];
                if (!CommandParser.TryParse(argument, type, out values[i]))
                {
                    error = "Couldn't parse '" + argument + "' to " + type.Name + " for " + method.Signature;
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static void Invoke(Command method, object[] values)
        {
            object result;
            try
            {
                result = method.Method.Invoke(method.Target, values);
            }
            catch (TargetInvocationException e)
            {
                Debug.LogException(e.InnerException ?? e);
                return;
            }

            if (method.Method.ReturnType != typeof(void)) Debug.Log("Returned: " + (result ?? "null"));
        }

        /// <summary>Keeps the list sorted by name, then parameter count; replaces a command with the same signature.</summary>
        private static void Insert(List<Command> list, Command command)
        {
            int index = 0;
            while (index < list.Count && Compare(list[index], command) <= 0)
            {
                if (Compare(list[index], command) == 0 && SameParameters(list[index], command))
                {
                    list[index] = command;
                    return;
                }
                index++;
            }
            list.Insert(index, command);
        }

        private static int Compare(Command a, Command b)
        {
            int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : a.ParameterTypes.Length.CompareTo(b.ParameterTypes.Length);
        }

        private static bool SameParameters(Command a, Command b)
        {
            for (int i = 0; i < a.ParameterTypes.Length; i++)
            {
                if (a.ParameterTypes[i] != b.ParameterTypes[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// Searches BugCatcher's assembly and the assemblies that reference it: no other assembly can use
        /// <see cref="ConsoleMethodAttribute"/>, so skipping them keeps the search short.
        /// </summary>
        private static List<Command> FindCommands()
        {
            var found = new List<Command>();
            Assembly own = typeof(CommandService).Assembly;
            string ownName = own.GetName().Name;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.IsDynamic || (assembly != own && !References(assembly, ownName))) continue;

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        types = e.Types;
                    }

                    foreach (Type type in types)
                    {
                        if (type == null) continue;
                        foreach (MethodInfo method in type.GetMethods(flags))
                        {
                            foreach (ConsoleMethodAttribute attribute in method.GetCustomAttributes(typeof(ConsoleMethodAttribute), false))
                            {
                                Command command = Command.Create(attribute.Command, attribute.Description, attribute.ParameterNames, method, null);
                                if (command != null) Insert(found, command);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[BugCatcher] Couldn't search " + assembly.GetName().Name + " for commands\n" + e);
                }
            }

            return found;
        }

        private static bool References(Assembly assembly, string name)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name == name) return true;
            }
            return false;
        }

        private sealed class Command
        {
            public string Name;
            public MethodInfo Method;
            /// <summary>Null for static methods.</summary>
            public object Target;
            public Type[] ParameterTypes;
            public CommandInfo Info;
            /// <summary>"name &lt;a&gt; &lt;b&gt;: description", for messages.</summary>
            public string Signature;

            /// <summary>False once the object the command runs on is gone (a destroyed Unity object).</summary>
            public bool IsValid => Method.IsStatic || (Target != null && !(Target is UnityEngine.Object unityObject && unityObject == null));

            /// <summary>Null, with an error logged, when the method cannot be a command.</summary>
            public static Command Create(string command, string description, string[] parameterNames, MethodInfo method, object target)
            {
                string where = method.DeclaringType + "." + method.Name;
                string name = command?.Trim();
                if (string.IsNullOrEmpty(name) || name.IndexOf(' ') >= 0)
                {
                    Debug.LogError("[BugCatcher] Command name must be one word: '" + command + "' on " + where);
                    return null;
                }

                if (method.IsGenericMethodDefinition)
                {
                    Debug.LogError("[BugCatcher] Command '" + name + "' can't be a generic method: " + where);
                    return null;
                }

                ParameterInfo[] parameters = method.GetParameters();
                var types = new Type[parameters.Length];
                var shown = new StringBuilder();
                for (int i = 0; i < parameters.Length; i++)
                {
                    Type type = parameters[i].ParameterType;
                    if (type.IsByRef || !CommandParser.IsSupported(type))
                    {
                        Debug.LogError("[BugCatcher] Command '" + name + "': parameter '" + parameters[i].Name + "' of type " + type + " isn't supported (" + where + ")");
                        return null;
                    }

                    types[i] = type;
                    string shownName = parameterNames != null && i < parameterNames.Length && !string.IsNullOrEmpty(parameterNames[i]) ? parameterNames[i] : parameters[i].Name;
                    if (shown.Length > 0) shown.Append(' ');
                    shown.Append('<').Append(shownName).Append('>');
                }

                description = description ?? string.Empty;
                string signature = shown.Length > 0 ? name + " " + shown : name;
                if (description.Length > 0) signature += ": " + description;

                return new Command
                {
                    Name = name,
                    Method = method,
                    Target = method.IsStatic ? null : target,
                    ParameterTypes = types,
                    Info = new CommandInfo(name, shown.ToString(), description, types.Length),
                    Signature = signature
                };
            }
        }
    }
}
