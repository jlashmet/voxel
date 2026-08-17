namespace Game.Structures.Runtime
{
    /// <summary>
    /// Single dispatch point for post-bootstrap content packs. New packs register here while the
    /// bootstrap catalog and its original stable IDs remain untouched.
    /// </summary>
    public static class DecorationContentExpansionRegistry
    {
        public static DecorationContentRecipe Recipe(DecorationContentKind kind)
        {
            DecorationContentRecipe recipe = DecorationContentCraftExpansionCatalog.Recipe(kind);
            if (recipe.IsWellFormed)
                return recipe;

            recipe = DecorationContentFoodExpansionCatalog.Recipe(kind);
            return recipe;
        }
    }
}
