using Godot;
using System.Collections.Generic;

// [Tool] removed along with Chunk's: content is Lua-registered at runtime.
public partial class ChunkManager : Node {
        public static ChunkManager Instance {get; private set;}

        private readonly Dictionary<Chunk, Vector2I> _chunkToPosition = new();
        private readonly Dictionary<Vector2I, Chunk> _positionToChunk = new();

    public override void _EnterTree() {
        // _EnterTree, not _Ready: chunk CHILDREN ready before their parent, so
        // a _Ready here fired after the chunks needed it - they hit a null
        // Instance first. The enter pass always completes before any _Ready.
        Instance = this;
    }

        public void UpdateChunkPosition(Chunk chunk, Vector2I currentPosition, Vector2I previousPosition) {
                if (_positionToChunk.TryGetValue(previousPosition, out var chunkAtPosition) && chunkAtPosition == chunk) {
                        _positionToChunk.Remove(previousPosition);
                }

                _chunkToPosition[chunk] = currentPosition;
                _positionToChunk[currentPosition] = chunk;
        }

        // The world-facing API: set a block by GLOBAL voxel position; the owning
        // chunk is found from the position. (The _chunks list collected in _Ready
        // was never read - removed, along with its Linq dependency.)
        //
        // Note: chunk-boundary blocks always render their outer faces (Chunk's
        // CheckTransparent treats out-of-bounds as air), so no neighbor rebuild
        // is needed when editing at a seam - just some harmless hidden overdraw.
        public void SetBlock(Vector3I globalPosition, Block block) {
                var chunkTilePosition = new Vector2I(
                        Mathf.FloorToInt(globalPosition.X / (float)Chunk.dimensions.X),
                        Mathf.FloorToInt(globalPosition.Z / (float)Chunk.dimensions.Z));

                if (_positionToChunk.TryGetValue(chunkTilePosition, out var chunk)) {
                        chunk.SetBlock((Vector3I)(globalPosition - chunk.GlobalPosition), block);
                }
        }
}
