using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PhotonRoomPoller : MonoBehaviourPunCallbacks
{
    // Creates a second Photon peer to poll online room counts info.
    // A second peer is necessary as one otherwise while in a Room can't join
    // the Lobby, needed to get the room list. API at
    // doc-api.photonengine.com/en/pun/v2/class_photon_1_1_realtime_1_1_load_balancing_client.html
    // https://stackoverflow.com/questions/57366704/polling-for-available-unity-photon-rooms-with-loadbalancingclient-while-not-in-l

    Action<List<RoomInfo>> callback = null;
    LoadBalancingClient client = null;
    public ServerSettings photonServerSettings;
    public TypedLobby defaultLobby;

    public void GetRoomsInfo(Action<List<RoomInfo>> callback)
    {
        this.callback = callback;

        client = new LoadBalancingClient();
        client.AddCallbackTarget(this);
        client.StateChanged += OnStateChanged;
        client.AppId = PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime;
        client.AppVersion = PhotonNetwork.NetworkingClient.AppVersion;
        client.ConnectToRegionMaster("au");
    }

    void Update()
    {
        if (client != null)
        {
            client.Service();
        }
    }

    void OnStateChanged(ClientState previousState, ClientState state)
    {
        if (state == ClientState.ConnectedToMasterServer)
        {
            client.OpJoinLobby(defaultLobby);
        }
    }

    public override void OnRoomListUpdate(List<RoomInfo> infos)
    {
        if (callback != null)
        {
            callback(infos);
        }

        client.Disconnect();
    }

}