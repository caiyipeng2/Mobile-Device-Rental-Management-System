using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace DeviceRental.Testing;

public static class InfrastructureDbContextFactory
{
    private const string ContextTypeName =
        "DeviceRental.Infrastructure.Persistence.DeviceRentalDbContext";

    public static DbContext Create(string connectionString)
    {
        var assembly = LoadInfrastructureAssembly();
        var contextType = assembly.GetType(ContextTypeName, throwOnError: false);
        if (contextType is null)
        {
            throw new InvalidOperationException(
                $"Expected database context '{ContextTypeName}' is missing. " +
                "This is the intentional RED condition until the persistence model is implemented.");
        }

        if (!typeof(DbContext).IsAssignableFrom(contextType))
        {
            throw new InvalidOperationException($"{ContextTypeName} must derive from DbContext.");
        }

        var genericBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType);
        var optionsBuilder = (DbContextOptionsBuilder)(Activator.CreateInstance(genericBuilderType)
            ?? throw new InvalidOperationException($"Could not create options for {ContextTypeName}."));
        optionsBuilder.UseNpgsql(connectionString, options =>
        {
            options.MigrationsAssembly(assembly.GetName().Name);
            options.MigrationsHistoryTable("__EFMigrationsHistory", "device_rental");
        });

        var expectedOptionsType = typeof(DbContextOptions<>).MakeGenericType(contextType);
        var options = genericBuilderType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(property =>
                property.Name == nameof(DbContextOptionsBuilder.Options) &&
                property.PropertyType == expectedOptionsType)
            .GetValue(optionsBuilder)
            ?? throw new InvalidOperationException($"Could not build options for {ContextTypeName}.");
        var constructor = contextType.GetConstructors()
            .SingleOrDefault(candidate =>
            {
                var parameters = candidate.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(options);
            });
        if (constructor is null)
        {
            throw new InvalidOperationException(
                $"{ContextTypeName} must expose a public constructor accepting " +
                $"DbContextOptions<{contextType.Name}>.");
        }

        return (DbContext)constructor.Invoke([options]);
    }

    public static Assembly LoadInfrastructureAssembly()
    {
        try
        {
            return Assembly.Load("DeviceRental.Infrastructure");
        }
        catch (FileNotFoundException)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "DeviceRental.Infrastructure.dll");
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    "DeviceRental.Infrastructure.dll was not copied to the database test output.");
            }

            return Assembly.LoadFrom(path);
        }
    }
}
