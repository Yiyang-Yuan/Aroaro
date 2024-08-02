using Microsoft.MixedReality.Toolkit.UI;
using Photon.Pun;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlayFabAuthenticator : MonoBehaviour
{
    public enum UserConnectionStatus
    {
        Offline,
        Private,
        Authenticating,
        ConnectingToRoom,
        ConnectedToDefaultRoom,
        ConnectedToRoom,
        LeavingRoom
    }

    private string _playFabPlayerIdCache;
    public TMP_InputField loginUsername;
    public TMP_InputField loginPassword;
    public TMP_InputField registerUsername;
    public TMP_InputField registerPassword;
    public TextMeshPro statusText;
    public TextMeshPro roomInfoText;
    public MultiuserManager multiuserManager;
    public Interactable rememberMe;
    public HandMenu handMenu;
    public GameObject toastPrefab;
    public NetworkConnectionManager networkConnectionManager;
    public bool startInOfflineMode = true;
    public bool loggedInAsGuest = false;
    private UserConnectionStatus _localUserConnectionStatus;

    public UserConnectionStatus LocalUserConnectionStatus
    {
        get { return _localUserConnectionStatus; }

        set
        {
            _localUserConnectionStatus = value;
            UpdateSocialButtons(value);
        }
    }


    static readonly string[] RandomAvatarIds =
    {
        "64e2e0d758f50a12df56bbd1",
        "64e2e4d295439dfcf3f092b1",
        "64e2e4f658f50a12df56c098",
        "64e2e52095439dfcf3f09308",
        "64e2e54958f50a12df56c0fe",
        "64e2e56c58f50a12df56c140",
        "64e2e58dc603b299c00d8567",
        "64e2e5a458f50a12df56c1b4",
        "64e2e5bd95439dfcf3f093f1",
        "64e2e5e0c603b299c00d8604",
    };

    private async void Awake()
    {
        while (networkConnectionManager.networkConnectionTested != true)
        {
            await Task.Delay(25);
        }

        if (!networkConnectionManager.networkConnectedAndReceiving)
        {
            Debug.Log("No network connection detected, starting in offline mode");
            startInOfflineMode = true;
            LocalUserConnectionStatus = UserConnectionStatus.Offline;
        }

        LoadRememberMe();
    }

    private void LoadRememberMe()
    {
        if (startInOfflineMode)
        {
            PhotonNetwork.OfflineMode = true;

            // Populate remember me - in case the user wants to join an online room
            if (bool.Parse(PlayerPrefs.GetString("RememberMe", "false")))
            {
                loginUsername.text = PlayerPrefs.GetString("Username", "");
                loginPassword.text = PlayerPrefs.GetString("Password", "");
                rememberMe.IsToggled = true;
            }
        }

        else
        {
            if (bool.Parse(PlayerPrefs.GetString("RememberMe", "false")))
            {
                loginUsername.text = PlayerPrefs.GetString("Username", "");
                loginPassword.text = PlayerPrefs.GetString("Password", "");
                rememberMe.IsToggled = true;

                AuthenticateWithPlayFab();
            }
        }
    }

    public void SetRememberMe()
    {
        if (rememberMe.IsToggled)
        {
            PlayerPrefs.SetString("RememberMe", "true");
            PlayerPrefs.SetString("Username", loginUsername.text);
            PlayerPrefs.SetString("Password", loginPassword.text);
        }

        else
        {
            PlayerPrefs.SetString("RememberMe", "false");
            PlayerPrefs.SetString("Username", "");
            PlayerPrefs.SetString("Password", "");
        }
    }

    public void AuthenticateWithPlayFab()
    {
        LocalUserConnectionStatus = UserConnectionStatus.Authenticating;

        PlayFabClientAPI.LoginWithPlayFab(new LoginWithPlayFabRequest()
        {
            Username = loginUsername.text,
            Password = loginPassword.text,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetUserAccountInfo = true
            }
        }, OnLoginSuccess, OnLoginFailure);
    }

    public void AuthenticateAsGuest()
    {
        LocalUserConnectionStatus = UserConnectionStatus.Authenticating;

        string customID = PlayFabSettings.DeviceUniqueIdentifier.Length > 50
            ? PlayFabSettings.DeviceUniqueIdentifier.Substring(0, 50)
            : PlayFabSettings.DeviceUniqueIdentifier;

        PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest()
        {
            CreateAccount = true,
            CustomId = customID,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetUserAccountInfo = true
            }
        }, OnLoginSuccess, OnLoginFailure);
        loggedInAsGuest = true;
    }

    private static string GetRandomAvatarId()
    {
        int randInd = Random.Range(0, RandomAvatarIds.Length);
        return RandomAvatarIds[randInd];
    }

    private void OnLoginSuccess(LoginResult result)
    {
        //PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion = Application.version;
        PhotonNetwork.ConnectUsingSettings();

        if (networkConnectionManager.networkConnectedAndReceiving)
        {
            MixpanelLogger.LogLogin(result.InfoResultPayload.AccountInfo);
        }

        try
        {
            // If the user has a customID they logged in as a Guest and will not have a display name
            string customId = result.InfoResultPayload.AccountInfo.CustomIdInfo.CustomId;

            // Except Guest accounts can have display names, so we set it here
            if (result.InfoResultPayload.AccountInfo.TitleInfo.DisplayName != null)
            {
                SetPhotonNickname(result.InfoResultPayload.AccountInfo.TitleInfo.DisplayName);
            }

            else
            {
                SetPhotonNickname(string.Empty);
            }

            // If the user has a ReadyPlayerMe avatar set -> load it
            multiuserManager.cachedAvatarID =
                result.InfoResultPayload.AccountInfo.TitleInfo.AvatarUrl + GetRandomAvatarId();
        }

        catch (NullReferenceException)
        {
            SetPhotonNickname(result.InfoResultPayload.AccountInfo.TitleInfo.DisplayName);

            // If the user has a ReadyPlayerMe avatar set -> load it
            if (result.InfoResultPayload.AccountInfo.TitleInfo.AvatarUrl != null &&
                result.InfoResultPayload.AccountInfo.TitleInfo.AvatarUrl != "")
            {
                multiuserManager.cachedAvatarID = result.InfoResultPayload.AccountInfo.TitleInfo.AvatarUrl;
            }
        }
    }

    private void OnLoginFailure(PlayFabError obj)
    {
        LocalUserConnectionStatus = UserConnectionStatus.Private;
        DisplayToastMessage(obj.ErrorMessage);
        loginPassword.text = string.Empty;
        handMenu.ChangeWindow("login");
    }

    private void SetPhotonNickname(string playfabDisplayName)
    {
        if (playfabDisplayName == string.Empty)
        {
            System.Random random = new System.Random();
            PhotonNetwork.NickName = $"Guest-{PlayFabSettings.DeviceUniqueIdentifier.Substring(0, 9)}";
            UpdatePlayfabDisplayName(PhotonNetwork.NickName);
        }
        else
        {
            PhotonNetwork.NickName = playfabDisplayName;
        }
    }

    public void UpdatePlayfabDisplayName(string updatedDisplayName)
    {
        PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest()
        {
            DisplayName = updatedDisplayName
        }, OnUpdatedDisplayNameSuccess, OnUpdatedDisplayNameFailure);
    }

    private void OnUpdatedDisplayNameSuccess(UpdateUserTitleDisplayNameResult result)
    {
        Debug.Log("Successfully updated PlayFab displayname");
    }

    private void OnUpdatedDisplayNameFailure(PlayFabError error)
    {
        Debug.Log(error);
        Debug.Log("Unable to update displayname");
    }

    public void Register()
    {
        var registerRequest = new RegisterPlayFabUserRequest
        {
            TitleId = PlayFabSettings.staticSettings.TitleId,
            Username = registerUsername.text,
            DisplayName = registerUsername.text,
            Password = registerPassword.text,
            RequireBothUsernameAndEmail = false
        };

        PlayFabClientAPI.RegisterPlayFabUser(registerRequest, OnRegisterSuccess, OnRegisterFailure);
    }

    private void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        loginUsername.text = registerUsername.text;
        loginPassword.text = registerPassword.text;
        registerUsername.text = "";
        registerPassword.text = "";

        statusText.text = $"Status: SUCCESSFULLY REGISTERED {result.Username}";

        AuthenticateWithPlayFab();
    }

    private void OnRegisterFailure(PlayFabError obj)
    {
        Debug.LogError(obj.Error);
        Debug.LogError(obj.ErrorDetails);
        Debug.LogError(obj.ErrorMessage);
    }

    public void CreateRoom(string roomName)
    {
        multiuserManager._roomName = roomName;
        multiuserManager.CreateRoom(roomName);
    }

    public void LeaveRoom()
    {
        LocalUserConnectionStatus = UserConnectionStatus.LeavingRoom;
        multiuserManager._roomName = MultiuserManager._defaultRoomName;
        PhotonNetwork.LeaveRoom();
    }

    public void Logout()
    {
        LocalUserConnectionStatus = UserConnectionStatus.Private;

        if (networkConnectionManager.networkConnectedAndReceiving)
        {
            //MixpanelLogger.LogLogout();
        }

        handMenu.ResetAvatar();

        loggedInAsGuest = false;
        StartCoroutine(WaitForPhotonDisconnect());
    }

    private IEnumerator WaitForPhotonDisconnect()
    {
        PhotonNetwork.Disconnect();

        while (PhotonNetwork.IsConnected)
        {
            yield return new WaitForSeconds(1);
        }

        PhotonNetwork.OfflineMode = true;
    }

    private void UpdateSocialButtons(UserConnectionStatus status)
    {
        switch (status)
        {
            case UserConnectionStatus.Offline:
                statusText.text = "Login disabled due to no network connection";
                multiuserManager.roomNameLabel.text = "OFFLINE";
                handMenu.UpdateSocialButtonCollection(false, false, false);
                break;

            case UserConnectionStatus.Private:
                statusText.text = "Status: Private Room";
                multiuserManager.roomNameLabel.text = "Private Room";
                handMenu.UpdateSocialButtonCollection(true, true, false);
                break;

            case UserConnectionStatus.Authenticating:
                statusText.text = "Status: Authenticating";
                multiuserManager.roomNameLabel.text = "Authenticating";
                //handMenu.UpdateSocialButtonCollection(false, false);
                break;

            case UserConnectionStatus.ConnectingToRoom:
                statusText.text = "Status: Connecting to room";
                multiuserManager.roomNameLabel.text = "Connecting to room";
                //handMenu.UpdateSocialButtonCollection(true, false);
                break;

            case UserConnectionStatus.ConnectedToDefaultRoom:
                statusText.text = "Status: Connected to Common Space";
                multiuserManager.roomNameLabel.text = "Status: Connected to Common Space";
                handMenu.UpdateSocialButtonCollection(true, false, true);
                break;

            case UserConnectionStatus.ConnectedToRoom:
                statusText.text = $"Status: Connected to {PhotonNetwork.CurrentRoom.Name}";
                multiuserManager.roomNameLabel.text = $"Connected to {PhotonNetwork.CurrentRoom.Name}";
                handMenu.UpdateSocialButtonCollection(true, false, false);
                break;

            case UserConnectionStatus.LeavingRoom:
                //statusText.text = "Status: Leaving room";
                //multiuserManager.roomNameLabel.text = "Leaving room";
                //handMenu.UpdateSocialButtonCollection(true, false);
                break;
        }
    }

    public void DisplayToastMessage(string messageText)
    {
        CloseExistingToasts();
        Toast toast = Instantiate(toastPrefab, transform).GetComponent<Toast>();
        toast.SetLabel(messageText);
    }

    private void CloseExistingToasts()
    {
        // Close any existing messages
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }
}