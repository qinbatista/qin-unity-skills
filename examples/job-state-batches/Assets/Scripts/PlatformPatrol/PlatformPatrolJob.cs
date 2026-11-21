using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Qin.UnitySamples.JobStateBatches
{
    public enum PatrolPhase
    {
        Outbound,
        HoldAtEnd,
        Inbound,
        HoldAtStart
    }

    public struct PlatformPatrolState
    {
        public Vector3 Start;
        public Vector3 End;
        public float HoldRemaining;
        public PatrolPhase Phase;
    }

    public struct PlatformPatrolJob : IJobParallelForTransform
    {
        public NativeArray<PlatformPatrolState> States;
        public float Speed;
        public float HoldDuration;
        public float DeltaTime;

        public void Execute(int index, TransformAccess actor)
        {
            PlatformPatrolState state = States[index];
            switch (state.Phase)
            {
                case PatrolPhase.HoldAtEnd:
                case PatrolPhase.HoldAtStart:
                    state.HoldRemaining -= DeltaTime;
                    if (state.HoldRemaining <= 0f)
                        state.Phase = state.Phase == PatrolPhase.HoldAtEnd ? PatrolPhase.Inbound : PatrolPhase.Outbound;
                    States[index] = state;
                    return;
            }

            bool outbound = state.Phase == PatrolPhase.Outbound;
            Vector3 endpoint = outbound ? state.End : state.Start;
            Vector3 current = actor.position;
            Vector3 next = Vector3.MoveTowards(current, endpoint, Speed * DeltaTime);
            if (!current.Equals(next)) actor.position = next;
            if (!next.Equals(endpoint)) return;

            state.Phase = outbound ? PatrolPhase.HoldAtEnd : PatrolPhase.HoldAtStart;
            state.HoldRemaining = HoldDuration;
            States[index] = state;
        }
    }
}
