namespace GlobalArena.Simulation;

/// <summary>
/// Internal failpoint surface for deterministic interruption and adversarial
/// destination tests. NOT a production orchestration hook.
/// </summary>
internal enum EconomicV9VersionedRecoveryPhase
{
    ArchiveVerified,
    InventoryExtracted,
    ReplayVerified,
    BeforePublish,
    AfterPublish,
    BeforeAdmission
}

internal interface IEconomicV9VersionedRecoveryPhaseProbe
{
    void OnPhase(EconomicV9VersionedRecoveryPhase phase);
}
