using UnityEngine;

public sealed class RoomTemplateSelector
{
    private readonly RoomTemplateSequenceSO _config;

    private int _globalRoomIndex;
    private int _progressionRoomIndex;

    public int CurrentRoomNumber => _globalRoomIndex;

    public RoomTemplateSelector(RoomTemplateSequenceSO config)
    {
        _config = config;
    }

    public RoomTemplate GetNextTemplate()
    {
        _globalRoomIndex++;

        if (IsNpcRoom())
            return _config.npcRoomTemplate;

        _progressionRoomIndex++;

        int templateIndex = GetTemplateIndex();
        return _config.roomSequence[templateIndex];
    }

    public void SetCurrentRoomNumber(int currentRoomNumber)
    {
        _globalRoomIndex = 0;
        _progressionRoomIndex = 0;

        int safeRoomNumber = Mathf.Max(0, currentRoomNumber);
        for (int i = 0; i < safeRoomNumber; i++)
        {
            _globalRoomIndex++;
            if (!IsNpcRoom())
                _progressionRoomIndex++;
        }
    }

    private bool IsNpcRoom()
    {
        if (_config == null || _config.npcRoomFrequency <= 0)
            return false;

        return _config.npcRoomTemplate != null &&
               _globalRoomIndex % _config.npcRoomFrequency == 0;
    }

    private int GetTemplateIndex()
    {
        if (_config == null || _config.roomSequence == null || _config.roomSequence.Count == 0)
            return 0;

        int roomsPerTemplate = Mathf.Max(1, _config.roomsPerTemplate);
        int index = (_progressionRoomIndex - 1) / roomsPerTemplate;
        return Mathf.Clamp(index, 0, _config.roomSequence.Count - 1);
    }
}
