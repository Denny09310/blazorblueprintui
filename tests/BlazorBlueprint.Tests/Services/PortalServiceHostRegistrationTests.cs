using BlazorBlueprint.Primitives.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorBlueprint.Tests.Services;

/// <summary>
/// Host registration is counted, not flagged (#545).
/// <para>
/// Two hosts are alive at the same time more often than the old "only the first one registers"
/// rule assumed. <c>BbPortalHost</c> is itself two category hosts, and Blazor initialises an
/// incoming page before it disposes the outgoing layout, so a layout swap overlaps them. Under a
/// boolean flag whichever host disposed first cleared it for the host still rendering, and every
/// portal registered afterwards logged "no &lt;BbPortalHost /&gt; is registered".
/// </para>
/// </summary>
public class PortalServiceHostRegistrationTests
{
    private static PortalService CreateService() =>
        new(NullLogger<PortalService>.Instance);

    [Fact]
    public void NoHostByDefault()
    {
        var service = CreateService();

        Assert.False(service.HasHost);
    }

    [Fact]
    public void OneRegistrationIsEnough()
    {
        var service = CreateService();

        service.RegisterHost();

        Assert.True(service.HasHost);
    }

    [Fact]
    public void SecondHostDisposingDoesNotClearTheFirst()
    {
        // BbPortalHost composes a Container host and an Overlay host. Both register; one going
        // away must not take the other's registration with it.
        var service = CreateService();
        service.RegisterHost();
        service.RegisterHost();

        service.UnregisterHost();

        Assert.True(service.HasHost);
    }

    [Fact]
    public void OutgoingHostDisposingAfterTheIncomingOneRegistersLeavesAHost()
    {
        // The layout-swap order from #545: the new page's host initialises, then the old layout's
        // host disposes. The surviving host must still be registered.
        var service = CreateService();
        service.RegisterHost();   // outgoing layout's host
        service.RegisterHost();   // incoming page's host
        service.UnregisterHost(); // outgoing layout disposes

        Assert.True(service.HasHost);
    }

    [Fact]
    public void LastHostLeavingClearsRegistration()
    {
        var service = CreateService();
        service.RegisterHost();
        service.RegisterHost();

        service.UnregisterHost();
        service.UnregisterHost();

        Assert.False(service.HasHost);
    }

    [Fact]
    public void UnbalancedUnregisterDoesNotPoisonALaterRegistration()
    {
        // A dispose without a matching register must not drive the count negative, or the next
        // real host would register into a deficit and still report itself missing.
        var service = CreateService();

        service.UnregisterHost();
        service.UnregisterHost();
        service.RegisterHost();

        Assert.True(service.HasHost);
    }
}
