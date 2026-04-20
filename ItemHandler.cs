public class ItemHandler
{
    public static bool IsGrantingItem = false;
    public static void ProcessItemQueue(UIManager uiManager)
    {
        if (ArchipelagoClient.ItemQueue.TryDequeue(out var item))
        {
            SeaFantasyAP.Log.LogInfo($"Granting item: {item.ItemName}");
            GrantItem(item.ItemId, uiManager);
        }
    }

    private static void GrantItem(long itemId, UIManager UIManager)
    {
        IsGrantingItem = true;
        try
        {
            // Rod
            if (itemId >= SeaFantasyAP.ITEM_ROD && itemId < SeaFantasyAP.ITEM_BAIT)
            {
                int rodId = (int)(itemId - SeaFantasyAP.ITEM_ROD);
                SeaFantasyAP.Log.LogInfo($"Granting Rod: id={rodId} | itemId={itemId}");
                UIManager.PUB_AddRod(rodId, 1);
            }
            // Bait
            else if (itemId >= SeaFantasyAP.ITEM_BAIT && itemId < SeaFantasyAP.ITEM_OUTFIT)
            {
                int baitId = (int)(itemId - SeaFantasyAP.ITEM_BAIT);
                UIManager.PUB_AddBite(baitId, 1);
            }
            // Outfit
            else if (itemId >= SeaFantasyAP.ITEM_OUTFIT && itemId < SeaFantasyAP.ITEM_POTION)
            {
                int outfitId = (int)(itemId - SeaFantasyAP.ITEM_OUTFIT);
                UIManager.PUB_AddDress(outfitId);
            }
            // Potion
            else if (itemId >= SeaFantasyAP.ITEM_POTION && itemId < SeaFantasyAP.ITEM_SPECIAL)
            {
                int potionId = (int)(itemId - SeaFantasyAP.ITEM_POTION);
                UIManager.PUB_AddPotion(potionId, 1);
            }
            // SP Item
            else if (itemId >= SeaFantasyAP.ITEM_SPECIAL && itemId < SeaFantasyAP.ITEM_GOLD)
            {
                int spId = (int)(itemId - SeaFantasyAP.ITEM_SPECIAL);
                SeaFantasyAP.Log.LogInfo($"Granting SP Item: id={spId} | itemId={itemId}");
                UIManager.PUB_AddItem(spId);

                // when boat is received, add ship manual and set ship key
                if (spId == 9)
                {
                    UIManager.PUB_AddItem(8);
                    SeaFantasyAP.PlayerControllerInstance?.PUB_SetShipKey(1);
                }
            }
            // Gold
            else if (itemId >= SeaFantasyAP.ITEM_GOLD && itemId < 100000)
            {
                int amount;
                switch (itemId)
                {
                    case 90000: amount = 100; break;
                    case 90001: amount = 500; break;
                    case 90002: amount = 1000; break;
                    default: amount = 100; break;
                }
                UIManager.PUB_SendGold(amount);
            }
        }
        finally
        {
            IsGrantingItem = false;
        }
        
    }
}