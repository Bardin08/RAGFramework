using System.Reflection;
using Microsoft.Extensions.Logging;

namespace RAG.Evaluation.Plugins;

/// <summary>
/// Service for discovering and loading evaluation plugins from assemblies.
/// Scans the plugins/evaluations/ directory for DLL files containing IEvaluationPlugin implementations.
/// </summary>
public class PluginLoaderService
{
    private readonly ILogger<PluginLoaderService> _logger;
    private readonly string _pluginDirectory;
    private readonly Dictionary<string, IEvaluationPlugin> _plugins = new();

    public PluginLoaderService(
        ILogger<PluginLoaderService> logger,
        string? pluginDirectory = null)
    {
        _logger = logger;
        _pluginDirectory = pluginDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "evaluations");
    }

    /// <summary>
    /// Gets all loaded plugins.
    /// </summary>
    public IReadOnlyDictionary<string, IEvaluationPlugin> Plugins => _plugins;

    /// <summary>
    /// Loads all plugins from the plugin directory.
    /// </summary>
    public async Task LoadPluginsAsync()
    {
        _logger.LogInformation("Loading evaluation plugins from: {PluginDirectory}", _pluginDirectory);

        if (!Directory.Exists(_pluginDirectory))
        {
            _logger.LogWarning("Plugin directory does not exist: {PluginDirectory}. Creating it.", _pluginDirectory);
            Directory.CreateDirectory(_pluginDirectory);
            return;
        }

        var dllFiles = Directory.GetFiles(_pluginDirectory, "*.dll", SearchOption.AllDirectories);
        _logger.LogInformation("Found {Count} DLL files in plugin directory", dllFiles.Length);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                await LoadPluginFromAssemblyAsync(dllPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {DllPath}", dllPath);
            }
        }

        _logger.LogInformation("Loaded {Count} evaluation plugins", _plugins.Count);
    }

    /// <summary>
    /// Loads plugins from a specific assembly file.
    /// </summary>
    private async Task LoadPluginFromAssemblyAsync(string assemblyPath)
    {
        _logger.LogDebug("Attempting to load assembly: {AssemblyPath}", assemblyPath);

        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(assemblyPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load assembly from {AssemblyPath}", assemblyPath);
            return;
        }

        var pluginType = typeof(IEvaluationPlugin);
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && pluginType.IsAssignableFrom(t))
            .ToList();

        if (types.Count == 0)
        {
            _logger.LogDebug("No IEvaluationPlugin implementations found in {AssemblyPath}", assemblyPath);
            return;
        }

        foreach (var type in types)
        {
            try
            {
                var plugin = Activator.CreateInstance(type) as IEvaluationPlugin;
                if (plugin == null)
                {
                    _logger.LogWarning("Failed to create instance of {TypeName} from {AssemblyPath}",
                        type.FullName, assemblyPath);
                    continue;
                }

                if (_plugins.ContainsKey(plugin.Name))
                {
                    _logger.LogWarning(
                        "Plugin with name '{PluginName}' already loaded. Skipping duplicate from {AssemblyPath}",
                        plugin.Name, assemblyPath);
                    continue;
                }

                _plugins[plugin.Name] = plugin;
                _logger.LogInformation(
                    "Loaded plugin: {PluginName} v{Version} from {AssemblyPath}",
                    plugin.Name, plugin.Version, assemblyPath);

                await Task.CompletedTask; // For async consistency
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to instantiate plugin type {TypeName} from {AssemblyPath}",
                    type.FullName, assemblyPath);
            }
        }
    }

    /// <summary>
    /// Gets a plugin by name.
    /// </summary>
    public IEvaluationPlugin? GetPlugin(string name)
    {
        return _plugins.TryGetValue(name, out var plugin) ? plugin : null;
    }

    /// <summary>
    /// Checks if a plugin with the given name is loaded.
    /// </summary>
    public bool HasPlugin(string name)
    {
        return _plugins.ContainsKey(name);
    }

    /// <summary>
    /// Reloads all plugins (clears and reloads).
    /// </summary>
    public async Task ReloadPluginsAsync()
    {
        _plugins.Clear();
        await LoadPluginsAsync();
    }
}
