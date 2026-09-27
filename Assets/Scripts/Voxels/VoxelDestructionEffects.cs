using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Voxels
{
    public readonly struct VoxelDestructionSample
    {
        public readonly Vector3 position;
        public readonly Color32 color;
        public readonly byte texture;

        public VoxelDestructionSample(Vector3 position, Color32 color, byte texture)
        {
            this.position = position;
            this.color = color;
            this.texture = texture;
        }
    }

    // Transient, client-side presentation. The edited chunk remains the authoritative world.
    public sealed class VoxelDestructionEffects : MonoBehaviour
    {
        private const int MaxPieces = 144;
        private const int MaxPiecesPerEdit = 24;
        private const float Lifetime = 1.5f;

        private sealed class Piece
        {
            public GameObject gameObject;
            public MeshFilter filter;
            public MeshRenderer renderer;
            public Rigidbody body;
            public float age;
            public float size;
        }

        private readonly List<Piece> m_Pieces = new(MaxPieces);
        private readonly Mesh[] m_Meshes = new Mesh[VoxelTexture.Last + 1];
        private MaterialPropertyBlock m_Properties;
        private Material m_DebrisMaterial;
        private Material m_SparkMaterial;
        private Texture2D m_SparkTexture;
        private PhysicsMaterial m_BounceMaterial;
        private ParticleSystem m_Sparks;
        private int m_NextPiece;
        private bool m_Initialized;

        public void Initialize(Material terrainMaterial)
        {
            if (m_Initialized) return;
            m_Initialized = true;
            m_DebrisMaterial = terrainMaterial;
            m_Properties = new MaterialPropertyBlock();

            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.SetActive(false);
            Mesh source = template.GetComponent<MeshFilter>().sharedMesh;
            for (byte id = 0; id <= VoxelTexture.Last; id++)
            {
                Mesh mesh = Instantiate(source);
                mesh.name = $"Debris {VoxelTexture.Name(id)}";
                Vector2Int tile = new Voxel {texture = id}.TexturePosition();
                Vector2[] uv = mesh.uv;
                const float inset = 0.025f;
                for (int i = 0; i < uv.Length; i++)
                    uv[i] = new Vector2((tile.x + inset + uv[i].x * (1f - 2f * inset)) * Voxel.TileRatio,
                                        (tile.y + inset + uv[i].y * (1f - 2f * inset)) * Voxel.TileRatio);
                mesh.uv = uv;
                m_Meshes[id] = mesh;
            }
            Destroy(template);

            m_BounceMaterial = new PhysicsMaterial("Voxel debris bounce")
            {
                bounciness = 0.32f,
                dynamicFriction = 0.45f,
                staticFriction = 0.5f,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            CreateSparks();
        }

        public void Emit(IReadOnlyList<VoxelDestructionSample> samples, Vector3 origin)
            => EmitPieces(samples, origin, false);

        public void EmitCollapse(IReadOnlyList<VoxelDestructionSample> samples, Vector3 center)
            => EmitPieces(samples, center, true);

        private void EmitPieces(IReadOnlyList<VoxelDestructionSample> samples, Vector3 origin, bool collapse)
        {
            if (!m_Initialized || samples.Count == 0) return;

            int piecesPerSample = Mathf.Clamp(MaxPiecesPerEdit / samples.Count, 1, 4);
            int emitted = 0;
            foreach (VoxelDestructionSample sample in samples)
            {
                Color tint = MaterialTint(sample.color, sample.texture);
                for (int i = 0; i < piecesPerSample && emitted < MaxPiecesPerEdit; i++, emitted++)
                {
                    Piece piece = GetPiece();
                    Vector3 outward = (sample.position - origin).normalized;
                    Vector3 scatter = Random.insideUnitSphere;
                    Vector3 direction = collapse
                        ? (outward * 0.35f + scatter * 0.25f + Vector3.down).normalized
                        : (outward * 0.65f + scatter * 0.8f + Vector3.up * 0.8f).normalized;
                    float size = collapse ? Random.Range(0.25f, 0.55f) : Random.Range(0.12f, 0.31f);
                    piece.gameObject.SetActive(true);
                    piece.gameObject.transform.position = sample.position + scatter * 0.18f;
                    piece.gameObject.transform.rotation = Random.rotation;
                    piece.gameObject.transform.localScale = Vector3.one * size;
                    piece.filter.sharedMesh = m_Meshes[Mathf.Min(sample.texture, VoxelTexture.Last)];
                    piece.age = 0f;
                    piece.size = size;
                    m_Properties.Clear();
                    m_Properties.SetColor("_Tint", tint);
                    m_Properties.SetColor("_EmissionColor", tint * 0.22f);
                    m_Properties.SetFloat("_DebrisColorStrength", 0.48f);
                    piece.renderer.SetPropertyBlock(m_Properties);
                    piece.body.linearVelocity = direction * (collapse ? Random.Range(3.2f, 5.5f) : Random.Range(4.0f, 8.5f));
                    piece.body.angularVelocity = Random.insideUnitSphere * Random.Range(4f, 12f);
                }

                Color sparkColor = Color.Lerp(tint, TextureAccent(sample.texture), 0.55f);
                for (int i = 0; i < 3; i++)
                {
                    var spark = new ParticleSystem.EmitParams
                    {
                        position = sample.position + Random.insideUnitSphere * 0.18f,
                        velocity = (Random.insideUnitSphere + Vector3.up * 0.8f) * Random.Range(2f, 5f),
                        startColor = sparkColor,
                        startSize = Random.Range(0.09f, 0.21f),
                        startLifetime = Random.Range(0.22f, 0.48f)
                    };
                    m_Sparks.Emit(spark, 1);
                }
            }
        }

        private void Update()
        {
            foreach (Piece piece in m_Pieces)
            {
                if (!piece.gameObject.activeSelf) continue;
                piece.age += Time.deltaTime;
                if (piece.age >= Lifetime)
                {
                    piece.gameObject.SetActive(false);
                    continue;
                }
                float shrink = Mathf.Clamp01((Lifetime - piece.age) / 0.35f);
                piece.gameObject.transform.localScale = Vector3.one * (piece.size * shrink);
            }
        }

        private Piece GetPiece()
        {
            if (m_Pieces.Count < MaxPieces)
            {
                var go = new GameObject("Voxel debris", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(Rigidbody));
                go.SetActive(false);
                go.layer = 2; // Ignore Raycast: fragments cannot absorb weapon hits.
                SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                go.transform.SetParent(transform, true);
                var piece = new Piece
                {
                    gameObject = go,
                    filter = go.GetComponent<MeshFilter>(),
                    renderer = go.GetComponent<MeshRenderer>(),
                    body = go.GetComponent<Rigidbody>()
                };
                piece.renderer.sharedMaterial = m_DebrisMaterial;
                go.GetComponent<BoxCollider>().sharedMaterial = m_BounceMaterial;
                piece.body.mass = 0.06f;
                piece.body.linearDamping = 0.55f;
                piece.body.angularDamping = 0.8f;
                piece.body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                m_Pieces.Add(piece);
                return piece;
            }
            Piece recycled = m_Pieces[m_NextPiece];
            m_NextPiece = (m_NextPiece + 1) % m_Pieces.Count;
            recycled.gameObject.SetActive(false);
            return recycled;
        }

        private static Color MaterialTint(Color32 source, byte texture)
        {
            Color original = source;
            Color color = Color.Lerp(original, TextureAccent(texture), 0.55f);
            float peak = Mathf.Max(color.r, color.g, color.b);
            if (peak < 0.72f) color *= 0.72f / Mathf.Max(peak, 0.01f);
            return color;
        }

        private static Color TextureAccent(byte texture) => texture switch
        {
            VoxelTexture.Solid => new Color(0.36f, 0.95f, 0.55f),
            VoxelTexture.Checkered => new Color(1f, 0.67f, 0.31f),
            VoxelTexture.Striped => new Color(0.45f, 0.74f, 1f),
            VoxelTexture.Speckled => new Color(1f, 0.47f, 0.73f),
            _ => Color.white
        };

        private void CreateSparks()
        {
            var go = new GameObject("Voxel sparks", typeof(ParticleSystem));
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            go.transform.SetParent(transform, true);
            m_Sparks = go.GetComponent<ParticleSystem>();
            var main = m_Sparks.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 450;
            main.gravityModifier = 0.5f;
            var emission = m_Sparks.emission;
            emission.enabled = false;
            var shape = m_Sparks.shape;
            shape.enabled = false;

            m_SparkTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            m_SparkTexture.wrapMode = TextureWrapMode.Clamp;
            m_SparkTexture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f)) / 7.5f;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.5f);
                m_SparkTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            m_SparkTexture.Apply();
            Shader shader = Shader.Find("Custom/VoxelSparks");
            if (!shader) shader = Shader.Find("Particles/Standard Unlit");
            m_SparkMaterial = new Material(shader) {mainTexture = m_SparkTexture};
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = m_SparkMaterial;
            m_Sparks.Play();
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in m_Meshes) if (mesh) Destroy(mesh);
            if (m_BounceMaterial) Destroy(m_BounceMaterial);
            if (m_SparkMaterial) Destroy(m_SparkMaterial);
            if (m_SparkTexture) Destroy(m_SparkTexture);
        }
    }
}
