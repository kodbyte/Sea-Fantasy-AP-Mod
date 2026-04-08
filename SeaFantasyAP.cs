using BepInEx;
using BepInEx.Logging;
using CodeStage.AntiCheat.ObscuredTypes;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[BepInPlugin("com.kodbyte.seafantasyap", "Sea Fantasy AP", "0.0.1")]
public class SeaFantasyAP : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    public static UIManager UIManagerInstance;
    public static MainPlayerController PlayerControllerInstance;
    public static bool IsFishingActive = false;
    public static bool IsOpeningChest = false;

    private static string configPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
        "connection.json"
    );

    private void Awake()
    {
        Log = Logger;
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
}

[HarmonyPatch(typeof(UIManager), "Update")]
public class UIManagerUpdatePatch
{
    static void Postfix()
    {
        if (!ArchipelagoClient.Connected) return;
        if (SeaFantasyAP.UIManagerInstance == null) return;
        if (SeaFantasyAP.IsFishingActive) return;

        ItemHandler.ProcessItemQueue(SeaFantasyAP.UIManagerInstance);
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_Init")]
public class UIManagerInitPatch
{
    static void Postfix(UIManager __instance)
    {
        SeaFantasyAP.UIManagerInstance = __instance;
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

        long locationId = 10000 + (int)fishPara.m_KindFish;

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

        long locationId = 80000 + AreaIdx;
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

        long locationId = 20000 + Convert.ToInt64(mID);
        ArchipelagoClient.SendLocation(locationId);

        SeaFantasyAP.IsOpeningChest = true;
    }
    
    static void Postfix()
    {
        SeaFantasyAP.IsOpeningChest = false;
    }
}

[HarmonyPatch(typeof(UIManager), "PUB_AddRod")]
public class AddRodPatch
{
    private static readonly HashSet<int> LocationRodIds = new HashSet<int> { 15 };
    static bool Prefix(int id, int num)
    {
        SeaFantasyAP.Log.LogInfo($"rod_type: id={id}");

        if (ItemHandler.IsGrantingItem) return true;
        if (!ArchipelagoClient.Connected) return true;
        if (SeaFantasyAP.IsOpeningChest) return false;

        if (LocationRodIds.Contains(id))
        {
            long locationId = 40000 + id;
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
    private static readonly HashSet<int> LocationOutfitIds = new HashSet<int> { 1 };
    static bool Prefix(int id)
    {
        SeaFantasyAP.Log.LogInfo($"AddDress: id={id}");

        if (ItemHandler.IsGrantingItem) return true;
        if (!ArchipelagoClient.Connected) return true;
        if (SeaFantasyAP.IsOpeningChest) return false;

        if (LocationOutfitIds.Contains(id))
        {
            SeaFantasyAP.Log.LogInfo($"Outfit id={id} is a location, sending to AP.");
            long locationId = 70000 + id;
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
    private static readonly HashSet<int> LocationSpIds = new HashSet<int> { 0, 5, 6, 9, 12, 14, 15, 16, 17, 18, 19, 20, 45 };
    static bool Prefix(int id)
    {
        SeaFantasyAP.Log.LogInfo($"PUB_AddItem: id={id} | IsGranting={ItemHandler.IsGrantingItem} | Connected={ArchipelagoClient.Connected} | IsLocation={LocationSpIds.Contains(id)}");

        if (ItemHandler.IsGrantingItem) return true;
        if (!ArchipelagoClient.Connected) return true;
        if (SeaFantasyAP.IsOpeningChest) return false;

        if (LocationSpIds.Contains(id))
        {
            long locationId = 30000 + id;
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
        var localnumcp = AccessTools.Field(typeof(StoryManager), "m_Localnumcp").GetValue(__instance);
        if (!ArchipelagoClient.Connected) return;

        if (localnumcp.ToString() == "CP3_11")
        {
            SeaFantasyAP.Log.LogInfo("StorySeq_Skip_cp CP3_11 - sending location check");
            ArchipelagoClient.SendLocation(30000 + 5); // item from gold dragon
        }

        if (localnumcp.ToString() == "CP4_6")
        {
            SeaFantasyAP.Log.LogInfo("StorySeq_Skip_cp CP4_6 - sending location checks");
            ArchipelagoClient.SendLocation(30000 + 9); // first item from Gwen
            ArchipelagoClient.SendLocation(30000 + 6); // item from Bacchus
        }
    }
}