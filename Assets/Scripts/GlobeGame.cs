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
// Algumas rodadas (sorteadas) mostram os NOMES dos países no globo; outras não.
public class GlobeGame : MonoBehaviour
{
    [Header("Cena")]
    [SerializeField] Camera cam;                    // a Main Camera
    [SerializeField] Renderer globeRenderer;        // o Mesh Renderer da esfera
    [SerializeField] MeshCollider globeCollider;    // o Mesh Collider da esfera

    [Header("Mapa de IDs (importar com Read/Write ligado, sem compressão, sem mipmaps)")]
    [SerializeField] Texture2D idMap;               // mapa_ids.png
    [SerializeField] TextAsset namesCsv;            // paises.csv (nomes dos países)

    [Header("País falso")]
    [Range(0f, 1f)]
    [SerializeField] float islandChance = 0.5f;     // chance de a rodada usar uma ILHA no oceano (o resto: país entre outros países)
    [SerializeField] int minArea = 250;             // tamanho mínimo do país falso (em pixels do mapa)
    [SerializeField] int maxArea = 1800;            // tamanho máximo (Portugal ~ 400, Espanha ~ 1700)
    [SerializeField] float minElongation = 1.4f;    // quão comprido ele sai (1 = redondo)
    [SerializeField] float maxElongation = 2.4f;

    [Header("Nomes dos países no globo")]
    [Range(0f, 1f)]
    [SerializeField] float namesChance = 0.5f;      // chance de uma rodada mostrar os nomes (0 = nunca, 1 = sempre)
    [SerializeField] float minLabelFont = 9f;       // menor tamanho de letra (em pixels) que o jogo mostra
    [SerializeField] float tinyCountryPx = 16f;     // país pequeno: mostra o nome (com a letra mínima) quando o país
                                                    // já tem pelo menos esse tamanho (em pixels) na tela

    [Header("Modo bandeira errada")]
    [Range(0f, 1f)]
    [SerializeField] float flagChance = 0.5f;       // chance de uma rodada ser "bandeira errada" (0 = nunca, 1 = sempre)
    [SerializeField] Texture2D flagAtlas;           // bandeiras.png
    [SerializeField] TextAsset flagsCsv;            // bandeiras.csv
    [SerializeField] int atlasColumns = 16;         // quantas bandeiras por linha no bandeiras.png
    [SerializeField] int atlasRows = 12;
    [SerializeField] float minFlagPx = 10f;         // menor altura (em pixels) de bandeira que o jogo mostra

    [Header("Interface")]
    [SerializeField] TMP_Text missionText;
    [SerializeField] TMP_Text resultText;
    [SerializeField] Button confirmButton;

    // ---------- dados internos ----------
    static readonly Color32 Red       = new Color32(220, 40, 40, 255);
    static readonly Color32 Highlight = new Color32(255, 255, 255, 255);   // branco: nenhum país usa

    int w, h;                       // largura e altura do mapa em pixels
    int oceanKey;                   // "cor-código" do oceano
    int[] baseKeys, roundKeys;      // cor-código de cada pixel (mapa original / mapa da rodada)
    Color32[] baseColors, roundColors, display;
    bool[] edge;                    // true = pixel de fronteira (desenhado mais escuro)
    readonly List<int> borderSeeds = new List<int>();               // pixels de fronteira entre 2 países
    readonly List<int> oceanSeeds = new List<int>();                // pixels de oceano longe de qualquer terra
    int[] landDist;                 // distância (x3) de cada pixel até a terra mais próxima
    readonly Dictionary<int, int> totalPx = new Dictionary<int, int>();     // cor-código -> nº de pixels
    readonly Dictionary<int, string> nameByKey = new Dictionary<int, string>();
    readonly List<int> fakeBlob = new List<int>();                  // pixels do país falso desta rodada

    Texture2D displayTex;           // textura que aparece na esfera
    int fakeKey, selectedKey;       // cor-código do país falso e do país clicado
    string fakeName;
    bool answered;
    Vector2 downPos;
    int pendingSeed;                // centro da última mancha tentada
    Vector2 fakeUV;                 // onde o país falso está no mapa (0 a 1)

    // ---------- dados dos nomes (rótulos) ----------
    int countryCount;                                   // nº de países reais (o falso é o índice countryCount)
    readonly List<int> keyByIdx = new List<int>();      // índice -> cor-código
    int[] baseIdx, roundIdx;                            // índice do país de cada pixel (-1 = oceano)
    int[] dist, bestDist, bestPix;                      // para achar o "meio" de cada país

    Vector3[] meshVerts;            // cópia da malha da esfera (para converter mapa -> ponto 3D)
    Vector2[] meshUvs;
    int[] meshTris;

    // ---------- modo bandeira errada ----------
    readonly Dictionary<int, int> flagByKey = new Dictionary<int, int>();       // cor-código -> posição no atlas
    readonly Dictionary<int, string> isoByKey = new Dictionary<int, string>();  // cor-código -> código do país (br, fr...)
    readonly HashSet<string> dupIso = new HashSet<string>();                    // códigos usados por 2 países no mapa
    bool canShowFlags, flagRound;
    string flagOwnerName;           // de quem é a bandeira que está no país errado
    RectTransform flagsRoot;
    RawImage[] flagImgs;
    bool flagGeoReady;              // a geometria das bandeiras só é calculada uma vez
    Vector3[] gPos; float[] gW, gH, gDiam; bool[] gOn; int[] gBestDist, gBestPix;
    static readonly string[][] LookAlikes =
    {
        new[]{"ro","td","ad","md"}, new[]{"id","mc","pl"}, new[]{"nl","lu"}, new[]{"au","nz"},
        new[]{"ru","sk","si"}, new[]{"co","ec","ve"}, new[]{"sn","ml","gn"}, new[]{"ie","ci","it"},
        new[]{"no","is","fi"}, new[]{"us","lr","my"}, new[]{"cu","pr"}, new[]{"sd","sy","ye","eg","iq"}
    };

    bool canShowNames;              // false se faltar CSV ou a malha não for legível
    bool showNames;                 // esta rodada mostra os nomes?
    Canvas canvas;
    RectTransform labelsRoot;
    TextMeshProUGUI[] labels;
    Vector3[] labelLocalPos;        // posição de cada rótulo na esfera (coordenadas locais)
    float[] labelWidthWorld, labelHeightWorld, labelDiameterWorld;
    int[] labelChars;
    bool[] labelOn;

    // transforma uma cor (R,G,B) em um número único
    static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

    static Color32 Dark(Color32 c) =>
        new Color32((byte)(c.r * 0.55f), (byte)(c.g * 0.55f), (byte)(c.b * 0.55f), 255);

    // ============================================================
    //  INÍCIO: lê o mapa uma única vez
    // ============================================================
    void Start()
    {
        ConfigureConfirmButton();
        ConfigureResultText();
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

        // lê as bandeiras (linhas no formato: R,G,B,posição,código)
        if (flagsCsv != null)
        {
            var count = new Dictionary<string, int>();
            foreach (string line in flagsCsv.text.Split('\n'))
            {
                string[] p = line.Trim().Split(',');
                if (p.Length < 5) continue;
                if (!int.TryParse(p[0], out int r) || !int.TryParse(p[1], out int g) ||
                    !int.TryParse(p[2], out int b) || !int.TryParse(p[3], out int idx)) continue;
                int key = (r << 16) | (g << 8) | b;
                flagByKey[key] = idx;
                isoByKey[key] = p[4].Trim();
                count.TryGetValue(p[4].Trim(), out int c);
                count[p[4].Trim()] = c + 1;
            }
            foreach (var kv in count) if (kv.Value > 1) dupIso.Add(kv.Key);   // ex.: Chipre e Chipre do Norte
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

        // distância de cada pixel até a terra (duas varreduras) -> serve para achar oceano aberto
        landDist = new int[n];
        for (int i = 0; i < n; i++) landDist[i] = baseKeys[i] == oceanKey ? 1000000 : 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x, d = landDist[i];
                if (d == 0) continue;
                if (x > 0) d = Mathf.Min(d, landDist[i - 1] + 3);
                if (y > 0)
                {
                    d = Mathf.Min(d, landDist[i - w] + 3);
                    if (x > 0) d = Mathf.Min(d, landDist[i - w - 1] + 4);
                    if (x < w - 1) d = Mathf.Min(d, landDist[i - w + 1] + 4);
                }
                landDist[i] = d;
            }
        for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x, d = landDist[i];
                if (d == 0) continue;
                if (x < w - 1) d = Mathf.Min(d, landDist[i + 1] + 3);
                if (y < h - 1)
                {
                    d = Mathf.Min(d, landDist[i + w] + 3);
                    if (x < w - 1) d = Mathf.Min(d, landDist[i + w + 1] + 4);
                    if (x > 0) d = Mathf.Min(d, landDist[i + w - 1] + 4);
                }
                landDist[i] = d;
            }
        // oceano a pelo menos 6 px de qualquer terra (latitudes -55 a 65)
        for (int y = h * 35 / 180; y < h * 155 / 180; y++)
            for (int x = 0; x < w; x++)
                if (landDist[y * w + x] >= 18) oceanSeeds.Add(y * w + x);

        // textura que vai na esfera (com mipmaps para as fronteiras não "tremerem")
        displayTex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        displayTex.filterMode = FilterMode.Trilinear;
        globeRenderer.material.mainTexture = displayTex;

        SetupNames(n);

        confirmButton.onClick.AddListener(Confirm);
        StartRound();
    }

    // Mantém o botão de confirmação visível em qualquer proporção da janela Game.
    void ConfigureConfirmButton()
    {
        RectTransform rect = confirmButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-32f, 32f);
        rect.sizeDelta = new Vector2(300f, 96f);

        TMP_Text label = confirmButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.fontSize = 36f;
    }

    // Centraliza o retorno da rodada e reserva uma área larga para mensagens longas.
    void ConfigureResultText()
    {
        RectTransform rect = resultText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -150f);
        rect.sizeDelta = new Vector2(900f, 220f);
        resultText.alignment = TextAlignmentOptions.Center;
        resultText.raycastTarget = false;
    }

    bool IsBigLand(int key) =>
        key != oceanKey && totalPx.TryGetValue(key, out int c) && c >= 30;

    // ============================================================
    //  PREPARA OS NOMES (rótulos) -- roda uma vez
    // ============================================================
    void SetupNames(int n)
    {
        // a malha da esfera precisa estar legível para sabermos onde cada ponto do mapa fica no globo 3D
        Mesh m = globeCollider.sharedMesh;
        if (m != null && m.isReadable)
        {
            meshVerts = m.vertices;
            meshUvs = m.uv;
            meshTris = m.triangles;
        }
        else
        {
            Debug.LogWarning("Nomes desligados: ligue Read/Write na malha da esfera (ou use a esfera padrão da Unity).");
        }
        if (nameByKey.Count == 0)
            Debug.LogWarning("Nomes desligados: arraste o arquivo paises.csv para o campo Names Csv.");

        canShowNames = meshTris != null && nameByKey.Count > 0 && namesChance > 0f;
        canShowFlags = meshTris != null && nameByKey.Count > 0 && flagAtlas != null && flagByKey.Count > 0 && flagChance > 0f;
        if (flagChance > 0f && flagAtlas == null)
            Debug.LogWarning("Modo bandeira desligado: arraste bandeiras.png (Flag Atlas) e bandeiras.csv (Flags Csv) no Inspector.");
        if (!canShowNames && !canShowFlags) return;

        // dá um número (0, 1, 2...) a cada país real
        var idxByKey = new Dictionary<int, int>();
        foreach (var kv in totalPx)
        {
            idxByKey[kv.Key] = keyByIdx.Count;
            keyByIdx.Add(kv.Key);
        }
        countryCount = keyByIdx.Count;

        baseIdx = new int[n];
        for (int i = 0; i < n; i++)
            baseIdx[i] = baseKeys[i] == oceanKey ? -1 : idxByKey[baseKeys[i]];
        roundIdx = new int[n];
        dist = new int[n];
        bestDist = new int[countryCount + 1];   // +1 = país falso
        bestPix = new int[countryCount + 1];

        // cria um texto (TextMeshPro) para cada país + um para o falso
        canvas = confirmButton.GetComponentInParent<Canvas>().rootCanvas;
        var rootGo = new GameObject("RotulosDosPaises", typeof(RectTransform));
        labelsRoot = rootGo.GetComponent<RectTransform>();
        labelsRoot.SetParent(canvas.transform, false);
        labelsRoot.SetAsFirstSibling();                 // fica atrás dos outros textos e do botão
        labelsRoot.anchorMin = Vector2.zero;
        labelsRoot.anchorMax = Vector2.one;
        labelsRoot.offsetMin = Vector2.zero;
        labelsRoot.offsetMax = Vector2.zero;

        int total = countryCount + 1;
        labels = new TextMeshProUGUI[total];
        labelLocalPos = new Vector3[total];
        labelWidthWorld = new float[total];
        labelHeightWorld = new float[total];
        labelDiameterWorld = new float[total];
        labelChars = new int[total];
        labelOn = new bool[total];

        for (int k = 0; k < total; k++)
        {
            var go = new GameObject("rotulo", typeof(RectTransform));
            go.transform.SetParent(labelsRoot, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = FontStyles.Bold;
            t.color = new Color(0f, 0f, 0f, 0.85f);
            t.raycastTarget = false;                    // não atrapalha o clique no globo
            t.rectTransform.sizeDelta = new Vector2(1000f, 100f);
            go.SetActive(false);
            labels[k] = t;
        }

        if (!canShowFlags) return;

        // uma imagem (RawImage) por país real, todas usando o mesmo atlas de bandeiras
        var fgo = new GameObject("BandeirasDosPaises", typeof(RectTransform));
        flagsRoot = fgo.GetComponent<RectTransform>();
        flagsRoot.SetParent(canvas.transform, false);
        flagsRoot.SetAsFirstSibling();
        flagsRoot.anchorMin = Vector2.zero; flagsRoot.anchorMax = Vector2.one;
        flagsRoot.offsetMin = Vector2.zero; flagsRoot.offsetMax = Vector2.zero;
        flagImgs = new RawImage[countryCount];
        for (int k = 0; k < countryCount; k++)
        {
            if (!flagByKey.ContainsKey(keyByIdx[k])) continue;
            var go = new GameObject("bandeira", typeof(RectTransform));
            go.transform.SetParent(flagsRoot, false);
            var img = go.AddComponent<RawImage>();
            img.texture = flagAtlas;
            img.raycastTarget = false;
            go.SetActive(false);
            flagImgs[k] = img;
        }
        flagsRoot.gameObject.SetActive(false);
        gPos = new Vector3[total]; gW = new float[total]; gH = new float[total]; gDiam = new float[total];
        gOn = new bool[total]; gBestDist = new int[total]; gBestPix = new int[total];
    }

    // ============================================================
    //  NOVA RODADA
    // ============================================================
    void StartRound()
    {
        answered = false;
        selectedKey = -1;
        resultText.text = "";
        confirmButton.gameObject.SetActive(false);

        // sorteia o modo da rodada: país que não existe OU bandeira errada
        flagRound = canShowFlags && Random.value < flagChance;
        if (flagRound)
        {
            PrepareFlagRound();     // escolhe o país que vai receber a bandeira errada
        }
        else
        {
            CreateFake();           // inventa o país falso desta rodada
            ComputeEdges();         // calcula onde ficam as linhas de fronteira
        }
        ResetDisplay();
        Upload();

        // sorteia se esta rodada mostra os nomes dos países (só no modo "país que não existe")
        showNames = !flagRound && canShowNames && fakeKey >= 0 && Random.value < namesChance;
        if (labelsRoot != null) labelsRoot.gameObject.SetActive(showNames);
        if (flagsRoot != null) flagsRoot.gameObject.SetActive(flagRound);
        if (showNames) BuildLabels(false);

        missionText.text = flagRound ? "Clique no país com a bandeira errada" : "Clique no país que não existe";
    }

    // ============================================================
    //  MODO BANDEIRA ERRADA
    // ============================================================
    void PrepareFlagRound()
    {
        System.Array.Copy(baseKeys, roundKeys, baseKeys.Length);
        System.Array.Copy(baseColors, roundColors, baseColors.Length);
        fakeBlob.Clear();
        fakeKey = -1;
        ComputeEdges();

        // posição e tamanho de cada bandeira: o cálculo é pesado, mas é igual em toda rodada -> faz 1 vez só
        if (!flagGeoReady)
        {
            BuildLabels(true);
            System.Array.Copy(labelLocalPos, gPos, gPos.Length);
            System.Array.Copy(labelWidthWorld, gW, gW.Length);
            System.Array.Copy(labelHeightWorld, gH, gH.Length);
            System.Array.Copy(labelDiameterWorld, gDiam, gDiam.Length);
            System.Array.Copy(labelOn, gOn, gOn.Length);
            System.Array.Copy(bestDist, gBestDist, gBestDist.Length);
            System.Array.Copy(bestPix, gBestPix, gBestPix.Length);
            flagGeoReady = true;
        }
        else
        {
            System.Array.Copy(gPos, labelLocalPos, gPos.Length);
            System.Array.Copy(gW, labelWidthWorld, gW.Length);
            System.Array.Copy(gH, labelHeightWorld, gH.Length);
            System.Array.Copy(gDiam, labelDiameterWorld, gDiam.Length);
            System.Array.Copy(gOn, labelOn, gOn.Length);
        }

        // países que podem receber a bandeira errada: de bom tamanho, com bandeira, sem código repetido
        var candidates = new List<int>();
        var owners = new List<int>();
        for (int k = 0; k < countryCount; k++)
        {
            int key = keyByIdx[k];
            if (!isoByKey.TryGetValue(key, out string iso) || dupIso.Contains(iso)) continue;
            owners.Add(k);
            if (gBestDist[k] >= 15 && gOn[k]) candidates.Add(k);
        }
        if (candidates.Count == 0) { flagRound = false; CreateFake(); ComputeEdges(); return; }

        int target = candidates[Random.Range(0, candidates.Count)];
        string targetIso = isoByKey[keyByIdx[target]];

        // sorteia de quem é a bandeira errada (nunca a dele, nem uma parecida demais)
        int owner = target;
        for (int tries = 0; tries < 200 && owner == target; tries++)
        {
            int o = owners[Random.Range(0, owners.Count)];
            if (o == target || LooksAlike(targetIso, isoByKey[keyByIdx[o]])) continue;
            owner = o;
        }
        if (owner == target) { flagRound = false; CreateFake(); ComputeEdges(); return; }

        fakeKey = keyByIdx[target];
        fakeName = NameOf(fakeKey);
        flagOwnerName = NameOf(keyByIdx[owner]);
        fakeUV = new Vector2((gBestPix[target] % w + 0.5f) / w, (gBestPix[target] / w + 0.5f) / h);

        // põe a bandeira certa em cada país (e a errada no escolhido)
        for (int k = 0; k < countryCount; k++)
        {
            if (flagImgs[k] == null) continue;
            int idx = flagByKey[keyByIdx[k == target ? owner : k]];
            int col = idx % atlasColumns, row = idx / atlasColumns;
            float cw = 1f / atlasColumns, ch = 1f / atlasRows;
            float insetX = 0.5f / flagAtlas.width, insetY = 0.5f / flagAtlas.height;   // evita "vazar" a bandeira do lado
            flagImgs[k].uvRect = new Rect(col * cw + insetX, 1f - (row + 1) * ch + insetY, cw - 2f * insetX, ch - 2f * insetY);
        }
    }

    static bool LooksAlike(string a, string b)
    {
        if (a == b) return true;
        foreach (var g in LookAlikes)
            if (System.Array.IndexOf(g, a) >= 0 && System.Array.IndexOf(g, b) >= 0) return true;
        return false;
    }

    // ============================================================
    //  CRIA O PAÍS FALSO (posição, tamanho e formato aleatórios)
    // ============================================================
    void CreateFake()
    {
        System.Array.Copy(baseKeys, roundKeys, baseKeys.Length);
        System.Array.Copy(baseColors, roundColors, baseColors.Length);
        fakeKey = -1;
        fakeBlob.Clear();

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

            // nome inventado que não seja igual ao de um país real
            do { fakeName = RandomName(); } while (nameByKey.ContainsValue(fakeName));

            // guarda a posição do centro do país falso (para girar o globo até ele)
            fakeUV = new Vector2((pendingSeed % w + 0.5f) / w, (pendingSeed / w + 0.5f) / h);

            foreach (int i in blob)
            {
                roundKeys[i] = fakeKey;
                roundColors[i] = fakeColor;
            }
            fakeBlob.AddRange(blob);
            return;
        }
        Debug.LogError("Não consegui criar o país falso. Confira o mapa de IDs.");
    }

    // Sorteia um modo (ilha ou país entre outros países), um ponto e um formato
    // alongado e irregular. Devolve false se o resultado ficou ruim.
    bool BuildBlob(List<int> blob)
    {
        bool island = oceanSeeds.Count > 0 && (borderSeeds.Count == 0 || Random.value < islandChance);
        int seed = island ? oceanSeeds[Random.Range(0, oceanSeeds.Count)]
                          : borderSeeds[Random.Range(0, borderSeeds.Count)];
        pendingSeed = seed;
        int sx = seed % w, sy = seed / w;

        float lat0 = (sy + 0.5f) / h * 180f - 90f;
        float cos0 = Mathf.Max(0.25f, Mathf.Cos(lat0 * Mathf.Deg2Rad));

        // elipse comprida (a = eixo maior, b = menor) com área sorteada, girada e levemente curvada
        float pxDeg = 180f / h;
        float area = Random.Range(minArea, maxArea) * pxDeg * pxDeg;
        float asp = Random.Range(minElongation, maxElongation);
        float b = Mathf.Sqrt(area / (Mathf.PI * asp)), a = b * asp;
        float rot = Random.value * Mathf.PI;
        float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
        float bend = Random.Range(-0.5f, 0.5f) / a;

        // bordas "quebradas": muitas ondulações de tamanhos diferentes (deixa o contorno com cara de país)
        const int Harm = 11;
        float[] hk = new float[Harm], ha = new float[Harm], hp = new float[Harm];
        for (int j = 0; j < Harm; j++)
        {
            hk[j] = j + 2;
            ha[j] = Random.Range(0.04f, 0.30f) / Mathf.Pow(hk[j], 0.6f);
            hp[j] = Random.value * 6.2832f;
        }

        float reach = a * 1.8f;
        int dyPx = Mathf.CeilToInt(reach / 180f * h);
        int dxPx = Mathf.CeilToInt(reach / cos0 / 360f * w);

        var removed = new Dictionary<int, int>();   // quantos pixels tirei de cada país
        int landPx = 0;
        // para medir o formato (aspecto): somas dos momentos
        double sumX = 0, sumY = 0, sumXX = 0, sumYY = 0, sumXY = 0;

        for (int y = Mathf.Max(0, sy - dyPx); y <= Mathf.Min(h - 1, sy + dyPx); y++)
        {
            for (int dx = -dxPx; dx <= dxPx; dx++)
            {
                float dLat = (y - sy) * 180f / h;
                float dLon = dx * 360f / w * cos0;
                float u = dLon * cr + dLat * sr;
                float v = -dLon * sr + dLat * cr;
                v -= bend * u * u;                              // curva (formato de banana)
                float th = Mathf.Atan2(v, u);
                float ct = Mathf.Cos(th), st = Mathf.Sin(th);
                float r = a * b / Mathf.Sqrt(b * b * ct * ct + a * a * st * st);
                float f = 1f;
                for (int j = 0; j < Harm; j++) f += ha[j] * Mathf.Sin(hk[j] * th + hp[j]);
                if (Mathf.Sqrt(u * u + v * v) > r * f) continue;   // fora do formato

                int x = ((sx + dx) % w + w) % w;                // dá a volta no globo
                int i = y * w + x;
                int key = baseKeys[i];

                if (island)
                {
                    if (landDist[i] < 9) return false;          // ilha não pode encostar em terra (3 px)
                }
                else if (key != oceanKey)
                {
                    landPx++;
                    removed.TryGetValue(key, out int cnt);
                    removed[key] = cnt + 1;
                }
                blob.Add(i);
                sumX += dx * cos0; sumY += y; sumXX += (double)dx * cos0 * dx * cos0; sumYY += (double)y * y; sumXY += dx * cos0 * y;
            }
        }

        int n = blob.Count;
        if (n < 200) return false;                              // pequeno demais para clicar

        if (!island)
        {
            if (landPx < n * 0.85f) return false;               // no mar demais: fica ilha, não "entre países"
            int significant = 0;
            foreach (var kv in removed)
            {
                if (kv.Value > totalPx[kv.Key] * 0.25f) return false;   // não engole um pedaço grande de um país
                if (kv.Value >= landPx * 0.2f) significant++;
            }
            if (significant < 2) return false;                  // tem que cortar a fronteira de pelo menos 2 países
        }

        // formato: não pode ser redondo (razão entre os eixos principais >= 1,5)
        double mx = sumX / n, my = sumY / n;
        double cxx = sumXX / n - mx * mx, cyy = sumYY / n - my * my, cxy = sumXY / n - mx * my;
        double tr = cxx + cyy, det = cxx * cyy - cxy * cxy;
        double disc = System.Math.Sqrt(System.Math.Max(0, tr * tr / 4 - det));
        double e1 = tr / 2 + disc, e2 = System.Math.Max(1e-9, tr / 2 - disc);
        return System.Math.Sqrt(e1 / e2) >= 1.5;
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
    //  NOMES NO GLOBO
    // ============================================================
    // Descobre o "meio" de cada país (o ponto mais longe de qualquer fronteira),
    // coloca o nome ali e calcula quanto espaço o nome tem.
    void BuildLabels(bool forFlags)
    {
        int n = w * h;

        // índice do país de cada pixel nesta rodada (o falso ganha o índice countryCount)
        System.Array.Copy(baseIdx, roundIdx, n);
        foreach (int i in fakeBlob) roundIdx[i] = countryCount;

        // 1) distância de cada pixel até a fronteira mais próxima (duas varreduras: ida e volta)
        const int Far = 1000000;
        for (int i = 0; i < n; i++)
            dist[i] = (edge[i] || roundIdx[i] < 0) ? 0 : Far;       // fronteira e oceano = distância 0

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                int d = dist[i];
                if (d == 0) continue;
                if (x > 0) d = Mathf.Min(d, dist[i - 1] + 3);
                if (y > 0)
                {
                    d = Mathf.Min(d, dist[i - w] + 3);
                    if (x > 0) d = Mathf.Min(d, dist[i - w - 1] + 4);
                    if (x < w - 1) d = Mathf.Min(d, dist[i - w + 1] + 4);
                }
                dist[i] = d;
            }
        }
        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x;
                int d = dist[i];
                if (d == 0) continue;
                if (x < w - 1) d = Mathf.Min(d, dist[i + 1] + 3);
                if (y < h - 1)
                {
                    d = Mathf.Min(d, dist[i + w] + 3);
                    if (x < w - 1) d = Mathf.Min(d, dist[i + w + 1] + 4);
                    if (x > 0) d = Mathf.Min(d, dist[i + w - 1] + 4);
                }
                dist[i] = d;
            }
        }

        // 2) para cada país, o pixel mais distante da fronteira = o "meio" dele
        for (int k = 0; k <= countryCount; k++) bestDist[k] = -1;
        for (int i = 0; i < n; i++)
        {
            int k = roundIdx[i];
            if (k >= 0 && dist[i] > bestDist[k])
            {
                bestDist[k] = dist[i];
                bestPix[k] = i;
            }
        }

        // 3) posiciona um texto em cada país
        float worldRadius = globeCollider.bounds.extents.x;     // raio do globo no mundo 3D
        float radPerPx = 2f * Mathf.PI / w;                     // quantos radianos vale 1 pixel do mapa

        for (int k = 0; k <= countryCount; k++)
        {
            labelOn[k] = false;
            labels[k].gameObject.SetActive(false);
            if (bestDist[k] < 3) continue;                      // país minúsculo (raio < 1 px): sem nome

            string text = null;
            if (forFlags)
            {
                if (k == countryCount || flagImgs[k] == null) continue;    // modo bandeira: só países reais com bandeira
                text = "";
            }
            else if (k == countryCount) text = fakeName;
            else nameByKey.TryGetValue(keyByIdx[k], out text);
            if (!forFlags && string.IsNullOrEmpty(text)) continue;

            int px = bestPix[k] % w, py = bestPix[k] / w;
            Vector2 uv = new Vector2((px + 0.5f) / w, (py + 0.5f) / h);
            if (!TryUvToLocal(uv, out Vector3 local)) continue;

            float rPx = bestDist[k] / 3f;                       // raio do maior círculo que cabe no país (pixels)
            float lat = ((py + 0.5f) / h - 0.5f) * Mathf.PI;

            // largura real do país na linha do nome (anda para os lados enquanto ainda é o mesmo país)
            int left = px, rightEdge = px;
            while (left > 0 && roundIdx[py * w + left - 1] == k) left--;
            while (rightEdge < w - 1 && roundIdx[py * w + rightEdge + 1] == k) rightEdge++;
            float runPx = rightEdge - left + 1;

            float widthRad = runPx * radPerPx * Mathf.Cos(lat);     // largura leste-oeste disponível
            float heightRad = 2f * rPx * radPerPx;                  // altura norte-sul disponível

            labelWidthWorld[k] = widthRad * worldRadius * 0.9f;
            labelHeightWorld[k] = heightRad * worldRadius * 0.7f;
            labelDiameterWorld[k] = heightRad * worldRadius;        // "tamanho" do país no globo
            labelLocalPos[k] = local;
            labelChars[k] = text.Length;
            if (!forFlags) labels[k].text = text;
            labelOn[k] = true;
        }
    }

    // Todo frame: acompanha o giro e o zoom do globo
    void LateUpdate()
    {
        if (flagRound) { UpdateFlags(); return; }
        if (!showNames || labels == null) return;

        Transform globe = globeCollider.transform;
        Vector3 camPos = cam.transform.position;
        float distToGlobe = Vector3.Distance(camPos, globe.position);
        // quantos pixels da tela vale 1 unidade do mundo 3D (perto do globo)
        float pxPerUnit = Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distToGlobe);
        float canvasScale = canvas.scaleFactor;

        for (int k = 0; k <= countryCount; k++)
        {
            if (!labelOn[k]) continue;

            Vector3 world = globe.TransformPoint(labelLocalPos[k]);
            Vector3 normal = globe.TransformDirection(labelLocalPos[k]).normalized;
            bool facing = Vector3.Dot(normal, (camPos - world).normalized) > 0.2f;   // está do lado visível?

            // tamanho da letra (em pixels) que cabe dentro do país
            float fontPx = Mathf.Min(labelWidthWorld[k] * pxPerUnit / (labelChars[k] * 0.55f),
                                     labelHeightWorld[k] * pxPerUnit);
            fontPx = Mathf.Min(fontPx, 40f);
            // país pequeno: a letra não cabe dentro dele, mas ele já está grande o bastante na tela,
            // então mostramos o nome com a letra mínima (pode passar um pouco da borda)
            if (fontPx < minLabelFont && labelDiameterWorld[k] * pxPerUnit >= tinyCountryPx)
                fontPx = minLabelFont;
            bool show = facing && fontPx >= minLabelFont;

            GameObject go = labels[k].gameObject;
            if (go.activeSelf != show) go.SetActive(show);
            if (!show) continue;

            labels[k].rectTransform.position = cam.WorldToScreenPoint(world);
            float size = Mathf.Round(fontPx / canvasScale);
            if (!Mathf.Approximately(labels[k].fontSize, size)) labels[k].fontSize = size;
        }
    }

    // Modo bandeira: acompanha o giro e o zoom do globo (igual aos nomes, mas com imagens)
    void UpdateFlags()
    {
        if (flagImgs == null) return;
        Transform globe = globeCollider.transform;
        Vector3 camPos = cam.transform.position;
        float distToGlobe = Vector3.Distance(camPos, globe.position);
        float pxPerUnit = Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distToGlobe);
        float canvasScale = canvas.scaleFactor;

        for (int k = 0; k < countryCount; k++)
        {
            if (flagImgs[k] == null || !labelOn[k]) continue;

            Vector3 world = globe.TransformPoint(labelLocalPos[k]);
            Vector3 normal = globe.TransformDirection(labelLocalPos[k]).normalized;
            bool facing = Vector3.Dot(normal, (camPos - world).normalized) > 0.2f;

            // altura (pixels) da bandeira que cabe no país (a bandeira é 4:3)
            float hPx = Mathf.Min(labelWidthWorld[k] * pxPerUnit / 1.33f, labelHeightWorld[k] * pxPerUnit);
            hPx = Mathf.Min(hPx, 48f);
            if (hPx < minFlagPx && labelDiameterWorld[k] * pxPerUnit >= tinyCountryPx) hPx = minFlagPx;
            bool show = facing && hPx >= minFlagPx;

            GameObject go = flagImgs[k].gameObject;
            if (go.activeSelf != show) go.SetActive(show);
            if (!show) continue;

            RectTransform rt = flagImgs[k].rectTransform;
            rt.position = cam.WorldToScreenPoint(world);
            rt.sizeDelta = new Vector2(hPx * 1.333f, hPx) / canvasScale;
        }
    }

    // Converte um ponto do mapa (UV, de 0 a 1) em um ponto da esfera (coordenadas locais).
    // Procura, na malha da esfera, o triângulo que contém esse ponto.
    bool TryUvToLocal(Vector2 p, out Vector3 local)
    {
        local = Vector3.zero;
        if (meshTris == null) return false;

        for (int t = 0; t < meshTris.Length; t += 3)
        {
            int a = meshTris[t], b = meshTris[t + 1], c = meshTris[t + 2];
            Vector2 ua = meshUvs[a];
            Vector2 e1 = meshUvs[b] - ua, e2 = meshUvs[c] - ua, ep = p - ua;
            float den = e1.x * e2.y - e2.x * e1.y;
            if (Mathf.Abs(den) < 1e-9f) continue;

            // coordenadas baricêntricas: "quanto" p pesa em cada canto do triângulo
            float wb = (ep.x * e2.y - e2.x * ep.y) / den;
            float wc = (e1.x * ep.y - ep.x * e1.y) / den;
            float wa = 1f - wb - wc;
            if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f) continue;   // p não está neste triângulo

            local = wa * meshVerts[a] + wb * meshVerts[b] + wc * meshVerts[c];
            return true;
        }
        return false;
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
        if (flagRound)
            resultText.text = right
                ? $"Correto! {fakeName} estava com a bandeira de {flagOwnerName}."
                : $"Errado! Você clicou em {NameOf(selectedKey)}. A bandeira errada estava em {fakeName} (era a bandeira de {flagOwnerName}): veja ele piscando em branco.";
        else
            resultText.text = right
                ? $"Correto! {fakeName} não existe."
                : $"Errado! Você clicou em {NameOf(selectedKey)}. O país que não existe era {fakeName}: veja ele piscando em branco.";

        // 1) se errou, gira o globo até o país falso ficar de frente para a câmera
        if (!right && TryUvToLocal(fakeUV, out Vector3 localDir))
        {
            Transform globe = globeCollider.transform;
            Vector3 worldDir = globe.TransformDirection(localDir).normalized;
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
