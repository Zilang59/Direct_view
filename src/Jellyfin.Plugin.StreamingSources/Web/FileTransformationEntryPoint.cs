using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StreamingSources.Web;

/// <summary>
/// Registers a Jellyfin Web transformation when the optional File Transformation plugin is installed.
/// </summary>
public sealed class FileTransformationEntryPoint : IHostedService
{
    private readonly ILogger<FileTransformationEntryPoint> _logger;

    public FileTransformationEntryPoint(ILogger<FileTransformationEntryPoint> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 10; attempt++)
        {
            if (TryRegisterTransformation())
            {
                return;
            }

            if (attempt < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogWarning("File Transformation plugin was not found after retrying. Streaming Sources web button will not be injected automatically.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private bool TryRegisterTransformation()
    {
        try
        {
            Assembly? fileTransformationAssembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(assembly => assembly.FullName?.Contains(".FileTransformation", StringComparison.OrdinalIgnoreCase) == true);

            if (fileTransformationAssembly is null)
            {
                return false;
            }

            Type? pluginInterfaceType = fileTransformationAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            MethodInfo? registerMethod = pluginInterfaceType?.GetMethod("RegisterTransformation", BindingFlags.Public | BindingFlags.Static);

            if (registerMethod is null)
            {
                _logger.LogWarning("File Transformation RegisterTransformation method was not found.");
                return true;
            }

            var payload = new
            {
                id = Guid.Parse("6e2be09d-745f-4a25-bcb3-3ca2ab309454"),
                fileNamePattern = "index.html",
                callbackAssembly = typeof(WebIndexTransformer).Assembly.FullName,
                callbackClass = typeof(WebIndexTransformer).FullName,
                callbackMethod = nameof(WebIndexTransformer.InjectClientScript)
            };

            registerMethod.Invoke(null, new object?[] { payload });
            _logger.LogInformation("Registered Streaming Sources web button injection with File Transformation.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Streaming Sources web transformation.");
            return true;
        }
    }
}
