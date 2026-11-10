-- Ironbound - the base game, as a mod.
-- Everything the game ships content-wise happens in this folder, through the
-- same api a third-party mod gets. If you can read these files, you can mod
-- this game.

-- Load order matters:
--   blocks      - blocks first, the texture atlas is built from them
--   components  - component TYPES before items (items resolve at register time)
--   items       - the items themselves
--   rules       - event rules + patches against the registered items
require('blocks')
require('components')
require('items')
require('rules')

api.log('Ironbound content loaded')
