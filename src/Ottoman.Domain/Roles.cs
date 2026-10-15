namespace Ottoman.Domain;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Employee = "Employee";
    public const string Client = "Client";

    public static readonly string[] All = [Admin, Employee, Client];
}
