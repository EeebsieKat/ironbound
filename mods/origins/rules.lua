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

-- Patching (doc's mechanism #3): mutate an existing registration later in
-- load order. Later mods patching the same id override this one - logged.
api.items.patch('ironbound:test_pickaxe', {
  display = 'Trusty Pickaxe',
})
