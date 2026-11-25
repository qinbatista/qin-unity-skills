using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Qin.UnityEffects.Fluids
{
    // One draw per active rain emitter. Vertices sample the native parcel and contact
    // buffers, so exposure follows the solver even when the cast is paused or interrupted.
    sealed class SideRainRenderer : IDisposable
    {
        private const int Capacity = 160;
        private static readonly int Parcels = Shader.PropertyToID("_RainParcels");
        private static readonly int Contacts = Shader.PropertyToID("_RainContacts");
        private static readonly int Waterline = Shader.PropertyToID("_RainWaterline");
        private static readonly int Clock = Shader.PropertyToID("_RainClock");
        private readonly Mesh _mesh;
        private readonly MeshRenderer _renderer;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();

        public SideRainRenderer(Transform owner, MeshRenderer volume, Material material)
        {
            GameObject child = new GameObject("Side-view rain", typeof(MeshFilter), typeof(MeshRenderer));
            child.hideFlags = HideFlags.DontSave;
            child.layer = volume.gameObject.layer;
            child.transform.SetParent(owner, false);
            _renderer = child.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingLayerID = volume.sortingLayerID;
            _renderer.sortingOrder = volume.sortingOrder + 1;
            _mesh = BuildMesh();
            child.GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private static Mesh BuildMesh()
        {
            // Interleaving streak and splash quads keeps a single geometry binding.
            const int quads = 2 * Capacity;
            Vector3[] vertices = new Vector3[4 * quads];
            List<Vector4> coordinates = new List<Vector4>(vertices.Length);
            int[] indices = new int[6 * quads];
            for (int quad = 0; quad < quads; quad++)
            {
                int vertex = quad * 4;
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = (corner & 1) * 2 - 1;
                    float y = (corner >> 1) * 2 - 1;
                    coordinates.Add(new Vector4(x, y, quad / 2, quad & 1));
                }
                int offset = quad * 6;
                indices[offset] = vertex;
                indices[offset + 1] = vertex + 1;
                indices[offset + 2] = vertex + 2;
                indices[offset + 3] = vertex + 2;
                indices[offset + 4] = vertex + 1;
                indices[offset + 5] = vertex + 3;
            }
            Mesh mesh = new Mesh { name = "Rain parcel quads", vertices = vertices, triangles = indices };
            mesh.SetUVs(0, coordinates);
            mesh.bounds = new Bounds(new Vector3(0f, 1.4f, 0f), new Vector3(6.8f, 4.8f, 1f));
            mesh.UploadMeshData(true);
            return mesh;
        }

        public void Draw(FluidMixSimulation simulation, SOFluidMixRecipe recipe, float seconds, float weight)
        {
            _properties.SetBuffer(Parcels, simulation.Particles);
            _properties.SetTexture(Contacts, simulation.Spray);
            _properties.SetTexture(Waterline, simulation.Waterline);
            _properties.SetVector(Clock, new Vector4(seconds, recipe.RainExposure, weight, recipe.Spray ? 1f : 0f));
            _renderer.SetPropertyBlock(_properties);
            _renderer.enabled = true;
        }

        public void Hide() => _renderer.enabled = false;
        public void SetSortingLayer(int id) => _renderer.sortingLayerID = id;

        public void Dispose()
        {
            Hide();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_renderer.gameObject);
                UnityEngine.Object.Destroy(_mesh);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_renderer.gameObject);
                UnityEngine.Object.DestroyImmediate(_mesh);
            }
        }
    }
}
