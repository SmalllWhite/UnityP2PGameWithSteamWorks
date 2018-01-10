using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
public class LobbyListPanel : BasePanel {

    [SerializeField]
    private Text _listText;

    public void SetLobbyList(string[] lobbyList) {
        StringBuilder sb = new StringBuilder();
        foreach(var lobby in lobbyList) {
            sb.Append(lobby);
            sb.Append("\n");
        }
        _listText.text = sb.ToString();

        Show();
    }
    
}