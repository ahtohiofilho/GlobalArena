using GlobalArena.Kernel;

namespace GlobalArena.Tests;

public sealed class SimulationContextTests
{
    [Fact]
    public void TurnAndSeedCreateSimulationContext()
    {
        var turn = new TurnNumber(7UL);
        var seed = new SimulationSeed(12345UL);

        var context = new SimulationContext(turn, seed);

        Assert.Equal(turn, context.Turn);
        Assert.Equal(seed, context.Seed);
    }

    [Fact]
    public void SameTurnAndSeedProduceEqualContexts()
    {
        var first = new SimulationContext(
            new TurnNumber(12UL),
            new SimulationSeed(98765UL));

        var second = new SimulationContext(
            new TurnNumber(12UL),
            new SimulationSeed(98765UL));

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentTurnsProduceDifferentContexts()
    {
        var seed = new SimulationSeed(98765UL);

        var first = new SimulationContext(
            new TurnNumber(12UL),
            seed);

        var second = new SimulationContext(
            new TurnNumber(13UL),
            seed);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DifferentSeedsProduceDifferentContexts()
    {
        var turn = new TurnNumber(12UL);

        var first = new SimulationContext(
            turn,
            new SimulationSeed(100UL));

        var second = new SimulationContext(
            turn,
            new SimulationSeed(200UL));

        Assert.NotEqual(first, second);
    }
}
