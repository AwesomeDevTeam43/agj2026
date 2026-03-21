using UnityEngine;

public class DistractionThrow : MonoBehaviour
{
    [SerializeField] private GameObject _distractionPrefab;
    [SerializeField] private float _throwCooldown = 5f;

    private float _lastThrowTime = -10f;

    private Camera _mainCam;

    private void Awake()
    {
        _mainCam = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) && Time.time - _lastThrowTime >= _throwCooldown)
        {
            ThrowDist();
        }
    }

    private void ThrowDist()
    {
        _lastThrowTime = Time.time;
        Vector3 mouseWorldPos = _mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        Instantiate(_distractionPrefab, mouseWorldPos, Quaternion.identity);
        AudioManager.Instance.Play("Coin");
    }
}