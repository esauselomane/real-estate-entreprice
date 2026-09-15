using Identity.Microservice.Controllers;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class AgencyTenantScopeTests
{
    [Fact]
    public void AgencyWithoutChildrenRetainsAccessToItsOwnTenant()
    {
        Assert.Equal(new[] { "root" }, AgencyTenantScope.GetSubtree("root", new Dictionary<string, List<string>>()));
    }

    [Fact]
    public void IncludesDescendantsOnceAndExcludesUnrelatedAgencies()
    {
        var hierarchy = new Dictionary<string, List<string>>
        {
            ["root"] = ["child"], ["child"] = ["grandchild", "root"], ["unrelated"] = ["other"]
        };
        var scope = AgencyTenantScope.GetSubtree("root", hierarchy);
        Assert.Equal(3, scope.Count);
        Assert.Contains("root", scope);
        Assert.Contains("child", scope);
        Assert.Contains("grandchild", scope);
        Assert.DoesNotContain("other", scope);
    }
}
