#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 기존 UpgradeMenuController 루트 안의 8개 슬롯과 결과·비교·상세 UI를 연결한다.
public static class BodyEquipmentRootUIConnectTool
{
    private const string PrefabPath = "Assets/03. Prefabs/UI/Upgrade/UpgradeMenuController.prefab"; // 수정할 기존 메뉴 원본 프리팹
    private static readonly (string name, BodyEquipmentSlot slot)[] Slots = // 기존 Hierarchy 이름과 실제 저장 슬롯의 대응
    {
        ("Slot_Helmet", BodyEquipmentSlot.Head), ("Slot_Torso", BodyEquipmentSlot.Torso),
        ("Slot_Arms", BodyEquipmentSlot.Arms), ("Slot_Legs", BodyEquipmentSlot.Legs),
        ("Slot_Eyes", BodyEquipmentSlot.Eyes), ("Slot_Jaw", BodyEquipmentSlot.Jaw),
        ("Slot_Heart", BodyEquipmentSlot.Heart), ("Slot_Spine", BodyEquipmentSlot.Spine)
    };
    private static TMP_FontAsset _font; // 기존 메뉴의 TMP Font Asset을 재사용한다

    // 기존 프리팹 안에서만 누락된 View와 Popup을 만들고 참조를 연결한다.
    [MenuItem("Tools/Raising Zombies/Body Equipment/Connect Root Slots And Popups")]
    public static void Connect()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath); // 원본 프리팹의 편집용 인스턴스
        try
        {
            UpgradeMenuController menu = contents.GetComponent<UpgradeMenuController>(); // 현재 강화 메뉴 컨트롤러
            if (menu == null) { Debug.LogError("[BodyEquipmentUI] UpgradeMenuController가 없습니다."); return; }
            SerializedObject menuFields = new(menu); // 기존 Stat Root 참조 조회
            GameObject statRoot = menuFields.FindProperty("statUpgradeRoot").objectReferenceValue as GameObject; // 신체 뽑기를 표시하는 기존 Root
            Transform drawRoot = statRoot == null ? null : DirectChild(statRoot.transform, "BodyEquipmentDrawRoot"); // 현재 사용 중인 뽑기 화면
            Transform inventory = drawRoot == null ? null : DirectChild(drawRoot, "Inventory"); // 기존 8개 장착 슬롯의 부모
            BodyDrawPanel panel = drawRoot == null ? null : drawRoot.GetComponent<BodyDrawPanel>(); // 현재 뽑기 버튼 컨트롤러
            if (inventory == null || panel == null) { Debug.LogError("[BodyEquipmentUI] 기존 BodyEquipmentDrawRoot/Inventory/BodyDrawPanel 연결이 없습니다."); return; }
            foreach ((string name, BodyEquipmentSlot _) in Slots)
                if (DirectChild(inventory, name) == null) { Debug.LogError($"[BodyEquipmentUI] 기존 슬롯 {name}이 없습니다. 프리팹을 수정하지 않았습니다."); return; }

            TMP_Text[] existingTexts = contents.GetComponentsInChildren<TMP_Text>(true); // 프로젝트에서 이미 사용하는 TMP Text
            _font = null;
            foreach (TMP_Text text in existingTexts) if (text != null && text.font != null) { _font = text.font; break; }
            BodyEquipmentSlotView[] views = new BodyEquipmentSlotView[Slots.Length]; // 순서가 아니라 각 View의 SlotType으로 식별할 8개 참조
            for (int index = 0; index < Slots.Length; index++) views[index] = ConnectSlot(DirectChild(inventory, Slots[index].name), Slots[index].slot);

            Transform popupLayer = EnsureRect(statRoot.transform, "BodyEquipmentPopupLayer"); // UpgradeMenuController/StatUpgradeRoot 내부 모달 레이어
            Stretch(popupLayer.GetComponent<RectTransform>());
            popupLayer.SetAsLastSibling();
            Transform center = EnsureRect(drawRoot, "CenterDrawResultRoot"); // 기존 뽑기 화면 내부 중앙 결과
            RectTransform centerRect = center.GetComponent<RectTransform>(); // 중앙 결과의 프리팹 레이아웃
            centerRect.anchorMin = centerRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerRect.anchoredPosition = Vector2.zero;
            centerRect.sizeDelta = new Vector2(220f, 235f);
            BodyEquipmentInfoView centerView = EnsureInfoView(center, "ResultEquipment"); // 결과의 공통 장비 표시
            center.gameObject.SetActive(false);

            BodyEquipmentDetailPopup detail = EnsureDetailPopup(popupLayer); // 장착 슬롯 클릭용 단일 장비 팝업
            BodyEquipmentComparePopup compare = ConnectDesignedItemDetail(contents.transform); // UpgradeMenuController 아래 사용자가 만든 ItemDetail 비교 화면
            if (compare == null) compare = EnsureComparePopup(popupLayer); // ItemDetail이 없을 때만 최소 비교 화면을 사용한다
            SerializedObject fields = new(panel); // 기존 Panel에 새 루트 내부 참조 연결
            SerializedProperty slotProperty = fields.FindProperty("equippedSlots"); // 8개 장착 View 배열
            slotProperty.arraySize = views.Length;
            for (int index = 0; index < views.Length; index++) slotProperty.GetArrayElementAtIndex(index).objectReferenceValue = views[index];
            Set(fields, "centerResultView", centerView);
            Set(fields, "centerResultRoot", center.gameObject);
            Set(fields, "detailPopup", detail);
            Set(fields, "comparePopup", compare);
            fields.ApplyModifiedPropertiesWithoutUndo();
            ConnectResultCardLabel();
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[BodyEquipmentUI] 기존 8개 슬롯과 중앙 결과·상세·비교 팝업을 UpgradeMenuController 루트 내부에 연결했습니다. 기존 배치 값은 유지했습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    // ItemDetail의 Popup_1과 Popup_2를 현재 장비와 새 장비 비교 View로 연결한다.
    private static BodyEquipmentComparePopup ConnectDesignedItemDetail(Transform statRoot)
    {
        Transform itemDetail = Descendant(statRoot, "ItemDetail"); // 사용자가 만든 실제 비교 화면
        Transform currentRoot = itemDetail == null ? null : DirectChild(itemDetail, "Popup_1"); // 현재 장착 장비 영역
        Transform newRoot = itemDetail == null ? null : DirectChild(itemDetail, "Popup_2"); // 새로 뽑은 장비 영역
        if (currentRoot == null || newRoot == null) return null;
        BodyEquipmentInfoView currentView = ConnectDesignedInfoView(currentRoot, false); // 현재 장비는 비교 아이콘을 사용하지 않는다
        BodyEquipmentInfoView newView = ConnectDesignedInfoView(newRoot, true); // 새 장비만 증감 아이콘을 표시한다
        Transform popupLayer = Descendant(statRoot, "BodyEquipmentPopupLayer"); // UpgradeMenuController 안의 기존 모달 레이어
        Transform host = popupLayer == null ? null : DirectChild(popupLayer, "EquipmentComparePopup"); // ItemDetail을 포함하는 바깥 모달 Root
        if (host == null) host = itemDetail;
        Transform designedButtons = DirectChild(newRoot, "Group_Buttons"); // 사용자가 만든 새 장비 버튼 자리
        MoveExistingButtons(host, designedButtons); // 이전 최소 팝업 버튼을 새 디자인 위치에서 재사용한다
        Button equip = FindButton(itemDetail, "EquipButton"); // 새 장비 장착 버튼
        Button dismantle = FindButton(itemDetail, "DismantleButton"); // 새 장비 파괴 버튼
        Button close = FindButton(itemDetail, "CloseButton"); // 새 장비 보관 및 닫기 버튼
        Transform comparePanel = DirectChild(host, "ComparePanel"); // 기존 확인 UI를 보유한 패널
        Transform confirmation = comparePanel == null ? null : DirectChild(comparePanel, "DismantleConfirmation"); // 기존 파기 확인 영역
        TMP_Text confirmationText = confirmation == null ? null : ComponentAt<TMP_Text>(confirmation, "ConfirmationText"); // 파기 대상 안내
        Button confirmDismantle = confirmation == null ? null : FindButton(confirmation, "ConfirmButton"); // 기존 파기 확인 버튼
        Button cancelDismantle = confirmation == null ? null : FindButton(confirmation, "CancelButton"); // 기존 파기 취소 버튼
        BodyEquipmentComparePopup unused = itemDetail.GetComponent<BodyEquipmentComparePopup>(); // 이전 연결 시 ItemDetail에 추가된 중복 Controller
        if (unused != null && host != itemDetail) UnityEngine.Object.DestroyImmediate(unused, true);
        BodyEquipmentComparePopup popup = host.GetComponent<BodyEquipmentComparePopup>() ?? host.gameObject.AddComponent<BodyEquipmentComparePopup>(); // 기존 바깥 팝업 Root의 Controller
        SerializedObject fields = new(popup); // ItemDetail 내부 참조 연결
        Set(fields, "currentView", currentView);
        Set(fields, "newView", newView);
        Set(fields, "differenceText", null);
        Set(fields, "messageText", null);
        Set(fields, "equipButton", equip);
        Set(fields, "dismantleButton", dismantle);
        Set(fields, "closeButton", close);
        Set(fields, "confirmationRoot", confirmation == null ? null : confirmation.gameObject);
        Set(fields, "confirmationText", confirmationText);
        Set(fields, "confirmDismantleButton", confirmDismantle);
        Set(fields, "cancelDismantleButton", cancelDismantle);
        fields.ApplyModifiedPropertiesWithoutUndo();
        itemDetail.gameObject.SetActive(true);
        host.gameObject.SetActive(false);
        return popup;
    }

    // 기존 최소 팝업의 버튼을 ItemDetail의 새 장비 버튼 영역으로 옮겨 중복 생성을 막는다.
    private static void MoveExistingButtons(Transform host, Transform target)
    {
        if (host == null || target == null || FindButton(target, "EquipButton") != null) return;
        Transform panel = DirectChild(host, "ComparePanel"); // 이전 최소 비교 패널
        Transform actions = panel == null ? null : DirectChild(panel, "Actions"); // 기존 장착/파기/닫기 버튼 부모
        if (actions == null) return;
        Transform[] buttons = new Transform[actions.childCount]; // 부모 변경 전에 보관할 기존 버튼
        for (int index = 0; index < buttons.Length; index++) buttons[index] = actions.GetChild(index);
        for (int index = 0; index < buttons.Length; index++) buttons[index].SetParent(target, false);
        actions.gameObject.SetActive(false);
    }

    // Popup 내부의 기존 이름, 아이콘, 레어도와 고정 Stat Object를 공통 View에 연결한다.
    private static BodyEquipmentInfoView ConnectDesignedInfoView(Transform popupRoot, bool comparisonSide)
    {
        Transform itemSlot = DirectChild(popupRoot, "ItemSlot"); // 기존 장비 아이콘 영역
        Transform groupOne = DirectChild(popupRoot, "Group_1"); // 기존 이름과 레어도 영역
        Transform statGroup = DirectChild(popupRoot, "Group_3"); // 고정 Stat Object 영역
        Image icon = ComponentAt<Image>(itemSlot, "Icon"); // 장비 Definition 아이콘
        Image rarityFrame = ComponentAt<Image>(popupRoot, "PopupBackGlow"); // 기존 레어도 강조 배경
        TMP_Text equipmentName = ComponentAt<TMP_Text>(groupOne, "Text_ItemName"); // 기존 장비 이름 Text
        TMP_Text rarity = ComponentAt<TMP_Text>(groupOne, "Text_Grade"); // 기존 레어도 Text
        BodyEquipmentStatRowView[] rows = ConnectDesignedStatRows(statGroup, comparisonSide); // 기존 Attack/Defense/HP/Critical 행
        BodyEquipmentInfoView view = popupRoot.GetComponent<BodyEquipmentInfoView>() ?? popupRoot.gameObject.AddComponent<BodyEquipmentInfoView>(); // Popup 한쪽의 공통 정보 View
        SerializedObject fields = new(view); // 기존 디자인 참조만 연결한다
        Set(fields, "equipmentIcon", icon);
        Set(fields, "slotIcon", null);
        Set(fields, "rarityFrame", rarityFrame);
        SerializedProperty applySprite = fields.FindProperty("applyRarityFrameSprite"); // 기존 Glow Sprite 보존 설정
        if (applySprite != null) applySprite.boolValue = false;
        Set(fields, "powerValueText", null);
        Set(fields, "equipmentNameText", equipmentName);
        Set(fields, "rarityText", rarity);
        Set(fields, "mainStatText", null);
        Set(fields, "subStatsText", null);
        Set(fields, "emptyText", null);
        SerializedProperty rowProperty = fields.FindProperty("statRows"); // 재사용할 고정 스탯 행 배열
        rowProperty.arraySize = rows.Length;
        for (int index = 0; index < rows.Length; index++) rowProperty.GetArrayElementAtIndex(index).objectReferenceValue = rows[index];
        fields.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // 디자인에 존재하고 실제 장비 시스템이 지원하는 고정 Stat Object만 연결한다.
    private static BodyEquipmentStatRowView[] ConnectDesignedStatRows(Transform group, bool comparisonSide)
    {
        if (group == null) return Array.Empty<BodyEquipmentStatRowView>();
        (string name, EquipmentStatType type)[] mappings = // 디자인 이름과 실제 Stat enum 대응
        {
            ("Attack", EquipmentStatType.Attack), ("Defense", EquipmentStatType.Defense),
            ("HP", EquipmentStatType.Health), ("Critical", EquipmentStatType.CriticalChance)
        };
        BodyEquipmentStatRowView[] rows = new BodyEquipmentStatRowView[mappings.Length]; // Instantiate 없이 재사용할 네 개 행
        for (int index = 0; index < mappings.Length; index++)
        {
            Transform rowRoot = DirectChild(group, mappings[index].name); // 기존 디자인의 Stat Object
            if (rowRoot == null) continue;
            TMP_Text nameText = ComponentAt<TMP_Text>(rowRoot, "StatNameText_1") ?? ComponentAt<TMP_Text>(rowRoot, "Text_1"); // 기존 StatName Text
            TMP_Text valueText = ComponentAt<TMP_Text>(rowRoot, "StatValueText_2") ?? ComponentAt<TMP_Text>(rowRoot, "Text_2"); // 기존 StatValue Text
            GameObject up = DirectChild(rowRoot, "UpIcon")?.gameObject; // 상승 아이콘
            GameObject down = DirectChild(rowRoot, "DownIcon")?.gameObject; // 하락 아이콘
            BodyEquipmentStatRowView row = rowRoot.GetComponent<BodyEquipmentStatRowView>() ?? rowRoot.gameObject.AddComponent<BodyEquipmentStatRowView>(); // 고정 행 View
            SerializedObject fields = new(row); // Stat 종류와 기존 UI 참조 연결
            fields.FindProperty("statType").enumValueIndex = (int)mappings[index].type;
            Set(fields, "statNameText", nameText);
            Set(fields, "statValueText", valueText);
            Set(fields, "upIcon", comparisonSide ? up : null);
            Set(fields, "downIcon", comparisonSide ? down : null);
            fields.ApplyModifiedPropertiesWithoutUndo();
            if (!comparisonSide) { if (up != null) up.SetActive(false); if (down != null) down.SetActive(false); }
            rows[index] = row;
        }
        Transform unsupportedDodge = DirectChild(group, "Dodge"); // 현재 장비 Stat enum에 없는 디자인 전용 행
        if (unsupportedDodge != null) unsupportedDodge.gameObject.SetActive(false);
        return rows;
    }

    // 기존 슬롯의 Item 영역을 클릭 대상으로 쓰고 Empty 아이콘을 유지한다.
    private static BodyEquipmentSlotView ConnectSlot(Transform slotRoot, BodyEquipmentSlot slot)
    {
        Transform item = DirectChild(slotRoot, "Item"); // 기존 슬롯의 비어 있지 않은 장비 영역
        Transform empty = DirectChild(slotRoot, "Icon_Empty"); // 유저가 만든 빈 슬롯 아이콘
        if (item == null) throw new InvalidOperationException($"{slotRoot.name}/Item이 없습니다.");
        Transform iconRoot = EnsureRect(item, "EquippedEquipmentIcon"); // 기존 Item 위의 실제 장비 아이콘
        Stretch(iconRoot.GetComponent<RectTransform>());
        Image icon = EnsureImage(iconRoot, new Color(1f, 1f, 1f, 1f)); // 장비 원형 Sprite 표시
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Transform frameRoot = EnsureRect(item, "EquippedRarityFrame"); // 기존 Item 위의 레어도 띠
        RectTransform frameRect = frameRoot.GetComponent<RectTransform>(); // 레어도 띠의 프리팹 레이아웃
        frameRect.anchorMin = new Vector2(0f, 0.91f);
        frameRect.anchorMax = Vector2.one;
        frameRect.anchoredPosition = Vector2.zero;
        frameRect.sizeDelta = Vector2.zero;
        Image frame = EnsureImage(frameRoot, Color.white); // Rarity Definition에서 색과 Sprite를 받는다
        frame.raycastTarget = false;
        Transform target = EnsureRect(item, "EquipmentClickTarget"); // 기존 디자인 전체를 누르는 투명 영역
        Stretch(target.GetComponent<RectTransform>());
        Image hitImage = EnsureImage(target, Color.clear); // uGUI raycast를 받는 투명 그래픽
        hitImage.raycastTarget = true;
        Button button = target.GetComponent<Button>() ?? target.gameObject.AddComponent<Button>(); // 슬롯 상세 창 버튼
        button.targetGraphic = hitImage;
        target.SetAsLastSibling();
        BodyEquipmentSlotView view = slotRoot.GetComponent<BodyEquipmentSlotView>() ?? slotRoot.gameObject.AddComponent<BodyEquipmentSlotView>(); // 기존 슬롯을 보존하는 View
        SerializedObject fields = new(view); // 슬롯별 의미 있는 참조 연결
        fields.FindProperty("slotType").enumValueIndex = (int)slot;
        Set(fields, "equipmentIcon", icon);
        Set(fields, "rarityFrame", frame);
        Set(fields, "emptyObject", empty == null ? null : empty.gameObject);
        Set(fields, "slotButton", button);
        fields.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // 장비 하나의 상세 모달을 기존 메뉴 루트 내부에 한 번만 구성한다.
    private static BodyEquipmentDetailPopup EnsureDetailPopup(Transform layer)
    {
        Transform root = EnsureModal(layer, "EquipmentDetailPopup"); // 상세 팝업 루트
        Transform panel = EnsurePanel(root, "DetailPanel"); // 사용자 꾸미기 기준이 되는 상세 패널
        EnsureText(panel, "Title", "장착 장비", 27f);
        BodyEquipmentInfoView info = EnsureInfoView(panel, "EquipmentInfo"); // 실제 옵션 표시 View
        TMP_Text state = EnsureText(panel, "StateText", "장착 중", 20f); // 장착 상태 표시
        Button close = EnsureButton(panel, "CloseButton", "닫기"); // 상세 창 닫기
        BodyEquipmentDetailPopup popup = root.GetComponent<BodyEquipmentDetailPopup>() ?? root.gameObject.AddComponent<BodyEquipmentDetailPopup>(); // 상세 버튼 Controller
        SerializedObject fields = new(popup); // 상세 View 참조 연결
        Set(fields, "equipmentView", info);
        Set(fields, "stateText", state);
        Set(fields, "closeButton", close);
        fields.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.SetActive(false);
        return popup;
    }

    // 현재 장비와 새 장비를 나란히 표시하는 메뉴 내부 모달을 구성한다.
    private static BodyEquipmentComparePopup EnsureComparePopup(Transform layer)
    {
        Transform root = EnsureModal(layer, "EquipmentComparePopup"); // 비교 팝업 루트
        Transform panel = EnsurePanel(root, "ComparePanel"); // 기존 프리팹 내부의 비교 패널
        EnsureText(panel, "Title", "현재 장비  VS  새 장비", 27f);
        Transform columns = EnsureRect(panel, "EquipmentColumns"); // 좌우 장비 정보 열
        HorizontalLayoutGroup horizontal = columns.GetComponent<HorizontalLayoutGroup>() ?? columns.gameObject.AddComponent<HorizontalLayoutGroup>(); // 좌우 비교 배치
        horizontal.spacing = 10f;
        horizontal.childControlWidth = true;
        horizontal.childForceExpandWidth = true;
        LayoutElement columnsLayout = columns.GetComponent<LayoutElement>() ?? columns.gameObject.AddComponent<LayoutElement>(); // 비교 열의 가변 공간
        columnsLayout.flexibleHeight = 1f;
        BodyEquipmentInfoView current = EnsureInfoView(columns, "CurrentEquipment"); // 현재 장착 장비
        BodyEquipmentInfoView next = EnsureInfoView(columns, "NewEquipment"); // 새로 얻은 장비
        TMP_Text difference = EnsureText(panel, "DifferenceText", string.Empty, 18f); // 동일 주 스탯 차이
        TMP_Text message = EnsureText(panel, "MessageText", string.Empty, 17f); // 실패 안내
        Transform actions = EnsureRect(panel, "Actions"); // 새 장비 동작 버튼 그룹
        HorizontalLayoutGroup actionLayout = actions.GetComponent<HorizontalLayoutGroup>() ?? actions.gameObject.AddComponent<HorizontalLayoutGroup>(); // 동작 버튼 가로 배치
        actionLayout.spacing = 8f;
        actionLayout.childForceExpandWidth = true;
        Button equip = EnsureButton(actions, "EquipButton", "장착"); // 실제 매니저 장착
        Button dismantle = EnsureButton(actions, "DismantleButton", "파기"); // 파기 확인 열기
        Button close = EnsureButton(actions, "CloseButton", "보관 / 닫기"); // Inventory에 남기기
        Transform confirmation = EnsureRect(panel, "DismantleConfirmation"); // 되돌릴 수 없는 파기 확인
        VerticalLayoutGroup confirmLayout = confirmation.GetComponent<VerticalLayoutGroup>() ?? confirmation.gameObject.AddComponent<VerticalLayoutGroup>(); // 확인 내용 세로 배치
        confirmLayout.spacing = 6f;
        TMP_Text confirmText = EnsureText(confirmation, "ConfirmationText", string.Empty, 18f); // 장비명과 포인트
        Button confirm = EnsureButton(confirmation, "ConfirmButton", "파기 확인"); // 최종 파기 버튼
        Button cancel = EnsureButton(confirmation, "CancelButton", "취소"); // 파기 취소 버튼
        confirmation.gameObject.SetActive(false);
        BodyEquipmentComparePopup popup = root.GetComponent<BodyEquipmentComparePopup>() ?? root.gameObject.AddComponent<BodyEquipmentComparePopup>(); // 비교 동작 Controller
        SerializedObject fields = new(popup); // 비교 View와 버튼 참조 연결
        Set(fields, "currentView", current);
        Set(fields, "newView", next);
        Set(fields, "differenceText", difference);
        Set(fields, "messageText", message);
        Set(fields, "equipButton", equip);
        Set(fields, "dismantleButton", dismantle);
        Set(fields, "closeButton", close);
        Set(fields, "confirmationRoot", confirmation.gameObject);
        Set(fields, "confirmationText", confirmText);
        Set(fields, "confirmDismantleButton", confirm);
        Set(fields, "cancelDismantleButton", cancel);
        fields.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.SetActive(false);
        return popup;
    }

    // 기존 결과 카드의 장착 버튼 문구를 비교/장착 상태에 맞춰 바꿀 수 있게 한다.
    private static void ConnectResultCardLabel()
    {
        string path = "Assets/03. Prefabs/UI/Upgrade/BodyEquipment/BodyEquipmentItemView.prefab"; // 기존 결과와 전체 보관함 카드
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 이미 만들어진 카드 원본
        if (prefab == null) return;
        GameObject card = PrefabUtility.LoadPrefabContents(path); // 기존 카드 편집 인스턴스
        try
        {
            BodyEquipmentItemView view = card.GetComponent<BodyEquipmentItemView>(); // 실제 장비 카드 View
            Transform equip = DirectChild(card.transform, "Actions"); // 카드 하단 버튼 그룹
            Transform button = equip == null ? null : DirectChild(equip, "EquipButton"); // 기존 장착 버튼
            TMP_Text label = button == null ? null : button.GetComponentInChildren<TMP_Text>(true); // 기존 버튼의 TMP 문구
            if (view == null || label == null) return;
            SerializedObject fields = new(view); // 결과 선택 버튼 문구 참조
            Set(fields, "equipButtonText", label);
            fields.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(card, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(card); }
    }

    // 장비 정보 View를 한 번 생성하고 반복 실행 시 기존 참조를 재사용한다.
    private static BodyEquipmentInfoView EnsureInfoView(Transform parent, string name)
    {
        Transform root = EnsureRect(parent, name); // 장비 하나의 상세 UI 영역
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>() ?? root.gameObject.AddComponent<VerticalLayoutGroup>(); // 옵션 개수에 따른 세로 배치
        layout.spacing = 4f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        LayoutElement rootLayout = root.GetComponent<LayoutElement>() ?? root.gameObject.AddComponent<LayoutElement>(); // 비교 열의 최소 영역
        rootLayout.flexibleWidth = 1f;
        rootLayout.flexibleHeight = 1f;
        Image frame = EnsureImage(EnsureRect(root, "RarityFrame"), Color.white); // 레어도 정의의 Sprite와 색
        frame.raycastTarget = false;
        LayoutElement frameLayout = frame.GetComponent<LayoutElement>() ?? frame.gameObject.AddComponent<LayoutElement>(); // 프레임 띠 높이
        frameLayout.preferredHeight = 8f;
        Image icon = EnsureImage(EnsureRect(root, "EquipmentIcon"), Color.white); // 장비 정의의 Icon Sprite
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        LayoutElement iconLayout = icon.GetComponent<LayoutElement>() ?? icon.gameObject.AddComponent<LayoutElement>(); // 장비 아이콘 높이
        iconLayout.preferredHeight = 72f;
        TMP_Text rarity = EnsureText(root, "RarityText", string.Empty, 18f); // 레어도 한국어 이름
        TMP_Text equipmentName = EnsureText(root, "EquipmentNameText", string.Empty, 20f); // 장비명과 부위
        TMP_Text main = EnsureText(root, "MainStatText", string.Empty, 19f); // 주 스탯
        TMP_Text sub = EnsureText(root, "SubStatsText", string.Empty, 16f); // 실제 보조 스탯 줄 목록
        TMP_Text empty = EnsureText(root, "EmptyText", "장착된 장비 없음", 17f); // 현재 장비가 없는 경우
        BodyEquipmentInfoView view = root.GetComponent<BodyEquipmentInfoView>() ?? root.gameObject.AddComponent<BodyEquipmentInfoView>(); // 공통 장비 정보 View
        SerializedObject fields = new(view); // 공통 UI 참조 연결
        Set(fields, "equipmentIcon", icon);
        Set(fields, "rarityFrame", frame);
        Set(fields, "equipmentNameText", equipmentName);
        Set(fields, "rarityText", rarity);
        Set(fields, "mainStatText", main);
        Set(fields, "subStatsText", sub);
        Set(fields, "emptyText", empty);
        fields.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // 화면 전체를 쓰되 별도 Canvas를 만들지 않는 기존 메뉴 내부 팝업 루트다.
    private static Transform EnsureModal(Transform parent, string name)
    {
        Transform root = EnsureRect(parent, name); // 한 번만 존재할 모달 오브젝트
        Stretch(root.GetComponent<RectTransform>());
        Image background = EnsureImage(root, new Color(0f, 0f, 0f, 0.78f)); // 배경 입력 차단과 구분
        background.raycastTarget = true;
        root.SetAsLastSibling();
        return root;
    }

    // 사용자 디자인을 나중에 바꾸기 쉬운 단순 패널을 구성한다.
    private static Transform EnsurePanel(Transform parent, string name)
    {
        Transform panel = EnsureRect(parent, name); // 실제 팝업 콘텐츠 영역
        RectTransform rect = panel.GetComponent<RectTransform>(); // 패널의 프리팹 기준 배치
        rect.anchorMin = new Vector2(0.06f, 0.15f);
        rect.anchorMax = new Vector2(0.94f, 0.85f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        EnsureImage(panel, new Color(0.12f, 0.15f, 0.18f, 1f));
        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>() ?? panel.gameObject.AddComponent<VerticalLayoutGroup>(); // 팝업 내용 세로 배치
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        return panel;
    }

    // 기존 TMP Font Asset을 재사용하는 짧은 안내 Text를 만든다.
    private static TMP_Text EnsureText(Transform parent, string name, string content, float size)
    {
        Transform root = EnsureRect(parent, name); // Text 오브젝트
        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>() ?? root.gameObject.AddComponent<TextMeshProUGUI>(); // uGUI TMP 텍스트
        text.font = _font;
        text.fontSize = size;
        text.text = content;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    // 기존 TMP 서체와 단순 uGUI Image로 동작 버튼을 만든다.
    private static Button EnsureButton(Transform parent, string name, string label)
    {
        Transform root = EnsureRect(parent, name); // 버튼 오브젝트
        Image image = EnsureImage(root, new Color(0.09f, 0.57f, 0.64f, 1f)); // 최소 버튼 배경
        Button button = root.GetComponent<Button>() ?? root.gameObject.AddComponent<Button>(); // 누르기 동작
        button.targetGraphic = image;
        LayoutElement layout = root.GetComponent<LayoutElement>() ?? root.gameObject.AddComponent<LayoutElement>(); // 클릭 가능한 최소 높이
        layout.preferredHeight = 48f;
        TMP_Text text = EnsureText(root, "Label", label, 18f); // 버튼의 한국어 문구
        Stretch(text.rectTransform);
        return button;
    }

    // SerializedField가 있을 때만 같은 프리팹 내부 참조를 지정한다.
    private static void Set(SerializedObject fields, string name, UnityEngine.Object value)
    {
        SerializedProperty property = fields.FindProperty(name); // 연결할 Inspector 필드
        if (property != null) property.objectReferenceValue = value;
    }

    // 새 부모 아래의 직접 자식만 확인해 중복 오브젝트를 방지한다.
    private static Transform DirectChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int index = 0; index < parent.childCount; index++)
            if (parent.GetChild(index).name == name) return parent.GetChild(index);
        return null;
    }

    // Editor 연결 시에만 전체 자식을 재귀 순회해 사용자가 만든 이름의 오브젝트를 찾는다.
    private static Transform Descendant(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform result = Descendant(parent.GetChild(index), name); // 현재 자식 아래의 검색 결과
            if (result != null) return result;
        }
        return null;
    }

    // 현재 Transform부터 위로 올라가 지정 이름의 모달 Root를 찾는다.
    private static Transform Ancestor(Transform child, string name)
    {
        Transform current = child; // 검사할 현재 부모 단계
        while (current != null)
        {
            if (current.name == name) return current;
            current = current.parent;
        }
        return null;
    }

    // 지정 이름의 자식에서 필요한 UI 컴포넌트를 가져온다.
    private static T ComponentAt<T>(Transform parent, string name) where T : Component
    {
        Transform child = DirectChild(parent, name); // 현재 디자인의 직접 자식
        return child == null ? null : child.GetComponent<T>();
    }

    // ItemDetail 내부에서 이름이 일치하는 기존 버튼만 찾는다.
    private static Button FindButton(Transform parent, string name)
    {
        Transform target = Descendant(parent, name); // 중첩 Prefab을 포함한 버튼 오브젝트
        return target == null ? null : target.GetComponent<Button>();
    }

    // 프리팹 안에 자식 RectTransform이 없을 때만 추가한다.
    private static Transform EnsureRect(Transform parent, string name)
    {
        Transform existing = DirectChild(parent, name); // 반복 실행 시 보존할 유저 수정 UI
        if (existing != null) return existing;
        GameObject created = new(name, typeof(RectTransform)); // uGUI 내부 오브젝트
        created.layer = 5;
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    // Image가 없을 때만 붙여 기존 Style 참조를 보존한다.
    private static Image EnsureImage(Transform root, Color color)
    {
        Image image = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>(); // uGUI 화면 그래픽
        if (image.sprite == null) image.color = color;
        return image;
    }

    // 현재 부모 Rect에 맞추는 Anchors를 프리팹 자식에 설정한다.
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }
}
#endif
