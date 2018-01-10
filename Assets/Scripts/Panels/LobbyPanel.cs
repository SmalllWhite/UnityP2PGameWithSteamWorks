using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
//大厅界面
public class LobbyPanel : BasePanel {
    [SerializeField]
    private string lobbyName;
    [SerializeField]
    Text m_NameText;
    [SerializeField]
    Text m_MemberText;
    
    public void createLobby(string name) {
        SetLobby(name);
        Show();
    }
    public void updateMember(string[] memberNames) {
        StringBuilder sb = new StringBuilder();
        foreach(var memeber in memberNames) {
            sb.Append(memeber + "\n");
        }
        m_MemberText.text = sb.ToString();
    }
    public void exitLobby() {
        Hide();
    }
    void SetLobby(string name) {
        lobbyName = name;
        m_NameText.text = name;
    }
   
}
