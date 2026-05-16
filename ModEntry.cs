using StardewModdingAPI;
using StardewModdingAPI.Events;
using System.Collections.Concurrent;

namespace StardewMCP;

public class ModEntry : Mod
{
    private McpServer? _server;

    // Background threads enqueue work here; the game thread drains it each tick.
    private static readonly ConcurrentQueue<Action> _gameThreadQueue = new();

    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var config = Helper.ReadConfig<ModConfig>();
        _server = new McpServer(Monitor, config.Port);
        _server.Start();
        Monitor.Log($"StardewMCP listening on http://localhost:{config.Port}", LogLevel.Info);
        Monitor.Log($"Add to your MCP client config: {{ \"stardew\": {{ \"url\": \"http://localhost:{config.Port}\" }} }}", LogLevel.Info);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        while (_gameThreadQueue.TryDequeue(out var action))
            action();
    }

    /// <summary>
    /// Schedule a value-returning function to run on the game thread.
    /// Safe to call from any background thread (e.g. the MCP HTTP handler).
    /// </summary>
    internal static Task<T> OnGameThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gameThreadQueue.Enqueue(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Schedule a fire-and-forget action to run on the game thread.
    /// </summary>
    internal static Task OnGameThread(Action action)
        => OnGameThread<bool>(() => { action(); return true; });
}
