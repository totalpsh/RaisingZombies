using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 현재 등급 확률과 하나의 재사용 가능한 레벨별 확률 패널을 표시합니다.
public sealed class GachaProbabilityView : MonoBehaviour
{
    private const int RarityCount = 6; // 확률 UI에서 사용하는 전체 등급 수

    [SerializeField] private UpgradeManager upgradeManager; // 실제 가챠 상태와 밸런스를 제공하는 매니저

    [Header("현재 레벨 등급 확률")]
    [SerializeField] private TMP_Text normalRateText; // 노말 확률 텍스트
    [SerializeField] private TMP_Text uncommonRateText; // 언커먼 확률 텍스트
    [SerializeField] private TMP_Text rareRateText; // 레어 확률 텍스트
    [SerializeField] private TMP_Text epicRateText; // 에픽 확률 텍스트
    [SerializeField] private TMP_Text uniqueRateText; // 유니크 확률 텍스트
    [SerializeField] private TMP_Text legendaryRateText; // 레전더리 확률 텍스트

    [Header("상세 확률 팝업")]
    [SerializeField] private Button probabilityButton; // 상세 확률 팝업을 여는 버튼
    [SerializeField] private GameObject probabilityPopupRoot; // 켜고 끌 상세 확률 팝업 루트
    [SerializeField] private Button closeButton; // 상세 확률 팝업을 닫는 버튼

    [Header("단일 확률 패널")]
    [SerializeField] private GameObject probabilityPanelRoot; // 모든 레벨이 함께 사용하는 확률 패널 루트
    [SerializeField] private Image[] popupFillImages = new Image[RarityCount]; // Normal부터 Legendary 순서의 확률 Fill 이미지
    [SerializeField] private TMP_Text[] popupPercentTexts = new TMP_Text[RarityCount]; // Normal부터 Legendary 순서의 확률 텍스트
    [SerializeField] private Button previousPageButton; // 이전 가챠 레벨 조회 버튼
    [SerializeField] private Button nextPageButton; // 다음 가챠 레벨 조회 버튼
    [SerializeField] private TMP_Text pageIndicatorText; // 현재 조회 중인 가챠 레벨 텍스트
    [SerializeField] private TMP_Text newlyUnlockedStatsText; // 조회 레벨에서 새로 해금되는 스탯 텍스트
    [SerializeField] private TMP_Text drawCostText; // 조회 레벨의 1회 뽑기 비용 텍스트
    [SerializeField] private TMP_Text drawsToNextLevelText; // 조회 레벨의 다음 레벨 필요 뽑기 수 텍스트
    [SerializeField, Min(0f)] private float probabilityAnimationDuration = 0.35f; // 확률 변경 애니메이션 시간

    private readonly float[] _displayedProbabilities = new float[RarityCount]; // 현재 UI에 실제로 표시 중인 정규화 확률
    private readonly float[] _animationStartProbabilities = new float[RarityCount]; // 현재 애니메이션의 시작 확률
    private readonly float[] _animationTargetProbabilities = new float[RarityCount]; // 현재 애니메이션의 목표 확률
    private readonly StringBuilder _statNameBuilder = new(128); // 해금 스탯 이름 문자열 재사용 버퍼
    private int _displayedProbabilityIndex; // 확률표에서 현재 조회 중인 밸런스 배열 순번
    private Coroutine _probabilityAnimation; // 실행 중인 확률 변경 애니메이션

    // 버튼과 상태 변경 이벤트를 연결하고 현재 확률을 표시합니다.
    private void OnEnable()
    {
        ResolveManager();
        AddButtonListener(probabilityButton, OpenProbabilityPopup);
        AddButtonListener(closeButton, CloseProbabilityPopup);
        AddButtonListener(previousPageButton, ShowPreviousPage);
        AddButtonListener(nextPageButton, ShowNextPage);
        SubscribeManager();
        CloseProbabilityPopup();
        RefreshAllContent();
    }

    // 비활성화될 때 버튼, 상태 이벤트, 확률 애니메이션을 해제합니다.
    private void OnDisable()
    {
        RemoveButtonListener(probabilityButton, OpenProbabilityPopup);
        RemoveButtonListener(closeButton, CloseProbabilityPopup);
        RemoveButtonListener(previousPageButton, ShowPreviousPage);
        RemoveButtonListener(nextPageButton, ShowNextPage);
        UnsubscribeManager();
        StopProbabilityAnimation();
    }

    // 런타임 또는 테스트에서 사용할 매니저를 교체합니다.
    public void SetUpgradeManager(UpgradeManager manager)
    {
        UnsubscribeManager();
        upgradeManager = manager;
        SubscribeManager();
        RefreshAllContent();
    }

    // 현재 가챠 레벨의 여섯 등급 확률을 각각 표시합니다.
    public void RefreshCurrentRates()
    {
        UpgradeBalanceSettings balance = upgradeManager == null ? null : upgradeManager.BalanceSettings; // 현재 실제 가챠 밸런스
        int level = upgradeManager == null ? 1 : upgradeManager.GachaLevel; // 화면에 표시할 현재 가챠 레벨
        SetRateText(normalRateText, balance, level, GachaRarity.Normal);
        SetRateText(uncommonRateText, balance, level, GachaRarity.Uncommon);
        SetRateText(rareRateText, balance, level, GachaRarity.Rare);
        SetRateText(epicRateText, balance, level, GachaRarity.Epic);
        SetRateText(uniqueRateText, balance, level, GachaRarity.Unique);
        SetRateText(legendaryRateText, balance, level, GachaRarity.Legendary);
    }

    // 팝업을 열고 실제 플레이어 가챠 레벨의 확률을 즉시 표시합니다.
    public void OpenProbabilityPopup()
    {
        if (probabilityPopupRoot == null) return;
        ResolveManager();
        SubscribeManager();
        probabilityPopupRoot.SetActive(true);
        if (probabilityPanelRoot != null) probabilityPanelRoot.SetActive(true);
        ShowCurrentLevelPage();
    }

    // 상세 확률 팝업을 닫고 실행 중인 표시 애니메이션을 중단합니다.
    public void CloseProbabilityPopup()
    {
        StopProbabilityAnimation();
        if (probabilityPopupRoot != null) probabilityPopupRoot.SetActive(false);
    }

    // 현재 조회 레벨의 바로 이전 밸런스 확률을 표시합니다.
    public void ShowPreviousPage()
    {
        ShowProbabilityLevel(_displayedProbabilityIndex - 1, true);
    }

    // 현재 조회 레벨의 바로 다음 밸런스 확률을 표시합니다.
    public void ShowNextPage()
    {
        ShowProbabilityLevel(_displayedProbabilityIndex + 1, true);
    }

    // 저장된 실제 가챠 레벨과 같은 확률을 애니메이션 없이 표시합니다.
    public void ShowCurrentLevelPage()
    {
        int currentLevel = upgradeManager == null ? 1 : upgradeManager.GachaLevel; // 처음 열 때 기준이 되는 실제 가챠 레벨
        ShowProbabilityLevel(FindPageIndex(currentLevel), false);
    }

    // 씬의 싱글턴 매니저가 이미 준비됐다면 자동으로 참조합니다.
    private void ResolveManager()
    {
        if (upgradeManager == null && UpgradeManager.HasInstance) upgradeManager = UpgradeManager.Instance;
    }

    // 상태 변경 이벤트를 중복 없이 연결합니다.
    private void SubscribeManager()
    {
        if (!isActiveAndEnabled || upgradeManager == null) return;
        upgradeManager.stateChanged -= HandleStateChanged;
        upgradeManager.stateChanged += HandleStateChanged;
    }

    // 연결된 상태 변경 이벤트를 해제합니다.
    private void UnsubscribeManager()
    {
        if (upgradeManager != null) upgradeManager.stateChanged -= HandleStateChanged;
    }

    // 실제 상태가 변경되면 메인 확률과 열려 있는 조회 패널을 갱신합니다.
    private void HandleStateChanged()
    {
        RefreshAllContent();
    }

    // 현재 확률과 열려 있는 단일 확률 패널을 갱신합니다.
    private void RefreshAllContent()
    {
        RefreshCurrentRates();
        if (probabilityPopupRoot != null && probabilityPopupRoot.activeSelf)
        {
            ShowProbabilityLevel(_displayedProbabilityIndex, false);
        }
    }

    // 밸런스 배열 순번에 해당하는 레벨 확률을 단일 패널에 표시합니다.
    private void ShowProbabilityLevel(int levelIndex, bool animate)
    {
        UpgradeBalanceSettings balance = upgradeManager == null ? null : upgradeManager.BalanceSettings; // 조회에만 사용하는 실제 가챠 밸런스
        int levelCount = GetLevelCount(balance); // 이동 가능한 실제 가챠 레벨 수
        if (levelCount == 0)
        {
            _displayedProbabilityIndex = 0;
            SetAllProbabilitiesImmediate(0f);
            UpdatePageInformation(null);
            UpdatePageNavigation(0, 0);
            return;
        }

        _displayedProbabilityIndex = Mathf.Clamp(levelIndex, 0, levelCount - 1);
        GachaLevelDefinition definition = balance.GachaLevels[_displayedProbabilityIndex]; // 현재 조회할 가챠 레벨 정의
        int level = definition == null ? _displayedProbabilityIndex + 1 : definition.level; // UI와 확률 조회에 사용할 실제 레벨 값

        for (int rarityIndex = 0; rarityIndex < RarityCount; rarityIndex++) // 여섯 등급의 목표 확률을 같은 순서로 준비
        {
            _animationTargetProbabilities[rarityIndex] = balance.GetRarityProbability(level, (GachaRarity)rarityIndex);
        }

        UpdatePageInformation(definition);
        UpdatePageNavigation(levelCount, level);

        if (animate)
        {
            StartProbabilityAnimation();
        }
        else
        {
            StopProbabilityAnimation();
            ApplyTargetProbabilitiesImmediate();
        }
    }

    // 현재 표시값을 시작점으로 삼아 여섯 등급 확률 애니메이션을 동시에 시작합니다.
    private void StartProbabilityAnimation()
    {
        StopProbabilityAnimation();
        for (int rarityIndex = 0; rarityIndex < RarityCount; rarityIndex++) // 연속 입력 시 현재 화면 값을 새 시작값으로 보존
        {
            _animationStartProbabilities[rarityIndex] = _displayedProbabilities[rarityIndex];
        }

        if (probabilityAnimationDuration <= 0f || !isActiveAndEnabled)
        {
            ApplyTargetProbabilitiesImmediate();
            return;
        }

        _probabilityAnimation = StartCoroutine(AnimateProbabilities());
    }

    // OutCubic 보간으로 모든 확률 텍스트와 Fill을 같은 진행률로 변경합니다.
    private IEnumerator AnimateProbabilities()
    {
        float elapsed = 0f; // 현재 애니메이션에서 흐른 실제 시간
        while (elapsed < probabilityAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / probabilityAnimationDuration); // 0부터 1까지의 선형 진행률
            float inverse = 1f - progress; // OutCubic 계산에 사용하는 남은 진행률
            float easedProgress = 1f - inverse * inverse * inverse; // 끝에서 부드럽게 감속하는 OutCubic 진행률

            for (int rarityIndex = 0; rarityIndex < RarityCount; rarityIndex++) // 여섯 등급을 한 프레임에 함께 갱신
            {
                float probability = Mathf.Lerp(
                    _animationStartProbabilities[rarityIndex],
                    _animationTargetProbabilities[rarityIndex],
                    easedProgress); // 텍스트와 Fill이 함께 사용할 현재 표시 확률
                SetDisplayedProbability(rarityIndex, probability);
            }

            yield return null;
        }

        ApplyTargetProbabilitiesImmediate();
    }

    // 준비된 목표 확률을 애니메이션 없이 정확한 최종값으로 적용합니다.
    private void ApplyTargetProbabilitiesImmediate()
    {
        for (int rarityIndex = 0; rarityIndex < RarityCount; rarityIndex++) // 모든 등급의 마지막 값을 정확하게 적용
        {
            SetDisplayedProbability(rarityIndex, _animationTargetProbabilities[rarityIndex]);
        }

        _probabilityAnimation = null;
    }

    // 여섯 등급 확률을 모두 같은 값으로 즉시 표시합니다.
    private void SetAllProbabilitiesImmediate(float probability)
    {
        StopProbabilityAnimation();
        for (int rarityIndex = 0; rarityIndex < RarityCount; rarityIndex++) // 연결된 모든 확률 행을 즉시 갱신
        {
            _animationTargetProbabilities[rarityIndex] = probability;
            SetDisplayedProbability(rarityIndex, probability);
        }
    }

    // 한 등급의 표시 확률, 정수 퍼센트 텍스트, Fill 양을 같은 값으로 갱신합니다.
    private void SetDisplayedProbability(int rarityIndex, float probability)
    {
        float normalizedProbability = Mathf.Clamp01(probability); // UI에 안전하게 적용할 0부터 1 사이 확률
        _displayedProbabilities[rarityIndex] = normalizedProbability;

        Image fillImage = GetArrayItem(popupFillImages, rarityIndex); // 현재 등급의 확률 Fill 이미지
        TMP_Text percentText = GetArrayItem(popupPercentTexts, rarityIndex); // 현재 등급의 독립 확률 텍스트
        if (fillImage != null) fillImage.fillAmount = normalizedProbability;
        if (percentText != null) percentText.SetText("{0:0}%", normalizedProbability * 100f);
    }

    // 진행 중인 확률 표시 애니메이션만 중단하고 현재 화면 값은 유지합니다.
    private void StopProbabilityAnimation()
    {
        if (_probabilityAnimation == null) return;
        StopCoroutine(_probabilityAnimation);
        _probabilityAnimation = null;
    }

    // 현재 조회 레벨의 비용, 승급 조건, 신규 해금 스탯을 갱신합니다.
    private void UpdatePageInformation(GachaLevelDefinition definition)
    {
        if (definition == null)
        {
            SetText(newlyUnlockedStatsText, ": 정보 없음");
            SetText(drawCostText, ": -");
            SetText(drawsToNextLevelText, ": -");
            return;
        }

        SetText(drawCostText, $": {definition.drawCost:N0}");
        SetText(drawsToNextLevelText, definition.drawsToNextLevel <= 0 ? ": MAX" : $": {definition.drawsToNextLevel:N0}");
        SetText(newlyUnlockedStatsText, BuildUnlockedStatNames(definition));
    }

    // 현재 조회 레벨에서 새로 해금되는 스탯 이름을 밸런스 표시명으로 만듭니다.
    private string BuildUnlockedStatNames(GachaLevelDefinition definition)
    {
        _statNameBuilder.Clear();
        _statNameBuilder.Append(": ");
        if (definition.newlyUnlockedStats == null || definition.newlyUnlockedStats.Length == 0)
        {
            _statNameBuilder.Append("없음");
            return _statNameBuilder.ToString();
        }

        UpgradeBalanceSettings balance = upgradeManager == null ? null : upgradeManager.BalanceSettings; // 한국어 표시명을 조회할 밸런스
        for (int index = 0; index < definition.newlyUnlockedStats.Length; index++) // 신규 해금 스탯 이름을 쉼표로 연결
        {
            if (index > 0) _statNameBuilder.Append(", ");
            UpgradeStatType statType = definition.newlyUnlockedStats[index]; // 현재 이름을 표시할 신규 해금 스탯
            UpgradeStatDefinition statDefinition = balance == null ? null : balance.GetStat(statType); // 해당 스탯의 표시 설정
            _statNameBuilder.Append(statDefinition == null || string.IsNullOrWhiteSpace(statDefinition.displayName)
                ? statType.ToString()
                : statDefinition.displayName);
        }

        return _statNameBuilder.ToString();
    }

    // 실제 가챠 레벨과 일치하는 밸런스 배열 순번을 찾습니다.
    private int FindPageIndex(int level)
    {
        UpgradeBalanceSettings balance = upgradeManager == null ? null : upgradeManager.BalanceSettings; // 실제 레벨을 찾을 밸런스
        if (balance != null && balance.GachaLevels != null)
        {
            for (int index = 0; index < balance.GachaLevels.Count; index++) // 실제 플레이어 레벨과 같은 정의를 탐색
            {
                GachaLevelDefinition definition = balance.GachaLevels[index]; // 비교할 가챠 레벨 정의
                if (definition != null && definition.level == level) return index;
            }
        }

        return Mathf.Max(0, level - 1);
    }

    // 페이지 레벨 표시와 좌우 조회 버튼의 경계 상태를 갱신합니다.
    private void UpdatePageNavigation(int levelCount, int level)
    {
        if (previousPageButton != null) previousPageButton.interactable = levelCount > 0 && _displayedProbabilityIndex > 0;
        if (nextPageButton != null) nextPageButton.interactable = levelCount > 0 && _displayedProbabilityIndex < levelCount - 1;
        SetText(pageIndicatorText, levelCount == 0 ? "가챠 레벨 정보 없음" : $"Gacha Level {level}");
    }

    // 밸런스에 실제로 정의된 이동 가능한 가챠 레벨 수를 반환합니다.
    private static int GetLevelCount(UpgradeBalanceSettings balance)
    {
        return balance == null || balance.GachaLevels == null ? 0 : balance.GachaLevels.Count;
    }

    // 배열 범위를 확인한 뒤 해당 Image를 반환합니다.
    private static Image GetArrayItem(Image[] items, int index)
    {
        return items != null && index >= 0 && index < items.Length ? items[index] : null;
    }

    // 배열 범위를 확인한 뒤 해당 TMP 텍스트를 반환합니다.
    private static TMP_Text GetArrayItem(TMP_Text[] items, int index)
    {
        return items != null && index >= 0 && index < items.Length ? items[index] : null;
    }

    // TMP 참조가 있을 때만 텍스트를 설정합니다.
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }

    // 현재 화면의 등급별 TMP 텍스트에 실제 정규화 확률을 설정합니다.
    private static void SetRateText(TMP_Text target, UpgradeBalanceSettings balance, int level, GachaRarity rarity)
    {
        if (target == null) return;
        float percentage = balance == null ? 0f : balance.GetRarityProbability(level, rarity) * 100f; // 현재 등급 표시 퍼센트
        target.text = FormatPercent(percentage);
    }

    // 확률을 불필요한 소수점 없이 퍼센트 문자열로 만듭니다.
    private static string FormatPercent(float percentage)
    {
        return $"{percentage:0.##}%";
    }

    // 버튼 이벤트를 중복 제거 후 연결합니다.
    private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // 연결된 버튼 이벤트를 해제합니다.
    private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.RemoveListener(action);
    }
}
