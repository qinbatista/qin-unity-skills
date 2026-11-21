using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace Qin.UnitySamples.JobStateBatches
{
    public struct FireballVolleyState
    {
        public float Angle;
        public float RemainingDistance;
    }

    public struct FireballVolleyJob : IJobParallelForTransform
    {
        public NativeArray<FireballVolleyState> States;
        public Vector3 Forward;
        public float Speed;
        public float DeltaTime;

        public void Execute(int index, TransformAccess actor)
        {
            FireballVolleyState state = States[index];
            if (state.RemainingDistance <= 0f) return;

            Vector3 direction = Quaternion.AngleAxis(state.Angle, Vector3.forward) * Forward;
            float distance = Mathf.Min(state.RemainingDistance, Speed * DeltaTime);
            actor.position += direction * distance;
            state.RemainingDistance -= distance;
            States[index] = state;
        }
    }
}
