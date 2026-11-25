using UnityEngine;

namespace Qin.UnityEffects.Presentation
{
    [DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class BowlProjection2D : MonoBehaviour
    {
        static readonly int Film = Shader.PropertyToID("_LiquidFilm");
        static readonly int Weight = Shader.PropertyToID("_FilmWeight");
        static readonly int Backdrop = Shader.PropertyToID("_BowlBackdrop");
        static readonly int UseBackdrop = Shader.PropertyToID("_UseBowlBackdrop");
        static readonly int ScreenRect = Shader.PropertyToID("_BowlScreenRect");

        MeshRenderer _surface;
        MaterialPropertyBlock _sectionValues, _surfaceValues;

        void Awake()
        {
            _surface = GetComponent<MeshRenderer>();
            Clear();
        }

        // The source can be hidden for its side-view drawing and remain a valid solver.
        // An explicit backdrop uses lower-left, normalized camera coordinates and half extents.
        public bool Bind(Renderer section, Texture dryBackdrop, Vector4 screenRect)
        {
            if (!_surface) _surface = GetComponent<MeshRenderer>();
            if (!isActiveAndEnabled || !section || section == _surface ||
                (dryBackdrop && (screenRect.z <= 0f || screenRect.w <= 0f)))
            {
                Clear();
                return false;
            }
            _sectionValues ??= new MaterialPropertyBlock();
            section.GetPropertyBlock(_sectionValues);
            Texture field = _sectionValues.GetTexture(Film);
            float weight = _sectionValues.GetFloat(Weight);
            if (!field || field.width != 128 || field.height != 4 || !(weight > 0f))
            {
                Clear();
                return false;
            }
            _surfaceValues ??= new MaterialPropertyBlock();
            _surface.GetPropertyBlock(_surfaceValues);
            _surfaceValues.SetTexture(Film, field);
            _surfaceValues.SetFloat(Weight, weight);
            _surfaceValues.SetTexture(Backdrop, dryBackdrop ? dryBackdrop : Texture2D.blackTexture);
            _surfaceValues.SetFloat(UseBackdrop, dryBackdrop ? 1f : 0f);
            _surfaceValues.SetVector(ScreenRect, screenRect);
            _surface.SetPropertyBlock(_surfaceValues);
            _surface.enabled = true;
            return true;
        }

        public void Clear()
        {
            if (!_surface) return;
            _surface.enabled = false;
            if (_surfaceValues == null) return;
            _surface.GetPropertyBlock(_surfaceValues);
            _surfaceValues.SetTexture(Film, Texture2D.blackTexture);
            _surfaceValues.SetTexture(Backdrop, Texture2D.blackTexture);
            _surfaceValues.SetFloat(Weight, 0f);
            _surface.SetPropertyBlock(_surfaceValues);
        }

        void OnDisable()
        {
            Clear();
            // Drop the saved source and game backplate handles when a pooled bowl retires.
            _sectionValues?.Clear();
            _surfaceValues?.Clear();
        }
    }
}
