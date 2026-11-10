-- Base-game gameplay RULES - written exactly the way a third-party mod would
-- write them. Nothing here is engine code; it's all listeners and data.

-- Rule: no weapon swapping mid-swing (priority 100 = gameplay rules run
-- before content hooks get a look in).
api.events.on('item.equipping', function(ctx)
  if api.player.attacking() then
    ctx:cancel()
    api.log('equip cancelled - mid-swing')
  end
end, { priority = 100 })

-- Rule: training enemies never die. Enemies carry an EnemyId; any id starting
-- "training:" takes damage all day but the KILLING blow is vetoed. i-frames,
-- armor, immortality zones, boss phases - all this same shape: look at
-- ctx.data, decide, ctx:cancel().
api.events.on('entity.hurt', function(ctx)
  local d = ctx.data
  if string.sub(d.entity, 1, 9) == 'training:' and d.health - d.damage <= 0 then
    ctx:cancel()
    api.log(d.entity .. ' shrugs off the killing blow')
  end
end, { priority = 100 })

-- Patching (doc's mechanism #3): mutate an existing registration later in
-- load order. Later mods patching the same id override this one - logged.
-- The id must be one that items.lua actually registered.
api.items.patch('ironbound:test_pickaxe', {
  display = 'Trusty Pickaxe',
})
