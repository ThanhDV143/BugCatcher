using UnityEngine.Scripting;

// Nothing in a game references BugCatcher (it starts itself via RuntimeInitializeOnLoadMethod),
// so without this the linker strips the whole assembly from IL2CPP builds.
[assembly: AlwaysLinkAssembly]
