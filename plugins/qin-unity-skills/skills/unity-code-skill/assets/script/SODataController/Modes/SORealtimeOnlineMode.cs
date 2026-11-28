using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ISORealtimeDataStore : ISODataKeyStore
{
    // Deliver the initial record (null for absence) and subsequent changes on the Unity thread.
    Task<IDisposable> SubscribeAsync(string scope, string key, Action<SODataRecord> receive, CancellationToken cancellation);
}

public interface ISOMatchSessionStore : ISORealtimeDataStore { }

internal sealed class SORealtimeOnlineMode : SODirectAccessOnlineMode
{
    private readonly ISORealtimeDataStore _channel;
    private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

    internal SORealtimeOnlineMode(SODataController owner, string scope, ISODataKeyStore channel)
        : base(owner, scope, channel as ISORealtimeDataStore ?? throw new ArgumentException("RealtimeOnline requires a live channel."), true)
    {
        _channel = (ISORealtimeDataStore)channel;
    }

    protected override async Task LoadAsync(SOStoredField field, CancellationToken token)
    {
        bool received = false;
        var subscription = await _channel.SubscribeAsync(Scope, field.Key, record =>
        {
            Receive(field, record, received);
            received = true;
        }, token);
        if (subscription == null) throw new InvalidOperationException("The channel returned no subscription.");
        if (Closed || !received) { subscription.Dispose(); throw new InvalidOperationException("The subscription closed before its initial state."); }
        _subscriptions.Add(subscription);
    }

    public override void Dispose()
    {
        base.Dispose();
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
    }

    internal override Task RefreshAsync() => Task.CompletedTask;
}
