using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

public class ArchipelagoClient
{
    public static ArchipelagoSession Session { get; private set; }
    public static bool Connected { get; private set; }
    public static int Goal = 0;
    public static int ExtraFrogs = 0;
    public static Dictionary<long, ScoutedItemInfo> ScoutedLocations = new Dictionary<long, ScoutedItemInfo>(); 

    // Queue for received items
    public static ConcurrentQueue<ItemInfo> ItemQueue = new ConcurrentQueue<ItemInfo>();

    public static void Connect(string host, string slotName, string password = null)
    {
        Session = ArchipelagoSessionFactory.CreateSession(host);

        var result = Session.TryConnectAndLogin(
            "Sea Fantasy",
            slotName,
            ItemsHandlingFlags.AllItems,
            password: password
        );

        if (result.Successful)
        {
            Connected = true;
            SeaFantasyAP.Log.LogInfo($"Connected to Archipelago as {slotName}!");
            Session.Items.ItemReceived += OnItemReceived;

            // read slot data
            var slotData = Session.DataStorage.GetSlotData();
            if (slotData.ContainsKey("goal"))
                Goal = Convert.ToInt32(slotData["goal"]);
            if (slotData.ContainsKey("extra_frogs"))
                ExtraFrogs = Convert.ToInt32(slotData["extra_frogs"]);
            SeaFantasyAP.Log.LogInfo($"Goal: {Goal} | Extra Frogs: {ExtraFrogs}");

            // scout locations
            var allLocations = Session.Locations.AllLocations.ToArray();
            var scoutResult = Session.Locations.ScoutLocationsAsync(false, allLocations).Result;
            ScoutedLocations = scoutResult;
        }
        else
        {
            Connected = false;
            var failure = (LoginFailure)result;
            SeaFantasyAP.Log.LogError($"Failed to connect: {string.Join(", ", failure.Errors)}");
        }
    }

    public static bool IsLocationChecked(long locationId)
    {
        if (!Connected) return false;
        return Session.Locations.AllLocationsChecked.Contains(locationId);
    }

    public static void SendLocation(long locationID)
    {
        if (!Connected) return;
        SeaFantasyAP.Log.LogInfo($"Sending Location: {locationID}");
        Session.Locations.CompleteLocationChecks(locationID);

        if (ScoutedLocations.TryGetValue(locationID, out var itemInfo))
        {
            string itemName = itemInfo.ItemName;
            string playerName = itemInfo.Player.Name;
            SeaFantasyAP.Log.LogInfo($"Item: {itemName} | Player: {playerName}");
            if (locationID >= SeaFantasyAP.LOC_FISH && locationID < SeaFantasyAP.LOC_CHEST ||
                locationID >= SeaFantasyAP.LOC_FROG)
            {
                ShowMessage($"Found {itemName} for {playerName}!");
            }
        }
        else
        {
            SeaFantasyAP.Log.LogInfo($"No scount info found for location {locationID}");
        }
    }

    public static void OnItemReceived(ReceivedItemsHelper helper)
    {
        while (helper.Any())
        {
            var item = helper.DequeueItem();
            ItemQueue.Enqueue(item);

            if (item.Player != Session.ConnectionInfo.Slot)
            {
                string sender = Session.Players.GetPlayerName(item.Player);
                ShowMessage($"Received {item.ItemName} from {sender}!");
            }
        }
    }

    public static void ShowMessage(string message)
    {
        SeaFantasyAP.Log.LogInfo($"ShowMessage called: {message}");
        SeaFantasyAP.MessageQueue.Enqueue(message);
    }

    public static void SendGoalCompletion()
    {
        if (!Connected) return;
        Session.SetGoalAchieved();
        SeaFantasyAP.Log.LogInfo("Goal Completed!");
    }
}