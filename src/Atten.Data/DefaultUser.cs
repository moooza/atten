namespace Atten.Data;

/// <summary>
/// One local user until real login exists. Migration 012 inserts this row.
/// </summary>
public static class DefaultUser
{
    public const long Id = 1;
    public const string DisplayName = "پیش‌فرض";
}
