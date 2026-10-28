using Lorrgs.Api.Models;
using Lorrgs.Api.Services;

namespace Lorrgs.Api.Tests;

public sealed class TimelineCorrectionsTests
{
    private static TimelineEvent Cast(int id, double time) => new(id, time, null, "cast");
    private static IReadOnlyList<TimelineEvent> Correct(int spec, params TimelineEvent[] events) =>
        TimelineCorrections.CorrectEvents(events, spec, "retail", true);

    [Fact]
    public void ExplosiveShotUsesLastRetainedCastAndIncludesExactWindowBoundary()
    {
        var result = Correct(254, Cast(212431, 0), Cast(212431, 5_000), Cast(260243, 8_000),
            Cast(212431, 10_000), Cast(212431, 16_000), Cast(212431, 20_000));

        Assert.Equal(new double[] { 0, 10_000, 20_000 },
            result.Where(item => item.SpellId == 212431).Select(item => item.Timestamp));
        Assert.Contains(result, item => item.SpellId == 260243);
    }

    [Theory]
    [InlineData(254, "anniversary")]
    [InlineData(null, "retail")]
    public void NonRetailAndUnknownSpecsKeepRawCasts(int? spec, string edition)
    {
        TimelineEvent[] casts = [Cast(212431, 0), Cast(212431, 5_000)];
        Assert.Equal(casts, TimelineCorrections.CorrectEvents(casts, spec, edition, true));
    }

    [Fact]
    public void TreeOfLifeIncludesExactCooldownBoundaryAndOnlyAffectsRestoration()
    {
        Assert.Equal(2, Correct(105, Cast(33891, 0), Cast(33891, 119_999), Cast(33891, 120_000)).Count);
        Assert.Equal(2, Correct(102, Cast(33891, 0), Cast(33891, 1_000)).Count);
    }

    [Fact]
    public void AscendanceOnlyRemapsMeasuredIntervalsStrictlyBelowThirteenSeconds()
    {
        var events = Correct(264, new(114052, 0, 6_000, "buff"),
            new(114052, 10_000, 13_000, "buff"), new(114052, 30_000, 5_000, "buff", true),
            Cast(114052, 40_000));

        Assert.Equal(378270, events[0].SpellId);
        Assert.Equal(114052, events[1].SpellId);
        Assert.Equal(114052, events[2].SpellId);
        Assert.Equal("cast", events[3].Kind);
        Assert.Equal("Deeply Rooted Elements", Assert.Single(TimelineCorrections.CorrectSpells([], events)).Name);
    }

    [Fact]
    public void StasisMeasuresStoreDurationAndOnlyRemovesReplaysInsideOneSecond()
    {
        var events = Correct(1468, Cast(370537, 5_000), Cast(370564, 25_000),
            Cast(355936, 25_999), Cast(355936, 26_000), Cast(370564, 60_000));

        Assert.Equal(20_000, Assert.Single(events, item => item.SpellId == 370537 && item.Kind == "cast").Duration);
        Assert.Equal(26_000, Assert.Single(events, item => item.SpellId == 355936).Timestamp);
        Assert.Equal("buff", Assert.Single(events, item => item.IsInferred).Kind);
    }

    [Fact]
    public void LoneStasisReleaseCreatesExplicitInferredPrepullIntervalWithMetadata()
    {
        var events = Correct(1468, Cast(370564, 26_000));
        var prepull = Assert.Single(events, item => item.IsInferred);

        Assert.Equal(-4_000, prepull.Timestamp);
        Assert.Equal(30_000, prepull.Duration);
        Assert.Equal("Stasis", Assert.Single(TimelineCorrections.CorrectSpells([], events)).Name);
    }

    [Fact]
    public void StasisDoesNotInferMissingStoresInPartialStreamsOrChangeOtherEvokerSpecs()
    {
        Assert.All(TimelineCorrections.CorrectEvents([Cast(370564, 26_000)], 1468, "retail", false),
            item => Assert.False(item.IsInferred));
        Assert.Equal(2, Correct(1467, Cast(370564, 26_000), Cast(355936, 26_500)).Count);
    }

    [Fact]
    public void VoidMetaAddsInferredMarkersOnlyForMeasuredEnds()
    {
        var events = Correct(1480, new(1217607, 0, 10_000, "buff"),
            new(1217607, 20_000, 8_000, "buff"), new(1217607, 35_000, 3_000, "buff", true));
        var markers = events.Where(item => item.Kind == "marker").ToArray();

        Assert.Equal(new double[] { 10_000, 28_000 }, markers.Select(item => item.Timestamp));
        Assert.All(markers, item => Assert.True(item.IsInferred));
        Assert.DoesNotContain(TimelineCorrections.CorrectEvents([new(1217607, 0, 10_000, "buff")],
            1480, "retail", false), item => item.Kind == "marker");
    }

    [Fact]
    public void NekzaliRebuildsPhasesFromCastsAndPreservesObservedNoteAnchors()
    {
        TimelinePhase[] raw = [new(1, 0), new(2, 20_000), new(3, 80_000), new(3, 90_000)];
        TimelineEvent[] casts = [Cast(1295124, 20_000), Cast(1292248, 60_000),
            Cast(1292248, 75_000), Cast(1295124, 30_000)];

        var phases = TimelineCorrections.CorrectPhases(3470, raw, casts, 100_000, true);

        Assert.Equal(new double[] { 1, 1.5, 1.75, 2 }, phases.Select(phase => phase.Id));
        Assert.Equal(new double[] { 0, 20_000, 60_000, 90_000 }, phases.Select(phase => phase.Timestamp));
        Assert.True(phases[1].IsIntermission);
        Assert.True(phases[2].IsIntermission);
        Assert.Equal(3, phases[3].NotePhaseId);
    }

    [Theory]
    [InlineData(3470, false)]
    [InlineData(3455, true)]
    public void OtherEncountersAndIncompleteCastsKeepObservedPhases(int encounterId, bool complete)
    {
        TimelinePhase[] phases = [new(1, 0), new(2, 20_000)];
        Assert.Same(phases, TimelineCorrections.CorrectPhases(encounterId, phases, [], 100_000, complete));
    }

    [Fact]
    public void AbsentCastsAndTransitionsDoNotInventIntermissions()
    {
        Assert.Equal(new TimelinePhase(1, 0, NotePhaseId: 1),
            Assert.Single(TimelineCorrections.CorrectPhases(3470, [], [], 100_000, true)));
    }
}
