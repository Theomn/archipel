using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LocalizedTMPText : MonoBehaviour
{
    [SerializeField] private string localizationKey;
    void Start()
    {
        GetComponent<TMP_Text>().text = GameController.instance.localization.GetText(localizationKey);
    }

}