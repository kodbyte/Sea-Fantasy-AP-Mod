using BepInEx;
using BepInEx.Logging;
using CodeStage.AntiCheat.ObscuredTypes;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

[BepInPlugin("com.kodbyte.seafantasyap", "Sea Fantasy AP", "0.0.1")]
public class SeaFantasyAP : BaseUnityPlugin
{
    public const long LOC_FISH = 10000;
    public const long LOC_CHEST = 20000;
    public const long LOC_ITEM = 30000;
    public const long LOC_ROD = 40000;
    public const long LOC_OUTFIT = 50000;
    public const long LOC_FROG = 80000;
    public const long ITEM_ROD = 10000;
    public const long ITEM_BAIT = 20000;
    public const long ITEM_OUTFIT = 30000;
    public const long ITEM_POTION = 40000;
    public const long ITEM_SPECIAL = 50000;
    public const long ITEM_GOLD = 90000;

    internal static ManualLogSource Log;
    public static UIManager UIManagerInstance;
    public static MainPlayerController PlayerControllerInstance;
    public static bool IsFishingActive = false;
    public static bool IsOpeningChest = false;
    public static bool IsLocationPickup = false;
    public static long LastLocationId = -1;
    public static bool IsCutsceneActive = false;
    public static float messageTimer = 0f;
    public const float MESSAGE_DURATION = 5f;
    public static Queue<string> MessageQueue = new Queue<string>();
    public static UnityEngine.UI.Text MessageText;
    public static Sprite APLogoSprite;

    private static string configPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
        "connection.json"
    );

    private void Awake()
    {
        Log = Logger;

        LoadAPLogo();

        if (!File.Exists(configPath))
        {
            File.WriteAllText(configPath,
                "{\n" +
                "  \"host\": \"localhost:38281\",\n" +
                "  \"slot\": \"\",\n" +
                "  \"password\": \"\"\n" +
                "}");
            Log.LogWarning("Created connection.json - fill in connection details");
            return;
        }

        try
        {
            string json = File.ReadAllText(configPath);
            JObject data = JObject.Parse(json);

            string host = data["host"]?.ToString() ?? "localhost:38281";
            string slot = data["slot"]?.ToString() ?? "";
            string password = data["password"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(slot))
            {
                Log.LogWarning("Slot name is empty in connectionl.json - cannot connect.");
                return;
            }

            ArchipelagoClient.Connect(
                host,
                slot,
                string.IsNullOrEmpty(password) ? null : password
            );
        }
        catch (Exception e)
        {
            Log.LogError($"Failed to read connection.json: {e.Message}");
            return;
        }

        Log.LogInfo("Mod Loaded!");

        // apply harmony patches
        Harmony harmony = new Harmony("com.kodbyte.seafantasyap");
        harmony.PatchAll();
    }

    public static void CreateMessageUI()
    {
        var canvas = GameObject.Find("GameCanvas");
        if (canvas == null)
        {
            Log.LogError("Could not find GameCanvas");
            return;
        }

        var msgObj = new GameObject("APMessageText");
        msgObj.transform.SetParent(canvas.transform, false);

        // CanvasGroup with ignoreParentGroups handles visibility
        var canvasGroup = msgObj.AddComponent<CanvasGroup>();
        canvasGroup.ignoreParentGroups = true;

        var allTexts = canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true);
        var targetText = allTexts.FirstOrDefault(t => t.font != null && t.font.name == "fusion-pixel-12px-monospaced-ja");

        var text = msgObj.AddComponent<UnityEngine.UI.Text>();
        if (targetText != null)
            text.font = targetText.font;
        text.fontSize = 32;
        text.color = Color.yellow;
        text.alignment = TextAnchor.LowerLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var rect = msgObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.offsetMin = new Vector2(10, 10);
        rect.offsetMax = new Vector2(-10, 60);

        MessageText = text;
        Log.LogInfo("AP message UI created successfully");
    }
    public static void LoadAPLogo()
    {
        string logoPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "ap-logo.png");
        if (!File.Exists(logoPath))
        {
            Log.LogWarning("ap-logo.png not found!");
            return;
        }

        try
        {
            var bitmap = new System.Drawing.Bitmap(logoPath);
            var texture = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.RGBA32, false);

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, bitmap.Height - 1 - y);
                    texture.SetPixel(x, y, new Color32(pixel.R, pixel.G, pixel.B, pixel.A));
                }
            }
            texture.Apply();

            APLogoSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            Log.LogInfo("AP logo loaded successfully");
        }
        catch (Exception e)
        {
            Log.LogWarning($"Failed to load AP logo: {e.Message}");
        }
    }
}

[HarmonyPatch(typeof(UIManager), "Update")]
public class UIManagerUpdatePatch
{
    static void Postfix(UIManager __instance)
    {
        if (!ArchipelagoClient.Connected) return;
        if (SeaFantasyAP.UIManagerInstance == null) return;
        
        if (!SeaFantasyAP.IsFishingActive)
        {
            ItemHandler.ProcessItemQueue(__instance);
        }

        if (SeaFantasyAP.MessageText == null) return;

        if (SeaFantasyAP.messageTimer > 0f)
        {
            SeaFantasyAP.messageTimer -= Time.deltaTime;
            if (SeaFantasyAP.messageTimer <= 0f)
            {
                SeaFantasyAP.MessageText.text = "";

                if (SeaFantasyAP.MessageQueue.Count > 0)
                {
                    SeaFantasyAP.MessageText.text = SeaFantasyAP.MessageQueue.Dequeue();
                    SeaFantasyAP.messageTimer = SeaFantasyAP.MESSAGE_DURATION;
                }
            }
        }
        else if (SeaFantasyAP.MessageQueue.Count > 0)
        {
            SeaFantasyAP.MessageText.text = SeaFantasyAP.MessageQueue.Dequeue();
            SeaFantasyAP.messageTimer = SeaFantasyAP.MESSAGE_DURATION;
        }
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_NewItemWindow")]
public class NewItemWindowPatch
{
    static bool Prefix(UIManager __instance, ref EN_ITEM_TYPE type, ref int id, ref int sub_id)
    {
        if (!ArchipelagoClient.Connected) return true;

        if (SeaFantasyAP.IsOpeningChest)
        {
            type = EN_ITEM_TYPE.GOLD;
            return true;
        }

        long locationId = GetLocationId(type, id);
        if (locationId == -1)
        {
            SeaFantasyAP.IsLocationPickup = false;
            SeaFantasyAP.LastLocationId = -1;
            return true;
        }

        SeaFantasyAP.LastLocationId = locationId;
        SeaFantasyAP.IsLocationPickup = true;
        type = EN_ITEM_TYPE.GOLD;
        return true;
    }
    static void Postfix(UIManager __instance)
    {
        if (!ArchipelagoClient.Connected) return;

        SeaFantasyAP.Log.LogInfo($"NewItemWindow Postfix: IsOpeningChest={SeaFantasyAP.IsOpeningChest} | IsLocationPickup={SeaFantasyAP.IsLocationPickup} | LastLocaationId={SeaFantasyAP.LastLocationId}");

        long locationId = -1;
        if (SeaFantasyAP.IsOpeningChest || SeaFantasyAP.IsLocationPickup)
            locationId = SeaFantasyAP.LastLocationId;

        SeaFantasyAP.Log.LogInfo($"Looking up locationId={locationId}");

        if (locationId == -1) return;

        if (ArchipelagoClient.ScoutedLocations.TryGetValue(locationId, out var itemInfo))
        {
            SeaFantasyAP.Log.LogInfo($"Found: {itemInfo.ItemName} for {itemInfo.Player.Name}");
            var newItemText = AccessTools.Field(typeof(UIManager), "m_NewItemText").GetValue(__instance) as UnityEngine.UI.Text;
            var newItemSubText = AccessTools.Field(typeof(UIManager), "m_NewItemSubText").GetValue(__instance) as UnityEngine.UI.Text;
            var newItemImage = AccessTools.Field(typeof(UIManager), "m_NewItemImage").GetValue(__instance) as UnityEngine.UI.Image; 

            if (newItemText != null)
                newItemText.text = $"Found {itemInfo.ItemName} for\n {itemInfo.Player.Name}!";
            if (newItemSubText != null)
                newItemSubText.text = "Location Checked!";
            if (newItemImage != null && SeaFantasyAP.APLogoSprite != null)
                newItemImage.sprite = SeaFantasyAP.APLogoSprite;
        }
        else
        {
            SeaFantasyAP.Log.LogInfo($"No scout info found for {locationId}");
        }
        SeaFantasyAP.IsLocationPickup = false;
        SeaFantasyAP.IsOpeningChest = false;
    }
    private static long GetLocationId(EN_ITEM_TYPE type, int id)
    {
        SeaFantasyAP.Log.LogInfo($"GetLocationID: type={type} | type==ITEM: {type == EN_ITEM_TYPE.ITEM} | id={id} | inSet={AddItemPatch.LocationSpIds.Contains(id)}");
        if (type == EN_ITEM_TYPE.ITEM && AddItemPatch.LocationSpIds.Contains(id))
            return SeaFantasyAP.LOC_ITEM + id;
        if (type == EN_ITEM_TYPE.ROD && AddRodPatch.LocationRodIds.Contains(id))
            return SeaFantasyAP.LOC_ROD + id;
        if (type == EN_ITEM_TYPE.DRESS && AddDressPatch.LocationOutfitIds.Contains(id))
            return SeaFantasyAP.LOC_OUTFIT + id;
        return -1;
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_Init")]
public class UIManagerInitPatch
{
    static void Postfix(UIManager __instance)
    {
        SeaFantasyAP.UIManagerInstance = __instance;
        SeaFantasyAP.CreateMessageUI();
    }
}

[HarmonyPatch(typeof(MainPlayerController), "PUB_Init_MainPlayerController")]
public class MainPlayerControllerInitPatch
{
    static void Postfix(MainPlayerController __instance)
    {
        SeaFantasyAP.PlayerControllerInstance = __instance;
    }
}

[HarmonyPatch(typeof(TitleWindow), "PUB_Init")]
public class TitleWindowInitPatch
{
    static void Postfix(TitleWindow __instance)
    {
        var verText = AccessTools.Field(typeof(TitleWindow), "m_VerText").GetValue(__instance) as UnityEngine.UI.Text;
        if (verText == null) return;

        if (ArchipelagoClient.Connected)
            verText.text += $" | <color=green>AP: Connected</color>";
        else
            verText.text += " | <color=red>AP: Not Connected</color>";
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_CatchState")]
public class CatchStatePatch
{
    static void Prefix(EN_PL_ACTION_STATE type)
    {
        if (type == EN_PL_ACTION_STATE.EN01_FISHING ||
            type == EN_PL_ACTION_STATE.EN02_FISHING_WAITE ||
            type == EN_PL_ACTION_STATE.EN03_FISHING_BATTLE)
        {
            SeaFantasyAP.IsFishingActive = true;
        }
        else
        {
            SeaFantasyAP.IsFishingActive = false;
        }
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_CatchEventMode")]
public class CatchEventModePatch
{
    static void Prefix(bool mode)
    {
        SeaFantasyAP.IsCutsceneActive = mode;

        if (SeaFantasyAP.MessageText != null)
        {
            var canvasGroup = SeaFantasyAP.MessageText.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
            SeaFantasyAP.MessageText.gameObject.SetActive(true);
        }
    }
}

[HarmonyPatch(typeof(MainGameManager), "PUB_FS_FishingOK")]
public class FishingOKPatch
{
    static void Prefix(MainGameManager __instance)
    {
        var fishPara = __instance.PUB_Get_FishPara();

        SeaFantasyAP.Log.LogInfo($"Fish caught! Kind: {fishPara.m_KindFish} | ID: {(int)fishPara.m_KindFish}");
        SeaFantasyAP.Log.LogInfo($"Size: {fishPara.m_FishSize}cm (category {fishPara.m_KindFishSize})");
        SeaFantasyAP.Log.LogInfo($"Stage: {fishPara.m_StageNum} | Boss: {fishPara.m_FlagBoss}");

        if (!ArchipelagoClient.Connected) return;

        long locationId = SeaFantasyAP.LOC_FISH + (int)fishPara.m_KindFish;

        if (ArchipelagoClient.IsLocationChecked(locationId)) return;
        ArchipelagoClient.SendLocation(locationId);

        // check for muddy catch for goal complete
        if (ArchipelagoClient.Goal == 1 && (int)fishPara.m_KindFish == 25)
            ArchipelagoClient.SendGoalCompletion();
    }
}

[HarmonyPatch(typeof(FishMakerManager), "PUB_Success_OUGONGAERU")]
public class GoldFrogPatch
{
    static void Prefix(int AreaIdx)
    {
        SeaFantasyAP.Log.LogInfo($"FrogIndex: id={AreaIdx}");

        if (!ArchipelagoClient.Connected) return;

        long locationId = SeaFantasyAP.LOC_FROG + AreaIdx;
        ArchipelagoClient.SendLocation(locationId);
    }
}

[HarmonyPatch(typeof(ChestController), "PUB_Exec")]
public class ChestOpenPatch
{
    static void Prefix(ChestController __instance)
    {
        var mType = AccessTools.Field(typeof(ChestController), "m_Type").GetValue(__instance);
        var mID = AccessTools.Field(typeof(ChestController), "m_ID").GetValue(__instance);
        var mMakaiChest = AccessTools.Field(typeof(ChestController), "m_MakaiChest").GetValue (__instance);

        SeaFantasyAP.Log.LogInfo($"m_Type = {mType}");
        SeaFantasyAP.Log.LogInfo($"m_ID = {mID}");
        SeaFantasyAP.Log.LogInfo($"m_MakaiChest = {mMakaiChest}");

        if (!ArchipelagoClient.Connected) return;

        long locationId = SeaFantasyAP.LOC_CHEST + Convert.ToInt64(mID);
        ArchipelagoClient.SendLocation(locationId);

        SeaFantasyAP.IsOpeningChest = true;
        SeaFantasyAP.LastLocationId = locationId;
    }
    
    static void Postfix()
    {
        SeaFantasyAP.IsOpeningChest = false;
        SeaFantasyAP.LastLocationId = -1;
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_AddRod")]
public class AddRodPatch
{
    public static readonly HashSet<int> LocationRodIds = new HashSet<int> { 15 };
    static bool Prefix(int id, int num)
    {
        SeaFantasyAP.Log.LogInfo($"rod_type: id={id}");

        if (ItemHandler.IsGrantingItem) return true;
        if (!ArchipelagoClient.Connected) return true;
        if (SeaFantasyAP.IsOpeningChest) return false;

        if (LocationRodIds.Contains(id))
        {
            long locationId = SeaFantasyAP.LOC_ROD + id;
            SeaFantasyAP.IsLocationPickup = true;
            SeaFantasyAP.LastLocationId = locationId;
            ArchipelagoClient.SendLocation(locationId);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_AddBite")]
public class AddBitePatch
{
    static bool Prefix(int id, int num)
    {
        if (SeaFantasyAP.IsOpeningChest) return false;
        SeaFantasyAP.Log.LogInfo($"AddBite: bite_type={id} | num={num}");
        return true;
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_AddDress")]
public class AddDressPatch
{
    public static readonly HashSet<int> LocationOutfitIds = new HashSet<int> { 1 };
    static bool Prefix(int id)
    {
        SeaFantasyAP.Log.LogInfo($"AddDress: id={id}");

        if (ItemHandler.IsGrantingItem) return true;
        if (!ArchipelagoClient.Connected) return true;
        if (SeaFantasyAP.IsOpeningChest) return false;

        if (LocationOutfitIds.Contains(id))
        {
            SeaFantasyAP.Log.LogInfo($"Outfit id={id} is a location, sending to AP.");
            long locationId = SeaFantasyAP.LOC_OUTFIT + id;
            SeaFantasyAP.IsLocationPickup = true;
            SeaFantasyAP.LastLocationId = locationId;
            ArchipelagoClient.SendLocation(locationId);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_AddPotion")]
public class AddPotionPatch
{
    static bool Prefix(int id, int num)
    {
        if (SeaFantasyAP.IsOpeningChest) return false;
        SeaFantasyAP.Log.LogInfo($"AddPotion: potion_type={id} | num={num}");
        return true;
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_SendGold")]
public class SendGoldPatch
{
    static bool Prefix(ObscuredInt add_gold)
    {
        if (SeaFantasyAP.IsOpeningChest) return false;
        SeaFantasyAP.Log.LogInfo($"SendGold: num={add_gold}");
        return true;
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_AddCharm")]
public class AddCharmPatch
{
    static bool Prefix(ST_GET_CHARM_DATA data)
    {
        if (SeaFantasyAP.IsOpeningChest) return false;
        SeaFantasyAP.Log.LogInfo($"AddCharm: rare={data.rare} | hp={data.hp} | atk={data.atk}");
        SeaFantasyAP.Log.LogInfo($"vit={data.vit} | tec={data.tec} | eye={data.eye} | luc={data.luc}");
        return true;
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_AddItem")]
public class AddItemPatch
{
    public static readonly HashSet<int> LocationSpIds = new HashSet<int> { 0, 5, 6, 9, 12, 14, 15, 16, 17, 18, 19, 20, 45 };
    static bool Prefix(int id)
    {
        SeaFantasyAP.Log.LogInfo($"PUB_AddItem: id={id} | IsGranting={ItemHandler.IsGrantingItem} | Connected={ArchipelagoClient.Connected} | IsLocation={LocationSpIds.Contains(id)}");

        if (ItemHandler.IsGrantingItem) return true;
        if (!ArchipelagoClient.Connected) return true;
        if (SeaFantasyAP.IsOpeningChest) return false;

        if (LocationSpIds.Contains(id))
        {
            long locationId = SeaFantasyAP.LOC_ITEM + id;
            SeaFantasyAP.IsLocationPickup = true;
            SeaFantasyAP.LastLocationId = locationId;
            SeaFantasyAP.Log.LogInfo($"Sending location {locationId}");
            ArchipelagoClient.SendLocation(locationId);
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(CraftWindow), "CraftExec")]
public class CraftExecPatch
{
    static void Prefix(CraftWindow __instance)
    {
        
    }
}

[HarmonyPatch(typeof(StoryManager), "StorySeq_Skip_cp")]
public class StorySkipPatch
{
    static void Prefix(StoryManager __instance)
    {
        var localnumcp = (EN_CP_NUM)AccessTools.Field(typeof(StoryManager), "m_Localnumcp").GetValue(__instance);
        if (!ArchipelagoClient.Connected) return;

        if (localnumcp == EN_CP_NUM.CP3_11)
        {
            SeaFantasyAP.Log.LogInfo("StorySeq_Skip_cp CP3_11 - sending location check");
            ArchipelagoClient.SendLocation(SeaFantasyAP.LOC_ITEM + 5); // item from gold dragon
        }

        if (localnumcp == EN_CP_NUM.CP4_6)
        {
            SeaFantasyAP.Log.LogInfo("StorySeq_Skip_cp CP4_6 - sending location checks");
            ArchipelagoClient.SendLocation(SeaFantasyAP.LOC_ITEM + 9); // first item from Gwen
            ArchipelagoClient.SendLocation(SeaFantasyAP.LOC_ITEM + 6); // item from Bacchus
        }
    }
}

[HarmonyPatch(typeof(SubStoryManager), "PUB_SubStorySeq_Skip_cp")]
public class SubstorySkipPatch
{
    static void Prefix(SubStoryManager __instance)
    {
        var subev_enum = (EN_STORY_SUBEV)AccessTools.Field(typeof(SubStoryManager), "m_SUBEV_Enum").GetValue(__instance);
        if (!ArchipelagoClient.Connected) return;

        if (subev_enum == EN_STORY_SUBEV.OUGONGAERU_INTRO)
        {
            SeaFantasyAP.Log.LogInfo("Frog event skipped: Sending location check");
            ArchipelagoClient.SendLocation(SeaFantasyAP.LOC_ROD + 15);
        }
    }
}