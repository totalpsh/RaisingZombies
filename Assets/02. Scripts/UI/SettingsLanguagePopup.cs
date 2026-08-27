using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 언어 선택 결과를 SettingsController에 전달할 팝업 입력값을 표현한다.
public sealed class SettingsLanguagePopupParameters
{
    public string currentLanguageCode; // 팝업을 열 때 선택되어 있는 Locale 코드
    public Action<string> languageSelected; // 사용자가 언어를 선택했을 때 실행할 콜백
}

// UIManager의 PopUp 레이어에서 한국어와 영어 선택을 처리한다.
public sealed class SettingsLanguagePopup : BaseUI
{
    [SerializeField] private Button koreanButton; // 한국어 Locale을 선택하는 버튼
    [SerializeField] private Button englishButton; // 영어 Locale을 선택하는 버튼
    [SerializeField] private Button closeButton; // 언어 선택 없이 팝업을 닫는 버튼
    [SerializeField] private TMP_Text koreanText; // 한국어 선택 항목의 표시 텍스트
    [SerializeField] private TMP_Text englishText; // 영어 선택 항목의 표시 텍스트
    private SettingsLanguagePopupParameters parameters; // 현재 팝업 호출자가 전달한 선택 정보
    private bool listenersRegistered; // 버튼 Listener 중복 등록 방지 상태

    // 전달받은 현재 언어를 표시하고 버튼 입력을 준비한다.
    public override void Init(object param = null)
    {
        parameters = param as SettingsLanguagePopupParameters;
        RegisterListeners();
        RefreshSelection();
    }

    // 활성화될 때 재사용된 팝업의 버튼 입력을 다시 연결한다.
    private void OnEnable()
    {
        RegisterListeners();
    }

    // 비활성화될 때 버튼 Listener를 해제한다.
    private void OnDisable()
    {
        UnregisterListeners();
    }

    // 한국어를 선택하고 팝업을 닫는다.
    private void SelectKorean()
    {
        SelectLanguage("ko");
    }

    // 영어를 선택하고 팝업을 닫는다.
    private void SelectEnglish()
    {
        SelectLanguage("en");
    }

    // 선택한 Locale 코드를 호출자에게 전달한다.
    private void SelectLanguage(string languageCode)
    {
        parameters?.languageSelected?.Invoke(languageCode);
        ClosePopup();
    }

    // UIManager의 기존 풀링 경로로 현재 팝업을 닫는다.
    private void ClosePopup()
    {
        if (UIManager.HasInstance) UIManager.Instance.CloseUI(this);
        else gameObject.SetActive(false);
    }

    // 현재 선택된 언어 항목을 간단한 체크 표시로 갱신한다.
    private void RefreshSelection()
    {
        string currentCode = parameters == null ? string.Empty : parameters.currentLanguageCode; // 팝업을 연 시점의 Locale 코드
        if (koreanText != null) koreanText.text = string.Equals(currentCode, "ko", StringComparison.Ordinal) ? "✓ 한국어" : "한국어";
        if (englishText != null) englishText.text = string.Equals(currentCode, "en", StringComparison.Ordinal) ? "✓ English" : "English";
    }

    // 세 버튼의 Listener를 한 번만 등록한다.
    private void RegisterListeners()
    {
        if (listenersRegistered) return;
        AddListener(koreanButton, SelectKorean);
        AddListener(englishButton, SelectEnglish);
        AddListener(closeButton, ClosePopup);
        listenersRegistered = true;
    }

    // 세 버튼의 Listener를 안전하게 해제한다.
    private void UnregisterListeners()
    {
        if (!listenersRegistered) return;
        RemoveListener(koreanButton, SelectKorean);
        RemoveListener(englishButton, SelectEnglish);
        RemoveListener(closeButton, ClosePopup);
        listenersRegistered = false;
    }

    // 버튼이 존재할 때 지정한 클릭 함수를 등록한다.
    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // 버튼이 존재할 때 지정한 클릭 함수를 해제한다.
    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.RemoveListener(action);
    }
}
