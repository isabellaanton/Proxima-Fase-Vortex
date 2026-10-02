using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;   // novo Input System (mouse)
using UnityEngine.UI;

// Jogo do globo: "Clique no país que não existe".
// A cada rodada o jogo INVENTA um país falso numa fronteira entre países reais,
// com posição, tamanho e formato aleatórios.
public class GlobeGame : MonoBehaviour
{
    [Header("Cena")]
    [SerializeField] Camera cam;                    // a Main Camera
    [SerializeField] Renderer globeRenderer;        // o Mesh Renderer da esfera
    [SerializeField] MeshCollider globeCollider;    // o Mesh Collider da esfera

    [Header("Mapa de IDs (importar com Read/Write ligado, sem compressão, sem mipmaps)")]
    [SerializeField] Texture2D idMap;               // mapa_ids.png
    [SerializeField] TextAsset namesCsv;            // paises.csv (opcional, só para mostrar nomes)

    [Header("Tamanho do país falso (em graus do globo)")]
    [SerializeField] float minRadius = 2.5f;        // pequeno: ~ tamanho de Portugal
    [SerializeField] float maxRadius = 8f;          // grande: ~ tamanho da Espanha/França

    [Header("Interface")]
    [SerializeField] TMP_Text missionText;
    [SerializeField] TMP_Text resultText;
    [SerializeField] Button confirmButton;

    // ---------- dados internos ----------
    static readonly Color32 Red   = new Color32(220, 40, 40, 255);
    static readonly Color32 Highlight = new Color32(255, 255, 255, 255);   // branco: nenhum país usa

    int w, h;                       // largura e altura do mapa em pixels
    int oceanKey;                   // "cor-código" do oceano
    int[] baseKeys, roundKeys;      // cor-código de cada pixel (mapa original / mapa da rodada)
    Color32[] baseColors, roundColors, display;
    bool[] edge;                    // true = pixel de fronteira (desenhado mais escuro)
    readonly List<int> borderSeeds = new List<int>();               // pixels de fronteira entre 2 países
    readonly Dictionary<int, int> totalPx = new Dictionary<int, int>();     // cor-código -> nº de pixels
    readonly Dictionary<int, string> nameByKey = new Dictionary<int, string>();

    Texture2D displayTex;           // textura que aparece na esfera
    int fakeKey, selectedKey;       // cor-código do país falso e do país clicado
    string fakeName;
    bool answered;
    Vector2 downPos;
    int pendingSeed;                // centro da última mancha tentada
    Vector2 fakeUV;                 // onde o país falso está no mapa (0 a 1)

    // transforma uma cor (R,G,B) em um número único
    static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

    static Color32 Dark(Color32 c) =>
        new Color32((byte)(c.r * 0.55f), (byte)(c.g * 0.55f), (byte)(c.b * 0.55f), 255);

    // ============================================================
    //  INÍCIO: lê o mapa uma única vez
    // ============================================================
    void Start()
    {
        w = idMap.width;
        h = idMap.height;
        baseColors = idMap.GetPixels32();
        int n = baseColors.Length;

        baseKeys    = new int[n];
        roundKeys   = new int[n];
        roundColors = new Color32[n];
        display     = new Color32[n];
        edge        = new bool[n];

        // o oceano é a cor do pixel do canto superior esquerdo (Ártico)
        oceanKey = Pack(baseColors[(h - 1) * w]);

        // conta quantos pixels cada país tem
        for (int i = 0; i < n; i++)
        {
            int k = Pack(baseColors[i]);
            baseKeys[i] = k;
            if (k == oceanKey) continue;
            totalPx.TryGetValue(k, out int c);
            totalPx[k] = c + 1;
        }

        // lê os nomes (linhas no formato: R,G,B,Nome)
        if (namesCsv != null)
        {
            foreach (string line in namesCsv.text.Split('\n'))
            {
                string[] p = line.Trim().Split(',');
                if (p.Length < 4) continue;
                if (!int.TryParse(p[0], out int r) || !int.TryParse(p[1], out int g) ||
                    !int.TryParse(p[2], out int b)) continue;
                nameByKey[(r << 16) | (g << 8) | b] = p[3];
            }
        }

        // guarda pixels que ficam na fronteira entre dois países reais
        // (só entre as latitudes -60 e 70, para evitar a distorção dos polos)
        int yMin = h * 30 / 180, yMax = h * 160 / 180;
        for (int y = yMin; y < yMax; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                int k = baseKeys[i];
                if (!IsBigLand(k)) continue;
                int kRight = baseKeys[y * w + (x + 1) % w];
                int kUp = baseKeys[i + w];
                if ((kRight != k && IsBigLand(kRight)) || (kUp != k && IsBigLand(kUp)))
                    borderSeeds.Add(i);
            }
        }
        if (borderSeeds.Count == 0)
            Debug.LogError("Nenhuma fronteira encontrada. Confira se o mapa foi importado SEM compressão e SEM suavização.");

        // textura que vai na esfera (com mipmaps para as fronteiras não "tremerem")
        displayTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        displayTex.filterMode = FilterMode.Trilinear;
        globeRenderer.material.mainTexture = displayTex;

        confirmButton.onClick.AddListener(Confirm);
        StartRound();
    }

    bool IsBigLand(int key) =>
        key != oceanKey && totalPx.TryGetValue(key, out int c) && c >= 30;

    // ============================================================
    //  NOVA RODADA
    // ============================================================
    void StartRound()
    {
        answered = false;
        selectedKey = -1;
        resultText.text = "";
        confirmButton.gameObject.SetActive(false);

        CreateFake();       // inventa o país falso desta rodada
        ComputeEdges();     // calcula onde ficam as linhas de fronteira
        ResetDisplay();
        Upload();

        missionText.text = "Clique no país que não existe";
    }

    // ============================================================
    //  CRIA O PAÍS FALSO (posição, tamanho e formato aleatórios)
    // ============================================================
    void CreateFake()
    {
        System.Array.Copy(baseKeys, roundKeys, baseKeys.Length);
        System.Array.Copy(baseColors, roundColors, baseColors.Length);
        fakeKey = -1;

        var blob = new List<int>();
        for (int attempt = 0; attempt < 80; attempt++)   // tenta até achar um formato válido
        {
            blob.Clear();
            if (!BuildBlob(blob)) continue;

            // cor aleatória que nenhum país real usa
            Color32 fakeColor;
            do
            {
                fakeColor = (Color32)Color.HSVToRGB(Random.value,
                                                    Random.Range(0.45f, 0.75f),
                                                    Random.Range(0.70f, 0.98f));
            } while (Pack(fakeColor) == oceanKey || totalPx.ContainsKey(Pack(fakeColor)));

            fakeKey = Pack(fakeColor);
            fakeName = RandomName();
            // guarda a posição do centro do país falso (para girar o globo até ele)
            fakeUV = new Vector2((pendingSeed % w + 0.5f) / w, (pendingSeed / w + 0.5f) / h);
            foreach (int i in blob)
            {
                roundKeys[i] = fakeKey;
                roundColors[i] = fakeColor;
            }
            return;
        }
        Debug.LogError("Não consegui criar o país falso. Confira o mapa de IDs.");
    }

    // Escolhe um ponto de fronteira e "desenha" uma mancha irregular em volta dele.
    // Devolve false se a mancha ficou ruim (pequena demais, engoliu um país, etc.).
    bool BuildBlob(List<int> blob)
    {
        int seed = borderSeeds[Random.Range(0, borderSeeds.Count)];
        pendingSeed = seed;
        int sx = seed % w, sy = seed / w;

        float lat0 = (sy + 0.5f) / h * 180f - 90f;     // latitude do centro (graus)
        float baseR = Random.Range(minRadius, maxRadius);   // raio base = TAMANHO aleatório

        // parâmetros aleatórios que deixam a borda irregular = FORMATO aleatório
        float p1 = Random.value * 6.2832f, p2 = Random.value * 6.2832f;
        int k1 = Random.Range(2, 4), k2 = Random.Range(4, 7);
        float a1 = Random.Range(0.15f, 0.35f), a2 = Random.Range(0.08f, 0.20f);

        // em graus de longitude, 1 grau "anda menos" perto dos polos
        float cos0 = Mathf.Max(0.2f, Mathf.Cos(lat0 * Mathf.Deg2Rad));
        float reach = baseR * 1.6f;
        int dyPx = Mathf.CeilToInt(reach / 180f * h);
        int dxPx = Mathf.CeilToInt(reach / cos0 / 360f * w);

        var removed = new Dictionary<int, int>();       // quantos pixels tirei de cada país

        for (int y = Mathf.Max(0, sy - dyPx); y <= Mathf.Min(h - 1, sy + dyPx); y++)
        {
            for (int dx = -dxPx; dx <= dxPx; dx++)
            {
                int x = ((sx + dx) % w + w) % w;        // dá a volta no globo (±180°)
                int i = y * w + x;
                int key = baseKeys[i];
                if (key == oceanKey) continue;          // não pinta o mar

                float dLat = (y - sy) * 180f / h;
                float dLon = dx * 360f / w * cos0;
                float dist = Mathf.Sqrt(dLat * dLat + dLon * dLon);
                float ang = Mathf.Atan2(dLat, dLon);
                float r = baseR * (1f + a1 * Mathf.Sin(k1 * ang + p1) + a2 * Mathf.Sin(k2 * ang + p2));
                if (dist > r) continue;                 // fora da mancha

                blob.Add(i);
                removed.TryGetValue(key, out int cnt);
                removed[key] = cnt + 1;
            }
        }

        if (blob.Count < 150) return false;             // pequeno demais para clicar

        int significant = 0;
        foreach (var kv in removed)
        {
            // não pode engolir quase um país inteiro
            if (kv.Value > totalPx[kv.Key] * 0.4f) return false;
            // conta países que contribuíram com pelo menos 15% da mancha
            if (kv.Value >= blob.Count * 0.15f) significant++;
        }
        return significant >= 2;                        // tem que ficar "entre" pelo menos 2 países
    }

    static readonly string[] Syllables =
        { "ka", "lor", "vi", "mar", "zen", "bor", "tal", "qui", "dra", "nes", "fo", "ur", "sel", "tran" };
    static readonly string[] Endings = { "ia", "land", "stan", "onia", "ovia", "ara" };

    static string RandomName()
    {
        string s = "";
        int n = Random.Range(2, 4);
        for (int i = 0; i < n; i++) s += Syllables[Random.Range(0, Syllables.Length)];
        return char.ToUpper(s[0]) + s.Substring(1) + Endings[Random.Range(0, Endings.Length)];
    }

    string NameOf(int key)
    {
        if (key == fakeKey) return fakeName;
        return nameByKey.TryGetValue(key, out string n) ? n : "esse país";
    }

    // ============================================================
    //  CLIQUE DO JOGADOR
    // ============================================================
    void Update()
    {
        Mouse mouse = Mouse.current;                    // o mouse no novo Input System
        if (mouse == null) return;
        Vector2 mousePos = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame) downPos = mousePos;
        if (answered || !mouse.leftButton.wasReleasedThisFrame) return;

        // se o mouse andou, foi arrasto (girar o globo), não clique
        if ((mousePos - downPos).sqrMagnitude > 25f) return;
        // ignora cliques em cima de botões
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = cam.ScreenPointToRay(mousePos);
        if (!globeCollider.Raycast(ray, out RaycastHit hit, 100f)) return;

        // posição do clique na imagem (UV de 0 a 1 -> pixel)
        Vector2 uv = hit.textureCoord;
        int x = Mathf.Clamp((int)(uv.x * w), 0, w - 1);
        int y = Mathf.Clamp((int)(uv.y * h), 0, h - 1);
        int key = roundKeys[y * w + x];

        if (key == oceanKey) return;                    // clicou no mar
        if (key != fakeKey && !totalPx.ContainsKey(key)) return;

        selectedKey = key;
        ResetDisplay();
        Paint(key, Red);                                // país clicado fica vermelho
        Upload();
        confirmButton.gameObject.SetActive(true);
    }

    void Confirm()
    {
        if (selectedKey < 0 || fakeKey < 0) return;
        answered = true;
        confirmButton.gameObject.SetActive(false);
        StartCoroutine(AnswerSequence(selectedKey == fakeKey));
    }

    // Mostra a resposta passo a passo (uma "coroutine" pode esperar entre os passos)
    IEnumerator AnswerSequence(bool right)
    {
        resultText.text = right
            ? $"Correto! {fakeName} não existe."
            : $"Errado! Você clicou em {NameOf(selectedKey)}. O país que não existe era {fakeName}: veja ele piscando em branco.";

        // 1) se errou, gira o globo até o país falso ficar de frente para a câmera
        if (!right && TryGetFakeDirection(out Vector3 worldDir))
        {
            Transform globe = globeCollider.transform;
            Vector3 toCam = (cam.transform.position - globe.position).normalized;
            Quaternion startRot = globe.rotation;
            Quaternion endRot = Quaternion.FromToRotation(worldDir, toCam) * startRot;

            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)      // dura ~1,2 segundo
            {
                globe.rotation = Quaternion.Slerp(startRot, endRot, Mathf.SmoothStep(0f, 1f, t));
                yield return null;                                      // espera o próximo frame
            }
            globe.rotation = endRot;
        }

        // 2) pisca o país falso em branco (se errou, o país clicado continua vermelho)
        for (int i = 0; i < 8; i++)
        {
            ResetDisplay();
            if (!right) Paint(selectedKey, Red);
            if (i % 2 == 0) Paint(fakeKey, Highlight);
            Upload();
            yield return new WaitForSeconds(0.25f);
        }

        // 3) deixa o país falso fixo em branco
        ResetDisplay();
        if (!right) Paint(selectedKey, Red);
        Paint(fakeKey, Highlight);
        Upload();

        yield return new WaitForSeconds(3f);
        StartRound();                                                   // próxima rodada
    }

    // Descobre em que direção (no mundo 3D) está o país falso.
    // Procura, na malha da esfera, o triângulo que contém o ponto do mapa (UV) do falso.
    bool TryGetFakeDirection(out Vector3 worldDir)
    {
        worldDir = Vector3.forward;
        Mesh m = globeCollider.sharedMesh;
        if (m == null || !m.isReadable) return false;   // precisa de Read/Write ligado na malha

        Vector3[] verts = m.vertices;
        Vector2[] uvs = m.uv;
        int[] tris = m.triangles;
        Vector2 p = fakeUV;

        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            Vector2 ua = uvs[a];
            Vector2 e1 = uvs[b] - ua, e2 = uvs[c] - ua, ep = p - ua;
            float den = e1.x * e2.y - e2.x * e1.y;
            if (Mathf.Abs(den) < 1e-9f) continue;

            // coordenadas baricêntricas: "quanto" p pesa em cada canto do triângulo
            float wb = (ep.x * e2.y - e2.x * ep.y) / den;
            float wc = (e1.x * ep.y - ep.x * e1.y) / den;
            float wa = 1f - wb - wc;
            if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f) continue;   // p não está neste triângulo

            Vector3 local = wa * verts[a] + wb * verts[b] + wc * verts[c];
            worldDir = globeCollider.transform.TransformDirection(local).normalized;
            return true;
        }
        return false;
    }

    // ============================================================
    //  DESENHO NA TEXTURA
    // ============================================================
    // marca como fronteira todo pixel cujo vizinho (direita ou acima) é de outro país
    void ComputeEdges()
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                bool e = roundKeys[i] != roundKeys[y * w + (x + 1) % w];
                if (!e && y + 1 < h) e = roundKeys[i] != roundKeys[i + w];
                edge[i] = e;
            }
        }
    }

    void ResetDisplay()
    {
        for (int i = 0; i < display.Length; i++)
            display[i] = edge[i] ? Dark(roundColors[i]) : roundColors[i];
    }

    void Paint(int key, Color32 color)
    {
        Color32 dark = Dark(color);
        for (int i = 0; i < display.Length; i++)
            if (roundKeys[i] == key) display[i] = edge[i] ? dark : color;
    }

    void Upload()
    {
        displayTex.SetPixels32(display);
        displayTex.Apply();
    }
}
