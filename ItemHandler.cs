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
            if (itemId >= 10000 && itemId < 20000)
            {
                int rodId = (int)(itemId - 10000);
                SeaFantasyAP.Log.LogInfo($"Granting Rod: id={rodId} | itemId={itemId}");
                UIManager.PUB_AddRod(rodId, 1);
            }
            // Bait
            else if (itemId >= 20000 && itemId < 30000)
            {
                int baitId = (int)(itemId - 20000);
                UIManager.PUB_AddBite(baitId, 1);
            }
            // Outfit
            else if (itemId >= 30000 && itemId < 40000)
            {
                int outfitId = (int)(itemId - 30000);
                UIManager.PUB_AddDress(outfitId);
            }
            // Potion
            else if (itemId >= 40000 && itemId < 50000)
            {
                int potionId = (int)(itemId - 40000);
                UIManager.PUB_AddPotion(potionId, 1);
            }
            // SP Item
            else if (itemId >= 50000 && itemId < 60000)
            {
                int spId = (int)(itemId - 50000);
                SeaFantasyAP.Log.LogInfo($"Granting SP Item: id={spId} | itemId={itemId}");
                UIManager.PUB_AddItem(spId);

                // when boat is received, add ship manual and set ship key
                if (spId == 09)
                {
                    UIManager.PUB_AddItem(8);
                    SeaFantasyAP.PlayerControllerInstance?.PUB_SetShipKey(1);
                }
            }
            // Gold
            else if (itemId >= 90000 && itemId < 100000)
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