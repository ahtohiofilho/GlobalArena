using System.Globalization;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class WorldGenerationCanonicalSignatureTests
{
    private const string ClassIDigest =
        "fa9677da59098b4eccc4a3911299520ad0549780b4470b2fae96f607da774c52";

    private const string ClassIIDigest =
        "a7d8fa845d373b9f77484dd0f1955fdc3d1897041d71eec0a785a5e4028e2f4c";

    private const string ClassIIIDigest =
        "91829cd3837be046631c83523b3857001479dd2cd5bed38e1a425b07b7cef349";

    [Fact]
    public void SignatureRejectsNonPositiveFormatVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new WorldGenerationSignature(
                    0,
                    new string(
                        '0',
                        64)));
    }

    [Fact]
    public void SignatureRejectsMalformedDigest()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new WorldGenerationSignature(
                    1,
                    "abc"));
    }

    [Fact]
    public void SignatureRejectsUppercaseDigest()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new WorldGenerationSignature(
                    1,
                    new string(
                        'A',
                        64)));
    }

    [Fact]
    public void NullResultIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                WorldGenerationCanonicalSignature.Compute(
                    null!));
    }

    [Fact]
    public void SignatureUsesCurrentFormatVersion()
    {
        var signature =
            Compute(
                7UL,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    1,
                    0));

        Assert.Equal(
            WorldGenerationSignature.CurrentFormatVersion,
            signature.FormatVersion);
    }

    [Fact]
    public void DigestIsCanonicalLowercaseSha256Hex()
    {
        var signature =
            Compute(
                7UL,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    1,
                    0));

        Assert.Equal(
            64,
            signature.Sha256Hex.Length);

        Assert.Matches(
            "^[0-9a-f]{64}$",
            signature.Sha256Hex);

        Assert.Equal(
            signature.Sha256Hex,
            signature.ToString());
    }

    [Fact]
    public void RepeatedGenerationProducesSameSignature()
    {
        var first =
            Compute(
                11UL,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    1));

        var second =
            Compute(
                11UL,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    1));

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void DifferentWorldSeedChangesSignature()
    {
        var first =
            Compute(
                13UL,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    1));

        var second =
            Compute(
                14UL,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    1));

        Assert.NotEqual(
            first.Sha256Hex,
            second.Sha256Hex);
    }

    [Fact]
    public void UnsupportedWorldGenerationVersionIsRejectedBeforeSignature()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var request =
            new WorldGenerationRequest(
                new WorldSeed(
                    17UL),
                new WorldGenerationVersion(
                    2),
                new GoldbergParameters(
                    2,
                    1));

        Assert.Throws<NotSupportedException>(
            () =>
                generator.Generate(
                    request));
    }

    [Fact]
    public void DifferentGoldbergParametersChangeSignature()
    {
        var first =
            Compute(
                19UL,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    0));

        var second =
            Compute(
                19UL,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    1));

        Assert.NotEqual(
            first.Sha256Hex,
            second.Sha256Hex);
    }

    [Fact]
    public void SignatureIsCultureIndependent()
    {
        var originalCulture =
            CultureInfo.CurrentCulture;

        var originalUiCulture =
            CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo(
                    "pt-BR");

            CultureInfo.CurrentUICulture =
                CultureInfo.GetCultureInfo(
                    "pt-BR");

            var first =
                Compute(
                    23UL,
                    WorldGenerationVersion.Initial,
                    new GoldbergParameters(
                        2,
                        1));

            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo(
                    "tr-TR");

            CultureInfo.CurrentUICulture =
                CultureInfo.GetCultureInfo(
                    "tr-TR");

            var second =
                Compute(
                    23UL,
                    WorldGenerationVersion.Initial,
                    new GoldbergParameters(
                        2,
                        1));

            Assert.Equal(
                first,
                second);
        }
        finally
        {
            CultureInfo.CurrentCulture =
                originalCulture;

            CultureInfo.CurrentUICulture =
                originalUiCulture;
        }
    }

    [Fact]
    public void ClassIKnownRegressionVectorMatches()
    {
        var signature =
            Compute(
                0UL,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    0));

        Assert.Equal(
            ClassIDigest,
            signature.Sha256Hex);
    }

    [Fact]
    public void ClassIIKnownRegressionVectorMatches()
    {
        var signature =
            Compute(
                42UL,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    2));

        Assert.Equal(
            ClassIIDigest,
            signature.Sha256Hex);
    }

    [Fact]
    public void ClassIIIKnownRegressionVectorMatches()
    {
        var signature =
            Compute(
                ulong.MaxValue,
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    1));

        Assert.Equal(
            ClassIIIDigest,
            signature.Sha256Hex);
    }

    [Fact]
    public void KnownRegressionVectorsAreDistinct()
    {
        Assert.Equal(
            3,
            new[]
            {
                ClassIDigest,
                ClassIIDigest,
                ClassIIIDigest
            }
            .Distinct()
            .Count());
    }

    private static WorldGenerationSignature Compute(
        ulong seed,
        WorldGenerationVersion version,
        GoldbergParameters parameters)
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                new WorldGenerationRequest(
                    new WorldSeed(
                        seed),
                    version,
                    parameters));

        return WorldGenerationCanonicalSignature.Compute(
            result);
    }
}
