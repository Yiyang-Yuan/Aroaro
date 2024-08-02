using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.UI;
using System.Collections;
using System;
using static PlayFabAuthenticator;
using System.Threading.Tasks;

public class MultiuserManager : MonoBehaviourPunCallbacks
{
    [HideInInspector]
    public string _roomName = "Common Space ";
    public const string _defaultRoomName = "Common Space ";
    public PlayFabAuthenticator playFabAuthenticator;
    public GridObjectCollection roomsListCollection;
    public InteractableToggleCollection roomsListToggleCollection;
    public GameObject radioButtonPrefab;
    public Transform roomsListContainer;
    public Transform roomsListQuad;
    public GameObject roomsListNoRoomsText;
    public NotesManager notesManager;
    public Avatar myAvatar;
    public string cachedAvatarID;
    public SkyboxGenerator skyboxGenerator;
    public TextMeshPro roomNameLabel;
    public VoiceManager voiceManager;
    public NetworkConnectionManager networkConnectionManager;
    public PhotonRoomPoller photonRoomPollerObj;
    public DatabaseEventLogger databaseEventLogger;
    private TypedLobby defaultLobby;

    private async Task Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.KeepAliveInBackground = PlayerPrefs.GetInt("TimeoutTime", 1200);
        PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion = Application.version;

        while (networkConnectionManager.networkConnectionTested != true)
        {
            await Task.Delay(25);
        }

        if (!networkConnectionManager.networkConnectedAndReceiving)
        {
            playFabAuthenticator.LocalUserConnectionStatus = UserConnectionStatus.Offline;
            databaseEventLogger.LogPhotonConnectionChanged(UserConnectionStatus.Offline);
        }
    }

    private void InstantiateAvatar()
    {
        playFabAuthenticator.handMenu.thirdPersonCamera.ReturnToFirstPerson();
        myAvatar = PhotonNetwork.Instantiate("Avatar/Prefabs/Avatar", Vector3.zero, Quaternion.identity).GetComponent<Avatar>();
        myAvatar.playerCamera = Camera.main.gameObject;
        myAvatar.handMenu = playFabAuthenticator.handMenu;
        voiceManager.avatarAudio = myAvatar.gameObject.GetComponent<AudioSource>();

        if (!string.IsNullOrEmpty(cachedAvatarID))
        {
            myAvatar.LoadAvatarFromPlayFab(cachedAvatarID);
        }
    }

    public void ConnectToLobby()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.JoinLobby();
        }

        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnJoinedLobby()
    {
        if (_roomName == _defaultRoomName)
        {
            CreateRoom(_defaultRoomName);
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        playFabAuthenticator.LocalUserConnectionStatus = UserConnectionStatus.Private;
        databaseEventLogger.LogPhotonConnectionChanged(UserConnectionStatus.Private);
        cachedAvatarID = null;
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        UpdateRoomList(roomList);
    }

    public void ConnectToRoom()
    {
        Debug.Log("ConnectToRoom");

        _roomName = roomsListContainer.GetChild(roomsListToggleCollection.CurrentIndex).GetComponent<MRTKRadioButton>().label.text.Replace("(Current Room)", "");

        if (_roomName.Replace(" ", "") != PhotonNetwork.CurrentRoom.Name.Replace(" ", ""))
        {
            playFabAuthenticator.LocalUserConnectionStatus = UserConnectionStatus.ConnectingToRoom;
            databaseEventLogger.LogPhotonConnectionChanged(UserConnectionStatus.ConnectingToRoom, new DBLogger { User = PhotonNetwork.LocalPlayer });
            playFabAuthenticator.handMenu.ChangeWindow("social");

            if (PhotonNetwork.InRoom)
            {
                StartCoroutine(WaitToLeaveRoom(_roomName));
            }
        }
    }

    public void CreateRoom(string roomName)
    {
        if (PhotonNetwork.InRoom)
        {
            StartCoroutine(WaitToLeaveRoom(roomName));
        }

        else
        {
            playFabAuthenticator.LocalUserConnectionStatus = UserConnectionStatus.ConnectingToRoom;
            databaseEventLogger.LogPhotonConnectionChanged(UserConnectionStatus.ConnectingToRoom, new DBLogger { User = PhotonNetwork.LocalPlayer });
            // CleanupCacheOnLeave causes issues with avatar destruction when a user leaves a scene, disabled until this is fixed
            PhotonNetwork.JoinOrCreateRoom(roomName, new RoomOptions { MaxPlayers = 20, IsVisible = true, EmptyRoomTtl = 300000 }, defaultLobby);
        }
    }

    private IEnumerator WaitToLeaveRoom(string roomName)
    {
        while (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            yield return new WaitForSeconds(1);
        }

        while (!PhotonNetwork.InLobby)
        {
            yield return new WaitForSeconds(1);
        }

        CreateRoom(roomName);
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.GameVersion = Application.version;

        if (PhotonNetwork.OfflineMode == true)
        {
            if (!networkConnectionManager.networkConnectedAndReceiving)
            {
                playFabAuthenticator.LocalUserConnectionStatus = UserConnectionStatus.Offline;
                databaseEventLogger.LogPhotonConnectionChanged(UserConnectionStatus.Offline);
            }

            else
            {
                playFabAuthenticator.LocalUserConnectionStatus = UserConnectionStatus.Private;
                databaseEventLogger.LogPhotonConnectionChanged(UserConnectionStatus.Private);
            }

            PhotonNetwork.CreateRoom(null);
        }

        else
        {
            defaultLobby ??= new TypedLobby("Default", LobbyType.Default);

            PhotonNetwork.JoinLobby(defaultLobby);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        // User already in room error, prevents infinite looping
        if (returnCode == 32750)
        {
            playFabAuthenticator.DisplayToastMessage(message);
            playFabAuthenticator.handMenu.ChangeWindow("login");
        }

        else
        {
            playFabAuthenticator.LocalUserConnectionStatus = UserConnectionStatus.ConnectingToRoom;
            databaseEventLogger.LogPhotonConnectionChanged(UserConnectionStatus.ConnectingToRoom, new DBLogger { User = PhotonNetwork.LocalPlayer });

            PhotonNetwork.JoinOrCreateRoom(_roomName, new RoomOptions { MaxPlayers = 20, IsVisible = true}, defaultLobby);
        }
    }

    public override void OnJoinedRoom()
    {
        if (playFabAuthenticator.LocalUserConnectionStatus != UserConnectionStatus.Offline && playFabAuthenticator.LocalUserConnectionStatus != UserConnectionStatus.Private)
        {
            // Create Space if it doesn't exist in db yet
            databaseEventLogger.dbManager.CreateSpace(PhotonNetwork.CurrentRoom.Name);

            if (PhotonNetwork.CurrentRoom.Name == _defaultRoomName)
            {
                playFabAuthenticator.LocalUserConnectionStatus = UserConnectionStatus.ConnectedToDefaultRoom;
                databaseEventLogger.LogPhotonConnectionChanged(UserConnectionStatus.ConnectedToDefaultRoom, new DBLogger { User = PhotonNetwork.LocalPlayer, Room = PhotonNetwork.CurrentRoom });
            }

            else
            {
                playFabAuthenticator.LocalUserConnectionStatus = UserConnectionStatus.ConnectedToRoom;
                databaseEventLogger.LogPhotonConnectionChanged(UserConnectionStatus.ConnectedToRoom, new DBLogger { User = PhotonNetwork.LocalPlayer, Room = PhotonNetwork.CurrentRoom });
            }
        }

        UpdatePlayerList();
        InstantiateAvatar();

        if (!PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);
        }

        if (networkConnectionManager.networkConnectedAndReceiving)
        {
            notesManager.LoadNotes();
        }

        // Update from AwaitingLogin -> Home window
        if (playFabAuthenticator.handMenu.currentWindow == HandMenu.WindowType.AwaitingLogin)
        {
            playFabAuthenticator.handMenu.ChangeWindow("home");
        }

        if (skyboxGenerator.useRoomBasedSkyboxes)
        {
            if (!PhotonNetwork.OfflineMode)
            {
                skyboxGenerator.SetSkyboxColour(_roomName);
            }
        }

        if (!PhotonNetwork.OfflineMode)
        {
            playFabAuthenticator.handMenu.roomInfoPanel.SetActive(true);
        }

        // Prevents artifacts from objects instantiated by clients before the current client joined
        StartCoroutine(SubscribeToInterestGroup());
    }

    private IEnumerator SubscribeToInterestGroup()
    {
        yield return new WaitForSeconds(1f);
        PhotonNetwork.SetInterestGroups(1, true);
    }

    public override void OnLeftRoom()
    {
        playFabAuthenticator.LocalUserConnectionStatus = UserConnectionStatus.LeavingRoom;
        databaseEventLogger.LogPhotonConnectionChanged(UserConnectionStatus.LeavingRoom);
        UpdatePlayerList();
        playFabAuthenticator.handMenu.roomInfoPanel.SetActive(false);

        if (skyboxGenerator.useRoomBasedSkyboxes)
        {
            skyboxGenerator.ResetSkybox();
        }

        // Update from Home -> AwaitingLogin window
        if (playFabAuthenticator.handMenu.currentWindow == HandMenu.WindowType.Home)
        {
            playFabAuthenticator.handMenu.ChangeWindow("awaitinglogin");
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerList();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerList();
    }

    public void UpdatePlayerList()
    {
        List<string> users = new List<string>();

        for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
        {
            users.Add(PhotonNetwork.PlayerList[i].NickName);
        }

        UpdateRoomInfo(users);
    }

    private void UpdateRoomInfo(List<string> users)
    {
        string info = string.Empty;
        string username = string.Empty;

        try
        {
            info = $"Current Space: {PhotonNetwork.CurrentRoom.Name}\nSpace owned by: {PhotonNetwork.CurrentRoom.GetPlayer(PhotonNetwork.CurrentRoom.MasterClientId).NickName}\n\nUsers in Space\n";

        }

        catch (NullReferenceException)
        {
            info = $"Currently in Lobby";
        }

        foreach (string user in users)
        {
            info += $"{user}\n";
            notesManager.roomInfoUsername = user;
        }

        playFabAuthenticator.roomInfoText.text = info;
        playFabAuthenticator.handMenu.roomInfoPanel.GetComponent<TextPanelResizer>().ResizePanel();
    }

    public void UpdateRoomListWhileInRoom()
    {
        PhotonRoomPoller roomPoller = Instantiate(photonRoomPollerObj, transform);
        roomPoller.defaultLobby = defaultLobby;

        roomPoller.GetRoomsInfo((roomInfos) =>
        {
            UpdateRoomList(roomInfos);
            Destroy(roomPoller.gameObject);
        });
    }

    private void UpdateRoomList(List<RoomInfo> roomList)
    {
        foreach (Transform child in roomsListContainer)
        {
            Destroy(child.gameObject);
        }

        roomsListToggleCollection.ToggleList = null;
        roomsListToggleCollection.OnSelectionEvents.RemoveAllListeners();

        if (roomList.Count == 0)
        {
            roomsListNoRoomsText.SetActive(true);
        }

        else
        {
            roomsListNoRoomsText.SetActive(false);

            List<Interactable> interactables = new List<Interactable>();

            foreach (RoomInfo room in roomList)
            {
                MRTKRadioButton radioButton = Instantiate(radioButtonPrefab, roomsListContainer).GetComponent<MRTKRadioButton>();

                if (PhotonNetwork.InRoom && room.Name == PhotonNetwork.CurrentRoom.Name)
                {
                    radioButton.label.text = $"{room.Name} (Current Room)";
                }

                else
                {
                    radioButton.label.text = room.Name;
                }

                interactables.Add(radioButton.interactable);
            }

            roomsListToggleCollection.ToggleList = interactables.ToArray();
            roomsListToggleCollection.OnSelectionEvents.AddListener(delegate { ConnectToRoom(); });

            roomsListQuad.localScale = new Vector3(roomsListQuad.localScale.x, roomList.Count * 0.034f, roomsListQuad.localScale.z);
            StartCoroutine(UpdateRoomListGridObjectCollection());
        }
    }

    private IEnumerator UpdateRoomListGridObjectCollection()
    {
        yield return null;
        roomsListCollection.UpdateCollection();
    }
}
;
