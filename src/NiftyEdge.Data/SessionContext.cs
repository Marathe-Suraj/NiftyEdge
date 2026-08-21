namespace NiftyEdge.Data;

/// <summary>
/// NiftyEdge is single-user in v1. Every stored procedure still requires the workspace-mandated
/// @CompanyID/@UserID parameters, reserved here for future multi-user support.
/// </summary>
public static class SessionContext
{
    public const int CompanyId = 1;
    public const int UserId = 1;
}
