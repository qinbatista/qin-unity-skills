using System;
using UnityEngine;

namespace Qin.UnityEffects.Fluids
{
    [CreateAssetMenu(menuName = "Qin/Fluids/Recipe")]
    public sealed class SOFluidMixRecipe : ScriptableObject
    {
        public enum Inlet { SurfaceImpulse, FallingMass, Jet, OpposedJets, Waterfall, Vortex, Curtain, Needle, FallingDrops, RadialBurst }

        [Flags]
        public enum Response { None = 0, SurfaceWaves = 1, SolidObstacle = 2, Spray = 4 }

        [SerializeField] private Inlet _feed = Inlet.Jet;
        [SerializeField] private Response _responses = Response.SurfaceWaves | Response.Spray;
        [SerializeField] private Vector3 _origin = new Vector3(-1.85f, 1.05f, 0f);
        [SerializeField] private Vector3 _velocity = new Vector3(3.2f, 1.75f, .07f);
        [SerializeField] private Vector3 _obstacleCenter = new Vector3(-.15f, .84f, 0f);
        [SerializeField, Min(.01f)] private float _obstacleRadius = .45f;
        [SerializeField, Min(0f)] private float _gravity = 8f;
        [SerializeField, Range(0f, 2f)] private float _strength = 1f;
        [SerializeField, Range(0f, 2f)] private float _pressure = 1f;
        [SerializeField, Range(0f, 2f)] private float _swirl = 1f;
        [SerializeField, Min(.05f)] private float _feedDuration = 1.25f;
        [SerializeField, Min(.3f)] private float _duration = 2.7f;

        [SerializeField] private bool _playOnEnable = true;
        [SerializeField, Min(.34f)] private float _curtainHold = .50f;
        [Tooltip("For the Waterfall inlet, replenish the falling sheet while the source is open.")]
        [SerializeField] private bool _continuousFeed;

        [SerializeField, Range(1, 160)] private int _dropCount = 48;
        [SerializeField, Range(0f, 5f)] private float _dropSpan = 3.8f;

        public int DropCount => Mathf.Clamp(_dropCount, 1, 160);
        public float DropSpan => Mathf.Clamp(_dropSpan, 0f, 5f);
        public bool PlayOnEnable => _playOnEnable;
        public bool ContinuousFeed => _feed == Inlet.Waterfall && _continuousFeed;
        public Inlet Feed => _feed;
        public bool SurfaceWaves => (_responses & Response.SurfaceWaves) != 0;
        public bool SolidObstacle => (_responses & Response.SolidObstacle) != 0;
        public bool Spray => (_responses & Response.Spray) != 0;
        public Vector3 Origin => _origin;
        public Vector3 Velocity => _velocity;
        public Vector3 ObstacleCenter => _obstacleCenter;
        public float ObstacleRadius => Mathf.Max(.01f, _obstacleRadius);
        public float Gravity => Mathf.Max(0f, _gravity);
        public float Strength => Mathf.Clamp(_strength, 0f, 2f);
        public float Swirl => Mathf.Clamp(_swirl, 0f, 2f);
        public float Pressure => Mathf.Clamp(_pressure, 0f, 2f);
        public float FeedDuration => Mathf.Clamp(_feedDuration, .05f, Duration);
        public float CurtainHold => Mathf.Max(.34f, _curtainHold);
        public float Duration => Mathf.Max(_feed == Inlet.Curtain ? CurtainHold + 1.40f : .3f, _duration);
    }
}
