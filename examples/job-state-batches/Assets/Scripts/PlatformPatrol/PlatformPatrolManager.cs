using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Qin.UnitySamples.JobStateBatches
{
    public sealed class PlatformPatrolManager : MonoBehaviour
    {
        private TransformAccessArray _actors;
        private NativeArray<PlatformPatrolState> _states;
        private PlatformPatrolJob _job;
        private bool _started;

        public event Action<NativeArray<PlatformPatrolState>.ReadOnly> StateCompleted = delegate { };

        private void FixedUpdate()
        {
            if (!_started) return;
            _job.DeltaTime = Time.fixedDeltaTime;
            _job.Schedule(_actors).Complete();
            StateCompleted.Invoke(_states.AsReadOnly());
        }

        private void OnDestroy()
        {
            if (!_started) return;
            _started = false;

            for (int index = 0; index < _actors.length; index++)
            {
                Transform actor = _actors[index];
                if (actor == null) continue;
                actor.gameObject.SetActive(false);
                Destroy(actor.gameObject);
            }

            _actors.Dispose();
            _states.Dispose();
        }

        public void Spawn(PlatformPatrolController prefab, Transform[] starts, Transform[] ends, float speed, float holdDuration)
        {
            if (_started) throw new InvalidOperationException("The scene already owns a batch.");
            if (prefab.gameObject.activeSelf) throw new ArgumentException("Save the actor prefab inactive.", nameof(prefab));
            if (starts.Length != ends.Length) throw new ArgumentException("Every platform needs one destination.");
            _actors = new TransformAccessArray(starts.Length);
            _states = new NativeArray<PlatformPatrolState>(starts.Length, Allocator.Persistent);
            _started = true;
            _job = new PlatformPatrolJob { States = _states, Speed = speed, HoldDuration = holdDuration };

            for (int index = 0; index < _states.Length; index++)
            {
                PlatformPatrolController actor = Instantiate(prefab, starts[index].position, Quaternion.identity, transform);
                _actors.Add(actor.transform);
                _states[index] = new PlatformPatrolState { Start = starts[index].position, End = ends[index].position };
                actor.Bind(this, index);
                actor.gameObject.SetActive(true);
            }
        }
    }
}
