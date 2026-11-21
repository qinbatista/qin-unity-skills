using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Qin.UnitySamples.JobStateBatches
{
    public sealed class CoinMagnetManager : MonoBehaviour
    {
        private Transform _collector;
        private TransformAccessArray _actors;
        private NativeArray<CoinMagnetState> _states;
        private CoinMagnetJob _job;
        private bool _started;

        public event Action<NativeArray<CoinMagnetState>.ReadOnly> StateCompleted = delegate { };

        private void FixedUpdate()
        {
            if (!_started) return;
            _job.CollectorPosition = _collector.position;
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

        public void Spawn(CoinMagnetController prefab, Transform collector, Transform[] points, float radius, float speed)
        {
            if (_started) throw new InvalidOperationException("The scene already owns a batch.");
            if (prefab.gameObject.activeSelf) throw new ArgumentException("Save the actor prefab inactive.", nameof(prefab));
            _actors = new TransformAccessArray(points.Length);
            _states = new NativeArray<CoinMagnetState>(points.Length, Allocator.Persistent);
            _started = true;
            _collector = collector;
            _job = new CoinMagnetJob { States = _states, Radius = radius, Speed = speed };

            for (int index = 0; index < _states.Length; index++)
            {
                CoinMagnetController actor = Instantiate(prefab, points[index].position, Quaternion.identity, transform);
                _actors.Add(actor.transform);
                actor.Bind(this, index);
                actor.gameObject.SetActive(true);
            }
        }
    }
}
