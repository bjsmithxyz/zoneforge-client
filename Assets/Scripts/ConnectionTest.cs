using System;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

public class ConnectionTest : MonoBehaviour
{
    private DbConnection _conn;

    void Start()
    {
        Debug.Log("Connecting to SpacetimeDB...");

        _conn = DbConnection.Builder()
            .WithUri("http://localhost:3000")
            .WithDatabaseName("zoneforge-server")
            .OnConnect(OnConnect)
            .OnConnectError(OnConnectError)
            .OnDisconnect(OnDisconnect)
            .Build();
    }

    void Update()
    {
        _conn?.FrameTick();
    }

    void OnConnect(DbConnection conn, Identity identity, string token)
    {
        Debug.Log($"Connected! Identity: {identity}");

        conn.SubscriptionBuilder()
            .OnApplied(OnSubscriptionApplied)
            .Subscribe(new[] {
                "SELECT * FROM player",
                "SELECT * FROM zone",
                "SELECT * FROM entity_instance"
            });
    }

    void OnSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("Subscription applied — calling create_player reducer");
        ctx.Reducers.CreatePlayer("TestPlayer");
    }

    void OnConnectError(Exception e)
    {
        Debug.LogError($"Connection failed: {e.Message}");
    }

    void OnDisconnect(DbConnection conn, Exception e)
    {
        if (e != null)
            Debug.LogWarning($"Disconnected with error: {e.Message}");
        else
            Debug.Log("Disconnected cleanly");
    }
}