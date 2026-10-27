using System;
using System.Collections.Generic;
using IngameDebugConsole;
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
    /// Thin layer over In-game Debug Console's command system: run, suggest, history,
    /// plus the built-in hud.* commands that mirror every HUD setting.
    /// </summary>
    public class CommandService
    {
        private readonly BugCatcherSettings settings;

        public CommandService(BugCatcherSettings settings)
        {
            this.settings = settings;
        }

        public IReadOnlyList<string> Recent => settings.recentCommands;

        public void Execute(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            command = command.Trim();
            settings.AddRecentCommand(command);
            Debug.Log("> " + command);
            DebugLogConsole.ExecuteCommand(command);
        }

        /// <summary>Commands whose name starts with the first word of <paramref name="input"/>, one row per name.</summary>
        public void Suggest(string input, List<CommandInfo> output, int max)
        {
            output.Clear();
            string prefix = FirstWord(input);
            if (prefix.Length == 0) return;

            string lastName = null;
            foreach (ConsoleMethodInfo method in DebugLogConsole.GetAllCommands())
            {
                if (!method.command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(method.command, lastName, StringComparison.OrdinalIgnoreCase))
                {
                    // Overloads: keep the one with the most parameters.
                    if (method.parameters.Length > output[output.Count - 1].ParameterCount) output[output.Count - 1] = ToInfo(method);
                    continue;
                }

                if (output.Count >= max) break;
                output.Add(ToInfo(method));
                lastName = method.command;
            }
        }

        /// <summary>Commands that take no parameters, so a single tap can run them.</summary>
        public void QuickCommands(List<string> output, int max)
        {
            output.Clear();
            var withParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ConsoleMethodInfo method in DebugLogConsole.GetAllCommands())
            {
                if (method.parameters.Length > 0) withParameters.Add(method.command);
            }

            foreach (ConsoleMethodInfo method in DebugLogConsole.GetAllCommands())
            {
                if (output.Count >= max) break;
                if (method.parameters.Length > 0 || withParameters.Contains(method.command)) continue;
                if (method.command == "help" || output.Contains(method.command)) continue;
                output.Add(method.command);
            }
        }

        private static string FirstWord(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            input = input.TrimStart();
            int space = input.IndexOf(' ');
            return space < 0 ? input : input.Substring(0, space);
        }

        private static CommandInfo ToInfo(ConsoleMethodInfo method)
        {
            var parameters = new System.Text.StringBuilder();
            foreach (string parameter in method.parameters)
            {
                // IDC formats parameters as "[Type name]"; show "<name>".
                string trimmed = parameter.Trim('[', ']', ' ');
                int space = trimmed.LastIndexOf(' ');
                string name = space < 0 ? trimmed : trimmed.Substring(space + 1);
                if (parameters.Length > 0) parameters.Append(' ');
                parameters.Append('<').Append(name).Append('>');
            }

            string description = string.Empty;
            int split = method.signature.IndexOf("</b>: ", StringComparison.Ordinal);
            if (split >= 0) description = method.signature.Substring(split + 6);

            return new CommandInfo(method.command, parameters.ToString(), description, method.parameters.Length);
        }
    }
}
