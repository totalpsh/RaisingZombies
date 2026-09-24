using UnityEngine;

// 도전 던전 한 종류의 클리어 재화 보상을 Inspector 데이터로 보관한다.
[CreateAssetMenu(fileName = "ChallengeDungeonReward", menuName = "Raising Zombies/Challenge/Dungeon Currency Reward")]
public sealed class ChallengeDungeonRewardSO : ScriptableObject
{
    [SerializeField] private GameCurrencyType currencyType = GameCurrencyType.BodyDrawTicket; // 클리어 시 지급할 기존 지갑 재화 종류
    [SerializeField, Min(0)] private long amount; // 클리어 한 번에 지급할 재화 수량
    [SerializeField] private Sprite icon; // 도전 보상 UI에 표시할 Inspector 지정 아이콘

    public GameCurrencyType CurrencyType => currencyType; // UI와 클리어 코드가 공유할 재화 종류
    public long Amount => amount < 0L ? 0L : amount; // 음수가 아닌 보상 수량
    public Sprite Icon => icon; // 보상 UI에 사용할 이미지

    // 실제 던전 클리어가 확정된 뒤 기존 지갑 경로로 보상을 한 번 지급한다.
    public bool TryGrantClearReward(CurrencyWalletManager wallet)
    {
        if (wallet == null || Amount <= 0L || !wallet.AddCurrency(currencyType, Amount)) return false;
        if (!SaveManager.EnsureInstance().SaveGame())
            Debug.LogWarning("[ChallengeDungeonReward] 지급된 보상을 즉시 저장하지 못했습니다. 자동 저장에서 재시도합니다.", this);
        return true;
    }
}
