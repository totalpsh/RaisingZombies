using UnityEngine;
using UnityEngine.UI;

// 기존 레어도 정의의 세 색을 장비 UI의 배경, 안쪽 테두리와 글로우에 함께 적용한다.
public sealed class BodyRarityVisual : MonoBehaviour
{
    [SerializeField] private Image background; // 레어도별 배경색을 표시한다.
    [SerializeField] private Image innerBorder; // 레어도별 안쪽 테두리색을 표시한다.
    [SerializeField] private Image glow; // 레어도별 글로우색을 표시한다.

    // 장비 원본의 레어도 정의 하나로 세 이미지를 한 번에 갱신한다.
    public void SetRarity(BodyRarityDefinitionSO rarity)
    {
        bool visible = rarity != null; // 유효한 레어도가 있는지 확인한다.
        Apply(background, visible, visible ? rarity.BackgroundColor : Color.clear);
        Apply(innerBorder, visible, visible ? rarity.InnerBorderColor : Color.clear);
        Apply(glow, visible, visible ? rarity.GlowColor : Color.clear);
    }

    // 저장된 레어도 단계로 기존 장비 데이터베이스의 정의를 찾아 적용한다.
    public bool SetRarity(int rarityTier, BodyEquipmentDatabaseSO database)
    {
        BodyRarityDefinitionSO rarity = null; // 조회한 기존 레어도 정의
        bool found = database != null && database.TryGetRarity(rarityTier, out rarity); // 기존 레어도 정의의 조회 결과
        SetRarity(found ? rarity : null);
        return found;
    }

    // 이미지의 활성 상태와 색을 모두 덮어써 재사용 셀의 이전 색이 남지 않게 한다.
    private static void Apply(Image image, bool visible, Color color)
    {
        if (image == null) return;
        image.color = color;
        image.enabled = visible;
    }

#if UNITY_EDITOR
    // Prefab의 세 로컬 이미지 참조가 빠졌다면 Inspector 작업 중 경고한다.
    private void OnValidate()
    {
        if (background == null || innerBorder == null || glow == null)
            Debug.LogWarning("[BodyRarityVisual] Background, Inner Border, Glow Image를 모두 연결하세요.", this);
    }
#endif
}
