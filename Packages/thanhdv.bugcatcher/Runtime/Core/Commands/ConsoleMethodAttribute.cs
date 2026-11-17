using System;

namespace ThanhDV.BugCatcher
{
    /// <summary>
    /// Turns a static method into a console command. The method's assembly must reference BugCatcher, which it does
    /// by using this attribute. Wrap the method in <c>#if BUGCATCHER_ENABLED</c> (or put it in an assembly that
    /// requires the symbol): without the symbol this attribute does not exist and the build fails.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class ConsoleMethodAttribute : Attribute
    {
        public string Command { get; }
        public string Description { get; }
        /// <summary>Names shown in suggestions; a missing entry falls back to the C# parameter name.</summary>
        public string[] ParameterNames { get; }

        public ConsoleMethodAttribute(string command, string description, params string[] parameterNames)
        {
            Command = command;
            Description = description;
            ParameterNames = parameterNames;
        }
    }
}
