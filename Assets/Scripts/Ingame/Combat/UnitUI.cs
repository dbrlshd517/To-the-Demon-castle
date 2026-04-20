using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeckRoguelike.Core;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace DeckRoguelike.Combat
{
    /// <summary>
    /// 플레이어·적·아군 공통 UI 컨트롤러.
    ///
    /// [담당 기능]
    ///  - HP 바 / 텍스트 갱신
    ///  - 쉴드(방어도) 바 / 텍스트 갱신
    ///  - conditionContainer 에 행동 아이콘(Enemy/Ally 전용) + 상태이상 아이콘을 왼→오 순 배치
    ///    · [0] = Attack, [1] = Move, [2+] = Skill 등 (actionIconPrefabs 배열 인덱스와 일치)
    ///    · 항상 최소 1개 이상 존재 (공격 아이콘 기본)
    ///
    /// [Prefab 권장 구조]
    /// UnitPrefab (UnitUI 부착)
    ///  ├─ Sprite
    ///  ├─ Healthbar
    ///  │   ├─ HPBarFill     (Image Filled)     ← hpBarFill
    ///  │   └─ HPText        (TMP)              ← hpText
    ///  ├─ ShieldRoot                           ← shieldObject
    ///  │   ├─ ShieldBarFill (Image Filled)     ← shieldBarFill
    ///  │   └─ ShieldText    (TMP)              ← shieldText
    ///  └─ ConditionContainer (Horizontal LG)   ← conditionContainer
    ///       ├─ (actionIconPrefabs 동적 생성 — 행동 아이콘, 항상 앞쪽)
    ///       └─ (effectIconPrefab  동적 생성 — 상태이상 아이콘, 행동 아이콘 뒤)
    /// </summary>
    public class UnitUI : MonoBehaviour
    {
        [Header("Unit Sprite (선택)")]
        [Tooltip("유닛 스프라이트 이미지. Initialize()에서 설정됩니다.")]
        [SerializeField] private Image unitSpriteImage;

        [Header("HP Bar")]
        [SerializeField] private Image hpBarFill;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("Shield")]
        [SerializeField] private GameObject shieldObject;
        [SerializeField] private Image shieldBarFill;
        [SerializeField] private TextMeshProUGUI shieldText;

        [Header("Condition Container (행동 + 상태이상 공용)")]
        [Tooltip("행동 아이콘과 상태이상 아이콘을 함께 나열할 단일 컨테이너 (Horizontal Layout Group 권장).\n" +
                 "왼쪽: 행동 아이콘 (actionIconPrefabs) → 오른쪽: 상태이상 아이콘 (effectIconPrefab) 순으로 배치됩니다.")]
        [SerializeField] private Transform conditionContainer;

        [Tooltip("행동 타입별 프리팹 배열. [0]=공격, [1]=이동, [2+]=기타.\n" +
                 "ActionIconEntry 컴포넌트를 가진 프리팹을 각 슬롯에 넣으십시오.")]
        [SerializeField] private GameObject[] actionIconPrefabs;

        [Tooltip("EffectIconEntry 컴포넌트를 가진 상태이상 아이콘 프리팹")]
        [SerializeField] private GameObject effectIconPrefab;

        [Header("Player Only")]
        [Tooltip("플레이어 프리팹에서만 true. GameManager.OnHPChanged 이벤트로 HP를 자동 갱신합니다.")]
        [SerializeField] private bool bindToGameManager = false;

        private readonly List<GameObject> _actionObjs = new List<GameObject>();
        private readonly List<GameObject> _effectObjs  = new List<GameObject>();

        // ─────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            SetShieldVisible(false);
            SetupConditionContainer();
        }

        /// <summary>
        /// conditionContainer 에 HorizontalLayoutGroup 을 보장합니다.
        /// childAlignment = MiddleLeft → 첫 아이콘이 항상 컨테이너 왼쪽 끝 · 세로 중앙에서 시작합니다.
        /// </summary>
        private void SetupConditionContainer()
        {
            if (conditionContainer == null) return;
            var hlg = conditionContainer.GetComponent<HorizontalLayoutGroup>()
                   ?? conditionContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;
        }

        private void OnEnable()
        {
            if (bindToGameManager && GameManager.Instance != null)
                GameManager.Instance.OnHPChanged += UpdateHP;
        }

        private void OnDisable()
        {
            if (bindToGameManager && GameManager.Instance != null)
                GameManager.Instance.OnHPChanged -= UpdateHP;
        }

        private void Start()
        {
            if (bindToGameManager && GameManager.Instance != null)
                UpdateHP(GameManager.Instance.CurrentHP, GameManager.Instance.MaxHP);

            SetShieldVisible(false);
        }

        /// <summary>
        /// 부모 유닛이 좌우반전(localScale.x &lt; 0)돼도 UI는 항상 정방향을 유지합니다.
        /// </summary>
        private void LateUpdate()
        {
            Vector3 s = transform.localScale;
            if (s.x < 0f)
            {
                s.x = -s.x;
                transform.localScale = s;
            }
        }

        #endregion

        // ─────────────────────────────────────────
        #region Initialize

        /// <summary>
        /// 유닛 생성 시 초기 HP와 스프라이트를 설정합니다.
        /// 적/아군 프리팹에서 호출하십시오. 플레이어는 bindToGameManager로 자동 갱신됩니다.
        /// </summary>
        public void Initialize(int maxHP, Sprite sprite = null)
        {
            UpdateHP(maxHP, maxHP);
            UpdateShield(0, maxHP, maxHP);
            if (sprite != null && unitSpriteImage != null)
                unitSpriteImage.sprite = sprite;
        }

        #endregion

        // ─────────────────────────────────────────
        #region HP

        public void UpdateHP(int current, int max)
        {
            if (hpBarFill != null)
                hpBarFill.fillAmount = max > 0 ? (float)current / max : 0f;
            if (hpText != null)
                hpText.text = $"{current}/{max}";
        }

        #endregion

        // ─────────────────────────────────────────
        #region Shield

        /// <summary>
        /// 방어도를 갱신합니다. shieldBarFill의 크기는 HP bar(currentHP/maxHP)와 동일하게 맞춥니다.
        /// block = 현재 방어도, currentHP = 현재 HP, maxHP = 최대 HP
        /// </summary>
        public void UpdateShield(int block, int currentHP, int maxHP)
        {
            bool hasShield = block > 0;
            SetShieldVisible(hasShield);

            if (!hasShield) return;

            if (shieldBarFill != null)
                shieldBarFill.fillAmount = maxHP > 0 ? Mathf.Clamp01((float)currentHP / maxHP) : 0f;
            if (shieldText != null)
                shieldText.text = block.ToString();
        }

        private void SetShieldVisible(bool visible)
        {
            if (shieldObject != null)
                shieldObject.SetActive(visible);
            if (shieldBarFill != null)
                shieldBarFill.gameObject.SetActive(visible);
            if (shieldText != null)
                shieldText.gameObject.SetActive(visible);
        }

        #endregion

        // ─────────────────────────────────────────
        #region Action Icons (Enemy / Ally)

        /// <summary>
        /// 행동 아이콘 목록을 새로 그립니다. Enemy / Ally 전용.
        /// conditionContainer의 앞쪽(sibling 0부터)에 배치되며,
        /// 기존 상태이상 아이콘은 행동 아이콘 뒤로 밀립니다.
        /// </summary>
        public void RefreshActions(List<UnitActionEntry> actions)
        {
            foreach (var obj in _actionObjs) if (obj != null) Destroy(obj);
            _actionObjs.Clear();

            if (actions == null || conditionContainer == null
                || actionIconPrefabs == null || actionIconPrefabs.Length == 0) return;

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                int prefabIdx = Mathf.Clamp((int)action.Type, 0, actionIconPrefabs.Length - 1);
                if (actionIconPrefabs[prefabIdx] == null) continue;

                var newObj = Instantiate(actionIconPrefabs[prefabIdx], conditionContainer);
                ResetRectTransform(newObj);
                var entry  = newObj.GetComponent<EffectIconEntry>();
                if (entry == null) entry = newObj.AddComponent<EffectIconEntry>();
                entry.Initialize(action.Type.ToString(), action.Icon, action.Value, showPlusSign: false);

                // 행동 아이콘을 항상 앞쪽에 고정 (상태이상 아이콘보다 왼쪽)
                newObj.transform.SetSiblingIndex(i);
                _actionObjs.Add(newObj);
            }
        }

        public void ClearActions()
        {
            foreach (var obj in _actionObjs) if (obj != null) Destroy(obj);
            _actionObjs.Clear();
        }

        #endregion

        // ─────────────────────────────────────────
        #region Effect Icons (상태이상 / 버프)

        /// <summary>
        /// 상태이상·버프 아이콘을 추가하거나 수치를 갱신합니다.
        /// conditionContainer의 행동 아이콘 뒤(오른쪽)에 왼쪽 → 오른쪽 순으로 배치됩니다.
        /// key 가 같은 아이콘이 이미 있으면 수치만 업데이트합니다.
        /// value = 0 이하이면 아이콘을 제거합니다.
        /// </summary>
        public void SetEffect(string key, Sprite icon, int value)
        {
            if (value <= 0)
            {
                RemoveEffect(key);
                return;
            }

            // 기존 아이콘 갱신
            foreach (var obj in _effectObjs)
            {
                if (obj == null) continue;
                var entry = obj.GetComponent<EffectIconEntry>();
                if (entry != null && entry.Key == key)
                {
                    entry.UpdateValue(value);
                    return;
                }
            }

            // 새 아이콘 생성 — conditionContainer 끝에 추가 (행동 아이콘보다 항상 오른쪽)
            if (effectIconPrefab == null || conditionContainer == null) return;
            var newObj = Instantiate(effectIconPrefab, conditionContainer);
            ResetRectTransform(newObj);
            var newEntry = newObj.GetComponent<EffectIconEntry>();
            if (newEntry == null) newEntry = newObj.AddComponent<EffectIconEntry>();
            newEntry.Initialize(key, icon, value);
            _effectObjs.Add(newObj);
        }

        public void RemoveEffect(string key)
        {
            for (int i = _effectObjs.Count - 1; i >= 0; i--)
            {
                if (_effectObjs[i] == null) { _effectObjs.RemoveAt(i); continue; }
                var entry = _effectObjs[i].GetComponent<EffectIconEntry>();
                if (entry != null && entry.Key == key)
                {
                    Destroy(_effectObjs[i]);
                    _effectObjs.RemoveAt(i);
                    return;
                }
            }
        }

        public void ClearEffects()
        {
            foreach (var obj in _effectObjs)
                if (obj != null) Destroy(obj);
            _effectObjs.Clear();
        }

        #endregion

        // ─────────────────────────────────────────
        #region Helpers

        /// <summary>
        /// 인스턴스화 직후 RectTransform을 conditionContainer 기준으로 초기화합니다.
        /// 프리팹에 저장된 위치값이 Layout Group 배치를 방해하지 않도록 합니다.
        /// </summary>
        private static void ResetRectTransform(GameObject obj)
        {
            var rt = obj.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.localPosition   = Vector3.zero;
            rt.localRotation   = Quaternion.identity;
            rt.localScale      = Vector3.one;
            rt.anchoredPosition = Vector2.zero;
        }

        #endregion
    }
}
