using GlobalArena.Kernel;

namespace GlobalArena.Tests;

public sealed class DeterministicRandomTests
{
    [Fact]
    public void SameSeedProducesSameSequence()
    {
        var seed = new SimulationSeed(12345UL);

        var first = new DeterministicRandom(seed);
        var second = new DeterministicRandom(seed);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(first.NextUInt64(), second.NextUInt64());
        }
    }

    [Fact]
    public void SeedZeroProducesStableReferenceSequence()
    {
        var random = new DeterministicRandom(new SimulationSeed(0UL));

        Assert.Equal(16294208416658607535UL, random.NextUInt64());
        Assert.Equal(7960286522194355700UL, random.NextUInt64());
        Assert.Equal(487617019471545679UL, random.NextUInt64());
    }
}
