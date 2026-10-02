using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;   // novo Input System (mouse)

// Gira o globo (arrastando com o botão esquerdo) e dá zoom (roda do mouse).
// Colocar na ESFERA (objeto "Globo").
public class GlobeRotate : MonoBehaviour
{
    [Header("Girar")]
    [SerializeField] float speed = 0.25f;     // aumente se girar devagar demais

    [Header("Zoom (roda do mouse)")]
    [SerializeField] Camera cam;              // se deixar vazio, usa a Main Camera
    [SerializeField] float zoomStep = 5f;     // quanto cada "clique" da roda aproxima
    [SerializeField] float minFov = 12f;      // zoom máximo (bem perto)
    [SerializeField] float maxFov = 60f;      // zoom mínimo (longe)

    float targetFov;                          // zoom que queremos alcançar

    void Start()
    {
        if (cam == null) cam = Camera.main;
        targetFov = cam.fieldOfView;
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // ----- ZOOM -----
        float scroll = mouse.scroll.ReadValue().y;      // + = roda para frente
        if (scroll != 0f && !overUI)
            targetFov = Mathf.Clamp(targetFov - Mathf.Sign(scroll) * zoomStep, minFov, maxFov);
        // aproxima aos poucos, para o zoom ficar suave
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 10f * Time.deltaTime);

        // ----- GIRAR -----
        if (!mouse.leftButton.isPressed || overUI) return;

        Vector2 delta = mouse.delta.ReadValue();        // quanto o mouse andou neste frame
        float k = cam.fieldOfView / maxFov;             // com zoom, gira mais devagar (mais preciso)
        transform.Rotate(Vector3.up,    -delta.x * speed * k, Space.World);
        transform.Rotate(Vector3.right,  delta.y * speed * k, Space.World);
    }
}
