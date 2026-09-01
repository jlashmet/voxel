using Game.Characters.Api;
using Game.Characters.Runtime;

namespace Game.Characters.Composition
{
    /// <summary>
    /// Composition-only construction seam for the gameplay character runtime.
    /// Consumers retain only Characters.Api contracts after construction.
    /// </summary>
    public static class CharacterRuntimeFactory
    {
        public static ICharacterRegistry CreateRegistry() => new CharacterRegistry();
    }
}
