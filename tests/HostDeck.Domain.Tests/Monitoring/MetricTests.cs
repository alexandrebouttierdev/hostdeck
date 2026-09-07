using System;
using System.Globalization;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;
using Xunit;

namespace HostDeck.Domain.Tests.Monitoring;

public sealed class PercentageTests
{
    [Theory]
    [InlineData(0d)]
    [InlineData(37.5d)]
    [InlineData(100d)]
    public void AcceptsValuesInRange(double value) => Assert.Equal(value, new Percentage(value).Value);

    [Theory]
    [InlineData(-0.1d)]
    [InlineData(100.1d)]
    public void RejectsValuesOutOfRange(double value) =>
        Assert.Throws<DomainValidationException>(() => new Percentage(value));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RejectsNonFiniteValues(double value) =>
        Assert.Throws<DomainValidationException>(() => new Percentage(value));

    /// <summary>
    /// Un calcul de CPU issu de deux relevés de /proc/stat peut produire 100,3 % par
    /// arrondi. Clamp absorbe ce bruit, là où le constructeur refuserait la mesure.
    /// </summary>
    [Theory]
    [InlineData(100.3d, 100d)]
    [InlineData(-0.2d, 0d)]
    [InlineData(42d, 42d)]
    public void ClampBringsMeasurementNoiseBackInRange(double input, double expected) =>
        Assert.Equal(expected, Percentage.Clamp(input).Value);

    [Fact]
    public void ClampStillRejectsNaN() =>
        Assert.Throws<DomainValidationException>(() => Percentage.Clamp(double.NaN));

    [Fact]
    public void RatioComputesUsagePercentage() =>
        Assert.Equal(50d, Percentage.OfRatio(50d, 100d).Value);

    /// <summary>
    /// Un système de fichiers de taille nulle n'est pas saturé : la division par zéro doit
    /// donner 0 %, pas NaN ni une exception.
    /// </summary>
    [Fact]
    public void RatioOfZeroCapacityIsZeroRatherThanDivisionByZero() =>
        Assert.Equal(0d, Percentage.OfRatio(10d, 0d).Value);
}

public sealed class ByteSizeTests
{
    [Fact]
    public void RejectsNegativeSizes() =>
        Assert.Throws<DomainValidationException>(() => new ByteSize(-1L));

    [Fact]
    public void ConvertsUsingBinaryUnits()
    {
        var size = ByteSize.FromMebibytes(1024);

        Assert.Equal(1d, size.Gibibytes);
        Assert.Equal(1_073_741_824L, size.Bytes);
    }

    /// <summary>
    /// Un compteur réseau remis à zéro par un redémarrage d'interface donnerait un delta
    /// négatif, donc un débit absurde. La soustraction est bornée à zéro.
    /// </summary>
    [Fact]
    public void SubtractionIsClampedToZero()
    {
        var result = new ByteSize(100L) - new ByteSize(500L);

        Assert.Equal(0L, result.Bytes);
    }

    [Fact]
    public void CounterFitsBeyondIntRange()
    {
        var huge = new ByteSize(9_000_000_000L);

        Assert.Equal(9_000_000_000L, huge.Bytes);
    }

    [Fact]
    public void FormatsWithBinaryUnitsIndependentlyOfCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            Assert.Equal("1 GiB", new ByteSize(1_073_741_824L).ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

public sealed class CpuUsageTests
{
    [Fact]
    public void TotalIsEverythingThatIsNotIdle()
    {
        var cpu = new CpuUsage(
            new Percentage(18.4),
            new Percentage(7.2),
            new Percentage(4.1),
            new Percentage(0.3),
            new Percentage(0.1),
            4);

        Assert.Equal(30.1d, cpu.Total.Value, precision: 5);
        Assert.Equal(69.9d, cpu.Idle.Value, precision: 5);
    }

    /// <summary>
    /// La somme des modes peut dépasser 100 % de quelques dixièmes par arrondi ; le total
    /// est borné plutôt que de lever et de perdre la mesure.
    /// </summary>
    [Fact]
    public void TotalIsClampedWhenModesSumAboveOneHundred()
    {
        var cpu = new CpuUsage(
            new Percentage(60d),
            new Percentage(30d),
            new Percentage(20d),
            new Percentage(0d),
            new Percentage(0d),
            8);

        Assert.Equal(100d, cpu.Total.Value);
        Assert.Equal(0d, cpu.Idle.Value);
    }

    [Fact]
    public void RejectsZeroCores() =>
        Assert.Throws<DomainValidationException>(() => new CpuUsage(
            Percentage.Zero, Percentage.Zero, Percentage.Zero, Percentage.Zero, Percentage.Zero, 0));
}

public sealed class MemoryUsageTests
{
    [Fact]
    public void AvailableIsTotalMinusUsed()
    {
        var memory = new MemoryUsage(
            ByteSize.FromMebibytes(20480),
            ByteSize.FromMebibytes(11800),
            ByteSize.FromMebibytes(4300),
            ByteSize.FromMebibytes(2150));

        Assert.Equal(ByteSize.FromMebibytes(8680).Bytes, memory.Available.Bytes);
        Assert.Equal(57.6d, memory.UsedRatio.Value, precision: 1);
    }

    [Fact]
    public void RejectsUsedAboveTotal() =>
        Assert.Throws<DomainValidationException>(() => new MemoryUsage(
            ByteSize.FromMebibytes(1024),
            ByteSize.FromMebibytes(2048),
            ByteSize.Zero,
            ByteSize.Zero));

    [Fact]
    public void MachineWithoutSwapIsNotAnError()
    {
        Assert.False(SwapUsage.None.IsConfigured);
        Assert.Equal(0d, SwapUsage.None.UsedRatio.Value);
    }
}

public sealed class DiskUsageTests
{
    [Fact]
    public void ComputesAvailableAndRatio()
    {
        var disk = new DiskUsage("/var", "/dev/sda1", ByteSize.FromMebibytes(1000), ByteSize.FromMebibytes(850));

        Assert.Equal(85d, disk.UsedRatio.Value, precision: 5);
        Assert.Equal(ByteSize.FromMebibytes(150).Bytes, disk.Available.Bytes);
    }

    [Fact]
    public void RejectsUsedAboveCapacity() =>
        Assert.Throws<DomainValidationException>(() => new DiskUsage(
            "/", null, ByteSize.FromMebibytes(100), ByteSize.FromMebibytes(200)));

    [Fact]
    public void RejectsBlankMountPoint() =>
        Assert.Throws<DomainValidationException>(() => new DiskUsage(
            "  ", null, ByteSize.FromMebibytes(100), ByteSize.Zero));
}

public sealed class NetworkUsageTests
{
    [Fact]
    public void ComputesRateFromTwoCumulativeReadings()
    {
        var previous = new NetworkUsage("eth0", new ByteSize(1000L), new ByteSize(500L));
        var current = new NetworkUsage("eth0", new ByteSize(3000L), new ByteSize(1500L));

        var rate = current.RateSince(previous, TimeSpan.FromSeconds(2));

        Assert.Equal(1000d, rate.ReceivedBytesPerSecond);
        Assert.Equal(500d, rate.TransmittedBytesPerSecond);
    }

    /// <summary>
    /// Comparer deux interfaces différentes produirait un débit silencieusement faux.
    /// </summary>
    [Fact]
    public void RejectsComparingDifferentInterfaces()
    {
        var previous = new NetworkUsage("eth0", new ByteSize(1000L), ByteSize.Zero);
        var current = new NetworkUsage("eth1", new ByteSize(3000L), ByteSize.Zero);

        Assert.Throws<DomainValidationException>(() =>
            current.RateSince(previous, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void CounterResetProducesZeroRateRatherThanNegative()
    {
        var previous = new NetworkUsage("eth0", new ByteSize(9_000_000L), ByteSize.Zero);
        var afterReboot = new NetworkUsage("eth0", new ByteSize(120L), ByteSize.Zero);

        var rate = afterReboot.RateSince(previous, TimeSpan.FromSeconds(60));

        Assert.Equal(0d, rate.ReceivedBytesPerSecond);
    }

    [Fact]
    public void ZeroElapsedTimeProducesZeroRateRatherThanInfinity()
    {
        var previous = new NetworkUsage("eth0", new ByteSize(1000L), ByteSize.Zero);
        var current = new NetworkUsage("eth0", new ByteSize(3000L), ByteSize.Zero);

        var rate = current.RateSince(previous, TimeSpan.Zero);

        Assert.Equal(0d, rate.ReceivedBytesPerSecond);
    }
}

public sealed class MetricSampleTests
{
    [Fact]
    public void ObservationInstantIsStoredInUtc()
    {
        var sample = TestData.Sample(new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.FromHours(2)));

        Assert.Equal(TimeSpan.Zero, sample.ObservedAt.Offset);
        Assert.Equal(10, sample.ObservedAt.Hour);
    }

    /// <summary>
    /// C'est la partition la plus remplie qui déclenche l'incident, pas la moyenne.
    /// </summary>
    [Fact]
    public void BusiestDiskIsTheFullestMountPoint()
    {
        var sample = new MetricSample(
            ServerId.New(),
            TestData.Now,
            TestData.Cpu(),
            TestData.Memory(),
            SwapUsage.None,
            new LoadAverage(1d, 1d, 1d),
            Uptime.FromSeconds(3600),
            disks:
            [
                new DiskUsage("/", null, ByteSize.FromMebibytes(1000), ByteSize.FromMebibytes(300)),
                new DiskUsage("/var", null, ByteSize.FromMebibytes(1000), ByteSize.FromMebibytes(930)),
                new DiskUsage("/home", null, ByteSize.FromMebibytes(1000), ByteSize.FromMebibytes(500)),
            ]);

        Assert.Equal("/var", sample.BusiestDisk!.MountPoint);
    }

    [Fact]
    public void BusiestDiskIsNullWithoutAnyDisk() => Assert.Null(TestData.Sample().BusiestDisk);

    /// <summary>
    /// Un redémarrage invalide les compteurs cumulés, donc tout débit calculé à partir
    /// d'eux. Le détecter est indispensable avant de dériver un taux.
    /// </summary>
    [Fact]
    public void DetectsRebootFromUptimeGoingBackwards()
    {
        var before = TestData.Sample(TestData.Now, uptimeSeconds: 86400d);
        var after = TestData.Sample(TestData.Now.AddMinutes(1), uptimeSeconds: 60d);

        Assert.True(after.IndicatesRebootSince(before));
        Assert.False(before.IndicatesRebootSince(before));
    }

    [Fact]
    public void CollectionsAreExposedAsImmutableArrays()
    {
        var sample = TestData.Sample();

        Assert.Empty(sample.Disks);
        Assert.Empty(sample.Interfaces);
    }
}

public sealed class RetentionPolicyTests
{
    [Fact]
    public void DefaultMatchesTheSettingsScreen()
    {
        Assert.Equal(TimeSpan.FromDays(90), RetentionPolicy.Default.MetricRetention);
        Assert.Equal(TimeSpan.FromDays(180), RetentionPolicy.Default.EventRetention);
    }

    [Fact]
    public void CutoffIsNowMinusRetention()
    {
        var cutoff = RetentionPolicy.Default.MetricCutoff(TestData.Now);

        Assert.Equal(TestData.Now.AddDays(-90), cutoff);
    }

    [Fact]
    public void RejectsRetentionBelowOneDay() =>
        Assert.Throws<DomainValidationException>(() =>
            new RetentionPolicy(TimeSpan.FromHours(1), TimeSpan.FromDays(30)));
}

public sealed class UptimeTests
{
    [Fact]
    public void ComputesBootInstant()
    {
        var uptime = Uptime.FromSeconds(3600d);

        Assert.Equal(TestData.Now.AddHours(-1), uptime.BootedAt(TestData.Now));
    }

    [Fact]
    public void RejectsNegativeUptime() =>
        Assert.Throws<DomainValidationException>(() => new Uptime(TimeSpan.FromSeconds(-1)));
}

public sealed class LoadAverageTests
{
    [Fact]
    public void PerCoreNormalisesAgainstCoreCount()
    {
        var load = new LoadAverage(4d, 3d, 2d);

        Assert.Equal(1d, load.PerCore(4));
        Assert.Equal(0.5d, load.PerCore(8));
    }

    [Fact]
    public void PerCoreFallsBackToRawLoadWhenCoreCountIsUnknown() =>
        Assert.Equal(4d, new LoadAverage(4d, 3d, 2d).PerCore(0));

    [Fact]
    public void RejectsNegativeLoad() =>
        Assert.Throws<DomainValidationException>(() => new LoadAverage(-1d, 0d, 0d));
}
