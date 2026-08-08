using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class BallRollVisual : MonoBehaviour
{
    [Header("Качение")]
    [Tooltip("Ребёнок с 3D-мешем мяча")] public Transform visual;
    public float radius = 0.5f;
    public float rollDir = 1f;

    [Header("Клавиши")]
    [Tooltip("Клавиша заряда/удара (вторая — мышь, если включена)")] public KeyCode kickKey = KeyCode.Space;
    public bool kickMouseLeft = true;
    [Tooltip("Клавиша ведения мяча")] public KeyCode dribbleKey = KeyCode.F;

    [Header("Удар и заряд")]
    public Vector2 kickDirection = new Vector2(0f, 1f); // для Kick() из кода
    public float minPower = 4f;
    public float maxPower = 15f;
    [Tooltip("Секунды от 0 до полного заряда")] public float chargeTime = 1f;
    [Tooltip("Заряд гуляет 0→1→0, как в футбольных минииграх")] public bool pingPongCharge = true;
    [Tooltip("Авто-удар в момент вбегания в радиус с зажатой кнопкой")] public bool autoKickOnEnter = false;
    public float maxSpeed = 20f;
    public float kickSpin = 25f;

    [Header("Автоудар при касании (тычок)")]
    [Tooltip("Включает авто-удар, когда игрок касается мяча")] public bool touchKick = true;
    public float touchKickPower = 6f;
    [Tooltip("Насколько скорость игрока добавляется к силе тычка")] public float touchKickSpeedScale = 0.5f;
    public float touchKickCooldown = 0.4f;
    [Tooltip("Ниже этой скорости игрока тычка нет — можно спокойно подойти и взять мяч")] public float touchKickMinPlayerSpeed = 3f;

    [Header("Ведение мяча")]
    [Tooltip("Дистанция точки ведения от пивота игрока, вдоль его движения")] public float dribbleDistance = 0.7f;
    [Tooltip("Жёсткость приклейки мяча к точке ведения (больше = мгновеннее переезжает на новую сторону)")] public float dribbleStiffness = 40f;
    [Tooltip("Радиус области, в которой мяч дрожит вокруг точки ведения")] public float dribbleJitterRadius = 0.06f;
    [Tooltip("Частота дрожания")] public float dribbleJitterFreq = 10f;

    [Header("Зоны")]
    [Tooltip("Радиус триггера: внутри можно заряжать удар")] public float kickZoneRadius = 2f;
    [Tooltip("Радиус удара: внутри срабатывает удар и захват ведения")] public float kickRange = 1f;

    [Header("Луч прицела")]
    public Color rayColor = Color.red;
    [Range(0.005f, 0.2f)] public float rayWidth = 0.03f;
    public float rayMinLength = 0.4f;
    public float rayMaxLength = 3f;
    [Range(0f, 1f)] [Tooltip("Альфа на кончике луча (0 = полностью тает)")] public float rayEndAlpha = 0f;
    public int raySortOrder = 1000;

    [Header("Slow motion")]
    [Range(0.1f, 1f)] public float aimTimeScale = 0.32f;
    [Min(0.01f)] public float slowMotionEnterDuration = 0.12f;
    [Min(0.01f)] public float slowMotionExitDuration = 0.08f;

    [Header("Пунктирное кольцо")]
    public Color ringColor = new Color(1f, 0.8f, 0.2f, 0.8f);
    [Range(0.01f, 0.2f)] public float ringThickness = 0.06f;
    [Range(4, 64)] public int ringDashCount = 24;
    [Range(0.1f, 0.9f)] [Tooltip("Доля штриха в шаге")] public float ringDashFill = 0.55f;
    public int ringSortOrder = 999;

    [Header("Подсветка мяча")]
    public Color glowColor = new Color(0.3f, 1f, 0.4f, 1f);
    [Range(0f, 1f)] public float glowAlpha = 0.6f;
    public float glowScale = 1.6f;
    public float glowPulseSpeed = 6f;
    public int glowSortOrder = 998;

    [Header("UI")]
    public Slider chargeSlider;
    public Color sliderColorZero = Color.red;
    public Color sliderColorOne = Color.green;
    [Tooltip("Текст-подсказка клавиши (любой UI Text)")] public Text hintText;

    [Header("Эффекты")]
    public ParticleSystem kickFX;
    public ParticleSystem impactFX;
    public AudioClip kickSound;
    public AudioClip impactSound;
    public float impactThreshold = 3f;

    private Rigidbody2D _rb;
    private Vector2 _prev;
    private Transform _kicker;
    private Rigidbody2D _kickerRb;
    private Collider2D _kickerSolid;
    private readonly List<Collider2D> _ballSolids = new List<Collider2D>();
    private int _playerContacts;
    private Vector2 _lastKickerPos;
    private Vector2 _kickerStep;
    private float _kickerSpeed;
    private bool _aiming;
    private bool _wasInRange;
    private bool _dribbling;
    private Vector2 _leadDir;
    private float _chargeT;
    private float _charge01;
    private float _touchKickNext;
    private float _hintPop;
    private bool _hintShown;
    private Color _hintBaseColor = Color.white;
    private Camera _cam;

    private LineRenderer _line;
    private LineRenderer _arrowHead;
    private GameObject _ringGo;
    private Mesh _ringMesh;
    private Material _ringMat;
    private SpriteRenderer _glow;
    private Image _sliderFill;

    private float _bRingRadius, _bRingThick, _bRingFill;
    private int _bRingDash;
    private float _distanceToKicker = Mathf.Infinity;
    private float _timeScaleBeforeAim = 1f;
    private bool _ownsSlowMotion;

    private static BallRollVisual _activeRangeBall;

    public float Charge => _charge01;
    public bool InKickRange { get; private set; }
    public bool IsDribbling => _dribbling;

    public void SetMaxPower(float v) => maxPower = Mathf.Max(minPower, v);

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _cam = Camera.main;
        EnsureZoneTrigger();
        foreach (var c in GetComponents<Collider2D>())
            if (!c.isTrigger) _ballSolids.Add(c);
        if (chargeSlider != null && chargeSlider.fillRect != null)
            _sliderFill = chargeSlider.fillRect.GetComponent<Image>();
        if (hintText != null) _hintBaseColor = hintText.color;
    }

    void OnEnable() => _prev = transform.position;

    void OnDisable()
    {
        CancelAim(true);
        ReleaseActiveRangeBall();
        if (_ringGo != null) _ringGo.SetActive(false);
        SetAimVisualVisible(false);
    }

    void OnDestroy()
    {
        RestoreTimeScaleImmediate();
        ReleaseActiveRangeBall();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) CancelAim(true);
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) CancelAim(true);
    }

    // ================= ЗОНА И ТРЕКИНГ ИГРОКА =================

    void EnsureZoneTrigger()
    {
        CircleCollider2D zone = null;
        foreach (var c in GetComponents<CircleCollider2D>())
            if (c.isTrigger) { zone = c; break; }
        if (zone == null)
        {
            zone = gameObject.AddComponent<CircleCollider2D>();
            zone.isTrigger = true;
        }
        zone.radius = kickZoneRadius;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerContacts++;
        if (_kicker == null)
        {
            _kicker = other.transform;
            _kickerRb = other.attachedRigidbody;
            _lastKickerPos = _kicker.position;
            _kickerStep = Vector2.zero;
            foreach (var c in other.GetComponents<Collider2D>())
                if (!c.isTrigger) { _kickerSolid = c; break; }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerContacts = Mathf.Max(0, _playerContacts - 1);
        if (_playerContacts == 0)
        {
            CancelAim(false);
            ReleaseActiveRangeBall();
            if (_dribbling) StopDribble();
            _kicker = null;
            _kickerRb = null;
            _kickerSolid = null;
            _kickerStep = Vector2.zero;
            _kickerSpeed = 0f;
        }
    }

    /// Направление и скорость игрока из дельты позиции — работает при ЛЮБОМ способе движения.
    void TrackKicker()
    {
        if (_kicker == null) return;
        Vector2 kp = _kicker.position;
        _kickerStep = kp - _lastKickerPos;
        _lastKickerPos = kp;
        _kickerSpeed = _kickerStep.magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
    }

    // ================= ЦИКЛ =================

    void Update()
    {
        EnsureZoneTrigger();
        TrackKicker();

        if (_ownsSlowMotion && Time.timeScale <= 0f)
            CancelAim(false);

        bool hold = (kickMouseLeft && Input.GetMouseButton(0)) || Input.GetKey(kickKey);
        bool inZone = _playerContacts > 0 && _kicker != null;
        float dist = inZone ? Vector2.Distance(_kicker.position, transform.position) : Mathf.Infinity;
        InKickRange = inZone && dist <= kickRange;
        _distanceToKicker = dist;

        UpdateActiveRangeBall();
        bool isActiveRangeBall = _activeRangeBall == this && InKickRange;
        UpdateRing(isActiveRangeBall);

        // захват ведения — только в области удара
        if (Input.GetKeyDown(dribbleKey))
        {
            if (_dribbling) StopDribble();
            else if (InKickRange) StartDribble();
        }

        if (_dribbling) UpdateDribble();

        UpdateGlow(isActiveRangeBall);

        // авто-удар при вбегании в радиус с зажатой кнопкой
        if (_aiming && autoKickOnEnter && !_dribbling && InKickRange && !_wasInRange)
        {
            DoKick();
            CancelAim(false);
        }

        if (_aiming)
        {
            if (hold && isActiveRangeBall)
            {
                _chargeT += Time.unscaledDeltaTime;
                _charge01 = pingPongCharge
                    ? Mathf.PingPong(_chargeT / chargeTime, 1f)
                    : Mathf.Clamp01(_chargeT / chargeTime);

                UpdateSlider(true);
                DrawRay();
            }
            else
            {
                if (!hold && isActiveRangeBall) DoKick();
                CancelAim(false);
            }
        }
        else if (hold && isActiveRangeBall)
        {
            BeginAim();
        }

        if (!isActiveRangeBall && _aiming)
            CancelAim(false);

        UpdateSlowMotion();

        UpdateHint();
        _wasInRange = InKickRange;
    }

    void LateUpdate()
    {
        if (visual == null) return;
        Vector2 cur = transform.position;
        Vector2 delta = cur - _prev;
        _prev = cur;

        float dist = delta.magnitude;
        if (dist < 0.0001f || dist > 1f) return;

        Vector3 axis = new Vector3(delta.y, -delta.x, 0f).normalized * rollDir;
        visual.Rotate(axis, (dist / radius) * Mathf.Rad2Deg, Space.World);
    }

    // ================= ВЕДЕНИЕ =================

    void StartDribble()
    {
        _dribbling = true;
        _leadDir = ((Vector2)transform.position - (Vector2)_kicker.position).normalized;
        SetIgnorePlayerCollision(true);
    }

    void StopDribble()
    {
        _dribbling = false;
        SetIgnorePlayerCollision(false);
    }

    void SetIgnorePlayerCollision(bool ignore)
    {
        if (_kickerSolid == null) return;
        foreach (var c in _ballSolids)
            Physics2D.IgnoreCollision(c, _kickerSolid, ignore);
    }

    void UpdateDribble()
    {
        if (_kicker == null) { StopDribble(); return; }

        // сторона переключается в тот же кадр, что и движение игрока
        if (_kickerStep.sqrMagnitude > 1e-8f) _leadDir = _kickerStep.normalized;
        else if (_leadDir.sqrMagnitude < 1e-4f) _leadDir = Vector2.down;

        // точка на луче движения на дистанции от пивота
        Vector2 anchor = (Vector2)_kicker.position + _leadDir * dribbleDistance;

        // «живое» дрожание вокруг точки ведения
        float t = Time.time * dribbleJitterFreq;
        Vector2 jitter = new Vector2(
            Mathf.PerlinNoise(t, 7.31f) * 2f - 1f,
            Mathf.PerlinNoise(t, 3.77f) * 2f - 1f) * dribbleJitterRadius;
        jitter *= Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(_kickerSpeed / 6f));

        // приклейка: скорость мяча полностью наша
        _rb.linearVelocity = (anchor + jitter - (Vector2)transform.position) * dribbleStiffness;
    }

    // ================= УДАР =================

    void DoKick()
    {
        if (_dribbling) StopDribble();

        Vector2 from = transform.position;
        Vector2 direction = MouseWorld() - from;
        if (direction.sqrMagnitude < 1e-6f) direction = Vector2.up;

        Kick(direction, Mathf.Lerp(minPower, maxPower, _charge01));

        Vector2 point = from + ((Vector2)_kicker.position - from).normalized * radius;
        SpawnFX(kickFX, point, kickSound);
    }

    void BeginAim()
    {
        _aiming = true;
        _chargeT = 0f;
        _charge01 = 0f;

        if (!_ownsSlowMotion && Time.timeScale > 0f)
        {
            _timeScaleBeforeAim = Time.timeScale;
            _ownsSlowMotion = true;
        }
    }

    void CancelAim(bool immediateTimeRestore)
    {
        _aiming = false;
        UpdateSlider(false);
        SetAimVisualVisible(false);

        if (immediateTimeRestore)
            RestoreTimeScaleImmediate();
    }

    void UpdateSlowMotion()
    {
        if (!_ownsSlowMotion)
            return;

        if (Time.timeScale <= 0f)
            return;

        float target = _aiming
            ? Mathf.Min(_timeScaleBeforeAim, aimTimeScale)
            : _timeScaleBeforeAim;
        float duration = _aiming
            ? slowMotionEnterDuration
            : slowMotionExitDuration;
        float maxDelta = Mathf.Abs(_timeScaleBeforeAim - aimTimeScale) /
            Mathf.Max(0.01f, duration) * Time.unscaledDeltaTime;

        Time.timeScale = Mathf.MoveTowards(Time.timeScale, target, maxDelta);

        if (!_aiming && Mathf.Approximately(Time.timeScale, target))
        {
            Time.timeScale = target;
            _ownsSlowMotion = false;
        }
    }

    void RestoreTimeScaleImmediate()
    {
        if (!_ownsSlowMotion)
            return;

        if (Time.timeScale > 0f)
            Time.timeScale = _timeScaleBeforeAim;

        _ownsSlowMotion = false;
    }

    public void Kick() => Kick(kickDirection, maxPower);

    public void Kick(Vector2 direction, float power)
    {
        if (_rb == null || visual == null) return;
        if (direction.sqrMagnitude < 1e-6f || power <= 0f) return;

        _rb.WakeUp();
        _rb.linearVelocity += direction.normalized * power;
        if (_rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;

        visual.Rotate(0f, 0f, Random.Range(-kickSpin, kickSpin));
    }

    public void KickFrom(Transform source, float power)
    {
        if (source == null) return;
        Kick((Vector2)transform.position - (Vector2)source.position, power);
    }

    public void ResetBall(Transform spawnPoint)
    {
        if (spawnPoint == null)
            return;

        CancelAim(true);
        if (_dribbling) StopDribble();
        ReleaseActiveRangeBall();

        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.position = spawnPoint.position;
        _rb.rotation = spawnPoint.eulerAngles.z;
        transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        _prev = transform.position;
        _playerContacts = 0;
        _kicker = null;
        _kickerRb = null;
        _kickerSolid = null;
        _kickerStep = Vector2.zero;
        _kickerSpeed = 0f;
        InKickRange = false;
        if (_ringGo != null) _ringGo.SetActive(false);
        if (_glow != null) _glow.enabled = false;
        UpdateHint();
    }

    // ================= ЭФФЕКТЫ И ТАЧ-УДАР =================

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.collider.CompareTag("Player"))
        {
            TryTouchKick(col);
            return;
        }
        if (col.relativeVelocity.magnitude < impactThreshold) return;
        SpawnFX(impactFX, col.GetContact(0).point, impactSound);
    }

    void TryTouchKick(Collision2D col)
    {
        if (!touchKick || _dribbling || _aiming) return;
        if (Time.time < _touchKickNext) return;

        float playerSpeed = _kickerSpeed;
        if (playerSpeed < touchKickMinPlayerSpeed) return; // подошёл спокойно — мяч не выбивает

        _touchKickNext = Time.time + touchKickCooldown;

        Vector2 away = (Vector2)transform.position - (Vector2)col.transform.position;
        if (away.sqrMagnitude < 1e-6f) away = Vector2.up;
        Vector2 dir = away.normalized;

        if (playerSpeed > 0.1f && _kickerStep.sqrMagnitude > 1e-8f)
            dir = (dir + _kickerStep.normalized * touchKickSpeedScale).normalized;

        Kick(dir, touchKickPower + playerSpeed * touchKickSpeedScale);
        SpawnFX(kickFX, col.GetContact(0).point, kickSound);
    }

    void SpawnFX(ParticleSystem prefab, Vector3 pos, AudioClip clip)
    {
        if (prefab != null)
        {
            var ps = Instantiate(prefab, pos, Quaternion.identity);
            Destroy(ps.gameObject, 3f);
        }
        if (clip != null) AudioSource.PlayClipAtPoint(clip, pos);
    }

    // ================= ЛУЧ =================

    Vector2 MouseWorld() => _cam.ScreenToWorldPoint(Input.mousePosition);

    static Material MakeSpriteMat() => new Material(Shader.Find("Sprites/Default"));

    void DrawRay()
    {
        EnsureLine();
        Vector2 from = transform.position;
        Vector2 dirV = MouseWorld() - from;
        if (dirV.sqrMagnitude < 1e-6f) dirV = Vector2.up;
        dirV.Normalize();

        float len = Mathf.Lerp(rayMinLength, rayMaxLength, _charge01);
        _line.SetPosition(0, from);
        Vector2 endPoint = from + dirV * len;
        _line.SetPosition(1, endPoint);

        Vector2 side = new Vector2(-dirV.y, dirV.x);
        float headLength = Mathf.Clamp(len * 0.16f, 0.28f, 0.48f);
        float headWidth = headLength * 0.62f;
        _arrowHead.SetPosition(0, endPoint - dirV * headLength + side * headWidth);
        _arrowHead.SetPosition(1, endPoint);
        _arrowHead.SetPosition(2, endPoint - dirV * headLength - side * headWidth);

        _line.startColor = rayColor;
        Color end = rayColor;
        end.a *= rayEndAlpha;
        _line.endColor = end;
        _line.startWidth = rayWidth;
        _line.endWidth = rayWidth;
        _line.enabled = true;
        _arrowHead.startColor = rayColor;
        _arrowHead.endColor = rayColor;
        _arrowHead.startWidth = rayWidth;
        _arrowHead.endWidth = rayWidth;
        _arrowHead.enabled = true;
    }

    void EnsureLine()
    {
        if (_line != null) return;
        var go = new GameObject("AimRay");
        go.transform.SetParent(transform, false);
        _line = go.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.positionCount = 2;
        _line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.6f));
        _line.material = MakeSpriteMat();
        _line.sortingOrder = raySortOrder;
        _line.enabled = false;

        var arrowObject = new GameObject("AimRayArrowHead");
        arrowObject.transform.SetParent(transform, false);
        _arrowHead = arrowObject.AddComponent<LineRenderer>();
        _arrowHead.useWorldSpace = true;
        _arrowHead.positionCount = 3;
        _arrowHead.numCapVertices = 3;
        _arrowHead.numCornerVertices = 2;
        _arrowHead.material = _line.material;
        _arrowHead.sortingOrder = raySortOrder;
        _arrowHead.enabled = false;
    }

    void SetAimVisualVisible(bool visible)
    {
        if (_line != null) _line.enabled = visible;
        if (_arrowHead != null) _arrowHead.enabled = visible;
    }

    void UpdateActiveRangeBall()
    {
        if (!InKickRange)
        {
            if (_activeRangeBall == this)
                _activeRangeBall = null;
            return;
        }

        if (_activeRangeBall == null || !_activeRangeBall.isActiveAndEnabled ||
            !_activeRangeBall.InKickRange)
        {
            _activeRangeBall = this;
            return;
        }

        if (_activeRangeBall != this &&
            _distanceToKicker < _activeRangeBall._distanceToKicker)
        {
            BallRollVisual previous = _activeRangeBall;
            _activeRangeBall = this;
            previous.LoseActiveRangeBall();
        }
    }

    void LoseActiveRangeBall()
    {
        CancelAim(false);
        if (_ringGo != null) _ringGo.SetActive(false);
        UpdateGlow(false);
    }

    void ReleaseActiveRangeBall()
    {
        if (_activeRangeBall == this)
            _activeRangeBall = null;
    }

    // ================= КОЛЬЦО =================

    void UpdateRing(bool visible)
    {
        if (!visible && _ringGo == null)
            return;

        EnsureRing();
        _ringGo.SetActive(visible);
        if (!visible) return;

        if (!Mathf.Approximately(kickRange, _bRingRadius) || !Mathf.Approximately(ringThickness, _bRingThick) ||
            !Mathf.Approximately(ringDashFill, _bRingFill) || ringDashCount != _bRingDash)
            BuildRingMesh();

        _ringMat.color = ringColor;
    }

    void EnsureRing()
    {
        if (_ringGo != null) return;
        _ringGo = new GameObject("KickRangeRing");
        _ringGo.transform.SetParent(transform, false);
        _ringGo.AddComponent<MeshFilter>();
        var mr = _ringGo.AddComponent<MeshRenderer>();
        _ringMat = MakeSpriteMat();
        mr.material = _ringMat;
        mr.sortingOrder = ringSortOrder;
        _ringMesh = new Mesh();
        _ringGo.GetComponent<MeshFilter>().mesh = _ringMesh;
        BuildRingMesh();
    }

    void BuildRingMesh()
    {
        _bRingRadius = kickRange;
        _bRingThick = ringThickness;
        _bRingFill = ringDashFill;
        _bRingDash = ringDashCount;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        float inner = Mathf.Max(0.01f, kickRange - ringThickness * 0.5f);
        float outer = kickRange + ringThickness * 0.5f;
        float step = Mathf.PI * 2f / ringDashCount;
        float dashSpan = step * ringDashFill;
        const int segPerDash = 6;

        for (int d = 0; d < ringDashCount; d++)
        {
            float a0 = d * step;
            for (int s = 0; s < segPerDash; s++)
            {
                float t0 = a0 + dashSpan * s / segPerDash;
                float t1 = a0 + dashSpan * (s + 1) / segPerDash;
                int i = verts.Count;
                verts.Add(new Vector3(Mathf.Cos(t0) * inner, Mathf.Sin(t0) * inner, 0f));
                verts.Add(new Vector3(Mathf.Cos(t0) * outer, Mathf.Sin(t0) * outer, 0f));
                verts.Add(new Vector3(Mathf.Cos(t1) * outer, Mathf.Sin(t1) * outer, 0f));
                verts.Add(new Vector3(Mathf.Cos(t1) * inner, Mathf.Sin(t1) * inner, 0f));
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
        }

        _ringMesh.Clear();
        _ringMesh.SetVertices(verts);
        _ringMesh.SetTriangles(tris, 0);
        _ringMesh.RecalculateBounds();
    }

    // ================= ПОДСВЕТКА =================

    void UpdateGlow(bool visible)
    {
        EnsureGlow();
        _glow.enabled = visible;
        if (!visible) return;

        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * glowPulseSpeed);
        Color c = glowColor;
        c.a = glowAlpha * pulse;
        _glow.color = c;
    }

    void EnsureGlow()
    {
        if (_glow != null) return;
        var go = new GameObject("BallGlow");
        go.transform.SetParent(transform, false);
        _glow = go.AddComponent<SpriteRenderer>();
        _glow.sprite = Sprite.Create(MakeRadialTexture(), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        _glow.material = MakeSpriteMat();
        _glow.sortingOrder = glowSortOrder;
        go.transform.localScale = Vector3.one * (radius * 2f * glowScale);
        _glow.enabled = false;
    }

    static Texture2D MakeRadialTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size);
        float c = size * 0.5f;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    // ================= UI =================

    void UpdateSlider(bool visible)
    {
        if (chargeSlider == null) return;
        if (chargeSlider.gameObject.activeSelf != visible)
            chargeSlider.gameObject.SetActive(visible);
        if (!visible) return;

        chargeSlider.value = _charge01;
        if (_sliderFill != null)
            _sliderFill.color = Color.Lerp(sliderColorZero, sliderColorOne, _charge01);
    }

    void UpdateHint()
    {
        if (hintText == null) return;

        bool show = _dribbling || InKickRange;

        if (show)
        {
            hintText.text = _dribbling
                ? "[" + dribbleKey.ToString() + "] отпустить мяч"
                : "[" + dribbleKey.ToString() + "] вести мяч";

            if (!_hintShown) _hintPop = 0f;
            _hintPop = Mathf.Min(1f, _hintPop + Time.deltaTime * 6f);
            hintText.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, _hintPop);
            Color c = _hintBaseColor;
            c.a = _hintPop;
            hintText.color = c;
        }

        if (_hintShown != show)
            hintText.gameObject.SetActive(show);
        _hintShown = show;
    }
}
