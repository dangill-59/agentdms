using Microsoft.AspNetCore.SignalR;
using AgentDMS.Core.Models;

namespace AgentDMS.Web.Hubs;

/// <summary>
/// SignalR hub for broadcasting real-time progress updates
/// </summary>
public class ProgressHub : Hub
{
    /// <summary>
    /// Join a job group to receive progress updates for that job
    /// </summary>
    public async Task JoinJob(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job_{jobId}");
    }

    /// <summary>
    /// Leave a job group to stop receiving progress updates
    /// </summary>
    public async Task LeaveJob(string jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job_{jobId}");
    }
}

/// <summary>
/// Service for broadcasting progress updates via SignalR
/// </summary>
public interface IProgressBroadcaster
{
    Task BroadcastProgress(string jobId, ProgressReport progress);
}

/// <summary>
/// Implementation of progress broadcaster using SignalR
/// </summary>
public class SignalRProgressBroadcaster : IProgressBroadcaster
{
    private readonly IHubContext<ProgressHub> _hubContext;

    public SignalRProgressBroadcaster(IHubContext<ProgressHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastProgress(string jobId, ProgressReport progress)
    {
        await _hubContext.Clients.Group($"job_{jobId}").SendAsync("ProgressUpdate", progress);
    }
}