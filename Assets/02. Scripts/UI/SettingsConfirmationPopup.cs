using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 위험 작업 확인 팝업에 표시할 내용과 실제 실행 콜백을 표현한다.
public sealed class SettingsConfirmationPopupParameters
{
    public string title; // 확인 팝업 제목
    public string message; // 사용자에게 보여줄 위험 또는 제한 안내
    public string confirmLabel; // 확인 버튼에 표시할 문구
    public string cancelLabel; // 취소 버튼에 표시할 문구
    public Action confirmed; // 실제 기능이 존재할 때만 전달할 확인 콜백
}

// UIManager의 PopUp 레이어에서 취소와 확인을 안전하게 처리한다.
public sealed class SettingsConfirmationPopup : BaseUI
{
    [SerializeField] private TMP_Text titleText; // 확인 팝업 제목 텍스트
    [SerializeField] private TMP_Text messageText; // 위험 또는 제한 안내 텍스트
    [SerializeField] private TMP_Text confirmText; // 확인 버튼 문구 텍스트
    [SerializeField] private TMP_Text cancelText; // 취소 버튼 문구 텍스트
    [SerializeField] private Button confirmButton; // 실제 확인 동작을 실행하는 버튼
    [SerializeField] private Button cancelButton; // 아무 작업 없이 팝업을 닫는 버튼
    private SettingsConfirmationPopupParameters parameters; // 현재 팝업 호출자가 전달한 확인 정보
    private bool listenersRegistered; // 버튼 Listener 중복 등록 방지 상태

    // 전달받은 문구와 실제 실행 가능 여부를 UI에 반영한다.
    public override void Init(object param = null)
    {
        parameters = param as SettingsConfirmationPopupParameters;
        RegisterListeners();
        RefreshContent();
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

    // 실제 콜백이 있는 경우에만 위험 작업을 실행하고 닫는다.
    private void Confirm()
    {
        if (parameters?.confirmed == null) return;
        parameters.confirmed.Invoke();
        ClosePopup();
    }

    // UIManager의 기존 풀링 경로로 현재 팝업을 닫는다.
    private void ClosePopup()
    {
        if (UIManager.HasInstance) UIManager.Instance.CloseUI(this);
        else gameObject.SetActive(false);
    }

    // 호출자가 전달한 문구와 확인 가능 여부를 표시한다.
    private void RefreshContent()
    {
        if (titleText != null) titleText.text = parameters?.title ?? "Confirm";
        if (messageText != null) messageText.text = parameters?.message ?? string.Empty;
        if (confirmText != null) confirmText.text = parameters?.confirmLabel ?? "확인";
        if (cancelText != null) cancelText.text = parameters?.cancelLabel ?? "취소";
        if (confirmButton != null) confirmButton.interactable = parameters?.confirmed != null;
    }

    // 확인과 취소 버튼 Listener를 한 번만 등록한다.
    private void RegisterListeners()
    {
        if (listenersRegistered) return;
        AddListener(confirmButton, Confirm);
        AddListener(cancelButton, ClosePopup);
        listenersRegistered = true;
    }

    // 확인과 취소 버튼 Listener를 안전하게 해제한다.
    private void UnregisterListeners()
    {
        if (!listenersRegistered) return;
        RemoveListener(confirmButton, Confirm);
        RemoveListener(cancelButton, ClosePopup);
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
