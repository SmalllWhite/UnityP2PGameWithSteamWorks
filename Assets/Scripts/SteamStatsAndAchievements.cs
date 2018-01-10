using UnityEngine;
using System.Collections;
using System.ComponentModel;
using System.Collections.Generic;
using Steamworks;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
// This is a port of StatsAndAchievements.cpp from SpaceWar, the official Steamworks Example.
class SteamStatsAndAchievements : MonoBehaviour {
    #region achievement
    private enum Achievement : int {
		ACH_WIN_ONE_GAME,
		ACH_WIN_100_GAMES,
		ACH_HEAVY_FIRE,
		ACH_TRAVEL_FAR_ACCUM,
		ACH_TRAVEL_FAR_SINGLE1,
	};

	private Achievement_t[] m_Achievements = new Achievement_t[] {
		new Achievement_t(Achievement.ACH_WIN_ONE_GAME, "Winner", ""),
		new Achievement_t(Achievement.ACH_WIN_100_GAMES, "Champion", ""),
		new Achievement_t(Achievement.ACH_TRAVEL_FAR_ACCUM, "Interstellar", ""),
		new Achievement_t(Achievement.ACH_TRAVEL_FAR_SINGLE1, "Orbiter", "")
	};
    #endregion

    #region steam账号基本信息
    // Our GameID
    private CGameID m_GameID;

	// Did we get the stats from Steam?
	private bool m_bRequestedStats;
	private bool m_bStatsValid;

	// Should we store stats this frame?
	private bool m_bStoreStats;

	// Current Stat details
	private float m_flGameFeetTraveled;
	private float m_ulTickCountGameStart;
	private double m_flGameDurationSeconds;

	// Persisted Stat details
	private int m_nTotalGamesPlayed;
	private int m_nTotalNumWins;
	private int m_nTotalNumLosses;
	private float m_flTotalFeetTraveled;
	private float m_flMaxFeetTraveled;
	private float m_flAverageSpeed;
    #endregion

    protected Callback<UserStatsReceived_t> m_UserStatsReceived;
	protected Callback<UserStatsStored_t> m_UserStatsStored;
	protected Callback<UserAchievementStored_t> m_UserAchievementStored;

    //添加的回调
    protected CallResult<LobbyCreated_t> m_LobbyCreated;
    protected CallResult<LobbyMatchList_t> m_LobbyMatchList;
    protected Callback<LobbyChatUpdate_t> m_LobbyChatUpdate;

    //杂变量
    protected CSteamID m_LobbyId;
    protected bool m_IsInLobby;

	void OnEnable() {
		if (!SteamManager.Initialized)
			return;

		// Cache the GameID for use in the Callbacks
		m_GameID = new CGameID(SteamUtils.GetAppID());

		m_UserStatsReceived = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
		m_UserStatsStored = Callback<UserStatsStored_t>.Create(OnUserStatsStored);
		m_UserAchievementStored = Callback<UserAchievementStored_t>.Create(OnAchievementStored);

        //添加的回调
        m_LobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
        m_LobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchedList);
        m_LobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdated);
		// These need to be reset to get the stats upon an Assembly reload in the Editor.
		m_bRequestedStats = false;
		m_bStatsValid = false;
	}

	private void Update() {
		if (!SteamManager.Initialized)
			return;

		if (!m_bRequestedStats) {
			// Is Steam Loaded? if no, can't get stats, done
			if (!SteamManager.Initialized) {
				m_bRequestedStats = true;
				return;
			}
			
			// If yes, request our stats
			bool bSuccess = SteamUserStats.RequestCurrentStats();

			// This function should only return false if we weren't logged in, and we already checked that.
			// But handle it being false again anyway, just ask again later.
			m_bRequestedStats = bSuccess;
		}

		if (!m_bStatsValid)
			return;

		// Get info from sources

		// Evaluate achievements
		foreach (Achievement_t achievement in m_Achievements) {
			if (achievement.m_bAchieved)
				continue;

			switch (achievement.m_eAchievementID) {
				case Achievement.ACH_WIN_ONE_GAME:
					if (m_nTotalNumWins != 0) {
						UnlockAchievement(achievement);
					}
					break;
				case Achievement.ACH_WIN_100_GAMES:
					if (m_nTotalNumWins >= 100) {
						UnlockAchievement(achievement);
					}
					break;
				case Achievement.ACH_TRAVEL_FAR_ACCUM:
					if (m_flTotalFeetTraveled >= 5280) {
						UnlockAchievement(achievement);
					}
					break;
				case Achievement.ACH_TRAVEL_FAR_SINGLE1:
					if (m_flGameFeetTraveled >= 500) {
						UnlockAchievement(achievement);
					}
					break;
			}
		}

		//Store stats in the Steam database if necessary
		if (m_bStoreStats) {
			// already set any achievements in UnlockAchievement

			// set stats
			SteamUserStats.SetStat("NumGames", m_nTotalGamesPlayed);
			SteamUserStats.SetStat("NumWins", m_nTotalNumWins);
			SteamUserStats.SetStat("NumLosses", m_nTotalNumLosses);
			SteamUserStats.SetStat("FeetTraveled", m_flTotalFeetTraveled);
			SteamUserStats.SetStat("MaxFeetTraveled", m_flMaxFeetTraveled);
			// Update average feet / second stat
			SteamUserStats.UpdateAvgRateStat("AverageSpeed", m_flGameFeetTraveled, m_flGameDurationSeconds);
			// The averaged result is calculated for us
			SteamUserStats.GetStat("AverageSpeed", out m_flAverageSpeed);

			bool bSuccess = SteamUserStats.StoreStats();
			// If this failed, we never sent anything to the server, try
			// again later.
			m_bStoreStats = !bSuccess;
		}
	}

	//-----------------------------------------------------------------------------
	// Purpose: Accumulate distance traveled
	//-----------------------------------------------------------------------------
	public void AddDistanceTraveled(float flDistance) {
		m_flGameFeetTraveled += flDistance;
	}
	
	//-----------------------------------------------------------------------------
	// Purpose: Game state has changed
	//-----------------------------------------------------------------------------
	public void OnGameStateChange(EClientGameState eNewState) {
		if (!m_bStatsValid)
			return;

		if (eNewState == EClientGameState.k_EClientGameActive) {
			// Reset per-game stats
			m_flGameFeetTraveled = 0;
			m_ulTickCountGameStart = Time.time;
		}
		else if (eNewState == EClientGameState.k_EClientGameWinner || eNewState == EClientGameState.k_EClientGameLoser) {
			if (eNewState == EClientGameState.k_EClientGameWinner) {
				m_nTotalNumWins++;
			}
			else {
				m_nTotalNumLosses++;
			}

			// Tally games
			m_nTotalGamesPlayed++;

			// Accumulate distances
			m_flTotalFeetTraveled += m_flGameFeetTraveled;

			// New max?
			if (m_flGameFeetTraveled > m_flMaxFeetTraveled)
				m_flMaxFeetTraveled = m_flGameFeetTraveled;

			// Calc game duration
			m_flGameDurationSeconds = Time.time - m_ulTickCountGameStart;

			// We want to update stats the next frame.
			m_bStoreStats = true;
		}
	}

	//-----------------------------------------------------------------------------
	// Purpose: Unlock this achievement
	//-----------------------------------------------------------------------------
	private void UnlockAchievement(Achievement_t achievement) {
		achievement.m_bAchieved = true;

		// the icon may change once it's unlocked
		//achievement.m_iIconImage = 0;

		// mark it down
		SteamUserStats.SetAchievement(achievement.m_eAchievementID.ToString());

		// Store stats end of frame
		m_bStoreStats = true;
	}
	
	//-----------------------------------------------------------------------------
	// Purpose: We have stats data from Steam. It is authoritative, so update
	//			our data with those results now.
	//-----------------------------------------------------------------------------
	private void OnUserStatsReceived(UserStatsReceived_t pCallback) {
		if (!SteamManager.Initialized)
			return;

		// we may get callbacks for other games' stats arriving, ignore them
		if ((ulong)m_GameID == pCallback.m_nGameID) {
			if (EResult.k_EResultOK == pCallback.m_eResult) {
				Debug.Log("Received stats and achievements from Steam\n");

				m_bStatsValid = true;

				// load achievements
				foreach (Achievement_t ach in m_Achievements) {
					bool ret = SteamUserStats.GetAchievement(ach.m_eAchievementID.ToString(), out ach.m_bAchieved);
					if (ret) {
						ach.m_strName = SteamUserStats.GetAchievementDisplayAttribute(ach.m_eAchievementID.ToString(), "name");
						ach.m_strDescription = SteamUserStats.GetAchievementDisplayAttribute(ach.m_eAchievementID.ToString(), "desc");
					}
					else {
						Debug.LogWarning("SteamUserStats.GetAchievement failed for Achievement " + ach.m_eAchievementID + "\nIs it registered in the Steam Partner site?");
					}
				}

				// load stats
				SteamUserStats.GetStat("NumGames", out m_nTotalGamesPlayed);
				SteamUserStats.GetStat("NumWins", out m_nTotalNumWins);
				SteamUserStats.GetStat("NumLosses", out m_nTotalNumLosses);
				SteamUserStats.GetStat("FeetTraveled", out m_flTotalFeetTraveled);
				SteamUserStats.GetStat("MaxFeetTraveled", out m_flMaxFeetTraveled);
				SteamUserStats.GetStat("AverageSpeed", out m_flAverageSpeed);
			}
			else {
				Debug.Log("RequestStats - failed, " + pCallback.m_eResult);
			}
		}
	}

	//-----------------------------------------------------------------------------
	// Purpose: Our stats data was stored!
	//-----------------------------------------------------------------------------
	private void OnUserStatsStored(UserStatsStored_t pCallback) {
		// we may get callbacks for other games' stats arriving, ignore them
		if ((ulong)m_GameID == pCallback.m_nGameID) {
			if (EResult.k_EResultOK == pCallback.m_eResult) {
				Debug.Log("StoreStats - success");
			}
			else if (EResult.k_EResultInvalidParam == pCallback.m_eResult) {
				// One or more stats we set broke a constraint. They've been reverted,
				// and we should re-iterate the values now to keep in sync.
				Debug.Log("StoreStats - some failed to validate");
				// Fake up a callback here so that we re-load the values.
				UserStatsReceived_t callback = new UserStatsReceived_t();
				callback.m_eResult = EResult.k_EResultOK;
				callback.m_nGameID = (ulong)m_GameID;
				OnUserStatsReceived(callback);
			}
			else {
				Debug.Log("StoreStats - failed, " + pCallback.m_eResult);
			}
		}
	}

	//-----------------------------------------------------------------------------
	// Purpose: An achievement was stored
	//-----------------------------------------------------------------------------
	private void OnAchievementStored(UserAchievementStored_t pCallback) {
		// We may get callbacks for other games' stats arriving, ignore them
		if ((ulong)m_GameID == pCallback.m_nGameID) {
			if (0 == pCallback.m_nMaxProgress) {
				Debug.Log("Achievement '" + pCallback.m_rgchAchievementName + "' unlocked!");
			}
			else {
				Debug.Log("Achievement '" + pCallback.m_rgchAchievementName + "' progress callback, (" + pCallback.m_nCurProgress + "," + pCallback.m_nMaxProgress + ")");
			}
		}
	}

    //添加的回调函数
    private void OnLobbyCreated(LobbyCreated_t pCallback,bool ioFailed) {
        if(pCallback.m_eResult == EResult.k_EResultOK) {
            Debug.Log("Lobby Create Success! LobbyId:" + pCallback.m_ulSteamIDLobby);
                  
            GameObject.Find("LobbyPanel").gameObject.SetActive(true);

            var lobbyname = SteamFriends.GetPersonaName() + "'s Lobby";
            GameObject.Find("LobbyPanel").GetComponent<LobbyPanel>().createLobby(lobbyname);

            m_IsInLobby = true;
            m_LobbyId = new CSteamID(pCallback.m_ulSteamIDLobby);
        } else {
            Debug.LogWarning("Lobby Create Failed!");
        }
        //test
        SendNetMessage();
    }
    private void OnLobbyMatchedList(LobbyMatchList_t pCallback,bool ioFailed) {
        if (ioFailed) { return; }

        List<string> lobbyNames = new List<string>();
        for(int i = 0; i < pCallback.m_nLobbiesMatching; i++) {
            var lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
            var LobbyName = SteamMatchmaking.GetLobbyData(lobbyID, "name");
            lobbyNames.Add(LobbyName);

            GameObject.Find("LobbyListPanel").GetComponent<LobbyListPanel>().SetLobbyList(lobbyNames.ToArray());
        }
    }
    private void OnLobbyChatUpdated(LobbyChatUpdate_t pCallback) {
       
        var membernum = SteamMatchmaking.GetNumLobbyMembers(m_LobbyId);

        Debug.Log("房间更新，当前人数为：" + membernum);

        List<string> members = new List<string>();
        for(int i = 0; i < membernum; i++) {
            var memberId = SteamMatchmaking.GetLobbyMemberByIndex(m_LobbyId, i);
           
            members.Add(SteamFriends.GetFriendPersonaName(memberId));
            GameObject.Find("LobbyPanel").GetComponent<LobbyPanel>().updateMember(members.ToArray());
        }
    }
	//-----------------------------------------------------------------------------
	// Purpose: Display the user's stats and achievements
	//-----------------------------------------------------------------------------
	public void Render() {
		if (!SteamManager.Initialized) {
			GUILayout.Label("Steamworks not Initialized");
			return;
		}
        /*
		GUILayout.Label("m_ulTickCountGameStart: " + m_ulTickCountGameStart);
		GUILayout.Label("m_flGameDurationSeconds: " + m_flGameDurationSeconds);
		GUILayout.Label("m_flGameFeetTraveled: " + m_flGameFeetTraveled);
		GUILayout.Space(10);
		GUILayout.Label("NumGames: " + m_nTotalGamesPlayed);
		GUILayout.Label("NumWins: " + m_nTotalNumWins);
		GUILayout.Label("NumLosses: " + m_nTotalNumLosses);
		GUILayout.Label("FeetTraveled: " + m_flTotalFeetTraveled);
		GUILayout.Label("MaxFeetTraveled: " + m_flMaxFeetTraveled);
		GUILayout.Label("AverageSpeed: " + m_flAverageSpeed);

		GUILayout.BeginArea(new Rect(Screen.width - 300, 0, 300, 800));
		foreach(Achievement_t ach in m_Achievements) {
			GUILayout.Label(ach.m_eAchievementID.ToString());
			GUILayout.Label(ach.m_strName + " - " + ach.m_strDescription);
			GUILayout.Label("Achieved: " + ach.m_bAchieved);
			GUILayout.Space(20);
		}

		// FOR TESTING PURPOSES ONLY!
		if (GUILayout.Button("RESET STATS AND ACHIEVEMENTS")) {
			SteamUserStats.ResetAllStats(true);
			SteamUserStats.RequestCurrentStats();
			OnGameStateChange(EClientGameState.k_EClientGameActive);
		}
		GUILayout.EndArea();*/
	}
	
	private class Achievement_t {
		public Achievement m_eAchievementID;
		public string m_strName;
		public string m_strDescription;
		public bool m_bAchieved;

		/// <summary>
		/// Creates an Achievement. You must also mirror the data provided here in https://partner.steamgames.com/apps/achievements/yourappid
		/// </summary>
		/// <param name="achievement">The "API Name Progress Stat" used to uniquely identify the achievement.</param>
		/// <param name="name">The "Display Name" that will be shown to players in game and on the Steam Community.</param>
		/// <param name="desc">The "Description" that will be shown to players in game and on the Steam Community.</param>
		public Achievement_t(Achievement achievementID, string name, string desc) {
			m_eAchievementID = achievementID;
			m_strName = name;
			m_strDescription = desc;
			m_bAchieved = false;
		}
	}

    #region 外部调用函数
    public void CreateLobby() {
        SteamAPICall_t handle =  SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 3);
        m_LobbyCreated.Set(handle);
    }
    public void GetLobbyList() {
        SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
        m_LobbyMatchList.Set(handle);
    }
    public void InviteFriend() {
        if (!m_IsInLobby) { return; }
        SteamFriends.ActivateGameOverlayInviteDialog(m_LobbyId);
    }
   
    //test 
    public void SendNetMessage() {
        TestByteClass tb = new TestByteClass();
        tb.a = 2;
        tb.b = "hello world";

        var packet = HelperFunction.ObjectToByteArray(tb as object);
        var rpacket = HelperFunction.ByteArrayToObject(packet) as TestByteClass ;
        Debug.Log(rpacket.a + " " + rpacket.b);

        var friendID = SteamFriends.GetFriendByIndex(0,EFriendFlags.k_EFriendFlagAll);
        SteamGameServerNetworking.SendP2PPacket(friendID, packet, (uint)(sizeof(byte) * packet.Length), EP2PSend.k_EP2PSendUnreliable);
    }
    #endregion
}
[System.Serializable]
public class TestByteClass {
    public int a;
    public string b;

   
}
public class HelperFunction{
    public static byte[] ObjectToByteArray(object obj) {
        BinaryFormatter bf = new BinaryFormatter();
        using (var ms = new MemoryStream()) {
            bf.Serialize(ms, obj);
            return ms.ToArray();
        }
    }
    public static object ByteArrayToObject(byte[] arrBytes) {
        using (var memStream = new MemoryStream()) {
            var binForm = new BinaryFormatter();
            memStream.Write(arrBytes, 0, arrBytes.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            var obj = binForm.Deserialize(memStream);
            return obj;
        }
    }
}






