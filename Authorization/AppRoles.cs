namespace SkillBridge.Authorization;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Employer = "Employer"; // posts jobs, compares applicants, selects Hunters, approves work, records payments
    public const string Hunter = "Hunter";     // builds a profile, applies/joins challenges, submits work

    public static readonly string[] All = { Admin, Employer, Hunter };
    public static readonly string[] SelfRegistrable = { Employer, Hunter };
}
