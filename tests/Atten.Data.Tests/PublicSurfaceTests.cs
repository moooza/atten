using System.Reflection;
using Atten.Core;
using Xunit;

namespace Atten.Data.Tests;

public class PublicSurfaceTests
{
    [Fact]
    public void PublicDataTypesDoNotExposeSqlite()
    {
        foreach (Type type in typeof(AttenStore).Assembly.GetExportedTypes())
        {
            Assert.DoesNotContain("Sqlite", type.Name, StringComparison.OrdinalIgnoreCase);
            AssertNoSqlite(type);
            foreach (Type nested in type.GetNestedTypes(BindingFlags.Public))
            {
                AssertNoSqlite(nested);
            }
        }
    }

    [Fact]
    public void CoreDoesNotReferenceDataOrSqlite()
    {
        string[] names = typeof(Dates).Assembly.GetReferencedAssemblies().Select(item => item.Name ?? "").ToArray();
        Assert.DoesNotContain("Atten.Data", names);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", names);
        Assert.DoesNotContain("Microsoft.Data.Sqlite.Core", names);
    }

    [Fact]
    public void StoreIsOpenedBehindInterfaces()
    {
        using var db = new TempDatabase();
        IAttenRepository store = db.OpenStore();

        Assert.IsAssignableFrom<IPersonnelRepository>(store.Personnel);
        Assert.IsAssignableFrom<ILeaveRepository>(store.Leaves);
        Assert.IsAssignableFrom<IClockEventRepository>(store.ClockEvents);
        Assert.IsAssignableFrom<ICalculationRepository>(store.Calculation);
        Assert.IsAssignableFrom<IPayrollRepository>(store.Payroll);
        Assert.IsAssignableFrom<IUserRepository>(store.Users);
        Assert.IsAssignableFrom<IBackupService>(store.Backup);
        Assert.IsAssignableFrom<IAttenRepository>(store);
        Assert.Empty(
            typeof(IAttenRepository)
                .GetProperties()
                .Select(property => property.PropertyType)
                .Where(IsSqliteType)
                .Select(type => type.FullName));
    }

    private static void AssertNoSqlite(Type type)
    {
        foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (ParameterInfo parameter in ctor.GetParameters())
            {
                Assert.False(IsSqliteType(parameter.ParameterType), $"{type.Name} constructor exposes {parameter.ParameterType}");
            }
        }

        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Assert.False(IsSqliteType(method.ReturnType), $"{type.Name}.{method.Name} returns {method.ReturnType}");
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                Assert.False(IsSqliteType(parameter.ParameterType), $"{type.Name}.{method.Name} takes {parameter.ParameterType}");
            }
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Assert.False(IsSqliteType(property.PropertyType), $"{type.Name}.{property.Name} is {property.PropertyType}");
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            Assert.False(IsSqliteType(field.FieldType), $"{type.Name}.{field.Name} is {field.FieldType}");
        }
    }

    private static bool IsSqliteType(Type type)
    {
        Type current = Nullable.GetUnderlyingType(type) ?? type;
        if (current.IsGenericType)
        {
            if (current.GetGenericArguments().Any(IsSqliteType))
            {
                return true;
            }

            current = current.GetGenericTypeDefinition();
        }

        if (current.IsArray)
        {
            return IsSqliteType(current.GetElementType()!);
        }

        string name = current.FullName ?? current.Name;
        return name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.Data.Sqlite", StringComparison.Ordinal);
    }
}
