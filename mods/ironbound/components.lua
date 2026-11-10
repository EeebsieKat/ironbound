-- Component TYPE registrations. This file must load BEFORE items.lua: items
-- resolve their components at registration time, so a component type that
-- registers later arrives as "unknown component" and gets silently dropped
-- from the item. (This exact bug: main.lua used to require rules.lua - which
-- registered the types - after items.lua.)
--
-- This is the doc's "mods register new component types" mechanism in action:
-- the engine knows nothing about bloodthirst; it just calls the hooks.

api.components.register('ironbound:bloodthirst', {
  on_equip = function(item_id, data)
    api.log(item_id .. ' hums with hunger (bonus ' .. tostring(data.bonus) .. ')')
  end,
  on_unequip = function(item_id)
    api.log(item_id .. ' goes quiet')
  end,
})
