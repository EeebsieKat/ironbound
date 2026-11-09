-- Base-game blocks. Each entry points at a Block resource (.tres) - the
-- assets are data files, the definitions live here.
--
-- >>> ADJUST THE PATHS to wherever your Block .tres files actually are <<<
-- (the files you previously wired into BlockManager's inspector exports).

api.blocks.register('origins:air',   { resource = 'res://Scenes/test/air.tres' })
api.blocks.register('origins:stone', { resource = 'res://Scenes/test/stone.tres' })
api.blocks.register('origins:dirt',  { resource = 'res://Scenes/test/dirt.tres' })
api.blocks.register('origins:grass', { resource = 'res://Scenes/test/grass.tres' })
