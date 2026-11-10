-- Base-game blocks, PURE DATA: png paths only. No .tres, no Godot editor -
-- the engine builds the Block resources from these at load time.
--
--   texture = sides of the block
--   top / bottom = optional per-face overrides (default: same as texture)
--   no textures at all = air-like (never rendered, never meshed)

api.blocks.register('ironbound:air', {})

api.blocks.register('ironbound:stone', {
  texture = 'res://Assets/stone.png',
})

api.blocks.register('ironbound:dirt', {
  texture = 'res://Assets/dirt.png',
})

api.blocks.register('ironbound:grass', {
  texture = 'res://Assets/grass_block_side.png',
  top = 'res://Assets/grass_block_top.png',
  bottom = 'res://Assets/dirt.png',
})
