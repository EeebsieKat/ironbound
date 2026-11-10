-- Base-game items. One table per item: display name, the view (see below),
-- socket fine-tuning, a moveset (stance), and the component bag.
--
-- Components: 'melee' makes the swing able to hit, 'block_interact' makes it
-- able to work blocks. Absence is meaningful - the sword can't mine, the
-- pickaxe can't hurt anyone, empty hands are the fallback in engine code.
--
-- 'ironbound:bloodthirst' is defined in components.lua (component TYPES must
-- register before items - main.lua requires components.lua first).
--
-- The view comes in two forms:
--   1. a scene path (the sword below) - your own .tscn, still works
--   2. a table the ENGINE builds (the pickaxe below) - no editor needed:
--        model = 'res://....glb'          a model file, or...
--        primitive = 'box'                box/sphere/cylinder/capsule
--        size = { x, y, z }               box only
--        texture = 'res://....png'        optional
--        color = '#rrggbb'                optional
--        rotation / scale                 on the view root
--      Fine-tuning hand placement stays socket_offset / socket_rotation.

api.items.register('ironbound:test_sword', {
  display = 'Iron Sword',
  view = {model = 'res://Assets/TestSword.glb'},
  socket_offset = { x = 0, y = 0, z = 0 },

  stance = { idle = 'idle', walk = 'walk', jump = 'jump', attack = 'attack' },

  components = {
    melee = { damage = 2, knockback = 6, hit_start = 0.25, hit_end = 0.6 },
    ['ironbound:bloodthirst'] = { bonus = 2 },
  },
})

api.items.register('ironbound:test_pickaxe', {
  display = 'Iron Pickaxe',

  -- Built by the engine from data alone - no .tscn, no editor:
  view = {
    primitive = 'box',
    size = { x = 0.12, y = 0.12, z = 1.1 },
    color = '#8f9aa6',
  },
  socket_offset = { x = 0.05, y = 0, z = 0 },

  -- no stance: falls back to the player's default moveset
  -- no melee: swings all day, hits nothing

  components = {
    block_interact = { can_mine = true, can_place = true, places = 'ironbound:stone' },
  },
})
