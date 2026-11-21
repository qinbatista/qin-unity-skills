using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Qin.UnitySamples.JobStateBatches
{
    public enum CoinPhase
    {
        Idle,
        Attracted,
        Collected
    }

    public struct CoinMagnetState
    {
        public CoinPhase Phase;
    }

    public struct CoinMagnetJob : IJobParallelForTransform
    {
        public NativeArray<CoinMagnetState> States;
        public Vector2 CollectorPosition;
        public float Radius;
        public float Speed;
        public float DeltaTime;

        public void Execute(int index, TransformAccess actor)
        {
            CoinMagnetState state = States[index];
            if (state.Phase == CoinPhase.Collected) return;

            Vector3 position = actor.position;
            Vector3 target = new Vector3(CollectorPosition.x, CollectorPosition.y, position.z);
            CoinPhase nextPhase = CoinPhase.Idle;
            if ((target - position).sqrMagnitude <= Radius * Radius)
            {
                Vector3 next = Vector3.MoveTowards(position, target, Speed * DeltaTime);
                if (!position.Equals(next)) actor.position = next;
                nextPhase = next.Equals(target) ? CoinPhase.Collected : CoinPhase.Attracted;
            }

            if (state.Phase == nextPhase) return;
            state.Phase = nextPhase;
            States[index] = state;
        }
    }
}
