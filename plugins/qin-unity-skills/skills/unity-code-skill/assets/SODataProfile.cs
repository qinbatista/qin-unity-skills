using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Storage Profile/Local or Custom")]
public class SODataProfile : ScriptableObject
{
    [SerializeField, Tooltip("The same data assets referenced by gameplay for this profile.")]
    private SODataController[] _domains = Array.Empty<SODataController>();

    private readonly List<SODataController> _opened = new List<SODataController>();
    private CancellationTokenSource _opening;
    private bool _active;
    private bool _transition;

    protected virtual ISODataKeyStore CreateCloudStore() =>
        throw new InvalidOperationException("Select a cloud profile or supply a persistent cloud provider at sign-in.");

    public async Task EnterAsync(string slot, ISODataKeyStore cloud = null, ISORealtimeDataStore realtime = null,
        ISOLocalDataStore local = null, CancellationToken cancellation = default)
    {
        if (_active || _transition) throw new InvalidOperationException("Leave the current data profile before entering another.");
        if (string.IsNullOrWhiteSpace(slot)) throw new ArgumentException("A saved slot or match ID is required.", nameof(slot));
        cancellation.ThrowIfCancellationRequested();
        using CancellationTokenSource opening = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _opening = opening;
        _transition = true;
        try
        {
            foreach (SODataController data in _domains)
            {
                if (data == null) throw new InvalidOperationException("A profile data reference is missing.");
                SODataMode mode = data.Mode;
                if (mode == SODataMode.None) continue;
                if (SODataWriteHooks.IsBound(data)) throw new InvalidOperationException($"{data.name} already belongs to an open profile.");
                if (mode == SODataMode.DirectAccessOnline && cloud == null) cloud = CreateCloudStore();
                ISODataKeyStore store = mode switch
                {
                    SODataMode.DirectAccessOnline => cloud,
                    SODataMode.RealtimeOnline => realtime,
                    SODataMode.DirectAccess => local,
                    _ => null
                };
                await data.OpenAsync(slot, store, opening.Token);
                _opened.Add(data);
                opening.Token.ThrowIfCancellationRequested();
            }
            _active = true;
        }
        catch
        {
            ReleaseDomains();
            throw;
        }
        finally { _opening = null; _transition = false; }
    }

    public async Task LeaveAsync()
    {
        if (_transition) throw new InvalidOperationException("A data profile transition is already running.");
        if (!_active) return;
        _transition = true;
        try
        {
            Task[] receipts = new Task[_opened.Count];
            for (int index = 0; index < _opened.Count; index++)
            {
                SODataController data = _opened[index];
                receipts[index] = data == null ? Task.CompletedTask : data.CloseAsync();
            }
            await Task.WhenAll(receipts);
        }
        finally { _opened.Clear(); _active = false; _transition = false; }
    }

    protected virtual void OnDisable()
    {
        _opening?.Cancel();
        if (_opening != null || !_transition) ReleaseDomains();
    }

    private void ReleaseDomains()
    {
        foreach (SODataController data in _opened)
        {
            if (data == null) continue;
            try { data.CloseData(); }
            catch (Exception error) { Debug.LogException(error, data); }
        }
        _opened.Clear();
        _active = false;
    }
}
