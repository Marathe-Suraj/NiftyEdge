using NiftyEdge.Core.Models;

namespace NiftyEdge.Web.Models;

public static class SignalStatusBadge
{
    public static string CssClass(SignalStatus status) => status switch
    {
        SignalStatus.Target1Hit or SignalStatus.Target2Hit => "text-bg-success",
        SignalStatus.StopHit => "text-bg-danger",
        SignalStatus.Open => "bg-info-subtle text-info-emphasis border border-info-subtle",
        _ => "text-bg-secondary"
    };
}
