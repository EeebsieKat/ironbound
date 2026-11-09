using Godot;
using System;

// [Tool] removed: content registers from Lua at RUNTIME (via the LuaEngine
// autoload), so there is nothing to generate in the editor anymore. Terrain
// previews in the viewport went with it; if you want them back later, the
// mod loader can be wired into an EditorPlugin.
public partial class Chunk : StaticBody3D
{
        [Export] public CollisionShape3D collisionShape {get; set;}
        [Export] public MeshInstance3D meshInstance {get; set;}

        public static Vector3I dimensions = new Vector3I(16, 64, 16);

        private static readonly Vector3I[] _vertices = new Vector3I[] {
                new(0, 0, 0),
                new(1, 0, 0),
                new(0, 1, 0),
                new(1, 1, 0),
                new(0, 0, 1),
                new(1, 0, 1),
                new(0, 1, 1),
                new(1, 1, 1)
        };

        private static readonly int[] _top = new int[] { 2, 3, 7, 6 };
        private static readonly int[] _bottom = new int[] { 0, 4, 5, 1 };
        private static readonly int[] _left = new int[] { 6, 4, 0, 2 };
        private static readonly int[] _right = new int[] { 3, 1, 5, 7 };
        private static readonly int[] _back = new int[] { 7, 5, 4, 6 };
        private static readonly int[] _front = new int[] { 2, 0, 1, 3 };

        private SurfaceTool _surfaceTool = new();
        private Block[,,] _blocks = new Block[dimensions.X, dimensions.Y, dimensions.Z];

        public Vector2I ChunkPosition {get; private set;}

        [Export] public FastNoiseLite Noise {get; set;}

        public void SetChunkPosition(Vector2I position) {
                // Defensive: world registration needs the manager; generation
                // itself can still run without it.
                if (ChunkManager.Instance == null) {
                        GD.PushWarning($"[chunk] {Name}: ChunkManager not ready - " +
                                       "this chunk won't respond to world edits yet");
                } else {
                        ChunkManager.Instance.UpdateChunkPosition(this, position, ChunkPosition);
                }

                ChunkPosition = position;
                GlobalPosition = new Vector3(ChunkPosition.X * dimensions.X, 0, ChunkPosition.Y * dimensions.Z);

                Generate();
                Update();
        }

    public override void _Ready() {
                // Deferred on purpose. The ready pass runs children-first, so a
                // chunk can _Ready before BlockManager (atlas material) exists -
                // and even before its own ChunkManager parent. A deferred call
                // runs after EVERY _Ready in the tree, so terrain never races
                // scene setup, no matter how the tree is arranged.
                CallDeferred(nameof(InitializeChunkPosition));
        }

    // Runs after all _Ready calls. (Float division + FloorToInt: plain integer
    // division truncates toward zero, which misplaces chunks at negative
    // coordinates.)
    // PUBLIC on purpose: CallDeferred goes through Godot's string-based
    // method lookup, which only sees public C# methods - a private method
    // here fails at flush time with "method not found" and the chunk never
    // initializes.
    public void InitializeChunkPosition() {
                SetChunkPosition(new Vector2I(
                        Mathf.FloorToInt(GlobalPosition.X / (float)dimensions.X),
                        Mathf.FloorToInt(GlobalPosition.Z / (float)dimensions.Z)));
        }

        public void Generate() {
                // Noise guard: a chunk with no Noise resource assigned used to NRE
                // here - once per chunk - and take its whole _Ready chain down.
                if (Noise == null) {
                        GD.PushWarning($"[chunk] {Name}: no Noise resource set - skipping terrain generation");
                        return;
                }

                // Content guard: if the mod loader didn't run (bad autoload, broken
                // mod), terrain blocks are null - warn once and skip instead of NRE-ing
                // every chunk.
                var manager = BlockManager.Instance;
                if (manager == null || manager.Stone == null || manager.Dirt == null ||
                    manager.Grass == null || manager.Air == null) {
                        GD.PushWarning("[chunk] base blocks not registered - skipping terrain generation");
                        return;
                }

                for (int x = 0; x < dimensions.X; x++) {
                        for (int y = 0; y < dimensions.Y; y++) {
                                for (int z = 0; z < dimensions.Z; z++) {
                                        Block block;

                                        var globalBlockPosition = ChunkPosition * new Vector2I(dimensions.X, dimensions.Z) + new Vector2(x, z);
                                        var groundHeight = (int)(dimensions.Y * ((Noise.GetNoise2D(globalBlockPosition.X, globalBlockPosition.Y) + 1f) / 2f));

                                        if (y < groundHeight / 2) {
                                                block = BlockManager.Instance.Stone;
                                        } else if (y < groundHeight) {
                                                block = BlockManager.Instance.Dirt;
                                        } else if (y == groundHeight) {
                                                block = BlockManager.Instance.Grass;
                                        } else {
                                                block = BlockManager.Instance.Air;
                                        }

                                        _blocks[x, y, z] = block;
                                }
                        }
                }
        }

        public void Update() {
                _surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

                for (int x = 0; x < dimensions.X; x++) {
                        for (int y = 0; y < dimensions.Y; y++) {
                                for (int z = 0; z < dimensions.Z; z++) {
                                        CreateBlockMesh(new Vector3I(x, y, z));
                                }
                        }
                }

                var material = BlockManager.Instance?.chunkMaterial;
                if (material == null) {
                        GD.PushWarning($"[chunk] {Name}: no atlas material yet - skipping mesh build");
                        return;
                }

                _surfaceTool.SetMaterial(material);
                var mesh = _surfaceTool.Commit();

                meshInstance.Mesh = mesh;
                collisionShape.Shape = mesh.CreateTrimeshShape();
        }

        private void CreateBlockMesh(Vector3I blockPosition) {
                var block = _blocks[blockPosition.X, blockPosition.Y, blockPosition.Z];

                if (block == BlockManager.Instance.Air) return;

                if (CheckTransparent(blockPosition + Vector3I.Up)) {
                        CreateFaceMesh(_top, blockPosition, block.TopTexture ?? block.Texture);
                }

                if (CheckTransparent(blockPosition + Vector3I.Down)) {
                        CreateFaceMesh(_bottom, blockPosition, block.BottomTexture ?? block.Texture);
                }

                if (CheckTransparent(blockPosition + Vector3I.Left)) {
                        CreateFaceMesh(_left, blockPosition, block.Texture);
                }

                if (CheckTransparent(blockPosition + Vector3I.Right)) {
                        CreateFaceMesh(_right, blockPosition, block.Texture);
                }

                if (CheckTransparent(blockPosition + Vector3I.Forward)) {
                        CreateFaceMesh(_front, blockPosition, block.Texture);
                }

                if (CheckTransparent(blockPosition + Vector3I.Back)) {
                        CreateFaceMesh(_back, blockPosition, block.Texture);
                }
        }

        private void CreateFaceMesh(int[] face, Vector3I blockPosition, Texture2D texture) {
                var texturePosition = BlockManager.Instance.GetTextureAtlasPosition(texture);
                var textureAtlasSize = BlockManager.Instance.TextureAtlasSize;

                var uvOffset = texturePosition / textureAtlasSize;
                var uvWidth = 1f / textureAtlasSize.X;
                var uvHeight = 1f / textureAtlasSize.Y;

                var uvA = uvOffset + new Vector2(0, 0);
                var uvB = uvOffset + new Vector2(0, uvHeight);
                var uvC = uvOffset + new Vector2(uvWidth, uvHeight);
                var uvD = uvOffset + new Vector2(uvWidth, 0);

                var a = _vertices[face[0]] + blockPosition;
                var b = _vertices[face[1]] + blockPosition;
                var c = _vertices[face[2]] + blockPosition;
                var d = _vertices[face[3]] + blockPosition;

                var uvTriangle1 = new Vector2[] {uvA, uvB, uvC};
                var uvTriangle2 = new Vector2[] {uvA, uvC, uvD};

                var triangle1 = new Vector3[] {a, b, c};
                var triangle2 = new Vector3[] {a, c, d};

                var normal = ((Vector3)(c-a)).Cross(((Vector3)(b-a))).Normalized();
                var normals = new Vector3[] {normal, normal, normal};

                _surfaceTool.AddTriangleFan(triangle1, uvTriangle1, normals: normals);
                _surfaceTool.AddTriangleFan(triangle2, uvTriangle2, normals: normals);
        }

        private bool CheckTransparent(Vector3I blockPosition) {
                if (blockPosition.X < 0 || blockPosition.X >= dimensions.X) return true;
                if (blockPosition.Y < 0 || blockPosition.Y >= dimensions.Y) return true;
                if (blockPosition.Z < 0 || blockPosition.Z >= dimensions.Z) return true;

                return _blocks[blockPosition.X, blockPosition.Y, blockPosition.Z] == BlockManager.Instance.Air;
        }

        public void SetBlock(Vector3I blockPosition, Block block) {
                _blocks[blockPosition.X, blockPosition.Y, blockPosition.Z] = block;
                Update();
        }
}
