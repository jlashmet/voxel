namespace Game.Residency.Api
{
    /// <summary>
    /// Presentation/interaction realization lifetime for an already-authoritative semantic target.
    /// Implementations may create or despawn engine-facing representation but never own target identity
    /// or authoritative gameplay state.
    /// </summary>
    public interface IResidencyTargetRealizationLifecycle
    {
        bool IsRealized(ResidencyTarget target);
        bool TryRealize(ResidencyTarget target);
        bool TryUnrealize(ResidencyTarget target);
    }
}
