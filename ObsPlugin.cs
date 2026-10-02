using System.Text.Json;
using YStreamUtils.SDK.Plugin;
using Microsoft.Extensions.Logging;
using OBSWebsocketDotNet;

[assembly: PluginManifest(
    "OBS-Plugin", 
    "v1.0.0", 
    "The official YStreamUtils OBS Integrations",
    "YStreamUtils",
    "OBS-Plugin",
    ["network"],
    ["ysnt-yes"],
    typeof(OBSPlugin.ObsPluginSettings))]

namespace OBSPlugin;

public record struct ObsPluginSettings(string? ObsUrl = "ws://localhost:4455", string? Password = null);

public class ObsPlugin(ILogger<ObsPlugin> logger) : IPlugin
{
    private OBSWebsocket _obsWebsocket = new();
    private ObsPluginSettings _settings;

    public Task SettingsUpdated(string newSettings)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<ObsPluginSettings>(newSettings);
            Initialize(settings);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private void Initialize(ObsPluginSettings settings)
    {
        _settings = settings;

        try
        {
            _obsWebsocket.Connected -= OnObsConnected;
            _obsWebsocket.Disconnected -= OnObsDisconnected;

            if (_obsWebsocket.IsConnected)
            {
                logger.LogInformation("Disconnecting old OBS instance...");
                _obsWebsocket.Disconnect();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while disconnecting old OBS instance. Continuing initialization.");
        }

        _obsWebsocket = new OBSWebsocket();

        _obsWebsocket.Connected += OnObsConnected;
        _obsWebsocket.Disconnected += OnObsDisconnected;

        try
        {
            logger.LogInformation("Initiating connection to OBS Engine at: {Url}...", _settings.ObsUrl);
            
            _obsWebsocket.ConnectAsync(_settings.ObsUrl ?? "ws://localhost:4455", _settings.Password ?? string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start OBS WebSocket connection sequence.");
        }
    }

    private void OnObsConnected(object? sender, EventArgs e)
    {
        logger.LogInformation("OBS Engine successfully connected to: {Url}", _settings.ObsUrl);
    }

    private void OnObsDisconnected(object? sender, OBSWebsocketDotNet.Communication.ObsDisconnectionInfo e)
    {
        if (e.DisconnectReason != null)
        {
            logger.LogError("OBS disconnected or failed to connect. Reason: {Reason}", e.DisconnectReason);
        }
        else
        {
            logger.LogWarning("OBS WebSocket connection closed cleanly.");
        }
    }

    public List<string> GetScenes()
    {
        if (!_obsWebsocket.IsConnected)
        {
            logger.LogWarning("Cannot fetch scenes: OBS is disconnected.");
            return [];
        }

        try
        {
            var basicList = _obsWebsocket.GetSceneList();
            return [.. basicList.Scenes.Select(s => s.Name)];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred fetching OBS scene collections.");
        }

        return [];
    }

    public void ChangeScene(string sceneName)
    {
        if (!_obsWebsocket.IsConnected) return;

        try
        {
            _obsWebsocket.SetCurrentProgramScene(sceneName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed executing Scene Transition to: {Scene}", sceneName);
        }
    }

    public void StartRecording()
    {
        if (!_obsWebsocket.IsConnected) return;

        try
        {
            _obsWebsocket.StartRecord();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to command OBS recording stream start.");
        }
    }

    public void StopRecording()
    {
        if (!_obsWebsocket.IsConnected) return;

        try
        {
            _obsWebsocket.StopRecord();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to command OBS recording stream stop.");
        }
    }
}
