// Compile-time-only reference stub for the game's Timberborn.ModManagerScene assembly.
// Never bundle this assembly in a released mod: Timberborn supplies the real one.
namespace Timberborn.ModManagerScene
{
    public interface IModStarter
    {
        void StartMod(IModEnvironment modEnvironment);
    }

    public interface IModEnvironment
    {
        string ModPath { get; }
    }
}
