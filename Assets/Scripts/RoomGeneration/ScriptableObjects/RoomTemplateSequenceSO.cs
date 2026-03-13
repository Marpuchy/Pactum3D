using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoomTemplateSequenceSO", menuName = "Room/RoomTemplateSequenceSO")]
public class RoomTemplateSequenceSO : ScriptableObject
{
    [Header("Main Room Sequence")] 
    public List<RoomTemplate> roomSequence = new();

    [Min(1)] 
    public int roomsPerTemplate = 5;

    [Header("NPC Room")] 
    public RoomTemplate npcRoomTemplate;

    [Min(1)] 
    public int npcRoomFrequency = 5;



}
