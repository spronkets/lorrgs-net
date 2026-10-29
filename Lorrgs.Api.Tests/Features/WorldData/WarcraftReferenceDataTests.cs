using Lorrgs.Api.Features.WorldData;

namespace Lorrgs.Api.Tests.Features.WorldData;

public sealed class WarcraftReferenceDataTests
{
    [Fact]
    public void AdapterPreservesCatalogIdentitiesUsedByWclEnrichment()
    {
        var source = WorldDataService.Instance;
        var adapter = new WarcraftReferenceData(source);
        Assert.Equal(source.Classes.Count, adapter.Classes.Count);
        Assert.Equal(source.Specs.Count, adapter.Specs.Count);
        Assert.Equal(source.Roles.Count, adapter.Roles.Count);
        foreach (var item in source.Classes)
        {
            var mapped = Assert.Single(adapter.Classes, value => value.Id == item.Id);
            Assert.Equal((item.Name, item.NameSlug), (mapped.Name, mapped.NameSlug));
            Assert.Same(mapped, adapter.GetClass(item.Id));
        }
        foreach (var item in source.Specs)
        {
            var mapped = Assert.Single(adapter.Specs, value => value.FullNameSlug == item.FullNameSlug);
            Assert.Equal((item.Name, item.NameSlug, item.FullNameSlug, item.ClassId, item.RoleId),
                (mapped.Name, mapped.NameSlug, mapped.FullNameSlug, mapped.ClassId, mapped.RoleId));
            Assert.Same(mapped, adapter.GetSpec(item.FullNameSlug));
        }
        foreach (var item in source.Roles)
            Assert.Equal(item.Code, Assert.Single(adapter.Roles, value => value.Id == item.Id).Code);
        foreach (var item in source.Bosses)
        {
            var mapped = adapter.GetBoss(item.NameSlug);
            Assert.NotNull(mapped);
            Assert.Equal((item.Id, item.Name, item.NameSlug, item.RaidId, item.Icon),
                (mapped.Id, mapped.Name, mapped.NameSlug, mapped.RaidId, mapped.Icon));
        }
        Assert.Null(adapter.GetClass(-1));
        Assert.Null(adapter.GetSpec("missing"));
        Assert.Null(adapter.GetBoss("missing"));
    }

    [Fact]
    public void ApiAssemblyNoLongerOwnsGraphQlResources()
    {
        Assert.DoesNotContain(typeof(ApiConfiguration).Assembly.GetManifestResourceNames(),
            name => name.EndsWith(".graphql", StringComparison.Ordinal));
    }
}
