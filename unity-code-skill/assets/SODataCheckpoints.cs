using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A shared Play-mode checkpoint for edits with no managed field-store instruction (e.g. list.Add).
internal sealed class SODataCheckpoints : MonoBehaviour
{
    private static SODataCheckpoints _instance;
    private readonly List<SODataController> _open = new List<SODataController>();
    private readonly List<SODataController> _indirect = new List<SODataController>();
    private Coroutine _periodic;

    internal static void Track(SODataController owner, bool indirect)
    {
        if (!Application.isPlaying) return;
        if (_instance == null)
        {
            GameObject host = new GameObject("SO data checkpoints") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<SODataCheckpoints>();
        }
        _instance._open.Add(owner);
        if (!indirect) return;
        _instance._indirect.Add(owner);
        if (_instance._periodic == null) _instance._periodic = _instance.StartCoroutine(_instance.CheckIndirect());
    }

    internal static void Forget(SODataController owner)
    {
        if (_instance == null) return;
        _instance._open.Remove(owner);
        _instance._indirect.Remove(owner);
        if (_instance._indirect.Count == 0 && _instance._periodic != null)
        {
            _instance.StopCoroutine(_instance._periodic);
            _instance._periodic = null;
        }
        if (_instance._open.Count != 0) return;
        var host = _instance.gameObject;
        _instance = null;
        Destroy(host);
    }

    private IEnumerator CheckIndirect()
    {
        WaitForSecondsRealtime interval = new WaitForSecondsRealtime(0.25f);
        while (true)
        {
            yield return interval;
            Check(_indirect, true);
        }
    }

    private static void Check(List<SODataController> owners, bool indirectOnly)
    {
        for (int index = owners.Count - 1; index >= 0; index--)
        {
            if (index >= owners.Count) continue;
            var owner = owners[index];
            if (owner == null) continue;
            try { owner.CheckForChanges(indirectOnly); }
            catch (Exception error) { Debug.LogException(error, owner); }
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Check(_open, false);
    }

    private void OnApplicationQuit() => Check(_open, false);
}
