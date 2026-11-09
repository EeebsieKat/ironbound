-- Base-game items. One table per item: display name, the scene that renders
-- in hand, socket fine-tuning, a moveset (stance), and the component bag.
--
-- Components: 'melee' makes the swing able to hit, 'block_interact' makes it
-- able to work blocks. Absence is meaningful - the sword can't mine, the
-- pickaxe can't hurt anyone, empty hands are the fallback in engine code.
--
-- 'origins:bloodthirst' is defined in components.lua and demonstrates a mod
-- registering its own component TYPE with its own hooks. Component TYPES must
-- register before items (main.lua requires components.lua first) because
-- components resolve at registration time.
--
-- >>> ADJUST THE view PATHS to your actual item scenes (or a box.tscn) <<<

api.items.register('ironbound:test_sword', {
  display = 'Iron Sword',
  view = 'res://Scenes/test/sword.tscn',
  socket_offset = { x = 0, y = 0, z = 0 },

  stance = { idle = 'idle', walk = 'walk', jump = 'jump', attack = 'attack' },

  components = {
    melee = { damage = 2, knockback = 6, hit_start = 0.25, hit_end = 0.6 },
    ['origins:bloodthirst'] = { bonus = 2 },
  },
})

api.items.register('ironbound:test_pickaxe', {
  display = 'Iron Pickaxe',
  view = 'res://Scenes/test/pickaxe.tscn',
  socket_offset = { x = 0.05, y = 0, z = 0 },

  -- no stance: falls back to the player's default moveset
  -- no melee: swings all day, hits nothing

  components = {
    block_interact = { can_mine = true, can_place = true, places = 'origins:stone' },
  },
})
