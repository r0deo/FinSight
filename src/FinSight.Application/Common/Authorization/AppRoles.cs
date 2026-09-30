namespace FinSight.Application.Common.Authorization;

public static class AppRoles
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";

    public static readonly string[] All = [Owner, Manager];
}
