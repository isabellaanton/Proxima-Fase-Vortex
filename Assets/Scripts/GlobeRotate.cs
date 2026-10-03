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
    [SerializeField] float zoomPerNotch = 0.85f;   // cada "clique" da roda multiplica o campo de visão por isso (menor = zoom mais forte)
    [SerializeField] float closestFov = 6f;        // zoom máximo (bem perto). Menor = chega mais perto
    [SerializeField] float farthestFov = 60f;      // zoom mínimo (longe)

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
        {
            // multiplicar (em vez de somar) deixa o zoom "do mesmo tamanho" de perto e de longe
            float factor = scroll > 0f ? zoomPerNotch : 1f / zoomPerNotch;
            targetFov = Mathf.Clamp(targetFov * factor, closestFov, farthestFov);
        }
        // aproxima aos poucos, para o zoom ficar suave
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 10f * Time.deltaTime);

        // ----- GIRAR -----
        if (!mouse.leftButton.isPressed || overUI) return;

        Vector2 delta = mouse.delta.ReadValue();        // quanto o mouse andou neste frame
        float k = cam.fieldOfView / farthestFov;        // com zoom, gira mais devagar (mais preciso)
        transform.Rotate(Vector3.up,    -delta.x * speed * k, Space.World);
        transform.Rotate(Vector3.right,  delta.y * speed * k, Space.World);
    }
}
