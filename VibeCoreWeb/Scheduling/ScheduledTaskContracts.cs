using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace VibeCore.Scheduling;

public interface IScheduledTaskHandler
{
    Task ExecuteAsync(ScheduledTaskExecutionContext context, CancellationToken cancellationToken);
}

public sealed record ScheduledTaskExecutionContext(
    Guid ScheduleId,
    Guid RunId,
    int Attempt,
    DateTimeOffset ScheduledAt);

public sealed record ScheduledTaskHandlerOptions(
    IReadOnlyList<TimeSpan>? RetryDelays = null,
    bool AllowConcurrentExecution = false)
{
    public static ScheduledTaskHandlerOptions Default { get; } = new(
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)]);
}

public sealed record ScheduledTaskDescriptor(
    string Key,
    string DisplayName,
    string Description,
    Type HandlerType,
    ScheduledTaskHandlerOptions Options);

public interface IScheduledTaskRegistry
{
    IReadOnlyCollection<ScheduledTaskDescriptor> Handlers { get; }
    bool TryGet(string key, out ScheduledTaskDescriptor descriptor);
}

public sealed class ScheduledTaskRegistry : IScheduledTaskRegistry
{
    private readonly ConcurrentDictionary<string, ScheduledTaskDescriptor> handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ScheduledTaskDescriptor> Handlers =>
        handlers.Values.OrderBy(handler => handler.DisplayName).ToArray();

    internal void Add(ScheduledTaskDescriptor descriptor)
    {
        if (!Regex.IsMatch(descriptor.Key, "^[a-z][a-z0-9-]{2,99}$"))
            throw new InvalidOperationException(
                $"Scheduled task key '{descriptor.Key}' must contain 3-100 lowercase letters, numbers, or hyphens and start with a letter.");
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName) ||
            string.IsNullOrWhiteSpace(descriptor.Description))
            throw new InvalidOperationException("Scheduled task display names and descriptions are required.");
        if (!handlers.TryAdd(descriptor.Key, descriptor))
            throw new InvalidOperationException($"Scheduled task key '{descriptor.Key}' is already registered.");
    }

    public bool TryGet(string key, out ScheduledTaskDescriptor descriptor) =>
        handlers.TryGetValue(key, out descriptor!);
}

public static class ScheduledTaskServiceCollectionExtensions
{
    public static IServiceCollection AddScheduledTasks(this IServiceCollection services)
    {
        var registry = services.FirstOrDefault(service =>
                service.ServiceType == typeof(ScheduledTaskRegistry))
            ?.ImplementationInstance as ScheduledTaskRegistry;
        if (registry is not null)
            return services;

        registry = new ScheduledTaskRegistry();
        services.AddSingleton(registry);
        services.AddSingleton<IScheduledTaskRegistry>(registry);
        return services;
    }

    public static IServiceCollection AddScheduledTask<THandler>(
        this IServiceCollection services,
        string key,
        string displayName,
        string description,
        ScheduledTaskHandlerOptions? options = null)
        where THandler : class, IScheduledTaskHandler
    {
        var handlerOptions = options ?? ScheduledTaskHandlerOptions.Default;
        if (handlerOptions.RetryDelays?.Any(delay => delay <= TimeSpan.Zero) == true)
            throw new InvalidOperationException("Scheduled task retry delays must be positive.");

        services.AddScheduledTasks();
        var registry = services.First(service =>
                service.ServiceType == typeof(ScheduledTaskRegistry))
            .ImplementationInstance as ScheduledTaskRegistry
            ?? throw new InvalidOperationException("Scheduled task registry was not initialized.");

        registry.Add(new ScheduledTaskDescriptor(
            key,
            displayName.Trim(),
            description.Trim(),
            typeof(THandler),
            handlerOptions));
        services.AddScoped<THandler>();
        return services;
    }
}
