# Shop and Chest – Test Checklist

The shop displays purchasable items using an orange button. Available items with insufficient coins are blue. Items locked behind a level milestone remain blue and show the required level.

| Item | Unlock level | Cost |
| --- | ---: | ---: |
| Lens | 1 | 60 |
| Rock Hammer | 7 | 85 |
| Pipe Wrench | 10 | 110 |
| Compass | 14 | 125 |
| Flow Orb | 20 | 160 |

## Unity Play Mode

1. Open `Assets/Scenes/Main.unity` and press Play.
2. At level 17 with 295 coins: expect four orange buttons and one blue locked button (Flow Orb).
3. Buy one unlocked item. Coins must decrease by its listed cost; inventory count must increase; the button must switch to blue if the remaining balance is insufficient. There should be no large popup panel.
4. Progress to level 20: Flow Orb should unlock and display an orange button only if 160 coins remain.
5. Open Bag: the sentence about opening a chest every 15 stars must not appear.
6. On the map, tap the chest before earning 15 stars: a brief contextual tip should appear. At 15 stars, opening the chest should grant an item.
7. Close and reopen the game to verify inventory, gold balance, unlock level and chest progress persist.

## Relevant source files

- `Assets/Scripts/PipeFlowGame.cs`: shop UI, status feedback, chest tip and input handling.
- `Assets/Scripts/PipeProgress.cs`: item unlock levels, prices, purchases, inventory and save data.
- `Assets/Resources/Art/Screens/shopClean.png`: shop artwork without permanently painted purchase buttons.
- `Assets/Resources/Art/UI/ShopUnlockedBadge.png`: unlocked item background.
- `Assets/Resources/Images/UI/SmallButton_gold.png` and `SmallButton_dark.png`: button states.
- `Assets/Resources/RawArt/`: matching offline artwork fallbacks.

The Unity Editor and Android runtime were not available in the build environment. The checks above are for Play Mode verification on your computer.
