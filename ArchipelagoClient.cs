using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using System;
using System.Collections.Concurrent;

public class ArchipelagoClient
{
    public static ArchipelagoSession Session { get; private set; }
    public static bool Connected { get; private set; }
    public static int Goal = 0;
    public static int ExtraFrogs = 0;

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
    }

    public static void OnItemReceived(ReceivedItemsHelper helper)
    {
        while (helper.Any())
        {
            ItemQueue.Enqueue(helper.DequeueItem());
        }
    }

    public static void SendGoalCompletion()
    {
        if (!Connected) return;
        Session.SetGoalAchieved();
        SeaFantasyAP.Log.LogInfo("Goal Completed!");
    }
}