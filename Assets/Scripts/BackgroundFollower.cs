using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// VFX를 카메라를 따라다니게 하여 배경 이펙트가 항상 시야 안에 유지되도록 함.
/// </summary>
[RequireComponent(typeof(VisualEffect))]
public class BackgroundFollower : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;
    [SerializeField] private Vector3 offset = Vector3.zero;
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = false;

    private Transform _transform;
    private VisualEffect _vfx;

    private void Awake()
    {
        _transform = transform;
        _vfx = GetComponent<VisualEffect>();
        if (targetCamera == null)
        {
            var cam = Camera.main;
            if (cam != null) targetCamera = cam.transform;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        if (followPosition)
            _transform.position = targetCamera.position + (followRotation ? offset : targetCamera.TransformDirection(offset));

        if (followRotation)
            _transform.rotation = targetCamera.rotation;
    }
}
