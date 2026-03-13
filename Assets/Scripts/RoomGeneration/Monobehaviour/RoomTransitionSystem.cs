using System.Collections;
using UnityEngine;

public class RoomTransitionSystem : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private DoorEnteredEvent doorEnteredEvent;
    
    [Header("References")]
    [SerializeField] private RoomBuilder roomBuilder;

    private GameObject _currentRoomRoot;
    private bool _isTransitioning;

    private void Start()
    {
        if (_currentRoomRoot == null)
            CreateNewRoom();
    }

    private void OnEnable()
    {
        doorEnteredEvent.Register(OnDoorEntered);
    }

    private void OnDisable()
    {
        doorEnteredEvent.Unregister(OnDoorEntered);
    }
    
    private void OnDoorEntered()
    {
        if (_isTransitioning)
            return;

        _isTransitioning = true;
        RegenerateRoom();
    }
    
    private void RegenerateRoom()
    {
        DestroySpecialTiles();
        DestroyCurrentRoom();
        CreateNewRoom();
        StartCoroutine(ReleaseTransitionLockNextFrame());
    }

    private void DestroySpecialTiles()
    {
        SpecialTilesContainer.ClearChildren();
    }
    
    private void DestroyCurrentRoom()
    {
        if (_currentRoomRoot != null)
        {
            Destroy(_currentRoomRoot);
            _currentRoomRoot = null;
        }   
    }

    private void CreateNewRoom()
    {
        _currentRoomRoot = new GameObject("Room");
        roomBuilder.Build(_currentRoomRoot.transform);
    }

    private IEnumerator ReleaseTransitionLockNextFrame()
    {
        yield return null;
        _isTransitioning = false;
    }
}
