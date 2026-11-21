using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Qin.UnitySamples.JobStateBatches
{
    public sealed class CompanionFormationManager : MonoBehaviour
    {
        private Transform _leader;
        private TransformAccessArray _actors;
        private NativeArray<CompanionFormationState> _states;
        private CompanionFormationJob _job;
        private bool _started;

        public event Action<NativeArray<CompanionFormationState>.ReadOnly> StateCompleted = delegate { };

        private void FixedUpdate()
        {
            if (!_started) return;
            _leader.GetPositionAndRotation(out _job.LeaderPosition, out _job.LeaderRotation);
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

        public void Spawn(CompanionFormationController prefab, Transform leader, Transform[] starts, Vector3[] offsets, float speed)
        {
            if (_started) throw new InvalidOperationException("The scene already owns a batch.");
            if (prefab.gameObject.activeSelf) throw new ArgumentException("Save the actor prefab inactive.", nameof(prefab));
            if (starts.Length != offsets.Length) throw new ArgumentException("Every companion needs one formation offset.");
            _actors = new TransformAccessArray(starts.Length);
            _states = new NativeArray<CompanionFormationState>(starts.Length, Allocator.Persistent);
            _started = true;
            _leader = leader;
            _job = new CompanionFormationJob { States = _states, Speed = speed };

            for (int index = 0; index < _states.Length; index++)
            {
                CompanionFormationController actor = Instantiate(prefab, starts[index].position, Quaternion.identity, transform);
                _actors.Add(actor.transform);
                _states[index] = new CompanionFormationState { LocalOffset = offsets[index] };
                actor.Bind(this, index);
                actor.gameObject.SetActive(true);
            }
        }
    }
}
