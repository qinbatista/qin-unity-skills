using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Qin.UnitySamples.JobStateBatches
{
    public sealed class EnemyChaseManager : MonoBehaviour
    {
        private Collider2D _player;
        private int _alive;
        private TransformAccessArray _actors;
        private NativeArray<EnemyChaseState> _states;
        private EnemyChaseJob _job;
        private bool _started;

        public event Action<NativeArray<EnemyChaseState>.ReadOnly> StateCompleted = delegate { };

        private void FixedUpdate()
        {
            if (_alive == 0) return;
            _job.PlayerPosition = _player.bounds.center;
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

        public void Spawn(EnemyChaseController prefab, Collider2D player, Transform[] points, float speed)
        {
            if (_started) throw new InvalidOperationException("The scene already owns a batch.");
            if (prefab.gameObject.activeSelf) throw new ArgumentException("Save the actor prefab inactive.", nameof(prefab));
            _actors = new TransformAccessArray(points.Length);
            _states = new NativeArray<EnemyChaseState>(points.Length, Allocator.Persistent);
            _started = true;
            _player = player;
            _alive = points.Length;
            _job = new EnemyChaseJob { States = _states, Speed = speed };

            for (int index = 0; index < _states.Length; index++)
            {
                EnemyChaseController actor = Instantiate(prefab, points[index].position, Quaternion.identity, transform);
                _actors.Add(actor.transform);
                _states[index] = new EnemyChaseState { IsAlive = true };
                actor.Bind(this, index);
                actor.gameObject.SetActive(true);
            }
        }

        public void HitPlayer(int slot, Collider2D other)
        {
            if (other != _player || !_states[slot].IsAlive) return;

            GameObject actor = _actors[slot].gameObject;
            _states[slot] = default;
            _actors[slot] = null;
            _alive--;
            actor.SetActive(false);
            Destroy(actor);
        }}
}
