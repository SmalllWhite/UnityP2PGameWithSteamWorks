using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class BasePanel : MonoBehaviour {

    protected Vector2 ui_originPos;
    private void OnEnable() {
        
        ui_originPos = GetComponent<RectTransform>().anchoredPosition;
    }
    private void Start() {
        Hide();
    }
    protected virtual void Hide() {
        GetComponent<RectTransform>().anchoredPosition = new Vector2(10000, 10000);
    }
    protected virtual void Show() {
        GetComponent<RectTransform>().anchoredPosition = ui_originPos;
    }
}
