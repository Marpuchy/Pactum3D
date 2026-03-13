using System.Collections.Generic;

public interface IPactOfferService
{
    IReadOnlyList<PactDefinition> BuildOffers(
        PactPoolSO generalPool,
        PactPoolSO linePool,
        IReadOnlyList<PactDefinition> activePacts,
        int offerCount);

    int CountEligiblePacts(
        PactPoolSO generalPool,
        PactPoolSO linePool,
        IReadOnlyList<PactDefinition> activePacts);

    bool IsPactEligible(
        PactDefinition pact,
        IReadOnlyList<PactDefinition> activePacts);
}
