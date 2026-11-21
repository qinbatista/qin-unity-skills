using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Qin.UnitySamples.JobStateBatches
{
    public struct CompanionFormationState
    {
        public Vector3 LocalOffset;
        public bool IsFollowing;
    }

    public struct CompanionFormationJob : IJobParallelForTransform
    {
        public NativeArray<CompanionFormationState> States;
        public Vector3 LeaderPosition;
        public Quaternion LeaderRotation;
        public float Speed;
        public float DeltaTime;

        public void Execute(int index, TransformAccess actor)
        {
            CompanionFormationState state = States[index];
            Vector3 destination = LeaderPosition + LeaderRotation * state.LocalOffset;
            Vector3 position = actor.position;
            Vector3 next = Vector3.MoveTowards(position, destination, Speed * DeltaTime);
            bool isFollowing = !position.Equals(next);
            if (isFollowing) actor.position = next;
            if (state.IsFollowing == isFollowing) return;

            state.IsFollowing = isFollowing;
            States[index] = state;
        }
    }
}
