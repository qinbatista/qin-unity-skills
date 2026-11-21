using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Qin.UnitySamples.JobStateBatches
{
    public struct EnemyChaseState
    {
        public bool IsAlive;
        public bool IsChasing;
    }

    public struct EnemyChaseJob : IJobParallelForTransform
    {
        public NativeArray<EnemyChaseState> States;
        public Vector2 PlayerPosition;
        public float Speed;
        public float DeltaTime;

        public void Execute(int index, TransformAccess actor)
        {
            EnemyChaseState state = States[index];
            if (!state.IsAlive) return;

            Vector3 position = actor.position;
            Vector3 target = new Vector3(PlayerPosition.x, PlayerPosition.y, position.z);
            Vector3 next = Vector3.MoveTowards(position, target, Speed * DeltaTime);
            bool isChasing = !position.Equals(next);
            if (isChasing) actor.position = next;
            if (state.IsChasing == isChasing) return;

            state.IsChasing = isChasing;
            States[index] = state;
        }
    }
}
