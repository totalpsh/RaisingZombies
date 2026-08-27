using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class BattleCameraDragInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public static event Action DragStarted;
    public static event Action<float> Dragged;
    public static event Action DragEnded;

    private int _activePointerId = int.MinValue;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_activePointerId != int.MinValue)
            return;

        _activePointerId = eventData.pointerId;
        DragStarted?.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != _activePointerId)
            return;

        Dragged?.Invoke(eventData.delta.x);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _activePointerId)
            return;

        _activePointerId = int.MinValue;
        DragEnded?.Invoke();
    }

    private void OnDisable()
    {
        if (_activePointerId == int.MinValue)
            return;

        _activePointerId = int.MinValue;
        DragEnded?.Invoke();
    }
}
