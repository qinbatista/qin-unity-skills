using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Qin.UnitySamples.JobStateBatches
{
    public sealed class FireballVolleyManager : MonoBehaviour
    {
        private const int FireballCount = 5;
        private bool _flying;
        private TransformAccessArray _actors;
        private NativeArray<FireballVolleyState> _states;
        private FireballVolleyJob _job;
        private bool _started;

        public event Action<NativeArray<FireballVolleyState>.ReadOnly> StateCompleted = delegate { };

        private void FixedUpdate()
        {
            if (!_flying) return;
            _job.DeltaTime = Time.fixedDeltaTime;
            _job.Schedule(_actors).Complete();
            _flying = _states[0].RemainingDistance > 0f;
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

        public void Fire(FireballVolleyController prefab, Transform muzzle, float speed, float range, float angleStep)
        {
            if (_started) throw new InvalidOperationException("The scene already owns a batch.");
            if (prefab.gameObject.activeSelf) throw new ArgumentException("Save the actor prefab inactive.", nameof(prefab));
            _actors = new TransformAccessArray(FireballCount);
            _states = new NativeArray<FireballVolleyState>(FireballCount, Allocator.Persistent);
            _started = true;
            _flying = true;
            _job = new FireballVolleyJob { States = _states, Forward = muzzle.right, Speed = speed };

            for (int index = 0; index < _states.Length; index++)
            {
                FireballVolleyController actor = Instantiate(prefab, muzzle.position, Quaternion.identity, transform);
                _actors.Add(actor.transform);
                _states[index] = new FireballVolleyState { Angle = (index - FireballCount / 2) * angleStep, RemainingDistance = range };
                actor.Bind(this, index);
                actor.gameObject.SetActive(true);
            }
        }

        public FireballVolleyState ReadState(int slot) => _states[slot];}
}
