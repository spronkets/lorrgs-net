using Lorrgs.Api.Models;

namespace Lorrgs.Api.Services;

internal static class TimelineCorrections
{
    internal static IReadOnlyList<TimelinePhase> CorrectPhases(int encounterId,
        IReadOnlyList<TimelinePhase> phases, IReadOnlyList<TimelineEvent> casts, double duration,
        bool complete)
    {
        if (encounterId != 3470 || !complete) return phases;

        var corrected = new List<TimelinePhase> { new(1, 0, NotePhaseId: 1) };
        var ritual = casts.Where(cast => cast.Kind == "cast" && cast.SpellId == 1295124)
            .OrderBy(cast => cast.Timestamp).FirstOrDefault();
        if (ritual is not null)
            corrected.Add(new(1.5, ritual.Timestamp, IsIntermission: true));
        var transfer = casts.Where(cast => cast.Kind == "cast" && cast.SpellId == 1292248)
            .OrderBy(cast => cast.Timestamp).LastOrDefault();
        if (transfer is not null)
            corrected.Add(new(1.75, Math.Max(0, transfer.Timestamp - 15_000), IsIntermission: true));
        var uncoiling = phases.Where(phase => phase.Id == 3)
            .OrderBy(phase => phase.Timestamp).LastOrDefault();
        if (uncoiling is not null)
            corrected.Add(new(2, uncoiling.Timestamp, NotePhaseId: 3));
        return corrected.Where(phase => phase.Timestamp >= 0 && phase.Timestamp <= duration)
            .OrderBy(phase => phase.Timestamp).ToArray();
    }

    internal static IReadOnlyList<TimelineEvent> CorrectEvents(
        IReadOnlyList<TimelineEvent> events, int? specId, string edition, bool complete)
    {
        if (!string.Equals(edition, "retail", StringComparison.OrdinalIgnoreCase)) return events;
        var result = events.OrderBy(item => item.Timestamp).ToList();
        if (specId is 254 or 105)
        {
            var spellId = specId == 254 ? 212431 : 33891;
            var window = specId == 254 ? 10_000 : 120_000;
            double? previous = null;
            result = result.Where(item =>
            {
                if (item.Kind != "cast" || item.SpellId != spellId) return true;
                if (previous is not null && item.Timestamp - previous.Value < window) return false;
                previous = item.Timestamp;
                return true;
            }).ToList();
        }
        else if (specId == 264)
        {
            // An interval clipped by fight end is not evidence of a short talent proc.
            result = result.Select(item => item.Kind == "buff" && item.SpellId == 114052 &&
                    item.Duration is > 0 and < 13_000 && !item.IsInferred
                ? item with { SpellId = 378270 } : item).ToList();
        }
        else if (specId == 1468)
        {
            TimelineEvent? charge = null;
            var releases = new List<double>();
            var additions = new List<TimelineEvent>();
            for (var index = 0; index < result.Count; index++)
            {
                var item = result[index];
                if (item.Kind != "cast") continue;
                if (item.SpellId == 370537) charge = item;
                else if (item.SpellId == 370564)
                {
                    releases.Add(item.Timestamp);
                    if (charge is not null)
                    {
                        var chargeIndex = result.IndexOf(charge);
                        result[chargeIndex] = charge with { Duration = item.Timestamp - charge.Timestamp };
                        charge = null;
                    }
                    else if (complete)
                        additions.Add(new(370537, item.Timestamp - 30_000, 30_000, "buff", true));
                }
            }
            result.RemoveAll(item => item.Kind == "cast" && item.SpellId == 355936 &&
                releases.Any(release => item.Timestamp >= release && item.Timestamp - release < 1_000));
            result.AddRange(additions);
        }
        else if (specId == 1480 && complete)
        {
            result.AddRange(events.Where(item => item.Kind == "buff" && item.SpellId == 1217607 &&
                    item.Duration is > 0 && !item.IsInferred)
                .Select(item => new TimelineEvent(267134, item.Timestamp + item.Duration!.Value,
                    0, "marker", true)));
        }
        return result.OrderBy(item => item.Timestamp).ThenBy(item => item.SpellId).ToArray();
    }

    internal static IReadOnlyList<TimelineSpell> CorrectSpells(IReadOnlyList<TimelineSpell> spells,
        IReadOnlyList<TimelineEvent> events)
    {
        var result = spells.ToDictionary(spell => spell.Id);
        if (events.Any(item => item.SpellId == 370537 && item.IsInferred) &&
            (!result.TryGetValue(370537, out var stasis) || stasis.Name == "Spell 370537"))
            result[370537] = new(370537, "Stasis", "ability_evoker_stasis.jpg");
        if (events.Any(item => item.SpellId == 378270))
            result[378270] = new(378270, "Deeply Rooted Elements", "inv_misc_herb_liferoot_stem.jpg");
        if (events.Any(item => item.SpellId == 267134 && item.IsInferred))
            result[267134] = new(267134, "Drop Void Meta", "inv_112_ability_demonhunter_metamorphasisvoid.jpg");
        return result.Values.OrderBy(spell => spell.Name).ToArray();
    }
}
