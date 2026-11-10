using Godot;
using System.Collections.Generic;
using System.Linq;

// Voxel mechanism only - zero content. Blocks arrive through the registry
// (the ironbound mod registers them from Lua); this node builds the texture
// atlas from whatever is registered and exposes the terrain generator's four
// block picks, resolved by id from the registry.
//
// Ordering-proof: setup runs in _EnterTree (see below), so node order in the
// scene tree no longer matters. The LuaEngine autoload still loads all mods
// before the main scene even enters the tree.
public partial class BlockManager : Node {

    // Which registry ids the terrain generator uses. Scene data, not code -
    // a modded world can point these at its own blocks.
    [Export] public string AirId = "ironbound:air";
    [Export] public string StoneId = "ironbound:stone";
    [Export] public string DirtId = "ironbound:dirt";
    [Export] public string GrassId = "ironbound:grass";

    public Block Air {get; private set;}
    public Block Stone {get; private set;}
    public Block Dirt {get; private set;}
    public Block Grass {get; private set;}

    private readonly Dictionary<Texture2D, Vector2I> _atlasLookup = new();

    private int _gridWidth = 4;
    private int _gridHeight;

    public Vector2I BlockTextureSize {get;} = new(16, 16);

    public Vector2 TextureAtlasSize {get; private set;}

    public static BlockManager Instance {get; private set;}

    public StandardMaterial3D chunkMaterial {get; private set;}

    public override void _EnterTree() {
        // _EnterTree, not _Ready: the entire scene finishes _enter_tree before
        // ANY node's _Ready runs. Chunks that ready before us (siblings ahead
        // in the tree, or children of a node above us) find Instance, the
        // resolved blocks and the atlas material already in place.
        Instance = this;

        Air = Registries.Blocks.Get(AirId);
        Stone = Registries.Blocks.Get(StoneId);
        Dirt = Registries.Blocks.Get(DirtId);
        Grass = Registries.Blocks.Get(GrassId);

        if (Air == null || Stone == null || Dirt == null || Grass == null) {
            // The count is the diagnostic: 0 blocks = the mod never loaded
            // (check the [mods] lines in Output); >0 but these four missing =
            // ids mismatched (renamed in mod.toml or blocks.lua?).
            GD.PushError("[blocks] terrain blocks missing from the registry " +
                         $"(registry has {Registries.Blocks.Ids.Count()} block(s)). " +
                         "Check the [mods] and [registry:blocks] lines in Output - " +
                         "terrain will not generate.");
            return;
        }

        RebuildAtlas();
    }

    // Builds the texture atlas from every REGISTERED block - so blocks a mod
    // registers get atlas tiles too. Call this again if blocks register
    // after startup.
    public void RebuildAtlas() {
        _atlasLookup.Clear();

        var blockTextures = Registries.Blocks.Values
            .SelectMany(block => block.Textures)
            .Where(texture => texture != null)
            .Distinct()
            .ToArray();

        for (int i = 0; i < blockTextures.Length; i++) {
            _atlasLookup[blockTextures[i]] = new Vector2I(i % _gridWidth, i / _gridWidth);
        }

        _gridHeight = Mathf.CeilToInt(blockTextures.Length / (float)_gridWidth);

        var image = Image.Create(_gridWidth * BlockTextureSize.X, _gridHeight * BlockTextureSize.Y, false, Image.Format.Rgba8);

        List<string> resized = new();
        List<string> flattened = new();

        for (int x = 0; x < _gridWidth; x++) {
            for (int y = 0; y < _gridHeight; y++) {
                var imgIndex = x + y * _gridWidth;

                if (imgIndex >= blockTextures.Length) continue;

                Texture2D texture = blockTextures[imgIndex];
                var currentImage = texture.GetImage();
                currentImage.Convert(Image.Format.Rgba8);

                // A texture that isn't tile-sized used to be silently CROPPED
                // (top-left 16x16 only) - the classic "my block texture looks
                // flat/wrong" cause. Scale into the tile instead, nearest so
                // pixel art stays crisp.
                if (currentImage.GetSize() != BlockTextureSize) {
                    currentImage.Resize(BlockTextureSize.X, BlockTextureSize.Y, Image.Interpolation.Nearest);
                    resized.Add(Describe(texture));
                }

                // The chunk material is opaque; a transparent png would show
                // the RGB hidden under its transparent pixels (often black or
                // white). Flatten so a block texture always draws solid.
                if (ForceOpaque(currentImage)) flattened.Add(Describe(texture));

                image.BlitRect(currentImage, new Rect2I(Vector2I.Zero, BlockTextureSize), new Vector2I(x, y) * BlockTextureSize);
            }
        }

        var textureAtlas = ImageTexture.CreateFromImage(image);

        chunkMaterial = new() {
            AlbedoTexture = textureAtlas,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
        };

        TextureAtlasSize = new Vector2(_gridWidth, _gridHeight);

        GD.Print($"Done loading {blockTextures.Length} images to make {_gridWidth} x {_gridHeight} atlas for {Registries.Blocks.Ids.Count()} blocks");

        foreach (string path in resized) {
            GD.PushWarning($"[blocks] {path} is not {BlockTextureSize.X}x{BlockTextureSize.Y} - " +
                           "scaled to fit its atlas tile (draw at tile size for best results)");
        }
        foreach (string path in flattened) {
            GD.PushWarning($"[blocks] {path} has transparent pixels - flattened to opaque " +
                           "(the block mesher can't draw see-through faces)");
        }

        DumpAtlasLayout();
    }

    // Which texture landed on which atlas tile, per block - the FIRST place
    // to look when a face renders wrong. Prints at every atlas build.
    private void DumpAtlasLayout() {
        foreach (var (id, block) in Registries.Blocks.Entries) {
            List<string> faces = new();

            if (block.Texture != null) faces.Add($"side {DescribeFace(block.Texture)}");
            if (block.TopTexture != null) faces.Add($"top {DescribeFace(block.TopTexture)}");
            if (block.BottomTexture != null) faces.Add($"bottom {DescribeFace(block.BottomTexture)}");

            GD.Print(faces.Count == 0
                ? $"[blocks] atlas '{id}': no textures (air-like)"
                : $"[blocks] atlas '{id}': {string.Join(", ", faces)}");
        }
    }

    private string DescribeFace(Texture2D texture) {
        Vector2I tile = _atlasLookup.TryGetValue(texture, out Vector2I position) ? position : new Vector2I(-1, -1);
        return $"{Describe(texture)} {texture.GetWidth()}x{texture.GetHeight()} @({tile.X},{tile.Y})";
    }

    private static string Describe(Texture2D texture) =>
        texture.ResourcePath.Length > 0 ? texture.ResourcePath : "<runtime texture>";

    // Returns true if any pixel was made opaque.
    private static bool ForceOpaque(Image image) {
        bool changed = false;
        for (int y = 0; y < image.GetHeight(); y++) {
            for (int x = 0; x < image.GetWidth(); x++) {
                Color c = image.GetPixel(x, y);
                if (c.A < 1f) {
                    image.SetPixel(x, y, new Color(c.R, c.G, c.B, 1f));
                    changed = true;
                }
            }
        }
        return changed;
    }

    public Vector2I GetTextureAtlasPosition(Texture2D texture) {
        if (texture == null) {
            return Vector2I.Zero;
        }

        if (_atlasLookup.TryGetValue(texture, out Vector2I position)) {
            return position;
        }

        // Unknown texture (registered after the atlas was built?) - warn and
        // fall back to tile 0 instead of crashing the mesher mid-chunk.
        GD.PushWarning($"[blocks] texture '{texture.ResourcePath}' is not in the atlas - was RebuildAtlas() run after it was registered?");
        return Vector2I.Zero;
    }

    // Convenience: block lookup by registry id, e.g. GetBlock("ironbound:stone").
    public Block GetBlock(string id) => Registries.Blocks.Get(id);
}
