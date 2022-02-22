using System;
using Unity.Netcode;
using Unity.Networking.Transport;
using UnityEngine;

public class GameNetworkManager: NetworkManager
{

    //public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
    //{
    //    GameObject player = (GameObject)Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
    //    player.GetComponent<Player>().color = Color.red;
    //    NetworkServer.AddPlayerForConnection(conn, player, playerControllerId);
    //}
}
