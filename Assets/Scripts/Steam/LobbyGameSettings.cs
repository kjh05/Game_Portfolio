using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LobbyGameSettings
{
    [Header("Hunter")]
    [Range(1, 99)]
    public int hunterRatio = 10;

    [Header("Round Settings")]
    [Range(1, 99)]
    public int hidingTime = 30;

    [Range(1, 99)]      
    public int roundNumber = 1;

    [Range(1, 999)]
    public int roundDuration = 300;

    [Header("Forced Taunt")]
    public bool forcedTauntEnabled = true;

    [Range(1, 99)]
    public int forcedTauntInterval = 30;

    [Header("Pass The Torch")]
    [Range(1, 99)]
    public int freezeTime = 10;

    [Header("Map Selection")]
    public List<string> selectedMapSceneNames = new List<string>();
}