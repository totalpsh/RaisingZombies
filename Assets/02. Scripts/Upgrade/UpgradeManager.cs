using System;
using System.Collections.Generic;
using UnityEngine;

//가챠, 연구, 저장과 최종 스탯 조회를 담당하는 독립 서비스입니다.
public sealed class UpgradeManager : MonoBehaviour
{
        private const string SaveKey = "RaisingZombies.Upgrade.State";
        [SerializeField] private UpgradeBalanceSettings balanceSettings; // 전체 업그레이드 밸런스 에셋
        [SerializeField, Min(0)] private int startingCurrency = 10000; // 최초 저장 파일 생성 시 지급할 테스트 재화
        private UpgradeState state;
        public event Action StateChanged;
        public event Action<IReadOnlyList<GachaDrawResult>> DrawCompleted;
        public int Currency => state == null ? 0 : state.currency;
        public int GachaLevel => state == null ? 1 : state.gachaLevel;
        public int DrawsAtCurrentLevel => state == null ? 0 : state.drawsAtCurrentLevel;
        public UpgradeBalanceSettings BalanceSettings => balanceSettings;

        private void Awake() { LoadState(); }
        private void OnValidate() { if (startingCurrency < 0) startingCurrency = 0; }
        public void AddCurrency(int amount) { if (amount <= 0) return; state.currency += amount; SaveAndNotify(); }
        [ContextMenu("테스트 재화 지급")]
        private void GrantTestCurrency() { AddCurrency(startingCurrency); }
        [ContextMenu("업그레이드 저장 초기화")]
        private void ResetSavedState() { PlayerPrefs.DeleteKey(SaveKey); state = CreateInitialState(); SaveAndNotify(); }

        //현재 가챠 풀에서 1회 뽑고 결과를 영구 누적합니다.
        public bool TryDrawOne(out GachaDrawResult result)
        {
            result = default;
            if (!CanUseBalance() || state.currency < GetCurrentDrawCost()) return false;
            result = ExecuteOneDraw(); SaveAndNotify(); DrawCompleted?.Invoke(new[] { result }); return true;
        }
        //레벨 상승과 비용 변경까지 사전 계산한 뒤 10회를 순차 실행합니다.
        public bool TryDrawTen(out IReadOnlyList<GachaDrawResult> results)
        {
            results = null;
            if (!CanUseBalance() || state.currency < GetDrawCostForCount(10)) return false;
            var values = new List<GachaDrawResult>(10);
            for (var i = 0; i < 10; i++) values.Add(ExecuteOneDraw());
            SaveAndNotify(); results = values; DrawCompleted?.Invoke(values); return true;
        }
        //원본 뽑기 수치는 유지한 채 해당 스탯의 연구 레벨만 올립니다.
        public bool TryUpgradeResearch(UpgradeStatType type)
        {
            var definition = balanceSettings == null ? null : balanceSettings.GetStat(type);
            if (definition == null || !IsUnlocked(type)) return false;
            var value = GetValue(type); var cost = GetResearchCost(type);
            if (state.currency < cost) return false;
            state.currency -= cost; value.researchLevel++; SaveAndNotify(); return true;
        }
        public int GetCurrentDrawCost() { var level = balanceSettings == null ? null : balanceSettings.GetGachaLevel(state.gachaLevel); return level == null ? 0 : level.drawCost; }
        public int GetDrawCostForCount(int count)
        {
            if (count <= 0 || !CanUseBalance()) return 0;
            var level = state.gachaLevel; var progress = state.drawsAtCurrentLevel; var total = 0;
            for (var i = 0; i < count; i++) { var definition = balanceSettings.GetGachaLevel(level); if (definition == null) return total; total += definition.drawCost; progress++; if (definition.drawsToNextLevel > 0 && progress >= definition.drawsToNextLevel && balanceSettings.GetGachaLevel(level + 1) != null) { level++; progress = 0; } }
            return total;
        }
        public int GetResearchCost(UpgradeStatType type) { var definition = balanceSettings == null ? null : balanceSettings.GetStat(type); return definition == null ? 0 : Mathf.CeilToInt(definition.researchBaseCost * Mathf.Pow(definition.researchCostGrowth, GetValue(type).researchLevel)); }
        public bool IsUnlocked(UpgradeStatType type) { return CanUseBalance() && GetUnlockedStats().Contains(type); }
        public List<UpgradeStatType> GetUnlockedStats()
        {
            var result = new List<UpgradeStatType>(); if (!CanUseBalance()) return result;
            for (var level = 1; level <= state.gachaLevel; level++) { var definition = balanceSettings.GetGachaLevel(level); if (definition?.newlyUnlockedStats == null) continue; foreach (var stat in definition.newlyUnlockedStats) if (!result.Contains(stat)) result.Add(stat); }
            return result;
        }
        // 전투 등 외부 시스템이 최종 보너스를 읽는 API입니다.
        public UpgradeStatSnapshot GetStatSnapshot(UpgradeStatType type)
        {
            var definition = balanceSettings == null ? null : balanceSettings.GetStat(type); var value = GetValue(type);
            if (definition == null) return new UpgradeStatSnapshot(type, value.accumulatedValue, value.accumulatedValue, value.researchLevel, 0f, 0f);
            var efficiency = definition.baseCoefficient * GetResearchMultiplier(definition, value.researchLevel);
            float effective = value.accumulatedValue;
            if (type != UpgradeStatType.StatIncrease) effective *= 1f + GetStatIncreaseEffect();
            return new UpgradeStatSnapshot(type, value.accumulatedValue, effective, value.researchLevel, efficiency, effective * efficiency);
        }
        private GachaDrawResult ExecuteOneDraw()
        {
            var cost = GetCurrentDrawCost(); state.currency -= cost;
            var pool = GetUnlockedStats(); var type = pool[UnityEngine.Random.Range(0, pool.Count)]; var amount = UnityEngine.Random.Range(1, 11); var value = GetValue(type); value.accumulatedValue += amount;
            var increased = AdvanceGachaLevel(); return new GachaDrawResult(type, amount, value.accumulatedValue, increased, state.gachaLevel);
        }
        private bool AdvanceGachaLevel() { var definition = balanceSettings.GetGachaLevel(state.gachaLevel); if (definition == null || definition.drawsToNextLevel <= 0) return false; state.drawsAtCurrentLevel++; if (state.drawsAtCurrentLevel < definition.drawsToNextLevel || balanceSettings.GetGachaLevel(state.gachaLevel + 1) == null) return false; state.gachaLevel++; state.drawsAtCurrentLevel = 0; return true; }
        private float GetStatIncreaseEffect() { var increase = GetStatSnapshotWithoutAmplifier(UpgradeStatType.StatIncrease); return increase.FinalBonus; }
        private UpgradeStatSnapshot GetStatSnapshotWithoutAmplifier(UpgradeStatType type) { var definition = balanceSettings.GetStat(type); var value = GetValue(type); var efficiency = definition.baseCoefficient * GetResearchMultiplier(definition, value.researchLevel); return new UpgradeStatSnapshot(type, value.accumulatedValue, value.accumulatedValue, value.researchLevel, efficiency, value.accumulatedValue * efficiency); }
        private float GetResearchMultiplier(UpgradeStatDefinition definition, int level) { return 1f + definition.researchMaxMultiplierBonus * (1f - Mathf.Exp(-definition.researchCurveRate * level)); }
        private UpgradeStatValue GetValue(UpgradeStatType type) { foreach (var item in state.stats) if (item.statType == type) return item; var added = new UpgradeStatValue { statType = type }; state.stats.Add(added); return added; }
        private bool CanUseBalance() { return balanceSettings != null && balanceSettings.GetGachaLevel(1) != null; }
        private void LoadState() { state = PlayerPrefs.HasKey(SaveKey) ? JsonUtility.FromJson<UpgradeState>(PlayerPrefs.GetString(SaveKey)) : CreateInitialState(); if (state == null) state = CreateInitialState(); if (state.stats == null) state.stats = new List<UpgradeStatValue>(); SaveAndNotify(); }
        private UpgradeState CreateInitialState() { return new UpgradeState { currency = startingCurrency }; }
        private void SaveAndNotify() { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(state)); PlayerPrefs.Save(); StateChanged?.Invoke(); }
}
